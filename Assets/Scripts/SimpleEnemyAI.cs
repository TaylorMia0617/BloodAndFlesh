using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CharacterStats))]
public class SimpleEnemyAI : MonoBehaviour, ILocalFreezable
{
    private enum CombatState
    {
        Chase,
        Recover
    }

    [SerializeField] private EnemyArchetype archetype;
    [SerializeField] private Transform target;
    [SerializeField] private float visionRange = 7f;
    [SerializeField] private float stopDistance = 0.25f;
    [SerializeField] private float accelerationTime = 0.35f;
    [SerializeField] private float knockbackDuration = 0.28f;
    [SerializeField] private float knockbackDistance = 2f;
    [SerializeField] private float meleeAttackRange = 0.72f;
    [SerializeField] private float meleeAttackCooldown = 1.4f;
    [SerializeField] private float meleePlayerKnockbackDistance = 0.65f;
    [SerializeField] private float meleePlayerKnockbackDuration = 0.18f;
    [SerializeField] private float rangedPlayerKnockbackDistance = 0.35f;
    [SerializeField] private float rangedPlayerKnockbackDuration = 0.12f;
    [SerializeField] private float meleeRecoverDistance = 2.3f;
    [SerializeField] private float rangedPreferredDistance = 3.6f;
    [SerializeField] private float rangedAttackCooldown = 1.8f;
    [SerializeField] private float rangedProjectileSpeed = 7f;
    [SerializeField] private float rangedProjectileDistance = 6f;
    [SerializeField] private float sentinelSignalRadius = 15f;
    [SerializeField] private float sentinelFleeDuration = 1.8f;
    [SerializeField] private float visionRayStep = 0.08f;
    [SerializeField] private float hearingRange = 6f;
    [SerializeField] private float hearingMemorySeconds = 2f;
    [SerializeField] private float magicSenseRange;
    [SerializeField] private float magicMemorySeconds = 3f;
    [SerializeField] private float hostilitySenseRange = 5f;
    [SerializeField] private bool hostilityIgnoresLineOfSight = true;

    private const float SentinelRealtimeBroadcastInterval = 0.45f;

    private Rigidbody2D body;
    private CharacterStats stats;
    private KnockbackReceiver knockbackReceiver;
    private SpriteRenderer spriteRenderer;
    private ParticleSystem signalParticles;
    private GridRouteMapGenerator mapGenerator;
    private float currentSpeedMultiplier = 1f;
    private float recoveryUntil;
    private float knockbackUntil;
    private float localHitStopUntil;
    private float strafeDirection = 1f;
    private Vector2 lastKnownPlayerPosition;
    private CombatState combatState = CombatState.Chase;
    private float nextAttackTime;
    private float recoverUntil;
    private float sentinelAlertUntil;
    private float forcedDirectChaseUntil;
    private float nextAllySearchTime;
    private bool sentinelAlerted;
    private float nextSentinelBroadcastTime;
    private float nextSentinelSummonTime;
    private Vector3[] sentinelPatrolRoute = System.Array.Empty<Vector3>();
    private int sentinelPatrolIndex;
    private int sentinelSummonCount;
    private string sentinelSummonEnemyArchetypes;
    private float sentinelSummonCooldown;
    private float sentinelSummonDenSearchRadius;
    private Transform cachedAlly;
    private float stunnedUntil;
    private Vector2 visualFacingDirection = Vector2.up;
    private Color preStunColor = Color.white;
    private bool stunTintActive;
    private StunStatusVisual stunVisual;

