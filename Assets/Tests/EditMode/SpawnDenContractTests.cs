using NUnit.Framework;
using UnityEngine;

public class SpawnDenContractTests
{
    [Test]
    public void SpawnDenConfigParsesStandalonePools()
    {
        string json = @"{
            ""spawnDenPools"": [
                {
                    ""id"": ""test_pool"",
                    ""entries"": [
                        {
                            ""id"": ""test_den"",
                            ""level"": 2,
                            ""minWorldHostility"": 1.5,
                            ""baseWeight"": 3,
                            ""weightPerHostilityAboveMin"": 0.5,
                            ""weightCurve"": ""steep"",
                            ""maxHealth"": 123,
                            ""baseSpawnInterval"": 4.5,
                            ""enemyArchetypes"": ""Attacker:2,Ranged:1"",
                            ""spawnLogic"": {
                                ""activationRange"": 9,
                                ""accelerationRange"": 3,
                                ""acceleratedIntervalMultiplier"": 0.5,
                                ""maxAliveEnemies"": 2,
                                ""spawnRadius"": 0.25
                            },
                            ""destroyLogic"": {
                                ""type"": ""reviveAfterDelayInterruptible"",
                                ""reviveDelaySeconds"": 2,
                                ""reviveHealPerSecond"": 8,
                                ""reviveHealthPercent"": 0.4
                            }
                        }
                    ]
                }
            ]
        }";

        SpawnDenConfigList config = JsonUtility.FromJson<SpawnDenConfigList>(json);

