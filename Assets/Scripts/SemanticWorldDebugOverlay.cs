using UnityEngine;

public readonly struct SemanticWorldMetrics
{
    public readonly int Width;
    public readonly int Height;
    public readonly int WalkableCells;
    public readonly int MainRouteCells;
    public readonly int BuildingCount;
    public readonly int ResourceNodeCount;
    public readonly int ResidueCount;
    public readonly int PooledResidueObjectCount;
    public readonly int VisibleCellCount;
    public readonly int ExploredCellCount;
    public readonly int LivingEnemyCount;
    public readonly float RawHostility;
    public readonly float SearchIntensity;

    public SemanticWorldMetrics(
        int width,
        int height,
        int walkableCells,
        int mainRouteCells,
        int buildingCount,
        int resourceNodeCount,
        int residueCount,
        int pooledResidueObjectCount,
        int visibleCellCount,
        int exploredCellCount,
        int livingEnemyCount,
        float rawHostility,
        float searchIntensity)
    {
        Width = width;
        Height = height;
        WalkableCells = walkableCells;
        MainRouteCells = mainRouteCells;
        BuildingCount = buildingCount;
        ResourceNodeCount = resourceNodeCount;
        ResidueCount = residueCount;
        PooledResidueObjectCount = Mathf.Max(0, pooledResidueObjectCount);
        VisibleCellCount = Mathf.Max(0, visibleCellCount);
        ExploredCellCount = Mathf.Max(0, exploredCellCount);
        LivingEnemyCount = Mathf.Max(0, livingEnemyCount);
        RawHostility = Mathf.Max(0f, rawHostility);
        SearchIntensity = Mathf.Max(0f, searchIntensity);
    }
}

public static class SemanticWorldTelemetry
{
    public static SemanticWorldMetrics Capture(MapData map, AftermathGrid aftermath, WorldHostilityDirector director, int livingEnemyCount, SemanticWorldView view = null, VisionRevealMap revealMap = null)
    {
        int walkable = 0;
        if (map != null)
        {
            for (int i = 0; i < map.Cells.Length; i++)
            {
                if (map.Cells[i].Flags.HasFlag(CellFlags.Walkable))
                {
                    walkable++;
                }
            }
        }

        DirectorSnapshot snapshot = director != null ? director.Snapshot : default;
        return new SemanticWorldMetrics(
            map != null ? map.Width : 0,
            map != null ? map.Height : 0,
            walkable,
            map != null ? map.MainRoute.Count : 0,
            map != null ? map.Buildings.Count : 0,
            map != null ? map.ResourceNodes.Count : 0,
            aftermath != null ? aftermath.Residues.Count : 0,
            view != null ? view.PooledResidueObjectCount : 0,
            revealMap != null ? revealMap.VisibleCount : 0,
            revealMap != null ? revealMap.ExploredCount : 0,
            Mathf.Max(0, livingEnemyCount),
            director != null ? director.RawHostility : 0f,
            snapshot.SearchIntensity);
    }
}

public readonly struct SemanticWorldBudgets
{
    public readonly int MaxBuildings;
    public readonly int MaxResourceNodes;
    public readonly int MaxResidues;
    public readonly int MaxPooledResidues;
    public readonly int MaxLivingEnemies;
    public readonly float MaxRawHostility;

    public SemanticWorldBudgets(int maxBuildings, int maxResourceNodes, int maxResidues, int maxPooledResidues, int maxLivingEnemies, float maxRawHostility)
    {
        MaxBuildings = Mathf.Max(0, maxBuildings);
        MaxResourceNodes = Mathf.Max(0, maxResourceNodes);
        MaxResidues = Mathf.Max(0, maxResidues);
        MaxPooledResidues = Mathf.Max(0, maxPooledResidues);
        MaxLivingEnemies = Mathf.Max(0, maxLivingEnemies);
        MaxRawHostility = Mathf.Max(0f, maxRawHostility);
    }

    public static SemanticWorldBudgets Default => new SemanticWorldBudgets(48, 32, 256, 128, 80, 12f);

    public static SemanticWorldBudgets FromStage(StageConfig stageConfig, SemanticWorldBudgets fallback)
    {
        if (stageConfig == null)
        {
            return fallback;
        }

        return new SemanticWorldBudgets(
            ResolveBudget(stageConfig.semanticMaxBuildings, fallback.MaxBuildings),
            ResolveBudget(stageConfig.semanticResourceNodeCount, fallback.MaxResourceNodes),
            ResolveBudget(stageConfig.semanticMaxResidues, fallback.MaxResidues),
            ResolveBudget(stageConfig.semanticMaxPooledResidues, fallback.MaxPooledResidues),
            ResolveBudget(stageConfig.semanticMaxLivingEnemies, fallback.MaxLivingEnemies),
            stageConfig.semanticMaxRawHostility >= 0f ? stageConfig.semanticMaxRawHostility : fallback.MaxRawHostility);
    }

    private static int ResolveBudget(int configuredValue, int fallback)
    {
        return configuredValue >= 0 ? configuredValue : fallback;
    }
}

