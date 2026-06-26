using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class EnemyConfig
{
    public EnemyArchetype archetype;
    public string displayName;
    public string spriteResource;
    public int spawnWeight;
    public int minHostilityLevel;
    public float weightPerHostilityLevel;
    public string hostilityWeightCurve;
    public float maxHealth;
    public float armor;
    public float magicImmunity;
    public float physicalAttack;
    public float magicAttack;
    public float moveSpeed;
    public float moveDelay;
    public float visionRange;
    public float stopDistance;
    public float colliderRadius;
    public float knockbackDistance;
    public float knockbackDuration;
    public float accelerationTime;
    public float meleeAttackRange;
    public float meleeAttackCooldown;
    public float meleeRecoverDistance;
    public float rangedPreferredDistance;
    public float rangedAttackCooldown;
    public float rangedProjectileSpeed;
    public float rangedProjectileDistance;
    public float sentinelSignalRadius;
    public float sentinelFleeDuration;
    public EnemyDropConfig[] drops;
    public EnemySkillConfig[] skills;
    public EnemyPhaseConfig[] phases;
}

[Serializable]
public sealed class EnemyDropConfig
{
    public string id;
    public string type;
    public float chance = 1f;
    public int min = 1;
    public int max = 1;
    public string rarity;
    public float hostilityMultiplier = 1f;
    public float hostilityChanceBonus;
}

[Serializable]
public sealed class EnemySkillConfig
{
    public string id;
    public string trigger;
    public float cooldown;
    public int minHostilityLevel;
}

[Serializable]
public sealed class EnemyPhaseConfig
{
    public float atHealthRatio = 1f;
    public EnemyStatMultipliers statMultipliers;
    public string[] addSkills;
}

[Serializable]
public sealed class EnemyStatMultipliers
{
    public float maxHealth = 1f;
    public float armor = 1f;
    public float attack = 1f;
    public float moveSpeed = 1f;
}

[Serializable]
public sealed class EnemyConfigJsonList
{
    public EnemyConfigJson[] enemies;
}

[Serializable]
public sealed class EnemyConfigJson
{
    public string archetype;
    public string displayName;
    public string spriteResource;
    public EnemyBaseStatsJson baseStats;
    public EnemySpawnJson spawn;
    public EnemyBehaviorJson behavior;
    public EnemyDropConfig[] drops;
    public EnemySkillConfig[] skills;
    public EnemyPhaseConfig[] phases;
}

[Serializable]
public sealed class EnemyBaseStatsJson
{
    public float maxHealth = 30f;
    public float armor;
    public float magicImmunity;
    public float attack = 6f;
    public float physicalAttack = -1f;
    public float magicAttack = -1f;
    public float moveSpeed = 2f;
    public float moveDelay = 0.15f;
    public float colliderRadius = 0.38f;
    public float knockbackDistance = 2f;
    public float knockbackDuration = 0.28f;
    public float accelerationTime = 0.35f;
}

[Serializable]
public sealed class EnemySpawnJson
{
    public int baseWeight = 1;
    public int minHostilityLevel;
    public float weightPerHostilityLevel;
    public string hostilityWeightCurve;
}

[Serializable]
public sealed class EnemyBehaviorJson
{
    public float visionRange = 7f;
    public float stopDistance = 0.25f;
    public float meleeAttackRange = 0.72f;
    public float meleeAttackCooldown = 1.4f;
    public float meleeRecoverDistance = 2.3f;
    public float rangedPreferredDistance = 3.6f;
    public float rangedAttackCooldown = 1.8f;
    public float rangedProjectileSpeed = 7f;
    public float rangedProjectileDistance = 6f;
    public float sentinelSignalRadius = 15f;
    public float sentinelFleeDuration = 1.8f;
}

public static class EnemyConfigDatabase
{
    private const string ResourcePath = "Configs/enemy_config";
    private static readonly Dictionary<EnemyArchetype, EnemyConfig> configs = new Dictionary<EnemyArchetype, EnemyConfig>();
    private static bool loaded;

    public static EnemyConfig Get(EnemyArchetype archetype)
    {
        EnsureLoaded();
        if (configs.TryGetValue(archetype, out EnemyConfig config))
        {
            return config;
        }

        return CreateFallback(archetype);
    }

    public static EnemyArchetype PickWeighted()
    {
        EnsureLoaded();
        return PickWeighted(WorldHostilityDirector.Current);
    }

