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
    public float maxHealth;
    public float armor;
    public float attack;
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
        int total = 0;
        foreach (EnemyConfig config in configs.Values)
        {
            total += Mathf.Max(0, config.spawnWeight);
        }

        if (total <= 0)
        {
            return EnemyArchetype.Attacker;
        }

        int roll = UnityEngine.Random.Range(0, total);
        foreach (EnemyConfig config in configs.Values)
        {
            int weight = Mathf.Max(0, config.spawnWeight);
            if (roll < weight)
            {
                return config.archetype;
            }
            roll -= weight;
        }

        return EnemyArchetype.Attacker;
    }

    public static Sprite LoadSprite(EnemyArchetype archetype)
    {
        EnemyConfig config = Get(archetype);
        return !string.IsNullOrEmpty(config.spriteResource) ? Resources.Load<Sprite>(config.spriteResource) : null;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        configs.Clear();
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            LoadFallbacks();
            return;
        }

        string[] lines = asset.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
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
                maxHealth = ParseFloat(cells[4], 30f),
                armor = ParseFloat(cells[5], 0f),
                attack = ParseFloat(cells[6], 6f),
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
                sentinelFleeDuration = ParseFloat(cells[23], 1.8f)
            };
        }

        if (configs.Count == 0)
        {
            LoadFallbacks();
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
            maxHealth = health,
            armor = armor,
            attack = GetFallbackAttack(archetype),
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
            sentinelFleeDuration = 1.8f
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
}
