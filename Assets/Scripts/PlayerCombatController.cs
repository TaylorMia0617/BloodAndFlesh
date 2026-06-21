using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(EquippedWeaponView))]
public class PlayerCombatController : MonoBehaviour
{
    private enum CombatActionState
    {
        Ready,
        PrimaryAttack,
        SpecialAction
    }

    public enum HotbarSelectionMode
    {
        Weapon,
        Item
    }

    [Header("Weapon")]
    [SerializeField] private WeaponDefinition startingWeapon;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private AttackWaveEffect attackWaveEffect;

    public bool IsAttacking => actionState != CombatActionState.Ready;
    public bool BlocksMovement { get; private set; }
    public bool LocksFacing => IsAttacking || Time.time < facingLockedUntil;

    private PlayerInput playerInput;
    private PlayerInputManager inputManager;
    private EquippedWeaponView weaponView;
    private Rigidbody2D playerBody;
    private CharacterStats stats;
    private SpriteRenderer playerSpriteRenderer;
    private InputAction attackAction;
    private InputAction specialAttackAction;
    private InputAction equipWeaponAction;
    private InputAction[] skillActions;
    private InputAction[] itemActions;
    private InputAction interactAction;
    private WeaponDefinition currentWeapon;
    private SpriteRenderer shieldVisual;
    private TrailRenderer lightningDashTrail;
    private ParticleSystem lightningDashParticles;
    private Coroutine shieldRoutine;
    private float facingLockedUntil;
    private float nextAttackTime;
    private float nextSpecialTime;
    private int lastAttackInputFrame = -1;
    private int lastSpecialInputFrame = -1;
    private Vector2 lastAimDirection = Vector2.right;
    private Vector2 pendingAimDirection = Vector2.right;
    private bool pendingAttackInput;
    private bool pendingSpecialInput;
    private CombatActionState actionState = CombatActionState.Ready;
    private HotbarSelectionMode hotbarSelection = HotbarSelectionMode.Weapon;
    private int selectedItemSlot = -1;

    public float AttackCooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
    public float AttackCooldownDuration => currentWeapon != null ? Mathf.Max(0.001f, currentWeapon.cooldown) : 0.001f;
    public HotbarSelectionMode CurrentHotbarSelection => hotbarSelection;
    public int SelectedItemSlot => selectedItemSlot;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        inputManager = GetComponent<PlayerInputManager>();
        weaponView = GetComponent<EquippedWeaponView>();
        playerBody = GetComponent<Rigidbody2D>();
        stats = GetComponent<CharacterStats>();
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (weaponView == null)
        {
            weaponView = gameObject.AddComponent<EquippedWeaponView>();
        }
        currentWeapon = startingWeapon != null ? startingWeapon : CreateDefaultWeapon();
        EnsureAttackWaveEffect();

        if (playerInput == null)
        {
            Debug.LogError("Player is missing a PlayerInput component.");
            return;
        }

        attackAction = playerInput.actions.FindAction("Attack");
        specialAttackAction = playerInput.actions.FindAction("SpecialAttack");
        equipWeaponAction = playerInput.actions.FindAction("EquipWeapon");
        skillActions = new[]
        {
            playerInput.actions.FindAction("Skill1"),
            playerInput.actions.FindAction("Skill2"),
            playerInput.actions.FindAction("Skill3"),
            playerInput.actions.FindAction("Skill4")
        };
        itemActions = new[]
        {
            playerInput.actions.FindAction("Item1"),
            playerInput.actions.FindAction("Item2"),
            playerInput.actions.FindAction("Item3"),
            playerInput.actions.FindAction("Item4"),
            playerInput.actions.FindAction("Item5")
        };
        interactAction = playerInput.actions.FindAction("Interact");

