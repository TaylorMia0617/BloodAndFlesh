using System.Collections;
using UnityEngine;

public partial class PlayerCombatController
{
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
            if (IsLocallyHitStopped)
            {
                yield return null;
                continue;
            }

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
        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(WeaponType.Knife);
        float duration = Mathf.Max(0.01f, special.blockDuration > 0f ? special.blockDuration : special.active);
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatPhase.Special, false);
        LockFacingFor(duration);
        if (stats != null)
        {
            stats.AddDamageImmunity(duration);
        }

        ShowShieldVisual(duration, new Color(1f, 0.92f, 0.72f, 0.78f), 1.25f);
        if (weaponView != null)
        {
            weaponView.PlayBlockPose(currentWeapon, attackDirection, duration);
        }

        yield return WaitCombatSeconds(duration);
        EndCombatAction();
    }

    private IEnumerator SwordDashRoutine(Vector2 attackDirection)
    {
        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(WeaponType.Sword);
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatPhase.Special, false);
        float duration = Mathf.Max(0.01f, special.dashDuration > 0f ? special.dashDuration : special.active);
        float distance = Mathf.Max(0f, special.dashDistance);
        float recovery = Mathf.Max(0f, special.recovery);
        LockFacingFor(duration + recovery);
        Vector2 forward = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : Vector2.up;
        if (stats != null)
        {
            stats.AddUntargetable(duration + Mathf.Max(0f, special.untargetableExtraDuration));
        }

        Vector2 start = playerBody != null ? playerBody.position : (Vector2)transform.position;
        Vector2 end = GetSwordDashEnd(start, forward, distance);
        bool wallImpact = distance > 0.01f && Vector2.Distance(start, end) < distance - 0.05f;
        StartCoroutine(LightningDashVisualRoutine(duration + Mathf.Max(0f, special.untargetableExtraDuration), start, end));
        float elapsed = 0f;
        bool enemyImpact = false;
        Vector2 impactPoint = end;
        while (elapsed < duration)
        {
            if (IsLocallyHitStopped)
            {
                yield return null;
                continue;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector2 currentPosition = playerBody != null ? playerBody.position : (Vector2)transform.position;
            Vector2 requestedPosition = Vector2.Lerp(start, end, eased);
            Vector2 nextPosition = requestedPosition;
            if (TryFindSwordDashEnemyHit(currentPosition, requestedPosition, out _, out impactPoint))
            {
                enemyImpact = true;
                nextPosition = impactPoint - forward * 0.28f;
            }

            if (playerBody != null)
            {
                playerBody.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            if (enemyImpact)
            {
                TriggerSwordDashImpact(impactPoint, true);
                break;
            }

            yield return null;
        }

        if (!enemyImpact && wallImpact)
        {
            TriggerSwordDashImpact(end, true);
        }

        if (recovery > 0f)
        {
            yield return WaitCombatSeconds(recovery);
        }
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
        return objectName.StartsWith("Obstacle_") || objectName.StartsWith("Wall_") || objectName.StartsWith("SafeWall_");
    }

    private bool TryFindSwordDashEnemyHit(Vector2 from, Vector2 to, out SimpleEnemyAI enemy, out Vector2 hitPoint)
    {
        enemy = null;
        hitPoint = to;
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        Vector2 direction = delta / distance;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(from, 0.48f, direction, distance, Physics2D.DefaultRaycastLayers);
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            SimpleEnemyAI candidate = hit.GetComponentInParent<SimpleEnemyAI>();
            if (candidate == null)
            {
                continue;
            }

            CharacterStats candidateStats = candidate.GetComponent<CharacterStats>();
            if (candidateStats != null && !candidateStats.IsAlive)
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                enemy = candidate;
                hitPoint = hits[i].point.sqrMagnitude > 0.001f ? hits[i].point : (from + direction * hits[i].distance);
            }
        }

        if (enemy != null)
        {
            return true;
        }

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(to, 0.52f, Physics2D.DefaultRaycastLayers);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D hit = overlaps[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            SimpleEnemyAI candidate = hit.GetComponentInParent<SimpleEnemyAI>();
            if (candidate == null)
            {
                continue;
            }

            CharacterStats candidateStats = candidate.GetComponent<CharacterStats>();
            if (candidateStats != null && !candidateStats.IsAlive)
            {
                continue;
            }

            enemy = candidate;
            hitPoint = hit.ClosestPoint(to);
            if (hitPoint.sqrMagnitude <= 0.001f)
            {
                hitPoint = candidate.transform.position;
            }

            return true;
        }

        return false;
    }

    private void TriggerSwordDashImpact(Vector2 point, bool stunPlayer)
    {
        EmitLightningDashImpact(point);
        if (stunPlayer && inputManager != null)
        {
            inputManager.ApplyStun(1f);
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        CameraShakeFeedback shake = targetCamera.GetComponent<CameraShakeFeedback>();
        if (shake == null)
        {
            shake = targetCamera.gameObject.AddComponent<CameraShakeFeedback>();
        }

        shake.Shake(0.38f, 0.24f);
        ImpactBlurFeedback blur = targetCamera.GetComponent<ImpactBlurFeedback>();
        if (blur == null)
        {
            blur = targetCamera.gameObject.AddComponent<ImpactBlurFeedback>();
        }

        blur.Pulse(0.22f, 0.95f);
    }

    private IEnumerator SpearSweepRoutine(Vector2 attackDirection)
    {
        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(WeaponType.Spear);
        CommitAimDirection(attackDirection);
        BeginCombatAction(CombatPhase.Special, false);
        WeaponDefinition sweepWeapon = CreateSpearSweepWeapon(currentWeapon);
        float windup = Mathf.Max(0f, special.windup);
        float active = Mathf.Max(0.01f, special.active);
        float recovery = Mathf.Max(0f, special.recovery);
        LockFacingFor(windup + active + recovery);
        yield return WaitCombatSeconds(windup);
        if (inputManager != null)
        {
            inputManager.SetTemporarySpeedMultiplier(Mathf.Max(0.01f, special.movementSpeedMultiplier), active);
        }
        PlayAttackWave(sweepWeapon, attackDirection, Mathf.Max(0.01f, special.visualMultiplier));
        yield return WaitCombatSeconds(active + recovery);
        EndCombatAction();
    }

    private bool TrySpellShield()
    {
        if (stats == null)
        {
            stats = GetComponent<CharacterStats>();
        }

        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(WeaponType.Spell);
        float cost = stats != null ? stats.GetManaCrystalValue(3) * Mathf.Max(0f, special.manaCrystalCost) : 0f;
        if (stats == null || !stats.TrySpendMana(cost))
        {
            EmitFailedSpecialPulse();
            nextSpecialTime = Time.time + 0.25f;
            EndCombatAction();
            return false;
        }

        nextSpecialTime = Time.time + Mathf.Max(currentWeapon.cooldown, special.cooldown);
        StartCoroutine(SpellShieldRoutine());
        return true;
    }

    private IEnumerator SpellShieldRoutine()
    {
        WeaponSpecialConfig special = WeaponSpecialConfigDatabase.Get(WeaponType.Spell);
        float active = Mathf.Max(0.01f, special.active);
        float shieldDuration = Mathf.Max(0.01f, special.shieldDuration);
        BeginCombatAction(CombatPhase.Special, false);
        LockFacingFor(active);
        if (stats != null)
        {
            stats.AddDamageImmunity(shieldDuration);
        }

        ShowShieldVisual(shieldDuration, new Color(0.42f, 0.76f, 1f, 0.72f), 1.45f);
        SensoryEventBus.Publish(SensoryEventType.Magic, transform.position, Mathf.Max(0f, special.magicSenseRadius), shieldDuration);
        if (weaponView != null)
        {
            weaponView.PlayAttackAnimation(currentWeapon, lastAimDirection, Mathf.Min(0.12f, active), Mathf.Min(0.1f, active), Mathf.Min(0.16f, active), false);
        }

        yield return WaitCombatSeconds(active);
        EndCombatAction();
    }

    private WeaponDefinition CreateSpearSweepWeapon(WeaponDefinition source)
    {
        WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.hideFlags = HideFlags.HideAndDontSave;
        weapon.weaponType = WeaponType.Spear;
        weapon.displayName = "Spear Sweep";
        weapon.weaponSprite = source.weaponSprite;
        weapon.physicalDamage = source.physicalDamage;
        weapon.magicDamage = source.magicDamage;
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
            if (IsLocallyHitStopped)
            {
                yield return null;
                continue;
            }

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
        yield return WaitCombatSeconds(0.12f);
        if (shieldVisual != null && shieldRoutine == null)
        {
            shieldVisual.enabled = false;
        }
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
            if (IsLocallyHitStopped)
            {
                yield return null;
                continue;
            }

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

    private void EmitLightningDashImpact(Vector2 point)
    {
        EnsureLightningDashVisuals();
        if (lightningDashParticles == null)
        {
            return;
        }

        const int count = 42;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            float speed = Random.Range(1.8f, 5.8f);
            EmitLightningParticle(point + direction * Random.Range(0f, 0.12f), direction * speed, Random.Range(0.08f, 0.22f), Random.Range(0.1f, 0.24f));
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

}
