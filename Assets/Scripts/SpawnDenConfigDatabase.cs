using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SpawnDenConfigList
{
    public SpawnDenPoolConfig[] spawnDenPools;
}

[Serializable]
public sealed class SpawnDenPoolConfig
{
    public string id = "default";
    public SpawnDenConfig[] entries;
}

[Serializable]
public sealed class SpawnDenConfig
{
    public string id = "spawn_den";
    public int level = 1;
    public float minWorldHostility;
    public float baseWeight = 1f;
    public float weightPerHostilityAboveMin;
    public string weightCurve = "linear";
    public float maxHealth = 80f;
    public float baseSpawnInterval = 4f;
    public string enemyArchetypes = "Attacker:3,Ranged:2";
    public SpawnDenSpawnLogic spawnLogic = new SpawnDenSpawnLogic();
    public SpawnDenPhaseConfig[] phases = Array.Empty<SpawnDenPhaseConfig>();
    public SpawnDenDestroyLogicConfig destroyLogic = new SpawnDenDestroyLogicConfig();
}

[Serializable]
public sealed class SpawnDenSpawnLogic
{
    public float activationRange = 0f;
    public float accelerationRange = 0f;
    public float acceleratedIntervalMultiplier = 0.65f;
    public int maxAliveEnemies = 4;
    public float spawnRadius = 0.25f;
}

[Serializable]
public sealed class SpawnDenPhaseConfig
{
    public float healthPercent = 1f;
    public string action = "intervalMultiplier";
    public string enemyArchetypes;
    public int count = 1;
    public float duration = 0f;
    public float intervalMultiplier = 1f;
}

[Serializable]
public sealed class SpawnDenDestroyLogicConfig
{
    public string type = "destroyOnDeath";
    public float reviveDelaySeconds = 5f;
    public float reviveHealPerSecond = 12f;
    public float reviveHealthPercent = 0.5f;
}

public static class SpawnDenConfigDatabase
{
    private const string ResourcePath = "Configs/spawn_den_config";
    private static SpawnDenConfigList configList;
    private static bool loaded;

    public static SpawnDenConfig[] PickSpawnDens(
        string poolId,
        int count,
        float worldHostility,
        bool allowDuplicate,
        float weightMultiplier = 1f,
        float levelBias = 0f)
    {
        EnsureLoaded();
        if (count <= 0)
        {
            return Array.Empty<SpawnDenConfig>();
        }

        List<Candidate> candidates = BuildEligibleCandidates(poolId, worldHostility, weightMultiplier, levelBias);
        if (candidates.Count == 0)
        {
            return Array.Empty<SpawnDenConfig>();
        }

        List<SpawnDenConfig> picked = new List<SpawnDenConfig>();
        int targetCount = Mathf.Max(0, count);
        for (int i = 0; i < targetCount; i++)
        {
            if (candidates.Count == 0)
            {
                break;
            }

            int pickedIndex = PickWeightedIndex(candidates);
            if (pickedIndex < 0)
            {
                break;
            }

            picked.Add(CloneSpawnDenConfig(candidates[pickedIndex].config));
            if (!allowDuplicate)
            {
                candidates.RemoveAt(pickedIndex);
            }
        }

        return picked.ToArray();
    }

    public static float CalculateWeight(SpawnDenConfig config, float worldHostility, float weightMultiplier = 1f, float levelBias = 0f)
    {
        if (config == null || worldHostility + 0.001f < Mathf.Max(0f, config.minWorldHostility))
        {
            return 0f;
        }

        float overMin = Mathf.Max(0f, worldHostility - Mathf.Max(0f, config.minWorldHostility));
        float bonus = overMin * Mathf.Max(0f, config.weightPerHostilityAboveMin);
        if (string.Equals(config.weightCurve, "steep", StringComparison.OrdinalIgnoreCase))
        {
            bonus *= 1f + overMin * 0.35f;
        }

        float levelMultiplier = Mathf.Max(0.05f, 1f + Mathf.Max(0, config.level - 1) * levelBias);
        return Mathf.Max(0f, (Mathf.Max(0f, config.baseWeight) + bonus) * Mathf.Max(0f, weightMultiplier) * levelMultiplier);
    }

    private static List<Candidate> BuildEligibleCandidates(string poolId, float worldHostility, float weightMultiplier, float levelBias)
    {
        SpawnDenPoolConfig pool = FindPool(poolId);
        List<Candidate> candidates = new List<Candidate>();
        if (pool == null || pool.entries == null)
        {
            return candidates;
        }

        for (int i = 0; i < pool.entries.Length; i++)
        {
            SpawnDenConfig entry = pool.entries[i];
            float weight = CalculateWeight(entry, worldHostility, weightMultiplier, levelBias);
            if (weight > 0f)
            {
                candidates.Add(new Candidate(entry, weight));
            }
        }

        return candidates;
    }

