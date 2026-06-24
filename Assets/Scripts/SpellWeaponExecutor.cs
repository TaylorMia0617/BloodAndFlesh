using UnityEngine;

public sealed class SpellWeaponExecutor : IWeaponExecutor
{
    public bool TryExecute(IWeaponExecutionContext context, WeaponDefinition weapon, WeaponActionKind actionKind, Vector2 direction)
    {
        if (context == null || weapon == null)
        {
            return false;
        }

        return actionKind == WeaponActionKind.Special ? context.ExecuteSpellShield(weapon, direction) : context.ExecutePrimaryAttack(weapon, direction);
    }
}