    public static EnemyArchetype PickWeighted(WorldHostilityDirector director)
    {
        EnsureLoaded();
        float total = 0f;
        foreach (EnemyConfig config in configs.Values)
        {
            total += GetHostilitySpawnWeight(config, director);
        }

        if (total <= 0)
        {
            return EnemyArchetype.Attacker;
        }

        float roll = UnityEngine.Random.Range(0f, total);
        foreach (EnemyConfig config in configs.Values)
        {
            float weight = GetHostilitySpawnWeight(config, director);
            if (roll < weight)
            {
                return config.archetype;
            }
            roll -= weight;
        }

        return EnemyArchetype.Attacker;
    }

    public static float GetHostilitySpawnWeight(EnemyConfig config, WorldHostilityDirector director)
    {
        if (config == null)
        {
            return 0f;
        }

        WorldHostilityDirector activeDirector = director ?? WorldHostilityDirector.Current;
        int hostilityLevel = activeDirector != null ? activeDirector.HostilityLevel : 0;
        if (hostilityLevel < config.minHostilityLevel)
        {
            return 0f;
        }

        float unlockedLevels = Mathf.Max(0, hostilityLevel - config.minHostilityLevel);
        float curveBonus = config.weightPerHostilityLevel * unlockedLevels;
        if (string.Equals(config.hostilityWeightCurve, "steep", StringComparison.OrdinalIgnoreCase))
        {
            curveBonus *= 1f + unlockedLevels * 0.35f;
        }

        return Mathf.Max(0f, config.spawnWeight + curveBonus);
    }

    public static Sprite LoadSprite(EnemyArchetype archetype)
    {
        EnemyConfig config = Get(archetype);
        return !string.IsNullOrEmpty(config.spriteResource) ? Resources.Load<Sprite>(config.spriteResource) : null;
    }

    public static EnemyArchetype ResolveArchetypeName(string name, EnemyArchetype fallback = EnemyArchetype.Sentinel)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        if (Enum.TryParse(name.Trim(), true, out EnemyArchetype parsed))
        {
            return parsed;
        }

        foreach (EnemyConfig config in configs.Values)
        {
            if (config == null || string.IsNullOrEmpty(config.displayName))
            {
                continue;
            }

            if (string.Equals(config.displayName, name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return config.archetype;
            }
        }

        return fallback;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        configs.Clear();
        TextAsset jsonAsset = FindEnemyConfigAsset(true);
        if (jsonAsset != null && TryLoadJson(jsonAsset.text))
        {
            return;
        }

        TextAsset csvAsset = FindEnemyConfigAsset(false);
        if (csvAsset == null)
        {
            LoadFallbacks();
            return;
        }

        LoadCsv(csvAsset.text);

        if (configs.Count == 0)
        {
            LoadFallbacks();
        }
    }

    private static TextAsset FindEnemyConfigAsset(bool json)
    {
        TextAsset[] assets = Resources.LoadAll<TextAsset>("Configs");
        for (int i = 0; i < assets.Length; i++)
        {
            TextAsset asset = assets[i];
            if (asset == null || asset.name != "enemy_config")
            {
                continue;
            }

            string text = asset.text != null ? asset.text.TrimStart() : string.Empty;
            bool looksLikeJson = text.StartsWith("{", StringComparison.Ordinal);
            if (looksLikeJson == json)
            {
                return asset;
            }
        }

        return null;
    }

