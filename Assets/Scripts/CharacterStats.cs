using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Core")]
    public float maxHealth = 70f;
    public float armor = 0f;
    public float moveSpeed = 3f;
    public float moveDelay = 0.15f;

    [Header("Combat")]
    public float attack = 0f;
    public float damageMultiplier = 1f;
    [Range(0f, 1f)] public float critChance = 0f;
    public float critDamage = 0.5f;

    [Header("Player")]
    public bool usesMana;
    public float maxMana = 50f;

    public float CurrentHealth { get; private set; }
    public float CurrentMana { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsDamageImmune => Time.time < damageImmuneUntil;
    public bool IsUntargetable => Time.time < untargetableUntil;

    private float damageImmuneUntil;
    private float untargetableUntil;

    private void Awake()
    {
        ResetStats();
    }

    public void ResetStats()
    {
        CurrentHealth = Mathf.Max(1f, maxHealth);
        CurrentMana = usesMana ? Mathf.Max(0f, maxMana) : 0f;
        damageImmuneUntil = 0f;
        untargetableUntil = 0f;
    }

    public float ApplyDamage(float amount, float armorPiercing = 0f, CharacterStats attacker = null)
    {
        if (IsDamageImmune || IsUntargetable)
        {
            return 0f;
        }

        DamageResult result = DamageCalculator.Calculate(attacker, this, amount, armorPiercing);
        CurrentHealth = Mathf.Max(0f, CurrentHealth - result.finalDamage);
        return result.finalDamage;
    }

    public bool TrySpendMana(float amount)
    {
        if (!usesMana)
        {
            return false;
        }

        float cost = Mathf.Max(0f, amount);
        if (CurrentMana + 0.001f < cost)
        {
            return false;
        }

        CurrentMana = Mathf.Max(0f, CurrentMana - cost);
        return true;
    }

    public void RestoreMana(float amount)
    {
        if (!usesMana)
        {
            return;
        }

        CurrentMana = Mathf.Min(Mathf.Max(0f, maxMana), CurrentMana + Mathf.Max(0f, amount));
    }

    public float GetManaCrystalValue(int crystalCount = 3)
    {
        return usesMana ? Mathf.Max(0.001f, maxMana / Mathf.Max(1, crystalCount)) : 0f;
    }

    public void AddDamageImmunity(float duration)
    {
        damageImmuneUntil = Mathf.Max(damageImmuneUntil, Time.time + Mathf.Max(0f, duration));
    }

    public void AddUntargetable(float duration)
    {
        untargetableUntil = Mathf.Max(untargetableUntil, Time.time + Mathf.Max(0f, duration));
        AddDamageImmunity(duration);
    }
}
