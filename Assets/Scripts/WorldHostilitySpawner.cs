using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldHostilitySpawner : MonoBehaviour
{
    [SerializeField] private GridRouteMapGenerator mapGenerator;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private int maxStageEnemies = 20;
    [SerializeField] private Sprite[] enemySprites;

    private readonly List<GameObject> livingEnemies = new List<GameObject>();
    private Coroutine spawnRoutine;
    private int spawnIndex;

    public void Configure(GridRouteMapGenerator generator, Transform target, Sprite[] sprites)
    {
        mapGenerator = generator;
        playerTarget = target;
        enemySprites = sprites;
    }

    public void SetSpawnInterval(float interval)
    {
        spawnInterval = Mathf.Max(0.25f, interval);
    }

    public void StartSpawning()
    {
        StopSpawning();
        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.25f, spawnInterval));
        while (enabled)
        {
            yield return wait;
            TrySpawnEnemy();
        }
    }

    private void TrySpawnEnemy()
    {
        if (mapGenerator == null || mapGenerator.EnemySpawnPositions.Count == 0)
        {
            return;
        }

        livingEnemies.RemoveAll(enemy => enemy == null || !enemy.activeInHierarchy);
        int stageBudget = RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentEnemyBudget : maxStageEnemies;
        int effectiveMaxEnemies = Mathf.Min(maxStageEnemies, stageBudget);
        if (EnemyRegistry.LivingCount >= effectiveMaxEnemies)
        {
            return;
        }

        Vector3 position = mapGenerator.EnemySpawnPositions[Random.Range(0, mapGenerator.EnemySpawnPositions.Count)];
        EnemyArchetype archetype = PickArchetype();
        GameObject enemy = CreateEnemy($"HostilityEnemy_{spawnIndex++:000}", position, archetype);
        livingEnemies.Add(enemy);
    }

    private EnemyArchetype PickArchetype()
    {
        return EnemyConfigDatabase.PickWeighted();
    }

    private GameObject CreateEnemy(string enemyName, Vector3 position, EnemyArchetype archetype)
    {
        GameObject enemy = new GameObject(enemyName);
        enemy.transform.SetParent(transform);
        enemy.transform.position = position + Vector3.back * 0.1f;

        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = GetEnemySprite(archetype);
        renderer.sortingOrder = 14;

        CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
        collider.radius = EnemyConfigDatabase.Get(archetype).colliderRadius;

        Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CharacterStats stats = enemy.AddComponent<CharacterStats>();
        enemy.AddComponent<HitVolumeFeedback>();
        SimpleEnemyAI ai = enemy.AddComponent<SimpleEnemyAI>();
        PrototypeDamageable damageable = enemy.AddComponent<PrototypeDamageable>();
        ai.Configure(archetype, playerTarget);
        damageable.enabled = true;
        stats.ResetStats();
        return enemy;
    }

    private Sprite GetEnemySprite(EnemyArchetype archetype)
    {
        if (enemySprites == null || enemySprites.Length == 0)
        {
            enemySprites = new[]
            {
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Attacker),
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Sentinel),
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Ranged),
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Shield)
            };
        }

        int index = Mathf.Clamp((int)archetype, 0, enemySprites.Length - 1);
        Sprite configured = enemySprites.Length > 0 ? enemySprites[index] : null;
        return configured != null ? configured : EnemyConfigDatabase.LoadSprite(archetype);
    }
}
