using UnityEngine;

public readonly struct DamageResult
{
    public readonly float rawDamage;
    public readonly float armorReducedDamage;
    public readonly float finalDamage;
    public readonly bool isCritical;

    public DamageResult(float rawDamage, float armorReducedDamage, float finalDamage, bool isCritical)
    {
        this.rawDamage = rawDamage;
        this.armorReducedDamage = armorReducedDamage;
        this.finalDamage = finalDamage;
        this.isCritical = isCritical;
    }
}

public static class DamageCalculator
{
    public static DamageResult Calculate(CharacterStats attacker, CharacterStats defender, float sourceDamage, float armorPiercing = 0f)
    {
        float attackerBaseAttack = attacker != null ? Mathf.Max(0f, attacker.attack) : 0f;
        float damageMultiplier = attacker != null ? Mathf.Max(0f, attacker.damageMultiplier) : 1f;
        float rawDamage = (attackerBaseAttack + Mathf.Max(0f, sourceDamage)) * damageMultiplier;

        float armor = defender != null ? Mathf.Max(0f, defender.armor) : 0f;
        float effectiveArmor = Mathf.Max(0f, armor - Mathf.Max(0f, armorPiercing));
        float armorReducedDamage = Mathf.Max(1f, rawDamage - effectiveArmor);

        bool isCritical = false;
        float finalDamage = armorReducedDamage;
        if (attacker != null && attacker.critChance > 0f && Random.value < Mathf.Clamp01(attacker.critChance))
        {
            isCritical = true;
            finalDamage *= 1f + Mathf.Max(0f, attacker.critDamage);
        }

        return new DamageResult(rawDamage, armorReducedDamage, finalDamage, isCritical);
    }
}
