# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 2022.3.23f1c1 project. Author gameplay code in `Assets/Scripts`, editor-only tooling in `Assets/Editor`, scenes in `Assets/Scenes`, shaders in `Assets/Shaders`, resources in `Assets/Resources`, and tests in `Assets/Tests`. Design notes and pseudocode live in `Docs`. Unity package dependencies are tracked in `Packages/manifest.json` and `Packages/packages-lock.json`.

Do not hand-edit generated Unity folders such as `Library`, `Logs`, or `UserSettings`; they are local machine state and should stay out of reviews.

## Art Asset Direction Conventions

Player-facing sprite names are literal. `Assets/Resources/Arts/SimpleSprites/player_left_simple.png` must show the character facing left, and `Assets/Resources/Arts/SimpleSprites/player_right_simple.png` must show the character facing right. `PlayerInputManager` uses `playerLeftSprite` when the target or cursor is left of the player and `playerRightSprite` when the target or cursor is right of the player.

Before renaming, replacing, regenerating, or assigning character-facing sprites, verify the visual direction in Unity so left and right are not swapped. Current enemy base sprites are left/default-facing, and `SimpleEnemyAI` flips them horizontally with `flipX` when the enemy faces right.

## Build, Test, and Development Commands

Open the project with Unity Hub or the matching editor version listed in `ProjectSettings/ProjectVersion.txt`.

- `Unity.exe -projectPath D:\DevFiles\TopDownActRogue`: open the project from the command line.
- `Unity.exe -batchmode -quit -projectPath D:\DevFiles\TopDownActRogue -runTests -testPlatform EditMode -testResults TestResults.xml`: run edit-mode tests headlessly.
- `dotnet build TopDownActRogue.sln`: compile generated C# projects when Unity project files are present.

Regenerate `.sln` and `.csproj` files from Unity if they are stale; these files are ignored by git.

## Coding Style & Naming Conventions

Use C# with 4-space indentation and braces on their own lines, matching existing files in `Assets/Scripts`. Use `PascalCase` for classes, structs, methods, properties, and enum values; use `camelCase` for locals, parameters, and public Unity-serialized fields already following that style. Keep MonoBehaviour components focused on Unity lifecycle and scene wiring; place deterministic gameplay rules in plain classes or static helpers where practical.

## Testing Guidelines

The project uses Unity Test Framework with NUnit. Current tests are edit-mode tests under `Assets/Tests/EditMode`, with an assembly definition at `Assets/Tests/EditMode/TopDownRogue.EditModeTests.asmdef`. Name test classes by feature, for example `CombatContractTests`, and use descriptive test method names such as `DamageCalculatorUsesAttackWeaponMultiplierAndArmor`. Add or update tests for combat math, state machines, config resolution, and other deterministic behavior.

## Commit & Pull Request Guidelines

The git history is currently minimal (`first commit`), so use clear imperative commit messages such as `Add enemy spawn balancing tests` or `Fix weapon timing recovery`. Keep commits scoped to one concern.

Pull requests should include a short summary, test results, linked issue or task when available, and screenshots or clips for visible gameplay, UI, shader, or scene changes. Note any Unity editor version or package changes explicitly.

## Security & Configuration Tips

Do not commit local build outputs, editor caches, credentials, or machine-specific settings. Review `Packages/manifest.json` changes carefully, especially Git-based dependencies such as `cn.unity.uos.launcher`.