    private static SpawnDenPoolConfig FindPool(string poolId)
    {
        string requestedId = string.IsNullOrEmpty(poolId) ? "default" : poolId;
        if (configList == null || configList.spawnDenPools == null)
        {
            return null;
        }

        SpawnDenPoolConfig fallback = null;
        for (int i = 0; i < configList.spawnDenPools.Length; i++)
        {
            SpawnDenPoolConfig pool = configList.spawnDenPools[i];
            if (pool == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = pool;
            }

            if (string.Equals(pool.id, requestedId, StringComparison.OrdinalIgnoreCase))
            {
                return pool;
            }
        }

        return fallback;
    }

    private static int PickWeightedIndex(List<Candidate> candidates)
    {
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            total += Mathf.Max(0f, candidates[i].weight);
        }

        if (total <= 0f)
        {
            return -1;
        }

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            float weight = Mathf.Max(0f, candidates[i].weight);
            if (roll < weight)
            {
                return i;
            }

            roll -= weight;
        }

        return candidates.Count - 1;
    }

    private static SpawnDenConfig CloneSpawnDenConfig(SpawnDenConfig source)
    {
        source = source ?? new SpawnDenConfig();
        return new SpawnDenConfig
        {
            id = string.IsNullOrEmpty(source.id) ? "spawn_den" : source.id,
            level = Mathf.Max(1, source.level),
            minWorldHostility = Mathf.Max(0f, source.minWorldHostility),
            baseWeight = Mathf.Max(0f, source.baseWeight),
            weightPerHostilityAboveMin = Mathf.Max(0f, source.weightPerHostilityAboveMin),
            weightCurve = string.IsNullOrEmpty(source.weightCurve) ? "linear" : source.weightCurve,
            maxHealth = Mathf.Max(1f, source.maxHealth),
            baseSpawnInterval = Mathf.Max(0.25f, source.baseSpawnInterval),
            enemyArchetypes = string.IsNullOrEmpty(source.enemyArchetypes) ? "Attacker:1" : source.enemyArchetypes,
            spawnLogic = CloneSpawnLogic(source.spawnLogic),
            phases = ClonePhases(source.phases),
            destroyLogic = CloneDestroyLogic(source.destroyLogic)
        };
    }

    private static SpawnDenSpawnLogic CloneSpawnLogic(SpawnDenSpawnLogic source)
    {
        source = source ?? new SpawnDenSpawnLogic();
        return new SpawnDenSpawnLogic
        {
            activationRange = Mathf.Max(0f, source.activationRange),
            accelerationRange = Mathf.Max(0f, source.accelerationRange),
            acceleratedIntervalMultiplier = Mathf.Clamp(source.acceleratedIntervalMultiplier, 0.05f, 1f),
            maxAliveEnemies = Mathf.Max(0, source.maxAliveEnemies),
            spawnRadius = Mathf.Max(0.01f, source.spawnRadius)
        };
    }

    private static SpawnDenPhaseConfig[] ClonePhases(SpawnDenPhaseConfig[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<SpawnDenPhaseConfig>();
        }

        SpawnDenPhaseConfig[] clone = new SpawnDenPhaseConfig[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            SpawnDenPhaseConfig phase = source[i] ?? new SpawnDenPhaseConfig();
            clone[i] = new SpawnDenPhaseConfig
            {
                healthPercent = Mathf.Clamp01(phase.healthPercent),
                action = phase.action,
                enemyArchetypes = phase.enemyArchetypes,
                count = Mathf.Max(1, phase.count),
                duration = Mathf.Max(0f, phase.duration),
                intervalMultiplier = Mathf.Max(0.05f, phase.intervalMultiplier)
            };
        }

        return clone;
    }

    private static SpawnDenDestroyLogicConfig CloneDestroyLogic(SpawnDenDestroyLogicConfig source)
    {
        source = source ?? new SpawnDenDestroyLogicConfig();
        return new SpawnDenDestroyLogicConfig
        {
            type = string.IsNullOrEmpty(source.type) ? "destroyOnDeath" : source.type,
            reviveDelaySeconds = Mathf.Max(0f, source.reviveDelaySeconds),
            reviveHealPerSecond = Mathf.Max(0f, source.reviveHealPerSecond),
            reviveHealthPercent = Mathf.Clamp01(source.reviveHealthPercent)
        };
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            configList = new SpawnDenConfigList { spawnDenPools = Array.Empty<SpawnDenPoolConfig>() };
            return;
        }

        configList = JsonUtility.FromJson<SpawnDenConfigList>(asset.text);
        if (configList == null)
        {
            configList = new SpawnDenConfigList { spawnDenPools = Array.Empty<SpawnDenPoolConfig>() };
        }
    }

    private readonly struct Candidate
    {
        public readonly SpawnDenConfig config;
        public readonly float weight;

        public Candidate(SpawnDenConfig config, float weight)
        {
            this.config = config;
            this.weight = weight;
        }
    }
}
