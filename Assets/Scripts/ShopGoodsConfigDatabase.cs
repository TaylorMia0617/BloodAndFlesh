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
    public sealed class ShopPoolList
    {
        public ShopPoolConfig[] shops;
    }

    [Serializable]
    public sealed class ShopPoolConfig
    {
        public string shopTag;
        public bool allowCursed;
        public ShopPoolEntry[] entries;
    }

    [Serializable]
    public sealed class ShopPoolEntry
    {
        public string id;
        public string type;
        public int basePrice;
        public float weight = 1f;
        public float priceMultiplier = 1f;
        public int minHostilityLevel;
        public bool allowCursed;
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
        public string iconResource;
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

    private static ShopPoolConfig[] cachedPools;
    private static ShopGoodConfig[] cachedLegacyGoods;
    private static bool loadedLegacyGoods;

    public static ShopGoodConfig[] GetGoodsForShop(string shopTag, int count)
    {
        EnsurePoolsLoaded();
        ShopPoolConfig poolConfig = FindPool(shopTag);
        if (poolConfig != null)
        {
            ShopGoodConfig[] pooled = PickWeighted(poolConfig, count);
            if (pooled.Length > 0)
            {
                return pooled;
            }
        }

        EnsureLegacyGoodsLoaded();
        return PickLegacyGoods(shopTag, count);
    }

    public static string FormatEffectSummary(ShopGoodConfig good)
    {
        if (good == null || good.effects == null || good.effects.Length == 0)
        {
            return "效果：暂无";
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

        return parts.Count > 0 ? string.Join(" / ", parts) : "效果：暂无";
    }

    private static ShopGoodConfig[] PickWeighted(ShopPoolConfig poolConfig, int count)
    {
        List<ShopPoolEntry> available = new List<ShopPoolEntry>();
        WorldHostilityDirector director = WorldHostilityDirector.Current;
        if (poolConfig.entries == null)
        {
            return Array.Empty<ShopGoodConfig>();
        }

        for (int i = 0; i < poolConfig.entries.Length; i++)
        {
            ShopPoolEntry entry = poolConfig.entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.id) || director.HostilityLevel < entry.minHostilityLevel)
            {
                continue;
            }

            available.Add(entry);
        }

        List<ShopGoodConfig> result = new List<ShopGoodConfig>(Mathf.Max(0, count));
        while (result.Count < count && available.Count > 0)
        {
            float total = 0f;
            for (int i = 0; i < available.Count; i++)
            {
                total += GetWeight(available[i], poolConfig, director);
            }

            if (total <= 0f)
            {
                break;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            for (int i = 0; i < available.Count; i++)
            {
                ShopPoolEntry entry = available[i];
                roll -= GetWeight(entry, poolConfig, director);
                if (roll > 0f)
                {
                    continue;
                }

                result.Add(BuildGood(poolConfig.shopTag, entry));
                available.RemoveAt(i);
                break;
            }
        }

        return result.ToArray();
    }

    private static float GetWeight(ShopPoolEntry poolEntry, ShopPoolConfig poolConfig, WorldHostilityDirector director)
    {
        if (poolEntry == null)
        {
            return 0f;
        }

        GameCatalogDatabase.CatalogEntry catalogEntry = GameCatalogDatabase.Get(poolEntry.type, poolEntry.id);
        bool cursedAllowed = poolConfig.allowCursed || poolEntry.allowCursed;
        float rarityMultiplier = director.GetRarityWeightMultiplier(catalogEntry.Rarity, cursedAllowed);
        return Mathf.Max(0f, poolEntry.weight) * rarityMultiplier;
    }

    private static ShopGoodConfig BuildGood(string shopTag, ShopPoolEntry poolEntry)
    {
        GameCatalogDatabase.CatalogEntry entry = GameCatalogDatabase.Get(poolEntry.type, poolEntry.id);
        int basePrice = poolEntry.basePrice > 0 ? poolEntry.basePrice : entry.basePrice;
        float priceMultiplier = poolEntry.priceMultiplier > 0f ? poolEntry.priceMultiplier : 1f;
        return new ShopGoodConfig
        {
            id = entry.id,
            name = entry.DisplayName,
            rarity = entry.Rarity,
            category = entry.Category,
            price = Mathf.Max(0, Mathf.RoundToInt(basePrice * priceMultiplier)),
            description = entry.Description,
            iconResource = entry.iconResource,
            appearanceChance = poolEntry.weight,
            shopTags = new[] { shopTag },
            effects = entry.effects
        };
    }

    private static ShopGoodConfig[] PickLegacyGoods(string shopTag, int count)
    {
        List<ShopGoodConfig> tagged = new List<ShopGoodConfig>();
        List<ShopGoodConfig> fallback = new List<ShopGoodConfig>();
        for (int i = 0; i < cachedLegacyGoods.Length; i++)
        {
            ShopGoodConfig good = cachedLegacyGoods[i];
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

        List<ShopGoodConfig> pool = tagged.Count > 0 ? tagged : fallback;
        List<ShopGoodConfig> result = new List<ShopGoodConfig>(Mathf.Max(0, count));
        WorldHostilityDirector director = WorldHostilityDirector.Current;
        bool cursedAllowed = shopTag == "blackMarket";
        while (result.Count < count && pool.Count > 0)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                total += GetLegacyWeight(pool[i], director, cursedAllowed);
            }

            if (total <= 0f)
            {
                break;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            for (int i = 0; i < pool.Count; i++)
            {
                ShopGoodConfig good = pool[i];
                roll -= GetLegacyWeight(good, director, cursedAllowed);
                if (roll > 0f)
                {
                    continue;
                }

                result.Add(good);
                pool.RemoveAt(i);
                break;
            }
        }

        return result.ToArray();
    }

    private static float GetLegacyWeight(ShopGoodConfig good, WorldHostilityDirector director, bool cursedAllowed)
    {
        if (good == null)
        {
            return 0f;
        }

        float baseChance = good.appearanceChance > 0f ? good.appearanceChance : 1f;
        return Mathf.Max(0f, baseChance * director.GetRarityWeightMultiplier(good.rarity, cursedAllowed));
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

    private static ShopPoolConfig FindPool(string shopTag)
    {
        if (cachedPools == null)
        {
            return null;
        }

        for (int i = 0; i < cachedPools.Length; i++)
        {
            if (cachedPools[i] != null && cachedPools[i].shopTag == shopTag)
            {
                return cachedPools[i];
            }
        }

        return null;
    }

    private static void EnsurePoolsLoaded()
    {
        if (cachedPools != null)
        {
            return;
        }

        TextAsset asset = Resources.Load<TextAsset>("Configs/shop_pool_config");
        if (asset == null)
        {
            cachedPools = Array.Empty<ShopPoolConfig>();
            return;
        }

        ShopPoolList list = JsonUtility.FromJson<ShopPoolList>(asset.text);
        cachedPools = list != null && list.shops != null ? list.shops : Array.Empty<ShopPoolConfig>();
    }

    private static void EnsureLegacyGoodsLoaded()
    {
        if (loadedLegacyGoods)
        {
            return;
        }

        loadedLegacyGoods = true;
        TextAsset asset = Resources.Load<TextAsset>("Configs/shop_goods_config");
        if (asset == null)
        {
            cachedLegacyGoods = Array.Empty<ShopGoodConfig>();
            return;
        }

        GoodsList list = JsonUtility.FromJson<GoodsList>(asset.text);
        cachedLegacyGoods = list != null && list.goods != null ? list.goods : Array.Empty<ShopGoodConfig>();
    }
}
