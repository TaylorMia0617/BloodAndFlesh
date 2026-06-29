using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public enum DirectorEventType : byte
{
    StageStarted,
    PlayerEnteredSafehouse,
    PlayerExitedSafehouse,
    PlayerDamaged,
    EnemyKilled,
    SpawnDenDestroyed,
    SentinelAlarm,
    LoudAttack,
    MagicUsed,
    ValuableLootPicked,
    TaskCompleted,
    TaskFailed,
    PlayerLingered
}

public readonly struct DirectorEvent
{
    public readonly DirectorEventType Type;
    public readonly Vector2 WorldPosition;
    public readonly float Magnitude;
    public readonly string Id;

    public DirectorEvent(DirectorEventType type, Vector2 worldPosition, float magnitude = 1f, string id = null)
    {
        Type = type;
        WorldPosition = worldPosition;
        Magnitude = Mathf.Max(0f, magnitude);
        Id = id ?? string.Empty;
    }
}

public readonly struct DirectorSnapshot
{
    public readonly float BaseHostility;
    public readonly float GlobalPressure;
    public readonly float SearchIntensity;
    public readonly float GreedScalar;
    public readonly bool PlayerInSafehouse;

    public DirectorSnapshot(float baseHostility, float globalPressure, float searchIntensity, float greedScalar, bool playerInSafehouse)
    {
        BaseHostility = Mathf.Max(0f, baseHostility);
        GlobalPressure = Mathf.Max(0f, globalPressure);
        SearchIntensity = Mathf.Max(0f, searchIntensity);
        GreedScalar = Mathf.Max(0f, greedScalar);
        PlayerInSafehouse = playerInSafehouse;
    }
}

public sealed class WorldHostilityDirector
{
    private const int ChaseLevel = 6;
    private const float LocalPressureRadius = 9f;
    private const int MaxLocalPressurePoints = 64;
    private static readonly ProfilerMarker TickMarker = new ProfilerMarker("SemanticWorld.Director.Tick");
    private static readonly ProfilerMarker NotifyMarker = new ProfilerMarker("SemanticWorld.Director.Notify");

    private static WorldHostilityDirector runtime;

    private readonly StageConfig stageConfig;
    private readonly List<LocalPressurePoint> localPressure = new List<LocalPressurePoint>();

    private float eventPressure;
    private float searchIntensity;
    private float greedScalar = 1f;
    private bool playerInSafehouse;

    public WorldHostilityDirector(StageConfig config)
    {
        stageConfig = config;
        BaseHostility = ResolveBaseHostility(config);
        Recalculate();
    }

    public static WorldHostilityDirector Current
    {
        get
        {
            StageConfig config = RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentStageConfig : null;
            if (runtime == null || !runtime.Matches(config))
            {
                runtime = new WorldHostilityDirector(config);
            }

            return runtime;
        }
    }

    public int HostilityLevel { get; private set; }
    public float HostilityIntensity { get; private set; }
    public float RawHostility { get; private set; }
    public float BaseHostility { get; }
    public DirectorSnapshot Snapshot => new DirectorSnapshot(BaseHostility, eventPressure, searchIntensity, greedScalar, playerInSafehouse);
    public float DropChanceMultiplier => 1f + RawHostility * 0.06f;
    public float DropAmountMultiplier => 1f + RawHostility * 0.04f;
    public float ShopRarityMultiplier => 1f + RawHostility * 0.18f;
    public float BuffRarityMultiplier => 1f + RawHostility * 0.14f;
    public float EnemyCountMultiplier => 1f + RawHostility * 0.1f;
    public int ExtraSpawnPointCount => Mathf.Clamp(HostilityLevel - 1, 0, 6);
    public bool ForceDirectAttack => HostilityLevel >= ChaseLevel;
    public float SpawnIntervalMultiplier => Mathf.Clamp(1f - RawHostility * 0.055f, 0.35f, 1f);
    public int StageEnemyBudgetBonus => Mathf.FloorToInt(RawHostility * 2f);

    public static void ResetRuntime(StageConfig config = null)
    {
        runtime = new WorldHostilityDirector(config ?? (RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentStageConfig : null));
    }