    public EnemyArchetype Archetype => archetype;
    private bool IsLocallyHitStopped => Time.unscaledTime < localHitStopUntil;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        stats = GetComponent<CharacterStats>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mapGenerator = FindObjectOfType<GridRouteMapGenerator>();
        if (target == null)
        {
            PlayerInputManager player = FindObjectOfType<PlayerInputManager>();
            target = player != null ? player.transform : null;
        }
        strafeDirection = Random.value < 0.5f ? -1f : 1f;
    }

    private void OnEnable()
    {
        EnemyRegistry.Register(this);
    }

    private void OnDisable()
    {
        EnemyRegistry.Unregister(this);
    }

    private void FixedUpdate()
    {
        if (stats == null || !stats.IsAlive)
        {
            return;
        }

        if (Time.time < stunnedUntil)
        {
            body.velocity = Vector2.zero;
            nextAttackTime = Mathf.Max(nextAttackTime, stunnedUntil);
            return;
        }

        ClearStunTintIfNeeded();

        if (IsLocallyHitStopped)
        {
            body.velocity = Vector2.zero;
            return;
        }

        if (Time.time < knockbackUntil)
        {
            return;
        }

        if (target == null)
        {
            if (archetype == EnemyArchetype.Sentinel && !sentinelAlerted && sentinelPatrolRoute.Length > 0)
            {
                MoveSentinelPatrol(body.position);
            }

            return;
        }

        Vector2 current = body.position;
        if (IsTargetUntargetable())
        {
            MoveTowardLastKnownPosition(current);
            return;
        }

        Vector2 toTarget = (Vector2)target.position - current;
        float distance = toTarget.magnitude;
        bool seesTarget = CanSeeTarget(current, target.position, distance);
        bool sensesHostility = CanSenseHostility(distance);
        bool sensesEvent = TrySenseRecentEvent(current, out Vector2 sensedPosition);
        bool forcedDirectChase = Time.time < forcedDirectChaseUntil;
        bool sentinelRealtimeAlert = archetype == EnemyArchetype.Sentinel && sentinelAlerted;
        if (!seesTarget && !sensesHostility && !forcedDirectChase && !sentinelRealtimeAlert)
        {
            if (sensesEvent)
            {
                lastKnownPlayerPosition = sensedPosition;
                if (archetype == EnemyArchetype.Sentinel)
                {
                    EnterSentinelAlert(sensedPosition, false);
                }
            }

            if (archetype == EnemyArchetype.Sentinel && !sentinelAlerted && sentinelPatrolRoute.Length > 0)
            {
                MoveSentinelPatrol(current);
                return;
            }

            MoveTowardLastKnownPosition(current);
            return;
        }

        if (archetype == EnemyArchetype.Sentinel && (seesTarget || sensesHostility || sensesEvent || forcedDirectChase))
        {
            EnterSentinelAlert(seesTarget || sensesHostility || forcedDirectChase ? (Vector2)target.position : sensedPosition, false);
        }

        lastKnownPlayerPosition = target.position;
        RecoverMoveSpeed();
        Vector2 direction = distance > 0.001f ? toTarget / distance : Vector2.zero;
        switch (EnemyBehaviorResolver.Resolve(archetype))
        {
            case EnemyBehaviorKind.Sentinel:
                MoveSentinel(current, direction, distance, lastKnownPlayerPosition);
                return;
            case EnemyBehaviorKind.Melee:
                MoveMeleeEnemy(current, direction, distance);
                return;
            case EnemyBehaviorKind.Ranged:
                MoveRangedEnemy(current, direction, distance);
                return;
        }

        float desiredStop = archetype == EnemyArchetype.Ranged ? Mathf.Max(stopDistance, 3.6f) : stopDistance;
        if (distance <= desiredStop)
        {
            return;
        }

        float speed = Mathf.Max(0.1f, stats.moveSpeed) * currentSpeedMultiplier;
        EnemyMovementMotor.Move(body, current, direction, speed);

        if (direction.sqrMagnitude > 0.001f)
        {
            FaceDirection(direction);
        }
    }

    public void Configure(EnemyArchetype nextArchetype, Transform nextTarget)
    {
        archetype = nextArchetype;
        target = nextTarget;

        if (stats == null)
        {
            stats = GetComponent<CharacterStats>();
        }

        ApplyConfig(EnemyConfigDatabase.Get(nextArchetype));
        ApplyColor(nextArchetype);
    }

    public void ConfigureFixedScoutPatrol(
        Vector3[] patrolRoute,
        string summonEnemyArchetypes,
        int summonCount,
        float summonCooldown,
        float summonDenSearchRadius)
    {
        sentinelPatrolRoute = patrolRoute ?? System.Array.Empty<Vector3>();
        sentinelPatrolIndex = sentinelPatrolRoute.Length > 1 ? 1 : 0;
        sentinelSummonEnemyArchetypes = string.IsNullOrWhiteSpace(summonEnemyArchetypes) ? "Attacker:1" : summonEnemyArchetypes;
        sentinelSummonCount = Mathf.Max(0, summonCount);
        sentinelSummonCooldown = Mathf.Max(0.1f, summonCooldown);
        sentinelSummonDenSearchRadius = Mathf.Max(0f, summonDenSearchRadius);
        sentinelAlerted = false;
        nextSentinelBroadcastTime = 0f;
        nextSentinelSummonTime = 0f;
    }

    public static float GetVisionRange(EnemyArchetype type)
    {
        switch (type)
        {
            case EnemyArchetype.Sentinel:
                return 20f;
            case EnemyArchetype.Ranged:
                return 15f;
            case EnemyArchetype.Shield:
                return 5f;
            default:
                return 7f;
        }
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
        nextAttackTime = Mathf.Max(nextAttackTime, stunnedUntil);
        body.velocity = Vector2.zero;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && !stunTintActive)
        {
            preStunColor = spriteRenderer.color;
            stunTintActive = true;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.58f, 0.92f, 1f, 1f);
        }

        EnsureStunVisual();
        stunVisual.Show(duration);
    }

    public void BeginDirectChase(float duration)
    {
        forcedDirectChaseUntil = Mathf.Max(forcedDirectChaseUntil, Time.time + Mathf.Max(0.1f, duration));
        if (target == null)
        {
            PlayerInputManager player = FindObjectOfType<PlayerInputManager>();
            target = player != null ? player.transform : null;
        }

        if (target != null)
        {
            lastKnownPlayerPosition = target.position;
        }
    }

    private void ClearStunTintIfNeeded()
    {
        if (!stunTintActive || Time.time < stunnedUntil)
        {
            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = preStunColor;
        }

        if (stunVisual != null)
        {
            stunVisual.Hide();
        }

        stunTintActive = false;
    }

    private void EnsureStunVisual()
    {
        if (stunVisual != null)
        {
            return;
        }

        stunVisual = GetComponent<StunStatusVisual>();
        if (stunVisual == null)
        {
            stunVisual = gameObject.AddComponent<StunStatusVisual>();
        }
    }

    private void ApplyStats(EnemyArchetype type)
    {
        if (stats == null)
        {
            return;
        }

        stats.usesMana = false;
        switch (type)
        {
            case EnemyArchetype.Sentinel:
                stats.maxHealth = 40f;
                stats.armor = 1f;
                stats.magicImmunity = 0f;
                stats.physicalAttack = 4f;
                stats.magicAttack = 0f;
                stats.attack = 0f;
                stats.moveSpeed = 2.15f;
                stats.moveDelay = 0.2f;
                break;
            case EnemyArchetype.Ranged:
                stats.maxHealth = 24f;
                stats.armor = 0f;
                stats.magicImmunity = 0.2f;
                stats.physicalAttack = 0f;
                stats.magicAttack = 6f;
                stats.attack = 0f;
                stats.moveSpeed = 2.2f;
                stats.moveDelay = 0.12f;
                break;
            case EnemyArchetype.Shield:
                stats.maxHealth = 65f;
                stats.armor = 4f;
                stats.magicImmunity = 0.1f;
                stats.physicalAttack = 10f;
                stats.magicAttack = 0f;
                stats.attack = 0f;
                stats.moveSpeed = 1.25f;
                stats.moveDelay = 0.35f;
                break;
            default:
                stats.maxHealth = 30f;
                stats.armor = 0.5f;
                stats.magicImmunity = 0f;
                stats.physicalAttack = 8f;
                stats.magicAttack = 0f;
                stats.attack = 0f;
                stats.moveSpeed = 2.45f;
                stats.moveDelay = 0.12f;
                break;
        }

        stats.ResetStats();
    }

    private void ApplyConfig(EnemyConfig config)
    {
        if (config == null)
        {
            ApplyStats(archetype);
            return;
        }

        visionRange = config.visionRange;
        stopDistance = config.stopDistance;
        accelerationTime = config.accelerationTime;
        knockbackDuration = config.knockbackDuration;
        knockbackDistance = config.knockbackDistance;
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
        if (knockbackReceiver != null)
        {
            knockbackReceiver.Configure(knockbackDistance, knockbackDuration);
        }
        meleeAttackRange = config.meleeAttackRange;
        meleeAttackCooldown = config.meleeAttackCooldown;
        meleePlayerKnockbackDistance = archetype == EnemyArchetype.Shield ? 0.9f : 0.65f;
        meleePlayerKnockbackDuration = Mathf.Max(0.08f, config.knockbackDuration * 0.65f);
        rangedPlayerKnockbackDistance = 0.35f;
        rangedPlayerKnockbackDuration = Mathf.Max(0.06f, config.knockbackDuration * 0.43f);
        meleeRecoverDistance = config.meleeRecoverDistance;
        rangedPreferredDistance = config.rangedPreferredDistance;
        rangedAttackCooldown = config.rangedAttackCooldown;
        rangedProjectileSpeed = config.rangedProjectileSpeed;
        rangedProjectileDistance = config.rangedProjectileDistance;
        sentinelSignalRadius = config.sentinelSignalRadius;
        sentinelFleeDuration = config.sentinelFleeDuration;
        ApplySenseConfig(EnemySenseConfigDatabase.Get(archetype));

        if (stats != null)
        {
            stats.usesMana = false;
            stats.maxHealth = config.maxHealth;
            stats.armor = config.armor;
            stats.magicImmunity = Mathf.Clamp01(config.magicImmunity);
            stats.physicalAttack = config.physicalAttack;
            stats.magicAttack = config.magicAttack;
            stats.attack = 0f;
            stats.moveSpeed = config.moveSpeed;
            stats.moveDelay = config.moveDelay;
            stats.ResetStats();
        }
    }

    private void ApplySenseConfig(EnemySenseConfig senseConfig)
    {
        if (senseConfig == null)
        {
            return;
        }

        if (senseConfig.visual != null && senseConfig.visual.enabled)
        {
            visionRange = senseConfig.visual.range;
            visionRayStep = Mathf.Max(0.02f, senseConfig.visual.rayStep);
        }

        if (senseConfig.hearing != null)
        {
            hearingRange = senseConfig.hearing.enabled ? Mathf.Max(0f, senseConfig.hearing.range) : 0f;
            hearingMemorySeconds = Mathf.Max(0.1f, senseConfig.hearing.memorySeconds);
        }

        if (senseConfig.magic != null)
        {
            magicSenseRange = senseConfig.magic.enabled ? Mathf.Max(0f, senseConfig.magic.range) : 0f;
            magicMemorySeconds = Mathf.Max(0.1f, senseConfig.magic.spellCastMemorySeconds);
        }

        if (senseConfig.hostility != null)
        {
            hostilitySenseRange = senseConfig.hostility.enabled ? Mathf.Max(0f, senseConfig.hostility.range) : 0f;
            hostilityIgnoresLineOfSight = senseConfig.hostility.ignoresLineOfSight;
            sentinelSignalRadius = Mathf.Max(sentinelSignalRadius, senseConfig.hostility.signalRadius);
        }
    }

    private void ApplyColor(EnemyArchetype type)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            return;
        }

        switch (type)
        {
            case EnemyArchetype.Sentinel:
                spriteRenderer.color = new Color(0.85f, 0.9f, 1f, 1f);
                break;
            case EnemyArchetype.Ranged:
                spriteRenderer.color = new Color(0.72f, 0.88f, 1f, 1f);
                break;
            case EnemyArchetype.Shield:
                spriteRenderer.color = new Color(0.92f, 0.92f, 0.82f, 1f);
                break;
            default:
                spriteRenderer.color = Color.white;
                break;
        }
    }

    public void OnDamaged(Vector2 hitSource, float weaponKnockbackDistance, float weaponKnockbackDuration)
    {
        if (body == null)
        {
            return;
        }

        float finalDuration = Mathf.Max(0.04f, weaponKnockbackDuration * Mathf.Max(0.01f, knockbackDuration));
        knockbackUntil = Time.time + finalDuration;
        currentSpeedMultiplier = 0.35f;
        recoveryUntil = knockbackUntil + accelerationTime;

        if (archetype == EnemyArchetype.Sentinel)
        {
            EnterSentinelAlert(hitSource, true);
        }

        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
    }

    public void PushHitStop(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        localHitStopUntil = Mathf.Max(localHitStopUntil, Time.unscaledTime + duration);
        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }

    private void MoveSentinel(Vector2 current, Vector2 directionToTarget, float distance, Vector2 playerPosition)
    {
        TickSentinelAlertActions(true);

        Vector2 moveDirection;
        float speedMultiplier = 1f;
        if (Time.time < sentinelAlertUntil)
        {
            moveDirection = -directionToTarget;
            speedMultiplier = 2f;
        }
        else
        {
            Transform ally = FindNearestAlly();
            if (ally != null)
            {
                Vector2 toAlly = (Vector2)ally.position - current;
                moveDirection = toAlly.sqrMagnitude > 0.001f ? toAlly.normalized : -directionToTarget;
            }
            else
            {
                Vector2 tangent = new Vector2(-directionToTarget.y, directionToTarget.x) * strafeDirection;
                if (Random.value < 0.004f)
                {
                    strafeDirection *= -1f;
                    tangent *= -1f;
                }

                moveDirection = distance < 8f ? -directionToTarget + tangent * 0.35f : tangent;
            }
        }

        float sentinelSpeed = Mathf.Max(0.1f, stats.moveSpeed) * currentSpeedMultiplier * speedMultiplier;
        EnemyMovementMotor.Move(body, current, moveDirection, sentinelSpeed);
        FaceDirection(directionToTarget);
    }

    private void MoveSentinelPatrol(Vector2 current)
    {
        if (sentinelPatrolRoute == null || sentinelPatrolRoute.Length == 0)
        {
            body.velocity = Vector2.zero;
            return;
        }

        Vector2 destination = sentinelPatrolRoute[Mathf.Clamp(sentinelPatrolIndex, 0, sentinelPatrolRoute.Length - 1)];
        Vector2 delta = destination - current;
        if (delta.sqrMagnitude <= 0.12f)
        {
            sentinelPatrolIndex = (sentinelPatrolIndex + 1) % sentinelPatrolRoute.Length;
            destination = sentinelPatrolRoute[sentinelPatrolIndex];
            delta = destination - current;
        }

        if (delta.sqrMagnitude <= 0.001f)
        {
            body.velocity = Vector2.zero;
            return;
        }

        RecoverMoveSpeed();
        Vector2 direction = delta.normalized;
        float speed = Mathf.Max(0.1f, stats.moveSpeed) * currentSpeedMultiplier;
        EnemyMovementMotor.Move(body, current, direction, speed);
        FaceDirection(direction);
    }

    private void MoveMeleeEnemy(Vector2 current, Vector2 directionToTarget, float distance)
    {
        float attackRange = archetype == EnemyArchetype.Shield ? 0.9f : meleeAttackRange;
        float cooldown = archetype == EnemyArchetype.Shield ? meleeAttackCooldown * 1.25f : meleeAttackCooldown;
        float speed = Mathf.Max(0.1f, stats.moveSpeed) * currentSpeedMultiplier;

        if (combatState == CombatState.Recover)
        {
            Vector2 away = -directionToTarget;
            Vector2 tangent = new Vector2(-directionToTarget.y, directionToTarget.x) * strafeDirection;
            if (Random.value < 0.004f)
            {
                strafeDirection *= -1f;
                tangent *= -1f;
            }

            Vector2 recoverDirection = distance < meleeRecoverDistance ? away * 0.85f + tangent * 0.35f : tangent;
            EnemyMovementMotor.Move(body, current, recoverDirection, speed * 0.72f);
            FaceDirection(directionToTarget);
            if (Time.time >= recoverUntil && Time.time >= nextAttackTime)
            {
                combatState = CombatState.Chase;
            }
            return;
        }

        if (distance <= attackRange && Time.time >= nextAttackTime)
        {
            PerformMeleeAttack(cooldown);
            return;
        }

        EnemyMovementMotor.Move(body, current, directionToTarget, speed);
        FaceDirection(directionToTarget);
    }

    private void MoveRangedEnemy(Vector2 current, Vector2 directionToTarget, float distance)
    {
        float speed = Mathf.Max(0.1f, stats.moveSpeed) * currentSpeedMultiplier;
        float closeDistance = rangedPreferredDistance - 0.75f;
        float farDistance = rangedPreferredDistance + 0.9f;
        Vector2 moveDirection = Vector2.zero;

        if (distance < closeDistance)
        {
            moveDirection = -directionToTarget;
        }
        else if (distance > farDistance)
        {
            moveDirection = directionToTarget;
        }
        else
        {
            Vector2 tangent = new Vector2(-directionToTarget.y, directionToTarget.x) * strafeDirection;
            if (Random.value < 0.004f)
            {
                strafeDirection *= -1f;
                tangent *= -1f;
            }
            moveDirection = tangent * 0.35f;
        }

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            EnemyMovementMotor.Move(body, current, moveDirection, speed);
        }

        FaceDirection(directionToTarget);
        if (Time.time >= nextAttackTime && distance <= visionRange && HasLineOfSight(current, target.position, distance))
        {
            nextAttackTime = Time.time + rangedAttackCooldown;
            EmitRangedProjectile(directionToTarget);
            TryHitPlayerWithRanged(current, directionToTarget, distance);
        }
    }

    private void EmitRangedProjectile(Vector2 direction)
    {
        EnsureSignalParticles();
        if (signalParticles == null || direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector2 forward = direction.normalized;
        Vector2 side = new Vector2(-forward.y, forward.x);
        Vector2 start = (Vector2)transform.position + forward * 0.35f;
        float projectileDistance = GetRangedProjectileDistance(start, forward);
        int coreCount = 14;
        Color core = new Color(1f, 0.05f, 0.02f, 0.95f);
        Color trail = new Color(1f, 0.18f, 0.08f, 0.7f);
        float lifetime = Mathf.Max(0.08f, projectileDistance / Mathf.Max(0.1f, rangedProjectileSpeed));

        for (int i = 0; i < coreCount; i++)
        {
            float t = i / (float)Mathf.Max(1, coreCount - 1);
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = start + forward * (t * 0.28f) + side * Random.Range(-0.025f, 0.025f);
            emitParams.velocity = forward * rangedProjectileSpeed + side * Random.Range(-0.15f, 0.15f);
            emitParams.startColor = i < 5 ? core : trail;
            emitParams.startSize = Mathf.Lerp(0.075f, 0.035f, t);
            emitParams.startLifetime = lifetime;
            signalParticles.Emit(emitParams, 1);
        }

        for (int i = 0; i < 5; i++)
        {
            ParticleSystem.EmitParams spark = new ParticleSystem.EmitParams();
            spark.position = start - forward * Random.Range(0f, 0.18f);
            spark.velocity = -forward * Random.Range(0.5f, 1.3f) + side * Random.Range(-0.45f, 0.45f);
            spark.startColor = trail;
            spark.startSize = Random.Range(0.025f, 0.05f);
            spark.startLifetime = Random.Range(0.12f, 0.22f);
            signalParticles.Emit(spark, 1);
        }

        if (projectileDistance < rangedProjectileDistance - 0.05f)
        {
            EmitRangedImpact(start + forward * projectileDistance, forward);
        }
    }

    private void MoveTowardLastKnownPosition(Vector2 current)
    {
        if (lastKnownPlayerPosition == Vector2.zero || archetype == EnemyArchetype.Sentinel)
        {
            return;
        }

        Vector2 toKnown = lastKnownPlayerPosition - current;
        if (toKnown.sqrMagnitude < 0.4f)
        {
            return;
        }

        RecoverMoveSpeed();
        Vector2 direction = toKnown.normalized;
        float speed = Mathf.Max(0.1f, stats.moveSpeed) * currentSpeedMultiplier;
        EnemyMovementMotor.Move(body, current, direction, speed);
        FaceDirection(direction);
    }

    private void PerformMeleeAttack(float cooldown)
    {
        nextAttackTime = Time.time + cooldown;
        recoverUntil = nextAttackTime;
        combatState = CombatState.Recover;
        currentSpeedMultiplier = 0.55f;
        recoveryUntil = Time.time + Mathf.Min(cooldown, accelerationTime);
        EmitMeleePulse();
        TryHitPlayerWithMelee();
    }

    private void EmitMeleePulse()
    {
        EnsureSignalParticles();
        if (signalParticles == null)
        {
            return;
        }

        Vector2 forward = visualFacingDirection.sqrMagnitude > 0.001f ? visualFacingDirection.normalized : Vector2.up;
        Vector2 side = new Vector2(-forward.y, forward.x);
        Color color = archetype == EnemyArchetype.Shield ? new Color(1f, 0.72f, 0.24f, 0.9f) : new Color(1f, 0.18f, 0.08f, 0.9f);
        int pulseCount = archetype == EnemyArchetype.Shield ? 28 : 22;
        float reach = archetype == EnemyArchetype.Shield ? 1.15f : 0.92f;
        for (int i = 0; i < pulseCount; i++)
        {
            float t = i / Mathf.Max(1f, pulseCount - 1f);
            float spread = Mathf.Lerp(-0.85f, 0.85f, t);
            Vector2 direction = (forward + side * spread).normalized;
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = (Vector2)transform.position + forward * 0.18f + side * Random.Range(-0.06f, 0.06f);
            emitParams.velocity = direction * Random.Range(1.2f, 2.8f) * reach;
            emitParams.startColor = color;
            emitParams.startSize = archetype == EnemyArchetype.Shield ? Random.Range(0.055f, 0.12f) : Random.Range(0.045f, 0.095f);
            emitParams.startLifetime = Random.Range(0.16f, 0.28f);
            signalParticles.Emit(emitParams, 1);
        }

        for (int i = 0; i < 8; i++)
        {
            ParticleSystem.EmitParams core = new ParticleSystem.EmitParams();
            core.position = (Vector2)transform.position + forward * Random.Range(0.18f, 0.42f);
            core.velocity = forward * Random.Range(0.8f, 1.8f) + side * Random.Range(-0.35f, 0.35f);
            core.startColor = new Color(1f, 0.04f, 0.02f, 0.95f);
            core.startSize = Random.Range(0.08f, 0.14f);
            core.startLifetime = Random.Range(0.08f, 0.16f);
            signalParticles.Emit(core, 1);
        }
    }

    private float GetRangedProjectileDistance(Vector2 start, Vector2 forward)
    {
        float distance = GetVisionBlockedDistance(start, forward, rangedProjectileDistance);
        RaycastHit2D[] hits = Physics2D.RaycastAll(start, forward, rangedProjectileDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!IsVisionBlocker(hit))
            {
                continue;
            }

            distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - 0.08f));
        }

        return distance;
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to, float distance)
    {
        if (distance <= 0.001f)
        {
            return true;
        }

        ResolveMapGenerator();
        return EnemyPerception.HasLineOfSight(mapGenerator, from, to, distance, visionRayStep);
    }

    private bool CanSeeTarget(Vector2 from, Vector2 to, float distance)
    {
        return distance <= visionRange && HasLineOfSight(from, to, distance);
    }

    private bool CanSenseHostility(float distance)
    {
        if (hostilitySenseRange <= 0f || distance > hostilitySenseRange)
        {
            return false;
        }

        if (hostilityIgnoresLineOfSight)
        {
            return true;
        }

        return target != null && HasLineOfSight(body.position, target.position, distance);
    }

    private bool TrySenseRecentEvent(Vector2 current, out Vector2 sensedPosition)
    {
        if (SensoryEventBus.TryGetLatest(SensoryEventType.Sound, current, hearingRange, out sensedPosition))
        {
            return true;
        }

        return SensoryEventBus.TryGetLatest(SensoryEventType.Magic, current, magicSenseRange, out sensedPosition);
    }

    private float GetVisionBlockedDistance(Vector2 origin, Vector2 direction, float maxDistance)
    {
        ResolveMapGenerator();
        return EnemyPerception.GetVisionBlockedDistance(mapGenerator, origin, direction, maxDistance, visionRayStep);
    }

    private void ResolveMapGenerator()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindObjectOfType<GridRouteMapGenerator>();
        }
    }

    private bool IsVisionBlocker(Collider2D hit)
    {
        return EnemyPerception.IsVisionBlocker(hit);
    }

    private void EmitRangedImpact(Vector2 point, Vector2 forward)
    {
        EnsureSignalParticles();
        if (signalParticles == null)
        {
            return;
        }

        Color color = new Color(1f, 0.05f, 0.02f, 0.86f);
        for (int i = 0; i < 12; i++)
        {
            Vector2 direction = -forward + Random.insideUnitCircle * 0.65f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = -forward;
            }

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = point + Random.insideUnitCircle * 0.035f;
            emitParams.velocity = direction.normalized * Random.Range(0.45f, 1.55f);
            emitParams.startColor = color;
            emitParams.startSize = Random.Range(0.025f, 0.06f);
            emitParams.startLifetime = Random.Range(0.1f, 0.22f);
            signalParticles.Emit(emitParams, 1);
        }
    }

    private void TryHitPlayerWithMelee()
    {
        if (target == null || IsTargetUntargetable())
        {
            return;
        }

        Vector2 current = transform.position;
        Vector2 targetPosition = target.position;
        float attackRange = archetype == EnemyArchetype.Shield ? 0.9f : meleeAttackRange;
        if (Vector2.Distance(current, targetPosition) > attackRange + 0.35f)
        {
            return;
        }

        Collider2D playerCollider = target.GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            Vector2 closest = playerCollider.ClosestPoint(current);
            if (Vector2.Distance(current, closest) > attackRange + 0.2f)
            {
                return;
            }

            targetPosition = closest;
        }

        EmitPlayerMeleeHit(targetPosition);
        if (DamagePlayer())
        {
            HitVolumeFeedback feedback = target.GetComponent<HitVolumeFeedback>();
            if (feedback != null)
            {
                feedback.Play(current, archetype == EnemyArchetype.Shield ? 1.25f : 1.1f);
            }

            PlayerInputManager playerMovement = target.GetComponent<PlayerInputManager>();
            if (playerMovement != null)
            {
                playerMovement.ApplyExternalKnockback(current, meleePlayerKnockbackDistance, meleePlayerKnockbackDuration);
            }
        }
    }

    private void TryHitPlayerWithRanged(Vector2 current, Vector2 directionToTarget, float distance)
    {
        if (target == null || directionToTarget.sqrMagnitude <= 0.001f || IsTargetUntargetable())
        {
            return;
        }

        Vector2 forward = directionToTarget.normalized;
        Vector2 start = current + forward * 0.35f;
        float projectileDistance = GetRangedProjectileDistance(start, forward);
        if (distance > projectileDistance + 0.45f)
        {
            return;
        }

        EmitPlayerMeleeHit(target.position);
        if (DamagePlayer())
        {
            HitVolumeFeedback feedback = target.GetComponent<HitVolumeFeedback>();
            if (feedback != null)
            {
                feedback.Play(current, 1.05f);
            }

            PlayerInputManager playerMovement = target.GetComponent<PlayerInputManager>();
            if (playerMovement != null)
            {
                playerMovement.ApplyExternalKnockback(current, rangedPlayerKnockbackDistance, rangedPlayerKnockbackDuration);
            }
        }
    }

    private bool DamagePlayer()
    {
        if (target == null)
        {
            return false;
        }

        CharacterStats playerStats = target.GetComponent<CharacterStats>();
        if (playerStats == null || !playerStats.IsAlive)
        {
            return false;
        }

        if (playerStats.IsDamageImmune || playerStats.IsUntargetable)
        {
            return false;
        }

        Vector2 hitPoint = target.position;
        Vector2 origin = transform.position;
        Vector2 hitDirection = hitPoint - origin;
        DamageContext context = new DamageContext(
            Time.frameCount,
            gameObject,
            stats,
            WeaponType.Knife,
            origin,
            hitPoint,
            hitDirection,
            0f,
            0f,
            0f,
            new FeedbackPayload(0.025f, 0.08f, archetype == EnemyArchetype.Shield ? 0.9f : 0.65f, 0.18f, 1.2f, 0f),
            false);
        HitResult result = playerStats.ApplyDamage(context);
        return result.accepted;
    }

    private void EmitPlayerMeleeHit(Vector2 hitPoint)
    {
        EnsureSignalParticles();
        if (signalParticles == null)
        {
            return;
        }

        Vector2 away = ((Vector2)target.position - (Vector2)transform.position);
        if (away.sqrMagnitude < 0.001f)
        {
            away = Vector2.up;
        }
        away.Normalize();

        Color core = new Color(1f, 0.04f, 0.02f, 0.96f);
        Color edge = new Color(1f, 0.24f, 0.08f, 0.76f);
        for (int i = 0; i < 18; i++)
        {
            float angle = (Mathf.PI * 2f * i) / 18f;
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 velocity = radial * Random.Range(0.45f, 1.35f) + away * Random.Range(0.35f, 1.1f);
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = hitPoint + Random.insideUnitCircle * 0.05f;
            emitParams.velocity = velocity;
            emitParams.startColor = i < 6 ? core : edge;
            emitParams.startSize = i < 6 ? Random.Range(0.05f, 0.09f) : Random.Range(0.025f, 0.055f);
            emitParams.startLifetime = Random.Range(0.08f, 0.2f);
            signalParticles.Emit(emitParams, 1);
        }
    }

    public void ReceivePlayerSignal(Vector2 playerPosition)
    {
        lastKnownPlayerPosition = playerPosition;
        if (target == null)
        {
            PlayerInputManager player = FindObjectOfType<PlayerInputManager>();
            target = player != null ? player.transform : null;
        }
    }

    private void EnterSentinelAlert(Vector2 playerPosition, bool urgent)
    {
        sentinelAlerted = true;
        sentinelAlertUntil = Mathf.Max(sentinelAlertUntil, Time.time + sentinelFleeDuration);
        lastKnownPlayerPosition = playerPosition;
        if (target != null)
        {
            lastKnownPlayerPosition = target.position;
        }

        BroadcastPlayerPosition(urgent);
        nextSentinelBroadcastTime = Time.time + SentinelRealtimeBroadcastInterval;
    }

    private void TickSentinelAlertActions(bool allowSummon)
    {
        if (!sentinelAlerted)
        {
            return;
        }

        if (target != null)
        {
            lastKnownPlayerPosition = target.position;
        }

        if (Time.time >= nextSentinelBroadcastTime)
        {
            BroadcastPlayerPosition(false);
            nextSentinelBroadcastTime = Time.time + SentinelRealtimeBroadcastInterval;
        }

        if (allowSummon && sentinelSummonCount > 0 && Time.time >= nextSentinelSummonTime)
        {
            TryRequestNearestSpawnDen();
            nextSentinelSummonTime = Time.time + sentinelSummonCooldown;
        }
    }

    private void TryRequestNearestSpawnDen()
    {
        ResolveMapGenerator();
        if (mapGenerator == null)
        {
            return;
        }

        IReadOnlyList<SpawnDenController> dens = mapGenerator.SpawnDens;
        SpawnDenController best = null;
        float bestDistance = float.MaxValue;
        Vector2 current = transform.position;
        for (int i = 0; i < dens.Count; i++)
        {
            SpawnDenController den = dens[i];
            if (den == null || den.IsDestroyed)
            {
                continue;
            }

            float distance = Vector2.Distance(current, den.transform.position);
            if (distance <= sentinelSummonDenSearchRadius && distance < bestDistance)
            {
                best = den;
                bestDistance = distance;
            }
        }

        if (best != null)
        {
            best.QueueExternalSpawnRequest(sentinelSummonEnemyArchetypes, sentinelSummonCount);
        }
    }

    private Transform FindNearestAlly()
    {
        if (Time.time < nextAllySearchTime && cachedAlly != null && cachedAlly.gameObject.activeInHierarchy)
        {
            return cachedAlly;
        }

        nextAllySearchTime = Time.time + 0.5f;
        EnemyRegistry.Prune();
        IReadOnlyList<SimpleEnemyAI> enemies = EnemyRegistry.Enemies;
        Transform best = null;
        float bestDistance = float.MaxValue;
        Vector2 current = transform.position;
        for (int i = 0; i < enemies.Count; i++)
        {
            SimpleEnemyAI enemy = enemies[i];
            if (enemy == null || enemy == this || enemy.archetype == EnemyArchetype.Sentinel)
            {
                continue;
            }

            float distance = Vector2.Distance(current, enemy.transform.position);
            if (distance <= sentinelSignalRadius && distance < bestDistance)
            {
                best = enemy.transform;
                bestDistance = distance;
            }
        }

        cachedAlly = best;
        return best;
    }

    private void BroadcastPlayerPosition(bool urgent)
    {
        EmitSignalWave(urgent);
        if (urgent && archetype == EnemyArchetype.Sentinel)
        {
            TaskRunState.Existing?.NotifySentinelAlarm(transform.position);
            WorldHostilityDirector.Current.Notify(new DirectorEvent(DirectorEventType.SentinelAlarm, transform.position, 1f, "sentinel_alarm"));
        }

        EnemyRegistry.Prune();
        IReadOnlyList<SimpleEnemyAI> enemies = EnemyRegistry.Enemies;
        Vector2 current = transform.position;
        float broadcastRadius = urgent ? sentinelSignalRadius * 1.5f : sentinelSignalRadius;
        for (int i = 0; i < enemies.Count; i++)
        {
            SimpleEnemyAI enemy = enemies[i];
            if (enemy == null || enemy == this)
            {
                continue;
            }

            if (Vector2.Distance(current, enemy.transform.position) <= broadcastRadius)
            {
                enemy.ReceivePlayerSignal(lastKnownPlayerPosition);
            }
        }
    }

    private void EmitSignalWave(bool urgent)
    {
        EnsureSignalParticles();
        if (signalParticles == null)
        {
            return;
        }

        int count = urgent ? 36 : 22;
        Color color = urgent ? new Color(1f, 0.08f, 0.04f, 0.95f) : new Color(1f, 0.22f, 0.12f, 0.78f);
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i) / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = transform.position;
            emitParams.velocity = direction * (urgent ? 4.6f : 3.2f);
            emitParams.startColor = color;
            emitParams.startSize = urgent ? 0.055f : 0.04f;
            emitParams.startLifetime = urgent ? 0.42f : 0.34f;
            signalParticles.Emit(emitParams, 1);
        }
    }

    private void EnsureSignalParticles()
    {
        if (signalParticles != null)
        {
            return;
        }

        Transform existing = transform.Find("SentinelSignalParticles");
        GameObject particleObject = existing != null ? existing.gameObject : new GameObject("SentinelSignalParticles");
        particleObject.transform.SetParent(transform);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;
        signalParticles = particleObject.GetComponent<ParticleSystem>();
        if (signalParticles == null)
        {
            signalParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = signalParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = signalParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = signalParticles.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 47;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private bool IsTargetUntargetable()
    {
        if (target == null)
        {
            return true;
        }

        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        return targetStats != null && targetStats.IsUntargetable;
    }

    private void RecoverMoveSpeed()
    {
        if (Time.time >= recoveryUntil)
        {
            currentSpeedMultiplier = 1f;
            return;
        }

        float recoveryStart = recoveryUntil - accelerationTime;
        float t = Mathf.InverseLerp(recoveryStart, recoveryUntil, Time.time);
        currentSpeedMultiplier = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(t));
    }

    private void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        visualFacingDirection = direction.normalized;
        transform.rotation = Quaternion.identity;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null && Mathf.Abs(visualFacingDirection.x) > 0.05f)
        {
            spriteRenderer.flipX = visualFacingDirection.x > 0f;
        }
    }
}
