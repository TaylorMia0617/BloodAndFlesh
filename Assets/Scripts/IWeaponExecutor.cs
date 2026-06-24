using UnityEngine;

public interface IWeaponExecutor
{
    bool TryExecute(IWeaponExecutionContext context, WeaponDefinition weapon, WeaponActionKind actionKind, Vector2 direction);
}
