using System.Collections.Generic;
using UnityEngine;

public class GridRouteMapGenerator : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int width = 100;
    [SerializeField] private int height = 80;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private int corridorHalfWidth = 2;
    [SerializeField] private int branchCount = 18;
    [SerializeField] private int roomCount = 12;

    [Header("Sprites")]
    [SerializeField] private Sprite roadSprite;
    [SerializeField] private Sprite dangerSprite;
    [SerializeField] private Sprite wallSprite;
    [SerializeField] private Sprite safeRoomSprite;
    [SerializeField] private Sprite safeDoorSprite;

    [Header("Enemy Spawns")]
    [SerializeField] private int enemySpawnCount = 8;
    [SerializeField] private float enemySpawnMinDistanceFromPlayer = 12f;
    [SerializeField] private float hostilitySpawnInterval = 3.5f;
    [SerializeField] private Sprite[] enemySprites;

    private readonly List<Vector2Int> currentPath = new List<Vector2Int>();
    private readonly List<Vector2Int> enemySpawnCells = new List<Vector2Int>();
    private readonly List<Vector3> enemySpawnPositions = new List<Vector3>();
    private Vector3 playerSpawnPosition;
    private bool[,] visionBlockers;

    public Vector3 PlayerSpawnPosition => playerSpawnPosition;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public Vector2 WorldMin => new Vector2(-width * 0.5f * cellSize, -height * 0.5f * cellSize);
    public Vector2 WorldSize => new Vector2(width * cellSize, height * cellSize);
    public IReadOnlyList<Vector3> EnemySpawnPositions => enemySpawnPositions;

    private void Awake()
    {
        LoadDefaultSprites();
    }

    public void GenerateMap()
    {
        LoadDefaultSprites();
        ClearMap();
        EnemyRegistry.Clear();
        currentPath.Clear();
        enemySpawnCells.Clear();
        enemySpawnPositions.Clear();

        bool[,] road = BuildCorridorNetwork();
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
                    CreateTile($"SafeRoom_{x}_{y}", safeRoomSprite, cell, 1, false);
                    continue;
                }

                if (isPath)
                {
                    visionBlockers[x, y] = false;
                    CreateTile($"Road_{x}_{y}", roadSprite, cell, 0, false);
                    continue;
                }

                if (isBorder || !isPath)
                {
                    visionBlockers[x, y] = true;
                    CreateTile($"Obstacle_{x}_{y}", wallSprite, cell, 5, true);
                }
                else
                {
                    visionBlockers[x, y] = false;
                    CreateTile($"Ground_{x}_{y}", dangerSprite, cell, 0, false);
                }
            }
        }

        CreateSafeDoor("EntryDoor", start + Vector2Int.right, SafeRoomPortal.PortalMode.DisabledVisualDoor);
        CreateSafeDoor("ExitDoor", end + Vector2Int.left, SafeRoomPortal.PortalMode.DungeonExitToSafeRoom);
        BuildEnemySpawnCells(road);
        SpawnPrototypeEnemies();
        ConfigureWorldHostilitySpawner();
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

    private void SpawnPrototypeEnemies()
    {
        if (enemySpawnCount <= 0 || enemySpawnCells.Count == 0)
        {
            return;
        }

        List<Vector2Int> candidates = new List<Vector2Int>(enemySpawnCells);
        int count = Mathf.Min(enemySpawnCount, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, candidates.Count);
            Vector2Int cell = candidates[index];
            candidates.RemoveAt(index);

            Vector3 position = GridToWorld(cell);
            enemySpawnPositions.Add(position);
            CreatePrototypeEnemy($"EnemySpawn_{i:00}", position, PickArchetype());
        }
    }

    private EnemyArchetype PickArchetype()
    {
        return EnemyConfigDatabase.PickWeighted();
    }

    private void CreatePrototypeEnemy(string enemyName, Vector3 position, EnemyArchetype archetype)
    {
        GameObject enemy = new GameObject(enemyName);
        enemy.transform.SetParent(transform);
        enemy.transform.position = position + Vector3.back * 0.1f;

        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = GetEnemySprite(archetype);
        renderer.sortingOrder = 14;

        CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
        collider.radius = EnemyConfigDatabase.Get(archetype).colliderRadius;

        Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CharacterStats stats = enemy.AddComponent<CharacterStats>();
        enemy.AddComponent<HitVolumeFeedback>();
        SimpleEnemyAI ai = enemy.AddComponent<SimpleEnemyAI>();
        PrototypeDamageable damageable = enemy.AddComponent<PrototypeDamageable>();
        ai.Configure(archetype, FindPlayerTarget());
        damageable.enabled = true;
        stats.ResetStats();
    }

    private void ConfigureWorldHostilitySpawner()
    {
        WorldHostilitySpawner spawner = GetComponent<WorldHostilitySpawner>();
        if (spawner == null)
        {
            spawner = gameObject.AddComponent<WorldHostilitySpawner>();
        }

        spawner.Configure(this, FindPlayerTarget(), enemySprites);
        spawner.SetSpawnInterval(hostilitySpawnInterval);
        spawner.StartSpawning();
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
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private void LoadDefaultSprites()
    {
        roadSprite = roadSprite != null ? roadSprite : Resources.Load<Sprite>("Arts/Tiles/tile_ground_grid");
        dangerSprite = dangerSprite != null ? dangerSprite : Resources.Load<Sprite>("Arts/Tiles/tile_danger_floor");
        wallSprite = wallSprite != null ? wallSprite : Resources.Load<Sprite>("Arts/Tiles/tile_wall_block");
        safeRoomSprite = safeRoomSprite != null ? safeRoomSprite : Resources.Load<Sprite>("Arts/Tiles/tile_safe_room_floor");
        safeDoorSprite = safeDoorSprite != null ? safeDoorSprite : Resources.Load<Sprite>("Arts/Tiles/tile_safe_door");
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
