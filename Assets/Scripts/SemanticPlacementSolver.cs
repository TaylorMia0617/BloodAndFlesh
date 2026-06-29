using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingKind : byte
{
    Safehouse,
    ExtractionRoom,
    SpawnDen,
    Ruin,
    SupplyCache,
    Shrine,
    AmbushHouse,
    WatchPost
}

public enum ResourceType : byte
{
    Medical,
    Currency,
    Material,
    RareCore,
    TaskItem,
    ExtractionUpgrade
}

[Serializable]
public sealed class BuildingDefinition
{
    public string Id = "building";
    public BuildingKind Kind;
    public Vector2Int Footprint = Vector2Int.one;
    public int MinClearance;
    public bool BlocksMainRoute;
    public RegionKind[] PreferredRegions = Array.Empty<RegionKind>();
    public float ThreatBias;
    public float LootBias;
}

public struct BuildingInstance
{
    public string DefinitionId;
    public BuildingKind Kind;
    public RectInt Footprint;
    public Vector2Int AnchorCell;
    public float Score;
}

public struct ResourceNode
{
    public ResourceType Type;
    public Vector2Int Cell;
    public int AmountMin;
    public int AmountMax;
    public string CatalogId;
    public string SourceBuildingId;
}

public sealed class SemanticPlacementSettings
{
    public int MaxSpawnDenBuildings = 6;
    public int MaxSupplyCaches = 4;
    public int MaxResourceNodes = 8;
    public int MinBuildingSpacing = 4;
    public int SafehouseFootprintRadius = 2;
}

public static class SemanticPlacementSolver
{
    public static void PopulateDefaultPlacements(MapData map, SemanticPlacementSettings settings = null)
    {
        if (map == null)
        {
            return;
        }

        settings ??= new SemanticPlacementSettings();
        map.Buildings.Clear();
        map.ResourceNodes.Clear();

        StampFixedBuilding(map, "start_safehouse", BuildingKind.Safehouse, map.StartCell, settings.SafehouseFootprintRadius);
        StampFixedBuilding(map, "extraction_room", BuildingKind.ExtractionRoom, map.EndCell, settings.SafehouseFootprintRadius);
        PlaceSpawnDenBuildings(map, settings);
        PlaceSupplyCaches(map, settings);
        PlaceResourceNodes(map, settings);
    }

    private static void StampFixedBuilding(MapData map, string id, BuildingKind kind, Vector2Int center, int radius)
    {
        RectInt footprint = RectAround(center, Mathf.Max(0, radius));
        map.Buildings.Add(new BuildingInstance
        {
            DefinitionId = id,
            Kind = kind,
            Footprint = footprint,
            AnchorCell = center,
            Score = 1f
        });

        MarkOccupied(map, footprint);
    }

    private static void PlaceSpawnDenBuildings(MapData map, SemanticPlacementSettings settings)
    {
        int count = Mathf.Min(Mathf.Max(0, settings.MaxSpawnDenBuildings), map.SpawnSockets.Count);
        for (int i = 0; i < count; i++)
        {
            Vector2Int socket = map.SpawnSockets[i];
            RectInt footprint = RectAround(socket, 1);
            if (!CanPlaceFootprint(map, footprint, false, 1))
            {
                continue;
            }

            MapCell cell = map.GetCell(socket);
            map.Buildings.Add(new BuildingInstance
            {
                DefinitionId = "spawn_den_socket",
                Kind = BuildingKind.SpawnDen,
                Footprint = footprint,
                AnchorCell = socket,
                Score = cell.Threat + 1f
            });
            MarkOccupied(map, footprint);
        }
    }

