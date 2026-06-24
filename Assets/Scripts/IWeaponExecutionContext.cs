using UnityEngine;

public interface IWeaponExecutionContext
{
    bool ExecutePrimaryAttack(WeaponDefinition weapon, Vector2 direction);
    bool ExecuteKnifeBlock(WeaponDefinition weapon, Vector2 direction);
    bool ExecuteSwordDash(WeaponDefinition weapon, Vector2 direction);
    bool ExecuteSpearSweep(WeaponDefinition weapon, Vector2 direction);
    bool ExecuteSpellShield(WeaponDefinition weapon, Vector2 direction);
}
