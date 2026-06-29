using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SemanticWorldValidationMenu
{
    private const string DefaultCatalogPath = "Assets/Resources/Configs/SemanticWorldVisualCatalog.asset";
    private const string DefaultReportPath = "Logs/semantic-world-validation-report.json";
    private const string DefaultSceneAuditReportPath = "Logs/semantic-world-scene-audit.json";
    private const float DefaultGenerationBudgetMilliseconds = 2000f;
    private static readonly int[] DefaultSeeds = { 3, 7, 11, 19, 23, 31, 43, 59, 71, 89 };

    [MenuItem("Tools/TopDownActRogue/Validate Semantic World Seeds")]
    public static void ValidateDefaultSeeds()
    {
        SemanticMapValidationBatchReport report = ValidateSeeds(DefaultSeeds);
        if (report.Passed)
        {
            Debug.Log($"Semantic world validation passed for {report.MapCount} seeds. Average generation: {report.AverageGenerationMilliseconds:0.00} ms. Slowest seed: {report.SlowestSeed} at {report.MaxGenerationMilliseconds:0.00} ms.");
            return;
        }

        Debug.LogError($"Semantic world validation failed for {report.MapCount} seeds. First failure: {report.Failures[0]}");
        for (int i = 0; i < report.Failures.Count; i++)
        {
            Debug.LogError(report.Failures[i]);
        }
    }

    public static SemanticMapValidationBatchReport ValidateSeeds(int[] seeds)
    {
        SemanticMapValidationBatchReport report = new SemanticMapValidationBatchReport();
        GameObject host = new GameObject("SemanticWorldValidationHost");
        try
        {
            GridRouteMapGenerator generator = host.AddComponent<GridRouteMapGenerator>();
            int[] safeSeeds = seeds != null && seeds.Length > 0 ? seeds : DefaultSeeds;
            for (int i = 0; i < safeSeeds.Length; i++)
            {
                generator.GenerateMap(safeSeeds[i]);
                SemanticWorldMetrics metrics = SemanticWorldTelemetry.Capture(
                    generator.CurrentMapData,
                    CombatAftermathSystem.Existing != null ? CombatAftermathSystem.Existing.Grid : null,
                    WorldHostilityDirector.Current,
                    EnemyRegistry.LivingCount,
                    host.GetComponent<SemanticWorldView>());
                SemanticWorldBudgets budgets = SemanticWorldBudgets.FromStage(generator.CurrentStageConfig, SemanticWorldBudgets.Default);
                SemanticWorldBudgetReport budgetReport = SemanticWorldBudgetChecker.Check(metrics, budgets);
                report.AddMapResult(safeSeeds[i], generator.LastValidationReport, generator.LastGenerationMilliseconds, budgetReport, DefaultGenerationBudgetMilliseconds);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            DestroyIfPresent("WorldHostilityDirectorRunner");
            DestroyIfPresent("CombatAftermathSystem");
        }

        return report;
    }

    public static void BatchCreateCatalogAndValidate()
    {
        CreateDefaultVisualCatalog();
        SemanticWorldSceneAudit sceneAudit = RunSceneAudit();
        WriteSceneAudit(sceneAudit, DefaultSceneAuditReportPath);
        SemanticMapValidationBatchReport report = ValidateSeeds(DefaultSeeds);
        WriteReport(report, DefaultReportPath);
        if (!sceneAudit.passed || !report.Passed)
        {
            Debug.LogError($"Semantic world batch validation failed. Reports: {DefaultSceneAuditReportPath}, {DefaultReportPath}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"Semantic world batch validation passed. Reports: {DefaultSceneAuditReportPath}, {DefaultReportPath}");
        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/TopDownActRogue/Audit Current Semantic World Scene")]
    public static void AuditCurrentScene()
    {
        SemanticWorldSceneAudit audit = RunSceneAudit();
        string prefix = audit.passed ? "Semantic scene audit passed" : "Semantic scene audit found issues";
        string message =
            $"{prefix}: scene={audit.scenePath}, generators={audit.mapGeneratorCount}, runManagers={audit.runLevelManagerCount}, " +
            $"worldViews={audit.worldViewCount}, overlays={audit.debugOverlayCount}, tilemapRenderers={audit.tilemapRendererCount}, catalogAsset={audit.defaultCatalogExists}";

        if (audit.passed)
        {
            Debug.Log(message);
            return;
        }

        Debug.LogWarning(message);
        for (int i = 0; i < audit.issues.Length; i++)
        {
            Debug.LogWarning(audit.issues[i]);
        }
    }

    public static SemanticWorldSceneAudit RunSceneAudit()
    {
        System.Collections.Generic.List<string> issues = new System.Collections.Generic.List<string>();
        GridRouteMapGenerator[] generators = UnityEngine.Object.FindObjectsOfType<GridRouteMapGenerator>(true);
        RunLevelManager[] runManagers = UnityEngine.Object.FindObjectsOfType<RunLevelManager>(true);
        SemanticWorldView[] worldViews = UnityEngine.Object.FindObjectsOfType<SemanticWorldView>(true);
        SemanticWorldDebugOverlay[] overlays = UnityEngine.Object.FindObjectsOfType<SemanticWorldDebugOverlay>(true);
        SemanticTilemapRenderer[] tilemapRenderers = UnityEngine.Object.FindObjectsOfType<SemanticTilemapRenderer>(true);
        PlayerVisionMask[] visionMasks = UnityEngine.Object.FindObjectsOfType<PlayerVisionMask>(true);
        SemanticWorldVisualCatalog defaultCatalog = AssetDatabase.LoadAssetAtPath<SemanticWorldVisualCatalog>(DefaultCatalogPath);
        bool defaultCatalogExists = defaultCatalog != null;

        if (generators.Length == 0)
        {
            issues.Add("Current scene has no GridRouteMapGenerator.");
        }

        if (runManagers.Length == 0)
        {
            issues.Add("Current scene has no RunLevelManager.");
        }

        if (visionMasks.Length == 0)
        {
            issues.Add("Current scene has no PlayerVisionMask.");
        }

        if (!defaultCatalogExists)
        {
            issues.Add($"Default semantic visual catalog is missing: {DefaultCatalogPath}");
        }
        else
        {
            defaultCatalog.ValidateRequiredBindings(issues);
        }

        return new SemanticWorldSceneAudit
        {
            scenePath = EditorSceneManager.GetActiveScene().path,
            passed = issues.Count == 0,
            mapGeneratorCount = generators.Length,
            runLevelManagerCount = runManagers.Length,
            worldViewCount = worldViews.Length,
            debugOverlayCount = overlays.Length,
            tilemapRendererCount = tilemapRenderers.Length,
            playerVisionMaskCount = visionMasks.Length,
            defaultCatalogExists = defaultCatalogExists,
            issues = issues.ToArray()
        };
    }

    [MenuItem("Tools/TopDownActRogue/Create Default Semantic Visual Catalog")]
    public static void CreateDefaultVisualCatalog()
    {
        SemanticWorldVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<SemanticWorldVisualCatalog>(DefaultCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SemanticWorldVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
        }

        catalog.Configure(
            new[]
            {
                new BuildingVisualBinding
                {
                    Kind = BuildingKind.Safehouse,
                    Sprite = LoadSprite("Assets/Resources/Arts/SafeHouse/safehouse_floor_stone.png"),
                    Scale = 1.25f
                },
                new BuildingVisualBinding
                {
                    Kind = BuildingKind.ExtractionRoom,
                    Sprite = LoadSprite("Assets/Resources/Arts/SafeHouse/safehouse_exit_door.png"),
                    Scale = 1.25f,
                    SortingOrderOffset = 1
                },
                new BuildingVisualBinding
                {
                    Kind = BuildingKind.SpawnDen,
                    Sprite = LoadSprite("Assets/Resources/Arts/SimpleSprites/spawn_point_purple_simple.png"),
                    Scale = 1.1f,
                    SortingOrderOffset = 2
                },
                new BuildingVisualBinding
                {
                    Kind = BuildingKind.SupplyCache,
                    Sprite = LoadSprite("Assets/Resources/Arts/SafeHouse/safehouse_crates.png"),
                    Scale = 0.9f,
                    SortingOrderOffset = 1
                },
                new BuildingVisualBinding
                {
                    Kind = BuildingKind.Shrine,
                    Sprite = LoadSprite("Assets/Resources/Arts/SafeHouse/safehouse_wish_statue.png"),
                    Scale = 1.1f,
                    SortingOrderOffset = 1
                },
                new BuildingVisualBinding
                {
                    Kind = BuildingKind.WatchPost,
                    Sprite = LoadSprite("Assets/Resources/Arts/SafeHouse/safehouse_task_board.png"),
                    Scale = 1f
                }
            },
            new[]
            {
                new ResourceVisualBinding
                {
                    Type = ResourceType.Currency,
                    Sprite = LoadSprite("Assets/Resources/Arts/Pickups/pickup_coin_hex.png"),
                    Scale = 0.55f
                },
                new ResourceVisualBinding
                {
                    Type = ResourceType.Material,
                    Sprite = LoadSprite("Assets/Resources/Arts/Pickups/pickup_energy_shard.png"),
                    Scale = 0.55f
                },
                new ResourceVisualBinding
                {
                    Type = ResourceType.RareCore,
                    Sprite = LoadSprite("Assets/Resources/Arts/Pickups/pickup_energy_shard.png"),
                    Scale = 0.72f,
                    SortingOrderOffset = 1
                },
                new ResourceVisualBinding
                {
                    Type = ResourceType.Medical,
                    Sprite = LoadSprite("Assets/Resources/Arts/SafeHouse/safehouse_shrine_floor.png"),
                    Scale = 0.65f
                }
            },
            new[]
            {
                new ResidueVisualBinding
                {
                    Type = CombatResidueType.BloodDrop,
                    Sprite = LoadSprite("Assets/Resources/Arts/VFX/attack_hit_circle.png"),
                    Scale = 0.7f
                },
                new ResidueVisualBinding
                {
                    Type = CombatResidueType.BloodPool,
                    Sprite = LoadSprite("Assets/Resources/Arts/VFX/attack_hit_circle.png"),
                    Scale = 1.1f
                },
                new ResidueVisualBinding
                {
                    Type = CombatResidueType.Scorch,
                    Sprite = LoadSprite("Assets/Resources/Arts/VFX/danger_telegraph.png"),
                    Scale = 0.85f
                },
                new ResidueVisualBinding
                {
                    Type = CombatResidueType.Corpse,
                    Sprite = LoadSprite("Assets/Resources/Arts/SimpleSprites/enemy_warrior_simple.png"),
                    Scale = 0.9f
                },
                new ResidueVisualBinding
                {
                    Type = CombatResidueType.Debris,
                    Sprite = LoadSprite("Assets/Resources/Arts/Tiles/tile_wall_block.png"),
                    Scale = 0.6f
                },
                new ResidueVisualBinding
                {
                    Type = CombatResidueType.GrassCluster,
                    Sprite = LoadSprite("Assets/Resources/Arts/Tiles/tile_danger_floor.png"),
                    Scale = 0.65f
                }
            },
            LoadSprite("Assets/Resources/Arts/Tiles/tile_danger_floor.png"),
            LoadSprite("Assets/Resources/Arts/Tiles/tile_ground_grid.png"),
            LoadSprite("Assets/Resources/Arts/Tiles/tile_wall_block.png"),
            LoadSprite("Assets/Resources/Arts/Tiles/tile_safe_room_floor.png"));

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = catalog;
        Debug.Log($"Semantic visual catalog created or refreshed at {DefaultCatalogPath}.");
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            Debug.LogWarning($"Missing semantic visual sprite: {assetPath}");
        }

        return sprite;
    }

    private static void WriteReport(SemanticMapValidationBatchReport report, string path)
    {
        SemanticWorldValidationJson json = SemanticWorldValidationJson.FromReport(report);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonUtility.ToJson(json, true));
    }

    private static void WriteSceneAudit(SemanticWorldSceneAudit audit, string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonUtility.ToJson(audit, true));
    }

    private static void DestroyIfPresent(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null)
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    [Serializable]
    private sealed class SemanticWorldValidationJson
    {
        public bool passed;
        public int mapCount;
        public float averageGenerationMilliseconds;
        public float maxGenerationMilliseconds;
        public int slowestSeed;
        public string[] failures;

        public static SemanticWorldValidationJson FromReport(SemanticMapValidationBatchReport report)
        {
            if (report == null)
            {
                return new SemanticWorldValidationJson
                {
                    passed = false,
                    failures = new[] { "missing validation report" }
                };
            }

            string[] copiedFailures = new string[report.Failures.Count];
            for (int i = 0; i < report.Failures.Count; i++)
            {
                copiedFailures[i] = report.Failures[i];
            }

            return new SemanticWorldValidationJson
            {
                passed = report.Passed,
                mapCount = report.MapCount,
                averageGenerationMilliseconds = report.AverageGenerationMilliseconds,
                maxGenerationMilliseconds = report.MaxGenerationMilliseconds,
                slowestSeed = report.SlowestSeed,
                failures = copiedFailures
            };
        }
    }

    [Serializable]
    public sealed class SemanticWorldSceneAudit
    {
        public string scenePath;
        public bool passed;
        public int mapGeneratorCount;
        public int runLevelManagerCount;
        public int worldViewCount;
        public int debugOverlayCount;
        public int tilemapRendererCount;
        public int playerVisionMaskCount;
        public bool defaultCatalogExists;
        public string[] issues;
    }
}
