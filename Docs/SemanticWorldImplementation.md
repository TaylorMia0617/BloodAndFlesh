# Semantic World Implementation

This note summarizes the current implementation of the deep-research world-generation plan in this Unity project.

See `Docs/DeepResearchExecutionSummary.md` for the report-to-project alignment summary.
See `Docs/SemanticWorldAcceptanceChecklist.md` for the evidence checklist required before considering the report execution complete.

## Implemented Runtime Layers

- `SemanticMapData` defines the grid contract: walkability, blockers, route cells, region kinds, spawn sockets, resource sockets, buildings, and resource nodes.
- `SemanticPlacementSolver` stamps safehouse, extraction, spawn-den, supply-cache, and resource-node data onto `MapData`.
- `CombatAftermathSystem` records persistent blood, scorch, corpse, debris, and grass residues, then refreshes `SemanticWorldView`.
- `WorldHostilityDirector` is now event-driven and tracks global pressure, local pressure, search intensity, greed, safehouse state, and spawn gating.
- `SemanticVisionQuery` makes player fog and enemy vision read the same semantic `BlocksVision` flags.
- `VisionRevealMap` tracks current visible cells and persistent explored cells for future fog and map UI work.
- `SemanticResourceResolver` and `SemanticResourcePickup` connect semantic resource nodes to the existing item/material catalogs, player inventory, and Director greed pressure.
- `DirectorSignalBridge` connects player damage, item side effects, high-value drops, and task completion/failure to Director events.
- `TaskRunState` lets the safe-room task board accept one active task, tracks kill/collect/failure conditions, grants simple currency/material rewards, and notifies the Director on task completion or failure. Gameplay event sources use `TaskRunState.Existing`, so they do not create empty task state when no task is active.
- `SemanticTilemapRenderer` provides an optional Tilemap-based terrain renderer for `MapData` layers.
- `SemanticWorldVisualCatalog` provides a ScriptableObject binding layer for semantic building, resource, residue, and tile visuals.

## Generator Integration

`GridRouteMapGenerator` remains the topology generator, but now produces `CurrentMapData` after the road/path pass.

The generator now supports deterministic runs:

```csharp
generator.GenerateMap(431);
```

Seeded generation saves and restores Unity's global random state. Each generation also records:

- `LastGenerationMilliseconds`
- `LastValidationReport`

## Validation And Tooling

`SemanticMapValidator` verifies core map constraints:

- main route starts at the safehouse and ends at extraction
- route cells are continuous and walkable
- buildings stay in bounds and do not overlap
- non-safehouse buildings do not stamp onto non-walkable cells
- resource nodes are walkable, non-safe, unique, and catalog-backed
- spawn sockets are walkable and outside Director deny zones

`SemanticMapValidationBatchReport` aggregates multiple seed runs.

Unity editor menu:

```text
Tools/TopDownActRogue/Validate Semantic World Seeds
```

This runs a default set of seeds and logs pass/fail, average generation time, the slowest seed, semantic budget failures, and generation-time budget failures.

```text
Tools/TopDownActRogue/Create Default Semantic Visual Catalog
```

`Assets/Resources/Configs/SemanticWorldVisualCatalog.asset` is included with the project using the current placeholder art bindings for semantic buildings, resources, residues, and terrain tiles. This menu can refresh that asset after placeholder art changes.

```text
Tools/TopDownActRogue/Audit Current Semantic World Scene
```

This inspects the active Unity scene for required semantic-world integration points: `GridRouteMapGenerator`, `RunLevelManager`, `PlayerVisionMask`, optional semantic render helpers, the default visual catalog asset, and required catalog bindings for core buildings, resources, residues, and Tilemap terrain.

Batch automation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\RunSemanticWorldAcceptance.ps1
```

This local acceptance wrapper builds the C# projects, runs Unity EditMode tests, then runs semantic world validation. It stops at the first failed step.

```text
Unity.exe -batchmode -quit -projectPath D:\DevFiles\TopDownActRogue -executeMethod SemanticWorldValidationMenu.BatchCreateCatalogAndValidate
```

This creates or refreshes the default visual catalog, audits the active scene, validates the default seed set, checks semantic and generation-time budgets, writes `Logs/semantic-world-scene-audit.json` and `Logs/semantic-world-validation-report.json`, and exits non-zero if either scene audit or seed validation fails.

For local validation, prefer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\RunSemanticWorldValidation.ps1
```

The wrapper checks `Temp\UnityLockfile` before launching Unity, then checks the Unity log and expected JSON reports after Unity runs. This matters because Unity can report a zero process exit code even when batchmode stops before running the validation method because the project is already open in another Editor instance.