public readonly struct SemanticWorldBudgetReport
{
    public readonly bool Passed;
    public readonly string Message;

    public SemanticWorldBudgetReport(bool passed, string message)
    {
        Passed = passed;
        Message = message ?? string.Empty;
    }
}

public static class SemanticWorldBudgetChecker
{
    public static SemanticWorldBudgetReport Check(SemanticWorldMetrics metrics, SemanticWorldBudgets budgets)
    {
        if (metrics.BuildingCount > budgets.MaxBuildings)
        {
            return Fail("building", metrics.BuildingCount, budgets.MaxBuildings);
        }

        if (metrics.ResourceNodeCount > budgets.MaxResourceNodes)
        {
            return Fail("resource", metrics.ResourceNodeCount, budgets.MaxResourceNodes);
        }

        if (metrics.ResidueCount > budgets.MaxResidues)
        {
            return Fail("residue", metrics.ResidueCount, budgets.MaxResidues);
        }

        if (metrics.PooledResidueObjectCount > budgets.MaxPooledResidues)
        {
            return Fail("pooled residue", metrics.PooledResidueObjectCount, budgets.MaxPooledResidues);
        }

        if (metrics.LivingEnemyCount > budgets.MaxLivingEnemies)
        {
            return Fail("living enemy", metrics.LivingEnemyCount, budgets.MaxLivingEnemies);
        }

        if (metrics.RawHostility > budgets.MaxRawHostility)
        {
            return new SemanticWorldBudgetReport(false, $"hostility budget exceeded: {metrics.RawHostility:0.00}/{budgets.MaxRawHostility:0.00}");
        }

        return new SemanticWorldBudgetReport(true, "semantic world budgets ok");
    }

    private static SemanticWorldBudgetReport Fail(string label, int actual, int budget)
    {
        return new SemanticWorldBudgetReport(false, $"{label} budget exceeded: {actual}/{budget}");
    }
}

public sealed class SemanticWorldDebugOverlay : MonoBehaviour
{
    [SerializeField] private GridRouteMapGenerator mapGenerator;
    [SerializeField] private bool drawWalkableCells;
    [SerializeField] private bool drawMainRoute = true;
    [SerializeField] private bool drawBuildings = true;
    [SerializeField] private bool drawResources = true;
    [SerializeField] private bool drawResidues = true;
    [SerializeField] private bool drawLocalPressure;
    [SerializeField] private bool showRuntimeHud;
    [SerializeField] private float cellGizmoSize = 0.28f;

    public SemanticWorldMetrics LastMetrics { get; private set; }
    public SemanticWorldBudgetReport LastBudgetReport { get; private set; }
    public SemanticMapValidationReport LastValidationReport { get; private set; }

    public void Configure(GridRouteMapGenerator generator)
    {
        mapGenerator = generator;
        RefreshMetrics();
    }

