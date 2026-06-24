using System;
using UnityEngine;

[Serializable]
public sealed class StageConfigList
{
    public StageConfig[] stages;
}

[Serializable]
public sealed class StageConfig
{
    public int chapter = 1;
    public int stage = 1;
    public string label = "1-1";
    public int mapWidth = 100;
    public int mapHeight = 80;
    public float cellSize = 1f;
    public int corridorHalfWidth = 2;
    public int branchCount = 18;
    public int roomCount = 12;
    public string roadSpriteResource = "Arts/Tiles/tile_ground_grid";
    public string dangerSpriteResource = "Arts/Tiles/tile_danger_floor";
    public string wallSpriteResource = "Arts/Tiles/tile_wall_block";
    public string safeRoomSpriteResource = "Arts/Tiles/tile_safe_room_floor";
    public string safeDoorSpriteResource = "Arts/Tiles/tile_safe_door";
    public int initialEnemySpawnCount = 6;
    public float enemySpawnMinDistanceFromPlayer = 12f;
    public int enemyBudget = 20;
    public float hostilitySpawnInterval = 3.5f;
    public int spawnDenCount = 0;
    public float worldHostility = 1f;
    public string enemySpawnWeights = "Ranged:2,Attacker:3,Sentinel:1,Shield:2";
}

public static class StageConfigDatabase
{
    private const string ResourcePath = "Configs/stage_config";
    private static StageConfigList configList;
    private static bool loaded;

    public static StageConfig Get(int chapter, int stage)
    {
        EnsureLoaded();
        if (configList != null && configList.stages != null)
        {
            for (int i = 0; i < configList.stages.Length; i++)
            {
                StageConfig config = configList.stages[i];
                if (config != null && config.chapter == chapter && config.stage == stage)
                {
                    return config;
                }
            }
        }

        return CreateFallback(chapter, stage);
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
            configList = new StageConfigList { stages = Array.Empty<StageConfig>() };
            return;
        }

        configList = JsonUtility.FromJson<StageConfigList>(asset.text);
        if (configList == null)
        {
            configList = new StageConfigList { stages = Array.Empty<StageConfig>() };
        }
    }

    private static StageConfig CreateFallback(int chapter, int stage)
    {
        return new StageConfig
        {
            chapter = chapter,
            stage = stage,
            label = $"{chapter}-{stage}",
            initialEnemySpawnCount = Mathf.Max(0, 6 + (chapter - 1) * 2 + (stage - 1)),
            enemyBudget = Mathf.Max(1, 20 + (chapter - 1) * 4 + (stage - 1) * 2),
            hostilitySpawnInterval = Mathf.Max(1.5f, 3.5f - (chapter - 1) * 0.25f - (stage - 1) * 0.15f)
        };
    }
}