        if (attackAction == null)
        {
            Debug.LogWarning("PlayerInput actions asset is missing an Attack action.");
        }
        if (specialAttackAction == null)
        {
            Debug.LogWarning("PlayerInput actions asset is missing a SpecialAttack action.");
        }
        if (equipWeaponAction == null)
        {
            Debug.LogWarning("PlayerInput actions asset is missing an EquipWeapon action.");
        }
    }

    private WeaponDefinition CreateDefaultWeapon()
    {
        WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.weaponType = WeaponType.Knife;
        weapon.displayName = "Knife";
        weapon.damage = 10f;
        weapon.armorPiercing = 1.5f;
        weapon.attackRange = 1.2f;
        weapon.attackRadius = 0.45f;
        weapon.cooldown = 0.6f;
        weapon.windup = 0.15f;
        weapon.recovery = 0.2f;
        weapon.description = "Fallback knife.";
        weapon.targetLayers = Physics2D.DefaultRaycastLayers;
        weapon.hideFlags = HideFlags.HideAndDontSave;
        return weapon;
    }

    private void Start()
    {
        if (weaponView == null)
        {
            weaponView = gameObject.AddComponent<EquippedWeaponView>();
        }

        if (weaponView != null)
        {
            weaponView.SetWeapon(currentWeapon);
            weaponView.SetAimDirection(lastAimDirection);
        }
        EnsureAttackWaveEffect();
    }

    private void OnEnable()
    {
        if (attackAction != null)
        {
            attackAction.performed += OnAttack;
        }
        if (specialAttackAction != null)
        {
            specialAttackAction.performed += OnSpecialAttack;
        }
        if (equipWeaponAction != null)
        {
            equipWeaponAction.performed += OnEquipWeapon;
        }
        SubscribeIndexedActions(skillActions, OnSkill);
        SubscribeIndexedActions(itemActions, OnItem);
        if (interactAction != null)
        {
            interactAction.performed += OnInteract;
        }
    }

    private void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.performed -= OnAttack;
        }
        if (specialAttackAction != null)
        {
            specialAttackAction.performed -= OnSpecialAttack;
        }
        if (equipWeaponAction != null)
        {
            equipWeaponAction.performed -= OnEquipWeapon;
        }
        UnsubscribeIndexedActions(skillActions, OnSkill);
        UnsubscribeIndexedActions(itemActions, OnItem);
        if (interactAction != null)
        {
            interactAction.performed -= OnInteract;
        }
    }

    private void Update()
    {
        if (MouseWorldInput.WasPrimaryPressedThisFrame())
        {
            TryAttackInput();
        }

        if (MouseWorldInput.WasSecondaryPressedThisFrame())
        {
            TrySpecialAttackInput();
        }
    }

    public void SetWeapon(WeaponDefinition weapon)
    {
        currentWeapon = weapon != null ? weapon : CreateDefaultWeapon();
        SelectWeaponSlot();
        if (weaponView == null)
        {
            weaponView = gameObject.AddComponent<EquippedWeaponView>();
        }

        if (weaponView != null)
        {
            weaponView.SetWeapon(currentWeapon);
            weaponView.SetAimDirection(lastAimDirection);
        }
        EnsureAttackWaveEffect();

        Debug.Log($"Equipped weapon: {currentWeapon.displayName}");
    }

    public void SetAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            if (LocksFacing)
            {
                pendingAimDirection = direction.normalized;
                return;
            }

            lastAimDirection = direction.normalized;
            pendingAimDirection = lastAimDirection;
            if (weaponView != null)
            {
                weaponView.SetAimDirection(lastAimDirection);
            }
        }
    }

    public void SetPendingAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            pendingAimDirection = direction.normalized;
        }
    }

    private void CommitAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        lastAimDirection = direction.normalized;
        pendingAimDirection = lastAimDirection;
        if (weaponView != null)
        {
            weaponView.SetAimDirection(lastAimDirection);
        }
    }

    private void RefreshPendingAimFromPointer()
    {
        Camera targetCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (targetCamera == null)
        {
            return;
        }

        if (!MouseWorldInput.TryGetWorldPosition(targetCamera, transform.position.z, null, out Vector2 pointerWorldPosition))
        {
            return;
        }

        Vector2 direction = pointerWorldPosition - (Vector2)transform.position;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        pendingAimDirection = direction.normalized;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        TryAttackInput();
    }

    private void OnSpecialAttack(InputAction.CallbackContext context)
    {
        TrySpecialAttackInput();
    }

    private void OnSkill(InputAction.CallbackContext context)
    {
        Debug.Log($"Arcane skill triggered: {context.action.name}");
    }

    private void OnItem(InputAction.CallbackContext context)
    {
        int itemIndex = GetActionIndex(itemActions, context.action);
        if (itemIndex >= 0)
        {
            ToggleItemSlot(itemIndex);
        }
    }

    private void OnEquipWeapon(InputAction.CallbackContext context)
    {
        SelectWeaponSlot();
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        Debug.Log("Interact / pickup triggered.");
    }

    public bool TryAttack()
    {
        return TryAttack(lastAimDirection);
    }

    public bool TryAttack(Vector2 attackDirection)
    {
        if (currentWeapon == null || IsAttacking || Time.time < nextAttackTime)
        {
            return false;
        }

        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatActionState.PrimaryAttack, true);
        StartCoroutine(AttackRoutine(currentWeapon, lastAimDirection));
        return true;
    }

    private bool TryAttackInput()
    {
        if (inputManager != null && !inputManager.canMove)
        {
            return false;
        }

        if (lastAttackInputFrame == Time.frameCount)
        {
            return false;
        }

        lastAttackInputFrame = Time.frameCount;
        RefreshPendingAimFromPointer();
        if (hotbarSelection == HotbarSelectionMode.Item)
        {
            UseSelectedItem(false);
            return true;
        }

        QueueAttack(false);
        return true;
    }

    public bool TrySpecialAttack()
    {
        return TrySpecialAttack(lastAimDirection);
    }

    public bool TrySpecialAttack(Vector2 attackDirection)
    {
        if (currentWeapon == null || IsAttacking || Time.time < nextSpecialTime)
        {
            return false;
        }

        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatActionState.SpecialAction, false);
        switch (currentWeapon.weaponType)
        {
            case WeaponType.Knife:
                nextSpecialTime = Time.time + Mathf.Max(currentWeapon.cooldown, 0.65f);
                StartCoroutine(KnifeBlockRoutine(lastAimDirection));
                return true;
            case WeaponType.Sword:
                nextSpecialTime = Time.time + Mathf.Max(currentWeapon.cooldown, 0.75f);
                StartCoroutine(SwordDashRoutine(lastAimDirection));
                return true;
            case WeaponType.Spear:
                nextSpecialTime = Time.time + Mathf.Max(currentWeapon.cooldown, 1.2f);
                StartCoroutine(SpearSweepRoutine(lastAimDirection));
                return true;
            case WeaponType.Spell:
                return TrySpellShield();
        }

        EndCombatAction();
        return false;
    }

    private bool TrySpecialAttackInput()
    {
        if (inputManager != null && !inputManager.canMove)
        {
            return false;
        }

        if (lastSpecialInputFrame == Time.frameCount)
        {
            return false;
        }

        lastSpecialInputFrame = Time.frameCount;
        RefreshPendingAimFromPointer();
        if (hotbarSelection == HotbarSelectionMode.Item)
        {
            UseSelectedItem(true);
            return true;
        }

        QueueAttack(true);
        return true;
    }

    public void SelectWeaponSlot()
    {
        hotbarSelection = HotbarSelectionMode.Weapon;
        selectedItemSlot = -1;
    }

    public void ToggleItemSlot(int zeroBasedSlot)
    {
        if (zeroBasedSlot < 0 || zeroBasedSlot >= 5)
        {
            return;
        }

        if (hotbarSelection == HotbarSelectionMode.Item && selectedItemSlot == zeroBasedSlot)
        {
            SelectWeaponSlot();
            return;
        }

        hotbarSelection = HotbarSelectionMode.Item;
        selectedItemSlot = zeroBasedSlot;
    }

    private void UseSelectedItem(bool secondaryUse)
    {
        if (selectedItemSlot < 0)
        {
            SelectWeaponSlot();
            return;
        }

        string mode = secondaryUse ? "secondary / throw" : "primary / use";
        Debug.Log($"Item slot {selectedItemSlot + 1} {mode} triggered.");
    }

    private void QueueAttack(bool special)
    {
        if (special)
        {
            if (pendingSpecialInput)
            {
                return;
            }

            pendingSpecialInput = true;
            StartCoroutine(QueuedAttackRoutine(true));
            return;
        }

        if (pendingAttackInput)
        {
            return;
        }

        pendingAttackInput = true;
        StartCoroutine(QueuedAttackRoutine(false));
    }

    private IEnumerator QueuedAttackRoutine(bool special)
    {
        Vector2 attackDirection = pendingAimDirection.sqrMagnitude > 0.001f ? pendingAimDirection.normalized : lastAimDirection;
        if (inputManager != null)
        {
            yield return inputManager.FaceDirectionBeforeAttack(attackDirection);
        }

        CommitAimDirection(attackDirection);
        if (special)
        {
            pendingSpecialInput = false;
            TrySpecialAttack(attackDirection);
        }
        else
        {
            pendingAttackInput = false;
            TryAttack(attackDirection);
        }
    }

    private IEnumerator AttackRoutine(WeaponDefinition weapon, Vector2 attackDirection)
    {
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatActionState.PrimaryAttack, true);
        nextAttackTime = Time.time + weapon.cooldown;
        float windup = GetSyncedWindupTime(weapon);
        float active = GetSyncedActiveTime(weapon);
        float recovery = GetSyncedRecoveryTime(weapon);
        LockFacingFor(windup + active + recovery);
        if (weaponView != null)
        {
            weaponView.PlayAttackAnimation(weapon, attackDirection, windup, active, recovery, false);
        }
        PlayAttackWave(weapon, attackDirection, 1f);
        if (weapon.weaponType == WeaponType.Spear)
        {
            StartCoroutine(SpearLungeRoutine(attackDirection));
        }

        if (windup > 0f)
        {
            yield return new WaitForSeconds(windup);
        }

        BlocksMovement = false;

        if (active > 0f)
        {
            yield return new WaitForSeconds(active);
        }

        if (recovery > 0f)
        {
            yield return new WaitForSeconds(recovery);
        }

        EndCombatAction();
    }

    private void ResolveHit(WeaponDefinition weapon)
    {
        Vector2 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Vector2 center = origin + lastAimDirection * weapon.attackRange;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, weapon.attackRadius, weapon.targetLayers);

        foreach (Collider2D hit in hits)
        {
            foreach (MonoBehaviour behaviour in hit.GetComponents<MonoBehaviour>())
            {
                if (behaviour is IDamageable damageable)
                {
                    damageable.TakeDamage(weapon.damage, weapon.armorPiercing, origin, inputManager != null ? inputManager.GetComponent<CharacterStats>() : null);
                }
            }
        }
    }

    private void PlayAttackWave(WeaponDefinition weapon, float visualMultiplier)
    {
        PlayAttackWave(weapon, lastAimDirection, visualMultiplier);
    }

    private void PlayAttackWave(WeaponDefinition weapon, Vector2 attackDirection, float visualMultiplier)
    {
        EnsureAttackWaveEffect();
        if (attackWaveEffect == null)
        {
            return;
        }

        Vector2 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Vector2 direction = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : Vector2.up;
        attackWaveEffect.Play(weapon, origin, direction, visualMultiplier, weapon.targetLayers);
    }

    private IEnumerator SpearLungeRoutine(Vector2 direction)
    {
        Vector2 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        float duration = 0.14f;
        float distance = 0.56f;
        float elapsed = 0f;
        Vector2 start = playerBody != null ? playerBody.position : (Vector2)transform.position;
        Vector2 end = start + forward * distance;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.Sin(t * Mathf.PI * 0.5f);
            Vector2 nextPosition = Vector2.Lerp(start, end, eased);
            if (playerBody != null)
            {
                playerBody.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            yield return null;
        }
    }

    private IEnumerator KnifeBlockRoutine(Vector2 attackDirection)
    {
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatActionState.SpecialAction, false);
        LockFacingFor(1f);
        if (stats != null)
        {
            stats.AddDamageImmunity(1f);
        }

        ShowShieldVisual(1f, new Color(1f, 0.92f, 0.72f, 0.78f), 1.25f);
        if (weaponView != null)
        {
            weaponView.PlayBlockPose(currentWeapon, attackDirection, 1f);
        }

        yield return new WaitForSeconds(1f);
        EndCombatAction();
    }

    private IEnumerator SwordDashRoutine(Vector2 attackDirection)
    {
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatActionState.SpecialAction, false);
        float duration = 0.22f;
        float distance = 4f;
        LockFacingFor(duration + 0.08f);
        Vector2 forward = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : Vector2.up;
        if (stats != null)
        {
            stats.AddUntargetable(duration + 0.05f);
        }

        Vector2 start = playerBody != null ? playerBody.position : (Vector2)transform.position;
        Vector2 end = GetSwordDashEnd(start, forward, distance);
        StartCoroutine(LightningDashVisualRoutine(duration + 0.05f, start, end));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector2 nextPosition = Vector2.Lerp(start, end, eased);
            if (playerBody != null)
            {
                playerBody.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.08f);
        EndCombatAction();
    }

    private Vector2 GetSwordDashEnd(Vector2 start, Vector2 forward, float distance)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(start, forward, distance);
        float bestDistance = distance;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (!IsDashBlocker(hit))
            {
                continue;
            }

            bestDistance = Mathf.Min(bestDistance, Mathf.Max(0f, hits[i].distance - 0.35f));
        }

        return start + forward * bestDistance;
    }

    private bool IsDashBlocker(Collider2D hit)
    {
        if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            return false;
        }

        if (hit.GetComponent<ObstacleHitFeedback>() != null || hit.GetComponentInParent<ObstacleHitFeedback>() != null)
        {
            return true;
        }

        string objectName = hit.gameObject.name;
        return objectName.StartsWith("Obstacle_") || objectName.StartsWith("Wall_");
    }

    private IEnumerator SpearSweepRoutine(Vector2 attackDirection)
    {
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatActionState.SpecialAction, false);
        WeaponDefinition sweepWeapon = CreateSpearSweepWeapon(currentWeapon);
        float windup = 0.7f;
        float active = 0.42f;
        float recovery = 0.5f;
        LockFacingFor(windup + active + recovery);
        yield return new WaitForSeconds(windup);
        if (inputManager != null)
        {
            inputManager.SetTemporarySpeedMultiplier(1.5f, active);
        }
        PlayAttackWave(sweepWeapon, attackDirection, 1.35f);
        yield return new WaitForSeconds(active + recovery);
        EndCombatAction();
    }

    private bool TrySpellShield()
    {
        if (stats == null)
        {
            stats = GetComponent<CharacterStats>();
        }

        float cost = stats != null ? stats.GetManaCrystalValue(3) * 0.5f : 0f;
        if (stats == null || !stats.TrySpendMana(cost))
        {
            EmitFailedSpecialPulse();
            nextSpecialTime = Time.time + 0.25f;
            EndCombatAction();
            return false;
        }

        nextSpecialTime = Time.time + Mathf.Max(currentWeapon.cooldown, 1.0f);
        StartCoroutine(SpellShieldRoutine());
        return true;
    }

    private IEnumerator SpellShieldRoutine()
    {
        BeginCombatAction(CombatActionState.SpecialAction, false);
        LockFacingFor(0.32f);
        if (stats != null)
        {
            stats.AddDamageImmunity(3f);
        }

        ShowShieldVisual(3f, new Color(0.42f, 0.76f, 1f, 0.72f), 1.45f);
        if (weaponView != null)
        {
            weaponView.PlayAttackAnimation(currentWeapon, lastAimDirection, 0.12f, 0.1f, 0.16f, false);
        }

        yield return new WaitForSeconds(0.32f);
        EndCombatAction();
    }

    private WeaponDefinition CreateSpearSweepWeapon(WeaponDefinition source)
    {
        WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.hideFlags = HideFlags.HideAndDontSave;
        weapon.weaponType = WeaponType.Spear;
        weapon.displayName = "Spear Sweep";
        weapon.weaponSprite = source.weaponSprite;
        weapon.damage = source.damage;
        weapon.armorPiercing = source.armorPiercing;
        weapon.attackRange = Mathf.Max(1.55f, source.attackRange * 0.82f);
        weapon.attackRadius = Mathf.Max(0.35f, source.attackRadius * 1.1f);
        weapon.cooldown = source.cooldown;
        weapon.windup = 0f;
        weapon.recovery = 0.5f;
        weapon.description = "Wide spear sweep.";
        weapon.equippedOffset = source.equippedOffset;
        weapon.equippedScale = source.equippedScale;
        weapon.swingAngle = 360f;
        weapon.thrustDistance = source.thrustDistance * 0.4f;
        weapon.useSweepArc = true;
        weapon.sweepLeftToRight = true;
        weapon.effectOffset = Vector2.zero;
        weapon.targetLayers = source.targetLayers;
        return weapon;
    }

    private void ShowShieldVisual(float duration, Color color, float scale)
    {
        EnsureShieldVisual();
        if (shieldRoutine != null)
        {
            StopCoroutine(shieldRoutine);
        }

        shieldRoutine = StartCoroutine(ShieldVisualRoutine(duration, color, scale));
    }

    private void EnsureShieldVisual()
    {
        if (shieldVisual != null)
        {
            return;
        }

        Transform existing = transform.Find("SpecialShieldVisual");
        GameObject shieldObject = existing != null ? existing.gameObject : new GameObject("SpecialShieldVisual");
        shieldObject.transform.SetParent(transform);
        shieldObject.transform.localPosition = Vector3.zero;
        shieldObject.transform.localRotation = Quaternion.identity;
        shieldVisual = shieldObject.GetComponent<SpriteRenderer>();
        if (shieldVisual == null)
        {
            shieldVisual = shieldObject.AddComponent<SpriteRenderer>();
        }

        shieldVisual.sprite = CreateShieldSprite();
        shieldVisual.sortingOrder = 38;
        shieldVisual.enabled = false;
    }

    private Sprite CreateShieldSprite()
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float ring = Mathf.InverseLerp(31f, 25f, distance) * Mathf.InverseLerp(16f, 22f, distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring)));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private IEnumerator ShieldVisualRoutine(float duration, Color color, float scale)
    {
        shieldVisual.enabled = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float pulse = 1f + Mathf.Sin(Time.time * 18f) * 0.05f;
            shieldVisual.transform.localScale = Vector3.one * scale * pulse;
            Color current = color;
            current.a *= 1f - Mathf.Pow(t, 3f) * 0.45f;
            shieldVisual.color = current;
            yield return null;
        }

        shieldVisual.enabled = false;
        shieldRoutine = null;
    }

    private void EmitFailedSpecialPulse()
    {
        EnsureShieldVisual();
        shieldVisual.enabled = true;
        shieldVisual.color = new Color(0.2f, 0.35f, 0.5f, 0.35f);
        shieldVisual.transform.localScale = Vector3.one * 0.7f;
        StartCoroutine(HideFailedPulse());
    }

    private IEnumerator HideFailedPulse()
    {
        yield return new WaitForSeconds(0.12f);
        if (shieldVisual != null && shieldRoutine == null)
        {
            shieldVisual.enabled = false;
        }
    }

    private float GetSyncedWindupTime(WeaponDefinition weapon)
    {
        if (weapon.weaponType == WeaponType.Sword)
        {
            return 0.035f;
        }

        if (weapon.weaponType == WeaponType.Spell)
        {
            return Mathf.Max(0.05f, weapon.windup * 0.7f);
        }

        return Mathf.Max(0.01f, weapon.windup);
    }

    private float GetSyncedActiveTime(WeaponDefinition weapon)
    {
        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                return 0.13f;
            case WeaponType.Sword:
                return 0.055f;
            case WeaponType.Spear:
                return 0.15f;
            case WeaponType.Spell:
                return 0.48f;
            default:
                return 0.12f;
        }
    }

    private float GetSyncedRecoveryTime(WeaponDefinition weapon)
    {
        if (weapon.weaponType == WeaponType.Sword)
        {
            return 0.035f;
        }

        return weapon.weaponType == WeaponType.Spell ? 0.24f : Mathf.Max(0.04f, weapon.recovery * 0.45f);
    }

    private void LockFacingFor(float duration)
    {
        facingLockedUntil = Mathf.Max(facingLockedUntil, Time.time + Mathf.Max(0f, duration));
    }

    private void BeginCombatAction(CombatActionState state, bool blocksMovement)
    {
        actionState = state;
        BlocksMovement = blocksMovement;
    }

    private void EndCombatAction()
    {
        actionState = CombatActionState.Ready;
        BlocksMovement = false;
    }

    private IEnumerator LightningDashVisualRoutine(float duration, Vector2 start, Vector2 end)
    {
        EnsureLightningDashVisuals();
        Color originalColor = playerSpriteRenderer != null ? playerSpriteRenderer.color : Color.white;
        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.color = new Color(0.45f, 0.95f, 1f, 0.88f);
        }

        if (lightningDashTrail != null)
        {
            lightningDashTrail.Clear();
            lightningDashTrail.emitting = true;
        }

        EmitLightningDashBurst(start, end);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (playerSpriteRenderer != null)
            {
                float pulse = 0.75f + Mathf.Sin(Time.time * 60f) * 0.25f;
                playerSpriteRenderer.color = Color.Lerp(new Color(0.2f, 0.78f, 1f, 0.72f), Color.white, pulse);
            }

            if (lightningDashParticles != null && Random.value < 0.75f)
            {
                EmitLightningParticle(transform.position, Random.insideUnitCircle.normalized * Random.Range(0.7f, 2.3f), Random.Range(0.06f, 0.14f), Random.Range(0.12f, 0.22f));
            }

            yield return null;
        }

        if (lightningDashTrail != null)
        {
            lightningDashTrail.emitting = false;
        }

        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.color = originalColor;
        }
    }

    private void EnsureLightningDashVisuals()
    {
        if (lightningDashTrail == null)
        {
            Transform existingTrail = transform.Find("LightningDashTrail");
            GameObject trailObject = existingTrail != null ? existingTrail.gameObject : new GameObject("LightningDashTrail");
            trailObject.transform.SetParent(transform);
            trailObject.transform.localPosition = Vector3.zero;
            trailObject.transform.localRotation = Quaternion.identity;
            lightningDashTrail = trailObject.GetComponent<TrailRenderer>();
            if (lightningDashTrail == null)
            {
                lightningDashTrail = trailObject.AddComponent<TrailRenderer>();
            }

            lightningDashTrail.material = new Material(Shader.Find("Sprites/Default"));
            lightningDashTrail.time = 0.18f;
            lightningDashTrail.minVertexDistance = 0.015f;
            lightningDashTrail.startWidth = 0.58f;
            lightningDashTrail.endWidth = 0.04f;
            lightningDashTrail.startColor = new Color(0.35f, 0.95f, 1f, 0.92f);
            lightningDashTrail.endColor = new Color(0.9f, 1f, 1f, 0f);
            lightningDashTrail.numCornerVertices = 2;
            lightningDashTrail.numCapVertices = 2;
            lightningDashTrail.sortingOrder = 44;
            lightningDashTrail.emitting = false;
        }

        if (lightningDashParticles == null)
        {
            Transform existingParticles = transform.Find("LightningDashParticles");
            GameObject particleObject = existingParticles != null ? existingParticles.gameObject : new GameObject("LightningDashParticles");
            particleObject.transform.SetParent(transform);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;
            lightningDashParticles = particleObject.GetComponent<ParticleSystem>();
            if (lightningDashParticles == null)
            {
                lightningDashParticles = particleObject.AddComponent<ParticleSystem>();
            }

            ParticleSystem.MainModule main = lightningDashParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 160;
            ParticleSystem.EmissionModule emission = lightningDashParticles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = lightningDashParticles.shape;
            shape.enabled = false;
            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 47;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    private void EmitLightningDashBurst(Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        direction /= distance;
        Vector2 side = new Vector2(-direction.y, direction.x);
        int count = Mathf.Clamp(Mathf.RoundToInt(distance * 18f), 18, 86);
        for (int i = 0; i < count; i++)
        {
            float t = Random.value;
            Vector2 position = Vector2.Lerp(start, end, t) + side * Random.Range(-0.24f, 0.24f);
            Vector2 velocity = -direction * Random.Range(0.25f, 1.6f) + side * Random.Range(-1.4f, 1.4f);
            EmitLightningParticle(position, velocity, Random.Range(0.05f, 0.17f), Random.Range(0.12f, 0.28f));
        }
    }

    private void EmitLightningParticle(Vector2 position, Vector2 velocity, float size, float lifetime)
    {
        if (lightningDashParticles == null)
        {
            return;
        }

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position = position;
        emitParams.velocity = velocity;
        emitParams.startSize = size;
        emitParams.startLifetime = lifetime;
        emitParams.startColor = new Color(0.45f, 0.92f, 1f, 0.9f);
        lightningDashParticles.Emit(emitParams, 1);
    }

    private void EnsureAttackWaveEffect()
    {
        if (attackWaveEffect != null)
        {
            return;
        }

        Transform existing = transform.Find("AttackWave");
        GameObject waveObject = existing != null ? existing.gameObject : new GameObject("AttackWave");
        waveObject.transform.SetParent(transform);
        waveObject.transform.localPosition = Vector3.zero;
        waveObject.transform.localRotation = Quaternion.identity;
        attackWaveEffect = waveObject.GetComponent<AttackWaveEffect>();
        if (attackWaveEffect == null)
        {
            attackWaveEffect = waveObject.AddComponent<AttackWaveEffect>();
        }
    }

    private void PlayAttachedAttackWave(WeaponDefinition weapon, Transform anchor, float visualMultiplier)
    {
        EnsureAttackWaveEffect();
        if (attackWaveEffect == null || anchor == null)
        {
            PlayAttackWave(weapon, visualMultiplier);
            return;
        }

        attackWaveEffect.PlayAttached(weapon, anchor, visualMultiplier, weapon.targetLayers, true, false, true);
    }

    private void SubscribeIndexedActions(InputAction[] actions, System.Action<InputAction.CallbackContext> callback)
    {
        if (actions == null)
        {
            return;
        }

        foreach (InputAction action in actions)
        {
            if (action != null)
            {
                action.performed += callback;
            }
        }
    }

    private void UnsubscribeIndexedActions(InputAction[] actions, System.Action<InputAction.CallbackContext> callback)
    {
        if (actions == null)
        {
            return;
        }

        foreach (InputAction action in actions)
        {
            if (action != null)
            {
                action.performed -= callback;
            }
        }
    }

    private int GetActionIndex(InputAction[] actions, InputAction targetAction)
    {
        if (actions == null || targetAction == null)
        {
            return -1;
        }

        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i] == targetAction)
            {
                return i;
            }
        }

        return -1;
    }
}