    public SemanticWorldMetrics RefreshMetrics()
    {
        MapData map = ResolveMap();
        CombatAftermathSystem aftermathSystem = CombatAftermathSystem.Existing;
        AftermathGrid aftermath = aftermathSystem != null ? aftermathSystem.Grid : null;
        SemanticWorldView view = GetComponent<SemanticWorldView>();
        PlayerVisionMask visionMask = Camera.main != null ? Camera.main.GetComponent<PlayerVisionMask>() : null;
        LastMetrics = SemanticWorldTelemetry.Capture(map, aftermath, WorldHostilityDirector.Current, EnemyRegistry.LivingCount, view, visionMask != null ? visionMask.RevealMap : null);
        SemanticWorldBudgets budgets = SemanticWorldBudgets.FromStage(mapGenerator != null ? mapGenerator.CurrentStageConfig : null, SemanticWorldBudgets.Default);
        LastBudgetReport = SemanticWorldBudgetChecker.Check(LastMetrics, budgets);
        LastValidationReport = SemanticMapValidator.Validate(map);
        return LastMetrics;
    }

    private void OnGUI()
    {
        if (!showRuntimeHud)
        {
            return;
        }

        SemanticWorldMetrics metrics = RefreshMetrics();
        Color previousColor = GUI.color;
        GUI.color = LastBudgetReport.Passed ? new Color(0.7f, 1f, 0.75f, 0.95f) : new Color(1f, 0.55f, 0.45f, 0.95f);

        Rect panel = new Rect(12f, 12f, 330f, 136f);
        GUI.Box(panel, string.Empty);
        string headline = LastValidationReport != null && !LastValidationReport.Passed
            ? LastValidationReport.Failures[0]
            : LastBudgetReport.Message;
        GUI.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 22f), headline);
        GUI.color = previousColor;
        GUI.Label(new Rect(panel.x + 10f, panel.y + 34f, panel.width - 20f, 22f), $"map {metrics.Width}x{metrics.Height} walkable {metrics.WalkableCells} route {metrics.MainRouteCells}");
        GUI.Label(new Rect(panel.x + 10f, panel.y + 56f, panel.width - 20f, 22f), $"buildings {metrics.BuildingCount} resources {metrics.ResourceNodeCount} residues {metrics.ResidueCount}");
        GUI.Label(new Rect(panel.x + 10f, panel.y + 78f, panel.width - 20f, 22f), $"pooled {metrics.PooledResidueObjectCount} enemies {metrics.LivingEnemyCount} hostility {metrics.RawHostility:0.00}");
        GUI.Label(new Rect(panel.x + 10f, panel.y + 100f, panel.width - 20f, 18f), $"vision {metrics.VisibleCellCount} explored {metrics.ExploredCellCount}");
        GUI.Label(new Rect(panel.x + 10f, panel.y + 118f, panel.width - 20f, 18f), $"search {metrics.SearchIntensity:0.00}");
    }

    private void OnDrawGizmosSelected()
    {
        MapData map = ResolveMap();
        if (map == null)
        {
            return;
        }

        float size = Mathf.Max(0.04f, cellGizmoSize);
        if (drawWalkableCells)
        {
            DrawCells(map, CellFlags.Walkable, new Color(0.2f, 0.75f, 0.35f, 0.18f), size);
        }

        if (drawMainRoute)
        {
            DrawMainRoute(map, new Color(0.15f, 0.55f, 1f, 0.65f), size * 1.35f);
        }

        if (drawBuildings)
        {
            DrawBuildings(map);
        }

        if (drawResources)
        {
            DrawResources(map, new Color(1f, 0.85f, 0.15f, 0.9f), size * 1.6f);
        }

        if (drawResidues)
        {
            DrawResidues();
        }

        if (drawLocalPressure)
        {
            DrawPressureSamples(map, size * 1.8f);
        }
    }

    private MapData ResolveMap()
    {
        if (mapGenerator == null)
        {
            mapGenerator = GetComponent<GridRouteMapGenerator>();
        }

        return mapGenerator != null ? mapGenerator.CurrentMapData : null;
    }

    private static void DrawCells(MapData map, CellFlags flags, Color color, float size)
    {
        Gizmos.color = color;
        for (int i = 0; i < map.Cells.Length; i++)
        {
            MapCell cell = map.Cells[i];
            if (cell.Flags.HasFlag(flags))
            {
                Gizmos.DrawCube(CellToWorld(map, cell.Coord), Vector3.one * size);
            }
        }
    }

    private static void DrawMainRoute(MapData map, Color color, float size)
    {
        Gizmos.color = color;
        for (int i = 0; i < map.MainRoute.Count; i++)
        {
            Gizmos.DrawCube(CellToWorld(map, map.MainRoute[i]), Vector3.one * size);
        }
    }

    private static void DrawBuildings(MapData map)
    {
        for (int i = 0; i < map.Buildings.Count; i++)
        {
            BuildingInstance building = map.Buildings[i];
            Gizmos.color = BuildingColor(building.Kind);
            Vector3 center = CellToWorld(map, building.AnchorCell);
            Vector3 size = new Vector3(
                Mathf.Max(0.25f, building.Footprint.width * map.CellSize),
                Mathf.Max(0.25f, building.Footprint.height * map.CellSize),
                0.08f);
            Gizmos.DrawWireCube(center, size);
        }
    }

    private static void DrawResources(MapData map, Color color, float size)
    {
        Gizmos.color = color;
        for (int i = 0; i < map.ResourceNodes.Count; i++)
        {
            Gizmos.DrawSphere(CellToWorld(map, map.ResourceNodes[i].Cell), size);
        }
    }

    private static void DrawResidues()
    {
        CombatAftermathSystem aftermathSystem = CombatAftermathSystem.Existing;
        AftermathGrid grid = aftermathSystem != null ? aftermathSystem.Grid : null;
        if (grid == null)
        {
            return;
        }

        for (int i = 0; i < grid.Residues.Count; i++)
        {
            CombatResidue residue = grid.Residues[i];
            Gizmos.color = ResidueColor(residue.Type);
            Gizmos.DrawSphere(residue.WorldPosition, Mathf.Max(0.04f, residue.Radius * 0.25f));
        }
    }

    private static void DrawPressureSamples(MapData map, float size)
    {
        WorldHostilityDirector director = WorldHostilityDirector.Current;
        for (int i = 0; i < map.SpawnSockets.Count; i++)
        {
            Vector3 world = CellToWorld(map, map.SpawnSockets[i]);
            float pressure = Mathf.Clamp01(director.GetLocalPressure(world) / 6f);
            Gizmos.color = Color.Lerp(new Color(0.25f, 0.8f, 1f, 0.55f), new Color(1f, 0.1f, 0.05f, 0.75f), pressure);
            Gizmos.DrawWireSphere(world, size + pressure * 0.5f);
        }
    }

    private static Vector3 CellToWorld(MapData map, Vector2Int cell)
    {
        float x = (cell.x - (map.Width - 1) * 0.5f) * map.CellSize;
        float y = (cell.y - (map.Height - 1) * 0.5f) * map.CellSize;
        return new Vector3(x, y, 0f);
    }

    private static Color BuildingColor(BuildingKind kind)
    {
        switch (kind)
        {
            case BuildingKind.Safehouse:
                return new Color(0.25f, 0.9f, 0.7f, 0.9f);
            case BuildingKind.ExtractionRoom:
                return new Color(0.6f, 0.35f, 1f, 0.9f);
            case BuildingKind.SpawnDen:
                return new Color(1f, 0.15f, 0.12f, 0.9f);
            case BuildingKind.SupplyCache:
                return new Color(1f, 0.75f, 0.2f, 0.9f);
            default:
                return Color.white;
        }
    }

    private static Color ResidueColor(CombatResidueType type)
    {
        switch (type)
        {
            case CombatResidueType.BloodDrop:
            case CombatResidueType.BloodPool:
                return new Color(0.7f, 0f, 0f, 0.85f);
            case CombatResidueType.Corpse:
                return new Color(0.45f, 0.38f, 0.32f, 0.85f);
            case CombatResidueType.Scorch:
                return new Color(0.18f, 0.08f, 0.02f, 0.85f);
            case CombatResidueType.Debris:
                return new Color(0.45f, 0.45f, 0.42f, 0.75f);
            case CombatResidueType.GrassCluster:
                return new Color(0.2f, 0.65f, 0.25f, 0.65f);
            default:
                return Color.white;
        }
    }
}
