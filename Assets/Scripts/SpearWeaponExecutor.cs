using UnityEngine;

public sealed class SpearWeaponExecutor : IWeaponExecutor
{
    public bool TryExecute(IWeaponExecutionContext context, WeaponDefinition weapon, WeaponActionKind actionKind, Vector2 direction)
    {
        if (context == null || weapon == null)
        {
            return false;
        }

        return actionKind == WeaponActionKind.Special ? context.ExecuteSpearSweep(weapon, direction) : context.ExecutePrimaryAttack(weapon, direction);
    }
}
