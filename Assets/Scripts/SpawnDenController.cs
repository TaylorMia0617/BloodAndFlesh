using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterStats))]
public sealed class SpawnDenController : MonoBehaviour, IDamageable
{
    private const int SpawnProbeCount = 16;
    private const int SpawnOverlapCapacity = 16;

    [SerializeField] private SpawnDenConfig config;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Sprite[] enemySprites;
    [SerializeField] private int globalEnemyLimit = 20;

    private readonly SpawnDenModel model = new SpawnDenModel();
    private readonly Collider2D[] spawnOverlapResults = new Collider2D[SpawnOverlapCapacity];
    private readonly ContactFilter2D spawnSpaceFilter = new ContactFilter2D
    {
        useTriggers = false,
        useLayerMask = true,
        layerMask = Physics2D.DefaultRaycastLayers
    };

    private CharacterStats stats;
    private SpawnDenView view;
    private Transform enemyParent;

    public string Id => config != null ? config.id : string.Empty;
    public bool IsDestroyed => model.state == SpawnDenState.Destroyed;
    public bool IsSpawningActive => model.state == SpawnDenState.Active;
    public int LivingSpawnedEnemyCount
    {
        get
        {
            model.PruneLivingEnemies();
            return model.livingEnemies.Count;
        }
    }

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
        view = new SpawnDenView(gameObject, GetComponent<SpriteRenderer>());
    }

    private void Update()
    {
        Tick(Time.deltaTime, true);
    }

    public void Configure(
        SpawnDenConfig nextConfig,
        Transform nextPlayerTarget,
        Sprite[] nextEnemySprites,
        Transform nextEnemyParent,
        int nextGlobalEnemyLimit)
    {
        config = nextConfig ?? new SpawnDenConfig();
        playerTarget = nextPlayerTarget;
        enemySprites = nextEnemySprites;
        enemyParent = nextEnemyParent != null ? nextEnemyParent : transform.parent;
        globalEnemyLimit = Mathf.Max(1, nextGlobalEnemyLimit);
        model.Reset();
        ParseWeightedArchetypes(config.enemyArchetypes, model.weightedArchetypes);

        stats = stats != null ? stats : GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.maxHealth = Mathf.Max(1f, config.maxHealth);
            stats.armor = Mathf.Max(0f, stats.armor);
            stats.ResetStats();
        }
    }

    public bool CanSpawnNow()
    {
        return CanAttemptSpawn(WorldHostilityDirector.Current);
    }

    public float DebugGetCurrentSpawnInterval()
    {
        return GetCurrentSpawnInterval(WorldHostilityDirector.Current);
    }

    public float DebugGetSpawnTimer()
    {
        return model.spawnTimer;
    }

    public int DebugGetPendingBurstCount()
    {
        int count = 0;
        for (int i = 0; i < model.timedBursts.Count; i++)
        {
            count += Mathf.Max(0, model.timedBursts[i].remainingCount);
        }

        return count;
    }

    public void DebugTick(float deltaTime)
    {
        Tick(Mathf.Max(0f, deltaTime), true);
    }

    public void QueueExternalSpawnRequest(string enemyArchetypes, int count)
    {
        int safeCount = Mathf.Max(0, count);
        if (safeCount <= 0 || model.state == SpawnDenState.Destroyed)
        {
            return;
        }

        float fallbackInterval = GetCurrentSpawnInterval(WorldHostilityDirector.Current);
        model.timedBursts.Add(new SpawnDenTimedBurst(enemyArchetypes, safeCount, 0f, fallbackInterval));
    }

    public HitResult ApplyDamage(in DamageContext context)
    {
        if (model.state == SpawnDenState.Destroyed)
        {
            return HitResult.Rejected;
        }

        if (model.state == SpawnDenState.ReviveDelay && stats != null && stats.CurrentHealth <= 0f && context.baseDamage > 0f)
        {
            DestroyPermanently();
            return new HitResult(true, true, true, false, context.baseDamage, context.feedback);
        }

        stats = stats != null ? stats : GetComponent<CharacterStats>();
        if (stats == null)
        {
            DestroyPermanently();
            return new HitResult(true, true, true, false, context.baseDamage, context.feedback);
        }

        bool wasAlive = stats.IsAlive;
        HitResult result = stats.ApplyDamage(context);
        if (result.dealtDamage)
        {
            view?.ShowDamageFeedback();
            TriggerEligiblePhases();
        }

        bool killed = wasAlive && !stats.IsAlive;
        if (killed)
        {
            HandleHealthDepleted();
        }
        else if (model.state == SpawnDenState.Reviving && !stats.IsAlive)
        {
            DestroyPermanently();
        }

        return result;
    }

    public void TakeDamage(float amount, float armorPiercing = 0f, Vector2 hitSource = default, CharacterStats attacker = null)
    {
        ApplyDamage(DamageContext.Legacy(amount, armorPiercing, hitSource, attacker));
    }

    private void Tick(float deltaTime, bool allowSpawning)
    {
        if (model.state == SpawnDenState.Destroyed)
        {
            return;
        }

        TickRevive(deltaTime);
        if (!allowSpawning || model.state != SpawnDenState.Active)
        {
            return;
        }

        WorldHostilityDirector director = WorldHostilityDirector.Current;
        TickTimedBursts(deltaTime, director);
        TickNormalSpawn(deltaTime, director);
    }

    private void TickNormalSpawn(float deltaTime, WorldHostilityDirector director)
    {
        model.spawnTimer += deltaTime;
        float interval = GetCurrentSpawnInterval(director);
        if (model.spawnTimer < interval)
        {
            return;
        }

        model.spawnTimer = 0f;
        TrySpawnEnemy(director, null);
    }

    private void TickRevive(float deltaTime)
    {
        if (model.state == SpawnDenState.ReviveDelay)
        {
            model.reviveDelayRemaining -= deltaTime;
            if (model.reviveDelayRemaining <= 0f)
            {
                model.state = SpawnDenState.Reviving;
            }
        }

        if (model.state == SpawnDenState.Reviving && stats != null)
        {
            float heal = Mathf.Max(0f, GetDestroyLogic().reviveHealPerSecond) * deltaTime;
            if (heal > 0f)
            {
                stats.RestoreHealth(heal);
            }

            if (stats.CurrentHealth >= model.reviveTargetHealth)
            {
                model.state = SpawnDenState.Active;
                model.spawnTimer = 0f;
            }
        }
    }

    private bool CanAttemptSpawn(WorldHostilityDirector director)
    {
        if (config == null || model.state != SpawnDenState.Active)
        {
            return false;
        }

        WorldHostilityDirector activeDirector = director ?? WorldHostilityDirector.Current;
        if (activeDirector.RawHostility + 0.001f < Mathf.Max(0f, config.minWorldHostility))
        {
            return false;
        }

        SpawnDenSpawnLogic logic = GetSpawnLogic();
        if (logic.activationRange > 0f)
        {
            if (playerTarget == null)
            {
                return false;
            }

            if (Vector2.Distance(transform.position, playerTarget.position) > logic.activationRange)
            {
                return false;
            }
        }

        if (model.weightedArchetypes.Count == 0)
        {
            return false;
        }

        model.PruneLivingEnemies();
        if (logic.maxAliveEnemies > 0 && model.livingEnemies.Count >= logic.maxAliveEnemies)
        {
            return false;
        }

        int effectiveGlobalLimit = GetEffectiveGlobalLimit(activeDirector);
        return EnemyRegistry.LivingCount < effectiveGlobalLimit && activeDirector.IsSpawnAllowed(transform.position);
    }

    private bool TrySpawnEnemy(WorldHostilityDirector director, string archetypeOverride)
    {
        if (!CanAttemptSpawn(director))
        {
            return false;
        }

        EnemyArchetype archetype = PickArchetype(archetypeOverride);
        float enemyRadius = Mathf.Max(0.05f, EnemyConfigDatabase.Get(archetype).colliderRadius);
        if (!TryFindSpawnPosition(enemyRadius, out Vector3 spawnPosition))
        {
            return false;
        }

        GameObject enemy = EnemySpawnFactory.CreateEnemy(
            enemyParent,
            $"{Id}_Enemy_{model.spawnIndex++:000}",
            spawnPosition,
            archetype,
            playerTarget,
            enemySprites,
            director);
        model.livingEnemies.Add(enemy);
        return true;
    }

    private bool TryFindSpawnPosition(float checkRadius, out Vector3 spawnPosition)
    {
        SpawnDenSpawnLogic logic = GetSpawnLogic();
        float spawnRadius = Mathf.Max(0.01f, logic.spawnRadius);
        Vector2 center = transform.position;
        if (IsSpawnSpaceClear(center, checkRadius))
        {
            spawnPosition = new Vector3(center.x, center.y, transform.position.z);
            return true;
        }

        for (int i = 0; i < SpawnProbeCount; i++)
        {
            float ring = i < SpawnProbeCount / 2 ? 0.5f : 1f;
            float angle = ((i % (SpawnProbeCount / 2)) / (float)(SpawnProbeCount / 2)) * Mathf.PI * 2f;
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius * ring;
            if (IsSpawnSpaceClear(candidate, checkRadius))
            {
                spawnPosition = new Vector3(candidate.x, candidate.y, transform.position.z);
                return true;
            }
        }

        spawnPosition = transform.position;
        return false;
    }

    private bool IsSpawnSpaceClear(Vector2 position, float checkRadius)
    {
        int count = Physics2D.OverlapCircle(position, checkRadius, spawnSpaceFilter, spawnOverlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = spawnOverlapResults[i];
            if (hit == null)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void TriggerEligiblePhases()
    {
        if (config == null || config.phases == null || stats == null || stats.maxHealth <= 0f)
        {
            return;
        }

        float healthRatio = stats.CurrentHealth / Mathf.Max(1f, stats.maxHealth);
        for (int i = 0; i < config.phases.Length; i++)
        {
            SpawnDenPhaseConfig phase = config.phases[i];
            if (phase == null || model.triggeredPhaseIndices.Contains(i))
            {
                continue;
            }

            if (healthRatio <= Mathf.Clamp01(phase.healthPercent))
            {
                model.triggeredPhaseIndices.Add(i);
                ExecutePhase(phase);
            }
        }
    }

    private void ExecutePhase(SpawnDenPhaseConfig phase)
    {
        string action = phase.action ?? string.Empty;
        if (string.Equals(action, "intervalMultiplier", StringComparison.OrdinalIgnoreCase))
        {
            model.intervalMultiplier *= Mathf.Max(0.05f, phase.intervalMultiplier);
            return;
        }

        if (string.Equals(action, "summonEnemies", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "burstSpawn", StringComparison.OrdinalIgnoreCase))
        {
            int count = Mathf.Max(1, phase.count);
            float fallbackInterval = GetCurrentSpawnInterval(WorldHostilityDirector.Current);
            model.timedBursts.Add(new SpawnDenTimedBurst(phase.enemyArchetypes, count, phase.duration, fallbackInterval));
        }
    }

    private void TickTimedBursts(float deltaTime, WorldHostilityDirector director)
    {
        for (int i = model.timedBursts.Count - 1; i >= 0; i--)
        {
            SpawnDenTimedBurst burst = model.timedBursts[i];
            burst.remainingSeconds -= deltaTime;
            burst.spawnTimer += deltaTime;
            while (burst.remainingCount > 0 && burst.spawnTimer >= burst.intervalSeconds)
            {
                burst.spawnTimer -= burst.intervalSeconds;
                TrySpawnEnemy(director, burst.enemyArchetypes);
                burst.remainingCount--;
            }

            if (burst.remainingCount <= 0 || burst.remainingSeconds <= 0f)
            {
                model.timedBursts.RemoveAt(i);
            }
            else
            {
                model.timedBursts[i] = burst;
            }
        }
    }

    private void HandleHealthDepleted()
    {
        if (model.state != SpawnDenState.Active)
        {
            DestroyPermanently();
            return;
        }

        SpawnDenDestroyLogicConfig logic = GetDestroyLogic();
        if (!string.Equals(logic.type, "reviveAfterDelayInterruptible", StringComparison.OrdinalIgnoreCase))
        {
            DestroyPermanently();
            return;
        }

        model.state = SpawnDenState.ReviveDelay;
        model.spawnTimer = 0f;
        model.reviveDelayRemaining = Mathf.Max(0f, logic.reviveDelaySeconds);
        model.reviveTargetHealth = Mathf.Max(1f, stats.maxHealth * Mathf.Clamp01(logic.reviveHealthPercent));
    }

    private void DestroyPermanently()
    {
        model.state = SpawnDenState.Destroyed;
        model.spawnTimer = 0f;
        view?.ShowDestroyed();
        WorldHostilityDirector.Current.Notify(new DirectorEvent(DirectorEventType.SpawnDenDestroyed, transform.position, 1f, Id));
    }

    private float GetCurrentSpawnInterval(WorldHostilityDirector director)
    {
        float interval = Mathf.Max(0.25f, config != null ? config.baseSpawnInterval : 4f) * Mathf.Max(0.05f, model.intervalMultiplier);
        SpawnDenSpawnLogic logic = GetSpawnLogic();
        if (logic.accelerationRange > 0f && playerTarget != null)
        {
            float distance = Vector2.Distance(transform.position, playerTarget.position);
            if (distance <= logic.accelerationRange)
            {
                interval *= Mathf.Clamp(logic.acceleratedIntervalMultiplier, 0.05f, 1f);
            }
        }

        return (director ?? WorldHostilityDirector.Current).ModifySpawnInterval(interval);
    }

    private int GetEffectiveGlobalLimit(WorldHostilityDirector director)
    {
        if (RunLevelManager.Instance != null)
        {
            return (director ?? WorldHostilityDirector.Current).ModifyEnemyBudget(RunLevelManager.Instance.CurrentEnemyBudget);
        }

        return Mathf.Max(1, globalEnemyLimit);
    }

    private EnemyArchetype PickArchetype(string archetypeOverride)
    {
        if (!string.IsNullOrEmpty(archetypeOverride))
        {
            List<SpawnDenWeightedArchetype> overrideWeights = new List<SpawnDenWeightedArchetype>();
            ParseWeightedArchetypes(archetypeOverride, overrideWeights);
            if (overrideWeights.Count > 0)
            {
                return PickWeighted(overrideWeights);
            }
        }

        return PickWeighted(model.weightedArchetypes);
    }

    private static EnemyArchetype PickWeighted(List<SpawnDenWeightedArchetype> weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            total += Mathf.Max(0f, weights[i].weight);
        }

        if (total <= 0f)
        {
            return EnemyArchetype.Attacker;
        }

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < weights.Count; i++)
        {
            SpawnDenWeightedArchetype entry = weights[i];
            if (roll < entry.weight)
            {
                return entry.archetype;
            }

            roll -= entry.weight;
        }

        return weights[0].archetype;
    }

    private static void ParseWeightedArchetypes(string value, List<SpawnDenWeightedArchetype> results)
    {
        results.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            results.Add(new SpawnDenWeightedArchetype(EnemyArchetype.Attacker, 1f));
            return;
        }

        string[] entries = value.Split(',');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (string.IsNullOrEmpty(entry))
            {
                continue;
            }

            string[] parts = entry.Split(':');
            if (!Enum.TryParse(parts[0].Trim(), true, out EnemyArchetype archetype))
            {
                continue;
            }

            float weight = 1f;
            if (parts.Length > 1)
            {
                if (!float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out weight))
                {
                    weight = 1f;
                }
            }

            if (weight > 0f)
            {
                results.Add(new SpawnDenWeightedArchetype(archetype, weight));
            }
        }

        if (results.Count == 0)
        {
            results.Add(new SpawnDenWeightedArchetype(EnemyArchetype.Attacker, 1f));
        }
    }

    private SpawnDenSpawnLogic GetSpawnLogic()
    {
        if (config == null)
        {
            return new SpawnDenSpawnLogic();
        }

        if (config.spawnLogic == null)
        {
            config.spawnLogic = new SpawnDenSpawnLogic();
        }

        return config.spawnLogic;
    }

    private SpawnDenDestroyLogicConfig GetDestroyLogic()
    {
        if (config == null)
        {
            return new SpawnDenDestroyLogicConfig();
        }

        if (config.destroyLogic == null)
        {
            config.destroyLogic = new SpawnDenDestroyLogicConfig();
        }

        return config.destroyLogic;
    }
}
