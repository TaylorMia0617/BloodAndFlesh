using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class WeaponConfig
{
    public WeaponType weaponType;
    public string displayName;
    public string spriteResource;
    public float damage;
    public float armorPiercing;
    public float attackRange;
    public float attackRadius;
    public float cooldown;
    public float windup;
    public float recovery;
    public float hitStopDuration;
    public float knockbackDistance;
    public float knockbackDuration;
    public float feedbackIntensity;
    public float cameraShake;
    public bool blocksMovementDuringAttack;
    public string description;
}

public static class WeaponConfigDatabase
{
    private const string ResourcePath = "Configs/weapon_config";
    private static readonly Dictionary<WeaponType, WeaponConfig> configs = new Dictionary<WeaponType, WeaponConfig>();
    private static bool loaded;

    public static WeaponConfig Get(WeaponType type)
    {
        EnsureLoaded();
        return configs.TryGetValue(type, out WeaponConfig config) ? config : CreateFallbackByType(type);
    }

    public static void ApplyTo(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return;
        }

        WeaponConfig config = Get(weapon.weaponType);
        weapon.displayName = config.displayName;
        weapon.damage = config.damage;
        weapon.armorPiercing = config.armorPiercing;
        weapon.attackRange = config.attackRange;
        weapon.attackRadius = config.attackRadius;
        weapon.cooldown = config.cooldown;
        weapon.windup = config.windup;
        weapon.recovery = config.recovery;
        weapon.hitStopDuration = config.hitStopDuration;
        weapon.knockbackDistance = config.knockbackDistance;
        weapon.knockbackDuration = config.knockbackDuration;
        weapon.feedbackIntensity = config.feedbackIntensity;
        weapon.cameraShake = config.cameraShake;
        weapon.blocksMovementDuringAttack = config.blocksMovementDuringAttack;
        weapon.description = config.description;

        Sprite sprite = !string.IsNullOrEmpty(config.spriteResource) ? Resources.Load<Sprite>(config.spriteResource) : null;
        if (sprite != null)
        {
            weapon.weaponSprite = sprite;
        }
    }

    public static WeaponDefinition CreateRuntimeDefinition(WeaponType type)
    {
        WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.weaponType = type;
        weapon.targetLayers = Physics2D.DefaultRaycastLayers;
        weapon.hideFlags = HideFlags.HideAndDontSave;
        ApplyTo(weapon);
        return weapon;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        configs.Clear();
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            LoadFallbacks();
            return;
        }

        string[] lines = asset.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            string[] cells = line.Split(',');
            if (cells.Length < 17 || !Enum.TryParse(cells[0], out WeaponType type))
            {
                Debug.LogWarning($"Weapon config row {i + 1} is invalid.");
                continue;
            }

            configs[type] = new WeaponConfig
            {
                weaponType = type,
                displayName = cells[1],
                spriteResource = cells[2],
                damage = ParseFloat(cells[3], 10f),
                armorPiercing = ParseFloat(cells[4], 0f),
                attackRange = ParseFloat(cells[5], 1f),
                attackRadius = ParseFloat(cells[6], 0.4f),
                cooldown = ParseFloat(cells[7], 0.6f),
                windup = ParseFloat(cells[8], 0.15f),
                recovery = ParseFloat(cells[9], 0.2f),
                hitStopDuration = ParseFloat(cells[10], 0.035f),
                knockbackDistance = ParseFloat(cells[11], 1f),
                knockbackDuration = ParseFloat(cells[12], 0.12f),
                feedbackIntensity = ParseFloat(cells[13], 1.4f),
                cameraShake = ParseFloat(cells[14], 0f),
                blocksMovementDuringAttack = ParseBool(cells[15], true),
                description = cells[16]
            };
        }

        if (configs.Count == 0)
        {
            LoadFallbacks();
        }
    }

    private static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static bool ParseBool(string value, bool fallback)
    {
        return bool.TryParse(value, out bool parsed) ? parsed : fallback;
    }

    private static void LoadFallbacks()
    {
        configs[WeaponType.Knife] = CreateFallbackByType(WeaponType.Knife);
        configs[WeaponType.Sword] = CreateFallbackByType(WeaponType.Sword);
        configs[WeaponType.Spear] = CreateFallbackByType(WeaponType.Spear);
        configs[WeaponType.Spell] = CreateFallbackByType(WeaponType.Spell);
    }

    private static WeaponConfig CreateFallbackByType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sword:
                return CreateFallback(WeaponType.Sword, "Sword", "Arts/Weapons/weapon_sword", 16f, 2.5f, 1.6f, 0.55f, 0.85f, 0.22f, 0.28f, "Fast lightning thrust.");
            case WeaponType.Spear:
                return CreateFallback(WeaponType.Spear, "Spear", "Arts/Weapons/weapon_spear", 13f, 3.5f, 2.1f, 0.35f, 0.75f, 0.18f, 0.24f, "Long piercing lunge.");
            case WeaponType.Spell:
                return CreateFallback(WeaponType.Spell, "Spell", "Arts/Weapons/weapon_spell_orb", 18f, 1f, 2.6f, 0.65f, 1f, 0.3f, 0.35f, "Mana orb projectile.");
            default:
                return CreateFallback(WeaponType.Knife, "Knife", "Arts/Weapons/weapon_knife", 10f, 1.5f, 1.2f, 0.45f, 0.6f, 0.15f, 0.2f, "Long blade crescent slash.");
        }
    }

    private static WeaponConfig CreateFallback(WeaponType type, string displayName, string spriteResource, float damage, float armorPiercing, float range, float radius, float cooldown, float windup, float recovery, string description)
    {
        return new WeaponConfig
        {
            weaponType = type,
            displayName = displayName,
            spriteResource = spriteResource,
            damage = damage,
            armorPiercing = armorPiercing,
            attackRange = range,
            attackRadius = radius,
            cooldown = cooldown,
            windup = windup,
            recovery = recovery,
            hitStopDuration = 0.035f,
            knockbackDistance = 1f,
            knockbackDuration = 0.12f,
            feedbackIntensity = 1.4f,
            cameraShake = 0f,
            blocksMovementDuringAttack = true,
            description = description
        };
    }
}