Run EditMode tests through Unity Test Framework with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\RunEditModeTests.ps1
```

Do not use `dotnet test` as the authoritative test result for this project. The test assembly is Unity-generated and must be imported by Unity before all EditMode test files are reflected into the generated project and test runner.

## Debug Overlay

`SemanticWorldDebugOverlay` can draw semantic gizmos for:

- walkable cells
- main route
- buildings
- resources
- residues
- local pressure samples

Its optional runtime HUD shows map metrics, budget status, validation failure headline, residue counts, enemy count, hostility, search intensity, and fog reveal counts.

## Profiling Markers

Runtime hot paths now emit Unity Profiler markers:

- `SemanticWorld.GenerateMap`
- `SemanticWorld.Tilemap.Render`
- `SemanticWorld.View.Render`
- `SemanticWorld.View.RefreshResidues`
- `SemanticWorld.Aftermath.AddCombatHit`
- `SemanticWorld.Director.Notify`
- `SemanticWorld.Director.Tick`
- `SemanticWorld.Vision.RenderMask`
- `SemanticWorld.Vision.UpdateRayTexture`

## Tilemap Terrain Path

`SemanticTilemapRenderer` can stamp `MapData` into separate Tilemaps:

- `Ground`
- `MainRoute`
- `Safehouse`
- `Walls`

The wall layer can add a `TilemapCollider2D`. `GridRouteMapGenerator` keeps the existing sprite-object terrain path as the default, and can switch to this Tilemap path with its `useTilemapTerrain` toggle.

`SemanticWorldVisualCatalog` can feed both `SemanticWorldView` and `SemanticTilemapRenderer`, so prefab and sprite swaps can happen through authoring data rather than generator code. `GridRouteMapGenerator` will use an explicitly assigned catalog first, then fall back to `Resources.Load<SemanticWorldVisualCatalog>("Configs/SemanticWorldVisualCatalog")`.

Stage configs can also provide semantic render settings:

- `semanticVisualCatalogResource` overrides the default catalog resource path for that stage.
- `semanticUseTilemapTerrain` can force Tilemap terrain on (`1`), force it off (`0`), or leave the scene/Inspector setting unchanged (`-1`).

## Verification Status

Confirmed:

- `dotnet build TopDownActRogue.sln -v:minimal` passes with 0 warnings and 0 errors.
- EditMode contract tests have been added for deterministic generation, validation, resource resolution, aftermath, Director signals, fog/vision semantics, rendering/pickup wiring, Tilemap rendering, and visual catalog bindings.
- Task runtime contract tests cover kill-task completion, collect progress, stage-end failure, reward/Director notification, and task failure pressure.
- Stage config contract tests cover semantic visual catalog and Tilemap terrain options loading from `stage_config.json`.
- The editor seed validator now checks map validity, semantic world budgets, generation-time budget, average generation time, and slowest seed.
- A batchmode validation method now creates the default visual catalog and writes JSON scene-audit plus seed-validation reports for CI/local automation.
- Runtime profiling markers have been added for generation, Tilemap rendering, semantic view rendering, combat aftermath, Director updates, and vision ray texture updates.
- A current-scene audit menu now checks whether the active scene has the semantic world integration points needed for manual QA.
- The visual catalog now exposes required-binding validation, and the scene audit fails if the default catalog is missing key semantic visuals.
- Local wrapper scripts now verify Unity log output and required result artifacts so Unity batch failures are not hidden by misleading process exit codes.

Not yet confirmed:

- Unity EditMode CLI test execution while this project is already open in the Unity Editor. `Temp\UnityLockfile` exists, so `Tools\RunEditModeTests.ps1` exits before launching Unity. Earlier Unity logs showed `HandleProjectAlreadyOpenInAnotherInstance`, and no `Logs/editmode-test-results.xml` was produced.
- Unity semantic batch validation execution while this project is already open in the Unity Editor. `Temp\UnityLockfile` exists, so `Tools\RunSemanticWorldValidation.ps1` exits before launching Unity. Earlier Unity logs showed `HandleProjectAlreadyOpenInAnotherInstance`, and no JSON reports were produced in that run. Close the open Unity editor instance for this project before rerunning `Tools\RunSemanticWorldValidation.ps1`. The default visual catalog asset now exists in source, so batch validation no longer has to be the first path that creates it.

Known local-state issue:

- `Library/Bee/TundraBuildState.state.map`
- `Library/Bee/tundra.log.json`

These are Unity/Bee generated cache files touched by the Unity CLI attempts. They are not source changes, but Windows is currently refusing `git restore` because the files are locked by the local environment.

## Remaining Report Work

The next larger steps are:

- promote `SemanticTilemapRenderer` from optional generator mode to the default terrain path after in-editor visual QA
- author production prefab/tile assets into one or more `SemanticWorldVisualCatalog` assets
- optionally author stage-specific visual catalogs through `semanticVisualCatalogResource`
- add PlayMode smoke tests once Unity CLI test execution is healthy
- profile generation, combat, fog, and Director updates in the Unity Profiler
- expand task rewards beyond currency/material into buff-choice, shop-discount, and longer-term safehouse progression effects
