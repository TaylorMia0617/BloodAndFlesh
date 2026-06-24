using UnityEngine;

public sealed class PrimaryOnlyWeaponExecutor : IWeaponExecutor
{
    public bool TryExecute(IWeaponExecutionContext context, WeaponDefinition weapon, WeaponActionKind actionKind, Vector2 direction)
    {
        return context != null && weapon != null && actionKind == WeaponActionKind.Primary && context.ExecutePrimaryAttack(weapon, direction);
    }
}
