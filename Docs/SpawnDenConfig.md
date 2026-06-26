# Spawn Den Config

Enemy spawning is driven by spawn dens. Runtime enemies must only be created from a spawn den position. If a spawn check fails, skip that spawn tick; do not fall back to a random road cell or a position near the player.

## Config Split

`stage_config.json` only describes stage placement strategy:

- `spawnDenCount`: how many spawn dens this stage tries to place.
- `spawnDenPoolId`: which pool to draw spawn den configs from.
- `allowDuplicateSpawnDens`: whether the same spawn den config can be picked more than once.
- `spawnDenWeightMultiplier`: stage-wide multiplier for spawn den selection weights.
- `spawnDenLevelBias`: optional stage bias toward higher-level spawn den configs.
- `worldHostility`: stage hostility value used by `WorldHostilityDirector`.

`spawn_den_config.json` owns spawn den behavior:

- `spawnDenPools`: named pools of spawn den configs.
- `entries`: spawn den configs in that pool.
- `id`, `level`, `minWorldHostility`, `maxHealth`, `baseSpawnInterval`, `enemyArchetypes`, `spawnLogic`, `phases`, and `destroyLogic`.
- `baseWeight`, `weightPerHostilityAboveMin`, and `weightCurve` for selection probability.

## Probability

Only entries with `minWorldHostility <= worldHostility` can be selected.

```csharp
eligible = pool.entries
    .Where(entry => entry.minWorldHostility <= worldHostility)
    .Select(entry => (entry, weight: CalculateWeight(entry, worldHostility)))
    .Where(candidate => candidate.weight > 0)
    .ToList();

for i in 0..spawnDenCount:
    if eligible empty:
        break;

    picked = WeightedRandom(eligible);
    result.Add(picked.entry);

    if !allowDuplicateSpawnDens:
        eligible.Remove(picked);
```

```csharp
overMin = max(0, worldHostility - entry.minWorldHostility);
bonus = overMin * entry.weightPerHostilityAboveMin;

if entry.weightCurve == "steep":
    bonus *= 1 + overMin * 0.35;

levelMultiplier = max(0.05, 1 + (entry.level - 1) * spawnDenLevelBias);
effectiveWeight = max(0, (entry.baseWeight + bonus) * spawnDenWeightMultiplier * levelMultiplier);
```

If eligible entries are fewer than `spawnDenCount` and duplicates are disabled, the stage places fewer dens. Code must not invent a default spawn den.

## Spawn Logic

`spawnLogic.activationRange > 0` means the player must be within that range for the den to spawn. `activationRange <= 0` means the den can spawn continuously.

`spawnLogic.accelerationRange > 0` applies `acceleratedIntervalMultiplier` while the player is within that range. Use values below `1` to speed up spawning.

`spawnLogic.maxAliveEnemies` limits enemies owned by this den. The den also respects the global stage enemy budget through `EnemyRegistry`.

Before every spawn, the den checks that it is active, hostility is high enough, the player is in range when required, local and global enemy caps are not exceeded, and a free spawn point near the den exists. Failed checks skip the spawn.

## Phases

`phases` run once when den health drops to or below `healthPercent`.

Supported actions:

- `intervalMultiplier`: multiplies future spawn intervals by `intervalMultiplier`.
- `summonEnemies`: queues `count` spawn attempts from this den.
- `burstSpawn`: same location rule as `summonEnemies`; each enemy still runs all pre-spawn checks.

`summonEnemies` and `burstSpawn` can override enemy weights with their own `enemyArchetypes`.

## Destroy And Revive

`destroyLogic.type = "destroyOnDeath"` permanently destroys the den when health reaches zero.

`destroyLogic.type = "reviveAfterDelayInterruptible"` uses this flow:

1. Health reaches zero.
2. The den stops spawning and waits `reviveDelaySeconds`.
3. After the delay, it attempts to revive and heals by `reviveHealPerSecond`.
4. When health reaches `reviveHealthPercent * maxHealth`, the den becomes active and can spawn again.
5. If the den reaches zero again during the waiting or healing period, it is permanently destroyed.
