using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BuildingVisualBinding
{
    public BuildingKind Kind;
    public Sprite Sprite;
    public GameObject Prefab;
    public float Scale = 1f;
    public int SortingOrderOffset;
}

[Serializable]
public sealed class ResourceVisualBinding
{
    public ResourceType Type;
    public Sprite Sprite;
    public GameObject Prefab;
    public float Scale = 0.55f;
    public int SortingOrderOffset;
}

[Serializable]
public sealed class ResidueVisualBinding
{
    public CombatResidueType Type;
    public Sprite Sprite;
    public GameObject Prefab;
    public float Scale = 1f;
    public int SortingOrderOffset;
}

[CreateAssetMenu(menuName = "TopDownActRogue/Semantic World Visual Catalog")]
public sealed class SemanticWorldVisualCatalog : ScriptableObject
{
    [Header("Buildings")]
    [SerializeField] private BuildingVisualBinding[] buildings = Array.Empty<BuildingVisualBinding>();

    [Header("Resources")]
    [SerializeField] private ResourceVisualBinding[] resources = Array.Empty<ResourceVisualBinding>();

    [Header("Combat Residues")]
    [SerializeField] private ResidueVisualBinding[] residues = Array.Empty<ResidueVisualBinding>();

    [Header("Tiles")]
    [SerializeField] private Sprite groundTileSprite;
    [SerializeField] private Sprite routeTileSprite;
    [SerializeField] private Sprite wallTileSprite;
    [SerializeField] private Sprite safehouseTileSprite;

    public Sprite GroundTileSprite => groundTileSprite;
    public Sprite RouteTileSprite => routeTileSprite;
    public Sprite WallTileSprite => wallTileSprite;
    public Sprite SafehouseTileSprite => safehouseTileSprite;

    public bool ValidateRequiredBindings(List<string> failures)
    {
        failures ??= new List<string>();
        int before = failures.Count;
        RequireBuilding(BuildingKind.Safehouse, failures);
        RequireBuilding(BuildingKind.ExtractionRoom, failures);
        RequireBuilding(BuildingKind.SpawnDen, failures);
        RequireBuilding(BuildingKind.SupplyCache, failures);
        RequireResource(ResourceType.Currency, failures);
        RequireResource(ResourceType.Material, failures);
        RequireResource(ResourceType.RareCore, failures);
        RequireResidue(CombatResidueType.BloodDrop, failures);
        RequireResidue(CombatResidueType.BloodPool, failures);
        RequireResidue(CombatResidueType.Corpse, failures);
        RequireResidue(CombatResidueType.Scorch, failures);
        RequireTile(groundTileSprite, "ground tile", failures);
        RequireTile(routeTileSprite, "route tile", failures);
        RequireTile(wallTileSprite, "wall tile", failures);
        RequireTile(safehouseTileSprite, "safehouse tile", failures);
        return failures.Count == before;
    }

    public void Configure(
        BuildingVisualBinding[] buildingBindings,
        ResourceVisualBinding[] resourceBindings,
        ResidueVisualBinding[] residueBindings,
        Sprite groundTile,
        Sprite routeTile,
        Sprite wallTile,
        Sprite safehouseTile)
    {
        buildings = buildingBindings ?? Array.Empty<BuildingVisualBinding>();
        resources = resourceBindings ?? Array.Empty<ResourceVisualBinding>();
        residues = residueBindings ?? Array.Empty<ResidueVisualBinding>();
        groundTileSprite = groundTile;
        routeTileSprite = routeTile;
        wallTileSprite = wallTile;
        safehouseTileSprite = safehouseTile;
    }

    public bool TryGetBuilding(BuildingKind kind, out BuildingVisualBinding binding)
    {
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] != null && buildings[i].Kind == kind)
            {
                binding = buildings[i];
                return true;
            }
        }

        binding = null;
        return false;
    }

    public bool TryGetResource(ResourceType type, out ResourceVisualBinding binding)
    {
        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] != null && resources[i].Type == type)
            {
                binding = resources[i];
                return true;
            }
        }

        binding = null;
        return false;
    }

    public bool TryGetResidue(CombatResidueType type, out ResidueVisualBinding binding)
    {
        for (int i = 0; i < residues.Length; i++)
        {
            if (residues[i] != null && residues[i].Type == type)
            {
                binding = residues[i];
                return true;
            }
        }

        binding = null;
        return false;
    }

    private void RequireBuilding(BuildingKind kind, List<string> failures)
    {
        if (!TryGetBuilding(kind, out BuildingVisualBinding binding) || (binding.Sprite == null && binding.Prefab == null))
        {
            failures.Add($"missing building visual: {kind}");
        }
    }

    private void RequireResource(ResourceType type, List<string> failures)
    {
        if (!TryGetResource(type, out ResourceVisualBinding binding) || (binding.Sprite == null && binding.Prefab == null))
        {
            failures.Add($"missing resource visual: {type}");
        }
    }

    private void RequireResidue(CombatResidueType type, List<string> failures)
    {
        if (!TryGetResidue(type, out ResidueVisualBinding binding) || (binding.Sprite == null && binding.Prefab == null))
        {
            failures.Add($"missing residue visual: {type}");
        }
    }

    private static void RequireTile(Sprite sprite, string label, List<string> failures)
    {
        if (sprite == null)
        {
            failures.Add($"missing {label} visual");
        }
    }
}
