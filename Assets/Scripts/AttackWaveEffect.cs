using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class AttackWaveEffect : MonoBehaviour
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ShapeId = Shader.PropertyToID("_Shape");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int ExpandId = Shader.PropertyToID("_Expand");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private PolygonCollider2D waveCollider;
    private Material material;
    private ParticleSystem vfxParticles;
    private CharacterStats attackerStats;
    private Coroutine playRoutine;
    private WeaponDefinition activeWeapon;
    private LayerMask activeTargetLayers;
    private Vector2 projectileStart;
    private Vector2 projectileDirection;
    private float projectileDistance;
    private float projectileRadius;
    private bool projectileHitSolid;
    private Transform followAnchor;
    private bool followAnchorPosition;
    private bool attachedWeaponBodyCollider;
    private bool renderAttachedVisual;
    private float attachedColliderBottom;
    private float attachedColliderTop;
    private float attachedColliderHalfWidth;
    private float sweepInnerRadius;
    private float sweepOuterRadius;
    private readonly HashSet<Collider2D> damagedColliders = new HashSet<Collider2D>();

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        waveCollider = GetComponent<PolygonCollider2D>();
        waveCollider.isTrigger = true;
        attackerStats = GetComponentInParent<CharacterStats>();
        EnsureParticleSystem();
        gameObject.SetActive(false);
    }

    public void Play(WeaponDefinition weapon, Vector2 origin, Vector2 direction, float visualMultiplier, LayerMask targetLayers)
    {
        if (weapon == null)
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }
        meshRenderer.enabled = true;
        waveCollider.enabled = false;
        activeWeapon = null;
        attachedWeaponBodyCollider = false;
        renderAttachedVisual = true;
        damagedColliders.Clear();
        SetFade(0f);
        SetExpand(0f);

        Vector2 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        Vector2 side = new Vector2(-forward.y, forward.x);
        float range = GetVisualRange(weapon, visualMultiplier);
        float width = GetVisualWidth(weapon, visualMultiplier);
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;
        Vector2 effectOrigin = origin + forward * weapon.effectOffset.x + side * weapon.effectOffset.y;
        WeaponType visualType = weapon.useSweepArc ? WeaponType.Knife : weapon.weaponType;

        transform.position = effectOrigin;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        ConfigureMesh(weapon, visualType, width, range);
        ConfigureMaterial(visualType, visualMultiplier);
        ConfigureCollider(weapon, visualType, width, range);
        ConfigureParticleDefaults(visualType);
        gameObject.SetActive(true);
        playRoutine = StartCoroutine(PlayRoutine(weapon, targetLayers, effectOrigin, forward, range));
    }

    public void PlayAttached(WeaponDefinition weapon, Transform anchor, float visualMultiplier, LayerMask targetLayers, bool followPosition = true, bool renderVisual = false, bool useWeaponBodyCollider = true)
    {
        if (weapon == null || anchor == null)
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        waveCollider.enabled = false;
        activeWeapon = null;
        attachedWeaponBodyCollider = useWeaponBodyCollider;
        renderAttachedVisual = renderVisual;
        damagedColliders.Clear();
        SetFade(0f);
        SetExpand(0f);

        followAnchor = anchor;
        followAnchorPosition = followPosition;
        float range = GetVisualRange(weapon, visualMultiplier);
        float width = GetVisualWidth(weapon, visualMultiplier);
        WeaponType visualType = weapon.useSweepArc ? WeaponType.Knife : weapon.weaponType;
        SyncToFollowAnchor();
        ConfigureMesh(weapon, visualType, width, range);
        ConfigureMaterial(visualType, visualMultiplier);
        ConfigureCollider(weapon, visualType, width, range);
        meshRenderer.enabled = renderAttachedVisual;
        ConfigureParticleDefaults(visualType);
        gameObject.SetActive(true);
        playRoutine = StartCoroutine(PlayAttachedRoutine(weapon, targetLayers, range));
    }

    private IEnumerator PlayRoutine(WeaponDefinition weapon, LayerMask targetLayers, Vector2 origin, Vector2 direction, float range)
    {
        SetProgress(0f);
        float windup = GetWindupTime(weapon);
        float activeTime = GetActiveTime(weapon);
        float fadeTime = GetFadeTime(weapon);
        activeWeapon = weapon;
        activeTargetLayers = targetLayers;
        damagedColliders.Clear();

        waveCollider.enabled = false;
        yield return AnimateProgress(0f, 0.18f, windup);

        if (weapon.weaponType == WeaponType.Spell)
        {
            projectileStart = origin + direction * 0.45f;
            projectileDirection = direction;
            projectileDistance = range;
            projectileRadius = Mathf.Max(0.12f, GetVisualWidth(weapon, 1f) * 0.45f);
            projectileHitSolid = false;
            transform.position = projectileStart;
        }

        waveCollider.enabled = true;
        Physics2D.SyncTransforms();
        bool immediateHit = DamageOverlappingTargets(weapon, targetLayers);
        EmitReleaseParticles(weapon.weaponType, range);
        if (weapon.weaponType == WeaponType.Spell)
        {
            if (!immediateHit)
            {
                yield return AnimateProjectile(activeTime);
            }
            waveCollider.enabled = false;
            activeWeapon = null;
            damagedColliders.Clear();
            EmitImpactParticles(weapon.weaponType);
            yield return AnimateSpellDissolve(fadeTime);
            gameObject.SetActive(false);
            playRoutine = null;
            yield break;
        }
        else
        {
            yield return AnimateProgress(0.18f, 0.92f, activeTime);
        }

        waveCollider.enabled = false;
        activeWeapon = null;
        damagedColliders.Clear();
        EmitImpactParticles(weapon.weaponType);
        yield return AnimateProgress(0.92f, 1f, fadeTime);

        gameObject.SetActive(false);
        playRoutine = null;
    }

    private IEnumerator PlayAttachedRoutine(WeaponDefinition weapon, LayerMask targetLayers, float range)
    {
        SetProgress(0.18f);
        activeWeapon = weapon;
        activeTargetLayers = targetLayers;
        damagedColliders.Clear();

        float windup = GetWindupTime(weapon);
        float activeTime = GetActiveTime(weapon);
        float fadeTime = GetFadeTime(weapon);

        waveCollider.enabled = false;
        float elapsed = 0f;
        while (elapsed < windup)
        {
            elapsed += Time.deltaTime;
            SyncToFollowAnchor();
            SetProgress(Mathf.Lerp(0f, 0.18f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, windup))));
            yield return null;
        }

        waveCollider.enabled = true;
        if (renderAttachedVisual)
        {
            EmitReleaseParticles(weapon.weaponType, range);
        }
        elapsed = 0f;
        while (elapsed < activeTime)
        {
            elapsed += Time.deltaTime;
            SyncToFollowAnchor();
            Physics2D.SyncTransforms();
            DamageOverlappingTargets(weapon, targetLayers);
            SetProgress(Mathf.Lerp(0.18f, 0.92f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, activeTime))));
            yield return null;
        }

        waveCollider.enabled = false;
        activeWeapon = null;
        if (renderAttachedVisual)
        {
            EmitImpactParticles(weapon.weaponType);
        }
        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            SyncToFollowAnchor();
            SetProgress(Mathf.Lerp(0.92f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fadeTime))));
            yield return null;
        }

        damagedColliders.Clear();
        followAnchor = null;
        gameObject.SetActive(false);
        playRoutine = null;
    }

    private void SyncToFollowAnchor()
    {
        if (followAnchor == null)
        {
            return;
        }

        if (followAnchorPosition)
        {
            transform.position = followAnchor.position;
        }

        transform.rotation = followAnchor.rotation;
        transform.localScale = followAnchor.lossyScale;
    }

    private IEnumerator AnimateSpellDissolve(float duration)
    {
        if (duration <= 0f)
        {
            SetExpand(1f);
            SetFade(1f);
            yield break;
        }

        SetProgress(1f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            SetExpand(eased);
            SetFade(t);
            yield return null;
        }
    }

    private IEnumerator AnimateProjectile(float duration)
    {
        if (duration <= 0f)
        {
            Vector2 endPosition = projectileStart + projectileDirection * projectileDistance;
            SweepSpellProjectile(projectileStart, endPosition);
            SetProgress(1f);
            yield break;
        }

        float elapsed = 0f;
        Vector2 previousPosition = transform.position;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector2 nextPosition = projectileStart + projectileDirection * (projectileDistance * eased);
            if (SweepSpellProjectile(previousPosition, nextPosition))
            {
                SetProgress(eased);
                yield break;
            }

            transform.position = nextPosition;
            previousPosition = nextPosition;
            SetProgress(eased);
            EmitSpellTrailParticle();
            yield return null;
        }
    }

    private IEnumerator AnimateProgress(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetProgress(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetProgress(Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f)));
            yield return null;
        }
    }

    private bool DamageOverlappingTargets(WeaponDefinition weapon, LayerMask targetLayers)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = targetLayers;
        filter.useTriggers = true;

        bool damagedAny = false;
        Collider2D[] results = new Collider2D[32];
        int hitCount = waveCollider.OverlapCollider(filter, results);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = results[i];
            if (hit == null)
            {
                continue;
            }

            damagedAny |= DamageCollider(hit, weapon);
        }

        return damagedAny;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activeWeapon == null || ((1 << other.gameObject.layer) & activeTargetLayers.value) == 0)
        {
            return;
        }

        DamageCollider(other, activeWeapon);
    }

    private bool DamageCollider(Collider2D hit, WeaponDefinition weapon)
    {
        if (hit == null || damagedColliders.Contains(hit))
        {
            return false;
        }

        foreach (MonoBehaviour behaviour in hit.GetComponents<MonoBehaviour>())
        {
            if (behaviour is IDamageable damageable)
            {
                if (attackerStats == null)
                {
                    attackerStats = GetComponentInParent<CharacterStats>();
                }

                bool spearTipHit = IsAttachedSpearTipHit(hit, weapon);
                float damage = weapon.damage * GetAttachedHitDamageMultiplier(hit, weapon);
                damageable.TakeDamage(damage, weapon.armorPiercing, transform.position, attackerStats);
                damagedColliders.Add(hit);
                if (weapon.weaponType == WeaponType.Sword)
                {
                    EmitSwordHitBurst(GetHitPoint(hit));
                }
                else if (weapon.weaponType == WeaponType.Spear && weapon.useSweepArc)
                {
                    EmitSpearSweepHitBurst(GetHitPoint(hit), spearTipHit);
                }
                else
                {
                    EmitGenericHitBurst(GetHitPoint(hit), weapon.weaponType);
                }
                return true;
            }
        }

        return false;
    }

    private bool SweepSpellProjectile(Vector2 from, Vector2 to)
    {
        if (projectileHitSolid || activeWeapon == null)
        {
            return projectileHitSolid;
        }

        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            transform.position = to;
            return DamageSpellOverlap(to);
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = activeTargetLayers;
        filter.useTriggers = true;

        RaycastHit2D[] results = new RaycastHit2D[32];
        int hitCount = Physics2D.CircleCast(from, projectileRadius, delta / distance, filter, results, distance);
        float bestDistance = float.MaxValue;
        Collider2D bestHit = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = results[i].collider;
            if (!IsValidProjectileHit(hit))
            {
                continue;
            }

            if (!IsProjectileBlocker(hit) && !HasDamageable(hit))
            {
                continue;
            }

            float hitDistance = results[i].distance;
            if (hitDistance < bestDistance)
            {
                bestDistance = hitDistance;
                bestHit = hit;
            }
        }

        if (bestHit == null)
        {
            transform.position = to;
            return false;
        }

        Vector2 hitPoint = from + delta.normalized * bestDistance;
        transform.position = hitPoint;
        DamageCollider(bestHit, activeWeapon);
        projectileHitSolid = true;
        return true;
    }

    private bool DamageSpellOverlap(Vector2 position)
    {
        Collider2D[] results = Physics2D.OverlapCircleAll(position, projectileRadius, activeTargetLayers);
        float bestDistance = float.MaxValue;
        Collider2D bestHit = null;
        for (int i = 0; i < results.Length; i++)
        {
            Collider2D hit = results[i];
            if (!IsValidProjectileHit(hit))
            {
                continue;
            }

            if (!IsProjectileBlocker(hit) && !HasDamageable(hit))
            {
                continue;
            }

            float distance = Vector2.Distance(position, hit.ClosestPoint(position));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestHit = hit;
            }
        }

        if (bestHit == null)
        {
            return false;
        }

        DamageCollider(bestHit, activeWeapon);
        projectileHitSolid = true;
        return true;
    }

    private bool IsValidProjectileHit(Collider2D hit)
    {
        if (hit == null || hit == waveCollider)
        {
            return false;
        }

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
        {
            return false;
        }

        return true;
    }

    private bool IsProjectileBlocker(Collider2D hit)
    {
        if (hit.GetComponent<ObstacleHitFeedback>() != null || hit.GetComponentInParent<ObstacleHitFeedback>() != null)
        {
            return true;
        }

        string objectName = hit.gameObject.name;
        return objectName.StartsWith("Obstacle_") || objectName.StartsWith("Wall_");
    }

    private bool HasDamageable(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        foreach (MonoBehaviour behaviour in hit.GetComponents<MonoBehaviour>())
        {
            if (behaviour is IDamageable)
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 GetHitPoint(Collider2D hit)
    {
        Vector2 origin = transform.position;
        Vector2 hitPoint = hit.ClosestPoint(origin);
        if ((hitPoint - origin).sqrMagnitude < 0.0001f)
        {
            hitPoint = transform.TransformPoint(Vector2.up * 0.85f);
        }

        return hitPoint;
    }

    private void ConfigureMesh(WeaponDefinition weapon, WeaponType weaponType, float width, float range)
    {
        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "AttackWaveMesh";
            meshFilter.sharedMesh = mesh;
        }

        float halfWidth = width * 0.5f;
        mesh.Clear();
        if (weapon != null && weapon.weaponType == WeaponType.Spear && weapon.useSweepArc)
        {
            ConfigureSpearSweepRingMesh(mesh, range, width);
            return;
        }

        if (weaponType == WeaponType.Knife)
        {
            ConfigureKnifeCrescentMesh(mesh, range, width);
            return;
        }

        if (weaponType == WeaponType.Spell)
        {
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfWidth, 0f),
                new Vector3(halfWidth, -halfWidth, 0f),
                new Vector3(-halfWidth, halfWidth, 0f),
                new Vector3(halfWidth, halfWidth, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return;
        }

        ConfigureThrustMesh(mesh, weaponType, width, range);
        mesh.RecalculateBounds();
    }

    private void ConfigureKnifeCrescentMesh(Mesh mesh, float range, float width)
    {
        int segments = 42;
        float outerRadius = Mathf.Max(1.08f, range * 1.22f);
        float innerRadius = Mathf.Max(0.2f, range * 0.22f);
        float halfAngle = Mathf.Clamp(width * 50f, 78f, 104f) * Mathf.Deg2Rad;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            int baseIndex = i * 2;
            float edgeTaper = Mathf.Sin(t * Mathf.PI);
            float innerCut = innerRadius * (0.62f + edgeTaper * 0.62f);
            float outerBulge = outerRadius * (0.76f + edgeTaper * 0.34f);
            Vector2 biteOffset = new Vector2(0f, -range * 0.08f);
            vertices[baseIndex] = direction * innerCut + biteOffset;
            vertices[baseIndex + 1] = direction * outerBulge;
            uvs[baseIndex] = new Vector2(t, 0f);
            uvs[baseIndex + 1] = new Vector2(t, 1f);

            if (i >= segments)
            {
                continue;
            }

            int triangle = i * 6;
            triangles[triangle] = baseIndex;
            triangles[triangle + 1] = baseIndex + 1;
            triangles[triangle + 2] = baseIndex + 2;
            triangles[triangle + 3] = baseIndex + 1;
            triangles[triangle + 4] = baseIndex + 3;
            triangles[triangle + 5] = baseIndex + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void ConfigureSpearSweepRingMesh(Mesh mesh, float range, float width)
    {
        int segments = 72;
        float outerRadius = Mathf.Max(1.35f, range * 1.08f);
        float innerRadius = Mathf.Max(0.42f, outerRadius - Mathf.Max(0.32f, width * 0.32f));
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        sweepInnerRadius = innerRadius;
        sweepOuterRadius = outerRadius;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-Mathf.PI, Mathf.PI, t);
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            int baseIndex = i * 2;
            vertices[baseIndex] = direction * innerRadius;
            vertices[baseIndex + 1] = direction * outerRadius;
            uvs[baseIndex] = new Vector2(t, 0f);
            uvs[baseIndex + 1] = new Vector2(t, 1f);

            if (i >= segments)
            {
                continue;
            }

            int triangle = i * 6;
            triangles[triangle] = baseIndex;
            triangles[triangle + 1] = baseIndex + 1;
            triangles[triangle + 2] = baseIndex + 2;
            triangles[triangle + 3] = baseIndex + 1;
            triangles[triangle + 4] = baseIndex + 3;
            triangles[triangle + 5] = baseIndex + 2;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void ConfigureThrustMesh(Mesh mesh, WeaponType weaponType, float width, float range)
    {
        if (weaponType == WeaponType.Sword)
        {
            ConfigureLightningSwordMesh(mesh, width, range);
            return;
        }

        float halfWidth = width * 0.5f;
        float baseWidth = weaponType == WeaponType.Spear ? halfWidth * 0.34f : halfWidth * 0.48f;
        float shoulderWidth = weaponType == WeaponType.Spear ? halfWidth * 0.9f : halfWidth;
        float shoulderY = weaponType == WeaponType.Spear ? range * 0.76f : range * 0.82f;

        mesh.vertices = new[]
        {
            new Vector3(-baseWidth, 0f, 0f),
            new Vector3(baseWidth, 0f, 0f),
            new Vector3(-shoulderWidth, shoulderY, 0f),
            new Vector3(0f, range, 0f),
            new Vector3(shoulderWidth, shoulderY, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0.38f, 0f),
            new Vector2(0.62f, 0f),
            new Vector2(0.06f, shoulderY / range),
            new Vector2(0.5f, 1f),
            new Vector2(0.94f, shoulderY / range)
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 4, 2, 3, 4 };
        mesh.RecalculateBounds();
    }

    private void ConfigureLightningSwordMesh(Mesh mesh, float width, float range)
    {
        float halfWidth = width * 0.5f;
        int segments = 7;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];
        int variant = Random.Range(0, 3);
        float variantBend = variant == 0 ? 0.04f : (variant == 1 ? -0.06f : 0.02f);
        float variantShoulder = variant == 2 ? 1.28f : 1f;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float y = range * t;
            float taper = Mathf.Lerp(1.05f, 0.1f, Mathf.Pow(t, 1.35f));
            float shoulder = Mathf.Exp(-Mathf.Pow((t - 0.18f) * 4.2f, 2f)) * 0.45f * variantShoulder;
            float edgeWidth = halfWidth * (taper + shoulder);
            float center = Mathf.Sin(t * Mathf.PI * 1.2f) * halfWidth * variantBend
                + Mathf.Sin(t * 23f + variant * 1.7f) * halfWidth * 0.06f;
            if (i == segments)
            {
                edgeWidth = halfWidth * 0.04f;
                center = 0f;
            }

            int baseIndex = i * 2;
            vertices[baseIndex] = new Vector3(center - edgeWidth, y, 0f);
            vertices[baseIndex + 1] = new Vector3(center + edgeWidth, y, 0f);
            uvs[baseIndex] = new Vector2(0.12f, t);
            uvs[baseIndex + 1] = new Vector2(0.88f, t);

            if (i >= segments)
            {
                continue;
            }

            int triangle = i * 6;
            triangles[triangle] = baseIndex;
            triangles[triangle + 1] = baseIndex + 2;
            triangles[triangle + 2] = baseIndex + 1;
            triangles[triangle + 3] = baseIndex + 1;
            triangles[triangle + 4] = baseIndex + 2;
            triangles[triangle + 5] = baseIndex + 3;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void ConfigureMaterial(WeaponType weaponType, float visualMultiplier)
    {
        if (material == null)
        {
            Shader shader = Shader.Find("TopDownRogue/AttackWave");
            material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            material.name = "RuntimeAttackWaveMaterial";
        }

        Color color = weaponType == WeaponType.Spell ? new Color(0.66f, 0.84f, 1f, 0.98f) : new Color(1f, 0.9f, 0.58f, 0.96f);
        material.SetColor(ColorId, color);
        material.SetFloat(ShapeId, GetShapeValue(weaponType));
        material.SetFloat(FadeId, 0f);
        material.SetFloat(ExpandId, 0f);
        material.SetFloat(ThicknessId, GetThickness(weaponType, visualMultiplier));
        material.SetFloat(SoftnessId, 0.08f);
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingOrder = 35;
    }

    private void ConfigureCollider(WeaponDefinition weapon, WeaponType weaponType, float width, float range)
    {
        if (weaponType == WeaponType.Spell)
        {
            ConfigureOrbCollider(width);
            return;
        }

        float halfWidth = width * 0.5f;
        waveCollider.pathCount = 1;
        if (attachedWeaponBodyCollider)
        {
            ConfigureAttachedWeaponCollider(weapon);
            return;
        }

        if (weaponType == WeaponType.Knife)
        {
            if (weapon != null && weapon.weaponType == WeaponType.Spear && weapon.useSweepArc)
            {
                ConfigureSweepRingCollider(range);
            }
            else if (weapon != null && weapon.useSweepArc)
            {
                ConfigureSweepTipCollider(width, range);
            }
            else
            {
                ConfigureKnifeFanCollider(width, range);
            }
            return;
        }

        waveCollider.SetPath(0, new[]
        {
            new Vector2(-halfWidth * 0.35f, 0.12f),
            new Vector2(halfWidth * 0.35f, 0.12f),
            new Vector2(halfWidth * 0.55f, range * 0.88f),
            new Vector2(0f, range),
            new Vector2(-halfWidth * 0.55f, range * 0.88f)
        });
    }

    private void ConfigureAttachedWeaponCollider(WeaponDefinition weapon)
    {
        float halfWidth;
        float bottom;
        float top;
        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                halfWidth = 0.14f;
                bottom = -0.5f;
                top = 1.08f;
                break;
            case WeaponType.Spear:
                halfWidth = 0.1f;
                bottom = -0.82f;
                top = 1.1f;
                break;
            default:
                halfWidth = 0.12f;
                bottom = -0.35f;
                top = 0.85f;
                break;
        }

        attachedColliderHalfWidth = halfWidth;
        attachedColliderBottom = bottom;
        attachedColliderTop = top;
        waveCollider.pathCount = 1;
        waveCollider.SetPath(0, new[]
        {
            new Vector2(-halfWidth, bottom),
            new Vector2(halfWidth, bottom),
            new Vector2(halfWidth, top),
            new Vector2(-halfWidth, top)
        });
    }

    private float GetAttachedHitDamageMultiplier(Collider2D hit, WeaponDefinition weapon)
    {
        if (weapon == null || hit == null)
        {
            return 1f;
        }

        if (weapon.weaponType != WeaponType.Spear || !weapon.useSweepArc)
        {
            return 1f;
        }

        if (attachedWeaponBodyCollider)
        {
            return IsAttachedSpearTipHit(hit, weapon) ? 1.1f : 0.8f;
        }

        return IsSpearSweepOuterHit(hit) ? 1.1f : 0.8f;
    }

    private bool IsAttachedSpearTipHit(Collider2D hit, WeaponDefinition weapon)
    {
        if (!attachedWeaponBodyCollider || weapon == null || hit == null || weapon.weaponType != WeaponType.Spear || !weapon.useSweepArc)
        {
            return false;
        }

        Vector2 tipWorld = transform.TransformPoint(new Vector2(0f, attachedColliderTop));
        Vector2 hitPoint = hit.ClosestPoint(tipWorld);
        Vector2 localHit = transform.InverseTransformPoint(hitPoint);
        float tipStart = Mathf.Lerp(attachedColliderBottom, attachedColliderTop, 0.72f);
        bool inTipBand = localHit.y >= tipStart;
        bool nearSpearWidth = Mathf.Abs(localHit.x) <= attachedColliderHalfWidth * 1.35f;
        return inTipBand && nearSpearWidth;
    }

    private bool IsSpearSweepOuterHit(Collider2D hit)
    {
        Vector2 hitPoint = hit.ClosestPoint(transform.position);
        Vector2 localHit = transform.InverseTransformPoint(hitPoint);
        float radius = localHit.magnitude;
        float tipStart = Mathf.Lerp(sweepInnerRadius, sweepOuterRadius, 0.72f);
        return radius >= tipStart;
    }

    private void ConfigureKnifeFanCollider(float width, float range)
    {
        int segments = 12;
        float innerRadius = Mathf.Max(0.16f, range * 0.18f);
        float outerRadius = Mathf.Max(innerRadius + 0.2f, range);
        float halfAngle = Mathf.Clamp(width * 42f, 58f, 82f) * Mathf.Deg2Rad;
        Vector2[] path = new Vector2[(segments + 1) * 2];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            path[i] = direction * outerRadius;
            path[path.Length - 1 - i] = direction * innerRadius;
        }

        waveCollider.pathCount = 1;
        waveCollider.SetPath(0, path);
    }

    private void ConfigureSweepTipCollider(float width, float range)
    {
        int segments = 12;
        float outerRadius = Mathf.Max(0.8f, range);
        float innerRadius = Mathf.Max(0.2f, outerRadius * 0.68f);
        float halfAngle = Mathf.Clamp(width * 39f, 52f, 76f) * Mathf.Deg2Rad;
        Vector2[] path = new Vector2[(segments + 1) * 2];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            path[i] = direction * outerRadius;
            path[path.Length - 1 - i] = direction * innerRadius;
        }

        waveCollider.pathCount = 1;
        waveCollider.SetPath(0, path);
    }

    private void ConfigureSweepRingCollider(float range)
    {
        int segments = 36;
        float outerRadius = Mathf.Max(1.35f, range * 1.08f);
        float innerRadius = Mathf.Max(0.42f, outerRadius * 0.68f);
        sweepInnerRadius = innerRadius;
        sweepOuterRadius = outerRadius;
        Vector2[] path = new Vector2[(segments + 1) * 2];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-Mathf.PI, Mathf.PI, t);
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            path[i] = direction * outerRadius;
            path[path.Length - 1 - i] = direction * innerRadius;
        }

        waveCollider.pathCount = 1;
        waveCollider.SetPath(0, path);
    }

    private void ConfigureOrbCollider(float width)
    {
        float radius = width * 0.5f;
        waveCollider.pathCount = 1;
        Vector2[] path = new Vector2[12];
        for (int i = 0; i < path.Length; i++)
        {
            float angle = (Mathf.PI * 2f * i) / path.Length;
            path[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        waveCollider.SetPath(0, path);
    }

    private void SetProgress(float progress)
    {
        if (material != null)
        {
            material.SetFloat(ProgressId, Mathf.Clamp01(progress));
        }
    }

    private void SetFade(float fade)
    {
        if (material != null)
        {
            material.SetFloat(FadeId, Mathf.Clamp01(fade));
        }
    }

    private void SetExpand(float expand)
    {
        if (material != null)
        {
            material.SetFloat(ExpandId, Mathf.Clamp01(expand));
        }
    }

    private void EnsureParticleSystem()
    {
        if (vfxParticles != null)
        {
            return;
        }

        Transform existing = transform.Find("AttackVfxParticles");
        GameObject particleObject = existing != null ? existing.gameObject : new GameObject("AttackVfxParticles");
        particleObject.transform.SetParent(transform);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;
        vfxParticles = particleObject.GetComponent<ParticleSystem>();
        if (vfxParticles == null)
        {
            vfxParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sortingOrder = 45;
        particleRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void ConfigureParticleDefaults(WeaponType weaponType)
    {
        EnsureParticleSystem();
        ParticleSystem.MainModule main = vfxParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startSize = weaponType == WeaponType.Spell ? 0.12f : 0.07f;
        main.startLifetime = weaponType == WeaponType.Spell ? 0.36f : 0.24f;
        main.maxParticles = 192;

        ParticleSystem.EmissionModule emission = vfxParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = vfxParticles.shape;
        shape.enabled = false;
    }

    private void EmitReleaseParticles(WeaponType weaponType, float range)
    {
        if (vfxParticles == null)
        {
            return;
        }

        switch (weaponType)
        {
            case WeaponType.Knife:
                break;
            case WeaponType.Sword:
                EmitLinearStreaks(range, 12, 0.22f);
                break;
            case WeaponType.Spear:
                break;
            case WeaponType.Spell:
                EmitSpellTrailParticle();
                break;
        }
    }

    private void EmitImpactParticles(WeaponType weaponType)
    {
        if (vfxParticles == null)
        {
            return;
        }

        if (weaponType == WeaponType.Spell)
        {
            for (int i = 0; i < 72; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                EmitParticle(transform.position, direction * Random.Range(1.0f, 3.6f), new Color(0.55f, 0.86f, 1f, 0.95f), Random.Range(0.055f, 0.15f), Random.Range(0.22f, 0.46f));
            }
            return;
        }

        if (weaponType == WeaponType.Sword || weaponType == WeaponType.Knife || weaponType == WeaponType.Spear)
        {
            return;
        }

        int count = weaponType == WeaponType.Spear ? 34 : 22;
        Color color = new Color(1f, 0.84f, 0.5f, 0.82f);
        for (int i = 0; i < count; i++)
        {
            Vector2 local = new Vector2(Random.Range(-0.22f, 0.22f), Random.Range(0.55f, 1.28f));
            Vector2 position = transform.TransformPoint(local);
            Vector2 velocity = transform.TransformDirection(new Vector2(Random.Range(-0.95f, 0.95f), Random.Range(0.45f, 2.45f)));
            EmitParticle(position, velocity, color, Random.Range(0.04f, 0.12f), Random.Range(0.12f, 0.28f));
        }
    }

    private void EmitSwordHitBurst(Vector2 hitPoint)
    {
        if (vfxParticles == null)
        {
            return;
        }

        Vector2 forward = transform.up;
        Vector2 right = transform.right;
        Color core = new Color(1f, 0.96f, 0.76f, 0.95f);
        Color spark = new Color(1f, 0.76f, 0.32f, 0.88f);

        for (int i = 0; i < 18; i++)
        {
            Vector2 jitter = Random.insideUnitCircle * 0.08f;
            Vector2 velocity = forward * Random.Range(0.85f, 2.45f) + Random.insideUnitCircle * 0.48f;
            EmitParticle(hitPoint + jitter, velocity, core, Random.Range(0.075f, 0.16f), Random.Range(0.1f, 0.24f));
        }

        for (int i = 0; i < 32; i++)
        {
            float angle = (Mathf.PI * 2f * i) / 32f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float bias = Mathf.Max(0.35f, Vector2.Dot(direction, forward));
            EmitParticle(hitPoint, direction * Random.Range(1.15f, 4.25f) * bias, spark, Random.Range(0.035f, 0.095f), Random.Range(0.1f, 0.28f));
        }

        for (int i = 0; i < 20; i++)
        {
            Vector2 position = hitPoint + right * Random.Range(-0.2f, 0.2f);
            Vector2 velocity = forward * Random.Range(2.2f, 5.1f) + right * Random.Range(-0.9f, 0.9f);
            EmitParticle(position, velocity, core, Random.Range(0.035f, 0.11f), Random.Range(0.1f, 0.24f));
        }
    }

    private void EmitSpearSweepHitBurst(Vector2 hitPoint, bool tipHit)
    {
        if (vfxParticles == null)
        {
            return;
        }

        Vector2 forward = transform.up;
        Vector2 right = transform.right;
        Color core = tipHit ? new Color(1f, 0.88f, 0.42f, 0.95f) : new Color(1f, 0.66f, 0.24f, 0.78f);
        Color spark = tipHit ? new Color(1f, 0.76f, 0.22f, 0.88f) : new Color(1f, 0.52f, 0.18f, 0.72f);
        int sparkCount = tipHit ? 42 : 22;
        float speed = tipHit ? 4.4f : 2.45f;
        float sizeScale = tipHit ? 2.25f : 1.45f;

        for (int i = 0; i < sparkCount; i++)
        {
            float angle = Random.Range(-1.05f, 1.05f);
            Vector2 direction = (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)).normalized;
            Vector2 jitter = Random.insideUnitCircle * (tipHit ? 0.07f : 0.04f);
            EmitParticle(hitPoint + jitter, direction * Random.Range(0.75f, speed), spark, Random.Range(0.025f, 0.075f) * sizeScale, Random.Range(0.1f, 0.25f));
        }

        for (int i = 0; i < (tipHit ? 12 : 6); i++)
        {
            Vector2 velocity = right * Random.Range(-0.85f, 0.85f) + forward * Random.Range(0.15f, speed);
            EmitParticle(hitPoint, velocity, core, Random.Range(0.05f, 0.11f) * sizeScale, Random.Range(0.07f, 0.14f));
        }
    }

    private void EmitGenericHitBurst(Vector2 hitPoint, WeaponType weaponType)
    {
        if (vfxParticles == null)
        {
            return;
        }

        Color color = weaponType == WeaponType.Spell ? new Color(0.5f, 0.82f, 1f, 0.92f) : new Color(1f, 0.82f, 0.46f, 0.9f);
        int count = weaponType == WeaponType.Spell ? 28 : 18;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.up;
            }

            EmitParticle(hitPoint + Random.insideUnitCircle * 0.07f, direction * Random.Range(0.85f, 2.8f), color, Random.Range(0.045f, 0.13f), Random.Range(0.1f, 0.26f));
        }
    }

    private void EmitKnifeSparks(float range, int count)
    {
        float radius = Mathf.Max(0.4f, range * 0.95f);
        Color color = new Color(1f, 0.78f, 0.42f, 0.86f);
        for (int i = 0; i < count; i++)
        {
            float t = i / Mathf.Max(1f, count - 1f);
            float angle = Mathf.Lerp(-68f, 68f, t) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 position = transform.TransformPoint(direction * radius);
            Vector2 tangent = transform.TransformDirection(new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)));
            EmitParticle(position, tangent * Random.Range(0.4f, 1.4f), color, Random.Range(0.025f, 0.07f), Random.Range(0.1f, 0.22f));
        }
    }

    private void EmitLinearStreaks(float range, int count, float spread)
    {
        Color color = new Color(1f, 0.88f, 0.56f, 0.82f);
        for (int i = 0; i < count; i++)
        {
            float y = Random.Range(0.2f, range);
            float x = Random.Range(-spread, spread) * (1f - y / (range * 1.2f));
            Vector2 position = transform.TransformPoint(new Vector2(x, y));
            Vector2 velocity = transform.TransformDirection(new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.6f, 1.8f)));
            EmitParticle(position, velocity, color, Random.Range(0.018f, 0.05f), Random.Range(0.08f, 0.18f));
        }
    }

    private void EmitSpellTrailParticle()
    {
        if (vfxParticles == null || Random.value > 0.55f)
        {
            return;
        }

        Vector2 offset = Random.insideUnitCircle * 0.18f;
        Vector2 position = (Vector2)transform.position + offset;
        Vector2 velocity = -projectileDirection * Random.Range(0.2f, 0.9f) + Random.insideUnitCircle * 0.25f;
        EmitParticle(position, velocity, new Color(0.45f, 0.78f, 1f, 0.75f), Random.Range(0.035f, 0.075f), Random.Range(0.16f, 0.32f));
    }

    private void EmitParticle(Vector2 position, Vector2 velocity, Color color, float size, float lifetime)
    {
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position = position;
        emitParams.velocity = velocity;
        emitParams.startColor = color;
        emitParams.startSize = size;
        emitParams.startLifetime = lifetime;
        vfxParticles.Emit(emitParams, 1);
    }

    private float GetVisualRange(WeaponDefinition weapon, float visualMultiplier)
    {
        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                return Mathf.Max(0.78f, weapon.attackRange * 0.82f * visualMultiplier);
            case WeaponType.Spear when weapon.useSweepArc:
                return Mathf.Max(1.45f, weapon.attackRange * 1.05f * visualMultiplier);
            case WeaponType.Sword:
                return Mathf.Max(1.7f, weapon.attackRange * 1.35f * visualMultiplier);
            case WeaponType.Spear:
                return Mathf.Max(2.65f, weapon.attackRange * 1.58f * visualMultiplier);
            case WeaponType.Spell:
                return Mathf.Max(4.1f, weapon.attackRange * 2.05f * visualMultiplier);
            default:
                return Mathf.Max(0.2f, weapon.attackRange * visualMultiplier);
        }
    }

    private float GetVisualWidth(WeaponDefinition weapon, float visualMultiplier)
    {
        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                return Mathf.Max(0.82f, weapon.attackRadius * 2.35f * visualMultiplier);
            case WeaponType.Spear when weapon.useSweepArc:
                return Mathf.Max(1.7f, weapon.attackRadius * 3.8f * visualMultiplier);
            case WeaponType.Sword:
                return Mathf.Max(1.08f, weapon.attackRadius * 2.75f * visualMultiplier);
            case WeaponType.Spear:
                return Mathf.Max(0.52f, weapon.attackRadius * 1.42f * visualMultiplier);
            case WeaponType.Spell:
                return Mathf.Max(0.88f, weapon.attackRadius * 1.32f * visualMultiplier);
            default:
                return Mathf.Max(0.25f, weapon.attackRadius * 2.6f * visualMultiplier);
        }
    }

    private float GetWindupTime(WeaponDefinition weapon)
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

    private float GetActiveTime(WeaponDefinition weapon)
    {
        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                return 0.1f;
            case WeaponType.Sword:
                return 0.05f;
            case WeaponType.Spear when weapon.useSweepArc:
                return 0.42f;
            case WeaponType.Spear:
                return 0.15f;
            case WeaponType.Spell:
                return 0.48f;
            default:
                return Mathf.Max(0.08f, Mathf.Min(0.18f, weapon.recovery + 0.04f));
        }
    }

    private float GetFadeTime(WeaponDefinition weapon)
    {
        if (weapon.weaponType == WeaponType.Sword)
        {
            return 0.035f;
        }

        return weapon.weaponType == WeaponType.Spell ? 0.24f : Mathf.Max(0.04f, weapon.recovery * 0.45f);
    }

    private float GetShapeValue(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Knife:
                return 0f;
            case WeaponType.Sword:
                return 1f;
            case WeaponType.Spear:
                return 2f;
            case WeaponType.Spell:
                return 3f;
            default:
                return 0f;
        }
    }

    private float GetThickness(WeaponType weaponType, float visualMultiplier)
    {
        switch (weaponType)
        {
            case WeaponType.Knife:
                return Mathf.Lerp(0.05f, 0.08f, Mathf.Clamp01(visualMultiplier - 1f));
            case WeaponType.Sword:
                return 0.26f;
            case WeaponType.Spear:
                return 0.12f;
            case WeaponType.Spell:
                return 0.22f;
            default:
                return 0.14f;
        }
    }
}