    private static void PlaceSupplyCaches(MapData map, SemanticPlacementSettings settings)
    {
        BuildingDefinition definition = new BuildingDefinition
        {
            Id = "supply_cache",
            Kind = BuildingKind.SupplyCache,
            Footprint = new Vector2Int(2, 2),
            MinClearance = 1,
            BlocksMainRoute = false,
            PreferredRegions = new[] { RegionKind.DeadEndLoot, RegionKind.BranchPocket },
            LootBias = 1.5f
        };

        List<Candidate> candidates = CollectCandidates(map, definition, settings.MinBuildingSpacing);
        int placed = 0;
        for (int i = 0; i < candidates.Count && placed < settings.MaxSupplyCaches; i++)
        {
            Candidate candidate = candidates[i];
            if (!CanPlaceFootprint(map, candidate.Footprint, definition.BlocksMainRoute, definition.MinClearance))
            {
                continue;
            }

            map.Buildings.Add(new BuildingInstance
            {
                DefinitionId = definition.Id,
                Kind = definition.Kind,
                Footprint = candidate.Footprint,
                AnchorCell = candidate.AnchorCell,
                Score = candidate.Score
            });
            MarkOccupied(map, candidate.Footprint);
            placed++;
        }
    }

    private static void PlaceResourceNodes(MapData map, SemanticPlacementSettings settings)
    {
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        int target = Mathf.Max(0, settings.MaxResourceNodes);

        for (int i = 0; i < map.Buildings.Count && map.ResourceNodes.Count < target; i++)
        {
            BuildingInstance building = map.Buildings[i];
            if (building.Kind != BuildingKind.SupplyCache)
            {
                continue;
            }

            Vector2Int cell = FindNearestResourceCell(map, building.AnchorCell, used);
            if (!map.IsInside(cell))
            {
                continue;
            }

            used.Add(cell);
            map.ResourceNodes.Add(SemanticResourceResolver.Normalize(map, new ResourceNode
            {
                Type = ResourceType.Material,
                Cell = cell,
                AmountMin = 1,
                AmountMax = 3,
                SourceBuildingId = building.DefinitionId
            }));
        }

        for (int i = 0; i < map.ResourceSockets.Count && map.ResourceNodes.Count < target; i++)
        {
            Vector2Int socket = map.ResourceSockets[i];
            if (used.Contains(socket))
            {
                continue;
            }

            used.Add(socket);
            MapCell cell = map.GetCell(socket);
            ResourceType type = cell.Threat >= 1.5f ? ResourceType.RareCore : ResourceType.Currency;
            map.ResourceNodes.Add(SemanticResourceResolver.Normalize(map, new ResourceNode
            {
                Type = type,
                Cell = socket,
                AmountMin = 1,
                AmountMax = type == ResourceType.Currency ? 12 : 2
            }));
        }
    }

