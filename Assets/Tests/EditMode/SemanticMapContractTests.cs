using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SemanticMapContractTests
{
    [Test]
    public void SemanticMapMarksWalkableWallsAndSafeEndpoints()
    {
        bool[,] walkable = CreateEmptyWalkable(8, 5);
        for (int x = 1; x <= 6; x++)
        {
            walkable[x, 2] = true;
        }

        Vector2Int start = new Vector2Int(1, 2);
        Vector2Int end = new Vector2Int(6, 2);
        Vector2Int[] route = { start, new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(5, 2), end };

        MapData map = SemanticMapBuilder.BuildFromRoute(8, 5, 1f, walkable, route, start, end, null, 1.25f, 123);

        Assert.AreEqual(8, map.Width);
        Assert.AreEqual(5, map.Height);
        Assert.AreEqual(123, map.Seed);
        Assert.AreEqual(start, map.StartCell);
        Assert.AreEqual(end, map.EndCell);
        Assert.AreEqual(route.Length, map.MainRoute.Count);

        MapCell startCell = map.GetCell(start);
        Assert.AreEqual(TileType.SafeFloor, startCell.TileType);
        Assert.AreEqual(RegionKind.StartSafehouse, startCell.RegionKind);
        Assert.IsTrue(startCell.Flags.HasFlag(CellFlags.Walkable));
        Assert.IsTrue(startCell.Flags.HasFlag(CellFlags.SafeZone));
        Assert.IsTrue(startCell.Flags.HasFlag(CellFlags.DirectorSpawnDeny));

        MapCell endCell = map.GetCell(end);
        Assert.AreEqual(RegionKind.EndSafehouse, endCell.RegionKind);
        Assert.IsTrue(endCell.Flags.HasFlag(CellFlags.ExtractionZone));

        MapCell wallCell = map.GetCell(new Vector2Int(0, 0));
        Assert.AreEqual(TileType.Wall, wallCell.TileType);
        Assert.IsTrue(wallCell.Flags.HasFlag(CellFlags.BlocksMovement));
        Assert.IsTrue(wallCell.Flags.HasFlag(CellFlags.BlocksVision));
        Assert.IsFalse(wallCell.Flags.HasFlag(CellFlags.Walkable));
    }

    [Test]
    public void SemanticMapPreservesSpawnAndResourceCandidates()
    {
        bool[,] walkable = CreateEmptyWalkable(9, 7);
        for (int x = 1; x <= 7; x++)
        {
            walkable[x, 3] = true;
        }

        walkable[4, 4] = true;
        walkable[4, 5] = true;
        Vector2Int start = new Vector2Int(1, 3);
        Vector2Int end = new Vector2Int(7, 3);
        Vector2Int spawn = new Vector2Int(4, 4);
        Vector2Int deadEnd = new Vector2Int(4, 5);
        Vector2Int[] route = { start, new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3), end };

        MapData map = SemanticMapBuilder.BuildFromRoute(
            9,
            7,
            1f,
            walkable,
            route,
            start,
            end,
            new[] { spawn, spawn },
            2f);

        Assert.AreEqual(1, map.SpawnSockets.Count);
        Assert.AreEqual(spawn, map.SpawnSockets[0]);
        Assert.IsTrue(map.IsSpawnCandidate(spawn));

        MapCell spawnCell = map.GetCell(spawn);
        Assert.AreEqual(RegionKind.DenTerritory, spawnCell.RegionKind);
        Assert.IsTrue(spawnCell.Flags.HasFlag(CellFlags.SpawnCandidate));
        Assert.Greater(spawnCell.Threat, 0f);

        MapCell deadEndCell = map.GetCell(deadEnd);
        Assert.AreEqual(RegionKind.DeadEndLoot, deadEndCell.RegionKind);
        Assert.IsTrue(deadEndCell.Flags.HasFlag(CellFlags.ResourceCandidate));
        Assert.Contains(deadEnd, map.ResourceSockets);
    }

    [Test]
    public void PlacementSolverCreatesFunctionalLayerWithoutBlockingMainRoute()
    {
        bool[,] walkable = CreateEmptyWalkable(12, 10);
        for (int x = 1; x <= 10; x++)
        {
            walkable[x, 4] = true;
        }

        for (int x = 5; x <= 7; x++)
        {
            for (int y = 5; y <= 7; y++)
            {
                walkable[x, y] = true;
            }
        }

        walkable[3, 5] = true;
        walkable[3, 6] = true;
        walkable[3, 7] = true;

        Vector2Int start = new Vector2Int(1, 4);
        Vector2Int end = new Vector2Int(10, 4);
        Vector2Int spawn = new Vector2Int(6, 6);
        Vector2Int resource = new Vector2Int(3, 7);
        Vector2Int[] route =
        {
            start,
            new Vector2Int(2, 4),
            new Vector2Int(3, 4),
            new Vector2Int(4, 4),
            new Vector2Int(5, 4),
            new Vector2Int(6, 4),
            new Vector2Int(7, 4),
            new Vector2Int(8, 4),
            new Vector2Int(9, 4),
            end
        };

        MapData map = SemanticMapBuilder.BuildFromRoute(12, 10, 1f, walkable, route, start, end, new[] { spawn }, 1f);
        SemanticPlacementSolver.PopulateDefaultPlacements(map, new SemanticPlacementSettings
        {
            MaxSpawnDenBuildings = 1,
            MaxSupplyCaches = 1,
            MaxResourceNodes = 2,
            MinBuildingSpacing = 2,
            SafehouseFootprintRadius = 0
        });

        Assert.AreEqual(1, CountBuildings(map, BuildingKind.Safehouse));
        Assert.AreEqual(1, CountBuildings(map, BuildingKind.ExtractionRoom));
        Assert.AreEqual(1, CountBuildings(map, BuildingKind.SpawnDen));
        Assert.GreaterOrEqual(map.ResourceNodes.Count, 1);
        Assert.AreEqual(resource, map.ResourceNodes[map.ResourceNodes.Count - 1].Cell);

        for (int i = 0; i < map.Buildings.Count; i++)
        {
            BuildingInstance building = map.Buildings[i];
            if (building.Kind == BuildingKind.Safehouse || building.Kind == BuildingKind.ExtractionRoom)
            {
                continue;
            }

            Assert.IsFalse(FootprintTouchesRoute(map, building.Footprint), building.Kind.ToString());
        }

        SemanticMapValidationReport report = SemanticMapValidator.Validate(map);
        Assert.IsTrue(report.Passed, report.Failures.Count > 0 ? report.Failures[0] : string.Empty);
    }

    [Test]
    public void SemanticMapValidatorReportsBrokenMainRoute()
    {
        bool[,] walkable = CreateEmptyWalkable(7, 5);
        walkable[1, 2] = true;
        walkable[2, 2] = true;
        walkable[5, 2] = true;

        Vector2Int start = new Vector2Int(1, 2);
        Vector2Int end = new Vector2Int(5, 2);
        Vector2Int[] route = { start, new Vector2Int(2, 2), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(7, 5, 1f, walkable, route, start, end, null, 1f);

        SemanticMapValidationReport report = SemanticMapValidator.Validate(map);

        Assert.IsFalse(report.Passed);
        Assert.Greater(report.Failures.Count, 0);
    }

    [Test]
    public void GridRouteMapGeneratorUsesSeedForDeterministicSemanticMaps()
    {
        GameObject firstObject = new GameObject("seeded-generator-a");
        GameObject secondObject = new GameObject("seeded-generator-b");
        try
        {
            GridRouteMapGenerator first = firstObject.AddComponent<GridRouteMapGenerator>();
            GridRouteMapGenerator second = secondObject.AddComponent<GridRouteMapGenerator>();
            ConfigureGeneratorForDeterminismTest(first);
            ConfigureGeneratorForDeterminismTest(second);

            first.GenerateMap(431);
            second.GenerateMap(431);

            Assert.AreEqual(431, first.CurrentMapData.Seed);
            Assert.AreEqual(Fingerprint(first.CurrentMapData), Fingerprint(second.CurrentMapData));
            Assert.IsTrue(SemanticMapValidator.Validate(first.CurrentMapData).Passed);
            Assert.IsNotNull(first.LastValidationReport);
            Assert.IsTrue(first.LastValidationReport.Passed, first.LastValidationReport.Failures.Count > 0 ? first.LastValidationReport.Failures[0] : string.Empty);
            Assert.GreaterOrEqual(first.LastGenerationMilliseconds, 0f);
        }
        finally
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
            DestroyGeneratedRuntimeObject("WorldHostilityDirectorRunner");
            DestroyGeneratedRuntimeObject("CombatAftermathSystem");
        }
    }

    [Test]
    public void SeededGridRouteGenerationRestoresUnityRandomState()
    {
        Random.InitState(991);
        float expectedNextValue = Random.value;
        Random.InitState(991);

        GameObject generatorObject = new GameObject("seeded-random-state-generator");
        try
        {
            GridRouteMapGenerator generator = generatorObject.AddComponent<GridRouteMapGenerator>();
            ConfigureGeneratorForDeterminismTest(generator);
            generator.GenerateMap(77);

            Assert.AreEqual(expectedNextValue, Random.value);
        }
        finally
        {
            Object.DestroyImmediate(generatorObject);
            DestroyGeneratedRuntimeObject("WorldHostilityDirectorRunner");
            DestroyGeneratedRuntimeObject("CombatAftermathSystem");
        }
    }

    [Test]
    public void SeededGridRouteGenerationPassesBatchValidation()
    {
        GameObject generatorObject = new GameObject("seeded-batch-validation-generator");
        try
        {
            GridRouteMapGenerator generator = generatorObject.AddComponent<GridRouteMapGenerator>();
            ConfigureGeneratorForDeterminismTest(generator);
            SemanticMapValidationBatchReport batchReport = new SemanticMapValidationBatchReport();
            int[] seeds = { 3, 7, 11, 19, 23 };

            for (int i = 0; i < seeds.Length; i++)
            {
                generator.GenerateMap(seeds[i]);
                batchReport.AddMapResult(seeds[i], generator.LastValidationReport, generator.LastGenerationMilliseconds);
            }

            Assert.AreEqual(seeds.Length, batchReport.MapCount);
            Assert.IsTrue(batchReport.Passed, batchReport.Failures.Count > 0 ? batchReport.Failures[0] : string.Empty);
            Assert.GreaterOrEqual(batchReport.AverageGenerationMilliseconds, 0f);
        }
        finally
        {
            Object.DestroyImmediate(generatorObject);
            DestroyGeneratedRuntimeObject("WorldHostilityDirectorRunner");
            DestroyGeneratedRuntimeObject("CombatAftermathSystem");
        }
    }

    [Test]
    public void BatchValidationReportCapturesGenerationAndBudgetFailures()
    {
        SemanticMapValidationBatchReport batchReport = new SemanticMapValidationBatchReport();
        SemanticMapValidationReport validMapReport = new SemanticMapValidationReport();
        SemanticWorldBudgetReport budgetReport = new SemanticWorldBudgetReport(false, "building budget exceeded: 9/8");

        batchReport.AddMapResult(17, validMapReport, 25f, budgetReport, 10f);

        Assert.AreEqual(1, batchReport.MapCount);
        Assert.AreEqual(25f, batchReport.MaxGenerationMilliseconds);
        Assert.AreEqual(17, batchReport.SlowestSeed);
        Assert.IsFalse(batchReport.Passed);
        Assert.IsTrue(batchReport.Failures[0].Contains("generation budget exceeded"));
        Assert.IsTrue(batchReport.Failures[1].Contains("building budget exceeded"));
    }

    [Test]
    public void AftermathGridRecordsPhysicalMagicAndKills()
    {
        bool[,] walkable = CreateEmptyWalkable(14, 6);
        for (int x = 1; x <= 12; x++)
        {
            walkable[x, 3] = true;
        }

        Vector2Int start = new Vector2Int(1, 3);
        Vector2Int end = new Vector2Int(12, 3);
        Vector2Int hitCell = new Vector2Int(6, 3);
        Vector2Int magicCell = new Vector2Int(7, 3);
        Vector2Int[] route =
        {
            start,
            new Vector2Int(2, 3),
            new Vector2Int(3, 3),
            new Vector2Int(4, 3),
            new Vector2Int(5, 3),
            hitCell,
            magicCell,
            new Vector2Int(8, 3),
            new Vector2Int(9, 3),
            new Vector2Int(10, 3),
            new Vector2Int(11, 3),
            end
        };
        MapData map = SemanticMapBuilder.BuildFromRoute(14, 6, 1f, walkable, route, start, end, null, 1f);
        AftermathGrid grid = new AftermathGrid(map, 8);

        DamageContext physicalHit = CreateDamageContext(CellToWorld(map, hitCell), 12f, 0f);
        grid.AddCombatHit(physicalHit, new HitResult(true, true, true, false, 20f, physicalHit.feedback), 1f);

        Assert.Greater(grid.GetBlood(hitCell), 0f);
        Assert.IsTrue(grid.HasCorpse(hitCell));
        Assert.AreEqual(2, grid.Residues.Count);
        Assert.AreEqual(CombatResidueType.BloodDrop, grid.Residues[0].Type);
        Assert.AreEqual(CombatResidueType.Corpse, grid.Residues[1].Type);

        DamageContext magicHit = CreateDamageContext(CellToWorld(map, magicCell), 0f, 15f);
        grid.AddCombatHit(magicHit, new HitResult(true, true, false, false, 15f, magicHit.feedback), 2f);

        Assert.Greater(grid.GetScorch(magicCell), 0f);
        Assert.AreEqual(CombatResidueType.Scorch, grid.Residues[2].Type);
    }

    [Test]
    public void AftermathGridSkipsSafeZoneResidues()
    {
        bool[,] walkable = CreateEmptyWalkable(6, 5);
        for (int x = 1; x <= 4; x++)
        {
            walkable[x, 2] = true;
        }

        Vector2Int start = new Vector2Int(1, 2);
        Vector2Int end = new Vector2Int(4, 2);
        Vector2Int[] route = { start, new Vector2Int(2, 2), new Vector2Int(3, 2), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(6, 5, 1f, walkable, route, start, end, null, 1f);
        AftermathGrid grid = new AftermathGrid(map);

        DamageContext safeHit = CreateDamageContext(CellToWorld(map, start), 12f, 0f);
        grid.AddCombatHit(safeHit, new HitResult(true, true, true, false, 12f, safeHit.feedback), 1f);

        Assert.AreEqual(0f, grid.GetBlood(start));
        Assert.IsFalse(grid.HasCorpse(start));
        Assert.AreEqual(0, grid.Residues.Count);
    }

    [Test]
    public void WorldHostilityDirectorRespondsToEventsAndSafehouseState()
    {
        StageConfig stage = new StageConfig
        {
            chapter = 9,
            stage = 1,
            worldHostility = 1f
        };
        WorldHostilityDirector director = new WorldHostilityDirector(stage);
        Vector2 eventPosition = new Vector2(4f, 2f);

        Assert.AreEqual(1f, director.RawHostility, 0.001f);
        Assert.IsTrue(director.IsSpawnAllowed(eventPosition));

        director.Notify(new DirectorEvent(DirectorEventType.SentinelAlarm, eventPosition, 1f));

        Assert.Greater(director.RawHostility, 1f);
        Assert.Greater(director.GetLocalPressure(eventPosition), director.RawHostility);
        Assert.Greater(director.Snapshot.SearchIntensity, 0f);

        director.Notify(new DirectorEvent(DirectorEventType.PlayerEnteredSafehouse, Vector2.zero, 1f));

        Assert.IsTrue(director.Snapshot.PlayerInSafehouse);
        Assert.IsFalse(director.IsSpawnAllowed(eventPosition));

        director.Notify(new DirectorEvent(DirectorEventType.PlayerExitedSafehouse, Vector2.zero, 1f));
        Assert.IsTrue(director.IsSpawnAllowed(eventPosition));
    }

    [Test]
    public void WorldHostilityDirectorLocalPressureDecays()
    {
        WorldHostilityDirector director = new WorldHostilityDirector(new StageConfig { worldHostility = 0.5f });
        Vector2 eventPosition = new Vector2(2f, 3f);
        director.Notify(new DirectorEvent(DirectorEventType.LoudAttack, eventPosition, 2f));
        float initialPressure = director.GetLocalPressure(eventPosition);

        director.Tick(20f);

        Assert.Less(director.GetLocalPressure(eventPosition), initialPressure);
    }

    [Test]
    public void DirectorSignalBridgeConvertsTaskAndLootSignals()
    {
        WorldHostilityDirector.ResetRuntime(new StageConfig { worldHostility = 1f });
        WorldHostilityDirector director = WorldHostilityDirector.Current;
        float initialHostility = director.RawHostility;
        TaskConfigDatabase.TaskConfig task = new TaskConfigDatabase.TaskConfig
        {
            id = "test_task",
            rewards = new[] { new TaskConfigDatabase.TaskReward { type = "material", id = "material_iron_shard", amount = 2 } },
            failurePenalties = new[] { new TaskConfigDatabase.TaskPenalty { type = "worldHostility", value = 0.5f } }
        };

        DirectorSignalBridge.NotifyLootAccepted("material", "material_arcane_splinter", "rare", 1, Vector2.one);
        Assert.Greater(director.Snapshot.GreedScalar, 1f);

        DirectorSignalBridge.NotifyTaskFailed(task, Vector2.one);
        Assert.Greater(director.RawHostility, initialHostility);

        float afterFailure = director.RawHostility;
        DirectorSignalBridge.NotifyTaskCompleted(task, Vector2.one);
        Assert.Less(director.RawHostility, afterFailure);

        WorldHostilityDirector.ResetRuntime(null);
    }

    [Test]
    public void TaskRunStateCompletesKillTaskAndNotifiesDirector()
    {
        WorldHostilityDirector.ResetRuntime(new StageConfig { worldHostility = 1f });
        GameObject taskObject = new GameObject("task-run-state-kill-test");
        try
        {
            TaskRunState taskState = taskObject.AddComponent<TaskRunState>();
            TaskConfigDatabase.TaskConfig task = new TaskConfigDatabase.TaskConfig
            {
                id = "kill_attacker_test",
                completionConditions = new[]
                {
                    new TaskConfigDatabase.TaskCondition
                    {
                        type = "killEnemy",
                        enemyArchetype = "Attacker",
                        count = 2
                    }
                },
                rewards = new[] { new TaskConfigDatabase.TaskReward { type = "currency", id = "coin", amount = 10 } }
            };

            WorldHostilityDirector.Current.Notify(new DirectorEvent(DirectorEventType.LoudAttack, Vector2.one, 2f));
            float beforeCompletion = WorldHostilityDirector.Current.RawHostility;

            Assert.IsTrue(taskState.AcceptTask(task));
            taskState.NotifyEnemyKilled(EnemyArchetype.Attacker, Vector2.one);
            Assert.IsFalse(taskState.IsCompleted);
            taskState.NotifyEnemyKilled(EnemyArchetype.Attacker, Vector2.one);

            Assert.IsTrue(taskState.IsCompleted);
            Assert.Less(WorldHostilityDirector.Current.RawHostility, beforeCompletion);
        }
        finally
        {
            Object.DestroyImmediate(taskObject);
            WorldHostilityDirector.ResetRuntime(null);
        }
    }

    [Test]
    public void TaskRunStateFailsStageEndTaskAndNotifiesDirector()
    {
        WorldHostilityDirector.ResetRuntime(new StageConfig { worldHostility = 1f });
        GameObject taskObject = new GameObject("task-run-state-failure-test");
        try
        {
            TaskRunState taskState = taskObject.AddComponent<TaskRunState>();
            TaskConfigDatabase.TaskConfig task = new TaskConfigDatabase.TaskConfig
            {
                id = "stage_end_failure_test",
                failurePenalties = new[] { new TaskConfigDatabase.TaskPenalty { type = "worldHostility", value = 0.5f } },
                completionConditions = new[]
                {
                    new TaskConfigDatabase.TaskCondition
                    {
                        type = "collectItem",
                        itemId = "unstable_crystal",
                        count = 3
                    }
                },
                failureConditions = new[]
                {
                    new TaskConfigDatabase.TaskCondition
                    {
                        type = "stageEndedWithoutCompletion",
                        count = 1
                    }
                }
            };

            float beforeFailure = WorldHostilityDirector.Current.RawHostility;
            Assert.IsTrue(taskState.AcceptTask(task));
            taskState.NotifyItemCollected("unstable_crystal", 2, Vector2.one);
            Assert.IsFalse(taskState.IsCompleted);
            taskState.NotifyStageEnded(Vector2.one);

            Assert.IsTrue(taskState.IsFailed);
            Assert.Greater(WorldHostilityDirector.Current.RawHostility, beforeFailure);
        }
        finally
        {
            Object.DestroyImmediate(taskObject);
            WorldHostilityDirector.ResetRuntime(null);
        }
    }

    [Test]
    public void TaskRunStateExistingDoesNotCreateRuntimeObject()
    {
        int before = Object.FindObjectsOfType<TaskRunState>().Length;

        TaskRunState existing = TaskRunState.Existing;

        int after = Object.FindObjectsOfType<TaskRunState>().Length;
        Assert.AreEqual(before, after);
        if (before == 0)
        {
            Assert.IsNull(existing);
        }
    }

    [Test]
    public void PlayerDamageSignalOnlyAppliesToPlayerStats()
    {
        WorldHostilityDirector.ResetRuntime(new StageConfig { worldHostility = 1f });
        GameObject playerObject = new GameObject("director-player-damage-test");
        GameObject enemyObject = new GameObject("director-enemy-damage-test");
        try
        {
            CharacterStats playerStats = playerObject.AddComponent<CharacterStats>();
            playerObject.AddComponent<PlayerInputManager>();
            CharacterStats enemyStats = enemyObject.AddComponent<CharacterStats>();
            HitResult hit = new HitResult(true, true, false, false, 12f, FeedbackPayload.None);
            DamageContext context = CreateDamageContext(Vector2.one, 12f, 0f);

            DirectorSignalBridge.NotifyPlayerDamaged(enemyStats, context, hit);
            float afterEnemySignal = WorldHostilityDirector.Current.RawHostility;

            DirectorSignalBridge.NotifyPlayerDamaged(playerStats, context, hit);

            Assert.Greater(WorldHostilityDirector.Current.RawHostility, afterEnemySignal);
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(enemyObject);
            WorldHostilityDirector.ResetRuntime(null);
        }
    }

    [Test]
    public void SemanticWorldTelemetryCapturesMapAndPressureMetrics()
    {
        bool[,] walkable = CreateEmptyWalkable(7, 5);
        for (int x = 1; x <= 5; x++)
        {
            walkable[x, 2] = true;
        }

        Vector2Int start = new Vector2Int(1, 2);
        Vector2Int end = new Vector2Int(5, 2);
        Vector2Int[] route = { start, new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(7, 5, 1f, walkable, route, start, end, null, 1f);
        SemanticPlacementSolver.PopulateDefaultPlacements(map, new SemanticPlacementSettings
        {
            MaxSpawnDenBuildings = 0,
            MaxSupplyCaches = 0,
            MaxResourceNodes = 0,
            SafehouseFootprintRadius = 0
        });
        AftermathGrid aftermath = new AftermathGrid(map, 4);
        aftermath.SeedAmbientResidues(1, 1);
        WorldHostilityDirector director = new WorldHostilityDirector(new StageConfig { worldHostility = 1f });
        director.Notify(new DirectorEvent(DirectorEventType.SentinelAlarm, Vector2.zero, 1f));
        VisionRevealMap revealMap = new VisionRevealMap(map.Width, map.Height);
        revealMap.BeginFrame();
        revealMap.MarkVisible(new Vector2Int(2, 2));
        revealMap.MarkVisible(new Vector2Int(3, 2));

        SemanticWorldMetrics metrics = SemanticWorldTelemetry.Capture(map, aftermath, director, 3, null, revealMap);

        Assert.AreEqual(7, metrics.Width);
        Assert.AreEqual(5, metrics.Height);
        Assert.AreEqual(5, metrics.WalkableCells);
        Assert.AreEqual(route.Length, metrics.MainRouteCells);
        Assert.AreEqual(map.Buildings.Count, metrics.BuildingCount);
        Assert.AreEqual(map.ResourceNodes.Count, metrics.ResourceNodeCount);
        Assert.AreEqual(aftermath.Residues.Count, metrics.ResidueCount);
        Assert.AreEqual(2, metrics.VisibleCellCount);
        Assert.AreEqual(2, metrics.ExploredCellCount);
        Assert.AreEqual(3, metrics.LivingEnemyCount);
        Assert.Greater(metrics.RawHostility, 1f);
        Assert.Greater(metrics.SearchIntensity, 0f);
    }

    [Test]
    public void SemanticWorldBudgetCheckerReportsBudgetFailures()
    {
        SemanticWorldMetrics metrics = new SemanticWorldMetrics(
            20,
            20,
            120,
            20,
            7,
            12,
            64,
            18,
            0,
            0,
            9,
            3f,
            1f);
        SemanticWorldBudgets budgets = new SemanticWorldBudgets(
            maxBuildings: 8,
            maxResourceNodes: 16,
            maxResidues: 128,
            maxPooledResidues: 16,
            maxLivingEnemies: 24,
            maxRawHostility: 6f);

        SemanticWorldBudgetReport report = SemanticWorldBudgetChecker.Check(metrics, budgets);

        Assert.IsFalse(report.Passed);
        StringAssert.Contains("pooled residue", report.Message);
    }

    [Test]
    public void SemanticWorldBudgetCheckerPassesDefaultPrototypeBudgets()
    {
        SemanticWorldMetrics metrics = new SemanticWorldMetrics(
            100,
            80,
            2000,
            180,
            12,
            10,
            48,
            48,
            0,
            0,
            18,
            2.5f,
            1f);

        SemanticWorldBudgetReport report = SemanticWorldBudgetChecker.Check(metrics, SemanticWorldBudgets.Default);

        Assert.IsTrue(report.Passed, report.Message);
    }

    [Test]
    public void SemanticWorldBudgetsCanBeOverriddenByStageConfig()
    {
        SemanticWorldBudgets fallback = new SemanticWorldBudgets(10, 11, 12, 13, 14, 15f);
        StageConfig stageConfig = new StageConfig
        {
            semanticMaxBuildings = 3,
            semanticResourceNodeCount = 4,
            semanticMaxResidues = 5,
            semanticMaxPooledResidues = 6,
            semanticMaxLivingEnemies = 7,
            semanticMaxRawHostility = 8f
        };

        SemanticWorldBudgets budgets = SemanticWorldBudgets.FromStage(stageConfig, fallback);

        Assert.AreEqual(3, budgets.MaxBuildings);
        Assert.AreEqual(4, budgets.MaxResourceNodes);
        Assert.AreEqual(5, budgets.MaxResidues);
        Assert.AreEqual(6, budgets.MaxPooledResidues);
        Assert.AreEqual(7, budgets.MaxLivingEnemies);
        Assert.AreEqual(8f, budgets.MaxRawHostility);

        StageConfig automaticStageConfig = new StageConfig();
        SemanticWorldBudgets automaticBudgets = SemanticWorldBudgets.FromStage(automaticStageConfig, fallback);
        Assert.AreEqual(fallback.MaxBuildings, automaticBudgets.MaxBuildings);
        Assert.AreEqual(fallback.MaxResourceNodes, automaticBudgets.MaxResourceNodes);
    }

    [Test]
    public void StageConfigLoadsSemanticVisualCatalogOptions()
    {
        StageConfig stageConfig = StageConfigDatabase.Get(1, 1);

        Assert.IsNotNull(stageConfig);
        Assert.AreEqual("Configs/SemanticWorldVisualCatalog", stageConfig.semanticVisualCatalogResource);
        Assert.AreEqual(-1, stageConfig.semanticUseTilemapTerrain);
    }

    [Test]
    public void SemanticWorldViewRendersBuildingsResourcesAndResidues()
    {
        bool[,] walkable = CreateEmptyWalkable(9, 7);
        for (int x = 1; x <= 7; x++)
        {
            walkable[x, 3] = true;
        }

        walkable[4, 4] = true;
        walkable[4, 5] = true;
        Vector2Int start = new Vector2Int(1, 3);
        Vector2Int end = new Vector2Int(7, 3);
        Vector2Int resource = new Vector2Int(4, 5);
        Vector2Int[] route = { start, new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(9, 7, 1f, walkable, route, start, end, null, 1f);
        SemanticPlacementSolver.PopulateDefaultPlacements(map, new SemanticPlacementSettings
        {
            MaxSpawnDenBuildings = 0,
            MaxSupplyCaches = 0,
            MaxResourceNodes = 1,
            SafehouseFootprintRadius = 0
        });
        AftermathGrid aftermath = new AftermathGrid(map, 4);
        DamageContext hit = CreateDamageContext(CellToWorld(map, resource), 10f, 0f);
        aftermath.AddCombatHit(hit, new HitResult(true, true, false, false, 10f, hit.feedback), 1f);

        GameObject viewObject = new GameObject("semantic-world-view-test");
        try
        {
            SemanticWorldView view = viewObject.AddComponent<SemanticWorldView>();
            view.Render(map, aftermath);

            Assert.GreaterOrEqual(view.VisibleBuildingCount, 2);
            Assert.GreaterOrEqual(view.VisibleResourceCount, 1);
            Assert.GreaterOrEqual(view.VisibleResidueCount, 1);
            int pooledResiduesAfterRender = view.PooledResidueObjectCount;

            view.RefreshResidues(aftermath);

            Assert.AreEqual(pooledResiduesAfterRender, view.PooledResidueObjectCount);
            Assert.GreaterOrEqual(view.VisibleResidueCount, 1);
        }
        finally
        {
            Object.DestroyImmediate(viewObject);
        }
    }

    [Test]
    public void SemanticResourceResolverCreatesCatalogBackedInventoryItems()
    {
        ResourceNode materialNode = new ResourceNode
        {
            Type = ResourceType.Material,
            Cell = Vector2Int.zero,
            AmountMin = 2,
            AmountMax = 2,
            CatalogId = "material_iron_shard"
        };
        ResourceNode medicalNode = new ResourceNode
        {
            Type = ResourceType.Medical,
            Cell = Vector2Int.zero,
            AmountMin = 1,
            AmountMax = 1,
            CatalogId = "item_small_heal"
        };

        PlayerInventory.InventoryItem material = SemanticResourceResolver.CreateInventoryItem(materialNode, 2);
        PlayerInventory.InventoryItem medical = SemanticResourceResolver.CreateInventoryItem(medicalNode, 1);

        Assert.AreEqual("material", material.category);
        Assert.AreEqual("material_iron_shard", material.id);
        Assert.AreEqual(2, material.quantity);
        Assert.GreaterOrEqual(material.maxStack, 2);
        Assert.AreEqual("item", medical.category);
        Assert.AreEqual("item_small_heal", medical.id);
    }

    [Test]
    public void SemanticPlacementResourcesResolveToCatalogIds()
    {
        bool[,] walkable = CreateEmptyWalkable(9, 7);
        for (int x = 1; x <= 7; x++)
        {
            walkable[x, 3] = true;
        }

        walkable[4, 4] = true;
        walkable[4, 5] = true;
        Vector2Int start = new Vector2Int(1, 3);
        Vector2Int end = new Vector2Int(7, 3);
        Vector2Int resource = new Vector2Int(4, 5);
        Vector2Int[] route = { start, new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(9, 7, 1f, walkable, route, start, end, new[] { resource }, 1f);
        SemanticPlacementSolver.PopulateDefaultPlacements(map, new SemanticPlacementSettings
        {
            MaxSpawnDenBuildings = 0,
            MaxSupplyCaches = 0,
            MaxResourceNodes = 1,
            SafehouseFootprintRadius = 0
        });

        Assert.AreEqual(1, map.ResourceNodes.Count);
        ResourceNode node = map.ResourceNodes[0];
        Assert.IsFalse(string.IsNullOrEmpty(node.CatalogId));

        PlayerInventory.InventoryItem item = SemanticResourceResolver.CreateInventoryItem(node, 1);
        Assert.IsFalse(string.IsNullOrEmpty(item.id));
        Assert.IsFalse(string.IsNullOrEmpty(item.category));
    }

    [Test]
    public void SemanticResourcePickupCollectsAndNotifiesDirector()
    {
        WorldHostilityDirector.ResetRuntime(null);
        float greedBefore = WorldHostilityDirector.Current.Snapshot.GreedScalar;
        GameObject inventoryObject = new GameObject("semantic-resource-inventory-test");
        GameObject pickupObject = new GameObject("semantic-resource-pickup-test");
        try
        {
            PlayerInventory inventory = inventoryObject.AddComponent<PlayerInventory>();
            SemanticResourcePickup pickup = pickupObject.AddComponent<SemanticResourcePickup>();
            pickup.Configure(new ResourceNode
            {
                Type = ResourceType.RareCore,
                Cell = Vector2Int.zero,
                AmountMin = 1,
                AmountMax = 1,
                CatalogId = "material_arcane_splinter"
            });

            Assert.IsTrue(pickup.TryCollect(inventory));

            PlayerInventory.InventoryItem item = inventory.GetItem(0);
            Assert.IsNotNull(item);
            Assert.AreEqual("material_arcane_splinter", item.id);
            Assert.Greater(WorldHostilityDirector.Current.Snapshot.GreedScalar, greedBefore);
            Assert.IsFalse(pickupObject.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(pickupObject);
            Object.DestroyImmediate(inventoryObject);
            WorldHostilityDirector.ResetRuntime(null);
        }
    }

    [Test]
    public void SemanticWorldViewAddsPickupComponentsToResourceObjects()
    {
        bool[,] walkable = CreateEmptyWalkable(7, 5);
        for (int x = 1; x <= 5; x++)
        {
            walkable[x, 2] = true;
        }

        Vector2Int start = new Vector2Int(1, 2);
        Vector2Int end = new Vector2Int(5, 2);
        Vector2Int resource = new Vector2Int(3, 2);
        Vector2Int[] route = { start, new Vector2Int(2, 2), resource, new Vector2Int(4, 2), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(7, 5, 1f, walkable, route, start, end, new[] { resource }, 1f);
        map.ResourceNodes.Add(new ResourceNode
        {
            Type = ResourceType.Material,
            Cell = resource,
            AmountMin = 1,
            AmountMax = 1,
            CatalogId = "material_iron_shard"
        });

        GameObject viewObject = new GameObject("semantic-world-resource-pickup-view-test");
        try
        {
            SemanticWorldView view = viewObject.AddComponent<SemanticWorldView>();
            view.Render(map, null);

            SemanticResourcePickup pickup = viewObject.GetComponentInChildren<SemanticResourcePickup>();
            CircleCollider2D collider = viewObject.GetComponentInChildren<CircleCollider2D>();
            Assert.IsNotNull(pickup);
            Assert.IsNotNull(collider);
            Assert.IsTrue(collider.isTrigger);
        }
        finally
        {
            Object.DestroyImmediate(viewObject);
        }
    }

    [Test]
    public void SemanticTilemapRendererStampsSemanticLayers()
    {
        bool[,] walkable = CreateEmptyWalkable(5, 3);
        for (int x = 1; x <= 3; x++)
        {
            walkable[x, 1] = true;
        }

        Vector2Int start = new Vector2Int(1, 1);
        Vector2Int end = new Vector2Int(3, 1);
        Vector2Int[] route = { start, new Vector2Int(2, 1), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(5, 3, 1f, walkable, route, start, end, null, 1f);

        GameObject rendererObject = new GameObject("semantic-tilemap-renderer-test");
        Sprite sprite = CreateTestSprite();
        try
        {
            SemanticTilemapRenderer renderer = rendererObject.AddComponent<SemanticTilemapRenderer>();
            SetPrivateField(renderer, "groundSprite", sprite);
            SetPrivateField(renderer, "routeSprite", sprite);
            SetPrivateField(renderer, "wallSprite", sprite);
            SetPrivateField(renderer, "safehouseSprite", sprite);

            renderer.Render(map);

            Assert.IsNotNull(renderer.WallMap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.IsNotNull(renderer.SafehouseMap.GetTile(new Vector3Int(start.x, start.y, 0)));
            Assert.IsNotNull(renderer.RouteMap.GetTile(new Vector3Int(2, 1, 0)));
            Assert.IsNotNull(renderer.WallMap.GetComponent<TilemapCollider2D>());
            Assert.AreEqual(new Vector3(-2f, -1f, 0f), rendererObject.transform.Find("SemanticTilemaps").localPosition);
        }
        finally
        {
            Object.DestroyImmediate(rendererObject);
            Object.DestroyImmediate(sprite.texture);
            Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void GridGeneratorCanRenderSemanticTerrainWithTilemaps()
    {
        GameObject generatorObject = new GameObject("semantic-tilemap-generator-test");
        try
        {
            GridRouteMapGenerator generator = generatorObject.AddComponent<GridRouteMapGenerator>();
            ConfigureGeneratorForDeterminismTest(generator);
            SetPrivateField(generator, "useTilemapTerrain", true);

            generator.GenerateMap(101);

            SemanticTilemapRenderer renderer = generatorObject.GetComponent<SemanticTilemapRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(renderer.RouteMap);
            Assert.IsNotNull(renderer.WallMap);

            MapData map = generator.CurrentMapData;
            Assert.IsNotNull(map);
            Assert.Greater(map.MainRoute.Count, 1);

            Vector2Int routeCell = map.MainRoute[1];
            Assert.IsNotNull(renderer.RouteMap.GetTile(new Vector3Int(routeCell.x, routeCell.y, 0)));
            Assert.IsNotNull(renderer.WallMap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.IsNotNull(generator.LastValidationReport);
            Assert.IsTrue(generator.LastValidationReport.IsValid);
        }
        finally
        {
            Object.DestroyImmediate(generatorObject);
            DestroyGeneratedRuntimeObject("WorldHostilityDirectorRunner");
            DestroyGeneratedRuntimeObject("CombatAftermathSystem");
        }
    }

    [Test]
    public void GridGeneratorPassesVisualCatalogToTilemapRenderer()
    {
        GameObject generatorObject = new GameObject("semantic-tilemap-generator-catalog-test");
        SemanticWorldVisualCatalog catalog = ScriptableObject.CreateInstance<SemanticWorldVisualCatalog>();
        Sprite routeSprite = CreateTestSprite();
        try
        {
            catalog.Configure(null, null, null, null, routeSprite, null, null);

            GridRouteMapGenerator generator = generatorObject.AddComponent<GridRouteMapGenerator>();
            ConfigureGeneratorForDeterminismTest(generator);
            SetPrivateField(generator, "useTilemapTerrain", true);
            SetPrivateField(generator, "visualCatalog", catalog);

            generator.GenerateMap(151);

            SemanticTilemapRenderer renderer = generatorObject.GetComponent<SemanticTilemapRenderer>();
            Assert.IsNotNull(renderer);
            MapData map = generator.CurrentMapData;
            Vector2Int routeCell = map.MainRoute[1];
            Tile routeTile = renderer.RouteMap.GetTile<Tile>(new Vector3Int(routeCell.x, routeCell.y, 0));
            Assert.IsNotNull(routeTile);
            Assert.AreEqual(routeSprite, routeTile.sprite);
        }
        finally
        {
            Object.DestroyImmediate(generatorObject);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(routeSprite.texture);
            Object.DestroyImmediate(routeSprite);
            DestroyGeneratedRuntimeObject("WorldHostilityDirectorRunner");
            DestroyGeneratedRuntimeObject("CombatAftermathSystem");
        }
    }

    [Test]
    public void SemanticWorldViewUsesVisualCatalogBindings()
    {
        bool[,] walkable = CreateEmptyWalkable(6, 4);
        for (int x = 1; x <= 4; x++)
        {
            walkable[x, 2] = true;
        }

        Vector2Int start = new Vector2Int(1, 2);
        Vector2Int end = new Vector2Int(4, 2);
        MapData map = SemanticMapBuilder.BuildFromRoute(6, 4, 1f, walkable, new[] { start, new Vector2Int(2, 2), new Vector2Int(3, 2), end }, start, end, null, 1f);
        map.Buildings.Add(new BuildingInstance
        {
            DefinitionId = "catalog_supply_cache",
            Kind = BuildingKind.SupplyCache,
            AnchorCell = new Vector2Int(2, 2),
            Footprint = new RectInt(2, 2, 1, 1),
            Score = 1f
        });
        map.ResourceNodes.Add(new ResourceNode
        {
            Type = ResourceType.Material,
            Cell = new Vector2Int(3, 2),
            AmountMin = 1,
            AmountMax = 1,
            CatalogId = "material_iron_shard"
        });

        GameObject viewObject = new GameObject("semantic-world-catalog-view-test");
        SemanticWorldVisualCatalog catalog = ScriptableObject.CreateInstance<SemanticWorldVisualCatalog>();
        Sprite buildingSprite = CreateTestSprite();
        Sprite resourceSprite = CreateTestSprite();
        try
        {
            SetPrivateField(catalog, "buildings", new[]
            {
                new BuildingVisualBinding
                {
                    Kind = BuildingKind.SupplyCache,
                    Sprite = buildingSprite,
                    Scale = 1.75f,
                    SortingOrderOffset = 3
                }
            });
            SetPrivateField(catalog, "resources", new[]
            {
                new ResourceVisualBinding
                {
                    Type = ResourceType.Material,
                    Sprite = resourceSprite,
                    Scale = 0.8f,
                    SortingOrderOffset = 2
                }
            });

            SemanticWorldView view = viewObject.AddComponent<SemanticWorldView>();
            view.SetVisualCatalog(catalog);
            view.Render(map, null);

            SpriteRenderer[] renderers = viewObject.GetComponentsInChildren<SpriteRenderer>();
            Assert.IsTrue(System.Array.Exists(renderers, renderer => renderer.sprite == buildingSprite && Mathf.Approximately(renderer.transform.localScale.x, 1.75f)));
            Assert.IsTrue(System.Array.Exists(renderers, renderer => renderer.sprite == resourceSprite && Mathf.Approximately(renderer.transform.localScale.x, 0.8f)));
            Assert.IsNotNull(viewObject.GetComponentInChildren<SemanticResourcePickup>());
        }
        finally
        {
            Object.DestroyImmediate(viewObject);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(buildingSprite.texture);
            Object.DestroyImmediate(resourceSprite.texture);
            Object.DestroyImmediate(buildingSprite);
            Object.DestroyImmediate(resourceSprite);
        }
    }

    [Test]
    public void SemanticWorldVisualCatalogValidatesRequiredBindings()
    {
        SemanticWorldVisualCatalog catalog = ScriptableObject.CreateInstance<SemanticWorldVisualCatalog>();
        Sprite sprite = CreateTestSprite();
        try
        {
            System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();
            Assert.IsFalse(catalog.ValidateRequiredBindings(failures));
            Assert.Greater(failures.Count, 0);

            catalog.Configure(
                new[]
                {
                    new BuildingVisualBinding { Kind = BuildingKind.Safehouse, Sprite = sprite },
                    new BuildingVisualBinding { Kind = BuildingKind.ExtractionRoom, Sprite = sprite },
                    new BuildingVisualBinding { Kind = BuildingKind.SpawnDen, Sprite = sprite },
                    new BuildingVisualBinding { Kind = BuildingKind.SupplyCache, Sprite = sprite }
                },
                new[]
                {
                    new ResourceVisualBinding { Type = ResourceType.Currency, Sprite = sprite },
                    new ResourceVisualBinding { Type = ResourceType.Material, Sprite = sprite },
                    new ResourceVisualBinding { Type = ResourceType.RareCore, Sprite = sprite }
                },
                new[]
                {
                    new ResidueVisualBinding { Type = CombatResidueType.BloodDrop, Sprite = sprite },
                    new ResidueVisualBinding { Type = CombatResidueType.BloodPool, Sprite = sprite },
                    new ResidueVisualBinding { Type = CombatResidueType.Corpse, Sprite = sprite },
                    new ResidueVisualBinding { Type = CombatResidueType.Scorch, Sprite = sprite }
                },
                sprite,
                sprite,
                sprite,
                sprite);

            failures.Clear();
            Assert.IsTrue(catalog.ValidateRequiredBindings(failures), failures.Count > 0 ? failures[0] : string.Empty);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(sprite.texture);
            Object.DestroyImmediate(sprite);
        }
    }

    [Test]
    public void SemanticTilemapRendererUsesVisualCatalogTiles()
    {
        bool[,] walkable = CreateEmptyWalkable(5, 3);
        for (int x = 1; x <= 3; x++)
        {
            walkable[x, 1] = true;
        }

        Vector2Int start = new Vector2Int(1, 1);
        Vector2Int end = new Vector2Int(3, 1);
        MapData map = SemanticMapBuilder.BuildFromRoute(5, 3, 1f, walkable, new[] { start, new Vector2Int(2, 1), end }, start, end, null, 1f);

        GameObject rendererObject = new GameObject("semantic-tilemap-catalog-test");
        SemanticWorldVisualCatalog catalog = ScriptableObject.CreateInstance<SemanticWorldVisualCatalog>();
        Sprite routeSprite = CreateTestSprite();
        try
        {
            SetPrivateField(catalog, "routeTileSprite", routeSprite);

            SemanticTilemapRenderer renderer = rendererObject.AddComponent<SemanticTilemapRenderer>();
            renderer.SetVisualCatalog(catalog);
            renderer.Render(map);

            Tile routeTile = renderer.RouteMap.GetTile<Tile>(new Vector3Int(2, 1, 0));
            Assert.IsNotNull(routeTile);
            Assert.AreEqual(routeSprite, routeTile.sprite);
        }
        finally
        {
            Object.DestroyImmediate(rendererObject);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(routeSprite.texture);
            Object.DestroyImmediate(routeSprite);
        }
    }

    [Test]
    public void CombatAftermathExistingDoesNotCreateRuntimeObject()
    {
        int before = Object.FindObjectsOfType<CombatAftermathSystem>().Length;

        CombatAftermathSystem existing = CombatAftermathSystem.Existing;

        int after = Object.FindObjectsOfType<CombatAftermathSystem>().Length;
        Assert.AreEqual(before, after);
        if (before == 0)
        {
            Assert.IsNull(existing);
        }
    }

    [Test]
    public void CombatAftermathSystemRefreshesCachedWorldViewAfterHit()
    {
        bool[,] walkable = CreateEmptyWalkable(7, 5);
        for (int x = 1; x <= 5; x++)
        {
            walkable[x, 2] = true;
        }

        Vector2Int start = new Vector2Int(1, 2);
        Vector2Int end = new Vector2Int(5, 2);
        Vector2Int hitCell = new Vector2Int(3, 2);
        Vector2Int[] route = { start, new Vector2Int(2, 2), hitCell, new Vector2Int(4, 2), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(7, 5, 1f, walkable, route, start, end, null, 1f);

        GameObject viewObject = new GameObject("cached-semantic-world-view-test");
        GameObject systemObject = new GameObject("cached-combat-aftermath-test");
        try
        {
            SemanticWorldView view = viewObject.AddComponent<SemanticWorldView>();
            CombatAftermathSystem aftermathSystem = systemObject.AddComponent<CombatAftermathSystem>();
            aftermathSystem.Configure(map, view);
            view.Render(map, aftermathSystem.Grid);
            int visibleResiduesBeforeHit = view.VisibleResidueCount;

            DamageContext hit = CreateDamageContext(CellToWorld(map, hitCell), 8f, 0f);
            CombatFeedbackBus.PublishHit(hit, new HitResult(true, true, false, false, 8f, hit.feedback));

            Assert.Greater(view.VisibleResidueCount, visibleResiduesBeforeHit);
            Assert.GreaterOrEqual(view.PooledResidueObjectCount, view.VisibleResidueCount);
        }
        finally
        {
            Object.DestroyImmediate(systemObject);
            Object.DestroyImmediate(viewObject);
        }
    }

    [Test]
    public void SemanticVisionQueryUsesMapDataVisionBlockers()
    {
        bool[,] walkable = CreateEmptyWalkable(5, 3);
        for (int x = 0; x < 5; x++)
        {
            walkable[x, 1] = true;
        }

        Vector2Int start = new Vector2Int(0, 1);
        Vector2Int end = new Vector2Int(4, 1);
        Vector2Int blocker = new Vector2Int(2, 1);
        Vector2Int[] route = { start, new Vector2Int(1, 1), blocker, new Vector2Int(3, 1), end };
        MapData map = SemanticMapBuilder.BuildFromRoute(5, 3, 1f, walkable, route, start, end, null, 1f);
        MapCell blockedCell = map.GetCell(blocker);
        blockedCell.Flags |= CellFlags.BlocksVision;
        map.Cell(blocker.x, blocker.y) = blockedCell;

        GameObject generatorObject = new GameObject("semantic-vision-query-test");
        try
        {
            GridRouteMapGenerator generator = generatorObject.AddComponent<GridRouteMapGenerator>();
            ConfigureGeneratorForVisionTest(generator, map, new bool[5, 3]);

            Assert.IsTrue(SemanticVisionQuery.BlocksVision(generator, blocker));
            Assert.Less(SemanticVisionQuery.GetBlockedDistance(generator, CellToWorld(map, start), Vector2.right, 4f, 0.25f), 4f);
        }
        finally
        {
            Object.DestroyImmediate(generatorObject);
        }
    }

    [Test]
    public void SemanticVisionQueryFallsBackToLegacyVisionBlockers()
    {
        bool[,] blockers = new bool[5, 3];
        blockers[2, 1] = true;

        GameObject generatorObject = new GameObject("legacy-vision-query-test");
        try
        {
            GridRouteMapGenerator generator = generatorObject.AddComponent<GridRouteMapGenerator>();
            ConfigureGeneratorForVisionTest(generator, null, blockers, 5, 3, 1f);

            Assert.IsTrue(SemanticVisionQuery.BlocksVision(generator, new Vector2Int(2, 1)));
            Assert.IsFalse(SemanticVisionQuery.BlocksVision(generator, new Vector2Int(1, 1)));
        }
        finally
        {
            Object.DestroyImmediate(generatorObject);
        }
    }

    [Test]
    public void VisionRevealMapKeepsExploredCellsAcrossFrames()
    {
        VisionRevealMap revealMap = new VisionRevealMap(4, 3);
        Vector2Int firstCell = new Vector2Int(1, 1);
        Vector2Int secondCell = new Vector2Int(2, 1);

        revealMap.BeginFrame();
        revealMap.MarkVisible(firstCell);
        revealMap.MarkVisible(firstCell);
        revealMap.MarkVisible(secondCell);

        Assert.AreEqual(2, revealMap.VisibleCount);
        Assert.AreEqual(2, revealMap.ExploredCount);
        Assert.IsTrue(revealMap.IsVisible(firstCell));
        Assert.IsTrue(revealMap.IsExplored(secondCell));

        revealMap.BeginFrame();

        Assert.AreEqual(0, revealMap.VisibleCount);
        Assert.IsFalse(revealMap.IsVisible(firstCell));
        Assert.IsTrue(revealMap.IsExplored(firstCell));
        Assert.IsTrue(revealMap.IsExplored(secondCell));
    }

    private static bool[,] CreateEmptyWalkable(int width, int height)
    {
        return new bool[width, height];
    }

    private static int CountBuildings(MapData map, BuildingKind kind)
    {
        int count = 0;
        for (int i = 0; i < map.Buildings.Count; i++)
        {
            if (map.Buildings[i].Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static bool FootprintTouchesRoute(MapData map, RectInt footprint)
    {
        for (int y = footprint.yMin; y < footprint.yMax; y++)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            {
                if (map.GetCell(new Vector2Int(x, y)).RegionKind == RegionKind.MainRoute)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static DamageContext CreateDamageContext(Vector2 hitPoint, float physicalDamage, float magicDamage)
    {
        return new DamageContext(
            1,
            null,
            null,
            WeaponType.Sword,
            hitPoint,
            hitPoint,
            Vector2.right,
            physicalDamage,
            magicDamage,
            0f,
            new FeedbackPayload(0.01f, 0.1f, 0.5f, 0.1f, 1.2f, 0f));
    }

    private static Vector2 CellToWorld(MapData map, Vector2Int cell)
    {
        float x = (cell.x - (map.Width - 1) * 0.5f) * map.CellSize;
        float y = (cell.y - (map.Height - 1) * 0.5f) * map.CellSize;
        return new Vector2(x, y);
    }

    private static Sprite CreateTestSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
    }

    private static void ConfigureGeneratorForVisionTest(GridRouteMapGenerator generator, MapData map, bool[,] blockers)
    {
        ConfigureGeneratorForVisionTest(generator, map, blockers, map.Width, map.Height, map.CellSize);
    }

    private static void ConfigureGeneratorForVisionTest(GridRouteMapGenerator generator, MapData map, bool[,] blockers, int width, int height, float cellSize)
    {
        SetPrivateField(generator, "width", width);
        SetPrivateField(generator, "height", height);
        SetPrivateField(generator, "cellSize", cellSize);
        SetPrivateField(generator, "currentMapData", map);
        SetPrivateField(generator, "visionBlockers", blockers);
    }

    private static void ConfigureGeneratorForDeterminismTest(GridRouteMapGenerator generator)
    {
        SetPrivateField(generator, "width", 28);
        SetPrivateField(generator, "height", 20);
        SetPrivateField(generator, "cellSize", 1f);
        SetPrivateField(generator, "corridorHalfWidth", 1);
        SetPrivateField(generator, "branchCount", 4);
        SetPrivateField(generator, "roomCount", 4);
        SetPrivateField(generator, "enemySpawnCount", 0);
        SetPrivateField(generator, "showEnemySpawnMarkers", false);
    }

    private static string Fingerprint(MapData map)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append(map.Width).Append('x').Append(map.Height).Append(':').Append(map.Seed).Append('|');
        for (int i = 0; i < map.MainRoute.Count; i++)
        {
            builder.Append(map.MainRoute[i].x).Append(',').Append(map.MainRoute[i].y).Append(';');
        }

        builder.Append('|');
        for (int i = 0; i < map.Cells.Length; i++)
        {
            MapCell cell = map.Cells[i];
            builder.Append((int)cell.TileType).Append(',').Append((int)cell.RegionKind).Append(',').Append((int)cell.Flags).Append(';');
        }

        builder.Append('|');
        for (int i = 0; i < map.Buildings.Count; i++)
        {
            BuildingInstance building = map.Buildings[i];
            builder.Append((int)building.Kind).Append('@').Append(building.AnchorCell.x).Append(',').Append(building.AnchorCell.y).Append(';');
        }

        builder.Append('|');
        for (int i = 0; i < map.ResourceNodes.Count; i++)
        {
            ResourceNode node = map.ResourceNodes[i];
            builder.Append((int)node.Type).Append('@').Append(node.Cell.x).Append(',').Append(node.Cell.y).Append(':').Append(node.CatalogId).Append(';');
        }

        return builder.ToString();
    }

    private static void DestroyGeneratedRuntimeObject(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null)
        {
            Object.DestroyImmediate(obj);
        }
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }
}
