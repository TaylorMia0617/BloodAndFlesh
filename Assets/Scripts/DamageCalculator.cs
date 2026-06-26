using UnityEngine;

public readonly struct DamageResult
{
    public readonly float rawDamage;
    public readonly float rawPhysicalDamage;
    public readonly float rawMagicDamage;
    public readonly float armorReducedDamage;
    public readonly float physicalDamage;
    public readonly float magicDamage;
    public readonly float finalDamage;
    public readonly bool isCritical;

    public DamageResult(float rawDamage, float armorReducedDamage, float finalDamage, bool isCritical)
        : this(rawDamage, 0f, armorReducedDamage, 0f, isCritical)
    {
    }

    public DamageResult(float rawPhysicalDamage, float rawMagicDamage, float physicalDamage, float magicDamage, bool isCritical)
    {
        this.rawPhysicalDamage = Mathf.Max(0f, rawPhysicalDamage);
        this.rawMagicDamage = Mathf.Max(0f, rawMagicDamage);
        this.physicalDamage = Mathf.Max(0f, physicalDamage);
        this.magicDamage = Mathf.Max(0f, magicDamage);
        this.rawDamage = this.rawPhysicalDamage + this.rawMagicDamage;
        this.armorReducedDamage = this.physicalDamage + this.magicDamage;
        this.finalDamage = this.physicalDamage + this.magicDamage;
        this.isCritical = isCritical;
    }
}

public static class DamageCalculator
{
    public static DamageResult Calculate(CharacterStats attacker, CharacterStats defender, float physicalSourceDamage, float magicSourceDamage = 0f, float armorPiercing = 0f, bool canCrit = true)
    {
        float attackerPhysicalAttack = attacker != null ? Mathf.Max(0f, attacker.physicalAttack + attacker.attack) : 0f;
        float attackerMagicAttack = attacker != null ? Mathf.Max(0f, attacker.magicAttack) : 0f;
        float damageMultiplier = attacker != null ? Mathf.Max(0f, attacker.damageMultiplier) : 1f;
        float rawPhysicalDamage = (attackerPhysicalAttack + Mathf.Max(0f, physicalSourceDamage)) * damageMultiplier;
        float rawMagicDamage = (attackerMagicAttack + Mathf.Max(0f, magicSourceDamage)) * damageMultiplier;
        float physicalDamage = CalculatePhysicalDamage(rawPhysicalDamage, defender, armorPiercing);
        float magicDamage = CalculateMagicDamage(rawMagicDamage, defender);

        bool isCritical = false;
        if (canCrit && attacker != null && attacker.critChance > 0f && Random.value < Mathf.Clamp01(attacker.critChance))
        {
            isCritical = true;
            float criticalMultiplier = 1f + Mathf.Max(0f, attacker.critDamage);
            physicalDamage *= criticalMultiplier;
            magicDamage *= criticalMultiplier;
        }

        return new DamageResult(rawPhysicalDamage, rawMagicDamage, physicalDamage, magicDamage, isCritical);
    }

    private static float CalculatePhysicalDamage(float rawDamage, CharacterStats defender, float armorPiercing)
    {
        if (rawDamage <= 0f)
        {
            return 0f;
        }

        float armor = defender != null ? Mathf.Max(0f, defender.armor) : 0f;
        float effectiveArmor = Mathf.Max(0f, armor - Mathf.Max(0f, armorPiercing));
        return Mathf.Max(1f, rawDamage - effectiveArmor);
    }

    private static float CalculateMagicDamage(float rawDamage, CharacterStats defender)
    {
        if (rawDamage <= 0f)
        {
            return 0f;
        }

        float magicImmunity = defender != null ? Mathf.Clamp01(defender.magicImmunity) : 0f;
        return rawDamage * (1f - magicImmunity);
    }
}
