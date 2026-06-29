using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public enum CombatResidueType : byte
{
    BloodDrop,
    BloodPool,
    Corpse,
    Scorch,
    Debris,
    GrassCluster
}

public readonly struct CombatResidue
{
    public readonly CombatResidueType Type;
    public readonly Vector2Int Cell;
    public readonly Vector2 WorldPosition;
    public readonly Vector2 Direction;
    public readonly float Intensity;
    public readonly float Radius;
    public readonly float Timestamp;

    public CombatResidue(CombatResidueType type, Vector2Int cell, Vector2 worldPosition, Vector2 direction, float intensity, float radius, float timestamp)
    {
        Type = type;
        Cell = cell;
        WorldPosition = worldPosition;
        Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        Intensity = Mathf.Max(0f, intensity);
        Radius = Mathf.Max(0f, radius);
        Timestamp = Mathf.Max(0f, timestamp);
    }
}

public sealed class AftermathGrid
{
    private static readonly ProfilerMarker AddHitMarker = new ProfilerMarker("SemanticWorld.Aftermath.AddCombatHit");

    private readonly float[] blood;
    private readonly float[] scorch;
    private readonly bool[] corpseOccupied;

    public AftermathGrid(MapData map, int maxResidues = 256)
    {
        Map = map;
        MaxResidues = Mathf.Max(1, maxResidues);
        int cellCount = map != null ? map.Width * map.Height : 0;
        blood = new float[cellCount];
        scorch = new float[cellCount];
        corpseOccupied = new bool[cellCount];
        Residues = new List<CombatResidue>(MaxResidues);
    }

    public MapData Map { get; }
    public int MaxResidues { get; }
    public List<CombatResidue> Residues { get; }

    public float GetBlood(Vector2Int cell)
    {
        return TryIndex(cell, out int index) ? blood[index] : 0f;
    }

    public float GetScorch(Vector2Int cell)
    {
        return TryIndex(cell, out int index) ? scorch[index] : 0f;
    }

    public bool HasCorpse(Vector2Int cell)
    {
        return TryIndex(cell, out int index) && corpseOccupied[index];
    }

    public void AddCombatHit(in DamageContext context, in HitResult result, float timestamp)
    {
        using ProfilerMarker.AutoScope scope = AddHitMarker.Auto();
        if (Map == null || !result.accepted || !result.dealtDamage)
        {
            return;
        }

        Vector2Int cell = WorldToCell(context.hitPoint);
        MapCell mapCell = Map.GetCell(cell);
        if (!mapCell.Flags.HasFlag(CellFlags.Walkable) || mapCell.Flags.HasFlag(CellFlags.SafeZone))
        {
            return;
        }

        float damageScale = Mathf.Clamp01(result.finalDamage / 40f);
        float intensity = Mathf.Max(0.1f, result.feedback.intensity) * (0.35f + damageScale);
        if (context.magicDamage > context.physicalDamage)
        {
            AddScorch(cell, context.hitPoint, context.hitDirection, intensity * 0.75f, timestamp);
        }
        else
        {
            AddBlood(cell, context.hitPoint, context.hitDirection, intensity, timestamp);
        }

        if (result.killed)
        {
            AddCorpse(cell, context.hitPoint, context.hitDirection, intensity, timestamp);
        }
    }

    public void SeedAmbientResidues(int maxGrassClusters, int maxDebris)
    {
        if (Map == null)
        {
            return;
        }

        int grassPlaced = 0;
        int debrisPlaced = 0;
        for (int i = 0; i < Map.Cells.Length; i++)
        {
            MapCell cell = Map.Cells[i];
            if (grassPlaced < maxGrassClusters && cell.Flags.HasFlag(CellFlags.GrassEligible) && cell.VegetationWeight >= 0.35f)
            {
                AddResidue(new CombatResidue(CombatResidueType.GrassCluster, cell.Coord, CellToWorld(cell.Coord), Vector2.up, cell.VegetationWeight, 0.65f, 0f));
                grassPlaced++;
            }

            if (debrisPlaced < maxDebris && cell.Flags.HasFlag(CellFlags.DebrisEligible) && cell.RegionKind != RegionKind.MainRoute)
            {
                AddResidue(new CombatResidue(CombatResidueType.Debris, cell.Coord, CellToWorld(cell.Coord), Vector2.up, 0.5f, 0.4f, 0f));
                debrisPlaced++;
            }

            if (grassPlaced >= maxGrassClusters && debrisPlaced >= maxDebris)
            {
                return;
            }
        }
    }

    private void AddBlood(Vector2Int cell, Vector2 worldPosition, Vector2 direction, float intensity, float timestamp)
    {
        if (!TryIndex(cell, out int index))
        {
            return;
        }

        blood[index] = Mathf.Clamp01(blood[index] + intensity * 0.35f);
        AddResidue(new CombatResidue(blood[index] >= 0.65f ? CombatResidueType.BloodPool : CombatResidueType.BloodDrop, cell, worldPosition, direction, intensity, 0.35f + intensity * 0.15f, timestamp));
    }

