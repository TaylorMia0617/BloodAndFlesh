using UnityEngine;

public static class SemanticResourceResolver
{
    public static string CategoryFor(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Medical:
            case ResourceType.TaskItem:
            case ResourceType.ExtractionUpgrade:
                return "item";
            case ResourceType.Currency:
                return "currency";
            case ResourceType.Material:
            case ResourceType.RareCore:
            default:
                return "material";
        }
    }

    public static string DefaultCatalogIdFor(ResourceType type, float threat = 0f, float lootWeight = 0f)
    {
        switch (type)
        {
            case ResourceType.Medical:
                return "item_small_heal";
            case ResourceType.TaskItem:
                return "item_blood_lantern";
            case ResourceType.ExtractionUpgrade:
                return "item_mana_shard";
            case ResourceType.RareCore:
                return "material_arcane_splinter";
            case ResourceType.Currency:
                return "gold";
            case ResourceType.Material:
            default:
                if (threat >= 1.5f || lootWeight >= 0.85f)
                {
                    return "material_signal_shard";
                }

                return "material_iron_shard";
        }
    }

    public static ResourceNode Normalize(MapData map, ResourceNode node)
    {
        if (!string.IsNullOrEmpty(node.CatalogId))
        {
            return node;
        }

        MapCell cell = map != null && map.IsInside(node.Cell) ? map.GetCell(node.Cell) : default;
        node.CatalogId = DefaultCatalogIdFor(node.Type, cell.Threat, cell.LootWeight);
        return node;
    }

    public static PlayerInventory.InventoryItem CreateInventoryItem(ResourceNode node)
    {
        int quantity = Random.Range(Mathf.Max(1, node.AmountMin), Mathf.Max(Mathf.Max(1, node.AmountMin), node.AmountMax) + 1);
        return CreateInventoryItem(node, quantity);
    }

    public static PlayerInventory.InventoryItem CreateInventoryItem(ResourceNode node, int quantity)
    {
        if (node.Type == ResourceType.Currency)
        {
            return new PlayerInventory.InventoryItem
            {
                id = string.IsNullOrEmpty(node.CatalogId) ? "gold" : node.CatalogId,
                category = "currency",
                displayName = "Gold",
                rarity = "common",
                description = "Run currency.",
                quantity = Mathf.Max(1, quantity),
                maxStack = 9999
            };
        }

        string category = CategoryFor(node.Type);
        string catalogId = string.IsNullOrEmpty(node.CatalogId)
            ? DefaultCatalogIdFor(node.Type)
            : node.CatalogId;
        return GameCatalogDatabase.CreateInventoryItem(category, catalogId, Mathf.Max(1, quantity));
    }
}
