using UnityEngine;

public sealed class PrototypeWeaponExecutor : IWeaponExecutor
{
    private readonly WeaponExecutorRegistry registry = new WeaponExecutorRegistry();

    public bool TryExecute(IWeaponExecutionContext context, WeaponDefinition weapon, WeaponActionKind actionKind, Vector2 direction)
    {
        if (context == null || weapon == null)
        {
            return false;
        }

        return registry.Resolve(weapon.weaponType).TryExecute(context, weapon, actionKind, direction);
    }
}
