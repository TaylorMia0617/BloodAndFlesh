using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum CellFlags : ushort
{
    None = 0,
    Walkable = 1 << 0,
    BlocksMovement = 1 << 1,
    BlocksVision = 1 << 2,
    BlocksProjectiles = 1 << 3,
    SafeZone = 1 << 4,
    ExtractionZone = 1 << 5,
    DirectorSpawnDeny = 1 << 6,
    ResourceCandidate = 1 << 7,
    SpawnCandidate = 1 << 8,
    GrassEligible = 1 << 9,
    DebrisEligible = 1 << 10,
    BloodEligible = 1 << 11,
    Occupied = 1 << 12
}

public enum TileType : byte
{
    Void,
    Ground,
    Road,
    RoomFloor,
    SafeFloor,
    SafeDoor,
    Wall,
    LowCover,
    HighCover,
    Hazard
}

public enum RegionKind : byte
{
    Unknown,
    StartSafehouse,
    EndSafehouse,
    MainRoute,
    BranchPocket,
    DeadEndLoot,
    ExtractionApproach,
    AmbushPocket,
    DenTerritory,
    OpenField
}

public struct MapCell
{
    public Vector2Int Coord;
    public TileType TileType;
    public RegionKind RegionKind;
    public CellFlags Flags;
    public short RegionId;
    public float Threat;
    public float LootWeight;
    public float VegetationWeight;
}

public sealed class MapData
{
    private readonly HashSet<Vector2Int> spawnCandidateSet = new HashSet<Vector2Int>();

    public MapData(int width, int height, float cellSize, int seed)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        CellSize = Mathf.Max(0.01f, cellSize);
        Seed = seed;
        Cells = new MapCell[Width * Height];
        MainRoute = new List<Vector2Int>();
        SpawnSockets = new List<Vector2Int>();
        ResourceSockets = new List<Vector2Int>();
        Buildings = new List<BuildingInstance>();
        ResourceNodes = new List<ResourceNode>();
    }

    public int Width { get; }
    public int Height { get; }
    public float CellSize { get; }
    public int Seed { get; }
    public MapCell[] Cells { get; }
    public List<Vector2Int> MainRoute { get; }
    public List<Vector2Int> SpawnSockets { get; }
    public List<Vector2Int> ResourceSockets { get; }
    public List<BuildingInstance> Buildings { get; }
    public List<ResourceNode> ResourceNodes { get; }
    public Vector2Int StartCell { get; set; }
    public Vector2Int EndCell { get; set; }

    public bool IsInside(Vector2Int coord)
    {
        return coord.x >= 0 && coord.y >= 0 && coord.x < Width && coord.y < Height;
    }

    public int IndexOf(Vector2Int coord)
    {
        return coord.y * Width + coord.x;
    }

    public ref MapCell Cell(int x, int y)
    {
        return ref Cells[y * Width + x];
    }

    public MapCell GetCell(Vector2Int coord)
    {
        return IsInside(coord) ? Cells[IndexOf(coord)] : default;
    }

    public bool IsSpawnCandidate(Vector2Int coord)
    {
        return spawnCandidateSet.Contains(coord);
    }

    public void AddSpawnSocket(Vector2Int coord)
    {
        if (!IsInside(coord) || spawnCandidateSet.Contains(coord))
        {
            return;
        }

        spawnCandidateSet.Add(coord);
        SpawnSockets.Add(coord);
    }
}

