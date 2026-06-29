using UnityEngine;

public static class SemanticVisionQuery
{
    public static bool BlocksVision(GridRouteMapGenerator mapGenerator, Vector2Int cell)
    {
        if (mapGenerator == null || !mapGenerator.IsGridInside(cell))
        {
            return true;
        }

        MapData map = mapGenerator.CurrentMapData;
        if (map != null && map.IsInside(cell))
        {
            return map.GetCell(cell).Flags.HasFlag(CellFlags.BlocksVision);
        }

        return mapGenerator.LegacyBlocksVision(cell);
    }

    public static bool HasLineOfSight(GridRouteMapGenerator mapGenerator, Vector2 from, Vector2 to, float distance, float step)
    {
        if (distance <= 0.001f)
        {
            return true;
        }

        Vector2 direction = (to - from) / distance;
        float blockedDistance = GetBlockedDistance(mapGenerator, from, direction, distance, step);
        return blockedDistance >= distance - Mathf.Max(0.04f, step * 1.5f);
    }

    public static float GetBlockedDistance(GridRouteMapGenerator mapGenerator, Vector2 origin, Vector2 direction, float maxDistance, float step)
    {
        if (mapGenerator == null || direction.sqrMagnitude <= 0.001f)
        {
            return maxDistance;
        }

        float safeStep = Mathf.Max(0.02f, step);
        float distance = 0f;
        Vector2 normalized = direction.normalized;
        while (distance < maxDistance)
        {
            distance += safeStep;
            Vector2 sample = origin + normalized * distance;
            Vector2Int cell = mapGenerator.WorldToGrid(sample);
            if (BlocksVision(mapGenerator, cell))
            {
                return Mathf.Max(0f, distance - safeStep);
            }
        }

        return maxDistance;
    }
}
