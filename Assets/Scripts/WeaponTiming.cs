using UnityEngine;

public static class WeaponTiming
{
    public static float GetWindup(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return 0f;
        }

        if (weapon.weaponType == WeaponType.Sword)
        {
            return 0.035f;
        }

        if (weapon.weaponType == WeaponType.Spell)
        {
            return Mathf.Max(0.05f, weapon.windup * 0.7f);
        }

        return Mathf.Max(0.01f, weapon.windup);
    }

    public static float GetActive(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return 0f;
        }

        switch (weapon.weaponType)
        {
            case WeaponType.Knife:
                return 0.13f;
            case WeaponType.Sword:
                return 0.055f;
            case WeaponType.Spear when weapon.useSweepArc:
                return 0.42f;
            case WeaponType.Spear:
                return 0.15f;
            case WeaponType.Spell:
                return 0.48f;
            default:
                return 0.12f;
        }
    }

    public static float GetRecovery(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return 0f;
        }

        if (weapon.weaponType == WeaponType.Sword)
        {
            return 0.035f;
        }

        return weapon.weaponType == WeaponType.Spell ? 0.24f : Mathf.Max(0.04f, weapon.recovery * 0.45f);
    }
}
