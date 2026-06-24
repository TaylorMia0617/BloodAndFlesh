using UnityEngine;

public sealed class KnifeWeaponExecutor : IWeaponExecutor
{
    public bool TryExecute(IWeaponExecutionContext context, WeaponDefinition weapon, WeaponActionKind actionKind, Vector2 direction)
    {
        if (context == null || weapon == null)
        {
            return false;
        }

        return actionKind == WeaponActionKind.Special ? context.ExecuteKnifeBlock(weapon, direction) : context.ExecutePrimaryAttack(weapon, direction);
    }
}
