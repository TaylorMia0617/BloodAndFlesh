# 2026-06-26 Editor UI Error And Performance Notes

## Unity editor exception

Observed exception:

`ArgumentNullException: Value cannot be null. Parameter name: _unity_self`

The stack is entirely inside UnityEditor/UIElements:

- `UnityEditor.UIElements.Bindings.SerializedObjectList.get_Count`
- `UnityEngine.UIElements.DynamicHeightVirtualizationController`
- `UnityEngine.UIElements.BaseVerticalCollectionView`
- `UnityEngine.GUIUtility.ProcessEvent`

Project search did not find custom UIElements inspectors, custom `ListView`, or project code in the stack. The local editor log shows the exception after script/resource import and a Play-mode UI interaction. Most likely cause is a Unity Inspector or other editor panel retaining a stale `SerializedObject` binding across reload/import, then its virtualized list tries to count a null Unity object.

Recommended immediate handling:

- Close and reopen the Inspector or Project Settings panel that is visible when it happens.
- Deselect and reselect the asset/object.
- If it repeats, reset the editor layout or restart Unity.
- If it still reproduces, capture the selected object/window at the moment of the error; that is the missing context needed to identify the exact serialized list.

## Current performance picture

The screenshot shows low render complexity but high CPU frame time:

- 7.7 FPS, about 129.9 ms CPU main thread.
- 21 batches, about 3.9k tris and 3.5k verts.

This points more to scripting/object/physics/editor overhead than GPU drawing.

Confirmed hotspots:

- `Assets/Scripts/GridRouteMapGenerator.cs`
  - Generates one GameObject per grid cell.
  - A 100 x 80 map can create about 8,000 tile objects.
  - Wall cells each get a `BoxCollider2D` and `ObstacleHitFeedback`.
  - This is the biggest structural optimization candidate.

- `Assets/Scripts/PlayerVisionMask.cs`
  - Previously recalculated 192 visibility rays every rendered frame.
  - Each ray marched through the grid and uploaded a CPU-written texture every frame.
  - First optimization pass added interval/movement-threshold refresh so it no longer recalculates and uploads every frame.

## Optimization backlog

1. Replace per-cell map GameObjects with a Tilemap, chunked mesh, or combined sprite batches.
2. Replace per-wall colliders with TilemapCollider2D plus CompositeCollider2D, or generate merged rectangle colliders from wall runs.
3. Keep the vision mask throttled; later move visibility to a lower-resolution grid texture or shader-side map lookup if needed.
4. Pool enemies, attack effects, hit particles, and spawn markers.
5. Reduce runtime `FindObjectOfType` usage by passing references from setup code.
6. Add a lightweight in-game debug overlay for object count, collider count, active enemies, and vision refresh rate.

## Verification

`dotnet build TopDownActRogue.sln` passes with 0 warnings and 0 errors after the damage split and the first vision-mask optimization.
