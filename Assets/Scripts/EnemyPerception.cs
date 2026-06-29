using UnityEngine;

public static class EnemyPerception
{
    public static bool HasLineOfSight(GridRouteMapGenerator mapGenerator, Vector2 from, Vector2 to, float distance, float step)
    {
        if (distance <= 0.001f)
        {
            return true;
        }

        return SemanticVisionQuery.HasLineOfSight(mapGenerator, from, to, distance, step);
    }

    public static float GetVisionBlockedDistance(GridRouteMapGenerator mapGenerator, Vector2 origin, Vector2 direction, float maxDistance, float step)
    {
        return SemanticVisionQuery.GetBlockedDistance(mapGenerator, origin, direction, maxDistance, step);
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
