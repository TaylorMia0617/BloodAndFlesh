using UnityEngine;

public static class EnemySpawnFactory
{
    public static GameObject CreateEnemy(
        Transform parent,
        string enemyName,
        Vector3 position,
        EnemyArchetype archetype,
        Transform playerTarget,
        Sprite[] enemySprites,
        WorldHostilityDirector director)
    {
        GameObject enemy = new GameObject(enemyName);
        enemy.transform.SetParent(parent);
        enemy.transform.position = position + Vector3.back * 0.1f;

        EnemyConfig enemyConfig = EnemyConfigDatabase.Get(archetype);

        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = GetEnemySprite(archetype, enemySprites);
        renderer.sortingOrder = 14;

        CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
        collider.radius = enemyConfig.colliderRadius;

        Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CharacterStats stats = enemy.AddComponent<CharacterStats>();
        enemy.AddComponent<HitVolumeFeedback>();
        KnockbackReceiver knockbackReceiver = enemy.AddComponent<KnockbackReceiver>();
        knockbackReceiver.Configure(enemyConfig.knockbackDistance, enemyConfig.knockbackDuration);
        SimpleEnemyAI ai = enemy.AddComponent<SimpleEnemyAI>();
        PrototypeDamageable damageable = enemy.AddComponent<PrototypeDamageable>();
        enemy.AddComponent<DamageableHurtbox>();
        ai.Configure(archetype, playerTarget);
        if (director != null && director.ForceDirectAttack)
        {
            ai.BeginDirectChase(8f);
        }

        damageable.enabled = true;
        stats.ResetStats();
        return enemy;
    }

    public static Sprite GetEnemySprite(EnemyArchetype archetype, Sprite[] enemySprites)
    {
        if (enemySprites != null && enemySprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp((int)archetype, 0, enemySprites.Length - 1);
            Sprite configuredSprite = enemySprites[spriteIndex];
            if (configuredSprite != null)
            {
                return configuredSprite;
            }
        }

        return EnemyConfigDatabase.LoadSprite(archetype);
    }
}
