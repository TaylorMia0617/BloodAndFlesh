using UnityEngine;

public sealed class WorldHostilityDirector
{
    private const int ChaseLevel = 6;

    private readonly StageConfig stageConfig;

    public WorldHostilityDirector(StageConfig config)
    {
        stageConfig = config;
        float raw = RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentWorldHostility : config != null ? config.worldHostility : 0f;
        HostilityLevel = Mathf.Max(0, Mathf.FloorToInt(raw));
        HostilityIntensity = Mathf.Max(0f, raw - HostilityLevel);
        RawHostility = Mathf.Max(0f, raw);
    }

    public static WorldHostilityDirector Current => new WorldHostilityDirector(RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentStageConfig : null);

    public int HostilityLevel { get; }
    public float HostilityIntensity { get; }
    public float RawHostility { get; }
    public float DropChanceMultiplier => 1f + RawHostility * 0.06f;
    public float DropAmountMultiplier => 1f + RawHostility * 0.04f;
    public float ShopRarityMultiplier => 1f + RawHostility * 0.18f;
    public float BuffRarityMultiplier => 1f + RawHostility * 0.14f;
    public float EnemyCountMultiplier => 1f + RawHostility * 0.1f;
    public int ExtraSpawnPointCount => Mathf.Clamp(HostilityLevel - 1, 0, 6);
    public bool ForceDirectAttack => HostilityLevel >= ChaseLevel;
    public float SpawnIntervalMultiplier => Mathf.Clamp(1f - RawHostility * 0.055f, 0.45f, 1f);
    public int StageEnemyBudgetBonus => Mathf.FloorToInt(RawHostility * 2f);

    public EnemyArchetype PickEnemyArchetype()
    {
        return EnemyConfigDatabase.PickWeighted(this);
    }

    public int ModifyInitialEnemyCount(int baseCount)
    {
        return Mathf.Max(0, Mathf.RoundToInt(baseCount * EnemyCountMultiplier));
    }

    public int ModifyEnemyBudget(int baseBudget)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseBudget * EnemyCountMultiplier) + StageEnemyBudgetBonus);
    }

    public float ModifySpawnInterval(float baseInterval)
    {
        return Mathf.Max(0.25f, baseInterval * SpawnIntervalMultiplier);
    }

    public float GetRarityWeightMultiplier(string rarity, bool cursedAllowed = false)
    {
        switch ((rarity ?? string.Empty).ToLowerInvariant())
        {
            case "rare":
                return RarityScale(ShopRarityMultiplier);
            case "cursed":
                return cursedAllowed || HostilityLevel >= 3 ? RarityScale(ShopRarityMultiplier * 1.35f) : 0.45f;
            case "legendary":
                return RarityScale(ShopRarityMultiplier * 1.7f);
            default:
                return 1f;
        }
    }

    public float GetBuffRarityWeightMultiplier(string rarity)
    {
        switch ((rarity ?? string.Empty).ToLowerInvariant())
        {
            case "rare":
                return RarityScale(BuffRarityMultiplier);
            case "cursed":
                return HostilityLevel >= 3 ? RarityScale(BuffRarityMultiplier * 1.25f) : 0.35f;
            default:
                return 1f;
        }
    }

    private static float RarityScale(float value)
    {
        return Mathf.Clamp(value, 0.1f, 5f);
    }
}
