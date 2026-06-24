using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerCombatController))]
[DisallowMultipleComponent]
public class PlayerInputManager : MonoBehaviour
{
    private enum PlayerMoveState
    {
        Idle,
        Moving,
        AttackLocked,
        Disabled
    }

    [Header("Player Settings")]
    [SerializeField] public float moveSpeed = 6f;
    [SerializeField] public float mark = 0.5f;
    [SerializeField] private float turnSpeedDegrees = 1440f;
    [SerializeField] private float attackTurnThresholdDegrees = 3f;
    [SerializeField] private float attackTurnGraceSeconds = 0.04f;
    public bool canMove = true;

    private Rigidbody2D playerRb;
    private PlayerInput playerInput;
    private PlayerCombatController combatController;
    private CharacterStats stats;
    private InputAction moveAction;
    private InputAction aimAction;
    private Camera mainCamera;
    private SpriteRenderer worldCursorRenderer;
    private Vector2 moveInput;
    private Vector2 facingDirection = Vector2.up;
    private float currentSpeed;
    private PlayerMoveState moveState = PlayerMoveState.Idle;
    private Coroutine knockbackRoutine;
    private float externalKnockbackUntil;
    private float speedMultiplier = 1f;
    private float stunnedUntil;
    private StunStatusVisual stunVisual;
    private PlayerBuffPool buffPool;

    public bool IsStunned => Time.time < stunnedUntil;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        combatController = GetComponent<PlayerCombatController>();
        stats = GetComponent<CharacterStats>();
        buffPool = GetComponent<PlayerBuffPool>();
        if (combatController == null)
        {
            combatController = gameObject.AddComponent<PlayerCombatController>();
        }

        if (playerRb == null)
        {
            Debug.LogError("Player prefab is missing a Rigidbody2D component.");
        }

        if (stats != null)
        {
            moveSpeed = stats.moveSpeed;
            mark = stats.moveDelay;
        }

        if (playerInput == null)
        {
            Debug.LogError("Player prefab is missing a PlayerInput component.");
            return;
        }

