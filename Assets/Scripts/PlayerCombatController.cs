using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(EquippedWeaponView))]
public partial class PlayerCombatController : MonoBehaviour, IWeaponExecutionContext, ILocalFreezable
{
    public enum HotbarSelectionMode
    {
        Weapon,
        Item
    }

    [Header("Weapon")]
    [SerializeField] private WeaponDefinition startingWeapon;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private AttackWaveEffect attackWaveEffect;
    [SerializeField] private float defaultAttackBufferSeconds = 0.16f;
    [SerializeField] private float defaultSpecialBufferSeconds = 0.18f;

    public bool IsAttacking => !combatState.IsReady;
    public bool BlocksMovement => combatState.BlocksMovement;
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
    private IWeaponExecutor weaponExecutor;
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
    private BufferedAttackCommand? bufferedAttack;
    private Coroutine bufferedAttackRoutine;
    private float localHitStopUntil;
    private readonly CombatStateMachine combatState = new CombatStateMachine();
    private HotbarSelectionMode hotbarSelection = HotbarSelectionMode.Weapon;
    private int selectedItemSlot = -1;

    private struct BufferedAttackCommand
    {
        public bool special;
        public Vector2 direction;
        public float expiresAt;
    }

    public float AttackCooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
    public float AttackCooldownDuration => currentWeapon != null ? Mathf.Max(0.001f, currentWeapon.cooldown) : 0.001f;
    public float SpecialCooldownRemaining => Mathf.Max(0f, nextSpecialTime - Time.time);
    public float SpecialCooldownDuration => currentWeapon != null ? Mathf.Max(0.001f, Mathf.Max(currentWeapon.cooldown, WeaponSpecialConfigDatabase.Get(currentWeapon.weaponType).cooldown)) : 0.001f;
    public HotbarSelectionMode CurrentHotbarSelection => hotbarSelection;
    public int SelectedItemSlot => selectedItemSlot;
    private bool IsLocallyHitStopped => Time.unscaledTime < localHitStopUntil;

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
        weaponExecutor = new PrototypeWeaponExecutor();
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
        weapon.physicalDamage = 10f;
        weapon.magicDamage = 0f;
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

        bufferedAttack = null;
        bufferedAttackRoutine = null;
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

        TryConsumeBufferedAttack();
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
        SafeRoomManager safeRoomManager = SafeRoomManager.Instance;
        if (safeRoomManager != null && safeRoomManager.TryInteractCurrent(transform))
        {
            return;
        }

