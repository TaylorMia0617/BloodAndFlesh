using UnityEngine;

public static class DropTableResolver
{
    public static void ResolveEnemyDrops(EnemyConfig enemyConfig, Vector3 deathPosition)
    {
        if (enemyConfig == null || enemyConfig.drops == null || enemyConfig.drops.Length == 0)
        {
            return;
        }

        PlayerInventory inventory = Object.FindObjectOfType<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning($"Enemy {enemyConfig.displayName} dropped loot at {deathPosition}, but no PlayerInventory was found.");
            return;
        }

        WorldHostilityDirector director = WorldHostilityDirector.Current;
        for (int i = 0; i < enemyConfig.drops.Length; i++)
        {
            EnemyDropConfig drop = enemyConfig.drops[i];
            if (drop == null || string.IsNullOrEmpty(drop.id))
            {
                continue;
            }

            float chance = Mathf.Clamp01(drop.chance * director.DropChanceMultiplier * Mathf.Max(0.01f, drop.hostilityMultiplier) + drop.hostilityChanceBonus * director.RawHostility);
            if (Random.value > chance)
            {
                continue;
            }

            int amount = Random.Range(Mathf.Max(1, drop.min), Mathf.Max(Mathf.Max(1, drop.min), drop.max) + 1);
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * director.DropAmountMultiplier));
            if (IsGold(drop))
            {
                inventory.AddGold(amount);
                continue;
            }

            PlayerInventory.InventoryItem item = GameCatalogDatabase.CreateInventoryItem(
                string.IsNullOrEmpty(drop.type) ? "material" : drop.type,
                drop.id,
                amount);

            if (!inventory.TryAddItem(item))
            {
                Debug.LogWarning($"Inventory full. Dropped {drop.id} x{amount} from {enemyConfig.displayName} could not be collected.");
            }
        }
    }

    private static bool IsGold(EnemyDropConfig drop)
    {
        return drop.type == "gold" || drop.id == "gold";
    }

}
