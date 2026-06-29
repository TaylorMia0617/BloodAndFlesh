using System.Collections.Generic;
using UnityEngine;

public sealed class SemanticMapValidationReport
{
    private readonly List<string> failures = new List<string>();

    public IReadOnlyList<string> Failures => failures;
    public bool Passed => failures.Count == 0;
    public bool IsValid => Passed;

    public void AddFailure(string failure)
    {
        if (!string.IsNullOrEmpty(failure))
        {
            failures.Add(failure);
        }
    }
}

public sealed class SemanticMapValidationBatchReport
{
    private readonly List<string> failures = new List<string>();

    public int MapCount { get; private set; }
    public float TotalGenerationMilliseconds { get; private set; }
    public float MaxGenerationMilliseconds { get; private set; }
    public int SlowestSeed { get; private set; }
    public IReadOnlyList<string> Failures => failures;
    public bool Passed => failures.Count == 0;
    public float AverageGenerationMilliseconds => MapCount > 0 ? TotalGenerationMilliseconds / MapCount : 0f;

    public void AddMapResult(int seed, SemanticMapValidationReport report, float generationMilliseconds)
    {
        AddMapResult(seed, report, generationMilliseconds, default, float.PositiveInfinity);
    }

    public void AddMapResult(int seed, SemanticMapValidationReport report, float generationMilliseconds, SemanticWorldBudgetReport budgetReport, float maxGenerationMilliseconds)
    {
        MapCount++;
        float safeGenerationMilliseconds = Mathf.Max(0f, generationMilliseconds);
        TotalGenerationMilliseconds += safeGenerationMilliseconds;
        if (safeGenerationMilliseconds >= MaxGenerationMilliseconds)
        {
            MaxGenerationMilliseconds = safeGenerationMilliseconds;
            SlowestSeed = seed;
        }

        if (safeGenerationMilliseconds > maxGenerationMilliseconds)
        {
            failures.Add($"seed {seed}: generation budget exceeded: {safeGenerationMilliseconds:0.00}/{maxGenerationMilliseconds:0.00} ms");
        }

        if (report == null)
        {
            failures.Add($"seed {seed}: missing validation report");
        }
        else
        {
            for (int i = 0; i < report.Failures.Count; i++)
            {
                failures.Add($"seed {seed}: {report.Failures[i]}");
            }
        }

        if (!string.IsNullOrEmpty(budgetReport.Message) && !budgetReport.Passed)
        {
            failures.Add($"seed {seed}: {budgetReport.Message}");
        }
    }
}

public static class SemanticMapValidator
{
    public static SemanticMapValidationReport Validate(MapData map)
    {
        SemanticMapValidationReport report = new SemanticMapValidationReport();
        if (map == null)
        {
            report.AddFailure("map is null");
            return report;
        }

        ValidateRoute(map, report);
        ValidateBuildings(map, report);
        ValidateResourceNodes(map, report);
        ValidateSpawnSockets(map, report);
        return report;
    }

    private static void ValidateRoute(MapData map, SemanticMapValidationReport report)
    {
        if (!map.IsInside(map.StartCell))
        {
            report.AddFailure("start cell is outside map");
        }

        if (!map.IsInside(map.EndCell))
        {
            report.AddFailure("end cell is outside map");
        }

        if (map.MainRoute.Count == 0)
        {
            report.AddFailure("main route is empty");
            return;
        }

        if (map.MainRoute[0] != map.StartCell)
        {
            report.AddFailure("main route does not start at start cell");
        }

        if (map.MainRoute[map.MainRoute.Count - 1] != map.EndCell)
        {
            report.AddFailure("main route does not end at extraction cell");
        }

        for (int i = 0; i < map.MainRoute.Count; i++)
        {
            Vector2Int cell = map.MainRoute[i];
            if (!map.IsInside(cell))
            {
                report.AddFailure($"main route cell outside map: {cell}");
                continue;
            }

            if (!map.GetCell(cell).Flags.HasFlag(CellFlags.Walkable))
            {
                report.AddFailure($"main route cell is not walkable: {cell}");
            }

            if (i > 0 && ManhattanDistance(map.MainRoute[i - 1], cell) != 1)
            {
                report.AddFailure($"main route has a gap between {map.MainRoute[i - 1]} and {cell}");
            }
        }
    }

    private static void ValidateBuildings(MapData map, SemanticMapValidationReport report)
    {
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        for (int i = 0; i < map.Buildings.Count; i++)
        {
            BuildingInstance building = map.Buildings[i];
            if (building.Footprint.width <= 0 || building.Footprint.height <= 0)
            {
                report.AddFailure($"building has invalid footprint: {building.DefinitionId}");
                continue;
            }

            for (int y = building.Footprint.yMin; y < building.Footprint.yMax; y++)
            {
                for (int x = building.Footprint.xMin; x < building.Footprint.xMax; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!map.IsInside(cell))
                    {
                        report.AddFailure($"building footprint outside map: {building.DefinitionId}");
                        continue;
                    }

                    if (!IsSafehouseLike(building.Kind) && !map.GetCell(cell).Flags.HasFlag(CellFlags.Walkable))
                    {
                        report.AddFailure($"building footprint touches non-walkable cell: {building.DefinitionId}");
                    }

                    if (!occupied.Add(cell))
                    {
                        report.AddFailure($"building footprints overlap at {cell}");
                    }
                }
            }

            if (IsSafehouseLike(building.Kind) && (!map.IsInside(building.AnchorCell) || !map.GetCell(building.AnchorCell).Flags.HasFlag(CellFlags.Walkable)))
            {
                report.AddFailure($"safehouse-like building anchor is not walkable: {building.DefinitionId}");
            }
        }
    }

    private static void ValidateResourceNodes(MapData map, SemanticMapValidationReport report)
    {
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        for (int i = 0; i < map.ResourceNodes.Count; i++)
        {
            ResourceNode node = map.ResourceNodes[i];
            if (!map.IsInside(node.Cell))
            {
                report.AddFailure($"resource node outside map: {node.CatalogId}");
                continue;
            }

            MapCell cell = map.GetCell(node.Cell);
            if (!cell.Flags.HasFlag(CellFlags.Walkable))
            {
                report.AddFailure($"resource node is not walkable: {node.CatalogId}");
            }

            if (cell.Flags.HasFlag(CellFlags.SafeZone))
            {
                report.AddFailure($"resource node is inside safe zone: {node.CatalogId}");
            }

            if (string.IsNullOrEmpty(node.CatalogId))
            {
                report.AddFailure("resource node has no catalog id");
            }

            if (!used.Add(node.Cell))
            {
                report.AddFailure($"duplicate resource node cell: {node.Cell}");
            }
        }
    }

    private static void ValidateSpawnSockets(MapData map, SemanticMapValidationReport report)
    {
        for (int i = 0; i < map.SpawnSockets.Count; i++)
        {
            Vector2Int socket = map.SpawnSockets[i];
            if (!map.IsInside(socket))
            {
                report.AddFailure($"spawn socket outside map: {socket}");
                continue;
            }

            MapCell cell = map.GetCell(socket);
            if (!cell.Flags.HasFlag(CellFlags.Walkable))
            {
                report.AddFailure($"spawn socket is not walkable: {socket}");
            }

            if (cell.Flags.HasFlag(CellFlags.DirectorSpawnDeny))
            {
                report.AddFailure($"spawn socket is inside director deny zone: {socket}");
            }
        }
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static bool IsSafehouseLike(BuildingKind kind)
    {
        return kind == BuildingKind.Safehouse || kind == BuildingKind.ExtractionRoom;
    }
}
