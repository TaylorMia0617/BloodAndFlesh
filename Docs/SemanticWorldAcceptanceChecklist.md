# Semantic World Acceptance Checklist

This checklist tracks completion evidence for executing the deep-research world-generation plan in this Unity project.

## Source Scope

- Research plan: `C:\Users\user\Downloads\deep-research-report.md`
- Project: `D:\DevFiles\TopDownActRogue`
- Unity version: `2022.3.23f1c1`
- Visual reference: dark pixel-art roguelike world kit with ruined stone, ritual structures, tents, corpses, blood trails, sparse vegetation, and landmark silhouettes

## Evidence Already Present

### Semantic Map Pipeline

- `Assets/Scripts/SemanticMapData.cs` defines semantic cells, flags, regions, buildings, resources, and map data.
- `Assets/Scripts/GridRouteMapGenerator.cs` exposes generated semantic map data, validation status, and generation timing.
- `Assets/Scripts/SemanticMapValidator.cs` validates connectivity, mandatory structures, resources, spawn sockets, and semantic budgets.

Status: source implemented; Unity scene audit still pending.

### Placement And Resources

- `Assets/Scripts/SemanticPlacementSolver.cs` places required structures and resources using map semantics.
- `Assets/Scripts/SemanticResourceResolver.cs` and `Assets/Scripts/SemanticResourcePickup.cs` connect semantic resources to existing inventory/catalog flow.
- `Assets/Resources/Configs/stage_config.json` includes semantic visual catalog and Tilemap override fields.

Status: source implemented; play-mode pickup QA still pending.

### Combat Aftermath

- `Assets/Scripts/CombatAftermathSystem.cs` records persistent residue through a coarsened grid.
- `Assets/Scripts/SemanticWorldView.cs` can render aftermath residues through catalog bindings.
- Aftermath emits Director-facing combat pressure events.

Status: source implemented; runtime readability/performance QA still pending.

### Fog And Perception

- `Assets/Scripts/SemanticVisionQuery.cs` provides line-of-sight checks against semantic blockers.
- `Assets/Scripts/VisionRevealMap.cs` tracks visible and explored cells.
- `Assets/Scripts/PlayerVisionMask.cs` and `Assets/Scripts/EnemyPerception.cs` consume semantic blockers.

Status: source implemented; visual shader QA still pending.

### Director And Task Hooks

- `Assets/Scripts/WorldHostilityDirector.cs` tracks pressure, local pressure, search, greed, safehouse state, and spawn gating.
- `Assets/Scripts/DirectorSignalBridge.cs` forwards player damage, item effects, drops, and task results.
- `Assets/Scripts/TaskRunState.cs` supports active task progress, completion/failure, rewards, and Director notification.

Status: source implemented; play-mode pacing/tuning still pending.

### Tooling And Visual Catalog

- `Assets/Scripts/SemanticWorldVisualCatalog.cs` provides authoring bindings for semantic visuals.
- `Assets/Resources/Configs/SemanticWorldVisualCatalog.asset` is included as the default catalog.
- `Assets/Editor/SemanticWorldValidationMenu.cs` provides seed validation, catalog creation, scene audit, and batch validation.
- `Tools/RunSemanticWorldValidation.ps1` checks Unity log output and required semantic validation reports.
- `Tools/RunEditModeTests.ps1` checks Unity log output and required EditMode test result XML.

Status: tooling implemented; Unity CLI execution is currently blocked by the project-open guard.

## Verified Commands

### C# Compilation

Command:

```powershell
dotnet build TopDownActRogue.sln -v:minimal
```

Latest observed result:

- success
- 0 warnings
- 0 errors

Coverage limit:

- This proves C# compilation only.
- It does not prove Unity asset import, generated test project freshness, scene wiring, shader behavior, or play-mode behavior.

## One-Command Acceptance

To check whether Unity is currently available for validation without launching Unity, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\GetSemanticWorldAcceptanceStatus.ps1
```

When `Temp\UnityLockfile` exists, this status command also lists visible Unity processes so the open editor instance can be identified without starting another Unity process.

After closing the Unity Editor for this project, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\RunSemanticWorldAcceptance.ps1
```

This command runs:

1. `dotnet build TopDownActRogue.sln -v:minimal`
2. `Tools\RunEditModeTests.ps1`
3. `Tools\RunSemanticWorldValidation.ps1`

It stops at the first failed step.

## Required Before Completion

### Unity EditMode Tests

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\RunEditModeTests.ps1
```

Required evidence:

- `Logs/editmode-test-results.xml` exists
- XML `test-run` total is greater than 0
- XML failed count is 0
- XML inconclusive count is 0
- Unity log does not contain fatal project-open, compilation, or test-run failure patterns

Current state:

- blocked
- `Temp\UnityLockfile` exists, which means this project is open in the Unity Editor
- `Logs/editmode-tests.log` contains `HandleProjectAlreadyOpenInAnotherInstance`
- `Logs/editmode-test-results.xml` is not produced

### Semantic World Batch Validation

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\RunSemanticWorldValidation.ps1
```

Required evidence:

- `Logs/semantic-world-scene-audit.json` exists
- `Logs/semantic-world-validation-report.json` exists
- Unity log does not contain fatal project-open, compilation, or validation failure patterns
- scene audit reports no missing required semantic integration points
- seed validation reports no semantic validation failures
- seed validation reports no semantic or generation-time budget failures

Current state:

- blocked
- `Temp\UnityLockfile` exists, which means this project is open in the Unity Editor
- `Logs/semantic-world-batch.log` contains `HandleProjectAlreadyOpenInAnotherInstance`
- semantic scene-audit and seed-validation JSON reports are not produced

### Manual Unity QA

Required evidence:

- active run scene renders the semantic terrain/buildings/resources without missing references
- fog reveal follows semantic blockers and player radius
- enemies respect the same semantic blockers for perception
- aftermath remains readable during combat and does not flood the scene with objects
- Director pressure visibly changes spawn/search behavior after alarms, kills, greed, safehouse use, and task outcomes
- profiler confirms generation, fog, Director, and aftermath paths are within budget for the target scene

Current state:

- pending
- requires Unity Editor play-mode inspection after the open-project CLI blocker is cleared

## Completion Rule

The report execution should not be considered complete until:

1. C# compilation passes.
2. Unity EditMode tests pass through `Tools\RunEditModeTests.ps1`.
3. Semantic world batch validation passes through `Tools\RunSemanticWorldValidation.ps1`.
4. Manual Unity QA confirms scene rendering, fog/perception, aftermath readability, Director behavior, and profiler budget.

`Tools\RunSemanticWorldAcceptance.ps1` covers the first three completion checks.