        Debug.Log("Interact / pickup triggered.");
    }

    public bool TryAttack()
    {
        return TryAttack(lastAimDirection);
    }

    public bool TryAttack(Vector2 attackDirection)
    {
        if (currentWeapon == null || IsAttacking || Time.time < nextAttackTime || (inputManager != null && inputManager.IsStunned))
        {
            return false;
        }

        return weaponExecutor.TryExecute(this, currentWeapon, WeaponActionKind.Primary, attackDirection);
    }

    private bool TryAttackInput()
    {
        if (inputManager != null && (!inputManager.canMove || inputManager.IsStunned))
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
        TryConsumeBufferedAttack();
        return true;
    }

    public bool TrySpecialAttack()
    {
        return TrySpecialAttack(lastAimDirection);
    }

    public bool TrySpecialAttack(Vector2 attackDirection)
    {
        if (currentWeapon == null || IsAttacking || Time.time < nextSpecialTime || (inputManager != null && inputManager.IsStunned))
        {
            return false;
        }

        return weaponExecutor.TryExecute(this, currentWeapon, WeaponActionKind.Special, attackDirection);
    }

    private bool TrySpecialAttackInput()
    {
        if (inputManager != null && (!inputManager.canMove || inputManager.IsStunned))
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
        TryConsumeBufferedAttack();
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
        Vector2 attackDirection = pendingAimDirection.sqrMagnitude > 0.001f ? pendingAimDirection.normalized : lastAimDirection;
        float fallbackWindow = special ? defaultSpecialBufferSeconds : defaultAttackBufferSeconds;
        float configuredWindow = currentWeapon != null ? WeaponTiming.GetInputBuffer(currentWeapon, special) : fallbackWindow;
        bufferedAttack = new BufferedAttackCommand
        {
            special = special,
            direction = attackDirection,
            expiresAt = Time.time + Mathf.Max(0.01f, configuredWindow > 0f ? configuredWindow : fallbackWindow)
        };
    }

    private void TryConsumeBufferedAttack()
    {
        if (!bufferedAttack.HasValue || bufferedAttackRoutine != null)
        {
            return;
        }

        BufferedAttackCommand command = bufferedAttack.Value;
        if (Time.time > command.expiresAt)
        {
            bufferedAttack = null;
            return;
        }

        if (!CanConsumeBufferedAttack(command))
        {
            return;
        }

        bufferedAttack = null;
        bufferedAttackRoutine = StartCoroutine(ExecuteBufferedAttackRoutine(command));
    }

    private bool CanConsumeBufferedAttack(BufferedAttackCommand command)
    {
        if (currentWeapon == null || IsAttacking || (inputManager != null && inputManager.IsStunned))
        {
            return false;
        }

        return command.special ? Time.time >= nextSpecialTime : Time.time >= nextAttackTime;
    }

    private IEnumerator ExecuteBufferedAttackRoutine(BufferedAttackCommand command)
    {
        Vector2 attackDirection = command.direction.sqrMagnitude > 0.001f ? command.direction.normalized : lastAimDirection;
        if (inputManager != null)
        {
            yield return inputManager.FaceDirectionBeforeAttack(attackDirection);
        }

        if (inputManager != null && inputManager.IsStunned)
        {
            bufferedAttackRoutine = null;
            yield break;
        }

        CommitAimDirection(attackDirection);
        if (command.special)
        {
            TrySpecialAttack(attackDirection);
        }
        else
        {
            TryAttack(attackDirection);
        }

        bufferedAttackRoutine = null;
    }

    private IEnumerator AttackRoutine(WeaponDefinition weapon, Vector2 attackDirection)
    {
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatPhase.Windup, weapon.blocksMovementDuringAttack);
        nextAttackTime = Time.time + weapon.cooldown;
        float windup = WeaponTiming.GetWindup(weapon);
        float active = WeaponTiming.GetActive(weapon);
        float recovery = WeaponTiming.GetRecovery(weapon);
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

        PublishWeaponSensoryEvents(weapon);

        if (windup > 0f)
        {
            yield return WaitCombatSeconds(windup);
        }

        combatState.SetPhase(CombatPhase.Active);

        if (active > 0f)
        {
            yield return WaitCombatSeconds(active);
        }

        combatState.SetPhase(CombatPhase.Recovery);
        if (recovery > 0f)
        {
            yield return WaitCombatSeconds(recovery);
        }

        EndCombatAction();
        TryConsumeBufferedAttack();
    }

    private IEnumerator WaitCombatSeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsLocallyHitStopped)
            {
                elapsed += Time.deltaTime;
            }

            yield return null;
        }
    }

    public void PushHitStop(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        localHitStopUntil = Mathf.Max(localHitStopUntil, Time.unscaledTime + duration);
        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
        }
    }

    public bool ExecutePrimaryAttack(WeaponDefinition weapon, Vector2 attackDirection)
    {
        if (weapon == null || IsAttacking || Time.time < nextAttackTime)
        {
            return false;
        }

        CommitAimDirection(attackDirection);
        StartCoroutine(AttackRoutine(weapon, lastAimDirection));
        return true;
    }

    public bool ExecuteKnifeBlock(WeaponDefinition weapon, Vector2 attackDirection)
    {
        if (!CanExecuteSpecial(weapon))
        {
            return false;
        }

        CommitAimDirection(attackDirection);
        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(weapon.weaponType);
        nextSpecialTime = Time.time + Mathf.Max(weapon.cooldown, special.cooldown);
        StartCoroutine(KnifeBlockRoutine(lastAimDirection));
        return true;
    }

    public bool ExecuteSwordDash(WeaponDefinition weapon, Vector2 attackDirection)
    {
        if (!CanExecuteSpecial(weapon))
        {
            return false;
        }

        CommitAimDirection(attackDirection);
        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(weapon.weaponType);
        nextSpecialTime = Time.time + Mathf.Max(weapon.cooldown, special.cooldown);
        StartCoroutine(SwordDashRoutine(lastAimDirection));
        return true;
    }

    public bool ExecuteSpearSweep(WeaponDefinition weapon, Vector2 attackDirection)
    {
        if (!CanExecuteSpecial(weapon))
        {
            return false;
        }

        CommitAimDirection(attackDirection);
        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(weapon.weaponType);
        nextSpecialTime = Time.time + Mathf.Max(weapon.cooldown, special.cooldown);
        StartCoroutine(SpearSweepRoutine(lastAimDirection));
        return true;
    }

    public bool ExecuteSpellShield(WeaponDefinition weapon, Vector2 attackDirection)
    {
        if (!CanExecuteSpecial(weapon))
        {
            return false;
        }

        CommitAimDirection(attackDirection);
        return TrySpellShield();
    }

    private bool CanExecuteSpecial(WeaponDefinition weapon)
    {
        return weapon != null && !IsAttacking && Time.time >= nextSpecialTime;
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

    private void LockFacingFor(float duration)
    {
        facingLockedUntil = Mathf.Max(facingLockedUntil, Time.time + Mathf.Max(0f, duration));
    }

    private void BeginCombatAction(CombatPhase state, bool blocksMovement)
    {
        combatState.Enter(state, blocksMovement);
    }

    private void EndCombatAction()
    {
        combatState.Clear();
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

    private void PublishWeaponSensoryEvents(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return;
        }

        float soundRadius = Mathf.Max(3f, weapon.attackRange * 3f);
        SensoryEventBus.Publish(SensoryEventType.Sound, transform.position, soundRadius, 2f);
        if (weapon.weaponType == WeaponType.Spell)
        {
            SensoryEventBus.Publish(SensoryEventType.Magic, transform.position, Mathf.Max(6f, weapon.attackRange * 3.2f), 3f);
        }
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
