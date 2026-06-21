---
status: in-progress
branch: no-git
timestamp: 2026-06-19T21:23:00+08:00
files_modified:
  - Assets/Scripts/PlayerInputManager.cs
  - Assets/Scripts/PlayerCombatController.cs
  - Assets/Scripts/WeaponDefinition.cs
  - Assets/Scripts/WeaponType.cs
  - Assets/Scripts/IDamageable.cs
  - Assets/Scripts/PlayerInput.inputactions
---

## Working On: Unity Migration And Core Prototype

### Summary

This project is a top-down action roguelite with delayed player movement, delayed/cooldown-based attacks, safe-room progression, and light roguelite weapon growth. The project was migrated from a Chinese-path Unity workspace to `D:\DevFiles\TopDownActRogue` to avoid path and Unity China/Unity Editor instability.

### Product Direction

- Core references: Barotrauma-style route/random-map pressure, Left 4 Dead safe-room pacing, Hades II-style starting room and weapon unlock space.
- Main scenes planned:
  - Player starting room: weapon unlocks, weapon skill trees, arcane system, core meta systems.
  - In-run combat: fighting, resource acquisition, procedural encounter segments.
  - Safe room: small station-like area between dangerous segments, one-time random buff reward, repeatable shop/weapon upgrades.
  - Ending area: run completion and settlement.
- World structure: 4 large maps, each with 5 small stages.
- Each small stage has safe rooms at both ends. Leaving a safe room regenerates the stage and enemies.
- Enemies should respawn continuously inside a stage.
- Difficulty should use world hostility plus adaptive difficulty based on player strength and survival rate.
- Player operations are intentionally compact: movement, attack, up to 4 arcane abilities, up to 5 items.

### Decisions Made

- Keep Unity `.meta` files. They preserve GUIDs and prevent broken prefab/scene/resource references.
- For migration, keep only `Assets`, `Packages`, and `ProjectSettings`. Unity will regenerate `Library`, `Temp`, logs, solution files, and IDE files.
- Use a pure-English project path: `D:\DevFiles\TopDownActRogue`.
- Do not rely on Unity Event wiring for movement/attack. Scripts subscribe to `PlayerInput` actions directly, making input less fragile.
- Player movement is modeled as a small state machine: idle, move delay, moving, attack locked, disabled.
- Attack timing is handled by `PlayerCombatController`: cooldown, windup, hit resolve, recovery.
- Weapons are data-driven through `WeaponDefinition`, with `WeaponType` values for knife, sword, spear, and spell.
- A default knife is created at runtime if no weapon asset is assigned, so early prototypes remain playable.
- Damage targets implement `IDamageable`.

### Current Technical State

- `PlayerInputManager.cs` handles delayed movement and stops movement while attacking.
- `PlayerCombatController.cs` handles attack input, attack cooldown, windup/recovery, and overlap-circle hit detection.
- `PlayerInput.inputactions` includes `Move` and `Attack`; attack is bound to mouse left button and space.
- `WeaponDefinition.cs` is a ScriptableObject template for weapon configuration.
- `WeaponType.cs` defines the first four weapon types.
- `IDamageable.cs` defines the target damage contract.
- Project version currently says `2022.3.23f1c1`.

### Unity Problem Notes

- Unity reported `Failed to load Editor resource file`.
- Investigation showed C# script compilation and AssetDatabase refresh completed before the error.
- The error appeared during `Initializing Unity extensions` and also happened in a pure-English test project.
- This strongly suggests a Unity Editor/Hub/licensing/install issue, not a project-code issue.
- Logs also showed path mojibake for the old Chinese project path and licensing client validation noise.
- The user is reinstalling Unity and considering whether to use Unity China/Tuanjie. If using mainland China tooling, prefer a consistent toolchain: either Tuanjie Hub/Engine or international Unity, not mixed per project.

### Migration State

- Original cleaned project kept only:
  - `Assets`
  - `Packages`
  - `ProjectSettings`
- Migrated project now lives at:
  - `D:\DevFiles\TopDownActRogue`
- Migrated project contains:
  - `Assets`
  - `Packages`
  - `ProjectSettings`
  - this `SESSION_CONTEXT.md`

### Remaining Work

1. Reinstall Unity or Tuanjie, then open `D:\DevFiles\TopDownActRogue`.
2. Let Unity regenerate `Library`, `Temp`, `.sln`, and `.csproj` files.
3. If Unity opens successfully, inspect the player object in `SampleScene` and make sure it has `Rigidbody2D`, `PlayerInput`, and `PlayerInputManager`. `PlayerInputManager` will add `PlayerCombatController` at runtime if missing.
4. Press WASD to test delayed movement.
5. Press left mouse or space to test attack lockout and cooldown.
6. Create real `WeaponDefinition` assets for knife, sword, spear, and spell.
7. Add a test enemy implementing `IDamageable` to verify hit detection.
8. After control feel is validated, build the safe-room/stage flow prototype.

### Useful Next Prompt

When reopening this project in Codex, say:

`Read SESSION_CONTEXT.md and continue from the migrated Unity project. First verify Unity opens, then help me test the player movement and attack prototype.`