    private static bool TryLoadJson(string json)
    {
        EnemyConfigJsonList list = JsonUtility.FromJson<EnemyConfigJsonList>(json);
        if (list == null || list.enemies == null || list.enemies.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < list.enemies.Length; i++)
        {
            EnemyConfigJson row = list.enemies[i];
            if (row == null || !Enum.TryParse(row.archetype, out EnemyArchetype archetype))
            {
                Debug.LogWarning($"Enemy JSON row {i + 1} has unknown archetype: {row?.archetype}");
                continue;
            }

            EnemyBaseStatsJson stats = row.baseStats ?? new EnemyBaseStatsJson();
            EnemySpawnJson spawn = row.spawn ?? new EnemySpawnJson();
            EnemyBehaviorJson behavior = row.behavior ?? new EnemyBehaviorJson();
            configs[archetype] = new EnemyConfig
            {
                archetype = archetype,
                displayName = row.displayName,
                spriteResource = row.spriteResource,
                spawnWeight = Mathf.Max(0, spawn.baseWeight),
                minHostilityLevel = Mathf.Max(0, spawn.minHostilityLevel),
                weightPerHostilityLevel = Mathf.Max(0f, spawn.weightPerHostilityLevel),
                hostilityWeightCurve = spawn.hostilityWeightCurve,
                maxHealth = stats.maxHealth,
                armor = stats.armor,
                magicImmunity = Mathf.Clamp01(stats.magicImmunity),
                physicalAttack = ResolvePhysicalAttack(stats, archetype),
                magicAttack = ResolveMagicAttack(stats, archetype),
                moveSpeed = stats.moveSpeed,
                moveDelay = stats.moveDelay,
                visionRange = behavior.visionRange,
                stopDistance = behavior.stopDistance,
                colliderRadius = stats.colliderRadius,
                knockbackDistance = stats.knockbackDistance,
                knockbackDuration = stats.knockbackDuration,
                accelerationTime = stats.accelerationTime,
                meleeAttackRange = behavior.meleeAttackRange,
                meleeAttackCooldown = behavior.meleeAttackCooldown,
                meleeRecoverDistance = behavior.meleeRecoverDistance,
                rangedPreferredDistance = behavior.rangedPreferredDistance,
                rangedAttackCooldown = behavior.rangedAttackCooldown,
                rangedProjectileSpeed = behavior.rangedProjectileSpeed,
                rangedProjectileDistance = behavior.rangedProjectileDistance,
                sentinelSignalRadius = behavior.sentinelSignalRadius,
                sentinelFleeDuration = behavior.sentinelFleeDuration,
                drops = row.drops ?? Array.Empty<EnemyDropConfig>(),
                skills = row.skills ?? Array.Empty<EnemySkillConfig>(),
                phases = row.phases ?? Array.Empty<EnemyPhaseConfig>()
            };
        }

        return configs.Count > 0;
    }

    private static void LoadCsv(string csv)
    {
        string[] lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            string[] cells = line.Split(',');
            if (cells.Length < 24)
            {
                Debug.LogWarning($"Enemy config row {i + 1} has {cells.Length} columns, expected 24.");
                continue;
            }

            if (!Enum.TryParse(cells[0], out EnemyArchetype archetype))
            {
                Debug.LogWarning($"Enemy config row {i + 1} has unknown archetype: {cells[0]}");
                continue;
            }

            configs[archetype] = new EnemyConfig
            {
                archetype = archetype,
                displayName = cells[1],
                spriteResource = cells[2],
                spawnWeight = ParseInt(cells[3], 1),
                minHostilityLevel = 0,
                weightPerHostilityLevel = 0f,
                maxHealth = ParseFloat(cells[4], 30f),
                armor = ParseFloat(cells[5], 0f),
                magicImmunity = 0f,
                physicalAttack = GetFallbackAttackDamageType(archetype) == LegacyDamageKind.Physical ? ParseFloat(cells[6], 6f) : 0f,
                magicAttack = GetFallbackAttackDamageType(archetype) == LegacyDamageKind.Magic ? ParseFloat(cells[6], 6f) : 0f,
                moveSpeed = ParseFloat(cells[7], 2f),
                moveDelay = ParseFloat(cells[8], 0.15f),
                visionRange = ParseFloat(cells[9], 7f),
                stopDistance = ParseFloat(cells[10], 0.25f),
                colliderRadius = ParseFloat(cells[11], 0.38f),
                knockbackDistance = ParseFloat(cells[12], 2f),
                knockbackDuration = ParseFloat(cells[13], 0.28f),
                accelerationTime = ParseFloat(cells[14], 0.35f),
                meleeAttackRange = ParseFloat(cells[15], 0.72f),
                meleeAttackCooldown = ParseFloat(cells[16], 1.4f),
                meleeRecoverDistance = ParseFloat(cells[17], 2.3f),
                rangedPreferredDistance = ParseFloat(cells[18], 3.6f),
                rangedAttackCooldown = ParseFloat(cells[19], 1.8f),
                rangedProjectileSpeed = ParseFloat(cells[20], 7f),
                rangedProjectileDistance = ParseFloat(cells[21], 6f),
                sentinelSignalRadius = ParseFloat(cells[22], 15f),
                sentinelFleeDuration = ParseFloat(cells[23], 1.8f),
                drops = Array.Empty<EnemyDropConfig>(),
                skills = Array.Empty<EnemySkillConfig>(),
                phases = Array.Empty<EnemyPhaseConfig>()
            };
        }
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    private static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static void LoadFallbacks()
    {
        configs[EnemyArchetype.Attacker] = CreateFallback(EnemyArchetype.Attacker);
        configs[EnemyArchetype.Sentinel] = CreateFallback(EnemyArchetype.Sentinel);
        configs[EnemyArchetype.Ranged] = CreateFallback(EnemyArchetype.Ranged);
        configs[EnemyArchetype.Shield] = CreateFallback(EnemyArchetype.Shield);
    }

