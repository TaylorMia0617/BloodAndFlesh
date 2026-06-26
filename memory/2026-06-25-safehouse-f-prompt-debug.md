# SafeHouse F Prompt Debug Report

- Symptom: SafeHouse scene interaction prompts could show stale or incorrect focus while the player moved between nearby interactables, and prompts could remain visible behind modal UI.
- Root cause: `SafeRoomManager` only refreshed focus on trigger enter/exit and interact, not continuously while the player moved or turned inside overlapping trigger ranges. Modal UI also did not suppress world prompts.
- Fix: `SafeRoomManager` now refreshes focused interactables during update, supports prompt suppression while modal UI is open, and clears focus on safe-room enter/exit. `SafeRoomInteractable` now uses a smaller stable prompt bubble with camera-facing refresh.
- Follow-up fix: `SafeRoomManager.Instance` no longer creates a new manager during shutdown or edit-mode teardown, and callers now tolerate a missing manager. This addresses the Unity scene-close warning where `SafeRoomManager` could be recreated while objects were being destroyed.
- Prompt asset: `SafeRoomInteractable` now loads `Assets/Resources/Arts/UI/SafeHouse/ui_interact_prompt_f_comic.png` as the F prompt sprite instead of composing TextMesh and a generated background at runtime.
- Evidence: `dotnet build TopDownActRogue.sln --nologo` passes with 0 warnings and 0 errors.
- Status: DONE_WITH_CONCERNS. Manual Unity play-mode verification is still needed for exact prompt placement and UI alignment.
