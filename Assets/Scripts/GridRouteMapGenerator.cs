using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class GridRouteMapGenerator : MonoBehaviour
{
    private const string DefaultVisualCatalogResourcePath = "Configs/SemanticWorldVisualCatalog";
    private static readonly ProfilerMarker GenerateMarker = new ProfilerMarker("SemanticWorld.GenerateMap");

    [Header("Grid")]
    [SerializeField] private int width = 100;
    [SerializeField] private int height = 80;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private int corridorHalfWidth = 2;
    [SerializeField] private int branchCount = 18;
    [SerializeField] private int roomCount = 12;
    [SerializeField] private int fixedSeed = -1;
    [SerializeField] private bool useTilemapTerrain;
    [SerializeField] private SemanticWorldVisualCatalog visualCatalog;

    [Header("Sprites")]
    [SerializeField] private Sprite roadSprite;
    [SerializeField] private Sprite dangerSprite;
    [SerializeField] private Sprite wallSprite;
    [SerializeField] private Sprite safeRoomSprite;
    [SerializeField] private Sprite safeDoorSprite;

    [Header("Enemy Spawns")]
    [SerializeField] private int enemySpawnCount = 6;
    [SerializeField] private float enemySpawnMinDistanceFromPlayer = 12f;
    [SerializeField] private float hostilitySpawnInterval = 3.5f;
    [SerializeField] private bool showEnemySpawnMarkers = true;
    [SerializeField] private float spawnMarkerScale = 0.55f;
    [SerializeField] private Sprite normalSpawnMarkerSprite;
    [SerializeField] private Sprite highHostilitySpawnMarkerSprite;
    [SerializeField] private Sprite[] enemySprites;

    private readonly List<Vector2Int> currentPath = new List<Vector2Int>();
    private readonly List<Vector2Int> enemySpawnCells = new List<Vector2Int>();
    private readonly List<Vector3> enemySpawnPositions = new List<Vector3>();
    private readonly List<SpawnDenController> spawnDens = new List<SpawnDenController>();
    private Vector3 playerSpawnPosition;
    private bool[,] visionBlockers;
    private StageConfig currentStageConfig;
    private MapData currentMapData;
    private SemanticWorldVisualCatalog cachedDefaultVisualCatalog;
    private string cachedDefaultVisualCatalogPath;

    public Vector3 PlayerSpawnPosition => playerSpawnPosition;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public Vector2 WorldMin => new Vector2(-width * 0.5f * cellSize, -height * 0.5f * cellSize);
    public Vector2 WorldSize => new Vector2(width * cellSize, height * cellSize);
    public IReadOnlyList<Vector3> EnemySpawnPositions => enemySpawnPositions;
    public IReadOnlyList<SpawnDenController> SpawnDens => spawnDens;
    public MapData CurrentMapData => currentMapData;
    public StageConfig CurrentStageConfig => currentStageConfig;
    public float LastGenerationMilliseconds { get; private set; }
    public SemanticMapValidationReport LastValidationReport { get; private set; }

    private void Awake()
    {
        LoadDefaultSprites();
    }

    public void GenerateMap()
    {
        if (fixedSeed >= 0)
        {
            GenerateMap(fixedSeed);
            return;
        }

        GenerateMapInternal(null);
    }

    public void GenerateMap(int seed)
    {
        GenerateMapInternal(seed);
    }

    private void GenerateMapInternal(int? seed)
    {
        using ProfilerMarker.AutoScope scope = GenerateMarker.Auto();
        Random.State previousRandomState = Random.state;
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (seed.HasValue)
        {
            Random.InitState(seed.Value);
        }

        try
        {
            ApplyStageConfig();
            LoadDefaultSprites();
            ClearMap();
            EnemyRegistry.Clear();
            currentPath.Clear();
            enemySpawnCells.Clear();
            enemySpawnPositions.Clear();
            spawnDens.Clear();

            bool[,] road = BuildCorridorNetwork();
            WorldHostilityDirector.ResetRuntime(currentStageConfig);
            WorldHostilityDirector.Current.Notify(new DirectorEvent(DirectorEventType.StageStarted, Vector2.zero));
            WorldHostilityDirectorRunner.Ensure();
            visionBlockers = new bool[width, height];
            Vector2Int start = currentPath[0];
            Vector2Int end = currentPath[currentPath.Count - 1];
            playerSpawnPosition = GridToWorld(start) + Vector3.right * (cellSize * 3f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    bool isPath = road[x, y];
                    bool isSafeRoom = cell == start || cell == end;

                    if (isSafeRoom)
                    {
                        visionBlockers[x, y] = false;
                        if (!useTilemapTerrain)
                        {
                            CreateTile($"SafeRoom_{x}_{y}", safeRoomSprite, cell, 1, false);
                        }

                        continue;
                    }

                    if (isPath)
                    {
                        visionBlockers[x, y] = false;
                        if (!useTilemapTerrain)
                        {
                            CreateTile($"Road_{x}_{y}", roadSprite, cell, 0, false);
                        }

                        continue;
                    }

                    if (isBorder || !isPath)
                    {
                        visionBlockers[x, y] = true;
                        if (!useTilemapTerrain)
                        {
                            CreateTile($"Obstacle_{x}_{y}", wallSprite, cell, 5, true);
                        }
                    }
                    else
                    {
                        visionBlockers[x, y] = false;
                        if (!useTilemapTerrain)
                        {
                            CreateTile($"Ground_{x}_{y}", dangerSprite, cell, 0, false);
                        }
                    }
                }
            }

            CreateSafeDoor("EntryDoor", start + Vector2Int.right, SafeRoomPortal.PortalMode.DisabledVisualDoor);
            CreateSafeDoor("ExitDoor", end + Vector2Int.left, SafeRoomPortal.PortalMode.DungeonExitToSafeRoom);
            BuildEnemySpawnCells(road);
            currentMapData = BuildCurrentMapData(road, start, end, seed ?? 0);
            LastValidationReport = SemanticMapValidator.Validate(currentMapData);
            if (useTilemapTerrain)
            {
                EnsureTilemapRenderer().Render(currentMapData);
            }

            SemanticWorldView worldView = EnsureWorldView();
            CombatAftermathSystem.Instance.Configure(this, worldView);
            worldView.Render(currentMapData, CombatAftermathSystem.Instance.Grid);
            EnsureDebugOverlay().RefreshMetrics();
            CreateSpawnDens();
            CreateFixedScouts(road);
            StopLegacyWorldHostilitySpawner();
        }
        finally
        {
            stopwatch.Stop();
            LastGenerationMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
            if (seed.HasValue)
            {
                Random.state = previousRandomState;
            }
        }
    }

    private void ApplyStageConfig()
    {
        currentStageConfig = RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentStageConfig : null;
        if (currentStageConfig == null)
        {
            return;
        }

        width = Mathf.Max(12, currentStageConfig.mapWidth);
        height = Mathf.Max(12, currentStageConfig.mapHeight);
        cellSize = Mathf.Max(0.25f, currentStageConfig.cellSize);
        corridorHalfWidth = Mathf.Max(1, currentStageConfig.corridorHalfWidth);
        branchCount = Mathf.Max(0, currentStageConfig.branchCount);
        roomCount = Mathf.Max(0, currentStageConfig.roomCount);
        enemySpawnCount = Mathf.Max(0, currentStageConfig.initialEnemySpawnCount);
        enemySpawnMinDistanceFromPlayer = Mathf.Max(0f, currentStageConfig.enemySpawnMinDistanceFromPlayer);
        hostilitySpawnInterval = Mathf.Max(0.1f, currentStageConfig.hostilitySpawnInterval);
        if (currentStageConfig.semanticUseTilemapTerrain >= 0)
        {
            useTilemapTerrain = currentStageConfig.semanticUseTilemapTerrain > 0;
        }

        roadSprite = LoadSpriteOrKeep(currentStageConfig.roadSpriteResource, roadSprite);
        dangerSprite = LoadSpriteOrKeep(currentStageConfig.dangerSpriteResource, dangerSprite);
        wallSprite = LoadSpriteOrKeep(currentStageConfig.wallSpriteResource, wallSprite);
        safeRoomSprite = LoadSpriteOrKeep(currentStageConfig.safeRoomSpriteResource, safeRoomSprite);
        safeDoorSprite = LoadSpriteOrKeep(currentStageConfig.safeDoorSpriteResource, safeDoorSprite);
    }

    private Sprite LoadSpriteOrKeep(string resourcePath, Sprite fallback)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return fallback;
        }

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        return sprite != null ? sprite : fallback;
    }

    private bool[,] BuildCorridorNetwork()
    {
        bool[,] road = new bool[width, height];
        int y = Random.Range(height / 3, (height * 2) / 3);
        int x = 2;

        while (x < width - 3)
        {
            DigCorridor(road, new Vector2Int(x, y), true);
            currentPath.Add(new Vector2Int(x, y));

            int runLength = Random.Range(4, 10);
            for (int i = 0; i < runLength && x < width - 3; i++)
            {
                x++;
                DigCorridor(road, new Vector2Int(x, y), true);
                currentPath.Add(new Vector2Int(x, y));
            }

            int targetY = Mathf.Clamp(y + Random.Range(-10, 11), 4, height - 5);
            int step = targetY >= y ? 1 : -1;
            while (y != targetY)
            {
                y += step;
                DigCorridor(road, new Vector2Int(x, y), true);
                currentPath.Add(new Vector2Int(x, y));
            }
        }

        for (int i = 0; i < branchCount; i++)
        {
            Vector2Int anchor = currentPath[Random.Range(0, currentPath.Count)];
            DigBranch(road, anchor);
        }

        for (int i = 0; i < roomCount; i++)
        {
            Vector2Int anchor = currentPath[Random.Range(0, currentPath.Count)];
            DigRoom(road, anchor, Random.Range(4, 8), Random.Range(3, 6));
        }

        return road;
    }

    private void DigBranch(bool[,] road, Vector2Int start)
    {
        Vector2Int direction = Random.value < 0.5f ? Vector2Int.up : Vector2Int.down;
        int length = Random.Range(8, 20);
        Vector2Int cursor = start;

        for (int i = 0; i < length; i++)
        {
            cursor += direction;
            if (!IsInside(cursor, 4))
            {
                break;
            }

            DigCorridor(road, cursor, false);
            if (Random.value < 0.28f)
            {
                cursor += Vector2Int.right;
            }
        }
    }

    private void DigCorridor(bool[,] road, Vector2Int center, bool horizontalBias)
    {
        int horizontal = horizontalBias ? corridorHalfWidth + 1 : corridorHalfWidth;
        int vertical = horizontalBias ? corridorHalfWidth : corridorHalfWidth + 1;

        for (int y = center.y - vertical; y <= center.y + vertical; y++)
        {
            for (int x = center.x - horizontal; x <= center.x + horizontal; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsInside(cell, 1))
                {
                    road[x, y] = true;
                }
            }
        }
    }

    private void DigRoom(bool[,] road, Vector2Int center, int roomWidth, int roomHeight)
    {
        for (int y = center.y - roomHeight; y <= center.y + roomHeight; y++)
        {
            for (int x = center.x - roomWidth; x <= center.x + roomWidth; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsInside(cell, 2))
                {
                    road[x, y] = true;
                }
            }
        }
    }

    private bool IsInside(Vector2Int cell, int margin)
    {
        return cell.x >= margin && cell.y >= margin && cell.x < width - margin && cell.y < height - margin;
    }

    private GameObject CreateTile(string tileName, Sprite sprite, Vector2Int gridPosition, int sortingOrder, bool blocksMovement)
    {
        GameObject tile = new GameObject(tileName);
        tile.transform.SetParent(transform);
        tile.transform.position = GridToWorld(gridPosition);

        SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;

        if (blocksMovement)
        {
            BoxCollider2D collider = tile.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * (cellSize * 0.92f);
            tile.AddComponent<ObstacleHitFeedback>();
        }

        return tile;
    }

    private void CreateSafeDoor(string doorName, Vector2Int gridPosition, SafeRoomPortal.PortalMode mode)
    {
        GameObject door = CreateTile(doorName, safeDoorSprite, gridPosition, 6, false);
        BoxCollider2D trigger = door.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = Vector2.one * (cellSize * 0.9f);
        SafeRoomPortal portal = door.AddComponent<SafeRoomPortal>();
        portal.Configure(mode);
    }

    private void BuildEnemySpawnCells(bool[,] road)
    {
        Vector2Int playerCell = WorldToGrid(playerSpawnPosition);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (!road[x, y])
                {
                    continue;
                }

                Vector2Int cell = new Vector2Int(x, y);
                if (Vector2.Distance(GridToWorld(cell), playerSpawnPosition) < enemySpawnMinDistanceFromPlayer)
                {
                    continue;
                }

                if (Mathf.Abs(cell.x - playerCell.x) <= 3 && Mathf.Abs(cell.y - playerCell.y) <= 3)
                {
                    continue;
                }

                if (HasAdjacentWall(road, cell))
                {
                    enemySpawnCells.Add(cell);
                }
            }
        }
    }

    private MapData BuildCurrentMapData(bool[,] road, Vector2Int start, Vector2Int end, int seed)
    {
        float baseHostility = currentStageConfig != null ? currentStageConfig.worldHostility : 0f;
        MapData mapData = SemanticMapBuilder.BuildFromRoute(
            width,
            height,
            cellSize,
            road,
            currentPath,
            start,
            end,
            enemySpawnCells,
            baseHostility,
            seed);
        SemanticPlacementSolver.PopulateDefaultPlacements(mapData, BuildPlacementSettings());
        return mapData;
    }

    private SemanticPlacementSettings BuildPlacementSettings()
    {
        StageConfig stageConfig = currentStageConfig;
        if (stageConfig == null)
        {
            return new SemanticPlacementSettings();
        }

        return new SemanticPlacementSettings
        {
            MaxSpawnDenBuildings = Mathf.Max(0, stageConfig.spawnDenCount),
            MaxSupplyCaches = ResolveSemanticCount(stageConfig.semanticSupplyCacheCount, Mathf.Clamp(stageConfig.roomCount / 3, 2, 6)),
            MaxResourceNodes = ResolveSemanticCount(stageConfig.semanticResourceNodeCount, Mathf.Clamp(stageConfig.roomCount + stageConfig.spawnDenCount, 4, 14)),
            MinBuildingSpacing = Mathf.Max(3, stageConfig.corridorHalfWidth + 2),
            SafehouseFootprintRadius = Mathf.Max(1, stageConfig.corridorHalfWidth)
        };
    }

    private static int ResolveSemanticCount(int configuredValue, int fallback)
    {
        return configuredValue >= 0 ? configuredValue : Mathf.Max(0, fallback);
    }

    private bool HasAdjacentWall(bool[,] road, Vector2Int cell)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                Vector2Int neighbor = new Vector2Int(cell.x + x, cell.y + y);
                if (!IsGridInside(neighbor) || !road[neighbor.x, neighbor.y])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CreateSpawnDens()
    {
        StageConfig stageConfig = currentStageConfig ?? (RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentStageConfig : null);
        if (stageConfig == null)
        {
            return;
        }

        WorldHostilityDirector director = WorldHostilityDirector.Current;
        SpawnDenConfig[] denConfigs = SpawnDenConfigDatabase.PickSpawnDens(
            stageConfig.spawnDenPoolId,
            stageConfig.spawnDenCount,
            director.RawHostility,
            stageConfig.allowDuplicateSpawnDens,
            stageConfig.spawnDenWeightMultiplier,
            stageConfig.spawnDenLevelBias);
        if (denConfigs.Length == 0 || enemySpawnCells.Count == 0)
        {
            return;
        }

        List<Vector2Int> candidates = new List<Vector2Int>(enemySpawnCells);
        int count = Mathf.Min(denConfigs.Length, candidates.Count);
        Transform playerTarget = FindPlayerTarget();
        int globalLimit = RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentEnemyBudget : 20;
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, candidates.Count);
            Vector2Int cell = candidates[index];
            candidates.RemoveAt(index);

            Vector3 position = GridToWorld(cell);
            enemySpawnPositions.Add(position);
            CreateSpawnPointMarker(position, false);
            CreateSpawnDen($"SpawnDen_{denConfigs[i].id}", position, denConfigs[i], playerTarget, globalLimit);
        }
    }

    private void CreateFixedScouts(bool[,] road)
    {
        StageConfig stageConfig = currentStageConfig ?? (RunLevelManager.Instance != null ? RunLevelManager.Instance.CurrentStageConfig : null);
        if (stageConfig == null || stageConfig.fixedScouts == null || stageConfig.fixedScouts.Length == 0)
        {
            return;
        }

        Transform playerTarget = FindPlayerTarget();
        WorldHostilityDirector director = WorldHostilityDirector.Current;
        for (int i = 0; i < stageConfig.fixedScouts.Length; i++)
        {
            FixedScoutConfig scoutConfig = stageConfig.fixedScouts[i];
            if (scoutConfig == null)
            {
                continue;
            }

            Vector3[] patrolRoute = BuildFixedScoutRoute(scoutConfig, road);
            if (patrolRoute.Length == 0)
            {
                continue;
            }

            EnemyArchetype archetype = EnemyConfigDatabase.ResolveArchetypeName(scoutConfig.scoutName, EnemyArchetype.Sentinel);
            int count = Mathf.Max(0, scoutConfig.count);
            for (int scoutIndex = 0; scoutIndex < count; scoutIndex++)
            {
                Vector3 spawnPosition = patrolRoute[0] + GetScoutSpawnOffset(scoutIndex);
                GameObject scout = EnemySpawnFactory.CreateEnemy(
                    transform,
                    $"FixedScout_{scoutConfig.id}_{scoutIndex + 1:00}",
                    spawnPosition,
                    archetype,
                    playerTarget,
                    enemySprites,
                    director);
                SimpleEnemyAI ai = scout.GetComponent<SimpleEnemyAI>();
                if (ai != null)
                {
                    ai.ConfigureFixedScoutPatrol(
                        patrolRoute,
                        scoutConfig.summonEnemyArchetypes,
                        scoutConfig.summonCount,
                        scoutConfig.summonCooldown,
                        scoutConfig.summonDenSearchRadius);
                }
            }
        }
    }

    private Vector3[] BuildFixedScoutRoute(FixedScoutConfig scoutConfig, bool[,] road)
    {
        if (scoutConfig == null || scoutConfig.patrolRoute == null || scoutConfig.patrolRoute.Length == 0)
        {
            return System.Array.Empty<Vector3>();
        }

        List<Vector3> route = new List<Vector3>();
        for (int i = 0; i < scoutConfig.patrolRoute.Length; i++)
        {
            GridPatrolPoint point = scoutConfig.patrolRoute[i];
            if (point == null)
            {
                continue;
            }

            Vector2Int grid = new Vector2Int(point.x, point.y);
            if (!IsGridInside(grid))
            {
                continue;
            }

            route.Add(GridToWorld(FindNearestRoadCell(grid, road)));
        }

        return route.ToArray();
    }

    private Vector2Int FindNearestRoadCell(Vector2Int origin, bool[,] road)
    {
        if (road == null || !IsGridInside(origin))
        {
            return origin;
        }

        if (road[origin.x, origin.y])
        {
            return origin;
        }

        int maxRadius = Mathf.Max(width, height);
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            Vector2Int best = origin;
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int y = origin.y - radius; y <= origin.y + radius; y++)
            {
                for (int x = origin.x - radius; x <= origin.x + radius; x++)
                {
                    if (x != origin.x - radius && x != origin.x + radius && y != origin.y - radius && y != origin.y + radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = new Vector2Int(x, y);
                    if (!IsGridInside(candidate) || !road[candidate.x, candidate.y])
                    {
                        continue;
                    }

                    float distance = (candidate - origin).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                        found = true;
                    }
                }
            }

            if (found)
            {
                return best;
            }
        }

        return origin;
    }

    private Vector3 GetScoutSpawnOffset(int scoutIndex)
    {
        if (scoutIndex <= 0)
        {
            return Vector3.zero;
        }

        float angle = scoutIndex * 2.399963f;
        float radius = Mathf.Min(0.45f, cellSize * 0.35f);
        return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
    }

    private void CreateSpawnPointMarker(Vector3 position, bool highHostility)
    {
        if (!showEnemySpawnMarkers)
        {
            return;
        }

        Sprite markerSprite = highHostility ? highHostilitySpawnMarkerSprite : normalSpawnMarkerSprite;
        if (markerSprite == null)
        {
            markerSprite = highHostility
                ? Resources.Load<Sprite>("Arts/SimpleSprites/spawn_point_purple_simple")
                : Resources.Load<Sprite>("Arts/SimpleSprites/spawn_point_red_simple");
        }

        if (markerSprite == null)
        {
            return;
        }

        GameObject marker = new GameObject(highHostility ? "HighHostilitySpawnMarker" : "EnemySpawnMarker");
        marker.transform.SetParent(transform);
        marker.transform.position = position + Vector3.back * 0.05f;
        marker.transform.localScale = Vector3.one * Mathf.Max(0.1f, spawnMarkerScale);

        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = markerSprite;
        renderer.sortingOrder = 13;
    }

    private void CreateSpawnDen(string denName, Vector3 position, SpawnDenConfig denConfig, Transform playerTarget, int globalEnemyLimit)
    {
        GameObject den = new GameObject(denName);
        den.transform.SetParent(transform);
        den.transform.position = position + Vector3.back * 0.08f;

        SpriteRenderer renderer = den.AddComponent<SpriteRenderer>();
        renderer.sprite = normalSpawnMarkerSprite != null ? normalSpawnMarkerSprite : Resources.Load<Sprite>("Arts/SimpleSprites/spawn_point_red_simple");
        renderer.sortingOrder = 12;

        CircleCollider2D collider = den.AddComponent<CircleCollider2D>();
        collider.radius = Mathf.Max(0.35f, spawnMarkerScale * 0.5f);

        Rigidbody2D body = den.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        den.AddComponent<DamageableHurtbox>();
        CharacterStats stats = den.AddComponent<CharacterStats>();
        SpawnDenController controller = den.AddComponent<SpawnDenController>();
        controller.Configure(denConfig, playerTarget, enemySprites, transform, globalEnemyLimit);
        stats.ResetStats();
        spawnDens.Add(controller);
    }

    private void StopLegacyWorldHostilitySpawner()
    {
        WorldHostilitySpawner spawner = GetComponent<WorldHostilitySpawner>();
        spawner?.StopSpawning();
    }

    private SemanticWorldDebugOverlay EnsureDebugOverlay()
    {
        SemanticWorldDebugOverlay overlay = GetComponent<SemanticWorldDebugOverlay>();
        if (overlay == null)
        {
            overlay = gameObject.AddComponent<SemanticWorldDebugOverlay>();
        }

        overlay.Configure(this);
        return overlay;
    }

    private SemanticWorldView EnsureWorldView()
    {
        SemanticWorldView view = GetComponent<SemanticWorldView>();
        if (view == null)
        {
            view = gameObject.AddComponent<SemanticWorldView>();
        }

        view.SetVisualCatalog(ResolveVisualCatalog());
        return view;
    }

    private SemanticTilemapRenderer EnsureTilemapRenderer()
    {
        SemanticTilemapRenderer renderer = GetComponent<SemanticTilemapRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<SemanticTilemapRenderer>();
        }

        renderer.SetVisualCatalog(ResolveVisualCatalog());
        return renderer;
    }

    private SemanticWorldVisualCatalog ResolveVisualCatalog()
    {
        if (visualCatalog != null)
        {
            return visualCatalog;
        }

        string resourcePath = ResolveVisualCatalogResourcePath();
        if (cachedDefaultVisualCatalog == null || cachedDefaultVisualCatalogPath != resourcePath)
        {
            cachedDefaultVisualCatalogPath = resourcePath;
            cachedDefaultVisualCatalog = Resources.Load<SemanticWorldVisualCatalog>(resourcePath);
        }

        return cachedDefaultVisualCatalog;
    }

    private string ResolveVisualCatalogResourcePath()
    {
        if (currentStageConfig != null && !string.IsNullOrEmpty(currentStageConfig.semanticVisualCatalogResource))
        {
            return currentStageConfig.semanticVisualCatalogResource;
        }

        return DefaultVisualCatalogResourcePath;
    }

    private Transform FindPlayerTarget()
    {
        PlayerInputManager player = FindObjectOfType<PlayerInputManager>();
        return player != null ? player.transform : null;
    }

    private Sprite GetEnemySprite(EnemyArchetype archetype)
    {
        if (enemySprites != null && enemySprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp((int)archetype, 0, enemySprites.Length - 1);
            Sprite configuredSprite = enemySprites[spriteIndex];
            if (configuredSprite != null)
            {
                return configuredSprite;
            }
        }

        LoadDefaultSprites();
        if (enemySprites != null && enemySprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp((int)archetype, 0, enemySprites.Length - 1);
            return enemySprites[spriteIndex];
        }

        return null;
    }

    private Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float x = (gridPosition.x - (width - 1) * 0.5f) * cellSize;
        float y = (gridPosition.y - (height - 1) * 0.5f) * cellSize;
        return new Vector3(x, y, 0f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt((worldPosition.x / cellSize) + (width - 1) * 0.5f);
        int y = Mathf.RoundToInt((worldPosition.y / cellSize) + (height - 1) * 0.5f);
        return new Vector2Int(x, y);
    }

    public bool IsGridInside(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0 && gridPosition.y >= 0 && gridPosition.x < width && gridPosition.y < height;
    }

    public bool BlocksVision(Vector2Int gridPosition)
    {
        return SemanticVisionQuery.BlocksVision(this, gridPosition);
    }

    public bool LegacyBlocksVision(Vector2Int gridPosition)
    {
        if (!IsGridInside(gridPosition))
        {
            return true;
        }

        return visionBlockers != null && visionBlockers[gridPosition.x, gridPosition.y];
    }

    private void ClearMap()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private void LoadDefaultSprites()
    {
        roadSprite = roadSprite != null ? roadSprite : Resources.Load<Sprite>("Arts/Tiles/tile_ground_grid");
        dangerSprite = dangerSprite != null ? dangerSprite : Resources.Load<Sprite>("Arts/Tiles/tile_danger_floor");
        wallSprite = wallSprite != null ? wallSprite : Resources.Load<Sprite>("Arts/Tiles/tile_wall_block");
        safeRoomSprite = safeRoomSprite != null ? safeRoomSprite : Resources.Load<Sprite>("Arts/Tiles/tile_safe_room_floor");
        safeDoorSprite = safeDoorSprite != null ? safeDoorSprite : Resources.Load<Sprite>("Arts/Tiles/tile_safe_door");
        normalSpawnMarkerSprite = normalSpawnMarkerSprite != null ? normalSpawnMarkerSprite : Resources.Load<Sprite>("Arts/SimpleSprites/spawn_point_red_simple");
        highHostilitySpawnMarkerSprite = highHostilitySpawnMarkerSprite != null ? highHostilitySpawnMarkerSprite : Resources.Load<Sprite>("Arts/SimpleSprites/spawn_point_purple_simple");
        if (enemySprites == null || enemySprites.Length == 0)
        {
            enemySprites = new[]
            {
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Attacker),
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Sentinel),
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Ranged),
                EnemyConfigDatabase.LoadSprite(EnemyArchetype.Shield)
            };
        }
    }
}
