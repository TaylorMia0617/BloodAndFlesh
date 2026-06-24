public static class EnemyBehaviorResolver
{
    public static EnemyBehaviorKind Resolve(EnemyArchetype archetype)
    {
        switch (archetype)
        {
            case EnemyArchetype.Sentinel:
                return EnemyBehaviorKind.Sentinel;
            case EnemyArchetype.Attacker:
            case EnemyArchetype.Shield:
                return EnemyBehaviorKind.Melee;
            case EnemyArchetype.Ranged:
                return EnemyBehaviorKind.Ranged;
            default:
                return EnemyBehaviorKind.Generic;
        }
    }
}
