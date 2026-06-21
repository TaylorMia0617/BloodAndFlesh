using System.Collections.Generic;

public static class EnemyRegistry
{
    private static readonly List<SimpleEnemyAI> enemies = new List<SimpleEnemyAI>();

    public static IReadOnlyList<SimpleEnemyAI> Enemies => enemies;

    public static int LivingCount
    {
        get
        {
            Prune();
            return enemies.Count;
        }
    }

    public static void Register(SimpleEnemyAI enemy)
    {
        if (enemy != null && !enemies.Contains(enemy))
        {
            enemies.Add(enemy);
        }
    }

    public static void Unregister(SimpleEnemyAI enemy)
    {
        enemies.Remove(enemy);
    }

    public static void Prune()
    {
        enemies.RemoveAll(enemy => enemy == null || !enemy.isActiveAndEnabled);
    }

    public static void Clear()
    {
        enemies.Clear();
    }
}
