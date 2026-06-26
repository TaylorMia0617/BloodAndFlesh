using UnityEngine;
using System;

public class CharacterStats : MonoBehaviour
{
    [Header("Core")]
    public float maxHealth = 70f;
    public float armor = 0f;
    [Range(0f, 1f)] public float magicImmunity = 0f;
    public float moveSpeed = 3f;
    public float moveDelay = 0.15f;

    [Header("Combat")]
    public float physicalAttack = 0f;
    public float magicAttack = 0f;
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

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnManaChanged;
    public event Action<DamageContext, HitResult> OnDamaged;
    public event Action<CharacterStats> OnDeath;

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
        OnHealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, maxHealth));
        OnManaChanged?.Invoke(CurrentMana, Mathf.Max(0f, maxMana));
    }

    public float ApplyDamage(float amount, float armorPiercing = 0f, CharacterStats attacker = null)
    {
        return ApplyDamageDetailed(amount, armorPiercing, attacker).finalDamage;
    }

    public DamageResult ApplyDamageDetailed(float amount, float armorPiercing = 0f, CharacterStats attacker = null, bool canCrit = true)
    {
        if (IsDamageImmune || IsUntargetable)
        {
            return new DamageResult(0f, 0f, 0f, false);
        }

        DamageResult result = DamageCalculator.Calculate(attacker, this, amount, 0f, armorPiercing, canCrit);
        bool wasAlive = IsAlive;
        ApplyDamageResult(result, out bool killed);
        if (wasAlive && killed)
        {
            OnDeath?.Invoke(this);
        }

        return result;
    }

    public HitResult ApplyDamage(in DamageContext context)
    {
        if (IsDamageImmune || IsUntargetable)
        {
            return HitResult.Rejected;
        }

        DamageResult damage = DamageCalculator.Calculate(context.attackerStats, this, context.physicalDamage, context.magicDamage, context.armorPiercing, context.canCrit);
        bool wasAlive = IsAlive;
        ApplyDamageResult(damage, out bool killed);
        if (wasAlive && !IsAlive)
        {
            OnDeath?.Invoke(this);
        }

        bool dealtDamage = damage.finalDamage > 0f;
        HitResult result = new HitResult(true, dealtDamage, wasAlive && killed, damage.isCritical, damage.finalDamage, context.feedback);
        OnDamaged?.Invoke(context, result);
        return result;
    }

    private void ApplyDamageResult(DamageResult damage, out bool killed)
    {
        bool wasAlive = IsAlive;
        if (damage.physicalDamage > 0f)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage.physicalDamage);
            OnHealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, maxHealth));
        }

        if (IsAlive && damage.magicDamage > 0f)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage.magicDamage);
            OnHealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, maxHealth));
        }

        killed = wasAlive && !IsAlive;
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
        OnManaChanged?.Invoke(CurrentMana, Mathf.Max(0f, maxMana));
        return true;
    }

    public void RestoreMana(float amount)
    {
        if (!usesMana)
        {
            return;
        }

        CurrentMana = Mathf.Min(Mathf.Max(0f, maxMana), CurrentMana + Mathf.Max(0f, amount));
        OnManaChanged?.Invoke(CurrentMana, Mathf.Max(0f, maxMana));
    }

    public void RestoreHealth(float amount)
    {
        CurrentHealth = Mathf.Min(Mathf.Max(1f, maxHealth), CurrentHealth + Mathf.Max(0f, amount));
        OnHealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, maxHealth));
    }

    public void ModifyMaxHealth(float delta, bool healByDelta = true)
    {
        float previousMax = Mathf.Max(1f, maxHealth);
        maxHealth = Mathf.Max(1f, maxHealth + delta);
        if (healByDelta && delta > 0f)
        {
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + delta);
        }
        else if (maxHealth < previousMax)
        {
            CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        }

        OnHealthChanged?.Invoke(CurrentHealth, Mathf.Max(1f, maxHealth));
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
