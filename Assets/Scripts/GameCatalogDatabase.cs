using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameCatalogDatabase
{
    [Serializable]
    public sealed class CatalogEntryList
    {
        public CatalogEntry[] entries;
    }

    [Serializable]
    public sealed class CatalogEntry
    {
        public string id;
        public string type;
        public string name;
        public string rarity;
        public string iconResource;
        public string description;
        public int basePrice;
        public int maxStack;
        public ShopGoodsConfigDatabase.ShopGoodEffect[] effects;

        public string DisplayName => string.IsNullOrEmpty(name) ? id : name;
        public string Category => string.IsNullOrEmpty(type) ? "item" : type;
        public string Rarity => string.IsNullOrEmpty(rarity) ? "common" : rarity;
        public string Description => string.IsNullOrEmpty(description) ? "No description." : description;
        public int MaxStack => maxStack > 0 ? maxStack : GetDefaultMaxStack(Category);
    }

    private static readonly Dictionary<string, CatalogEntry> entries = new Dictionary<string, CatalogEntry>();
    private static bool loaded;

    public static CatalogEntry Get(string type, string id)
    {
        EnsureLoaded();
        if (entries.TryGetValue(Key(type, id), out CatalogEntry entry))
        {
            return entry;
        }

        return CreateFallback(type, id);
    }

    public static bool TryGet(string type, string id, out CatalogEntry entry)
    {
        EnsureLoaded();
        return entries.TryGetValue(Key(type, id), out entry);
    }

    public static PlayerInventory.InventoryItem CreateInventoryItem(string type, string id, int quantity)
    {
        CatalogEntry entry = Get(type, id);
        return new PlayerInventory.InventoryItem
        {
            id = id,
            category = entry.Category,
            displayName = entry.DisplayName,
            rarity = entry.Rarity,
            description = entry.Description,
            quantity = Mathf.Max(1, quantity),
            maxStack = entry.MaxStack,
            effects = entry.effects
        };
    }

    public static void HydrateInventoryItem(PlayerInventory.InventoryItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
        {
            return;
        }

        CatalogEntry entry = Get(item.category, item.id);
        item.category = entry.Category;
        item.displayName = entry.DisplayName;
        item.rarity = entry.Rarity;
        item.description = entry.Description;
        item.maxStack = entry.MaxStack;
        item.effects = entry.effects;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        entries.Clear();
        LoadCatalog("Configs/item_catalog", "item");
        LoadCatalog("Configs/skill_catalog", "skill");
        LoadCatalog("Configs/material_catalog", "material");
    }

    private static void LoadCatalog(string resourcePath, string defaultType)
    {
        TextAsset asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            return;
        }

        CatalogEntryList list = JsonUtility.FromJson<CatalogEntryList>(asset.text);
        if (list == null || list.entries == null)
        {
            return;
        }

        for (int i = 0; i < list.entries.Length; i++)
        {
            CatalogEntry entry = list.entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.id))
            {
                continue;
            }

            if (string.IsNullOrEmpty(entry.type))
            {
                entry.type = defaultType;
            }

            if (entry.maxStack <= 0)
            {
                entry.maxStack = GetDefaultMaxStack(entry.type);
            }

            entries[Key(entry.type, entry.id)] = entry;
        }
    }

    private static CatalogEntry CreateFallback(string type, string id)
    {
        string safeType = string.IsNullOrEmpty(type) ? "item" : type;
        string safeId = string.IsNullOrEmpty(id) ? "unknown" : id;
        return new CatalogEntry
        {
            id = safeId,
            type = safeType,
            name = safeId.Replace('_', ' '),
            rarity = "common",
            description = $"{safeType} entry missing from catalog.",
            maxStack = GetDefaultMaxStack(safeType)
        };
    }

    private static int GetDefaultMaxStack(string type)
    {
        switch (string.IsNullOrEmpty(type) ? "item" : type)
        {
            case "material":
                return 99;
            case "skill":
            case "item":
            default:
                return 1;
        }
    }

    private static string Key(string type, string id)
    {
        string safeType = string.IsNullOrEmpty(type) ? "item" : type;
        return $"{safeType}:{id}";
    }
}