public static class SemanticMapBuilder
{
    public static MapData BuildFromRoute(
        int width,
        int height,
        float cellSize,
        bool[,] walkable,
        IReadOnlyList<Vector2Int> mainRoute,
        Vector2Int startCell,
        Vector2Int endCell,
        IReadOnlyList<Vector2Int> spawnCandidates,
        float baseHostility,
        int seed = 0)
    {
        MapData map = new MapData(width, height, cellSize, seed)
        {
            StartCell = startCell,
            EndCell = endCell
        };

        HashSet<Vector2Int> routeSet = new HashSet<Vector2Int>();
        if (mainRoute != null)
        {
            for (int i = 0; i < mainRoute.Count; i++)
            {
                Vector2Int coord = mainRoute[i];
                if (map.IsInside(coord) && routeSet.Add(coord))
                {
                    map.MainRoute.Add(coord);
                }
            }
        }

        HashSet<Vector2Int> spawnSet = new HashSet<Vector2Int>();
        if (spawnCandidates != null)
        {
            for (int i = 0; i < spawnCandidates.Count; i++)
            {
                Vector2Int coord = spawnCandidates[i];
                if (map.IsInside(coord) && spawnSet.Add(coord))
                {
                    map.AddSpawnSocket(coord);
                }
            }
        }

        float maxRouteDistance = Mathf.Max(1f, width + height);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                bool isWalkable = IsWalkable(walkable, coord);
                bool isStart = coord == startCell;
                bool isEnd = coord == endCell;
                bool isRoute = routeSet.Contains(coord);
                bool isSpawnCandidate = spawnSet.Contains(coord);

                CellFlags flags = CellFlags.None;
                TileType tileType = isWalkable ? TileType.Road : TileType.Wall;
                RegionKind regionKind = isWalkable ? RegionKind.OpenField : RegionKind.Unknown;

                if (isWalkable)
                {
                    flags |= CellFlags.Walkable | CellFlags.BloodEligible;
                    if (TouchesWall(walkable, coord))
                    {
                        flags |= CellFlags.DebrisEligible;
                    }

                    float routeDistance = DistanceToNearestRoute(coord, map.MainRoute, maxRouteDistance);
                    float normalizedRouteDistance = Mathf.Clamp01(routeDistance / maxRouteDistance);
                    if (isRoute)
                    {
                        regionKind = RegionKind.MainRoute;
                    }
                    else if (HasSingleWalkableExit(walkable, coord))
                    {
                        regionKind = RegionKind.DeadEndLoot;
                        flags |= CellFlags.ResourceCandidate;
                        map.ResourceSockets.Add(coord);
                    }
                    else if (TouchesWall(walkable, coord))
                    {
                        regionKind = RegionKind.BranchPocket;
                    }

                    if (isSpawnCandidate)
                    {
                        flags |= CellFlags.SpawnCandidate;
                        regionKind = RegionKind.DenTerritory;
                    }

                    if (isStart)
                    {
                        tileType = TileType.SafeFloor;
                        regionKind = RegionKind.StartSafehouse;
                        flags |= CellFlags.SafeZone | CellFlags.DirectorSpawnDeny;
                    }
                    else if (isEnd)
                    {
                        tileType = TileType.SafeFloor;
                        regionKind = RegionKind.EndSafehouse;
                        flags |= CellFlags.SafeZone | CellFlags.ExtractionZone | CellFlags.DirectorSpawnDeny;
                    }

                    float safeDistance = Mathf.Min(Vector2Int.Distance(coord, startCell), Vector2Int.Distance(coord, endCell));
                    if (safeDistance <= 4f)
                    {
                        flags |= CellFlags.DirectorSpawnDeny;
                    }

                    if (safeDistance > 5f && !isRoute)
                    {
                        flags |= CellFlags.GrassEligible;
                    }

                    map.Cell(x, y) = new MapCell
                    {
                        Coord = coord,
                        TileType = tileType,
                        RegionKind = regionKind,
                        Flags = flags,
                        RegionId = (short)regionKind,
                        Threat = Mathf.Max(0f, baseHostility) * (0.65f + normalizedRouteDistance),
                        LootWeight = flags.HasFlag(CellFlags.ResourceCandidate) ? 1f : Mathf.Clamp01(normalizedRouteDistance),
                        VegetationWeight = flags.HasFlag(CellFlags.GrassEligible) ? Mathf.Clamp01(0.35f + normalizedRouteDistance) : 0f
                    };
                }
                else
                {
                    flags |= CellFlags.BlocksMovement | CellFlags.BlocksVision | CellFlags.BlocksProjectiles;
                    map.Cell(x, y) = new MapCell
                    {
                        Coord = coord,
                        TileType = tileType,
                        RegionKind = regionKind,
                        Flags = flags,
                        RegionId = -1
                    };
                }
            }
        }

        return map;
    }

    private static bool IsWalkable(bool[,] walkable, Vector2Int coord)
    {
        return walkable != null
            && coord.x >= 0
            && coord.y >= 0
            && coord.x < walkable.GetLength(0)
            && coord.y < walkable.GetLength(1)
            && walkable[coord.x, coord.y];
    }

    private static bool TouchesWall(bool[,] walkable, Vector2Int coord)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                if (!IsWalkable(walkable, new Vector2Int(coord.x + x, coord.y + y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasSingleWalkableExit(bool[,] walkable, Vector2Int coord)
    {
        int exits = 0;
        if (IsWalkable(walkable, coord + Vector2Int.up)) exits++;
        if (IsWalkable(walkable, coord + Vector2Int.down)) exits++;
        if (IsWalkable(walkable, coord + Vector2Int.left)) exits++;
        if (IsWalkable(walkable, coord + Vector2Int.right)) exits++;
        return exits <= 1;
    }

    private static float DistanceToNearestRoute(Vector2Int coord, IReadOnlyList<Vector2Int> route, float fallback)
    {
        if (route == null || route.Count == 0)
        {
            return fallback;
        }

        float best = fallback;
        for (int i = 0; i < route.Count; i++)
        {
            float distance = Vector2Int.Distance(coord, route[i]);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }
}