    private static List<Candidate> CollectCandidates(MapData map, BuildingDefinition definition, int minSpacing)
    {
        List<Candidate> candidates = new List<Candidate>();
        Vector2Int footprint = new Vector2Int(Mathf.Max(1, definition.Footprint.x), Mathf.Max(1, definition.Footprint.y));
        for (int y = 0; y <= map.Height - footprint.y; y++)
        {
            for (int x = 0; x <= map.Width - footprint.x; x++)
            {
                RectInt rect = new RectInt(x, y, footprint.x, footprint.y);
                Vector2Int anchor = new Vector2Int(x + footprint.x / 2, y + footprint.y / 2);
                if (!CanPlaceFootprint(map, rect, definition.BlocksMainRoute, definition.MinClearance))
                {
                    continue;
                }

                if (DistanceToNearestBuilding(map, anchor) < minSpacing)
                {
                    continue;
                }

                float score = ScoreFootprint(map, rect, definition);
                if (score <= 0f)
                {
                    continue;
                }

                candidates.Add(new Candidate(rect, anchor, score));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        return candidates;
    }

    private static float ScoreFootprint(MapData map, RectInt footprint, BuildingDefinition definition)
    {
        float score = 0f;
        int cells = 0;
        for (int y = footprint.yMin; y < footprint.yMax; y++)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            {
                MapCell cell = map.GetCell(new Vector2Int(x, y));
                score += cell.LootWeight * Mathf.Max(0f, definition.LootBias);
                score += cell.Threat * Mathf.Max(0f, definition.ThreatBias);
                if (MatchesPreferredRegion(cell.RegionKind, definition.PreferredRegions))
                {
                    score += 2f;
                }

                cells++;
            }
        }

        return cells > 0 ? score / cells : 0f;
    }

    private static bool MatchesPreferredRegion(RegionKind region, RegionKind[] preferredRegions)
    {
        if (preferredRegions == null || preferredRegions.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < preferredRegions.Length; i++)
        {
            if (preferredRegions[i] == region)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanPlaceFootprint(MapData map, RectInt footprint, bool canBlockMainRoute, int clearance)
    {
        for (int y = footprint.yMin; y < footprint.yMax; y++)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                if (!map.IsInside(coord))
                {
                    return false;
                }

                MapCell cell = map.GetCell(coord);
                if (!cell.Flags.HasFlag(CellFlags.Walkable))
                {
                    return false;
                }

                if (cell.Flags.HasFlag(CellFlags.SafeZone) || cell.Flags.HasFlag(CellFlags.Occupied))
                {
                    return false;
                }

                if (!canBlockMainRoute && cell.RegionKind == RegionKind.MainRoute)
                {
                    return false;
                }
            }
        }

        RectInt expanded = Expand(footprint, Mathf.Max(0, clearance));
        for (int y = expanded.yMin; y < expanded.yMax; y++)
        {
            for (int x = expanded.xMin; x < expanded.xMax; x++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                if (!map.IsInside(coord))
                {
                    return false;
                }

                MapCell cell = map.GetCell(coord);
                if (cell.Flags.HasFlag(CellFlags.SafeZone) || cell.Flags.HasFlag(CellFlags.Occupied))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static Vector2Int FindNearestResourceCell(MapData map, Vector2Int origin, HashSet<Vector2Int> used)
    {
        Vector2Int best = new Vector2Int(-1, -1);
        float bestScore = float.MinValue;
        for (int i = 0; i < map.ResourceSockets.Count; i++)
        {
            Vector2Int socket = map.ResourceSockets[i];
            if (used.Contains(socket))
            {
                continue;
            }

            MapCell cell = map.GetCell(socket);
            float distance = Mathf.Max(1f, Vector2Int.Distance(origin, socket));
            float score = cell.LootWeight / distance;
            if (score > bestScore)
            {
                bestScore = score;
                best = socket;
            }
        }

        return best;
    }

    private static float DistanceToNearestBuilding(MapData map, Vector2Int anchor)
    {
        if (map.Buildings.Count == 0)
        {
            return float.MaxValue;
        }

        float best = float.MaxValue;
        for (int i = 0; i < map.Buildings.Count; i++)
        {
            float distance = Vector2Int.Distance(anchor, map.Buildings[i].AnchorCell);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    private static void MarkOccupied(MapData map, RectInt footprint)
    {
        for (int y = footprint.yMin; y < footprint.yMax; y++)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                if (!map.IsInside(coord))
                {
                    continue;
                }

                ref MapCell cell = ref map.Cell(x, y);
                cell.Flags |= CellFlags.Occupied;
            }
        }
    }

    private static RectInt RectAround(Vector2Int center, int radius)
    {
        int size = radius * 2 + 1;
        return new RectInt(center.x - radius, center.y - radius, size, size);
    }

    private static RectInt Expand(RectInt rect, int amount)
    {
        return new RectInt(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2, rect.height + amount * 2);
    }

    private readonly struct Candidate
    {
        public Candidate(RectInt footprint, Vector2Int anchorCell, float score)
        {
            Footprint = footprint;
            AnchorCell = anchorCell;
            Score = score;
        }

        public RectInt Footprint { get; }
        public Vector2Int AnchorCell { get; }
        public float Score { get; }
    }
}
