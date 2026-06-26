using System.Collections.Generic;
using UnityEngine;

public enum SpawnDenState
{
    Active,
    ReviveDelay,
    Reviving,
    Destroyed
}

public sealed class SpawnDenModel
{
    public readonly List<GameObject> livingEnemies = new List<GameObject>();
    public readonly List<SpawnDenWeightedArchetype> weightedArchetypes = new List<SpawnDenWeightedArchetype>();
    public readonly List<SpawnDenTimedBurst> timedBursts = new List<SpawnDenTimedBurst>();
    public readonly HashSet<int> triggeredPhaseIndices = new HashSet<int>();

    public SpawnDenState state = SpawnDenState.Active;
    public float spawnTimer;
    public float intervalMultiplier = 1f;
    public float reviveDelayRemaining;
    public float reviveTargetHealth;
    public int spawnIndex;

    public void Reset()
    {
        state = SpawnDenState.Active;
        spawnTimer = 0f;
        intervalMultiplier = 1f;
        reviveDelayRemaining = 0f;
        reviveTargetHealth = 0f;
        spawnIndex = 0;
        livingEnemies.Clear();
        weightedArchetypes.Clear();
        timedBursts.Clear();
        triggeredPhaseIndices.Clear();
    }

    public void PruneLivingEnemies()
    {
        livingEnemies.RemoveAll(enemy => enemy == null || !enemy.activeInHierarchy);
    }
}

public readonly struct SpawnDenWeightedArchetype
{
    public readonly EnemyArchetype archetype;
    public readonly float weight;

    public SpawnDenWeightedArchetype(EnemyArchetype archetype, float weight)
    {
        this.archetype = archetype;
        this.weight = weight;
    }
}

public struct SpawnDenTimedBurst
{
    public readonly string enemyArchetypes;
    public readonly float intervalSeconds;
    public int remainingCount;
    public float remainingSeconds;
    public float spawnTimer;

    public SpawnDenTimedBurst(string enemyArchetypes, int count, float duration, float fallbackInterval)
    {
        this.enemyArchetypes = enemyArchetypes;
        remainingCount = Mathf.Max(1, count);
        intervalSeconds = duration > 0f
            ? Mathf.Max(0.01f, duration / remainingCount)
            : Mathf.Max(0.01f, fallbackInterval);
        remainingSeconds = duration > 0f
            ? Mathf.Max(0.01f, duration)
            : intervalSeconds * remainingCount;
        spawnTimer = 0f;
    }
}
