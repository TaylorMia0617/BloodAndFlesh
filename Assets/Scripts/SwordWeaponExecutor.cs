using UnityEngine;

public sealed class SwordWeaponExecutor : IWeaponExecutor
{
    public bool TryExecute(IWeaponExecutionContext context, WeaponDefinition weapon, WeaponActionKind actionKind, Vector2 direction)
    {
        if (context == null || weapon == null)
        {
            return false;
        }

        return actionKind == WeaponActionKind.Special ? context.ExecuteSwordDash(weapon, direction) : context.ExecutePrimaryAttack(weapon, direction);
    }
}