    public void Notify(in DirectorEvent directorEvent)
    {
        using ProfilerMarker.AutoScope scope = NotifyMarker.Auto();
        float magnitude = Mathf.Max(0.1f, directorEvent.Magnitude);
        switch (directorEvent.Type)
        {
            case DirectorEventType.StageStarted:
                eventPressure = 0f;
                searchIntensity = 0f;
                greedScalar = 1f;
                playerInSafehouse = false;
                localPressure.Clear();
                break;
            case DirectorEventType.PlayerEnteredSafehouse:
                playerInSafehouse = true;
                eventPressure = Mathf.Max(0f, eventPressure - 0.75f * magnitude);
                searchIntensity = Mathf.Max(0f, searchIntensity - 0.5f * magnitude);
                break;
            case DirectorEventType.PlayerExitedSafehouse:
                playerInSafehouse = false;
                AddGlobalPressure(0.2f * magnitude);
                break;
            case DirectorEventType.PlayerDamaged:
                AddGlobalPressure(0.25f * magnitude);
                AddLocalPressure(directorEvent.WorldPosition, 0.45f * magnitude);
                break;
            case DirectorEventType.EnemyKilled:
                AddGlobalPressure(0.08f * magnitude);
                AddLocalPressure(directorEvent.WorldPosition, 0.6f * magnitude);
                break;
            case DirectorEventType.SpawnDenDestroyed:
                AddGlobalPressure(0.35f * magnitude);
                AddLocalPressure(directorEvent.WorldPosition, 1.1f * magnitude);
                break;
            case DirectorEventType.SentinelAlarm:
                AddGlobalPressure(0.9f * magnitude);
                searchIntensity += 1.2f * magnitude;
                AddLocalPressure(directorEvent.WorldPosition, 1.5f * magnitude);
                break;
            case DirectorEventType.LoudAttack:
                AddGlobalPressure(0.18f * magnitude);
                searchIntensity += 0.35f * magnitude;
                AddLocalPressure(directorEvent.WorldPosition, 0.7f * magnitude);
                break;
            case DirectorEventType.MagicUsed:
                AddGlobalPressure(0.22f * magnitude);
                searchIntensity += 0.45f * magnitude;
                AddLocalPressure(directorEvent.WorldPosition, 0.85f * magnitude);
                break;
            case DirectorEventType.ValuableLootPicked:
                greedScalar += 0.15f * magnitude;
                AddGlobalPressure(0.25f * magnitude);
                break;
            case DirectorEventType.TaskCompleted:
                eventPressure = Mathf.Max(0f, eventPressure - 0.25f * magnitude);
                break;
            case DirectorEventType.TaskFailed:
                AddGlobalPressure(0.55f * magnitude);
                searchIntensity += 0.4f * magnitude;
                break;
            case DirectorEventType.PlayerLingered:
                AddGlobalPressure(0.12f * magnitude);
                searchIntensity += 0.15f * magnitude;
                break;
        }

        Recalculate();
    }

    public void Tick(float deltaTime)
    {
        using ProfilerMarker.AutoScope scope = TickMarker.Auto();
        float safeDelta = Mathf.Max(0f, deltaTime);
        if (safeDelta <= 0f)
        {
            return;
        }

        float safehouseRelief = playerInSafehouse ? 0.75f : 0.25f;
        eventPressure = Mathf.Max(0f, eventPressure - safeDelta * safehouseRelief * 0.05f);
        searchIntensity = Mathf.Max(0f, searchIntensity - safeDelta * 0.08f);

        for (int i = localPressure.Count - 1; i >= 0; i--)
        {
            LocalPressurePoint point = localPressure[i];
            point.Pressure -= safeDelta * point.DecayPerSecond;
            if (point.Pressure <= 0f)
            {
                localPressure.RemoveAt(i);
            }
            else
            {
                localPressure[i] = point;
            }
        }

        Recalculate();
    }

    public float GetLocalPressure(Vector2 worldPosition)
    {
        float pressure = RawHostility;
        for (int i = 0; i < localPressure.Count; i++)
        {
            LocalPressurePoint point = localPressure[i];
            float distance = Vector2.Distance(worldPosition, point.Position);
            if (distance > point.Radius)
            {
                continue;
            }

            pressure += point.Pressure * (1f - distance / Mathf.Max(0.001f, point.Radius));
        }

        return Mathf.Max(0f, pressure);
    }

    public bool IsSpawnAllowed(Vector2 worldPosition)
    {
        if (playerInSafehouse)
        {
            return false;
        }

        return GetLocalPressure(worldPosition) >= 0.05f;
    }

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

    private bool Matches(StageConfig config)
    {
        if (stageConfig == config)
        {
            return true;
        }

        if (stageConfig == null || config == null)
        {
            return false;
        }

        return stageConfig.chapter == config.chapter && stageConfig.stage == config.stage && Mathf.Approximately(stageConfig.worldHostility, config.worldHostility);
    }

    private void AddGlobalPressure(float amount)
    {
        eventPressure = Mathf.Clamp(eventPressure + Mathf.Max(0f, amount), 0f, 8f);
    }

    private void AddLocalPressure(Vector2 position, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        if (localPressure.Count >= MaxLocalPressurePoints)
        {
            localPressure.RemoveAt(0);
        }

        localPressure.Add(new LocalPressurePoint(position, Mathf.Clamp(amount, 0f, 6f), LocalPressureRadius, 0.08f));
    }

    private void Recalculate()
    {
        float safehouseMultiplier = playerInSafehouse ? 0.45f : 1f;
        RawHostility = Mathf.Max(0f, BaseHostility + eventPressure * safehouseMultiplier + searchIntensity * 0.15f + (greedScalar - 1f) * 0.5f);
        HostilityLevel = Mathf.Max(0, Mathf.FloorToInt(RawHostility));
        HostilityIntensity = Mathf.Max(0f, RawHostility - HostilityLevel);
    }

    private static float ResolveBaseHostility(StageConfig config)
    {
        if (RunLevelManager.Instance != null)
        {
            return Mathf.Max(0f, RunLevelManager.Instance.CurrentWorldHostility);
        }

        return Mathf.Max(0f, config != null ? config.worldHostility : 0f);
    }

    private static float RarityScale(float value)
    {
        return Mathf.Clamp(value, 0.1f, 5f);
    }

    private struct LocalPressurePoint
    {
        public Vector2 Position;
        public float Pressure;
        public float Radius;
        public float DecayPerSecond;

        public LocalPressurePoint(Vector2 position, float pressure, float radius, float decayPerSecond)
        {
            Position = position;
            Pressure = pressure;
            Radius = radius;
            DecayPerSecond = decayPerSecond;
        }
    }
}
