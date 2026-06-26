using System.Collections;
using UnityEngine;

public class EquippedWeaponView : MonoBehaviour, ILocalFreezable
{
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private SpriteRenderer enchantRenderer;
    [SerializeField] private SpriteRenderer[] outlineRenderers;
    [SerializeField] private TrailRenderer weaponTrail;
    [SerializeField] private int sortingOrder = 34;
    [SerializeField] private bool showWeaponSpriteInWorld = true;
    [SerializeField] private Transform bladeAnchor;
    [SerializeField] private Transform tipAnchor;

    private WeaponDefinition currentWeapon;
    private Vector2 aimDirection = Vector2.up;
    private Coroutine animationRoutine;
    private Coroutine enchantRoutine;
    private float animationForwardOffset;
    private float animationSideOffset;
    private float animationAngleOffset;
    private Vector2 animationPivotOffset;
    private bool sweepAroundPlayer;
    private float sweepAroundPlayerRadius;
    private float localHitStopUntil;
    private bool IsLocallyHitStopped => Time.unscaledTime < localHitStopUntil;

    public Transform WeaponPivot
    {
        get
        {
            EnsureVisuals();
            return weaponPivot;
        }
    }

    public Transform BladeAnchor
    {
        get
        {
            EnsureVisuals();
            return bladeAnchor;
        }
    }

    public Transform TipAnchor
    {
        get
        {
            EnsureVisuals();
            return tipAnchor;
        }
    }

    private void Awake()
    {
        showWeaponSpriteInWorld = true;
        EnsureVisuals();
    }

    private void LateUpdate()
    {
        if (IsLocallyHitStopped)
        {
            return;
        }

        UpdateWeaponTransform();
    }

    public void SetWeapon(WeaponDefinition weapon)
    {
        EnsureVisuals();
        currentWeapon = weapon;

        if (weaponRenderer == null)
        {
            return;
        }

        weaponRenderer.sprite = currentWeapon != null ? currentWeapon.weaponSprite : null;
        weaponRenderer.enabled = ShouldShowWorldSprite(currentWeapon);
        weaponRenderer.sortingOrder = sortingOrder;
        if (enchantRenderer != null)
        {
            enchantRenderer.sprite = weaponRenderer.sprite;
            enchantRenderer.enabled = false;
        }
        ResetAnimationOffsets();
        UpdateWeaponTransform();
    }

