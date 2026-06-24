using System;
using UnityEngine;

[Serializable]
public sealed class EnemySenseConfigList
{
    public EnemySenseConfig[] senses;
}

[Serializable]
public sealed class EnemySenseConfig
{
    public EnemyArchetype archetype;
    public SenseVisualConfig visual = new SenseVisualConfig();
    public SenseHearingConfig hearing = new SenseHearingConfig();
    public SenseMagicConfig magic = new SenseMagicConfig();
    public SenseHostilityConfig hostility = new SenseHostilityConfig();
}

[Serializable]
public sealed class SenseVisualConfig
{
    public bool enabled = true;
    public float range = 7f;
    public float rayStep = 0.08f;
    public bool blockedByWalls = true;
}

[Serializable]
public sealed class SenseHearingConfig
{
    public bool enabled = true;
    public float range = 6f;
    public float memorySeconds = 2f;
    public float sprintNoiseMultiplier = 1.6f;
    public float attackNoiseMultiplier = 2.2f;
}

[Serializable]
public sealed class SenseMagicConfig
{
    public bool enabled = false;
    public float range = 0f;
    public float spellCastMemorySeconds = 3f;
    public bool detectsShields = false;
}

[Serializable]
public sealed class SenseHostilityConfig
{
    public bool enabled = true;
    public float range = 0f;
    public bool ignoresLineOfSight = true;
    public float memorySeconds = 1.5f;
    public float signalRadius = 15f;
}

public static class EnemySenseConfigDatabase
{
    private const string ResourcePath = "Configs/enemy_senses";
    private static EnemySenseConfigList configList;
    private static bool loaded;

    public static EnemySenseConfig Get(EnemyArchetype archetype)
    {
        EnsureLoaded();
        if (configList != null && configList.senses != null)
        {
            for (int i = 0; i < configList.senses.Length; i++)
            {
                EnemySenseConfig config = configList.senses[i];
                if (config != null && config.archetype == archetype)
                {
                    return config;
                }
            }
        }

        return CreateFallback(archetype);
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
            configList = new EnemySenseConfigList { senses = Array.Empty<EnemySenseConfig>() };
            return;
        }

        configList = JsonUtility.FromJson<EnemySenseConfigList>(asset.text);
        if (configList == null)
        {
            configList = new EnemySenseConfigList { senses = Array.Empty<EnemySenseConfig>() };
        }
    }

    private static EnemySenseConfig CreateFallback(EnemyArchetype archetype)
    {
        EnemySenseConfig config = new EnemySenseConfig { archetype = archetype };
        switch (archetype)
        {
            case EnemyArchetype.Sentinel:
                config.visual.range = 20f;
                config.hearing.range = 10f;
                config.magic.enabled = true;
                config.magic.range = 12f;
                config.hostility.range = 12f;
                config.hostility.signalRadius = 18f;
                break;
            case EnemyArchetype.Ranged:
                config.visual.range = 15f;
                config.hearing.range = 7f;
                config.magic.enabled = true;
                config.magic.range = 8f;
                config.hostility.range = 6f;
                break;
            case EnemyArchetype.Shield:
                config.visual.range = 5f;
                config.hearing.range = 5f;
                config.hostility.range = 4f;
                break;
            default:
                config.visual.range = 7f;
                config.hearing.range = 6f;
                config.hostility.range = 5f;
                break;
        }

        return config;
    }
}
