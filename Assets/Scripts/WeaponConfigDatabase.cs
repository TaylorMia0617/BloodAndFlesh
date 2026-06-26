using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class WeaponConfig
{
    public WeaponType weaponType;
    public string displayName;
    public string spriteResource;
    public float physicalDamage;
    public float magicDamage;
    public float armorPiercing;
    public float attackRange;
    public float attackRadius;
    public float cooldown;
    public float windup;
    public float active;
    public float recovery;
    public float inputBuffer;
    public float hitStopDuration;
    public float hitStopTimeScale;
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
        weapon.physicalDamage = config.physicalDamage;
        weapon.magicDamage = config.magicDamage;
        weapon.armorPiercing = config.armorPiercing;
        weapon.attackRange = config.attackRange;
        weapon.attackRadius = config.attackRadius;
        weapon.cooldown = config.cooldown;
        weapon.windup = config.windup;
        weapon.recovery = config.recovery;
        weapon.hitStopDuration = config.hitStopDuration;
        weapon.hitStopTimeScale = config.hitStopTimeScale;
        weapon.knockbackDistance = config.knockbackDistance;
        weapon.knockbackDuration = config.knockbackDuration;
        weapon.feedbackIntensity = config.feedbackIntensity;
        weapon.cameraShake = config.cameraShake;
        weapon.primaryAction = new AttackActionData
        {
            useCustomTiming = true,
            windup = config.windup,
            active = config.active,
            recovery = config.recovery,
            inputBuffer = config.inputBuffer,
            hitStopDuration = config.hitStopDuration,
            hitStopTimeScale = config.hitStopTimeScale,
            knockbackDistance = config.knockbackDistance,
            knockbackDuration = config.knockbackDuration,
            cameraShake = config.cameraShake
        };
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

            bool hasSplitDamageColumns = cells.Length >= 21;
            int offset = hasSplitDamageColumns ? 1 : 0;
            bool hasActionColumns = cells.Length >= 20 + offset;
            float legacyDamage = ParseFloat(cells[3], 10f);

            configs[type] = new WeaponConfig
            {
                weaponType = type,
                displayName = cells[1],
                spriteResource = cells[2],
                physicalDamage = hasSplitDamageColumns ? ParseFloat(cells[3], 10f) : GetLegacyPhysicalDamage(type, legacyDamage),
                magicDamage = hasSplitDamageColumns ? ParseFloat(cells[4], 0f) : GetLegacyMagicDamage(type, legacyDamage),
                armorPiercing = ParseFloat(cells[4 + offset], 0f),
                attackRange = ParseFloat(cells[5 + offset], 1f),
                attackRadius = ParseFloat(cells[6 + offset], 0.4f),
                cooldown = ParseFloat(cells[7 + offset], 0.6f),
                windup = ParseFloat(cells[8 + offset], 0.15f),
                active = hasActionColumns ? ParseFloat(cells[9 + offset], GetLegacyActive(type, false)) : GetLegacyActive(type, false),
                recovery = ParseFloat(cells[hasActionColumns ? 10 + offset : 9 + offset], 0.2f),
                inputBuffer = hasActionColumns ? ParseFloat(cells[11 + offset], GetDefaultInputBuffer(type)) : GetDefaultInputBuffer(type),
                hitStopDuration = ParseFloat(cells[hasActionColumns ? 12 + offset : 10 + offset], 0.035f),
                hitStopTimeScale = hasActionColumns ? ParseFloat(cells[13 + offset], 0.05f) : 0.05f,
                knockbackDistance = ParseFloat(cells[hasActionColumns ? 14 + offset : 11 + offset], 1f),
                knockbackDuration = ParseFloat(cells[hasActionColumns ? 15 + offset : 12 + offset], 0.12f),
                feedbackIntensity = ParseFloat(cells[hasActionColumns ? 16 + offset : 13 + offset], 1.4f),
                cameraShake = ParseFloat(cells[hasActionColumns ? 17 + offset : 14 + offset], 0f),
                blocksMovementDuringAttack = ParseBool(cells[hasActionColumns ? 18 + offset : 15 + offset], true),
                description = cells[hasActionColumns ? 19 + offset : 16 + offset]
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
                return CreateFallback(WeaponType.Sword, "Sword", "Arts/Weapons/weapon_sword", 16f, 0f, 2.5f, 1.6f, 0.55f, 0.85f, 0.22f, 0.28f, "Fast lightning thrust.");
            case WeaponType.Spear:
                return CreateFallback(WeaponType.Spear, "Spear", "Arts/Weapons/weapon_spear", 13f, 0f, 3.5f, 2.1f, 0.35f, 0.75f, 0.18f, 0.24f, "Long piercing lunge.");
            case WeaponType.Spell:
                return CreateFallback(WeaponType.Spell, "Spell", "Arts/Weapons/weapon_spell_orb", 0f, 18f, 1f, 2.6f, 0.65f, 1f, 0.3f, 0.35f, "Mana orb projectile.");
            default:
                return CreateFallback(WeaponType.Knife, "Knife", "Arts/Weapons/weapon_knife", 10f, 0f, 1.5f, 1.2f, 0.45f, 0.6f, 0.15f, 0.2f, "Long blade crescent slash.");
        }
    }

    private static WeaponConfig CreateFallback(WeaponType type, string displayName, string spriteResource, float physicalDamage, float magicDamage, float armorPiercing, float range, float radius, float cooldown, float windup, float recovery, string description)
    {
        return new WeaponConfig
        {
            weaponType = type,
            displayName = displayName,
            spriteResource = spriteResource,
            physicalDamage = physicalDamage,
            magicDamage = magicDamage,
            armorPiercing = armorPiercing,
            attackRange = range,
            attackRadius = radius,
            cooldown = cooldown,
            windup = windup,
            active = GetLegacyActive(type, false),
            recovery = recovery,
            inputBuffer = GetDefaultInputBuffer(type),
            hitStopDuration = 0.035f,
            hitStopTimeScale = 0.05f,
            knockbackDistance = 1f,
            knockbackDuration = 0.12f,
            feedbackIntensity = 1.4f,
            cameraShake = 0f,
            blocksMovementDuringAttack = true,
            description = description
        };
    }

    private static float GetDefaultInputBuffer(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sword:
            case WeaponType.Spear:
                return 0.18f;
            case WeaponType.Spell:
                return 0.14f;
            default:
                return 0.16f;
        }
    }

    private static float GetLegacyPhysicalDamage(WeaponType type, float damage)
    {
        return type == WeaponType.Spell ? 0f : damage;
    }

    private static float GetLegacyMagicDamage(WeaponType type, float damage)
    {
        return type == WeaponType.Spell ? damage : 0f;
    }

    private static float GetLegacyActive(WeaponType type, bool useSweepArc)
    {
        switch (type)
        {
            case WeaponType.Knife:
                return 0.13f;
            case WeaponType.Sword:
                return 0.055f;
            case WeaponType.Spear:
                return useSweepArc ? 0.42f : 0.15f;
            case WeaponType.Spell:
                return 0.48f;
            default:
                return 0.12f;
        }
    }
}
