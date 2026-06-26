using UnityEngine;

public readonly struct DamageContext
{
    public readonly int attackId;
    public readonly GameObject instigator;
    public readonly CharacterStats attackerStats;
    public readonly WeaponType weaponType;
    public readonly Vector2 origin;
    public readonly Vector2 hitPoint;
    public readonly Vector2 hitDirection;
    public readonly float physicalDamage;
    public readonly float magicDamage;
    public readonly float armorPiercing;
    public readonly FeedbackPayload feedback;
    public readonly bool canCrit;
    public float baseDamage => physicalDamage + magicDamage;

    public DamageContext(
        int attackId,
        GameObject instigator,
        CharacterStats attackerStats,
        WeaponType weaponType,
        Vector2 origin,
        Vector2 hitPoint,
        Vector2 hitDirection,
        float physicalDamage,
        float magicDamage,
        float armorPiercing,
        FeedbackPayload feedback,
        bool canCrit = true)
    {
        this.attackId = attackId;
        this.instigator = instigator;
        this.attackerStats = attackerStats;
        this.weaponType = weaponType;
        this.origin = origin;
        this.hitPoint = hitPoint;
        this.hitDirection = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : Vector2.up;
        this.physicalDamage = Mathf.Max(0f, physicalDamage);
        this.magicDamage = Mathf.Max(0f, magicDamage);
        this.armorPiercing = Mathf.Max(0f, armorPiercing);
        this.feedback = feedback;
        this.canCrit = canCrit;
    }

    public static DamageContext Legacy(float amount, float armorPiercing, Vector2 hitSource, CharacterStats attacker)
    {
        Vector2 direction = hitSource.sqrMagnitude > 0.001f ? -hitSource.normalized : Vector2.up;
        return new DamageContext(0, attacker != null ? attacker.gameObject : null, attacker, WeaponType.Knife, hitSource, hitSource, direction, amount, 0f, armorPiercing, FeedbackPayload.None);
    }
}
