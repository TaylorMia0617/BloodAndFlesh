using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount, float armorPiercing = 0f, Vector2 hitSource = default, CharacterStats attacker = null);
}
