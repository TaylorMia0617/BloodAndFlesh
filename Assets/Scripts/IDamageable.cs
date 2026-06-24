using UnityEngine;

public interface IDamageable
{
    HitResult ApplyDamage(in DamageContext context);
    void TakeDamage(float amount, float armorPiercing = 0f, Vector2 hitSource = default, CharacterStats attacker = null);
}