    private static EnemyConfig CreateFallback(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Sentinel:
                return CreateFallback(archetype, "Scout", "Arts/Enemies/enemy_circle_scout", 1, 40f, 1f, 2.15f, 20f, 0.38f);
            case EnemyArchetype.Ranged:
                return CreateFallback(archetype, "Ranged", "Arts/Enemies/enemy_pentagon_ranged", 2, 24f, 0f, 2.2f, 15f, 0.38f);
            case EnemyArchetype.Shield:
                return CreateFallback(archetype, "Shield", "Arts/Enemies/enemy_square_guard", 2, 65f, 4f, 1.25f, 5f, 0.46f);
            default:
                return CreateFallback(archetype, "Warrior", "Arts/Enemies/enemy_triangle_chaser", 3, 30f, 0.5f, 2.45f, 7f, 0.38f);
        }
    }

    private static EnemyConfig CreateFallback(EnemyArchetype archetype, string displayName, string spriteResource, int weight, float health, float armor, float speed, float vision, float colliderRadius)
    {
        return new EnemyConfig
        {
            archetype = archetype,
            displayName = displayName,
            spriteResource = spriteResource,
            spawnWeight = weight,
            minHostilityLevel = 0,
            weightPerHostilityLevel = 0f,
            maxHealth = health,
            armor = armor,
            magicImmunity = 0f,
            physicalAttack = GetFallbackAttackDamageType(archetype) == LegacyDamageKind.Physical ? GetFallbackAttack(archetype) : 0f,
            magicAttack = GetFallbackAttackDamageType(archetype) == LegacyDamageKind.Magic ? GetFallbackAttack(archetype) : 0f,
            moveSpeed = speed,
            moveDelay = 0.15f,
            visionRange = vision,
            stopDistance = archetype == EnemyArchetype.Ranged ? 3.6f : 0.25f,
            colliderRadius = colliderRadius,
            knockbackDistance = 2f,
            knockbackDuration = 0.28f,
            accelerationTime = 0.35f,
            meleeAttackRange = archetype == EnemyArchetype.Shield ? 0.9f : 0.72f,
            meleeAttackCooldown = archetype == EnemyArchetype.Shield ? 1.75f : 1.4f,
            meleeRecoverDistance = 2.3f,
            rangedPreferredDistance = 3.6f,
            rangedAttackCooldown = 1.8f,
            rangedProjectileSpeed = 7f,
            rangedProjectileDistance = 6f,
            sentinelSignalRadius = 15f,
            sentinelFleeDuration = 1.8f,
            drops = Array.Empty<EnemyDropConfig>(),
            skills = Array.Empty<EnemySkillConfig>(),
            phases = Array.Empty<EnemyPhaseConfig>()
        };
    }

    private static float GetFallbackAttack(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Sentinel:
                return 4f;
            case EnemyArchetype.Ranged:
                return 6f;
            case EnemyArchetype.Shield:
                return 10f;
            default:
                return 8f;
        }
    }

    private enum LegacyDamageKind
    {
        Physical,
        Magic
    }

    private static LegacyDamageKind GetFallbackAttackDamageType(EnemyArchetype archetype)
    {
        return archetype == EnemyArchetype.Ranged ? LegacyDamageKind.Magic : LegacyDamageKind.Physical;
    }

    private static float ResolvePhysicalAttack(EnemyBaseStatsJson stats, EnemyArchetype archetype)
    {
        if (stats.physicalAttack >= 0f || stats.magicAttack >= 0f)
        {
            return Mathf.Max(0f, stats.physicalAttack);
        }

        return GetFallbackAttackDamageType(archetype) == LegacyDamageKind.Physical ? Mathf.Max(0f, stats.attack) : 0f;
    }

    private static float ResolveMagicAttack(EnemyBaseStatsJson stats, EnemyArchetype archetype)
    {
        if (stats.physicalAttack >= 0f || stats.magicAttack >= 0f)
        {
            return Mathf.Max(0f, stats.magicAttack);
        }

        return GetFallbackAttackDamageType(archetype) == LegacyDamageKind.Magic ? Mathf.Max(0f, stats.attack) : 0f;
    }
}