        moveAction = playerInput.actions.FindAction("Move");
        aimAction = playerInput.actions.FindAction("Aim");
        mainCamera = Camera.main;
        if (moveAction == null)
        {
            Debug.LogError("PlayerInput actions asset is missing a Move action.");
        }
        if (aimAction == null)
        {
            Debug.LogWarning("PlayerInput actions asset is missing an Aim action. Falling back to Mouse.current.position.");
        }
    }

    private void OnEnable()
    {
        if (moveAction == null)
        {
            return;
        }

        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
    }

    private void OnDisable()
    {
        if (moveAction == null)
        {
            return;
        }

        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        StopMovement(PlayerMoveState.Disabled);
    }

    private void Update()
    {
        UpdateAimAndFacing();
    }

    private void FixedUpdate()
    {
        if (playerRb == null)
        {
            return;
        }

        Vector2 inputDir = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        if (!canMove)
        {
            StopMovement(PlayerMoveState.Disabled);
            return;
        }

        if (IsStunned)
        {
            StopMovement(PlayerMoveState.AttackLocked);
            return;
        }

        if (Time.time < externalKnockbackUntil)
        {
            return;
        }

        if (combatController != null && combatController.BlocksMovement)
        {
            HoldMovementLock();
            return;
        }

        ApplyMoveInput(inputDir);

        if (moveState == PlayerMoveState.Moving)
        {
            float accelerationDuration = Mathf.Max(0.001f, mark);
            float targetSpeed = moveSpeed * Mathf.Max(0.01f, speedMultiplier);
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, (targetSpeed / accelerationDuration) * Time.fixedDeltaTime);
            playerRb.velocity = moveInput * currentSpeed;
        }
        else
        {
            currentSpeed = 0f;
            playerRb.velocity = Vector2.zero;
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        ApplyMoveInput(context.ReadValue<Vector2>());
    }

    public void ApplyExternalKnockback(Vector2 hitSource, float distance, float duration)
    {
        if (playerRb == null)
        {
            return;
        }

        Vector2 away = (Vector2)transform.position - hitSource;
        if (away.sqrMagnitude < 0.001f)
        {
            away = facingDirection.sqrMagnitude > 0.001f ? facingDirection : Vector2.up;
        }

        away.Normalize();
        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
        }

        knockbackRoutine = StartCoroutine(ExternalKnockbackRoutine(away, Mathf.Max(0f, distance), Mathf.Max(0.01f, duration)));
    }

    private void ApplyMoveInput(Vector2 inputDir)
    {
        if (!canMove || IsStunned || (combatController != null && combatController.BlocksMovement))
        {
            return;
        }

        if (inputDir != Vector2.zero)
        {
            moveInput = inputDir.normalized;
            moveState = PlayerMoveState.Moving;
        }
        else
        {
            StopMovement(PlayerMoveState.Idle);
        }
    }

    private void UpdateAimAndFacing()
    {
        if (!TryGetPointerWorldPosition(out Vector2 pointerWorldPosition))
        {
            SetWorldCursorVisible(false);
            return;
        }

        UpdateWorldCursor(pointerWorldPosition);
        Vector2 direction = pointerWorldPosition - (Vector2)transform.position;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Vector2 aimDirection = direction.normalized;
        if (combatController != null)
        {
            combatController.SetPendingAimDirection(aimDirection);
        }

        if (combatController != null && combatController.LocksFacing)
        {
            return;
        }

        facingDirection = aimDirection;
        RotateToFacingDirection();
        if (combatController != null)
        {
            combatController.SetAimDirection(transform.up);
        }
    }

    public bool TryFaceDirectionForAttack(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        facingDirection = direction.normalized;
        RotateToFacingDirection();
        return IsFacingTarget(facingDirection, attackTurnThresholdDegrees);
    }

    public IEnumerator FaceDirectionBeforeAttack(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (!TryFaceDirectionForAttack(direction) && elapsed < attackTurnGraceSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public void SetTemporarySpeedMultiplier(float multiplier, float duration)
    {
        StartCoroutine(TemporarySpeedMultiplierRoutine(Mathf.Max(0.01f, multiplier), Mathf.Max(0f, duration)));
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
        StopMovement(PlayerMoveState.AttackLocked);
        EnsureStunVisual();
        stunVisual.Show(duration);
        if (buffPool == null)
        {
            buffPool = GetComponent<PlayerBuffPool>();
        }

        if (buffPool != null)
        {
            buffPool.AddDebuff("stun", "Stun", Resources.Load<Sprite>("Arts/UI/Status/status_stun"), duration);
        }
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

    private bool TryGetPointerWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = Vector2.zero;
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera == null)
        {
            return false;
        }

        return MouseWorldInput.TryGetWorldPosition(mainCamera, transform.position.z, aimAction, out worldPosition);
    }

    private void UpdateWorldCursor(Vector2 worldPosition)
    {
        EnsureWorldCursor();
        worldCursorRenderer.transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z - 0.2f);
        SetWorldCursorVisible(canMove);
    }

    private void SetWorldCursorVisible(bool visible)
    {
        if (worldCursorRenderer != null)
        {
            worldCursorRenderer.enabled = visible;
        }
    }

    private void EnsureWorldCursor()
    {
        if (worldCursorRenderer != null)
        {
            return;
        }

        GameObject cursorObject = new GameObject("WorldMouseCursor");
        worldCursorRenderer = cursorObject.AddComponent<SpriteRenderer>();
        worldCursorRenderer.sprite = CreateCursorSprite();
        worldCursorRenderer.color = new Color(1f, 1f, 1f, 0.92f);
        worldCursorRenderer.sortingOrder = 58;
        cursorObject.transform.localScale = Vector3.one * 0.7f;
    }

    private Sprite CreateCursorSprite()
    {
        Texture2D texture = new Texture2D(9, 9, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        for (int y = 0; y < 9; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                bool vertical = x == 4 && y != 4;
                bool horizontal = y == 4 && x != 4;
                bool center = x >= 3 && x <= 5 && y >= 3 && y <= 5;
                texture.SetPixel(x, y, vertical || horizontal || center ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 9f, 9f), new Vector2(0.5f, 0.5f), 100f);
    }

    private void HoldMovementLock()
    {
        moveState = PlayerMoveState.AttackLocked;
        currentSpeed = 0f;
        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
        }
    }

    private void StopMovement(PlayerMoveState nextState)
    {
        moveInput = Vector2.zero;
        currentSpeed = 0f;
        moveState = nextState;

        if (playerRb != null)
        {
            playerRb.velocity = Vector2.zero;
        }
    }

    private void RotateToFacingDirection()
    {
        float targetAngle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg - 90f;
        float currentAngle = playerRb != null ? playerRb.rotation : transform.eulerAngles.z;
        float angle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeedDegrees * Time.deltaTime);
        if (playerRb != null)
        {
            playerRb.rotation = angle;
        }
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool IsFacingTarget(Vector2 targetDirection, float thresholdDegrees)
    {
        if (targetDirection.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        Vector2 currentUp = transform.up;
        return Vector2.Angle(currentUp, targetDirection.normalized) <= thresholdDegrees;
    }

    private IEnumerator TemporarySpeedMultiplierRoutine(float multiplier, float duration)
    {
        speedMultiplier = Mathf.Max(speedMultiplier, multiplier);
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
    }

    private IEnumerator ExternalKnockbackRoutine(Vector2 direction, float distance, float duration)
    {
        externalKnockbackUntil = Time.time + duration;
        moveState = PlayerMoveState.AttackLocked;
        currentSpeed = 0f;
        playerRb.velocity = Vector2.zero;

        Vector2 start = playerRb.position;
        Vector2 end = start + direction * distance;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            playerRb.MovePosition(Vector2.Lerp(start, end, eased));
            yield return new WaitForFixedUpdate();
        }

        playerRb.velocity = Vector2.zero;
        externalKnockbackUntil = 0f;
        knockbackRoutine = null;
    }
}
