using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public sealed class SemanticWorldView : MonoBehaviour
{
    private const string ViewRootName = "SemanticWorldView";
    private static readonly ProfilerMarker RenderMarker = new ProfilerMarker("SemanticWorld.View.Render");
    private static readonly ProfilerMarker RefreshResiduesMarker = new ProfilerMarker("SemanticWorld.View.RefreshResidues");

    [Header("Catalog")]
    [SerializeField] private SemanticWorldVisualCatalog visualCatalog;

    [Header("Sprites")]
    [SerializeField] private Sprite safehouseSprite;
    [SerializeField] private Sprite extractionSprite;
    [SerializeField] private Sprite spawnDenSprite;
    [SerializeField] private Sprite supplyCacheSprite;
    [SerializeField] private Sprite currencySprite;
    [SerializeField] private Sprite materialSprite;
    [SerializeField] private Sprite bloodSprite;
    [SerializeField] private Sprite scorchSprite;
    [SerializeField] private Sprite corpseSprite;
    [SerializeField] private Sprite debrisSprite;
    [SerializeField] private Sprite grassSprite;

    [Header("Rendering")]
    [SerializeField] private int buildingSortingOrder = 8;
    [SerializeField] private int resourceSortingOrder = 11;
    [SerializeField] private int residueSortingOrder = 3;
    [SerializeField] private int maxVisibleResidues = 96;

    private readonly List<GameObject> residueObjects = new List<GameObject>();
    private Transform root;
    private Transform buildingsRoot;
    private Transform resourcesRoot;
    private Transform residuesRoot;
    private MapData currentMap;

    public int VisibleBuildingCount => buildingsRoot != null ? buildingsRoot.childCount : 0;
    public int VisibleResourceCount => resourcesRoot != null ? resourcesRoot.childCount : 0;
    public int VisibleResidueCount => residueObjects.Count;
    public int PooledResidueObjectCount => residuesRoot != null ? residuesRoot.childCount : 0;

    public void SetVisualCatalog(SemanticWorldVisualCatalog catalog)
    {
        visualCatalog = catalog;
    }

    public void Render(MapData map, AftermathGrid aftermath)
    {
        using ProfilerMarker.AutoScope scope = RenderMarker.Auto();
        currentMap = map;
        EnsureSprites();
        EnsureRoots();
        ClearChildren(buildingsRoot);
        ClearChildren(resourcesRoot);
        ClearResidues();

        if (map == null)
        {
            return;
        }

        RenderBuildings(map);
        RenderResources(map);
        RenderResidues(aftermath);
    }

    public void RefreshResidues(AftermathGrid aftermath)
    {
        using ProfilerMarker.AutoScope scope = RefreshResiduesMarker.Auto();
        EnsureSprites();
        EnsureRoots();
        ClearResidues();
        RenderResidues(aftermath);
    }

    private void RenderBuildings(MapData map)
    {
        for (int i = 0; i < map.Buildings.Count; i++)
        {
            BuildingInstance building = map.Buildings[i];
            BuildingVisualBinding binding = GetBuildingBinding(building.Kind);
            Sprite sprite = binding != null && binding.Sprite != null ? binding.Sprite : GetBuildingSprite(building.Kind);
            GameObject prefab = binding != null ? binding.Prefab : null;
            if (sprite == null && prefab == null)
            {
                continue;
            }

            int sortingOrder = buildingSortingOrder + (binding != null ? binding.SortingOrderOffset : 0);
            GameObject obj = CreateVisualObject(buildingsRoot, $"Building_{building.Kind}_{i:00}", prefab, sprite, CellToWorld(map, building.AnchorCell), sortingOrder);
            float defaultSize = Mathf.Max(1f, Mathf.Max(building.Footprint.width, building.Footprint.height) * 0.35f);
            float size = binding != null && binding.Scale > 0f ? binding.Scale : defaultSize;
            obj.transform.localScale = Vector3.one * size;
        }
    }

    private void RenderResources(MapData map)
    {
        for (int i = 0; i < map.ResourceNodes.Count; i++)
        {
            ResourceNode node = map.ResourceNodes[i];
            ResourceVisualBinding binding = GetResourceBinding(node.Type);
            Sprite sprite = binding != null && binding.Sprite != null ? binding.Sprite : GetResourceSprite(node.Type);
            GameObject prefab = binding != null ? binding.Prefab : null;
            if (sprite == null && prefab == null)
            {
                continue;
            }

            int sortingOrder = resourceSortingOrder + (binding != null ? binding.SortingOrderOffset : 0);
            GameObject obj = CreateVisualObject(resourcesRoot, $"Resource_{node.Type}_{i:00}", prefab, sprite, CellToWorld(map, node.Cell) + Vector3.back * 0.04f, sortingOrder);
            float scale = binding != null && binding.Scale > 0f ? binding.Scale : 0.55f;
            obj.transform.localScale = Vector3.one * scale;
            ConfigureResourcePickup(obj, node);
        }
    }

    private static void ConfigureResourcePickup(GameObject obj, ResourceNode node)
    {
        CircleCollider2D collider = obj.GetComponent<CircleCollider2D>();
        if (collider == null)
        {
            collider = obj.AddComponent<CircleCollider2D>();
        }

        collider.isTrigger = true;
        collider.radius = 0.45f;

        SemanticResourcePickup pickup = obj.GetComponent<SemanticResourcePickup>();
        if (pickup == null)
        {
            pickup = obj.AddComponent<SemanticResourcePickup>();
        }

        pickup.Configure(node);
    }

    private void RenderResidues(AftermathGrid aftermath)
    {
        if (aftermath == null)
        {
            return;
        }

        int start = Mathf.Max(0, aftermath.Residues.Count - Mathf.Max(1, maxVisibleResidues));
        for (int i = start; i < aftermath.Residues.Count; i++)
        {
            CombatResidue residue = aftermath.Residues[i];
            ResidueVisualBinding binding = GetResidueBinding(residue.Type);
            Sprite sprite = binding != null && binding.Sprite != null ? binding.Sprite : GetResidueSprite(residue.Type);
            if (sprite == null)
            {
                continue;
            }

            GameObject obj = GetResidueObject($"Residue_{residue.Type}_{i:000}");
            int sortingOrder = residueSortingOrder + (binding != null ? binding.SortingOrderOffset : 0);
            ConfigureSpriteObject(obj, sprite, residue.WorldPosition + Vector2.down * 0.02f, sortingOrder);
            float scale = GetResidueScale(residue) * (binding != null && binding.Scale > 0f ? binding.Scale : 1f);
            obj.transform.localScale = Vector3.one * scale;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(residue.Direction.y, residue.Direction.x) * Mathf.Rad2Deg);
            residueObjects.Add(obj);
        }
    }

    private BuildingVisualBinding GetBuildingBinding(BuildingKind kind)
    {
        if (visualCatalog != null && visualCatalog.TryGetBuilding(kind, out BuildingVisualBinding binding))
        {
            return binding;
        }

        return null;
    }

    private ResourceVisualBinding GetResourceBinding(ResourceType type)
    {
        if (visualCatalog != null && visualCatalog.TryGetResource(type, out ResourceVisualBinding binding))
        {
            return binding;
        }

        return null;
    }

    private ResidueVisualBinding GetResidueBinding(CombatResidueType type)
    {
        if (visualCatalog != null && visualCatalog.TryGetResidue(type, out ResidueVisualBinding binding))
        {
            return binding;
        }

        return null;
    }

    private Sprite GetBuildingSprite(BuildingKind kind)
    {
        switch (kind)
        {
            case BuildingKind.Safehouse:
                return safehouseSprite;
            case BuildingKind.ExtractionRoom:
                return extractionSprite;
            case BuildingKind.SpawnDen:
                return spawnDenSprite;
            case BuildingKind.SupplyCache:
                return supplyCacheSprite;
            default:
                return supplyCacheSprite;
        }
    }

    private Sprite GetResidueSprite(CombatResidueType type)
    {
        switch (type)
        {
            case CombatResidueType.BloodDrop:
            case CombatResidueType.BloodPool:
                return bloodSprite;
            case CombatResidueType.Corpse:
                return corpseSprite;
            case CombatResidueType.Scorch:
                return scorchSprite;
            case CombatResidueType.Debris:
                return debrisSprite;
            case CombatResidueType.GrassCluster:
                return grassSprite;
            default:
                return null;
        }
    }

    private Sprite GetResourceSprite(ResourceType type)
    {
        return type == ResourceType.Currency ? currencySprite : materialSprite;
    }

    private float GetResidueScale(CombatResidue residue)
    {
        switch (residue.Type)
        {
            case CombatResidueType.BloodPool:
                return Mathf.Clamp(0.45f + residue.Intensity * 0.2f, 0.35f, 0.9f);
            case CombatResidueType.Corpse:
                return 0.75f;
            case CombatResidueType.GrassCluster:
                return Mathf.Clamp(0.4f + residue.Intensity * 0.15f, 0.35f, 0.75f);
            default:
                return Mathf.Clamp(0.25f + residue.Radius * 0.4f, 0.25f, 0.65f);
        }
    }

    private void EnsureRoots()
    {
        root = EnsureChild(transform, ViewRootName);
        buildingsRoot = EnsureChild(root, "Buildings");
        resourcesRoot = EnsureChild(root, "Resources");
        residuesRoot = EnsureChild(root, "Residues");
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

    private GameObject CreateSpriteObject(Transform parent, string objectName, Sprite sprite, Vector3 position, int sortingOrder)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(parent);
        ConfigureSpriteObject(obj, sprite, position, sortingOrder);
        return obj;
    }

    private GameObject CreateVisualObject(Transform parent, string objectName, GameObject prefab, Sprite sprite, Vector3 position, int sortingOrder)
    {
        GameObject obj = prefab != null ? Instantiate(prefab, parent) : new GameObject(objectName);
        obj.name = objectName;
        obj.transform.SetParent(parent);
        ConfigureVisualObject(obj, prefab, sprite, position, sortingOrder);
        return obj;
    }

    private static void ConfigureVisualObject(GameObject obj, GameObject prefab, Sprite sprite, Vector3 position, int sortingOrder)
    {
        if (prefab != null && obj.GetComponent<SpriteRenderer>() == null && sprite == null)
        {
            obj.SetActive(true);
            obj.transform.position = position;
            return;
        }

        ConfigureSpriteObject(obj, sprite, position, sortingOrder);
    }

    private static void ConfigureSpriteObject(GameObject obj, Sprite sprite, Vector3 position, int sortingOrder)
    {
        obj.SetActive(true);
        obj.transform.position = position;
        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null && sprite != null)
        {
            renderer = obj.AddComponent<SpriteRenderer>();
        }

        if (renderer != null)
        {
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }

            renderer.sortingOrder = sortingOrder;
        }
    }

    private void ClearResidues()
    {
        for (int i = 0; i < residueObjects.Count; i++)
        {
            if (residueObjects[i] != null)
            {
                residueObjects[i].SetActive(false);
            }
        }

        residueObjects.Clear();
    }

    private GameObject GetResidueObject(string objectName)
    {
        for (int i = 0; i < residuesRoot.childCount; i++)
        {
            GameObject child = residuesRoot.GetChild(i).gameObject;
            if (!child.activeSelf)
            {
                child.name = objectName;
                return child;
            }
        }

        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(residuesRoot);
        return obj;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            DestroyObject(parent.GetChild(i).gameObject);
        }
    }

    private static void DestroyObject(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }

    private Vector3 CellToWorld(MapData map, Vector2Int cell)
    {
        float x = (cell.x - (map.Width - 1) * 0.5f) * map.CellSize;
        float y = (cell.y - (map.Height - 1) * 0.5f) * map.CellSize;
        return new Vector3(x, y, -0.05f);
    }

    private void EnsureSprites()
    {
        safehouseSprite = safehouseSprite != null ? safehouseSprite : Resources.Load<Sprite>("Arts/SafeHouse/safehouse_floor_stone");
        extractionSprite = extractionSprite != null ? extractionSprite : Resources.Load<Sprite>("Arts/SafeHouse/safehouse_exit_door");
        spawnDenSprite = spawnDenSprite != null ? spawnDenSprite : Resources.Load<Sprite>("Arts/SimpleSprites/spawn_point_purple_simple");
        supplyCacheSprite = supplyCacheSprite != null ? supplyCacheSprite : Resources.Load<Sprite>("Arts/SafeHouse/safehouse_crates");
        currencySprite = currencySprite != null ? currencySprite : Resources.Load<Sprite>("Arts/Pickups/pickup_coin_hex");
        materialSprite = materialSprite != null ? materialSprite : Resources.Load<Sprite>("Arts/Pickups/pickup_energy_shard");
        bloodSprite = bloodSprite != null ? bloodSprite : Resources.Load<Sprite>("Arts/VFX/attack_hit_circle");
        scorchSprite = scorchSprite != null ? scorchSprite : Resources.Load<Sprite>("Arts/VFX/danger_telegraph");
        corpseSprite = corpseSprite != null ? corpseSprite : Resources.Load<Sprite>("Arts/SimpleSprites/enemy_warrior_simple");
        debrisSprite = debrisSprite != null ? debrisSprite : Resources.Load<Sprite>("Arts/Tiles/tile_wall_block");
        grassSprite = grassSprite != null ? grassSprite : Resources.Load<Sprite>("Arts/Tiles/tile_danger_floor");
    }
}
