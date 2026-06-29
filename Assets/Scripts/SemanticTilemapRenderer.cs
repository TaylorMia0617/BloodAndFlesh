using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class SemanticTilemapRenderer : MonoBehaviour
{
    private const string RootName = "SemanticTilemaps";
    private static readonly ProfilerMarker RenderMarker = new ProfilerMarker("SemanticWorld.Tilemap.Render");

    [SerializeField] private SemanticWorldVisualCatalog visualCatalog;
    [SerializeField] private Sprite groundSprite;
    [SerializeField] private Sprite routeSprite;
    [SerializeField] private Sprite wallSprite;
    [SerializeField] private Sprite safehouseSprite;
    [SerializeField] private bool addWallCollider = true;

    private Transform root;
    private Tilemap groundMap;
    private Tilemap routeMap;
    private Tilemap safehouseMap;
    private Tilemap wallMap;
    private Tile groundTile;
    private Tile routeTile;
    private Tile wallTile;
    private Tile safehouseTile;

    public Tilemap GroundMap => groundMap;
    public Tilemap RouteMap => routeMap;
    public Tilemap SafehouseMap => safehouseMap;
    public Tilemap WallMap => wallMap;

    public void SetVisualCatalog(SemanticWorldVisualCatalog catalog)
    {
        visualCatalog = catalog;
    }

    public void Render(MapData map)
    {
        using ProfilerMarker.AutoScope scope = RenderMarker.Auto();
        EnsureTiles();
        EnsureTilemaps();
        Clear();

        if (map == null)
        {
            return;
        }

        Grid grid = root.GetComponent<Grid>();
        if (grid != null)
        {
            grid.cellSize = new Vector3(map.CellSize, map.CellSize, 1f);
        }

        root.localPosition = new Vector3(
            -(map.Width - 1) * 0.5f * map.CellSize,
            -(map.Height - 1) * 0.5f * map.CellSize,
            0f);

        for (int i = 0; i < map.Cells.Length; i++)
        {
            MapCell cell = map.Cells[i];
            Vector3Int position = new Vector3Int(cell.Coord.x, cell.Coord.y, 0);
            if (cell.Flags.HasFlag(CellFlags.BlocksMovement))
            {
                SetTileIfAvailable(wallMap, position, wallTile);
                continue;
            }

            if (!cell.Flags.HasFlag(CellFlags.Walkable))
            {
                continue;
            }

            if (cell.Flags.HasFlag(CellFlags.SafeZone))
            {
                SetTileIfAvailable(safehouseMap, position, safehouseTile);
            }
            else if (cell.RegionKind == RegionKind.MainRoute)
            {
                SetTileIfAvailable(routeMap, position, routeTile);
            }
            else
            {
                SetTileIfAvailable(groundMap, position, groundTile);
            }
        }

        CompressBounds();
    }

    public void Clear()
    {
        groundMap?.ClearAllTiles();
        routeMap?.ClearAllTiles();
        safehouseMap?.ClearAllTiles();
        wallMap?.ClearAllTiles();
    }

    private void EnsureTilemaps()
    {
        root = EnsureChild(transform, RootName);
        groundMap = EnsureTilemap(root, "Ground", 0, false);
        routeMap = EnsureTilemap(root, "MainRoute", 1, false);
        safehouseMap = EnsureTilemap(root, "Safehouse", 2, false);
        wallMap = EnsureTilemap(root, "Walls", 5, addWallCollider);
    }

    private void EnsureTiles()
    {
        groundSprite = ResolveSprite(groundSprite, visualCatalog != null ? visualCatalog.GroundTileSprite : null, "Arts/Tiles/tile_danger_floor");
        routeSprite = ResolveSprite(routeSprite, visualCatalog != null ? visualCatalog.RouteTileSprite : null, "Arts/Tiles/tile_ground_grid");
        wallSprite = ResolveSprite(wallSprite, visualCatalog != null ? visualCatalog.WallTileSprite : null, "Arts/Tiles/tile_wall_block");
        safehouseSprite = ResolveSprite(safehouseSprite, visualCatalog != null ? visualCatalog.SafehouseTileSprite : null, "Arts/Tiles/tile_safe_room_floor");

        groundTile = CreateTile(groundTile, groundSprite);
        routeTile = CreateTile(routeTile, routeSprite);
        wallTile = CreateTile(wallTile, wallSprite);
        safehouseTile = CreateTile(safehouseTile, safehouseSprite);
    }

    private static Sprite ResolveSprite(Sprite explicitSprite, Sprite catalogSprite, string resourcePath)
    {
        if (explicitSprite != null)
        {
            return explicitSprite;
        }

        if (catalogSprite != null)
        {
            return catalogSprite;
        }

        return Resources.Load<Sprite>(resourcePath);
    }

    private static Tile CreateTile(Tile existing, Sprite sprite)
    {
        if (sprite == null)
        {
            return null;
        }

        Tile tile = existing != null ? existing : ScriptableObject.CreateInstance<Tile>();
        tile.hideFlags = HideFlags.HideAndDontSave;
        tile.sprite = sprite;
        return tile;
    }

    private static void SetTileIfAvailable(Tilemap tilemap, Vector3Int position, Tile tile)
    {
        if (tilemap != null && tile != null)
        {
            tilemap.SetTile(position, tile);
        }
    }

    private void CompressBounds()
    {
        groundMap.CompressBounds();
        routeMap.CompressBounds();
        safehouseMap.CompressBounds();
        wallMap.CompressBounds();
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject obj = new GameObject(childName);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        return obj.transform;
    }

    private static Tilemap EnsureTilemap(Transform parent, string name, int sortingOrder, bool withCollider)
    {
        Transform child = EnsureChild(parent, name);
        Grid grid = parent.GetComponent<Grid>();
        if (grid == null)
        {
            grid = parent.gameObject.AddComponent<Grid>();
        }

        Tilemap tilemap = child.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            tilemap = child.gameObject.AddComponent<Tilemap>();
        }

        TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<TilemapRenderer>();
        }

        renderer.sortingOrder = sortingOrder;

        if (withCollider && child.GetComponent<TilemapCollider2D>() == null)
        {
            child.gameObject.AddComponent<TilemapCollider2D>();
        }

        return tilemap;
    }
}
