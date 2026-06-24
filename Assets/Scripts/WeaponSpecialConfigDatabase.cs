using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class WeaponSpecialConfig
{
    public WeaponType weaponType;
    public string specialType;
    public float cooldown;
    public float windup;
    public float active;
    public float recovery;
    public float blockDuration;
    public float shieldDuration;
    public float manaCrystalCost;
    public float dashDistance;
    public float dashDuration;
    public float untargetableExtraDuration;
    public float movementSpeedMultiplier;
    public float visualMultiplier;
    public float magicSenseRadius;
}

public static class WeaponSpecialConfigDatabase
{
    private const string ResourcePath = "Configs/weapon_special_config";
    private static readonly Dictionary<WeaponType, WeaponSpecialConfig> configs = new Dictionary<WeaponType, WeaponSpecialConfig>();
    private static bool loaded;

    public static WeaponSpecialConfig Get(WeaponType type)
    {
        EnsureLoaded();
        return configs.TryGetValue(type, out WeaponSpecialConfig config) ? config : CreateFallback(type);
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
            if (cells.Length < 15 || !Enum.TryParse(cells[0], out WeaponType type))
            {
                Debug.LogWarning($"Weapon special config row {i + 1} is invalid.");
                continue;
            }

            configs[type] = new WeaponSpecialConfig
            {
                weaponType = type,
                specialType = cells[1],
                cooldown = ParseFloat(cells[2], 1f),
                windup = ParseFloat(cells[3], 0f),
                active = ParseFloat(cells[4], 0.2f),
                recovery = ParseFloat(cells[5], 0.1f),
                blockDuration = ParseFloat(cells[6], 0f),
                shieldDuration = ParseFloat(cells[7], 0f),
                manaCrystalCost = ParseFloat(cells[8], 0f),
                dashDistance = ParseFloat(cells[9], 0f),
                dashDuration = ParseFloat(cells[10], 0.2f),
                untargetableExtraDuration = ParseFloat(cells[11], 0f),
                movementSpeedMultiplier = ParseFloat(cells[12], 1f),
                visualMultiplier = ParseFloat(cells[13], 1f),
                magicSenseRadius = ParseFloat(cells[14], 0f)
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

    private static void LoadFallbacks()
    {
        configs[WeaponType.Knife] = CreateFallback(WeaponType.Knife);
        configs[WeaponType.Sword] = CreateFallback(WeaponType.Sword);
        configs[WeaponType.Spear] = CreateFallback(WeaponType.Spear);
        configs[WeaponType.Spell] = CreateFallback(WeaponType.Spell);
    }

    private static WeaponSpecialConfig CreateFallback(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sword:
                return new WeaponSpecialConfig { weaponType = type, specialType = "LightningDash", cooldown = 0.75f, active = 0.22f, recovery = 0.08f, dashDistance = 4f, dashDuration = 0.22f, untargetableExtraDuration = 0.05f, movementSpeedMultiplier = 1f, visualMultiplier = 1f };
            case WeaponType.Spear:
                return new WeaponSpecialConfig { weaponType = type, specialType = "SpinSweep", cooldown = 1.2f, windup = 0.7f, active = 0.42f, recovery = 0.5f, movementSpeedMultiplier = 1.5f, visualMultiplier = 1.35f };
            case WeaponType.Spell:
                return new WeaponSpecialConfig { weaponType = type, specialType = "ManaShield", cooldown = 1f, active = 0.32f, shieldDuration = 3f, manaCrystalCost = 0.5f, visualMultiplier = 1f, magicSenseRadius = 8f };
            default:
                return new WeaponSpecialConfig { weaponType = WeaponType.Knife, specialType = "Block", cooldown = 0.65f, active = 1f, blockDuration = 1f, shieldDuration = 1f, movementSpeedMultiplier = 1f, visualMultiplier = 1f };
        }
    }
}