    public void SetAimDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        aimDirection = direction.normalized;
        UpdateWeaponTransform();
    }

    public void PlayAttackAnimation(WeaponDefinition weapon, Vector2 direction)
    {
        PlayAttackAnimation(weapon, direction, -1f, -1f, -1f, false);
    }

    public void PlayAttackAnimation(WeaponDefinition weapon, Vector2 direction, float windupOverride, float strikeOverride, float recoveryOverride, bool forceSweep)
    {
        SetAimDirection(direction);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(AttackAnimationRoutine(weapon, windupOverride, strikeOverride, recoveryOverride, forceSweep));
    }

    public void ShowWeaponEnchant(float duration, Color color)
    {
        EnsureVisuals();
        if (outlineRenderers == null || outlineRenderers.Length == 0)
        {
            return;
        }

        if (enchantRoutine != null)
        {
            StopCoroutine(enchantRoutine);
        }

        enchantRoutine = StartCoroutine(WeaponEnchantRoutine(duration, color));
    }

    public void StopWeaponEnchant()
    {
        if (enchantRoutine != null)
        {
            StopCoroutine(enchantRoutine);
            enchantRoutine = null;
        }

        SetOutlineEnabled(false, Color.clear);
    }

    public void PlayWeaponTrail(float duration, Color color, float width)
    {
        EnsureVisuals();
        if (weaponTrail == null)
        {
            return;
        }

        StartCoroutine(WeaponTrailRoutine(duration, color, width));
    }

    public void PlaySpearSpinAnimation(WeaponDefinition weapon, Vector2 direction, float windup, float active, float recovery)
    {
        SetAimDirection(direction);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(SpearSpinRoutine(weapon, windup, active, recovery));
    }

    public void PlayBlockPose(WeaponDefinition weapon, Vector2 direction, float duration)
    {
        SetAimDirection(direction);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(BlockPoseRoutine(weapon, duration));
    }

    private IEnumerator AttackAnimationRoutine(WeaponDefinition weapon, float windupOverride, float strikeOverride, float recoveryOverride, bool forceSweep)
    {
        if (weapon == null)
        {
            yield break;
        }

        float windup = windupOverride >= 0f ? Mathf.Max(0.01f, windupOverride) : Mathf.Max(0.02f, weapon.windup);
        float strike = strikeOverride >= 0f ? Mathf.Max(0.02f, strikeOverride) : Mathf.Max(0.05f, Mathf.Min(0.12f, weapon.recovery * 0.45f + 0.04f));
        float returnTime = recoveryOverride >= 0f ? Mathf.Max(0.01f, recoveryOverride) : Mathf.Max(0.05f, weapon.recovery - strike);

        bool thrustOnly = !forceSweep && (weapon.weaponType == WeaponType.Spear || weapon.weaponType == WeaponType.Spell);
        float sweepSign = weapon.useSweepArc && weapon.sweepLeftToRight ? -1f : 1f;
        if (weapon.weaponType == WeaponType.Knife && !weapon.useSweepArc)
        {
            sweepSign = -1f;
        }

        if (weapon.weaponType == WeaponType.Knife)
        {
            sweepSign = -1f;
        }

        float swingAngle = weapon.weaponType == WeaponType.Knife ? Mathf.Max(160f, weapon.swingAngle) : weapon.swingAngle;
        float windupAngle = thrustOnly ? 0f : -swingAngle * sweepSign * 0.52f;
        float strikeAngle = thrustOnly ? 0f : swingAngle * sweepSign * 0.62f;
        float strikeForward = thrustOnly
            ? weapon.thrustDistance
            : weapon.thrustDistance * 0.45f;
        animationPivotOffset = weapon.effectOffset;

        bool pureDelayWindup = weapon.weaponType == WeaponType.Knife || weapon.useSweepArc;
        if (pureDelayWindup && windup > 0f)
        {
            yield return WaitLocalSeconds(windup);
        }
        else
        {
            yield return AnimateOffsets(windup, -weapon.thrustDistance * 0.12f, -weapon.effectOffset.y * 0.45f, windupAngle);
        }

        yield return AnimateOffsets(strike, strikeForward, weapon.effectOffset.y * 0.25f, strikeAngle);
        yield return AnimateOffsets(returnTime, 0f, 0f, 0f);

        ResetAnimationOffsets();
        animationRoutine = null;
    }

    private IEnumerator SpearSpinRoutine(WeaponDefinition weapon, float windup, float active, float recovery)
    {
        if (weapon == null)
        {
            yield break;
        }

        animationPivotOffset = Vector2.zero;
        sweepAroundPlayer = false;
        float delay = Mathf.Max(0f, windup);
        if (delay > 0f)
        {
            yield return WaitLocalSeconds(delay);
        }

        sweepAroundPlayer = true;
        sweepAroundPlayerRadius = Mathf.Max(0.52f, weapon.equippedOffset.x + 0.18f);
        float elapsed = 0f;
        while (elapsed < active)
        {
            if (IsLocallyHitStopped)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, active));
            animationAngleOffset = Mathf.Lerp(0f, -360f, t);
            animationForwardOffset = 0f;
            animationSideOffset = 0f;
            UpdateWeaponTransform();
            yield return null;
        }

        sweepAroundPlayer = false;
        ResetAnimationOffsets();
        if (recovery > 0f)
        {
            yield return WaitLocalSeconds(recovery);
        }
        ResetAnimationOffsets();
        animationRoutine = null;
    }

    private IEnumerator BlockPoseRoutine(WeaponDefinition weapon, float duration)
    {
        if (weapon == null)
        {
            yield break;
        }

        animationPivotOffset = weapon.effectOffset;
        yield return AnimateOffsets(0.08f, weapon.thrustDistance * 0.18f, 0.18f, -72f);

        float elapsed = 0f;
        float holdTime = Mathf.Max(0f, duration - 0.16f);
        while (elapsed < holdTime)
        {
            if (IsLocallyHitStopped)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            animationAngleOffset = -72f + Mathf.Sin(Time.time * 18f) * 2.5f;
            UpdateWeaponTransform();
            yield return null;
        }

        yield return AnimateOffsets(0.08f, 0f, 0f, 0f);
        ResetAnimationOffsets();
        animationRoutine = null;
    }

    private IEnumerator AnimateOffsets(float duration, float targetForwardOffset, float targetSideOffset, float targetAngleOffset)
    {
        if (duration <= 0f)
        {
            animationForwardOffset = targetForwardOffset;
            animationSideOffset = targetSideOffset;
            animationAngleOffset = targetAngleOffset;
            UpdateWeaponTransform();
            yield break;
        }

        float startForward = animationForwardOffset;
        float startSide = animationSideOffset;
        float startAngle = animationAngleOffset;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (IsLocallyHitStopped)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            animationForwardOffset = Mathf.Lerp(startForward, targetForwardOffset, t);
            animationSideOffset = Mathf.Lerp(startSide, targetSideOffset, t);
            animationAngleOffset = Mathf.Lerp(startAngle, targetAngleOffset, t);
            UpdateWeaponTransform();
            yield return null;
        }
    }

    private IEnumerator WaitLocalSeconds(float duration)
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
    }

    private void UpdateWeaponTransform()
    {
        if (weaponPivot == null || currentWeapon == null)
        {
            return;
        }

        Vector2 forward = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.up;
        Vector2 side = new Vector2(-forward.y, forward.x);
        float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;
        float angle = baseAngle + animationAngleOffset;
        Vector2 offset;
        if (sweepAroundPlayer)
        {
            Vector2 spinForward = Quaternion.Euler(0f, 0f, animationAngleOffset) * forward;
            offset = spinForward.normalized * sweepAroundPlayerRadius;
            angle = Mathf.Atan2(spinForward.y, spinForward.x) * Mathf.Rad2Deg - 90f;
        }
        else
        {
            offset = forward * (currentWeapon.equippedOffset.x + animationPivotOffset.x + animationForwardOffset)
                + side * (currentWeapon.equippedOffset.y + animationPivotOffset.y + animationSideOffset);
        }

        weaponPivot.position = (Vector2)transform.position + offset;
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        weaponPivot.localScale = Vector3.one * Mathf.Max(0.01f, currentWeapon.equippedScale);
        if (weaponRenderer != null)
        {
            weaponRenderer.sprite = currentWeapon.weaponSprite;
            weaponRenderer.enabled = ShouldShowWorldSprite(currentWeapon);
            weaponRenderer.sortingOrder = sortingOrder;
        }

        if (enchantRenderer != null && weaponRenderer != null)
        {
            enchantRenderer.sprite = weaponRenderer.sprite;
        }

        UpdateAnchors();
    }

    private bool ShouldShowWorldSprite(WeaponDefinition weapon)
    {
        if (!showWeaponSpriteInWorld || weapon == null || weapon.weaponSprite == null)
        {
            return false;
        }

        return true;
    }

    private void EnsureVisuals()
    {
        if (weaponPivot == null)
        {
            Transform existingPivot = transform.Find("WeaponPivot");
            weaponPivot = existingPivot != null ? existingPivot : new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform);
        }

        if (weaponRenderer == null)
        {
            Transform existingWeapon = weaponPivot.Find("EquippedWeapon");
            GameObject weaponObject = existingWeapon != null ? existingWeapon.gameObject : new GameObject("EquippedWeapon");
            weaponObject.transform.SetParent(weaponPivot);
            weaponObject.transform.localPosition = Vector3.zero;
            weaponObject.transform.localRotation = Quaternion.identity;
            weaponObject.transform.localScale = Vector3.one;
            weaponRenderer = weaponObject.GetComponent<SpriteRenderer>();
            if (weaponRenderer == null)
            {
                weaponRenderer = weaponObject.AddComponent<SpriteRenderer>();
            }
        }

        if (enchantRenderer == null)
        {
            Transform existingEnchant = weaponPivot.Find("WeaponEnchant");
            GameObject enchantObject = existingEnchant != null ? existingEnchant.gameObject : new GameObject("WeaponEnchant");
            enchantObject.transform.SetParent(weaponPivot);
            enchantObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            enchantObject.transform.localRotation = Quaternion.identity;
            enchantObject.transform.localScale = Vector3.one;
            enchantRenderer = enchantObject.GetComponent<SpriteRenderer>();
            if (enchantRenderer == null)
            {
                enchantRenderer = enchantObject.AddComponent<SpriteRenderer>();
            }

            enchantRenderer.enabled = false;
        }

        if (enchantRenderer != null && weaponRenderer != null)
        {
            enchantRenderer.sprite = weaponRenderer.sprite;
            enchantRenderer.sortingOrder = sortingOrder + 1;
            enchantRenderer.enabled = false;
        }

        EnsureOutlineRenderers();
        EnsureTrailRenderer();

        bladeAnchor = EnsureChildAnchor("BladeAnchor", new Vector3(0f, 0.42f, 0f), weaponPivot);
        tipAnchor = EnsureChildAnchor("TipAnchor", new Vector3(0f, 0.92f, 0f), weaponPivot);
    }

    private Transform EnsureChildAnchor(string anchorName, Vector3 localPosition, Transform parent)
    {
        Transform existing = parent.Find(anchorName);
        Transform anchor = existing != null ? existing : new GameObject(anchorName).transform;
        anchor.SetParent(parent);
        anchor.localPosition = localPosition;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        return anchor;
    }

    private void UpdateAnchors()
    {
        if (currentWeapon == null || bladeAnchor == null || tipAnchor == null)
        {
            return;
        }

        float spriteHalfLength = Mathf.Max(0.42f, 0.5f / Mathf.Max(0.01f, currentWeapon.equippedScale));
        switch (currentWeapon.weaponType)
        {
            case WeaponType.Knife:
                bladeAnchor.localPosition = new Vector3(0f, spriteHalfLength * 0.46f, 0f);
                tipAnchor.localPosition = new Vector3(0f, spriteHalfLength * 0.88f, 0f);
                break;
            case WeaponType.Spear:
                bladeAnchor.localPosition = new Vector3(0f, spriteHalfLength * 0.58f, 0f);
                tipAnchor.localPosition = new Vector3(0f, spriteHalfLength * 0.96f, 0f);
                break;
            default:
                bladeAnchor.localPosition = new Vector3(0f, spriteHalfLength * 0.45f, 0f);
                tipAnchor.localPosition = new Vector3(0f, spriteHalfLength * 0.82f, 0f);
                break;
        }
    }

    private void ResetAnimationOffsets()
    {
        animationForwardOffset = 0f;
        animationSideOffset = 0f;
        animationAngleOffset = 0f;
        animationPivotOffset = Vector2.zero;
        sweepAroundPlayer = false;
        sweepAroundPlayerRadius = 0f;
    }

    private IEnumerator WeaponEnchantRoutine(float duration, Color color)
    {
        SetOutlineEnabled(true, color);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            Color current = color;
            current.a *= Mathf.Lerp(1f, 0.18f, Mathf.Pow(t, 3f));
            SetOutlineColor(current);
            yield return null;
        }

        SetOutlineEnabled(false, color);
        enchantRoutine = null;
    }

    private IEnumerator WeaponTrailRoutine(float duration, Color color, float width)
    {
        weaponTrail.startColor = color;
        Color end = color;
        end.a = 0f;
        weaponTrail.endColor = end;
        weaponTrail.startWidth = width;
        weaponTrail.endWidth = width * 0.12f;
        weaponTrail.Clear();
        weaponTrail.emitting = true;
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        weaponTrail.emitting = false;
    }

    private void EnsureOutlineRenderers()
    {
        if (outlineRenderers != null && outlineRenderers.Length == 8)
        {
            return;
        }

        outlineRenderers = new SpriteRenderer[8];
        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            string childName = $"WeaponEnchantOutline_{i}";
            Transform existing = weaponPivot.Find(childName);
            GameObject outlineObject = existing != null ? existing.gameObject : new GameObject(childName);
            outlineObject.transform.SetParent(weaponPivot);
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;
            SpriteRenderer renderer = outlineObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = outlineObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = weaponRenderer != null ? weaponRenderer.sprite : null;
            renderer.sortingOrder = sortingOrder + 1;
            renderer.enabled = false;
            outlineRenderers[i] = renderer;
        }

        UpdateOutlineOffsets();
    }

    private void EnsureTrailRenderer()
    {
        if (weaponTrail != null)
        {
            return;
        }

        Transform existing = tipAnchor != null ? tipAnchor.Find("WeaponTrail") : null;
        GameObject trailObject = existing != null ? existing.gameObject : new GameObject("WeaponTrail");
        trailObject.transform.SetParent(tipAnchor != null ? tipAnchor : weaponPivot);
        trailObject.transform.localPosition = Vector3.zero;
        trailObject.transform.localRotation = Quaternion.identity;
        trailObject.transform.localScale = Vector3.one;
        weaponTrail = trailObject.GetComponent<TrailRenderer>();
        if (weaponTrail == null)
        {
            weaponTrail = trailObject.AddComponent<TrailRenderer>();
        }

        weaponTrail.material = new Material(Shader.Find("Sprites/Default"));
        weaponTrail.time = 0.16f;
        weaponTrail.minVertexDistance = 0.02f;
        weaponTrail.numCornerVertices = 3;
        weaponTrail.numCapVertices = 3;
        weaponTrail.sortingOrder = sortingOrder + 2;
        weaponTrail.emitting = false;
    }

    private void SetOutlineEnabled(bool enabled, Color color)
    {
        if (outlineRenderers == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in outlineRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.sprite = weaponRenderer != null ? weaponRenderer.sprite : null;
            renderer.color = color;
            renderer.enabled = enabled && renderer.sprite != null;
        }

        if (enabled)
        {
            UpdateOutlineOffsets();
        }
    }

    private void SetOutlineColor(Color color)
    {
        if (outlineRenderers == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in outlineRenderers)
        {
            if (renderer != null)
            {
                renderer.color = color;
            }
        }
    }

    private void UpdateOutlineOffsets()
    {
        if (outlineRenderers == null)
        {
            return;
        }

        Vector2[] offsets = GetEnchantOffsets();
        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            if (outlineRenderers[i] == null)
            {
                continue;
            }

            Vector2 offset = offsets[Mathf.Min(i, offsets.Length - 1)];
            outlineRenderers[i].transform.localPosition = offset;
            outlineRenderers[i].transform.localScale = Vector3.one;
        }
    }

    private Vector2[] GetEnchantOffsets()
    {
        if (currentWeapon == null)
        {
            return new[] { Vector2.right * 0.03f };
        }

        switch (currentWeapon.weaponType)
        {
            case WeaponType.Knife:
                return new[]
                {
                    new Vector2(0.035f, 0.02f),
                    new Vector2(0.055f, 0.11f),
                    new Vector2(0.048f, 0.22f),
                    new Vector2(0.028f, 0.34f),
                    new Vector2(0.018f, 0.46f),
                    new Vector2(0.012f, 0.58f),
                    new Vector2(0.006f, 0.7f),
                    new Vector2(0.04f, 0.78f)
                };
            case WeaponType.Spear:
                return new[]
                {
                    new Vector2(0.018f, 0.72f),
                    new Vector2(-0.018f, 0.72f),
                    new Vector2(0.028f, 0.84f),
                    new Vector2(-0.028f, 0.84f),
                    new Vector2(0.012f, 0.96f),
                    new Vector2(-0.012f, 0.96f),
                    new Vector2(0.008f, 0.58f),
                    new Vector2(-0.008f, 0.58f)
                };
            default:
                return new[]
                {
                    Vector2.up * 0.028f,
                    Vector2.down * 0.028f,
                    Vector2.left * 0.028f,
                    Vector2.right * 0.028f,
                    new Vector2(0.02f, 0.02f),
                    new Vector2(-0.02f, 0.02f),
                    new Vector2(0.02f, -0.02f),
                    new Vector2(-0.02f, -0.02f)
                };
        }
    }
}