    private void AddScorch(Vector2Int cell, Vector2 worldPosition, Vector2 direction, float intensity, float timestamp)
    {
        if (!TryIndex(cell, out int index))
        {
            return;
        }

        scorch[index] = Mathf.Clamp01(scorch[index] + intensity * 0.3f);
        AddResidue(new CombatResidue(CombatResidueType.Scorch, cell, worldPosition, direction, intensity, 0.3f + intensity * 0.12f, timestamp));
    }

    private void AddCorpse(Vector2Int cell, Vector2 worldPosition, Vector2 direction, float intensity, float timestamp)
    {
        if (!TryIndex(cell, out int index) || corpseOccupied[index])
        {
            return;
        }

        MapCell mapCell = Map.GetCell(cell);
        if (mapCell.Flags.HasFlag(CellFlags.DirectorSpawnDeny) || mapCell.Flags.HasFlag(CellFlags.ResourceCandidate))
        {
            return;
        }

        corpseOccupied[index] = true;
        AddResidue(new CombatResidue(CombatResidueType.Corpse, cell, worldPosition, direction, intensity, 0.65f, timestamp));
    }

    private void AddResidue(CombatResidue residue)
    {
        if (Residues.Count >= MaxResidues)
        {
            Residues.RemoveAt(0);
        }

        Residues.Add(residue);
    }

    private bool TryIndex(Vector2Int cell, out int index)
    {
        index = -1;
        if (Map == null || !Map.IsInside(cell))
        {
            return false;
        }

        index = Map.IndexOf(cell);
        return true;
    }

    private Vector2Int WorldToCell(Vector2 worldPosition)
    {
        float x = worldPosition.x / Map.CellSize + (Map.Width - 1) * 0.5f;
        float y = worldPosition.y / Map.CellSize + (Map.Height - 1) * 0.5f;
        return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        float x = (cell.x - (Map.Width - 1) * 0.5f) * Map.CellSize;
        float y = (cell.y - (Map.Height - 1) * 0.5f) * Map.CellSize;
        return new Vector2(x, y);
    }
}

public sealed class CombatAftermathSystem : MonoBehaviour
{
    private static CombatAftermathSystem instance;

    [SerializeField] private GridRouteMapGenerator mapGenerator;
    [SerializeField] private SemanticWorldView worldView;
    [SerializeField] private int maxResidues = 256;
    [SerializeField] private int ambientGrassClusters = 32;
    [SerializeField] private int ambientDebris = 24;

    public static CombatAftermathSystem Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<CombatAftermathSystem>();
            if (instance != null)
            {
                return instance;
            }

            GameObject systemObject = new GameObject("CombatAftermathSystem");
            instance = systemObject.AddComponent<CombatAftermathSystem>();
            return instance;
        }
    }

    public static CombatAftermathSystem Existing
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            return FindObjectOfType<CombatAftermathSystem>();
        }
    }

    public AftermathGrid Grid { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnEnable()
    {
        CombatFeedbackBus.HitConfirmed += OnHitConfirmed;
    }

    private void OnDisable()
    {
        CombatFeedbackBus.HitConfirmed -= OnHitConfirmed;
    }

    public void Configure(MapData mapData)
    {
        Grid = mapData != null ? new AftermathGrid(mapData, maxResidues) : null;
        Grid?.SeedAmbientResidues(ambientGrassClusters, ambientDebris);
    }

    public void Configure(MapData mapData, SemanticWorldView view)
    {
        worldView = view;
        Configure(mapData);
    }

    public void Configure(GridRouteMapGenerator generator)
    {
        Configure(generator, null);
    }

    public void Configure(GridRouteMapGenerator generator, SemanticWorldView view)
    {
        mapGenerator = generator;
        Configure(generator != null ? generator.CurrentMapData : null, view);
    }

    private void OnHitConfirmed(DamageContext context, HitResult result)
    {
        EnsureGrid();
        Grid?.AddCombatHit(context, result, Time.time);
        NotifyDirector(context, result);
        RefreshWorldViewResidues();
    }

    private void EnsureGrid()
    {
        if (Grid != null)
        {
            return;
        }

        if (mapGenerator == null)
        {
            mapGenerator = FindObjectOfType<GridRouteMapGenerator>();
        }

        if (mapGenerator != null && mapGenerator.CurrentMapData != null)
        {
            Configure(mapGenerator, worldView);
        }
    }

    private static void NotifyDirector(in DamageContext context, in HitResult result)
    {
        if (!result.accepted)
        {
            return;
        }

        float magnitude = Mathf.Max(0.1f, result.finalDamage / 20f);
        if (result.dealtDamage)
        {
            DirectorEventType type = context.magicDamage > context.physicalDamage
                ? DirectorEventType.MagicUsed
                : DirectorEventType.LoudAttack;
            WorldHostilityDirector.Current.Notify(new DirectorEvent(type, context.hitPoint, magnitude, context.weaponType.ToString()));
        }

        if (result.killed)
        {
            WorldHostilityDirector.Current.Notify(new DirectorEvent(DirectorEventType.EnemyKilled, context.hitPoint, magnitude, context.weaponType.ToString()));
        }
    }

    private void RefreshWorldViewResidues()
    {
        if (Grid == null)
        {
            return;
        }

        if (worldView == null)
        {
            worldView = FindObjectOfType<SemanticWorldView>();
        }

        if (worldView != null)
        {
            worldView.RefreshResidues(Grid);
        }
    }
}
