using System;
using System.Collections.Generic;
using UnityEngine;

public static class ShopGoodsConfigDatabase
{
    [Serializable]
    public sealed class GoodsList
    {
        public ShopGoodConfig[] goods;
    }

    [Serializable]
    public sealed class ShopGoodConfig
    {
        public string id;
        public string name;
        public string rarity;
        public string category;
        public int price;
        public string description;
        public float appearanceChance;
        public string[] shopTags;
        public ShopGoodEffect[] effects;
    }

    [Serializable]
    public sealed class ShopGoodEffect
    {
        public string type;
        public string target;
        public string skillId;
        public string materialId;
        public string buffId;
        public string debuffId;
        public string slot;
        public float value;
        public int amount;
        public int stacks;
    }

    private static ShopGoodConfig[] cachedGoods;

    public static ShopGoodConfig[] GetGoodsForShop(string shopTag, int count)
    {
        EnsureLoaded();
        List<ShopGoodConfig> tagged = new List<ShopGoodConfig>();
        List<ShopGoodConfig> fallback = new List<ShopGoodConfig>();

        for (int i = 0; i < cachedGoods.Length; i++)
        {
            ShopGoodConfig good = cachedGoods[i];
            if (good == null)
            {
                continue;
            }

            if (HasTag(good, shopTag))
            {
                tagged.Add(good);
            }
            else if (shopTag != "blackMarket" && !HasTag(good, "blackMarket"))
            {
                fallback.Add(good);
            }
        }

        List<ShopGoodConfig> result = tagged.Count > 0 ? tagged : fallback;
        if (result.Count < count)
        {
            for (int i = 0; i < fallback.Count && result.Count < count; i++)
            {
                if (!result.Contains(fallback[i]))
                {
                    result.Add(fallback[i]);
                }
            }
        }

        while (result.Count < count && cachedGoods.Length > 0)
        {
            result.Add(cachedGoods[result.Count % cachedGoods.Length]);
        }

        if (result.Count > count)
        {
            result.RemoveRange(count, result.Count - count);
        }

        return result.ToArray();
    }

    public static string FormatEffectSummary(ShopGoodConfig good)
    {
        if (good == null || good.effects == null || good.effects.Length == 0)
        {
            return "效果：待接入";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < good.effects.Length; i++)
        {
            ShopGoodEffect effect = good.effects[i];
            if (effect == null)
            {
                continue;
            }

            switch (effect.type)
            {
                case "restoreHealth":
                    parts.Add($"恢复生命 {effect.value:0}");
                    break;
                case "restoreMana":
                    parts.Add($"恢复法力 {effect.value:0.#}");
                    break;
                case "unlockSkill":
                    parts.Add($"解锁技能 {effect.slot}");
                    break;
                case "addMaterial":
                    parts.Add($"获得材料 x{effect.amount}");
                    break;
                case "addBuff":
                    parts.Add("获得增益");
                    break;
                case "addDebuff":
                    parts.Add("附带代价");
                    break;
                default:
                    parts.Add("特殊效果");
                    break;
            }
        }

        return parts.Count > 0 ? string.Join(" / ", parts) : "效果：待接入";
    }

    private static bool HasTag(ShopGoodConfig good, string tag)
    {
        if (good == null || good.shopTags == null || string.IsNullOrEmpty(tag))
        {
            return false;
        }

        for (int i = 0; i < good.shopTags.Length; i++)
        {
            if (good.shopTags[i] == tag)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureLoaded()
    {
        if (cachedGoods != null)
        {
            return;
        }

        TextAsset asset = Resources.Load<TextAsset>("Configs/shop_goods_config");
        if (asset == null)
        {
            cachedGoods = Array.Empty<ShopGoodConfig>();
            return;
        }

        GoodsList list = JsonUtility.FromJson<GoodsList>(asset.text);
        cachedGoods = list != null && list.goods != null ? list.goods : Array.Empty<ShopGoodConfig>();
    }
}