        Assert.NotNull(config.spawnDenPools);
        Assert.AreEqual(1, config.spawnDenPools.Length);
        Assert.AreEqual("test_pool", config.spawnDenPools[0].id);
        Assert.AreEqual("test_den", config.spawnDenPools[0].entries[0].id);
        Assert.AreEqual(9f, config.spawnDenPools[0].entries[0].spawnLogic.activationRange, 0.001f);
        Assert.AreEqual("reviveAfterDelayInterruptible", config.spawnDenPools[0].entries[0].destroyLogic.type);
    }

    [Test]
    public void SpawnDenWeightRequiresMinimumHostility()
    {
        SpawnDenConfig config = new SpawnDenConfig
        {
            minWorldHostility = 2f,
            baseWeight = 3f,
            weightPerHostilityAboveMin = 1f
        };

        Assert.AreEqual(0f, SpawnDenConfigDatabase.CalculateWeight(config, 1.99f), 0.001f);
        Assert.AreEqual(3f, SpawnDenConfigDatabase.CalculateWeight(config, 2f), 0.001f);
        Assert.AreEqual(4f, SpawnDenConfigDatabase.CalculateWeight(config, 3f), 0.001f);
    }

    [Test]
    public void SpawnDenWeightSupportsSteepCurve()
    {
        SpawnDenConfig config = new SpawnDenConfig
        {
            minWorldHostility = 1f,
            baseWeight = 2f,
            weightPerHostilityAboveMin = 1f,
            weightCurve = "steep"
        };

        float weight = SpawnDenConfigDatabase.CalculateWeight(config, 3f);

        Assert.AreEqual(5.4f, weight, 0.001f);
    }

    [Test]
    public void SpawnDenPickWithoutDuplicatesStopsAtEligibleCount()
    {
        SpawnDenConfig[] spawnDens = SpawnDenConfigDatabase.PickSpawnDens("default", 4, 0f, false);

        Assert.AreEqual(2, spawnDens.Length);
        Assert.AreNotEqual(spawnDens[0].id, spawnDens[1].id);
        Assert.LessOrEqual(spawnDens[0].minWorldHostility, 0f);
        Assert.LessOrEqual(spawnDens[1].minWorldHostility, 0f);
    }

    [Test]
    public void SpawnDenPickWithDuplicatesCanFillRequestedCount()
    {
        SpawnDenConfig[] spawnDens = SpawnDenConfigDatabase.PickSpawnDens("default", 4, 0f, true);

        Assert.AreEqual(4, spawnDens.Length);
        for (int i = 0; i < spawnDens.Length; i++)
        {
            Assert.LessOrEqual(spawnDens[i].minWorldHostility, 0f);
        }
    }

    [Test]
    public void SpawnDenChecksActivationRangeBeforeSpawning()
    {
        EnemyRegistry.Clear();
        GameObject denObject = new GameObject("den");
        GameObject playerObject = new GameObject("player");
        try
        {
            SpawnDenController den = CreateDen(denObject, playerObject.transform, new SpawnDenConfig
            {
                id = "range_den",
                minWorldHostility = 0f,
                maxHealth = 50f,
                baseSpawnInterval = 3f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic
                {
                    activationRange = 4f,
                    accelerationRange = 2f,
                    acceleratedIntervalMultiplier = 0.5f,
                    maxAliveEnemies = 2,
                    spawnRadius = 0.25f
                }
            });

            playerObject.transform.position = new Vector3(10f, 0f, 0f);
            Assert.IsFalse(den.CanSpawnNow());

            playerObject.transform.position = new Vector3(3f, 0f, 0f);
            Assert.IsTrue(den.CanSpawnNow());
        }
        finally
        {
            Object.DestroyImmediate(denObject);
            Object.DestroyImmediate(playerObject);
            EnemyRegistry.Clear();
        }
    }

    [Test]
    public void SpawnDenAccelerationRangeShortensInterval()
    {
        GameObject denObject = new GameObject("den");
        GameObject playerObject = new GameObject("player");
        try
        {
            SpawnDenController den = CreateDen(denObject, playerObject.transform, new SpawnDenConfig
            {
                id = "speed_den",
                minWorldHostility = 0f,
                maxHealth = 50f,
                baseSpawnInterval = 4f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic
                {
                    activationRange = 0f,
                    accelerationRange = 3f,
                    acceleratedIntervalMultiplier = 0.5f,
                    maxAliveEnemies = 1,
                    spawnRadius = 0.25f
                }
            });

            playerObject.transform.position = new Vector3(8f, 0f, 0f);
            float farInterval = den.DebugGetCurrentSpawnInterval();
            playerObject.transform.position = new Vector3(2f, 0f, 0f);
            float nearInterval = den.DebugGetCurrentSpawnInterval();

            Assert.AreEqual(farInterval * 0.5f, nearInterval, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(denObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void SpawnDenConsumesIntervalWhenSpawnSpaceIsBlocked()
    {
        EnemyRegistry.Clear();
        GameObject denObject = new GameObject("den");
        GameObject blocker = CreateBlocker("spawn_blocker", Vector3.zero, 2f);
        try
        {
            SpawnDenController den = CreateDen(denObject, null, new SpawnDenConfig
            {
                id = "blocked_den",
                minWorldHostility = 0f,
                maxHealth = 50f,
                baseSpawnInterval = 1f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic
                {
                    maxAliveEnemies = 2,
                    spawnRadius = 0.25f
                }
            });

            den.DebugTick(0.9f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);
            Assert.Greater(den.DebugGetSpawnTimer(), 0f);

            den.DebugTick(0.2f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);
            Assert.AreEqual(0f, den.DebugGetSpawnTimer(), 0.001f);

            Object.DestroyImmediate(blocker);
            Physics2D.SyncTransforms();
            den.DebugTick(0.9f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);

            den.DebugTick(0.2f);
            Assert.AreEqual(1, den.LivingSpawnedEnemyCount);
        }
        finally
        {
            Object.DestroyImmediate(denObject);
            Object.DestroyImmediate(blocker);
            DestroySpawnedEnemies();
            EnemyRegistry.Clear();
        }
    }

    [Test]
    public void SpawnDenActivationDoesNotSpawnUntilNextInterval()
    {
        EnemyRegistry.Clear();
        GameObject denObject = new GameObject("den");
        GameObject playerObject = new GameObject("player");
        try
        {
            SpawnDenController den = CreateDen(denObject, playerObject.transform, new SpawnDenConfig
            {
                id = "activation_den",
                minWorldHostility = 0f,
                maxHealth = 50f,
                baseSpawnInterval = 1f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic
                {
                    activationRange = 2f,
                    maxAliveEnemies = 2,
                    spawnRadius = 0.25f
                }
            });

            playerObject.transform.position = new Vector3(10f, 0f, 0f);
            den.DebugTick(1.1f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);
            Assert.AreEqual(0f, den.DebugGetSpawnTimer(), 0.001f);

            playerObject.transform.position = new Vector3(1f, 0f, 0f);
            den.DebugTick(0.9f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);

            den.DebugTick(0.2f);
            Assert.AreEqual(1, den.LivingSpawnedEnemyCount);
        }
        finally
        {
            Object.DestroyImmediate(denObject);
            Object.DestroyImmediate(playerObject);
            DestroySpawnedEnemies();
            EnemyRegistry.Clear();
        }
    }

    [Test]
    public void SpawnDenPhaseMultiplierTriggersOnlyOnceOnDamage()
    {
        GameObject denObject = new GameObject("den");
        try
        {
            SpawnDenController den = CreateDen(denObject, null, new SpawnDenConfig
            {
                id = "phase_den",
                minWorldHostility = 0f,
                maxHealth = 100f,
                baseSpawnInterval = 4f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic { maxAliveEnemies = 2 },
                phases = new[]
                {
                    new SpawnDenPhaseConfig
                    {
                        healthPercent = 0.8f,
                        action = "intervalMultiplier",
                        intervalMultiplier = 0.5f
                    }
                }
            });

            Assert.AreEqual(4f, den.DebugGetCurrentSpawnInterval(), 0.001f);
            den.TakeDamage(25f);
            Assert.AreEqual(2f, den.DebugGetCurrentSpawnInterval(), 0.001f);
            den.TakeDamage(1f);
            Assert.AreEqual(2f, den.DebugGetCurrentSpawnInterval(), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(denObject);
        }
    }

    [Test]
    public void SpawnDenBurstSpawnUsesQueuedAttemptsAndSpaceChecks()
    {
        EnemyRegistry.Clear();
        GameObject denObject = new GameObject("den");
        GameObject blocker = CreateBlocker("burst_blocker", Vector3.zero, 2f);
        try
        {
            SpawnDenController den = CreateDen(denObject, null, new SpawnDenConfig
            {
                id = "burst_den",
                minWorldHostility = 0f,
                maxHealth = 100f,
                baseSpawnInterval = 10f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic
                {
                    maxAliveEnemies = 1,
                    spawnRadius = 0.25f
                },
                phases = new[]
                {
                    new SpawnDenPhaseConfig
                    {
                        healthPercent = 0.8f,
                        action = "burstSpawn",
                        enemyArchetypes = "Attacker:1",
                        count = 2,
                        duration = 0f
                    }
                }
            });

            den.TakeDamage(25f);
            Assert.AreEqual(2, den.DebugGetPendingBurstCount());

            den.DebugTick(10.1f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);
            Assert.AreEqual(1, den.DebugGetPendingBurstCount());

            Object.DestroyImmediate(blocker);
            Physics2D.SyncTransforms();
            den.DebugTick(9.9f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);

            den.DebugTick(0.2f);
            Assert.AreEqual(1, den.LivingSpawnedEnemyCount);
            Assert.AreEqual(0, den.DebugGetPendingBurstCount());
        }
        finally
        {
            Object.DestroyImmediate(denObject);
            Object.DestroyImmediate(blocker);
            DestroySpawnedEnemies();
            EnemyRegistry.Clear();
        }
    }

    [Test]
    public void SpawnDenReviveWaitsThenHealsAndCanBeInterrupted()
    {
        GameObject denObject = new GameObject("den");
        try
        {
            SpawnDenController den = CreateDen(denObject, null, new SpawnDenConfig
            {
                id = "revive_den",
                minWorldHostility = 0f,
                maxHealth = 40f,
                baseSpawnInterval = 4f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic { maxAliveEnemies = 1 },
                destroyLogic = new SpawnDenDestroyLogicConfig
                {
                    type = "reviveAfterDelayInterruptible",
                    reviveDelaySeconds = 2f,
                    reviveHealPerSecond = 10f,
                    reviveHealthPercent = 0.5f
                }
            });
            CharacterStats stats = denObject.GetComponent<CharacterStats>();

            den.TakeDamage(100f);
            Assert.IsFalse(den.IsSpawningActive);
            Assert.AreEqual(0f, stats.CurrentHealth, 0.001f);

            den.DebugTick(1.9f);
            Assert.AreEqual(0f, stats.CurrentHealth, 0.001f);

            den.DebugTick(1.0f);
            Assert.Greater(stats.CurrentHealth, 0f);
            Assert.IsFalse(den.IsSpawningActive);

            den.TakeDamage(100f);
            Assert.IsTrue(den.IsDestroyed);
        }
        finally
        {
            Object.DestroyImmediate(denObject);
        }
    }

    [Test]
    public void SpawnDenExternalRequestQueuesInsteadOfSpawningImmediately()
    {
        EnemyRegistry.Clear();
        GameObject denObject = new GameObject("den");
        try
        {
            SpawnDenController den = CreateDen(denObject, null, new SpawnDenConfig
            {
                id = "external_den",
                minWorldHostility = 0f,
                maxHealth = 40f,
                baseSpawnInterval = 1f,
                enemyArchetypes = "Attacker:1",
                spawnLogic = new SpawnDenSpawnLogic
                {
                    maxAliveEnemies = 1,
                    spawnRadius = 0.25f
                }
            });

            den.QueueExternalSpawnRequest("Ranged:1", 2);
            Assert.AreEqual(2, den.DebugGetPendingBurstCount());
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);

            den.DebugTick(0.9f);
            Assert.AreEqual(0, den.LivingSpawnedEnemyCount);

            den.DebugTick(0.2f);
            Assert.AreEqual(1, den.LivingSpawnedEnemyCount);
            Assert.AreEqual(1, den.DebugGetPendingBurstCount());
        }
        finally
        {
            Object.DestroyImmediate(denObject);
            DestroySpawnedEnemies();
            EnemyRegistry.Clear();
        }
    }

    private static SpawnDenController CreateDen(GameObject denObject, Transform playerTarget, SpawnDenConfig config)
    {
        denObject.transform.position = Vector3.zero;
        denObject.AddComponent<CharacterStats>();
        denObject.AddComponent<DamageableHurtbox>();
        SpawnDenController den = denObject.AddComponent<SpawnDenController>();
        den.Configure(config, playerTarget, null, null, 20);
        return den;
    }

    private static GameObject CreateBlocker(string name, Vector3 position, float radius)
    {
        GameObject blocker = new GameObject(name);
        blocker.transform.position = position;
        CircleCollider2D collider = blocker.AddComponent<CircleCollider2D>();
        collider.radius = radius;
        Physics2D.SyncTransforms();
        return blocker;
    }

    private static void DestroySpawnedEnemies()
    {
        SimpleEnemyAI[] enemies = Object.FindObjectsOfType<SimpleEnemyAI>();
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
            {
                Object.DestroyImmediate(enemies[i].gameObject);
            }
        }
    }
}
