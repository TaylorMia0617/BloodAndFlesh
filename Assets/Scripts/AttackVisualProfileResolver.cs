using UnityEngine;

public static class AttackVisualProfileResolver
{
    public static AttackVisualProfile Resolve(WeaponDefinition weapon, float visualMultiplier)
    {
        if (weapon == null)
        {
            return new AttackVisualProfile(WeaponType.Knife, 0.2f, 0.25f, 0f, 0.14f, false);
        }

        bool isSpearSweep = weapon.weaponType == WeaponType.Spear && weapon.useSweepArc;
        WeaponType visualType = weapon.useSweepArc ? WeaponType.Knife : weapon.weaponType;
        float range = GetVisualRange(weapon, visualMultiplier);
        float width = GetVisualWidth(weapon, visualMultiplier);
        return new AttackVisualProfile(
            visualType,
            range,
            width,
            GetShapeValue(visualType),
            GetThickness(visualType, visualMultiplier),
            isSpearSweep);
    }

    private static float GetVisualRange(WeaponDefinition weapon, float visualMultiplier)
    {
        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                return Mathf.Max(0.78f, weapon.attackRange * 0.82f * visualMultiplier);
            case WeaponType.Spear when weapon.useSweepArc:
                return Mathf.Max(1.45f, weapon.attackRange * 1.05f * visualMultiplier);
            case WeaponType.Sword:
                return Mathf.Max(1.7f, weapon.attackRange * 1.35f * visualMultiplier);
            case WeaponType.Spear:
                return Mathf.Max(2.65f, weapon.attackRange * 1.58f * visualMultiplier);
            case WeaponType.Spell:
                return Mathf.Max(4.1f, weapon.attackRange * 2.05f * visualMultiplier);
            default:
                return Mathf.Max(0.2f, weapon.attackRange * visualMultiplier);
        }
    }

    private static float GetVisualWidth(WeaponDefinition weapon, float visualMultiplier)
    {
        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                return Mathf.Max(0.82f, weapon.attackRadius * 2.35f * visualMultiplier);
            case WeaponType.Spear when weapon.useSweepArc:
                return Mathf.Max(1.7f, weapon.attackRadius * 3.8f * visualMultiplier);
            case WeaponType.Sword:
                return Mathf.Max(1.08f, weapon.attackRadius * 2.75f * visualMultiplier);
            case WeaponType.Spear:
                return Mathf.Max(0.52f, weapon.attackRadius * 1.42f * visualMultiplier);
            case WeaponType.Spell:
                return Mathf.Max(0.88f, weapon.attackRadius * 1.32f * visualMultiplier);
            default:
                return Mathf.Max(0.25f, weapon.attackRadius * 2.6f * visualMultiplier);
        }
    }

    private static float GetShapeValue(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Knife:
                return 0f;
            case WeaponType.Sword:
                return 1f;
            case WeaponType.Spear:
                return 2f;
            case WeaponType.Spell:
                return 3f;
            default:
                return 0f;
        }
    }

    private static float GetThickness(WeaponType weaponType, float visualMultiplier)
    {
        switch (weaponType)
        {
            case WeaponType.Knife:
                return Mathf.Lerp(0.05f, 0.08f, Mathf.Clamp01(visualMultiplier - 1f));
            case WeaponType.Sword:
                return 0.26f;
            case WeaponType.Spear:
                return 0.12f;
            case WeaponType.Spell:
                return 0.22f;
            default:
                return 0.14f;
        }
    }
}
