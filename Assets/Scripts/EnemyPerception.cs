using UnityEngine;

public static class EnemyPerception
{
    public static bool HasLineOfSight(GridRouteMapGenerator mapGenerator, Vector2 from, Vector2 to, float distance, float step)
    {
        if (distance <= 0.001f)
        {
            return true;
        }

        Vector2 direction = (to - from) / distance;
        float blockedDistance = GetVisionBlockedDistance(mapGenerator, from, direction, distance, step);
        return blockedDistance >= distance - Mathf.Max(0.04f, step * 1.5f);
    }

    public static float GetVisionBlockedDistance(GridRouteMapGenerator mapGenerator, Vector2 origin, Vector2 direction, float maxDistance, float step)
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
            if (mapGenerator.BlocksVision(mapGenerator.WorldToGrid(sample)))
            {
                return Mathf.Max(0f, distance - safeStep);
            }
        }

        return maxDistance;
    }

    public static bool IsVisionBlocker(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.GetComponent<ObstacleHitFeedback>() != null || hit.GetComponentInParent<ObstacleHitFeedback>() != null)
        {
            return true;
        }

        string objectName = hit.gameObject.name;
        return objectName.StartsWith("Obstacle_") || objectName.StartsWith("Wall_");
    }
}
