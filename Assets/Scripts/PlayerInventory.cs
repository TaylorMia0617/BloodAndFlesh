using System;
using UnityEngine;

public sealed class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public sealed class InventoryItem
    {
        public string id;
        public string displayName;
        public string category;
        public string rarity;
        public string description;
        public int quantity;
        public int maxStack;
        public ShopGoodsConfigDatabase.ShopGoodEffect[] effects;

        public string Summary => string.IsNullOrEmpty(description) ? "暂无说明。" : description;
    }

    [SerializeField] private int startingGold = 120;
    [SerializeField] private int capacity = 16;

    private InventoryItem[] slots;
    private int gold;

    public event Action<int> OnGoldChanged;
    public event Action OnInventoryChanged;

    public int Gold => gold;
    public int Capacity => slots != null ? slots.Length : Mathf.Max(1, capacity);

    private void Awake()
    {
        EnsureSlots();
        gold = Mathf.Max(0, startingGold);
    }

    private void Start()
    {
        OnGoldChanged?.Invoke(gold);
        OnInventoryChanged?.Invoke();
    }

    public InventoryItem GetItem(int index)
    {
        EnsureSlots();
        return index >= 0 && index < slots.Length ? slots[index] : null;
    }

    public bool TrySpendGold(int amount)
    {
        int cost = Mathf.Max(0, amount);
        if (gold < cost)
        {
            return false;
        }

        gold -= cost;
        OnGoldChanged?.Invoke(gold);
        return true;
    }

    public void AddGold(int amount)
    {
        gold = Mathf.Max(0, gold + amount);
        OnGoldChanged?.Invoke(gold);
    }

    public bool TryAddShopGood(ShopGoodsConfigDatabase.ShopGoodConfig good)
    {
        if (good == null)
        {
            return false;
        }

        return TryAddItem(GameCatalogDatabase.CreateInventoryItem(good.category, good.id, 1));
    }

    public bool TryAddItem(InventoryItem item)
    {
        EnsureSlots();
        if (item == null || string.IsNullOrEmpty(item.id))
        {
            return false;
        }

        GameCatalogDatabase.HydrateInventoryItem(item);
        int maxStack = Mathf.Max(1, item.maxStack);
        int remaining = Mathf.Max(1, item.quantity);
        int accepted = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].id == item.id && slots[i].category == item.category)
            {
                GameCatalogDatabase.HydrateInventoryItem(slots[i]);
                int slotMaxStack = Mathf.Max(1, slots[i].maxStack);
                int room = Mathf.Max(0, slotMaxStack - slots[i].quantity);
                if (room <= 0)
                {
                    continue;
                }

                int moved = Mathf.Min(room, remaining);
                slots[i].quantity += moved;
                remaining -= moved;
                accepted += moved;
                if (remaining <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                int moved = Mathf.Min(maxStack, remaining);
                slots[i] = CloneInventoryItem(item, moved);
                remaining -= moved;
                accepted += moved;
                if (remaining <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        if (accepted > 0)
        {
            Debug.LogWarning($"Inventory accepted {accepted} {item.id}, but {remaining} could not fit.");
            OnInventoryChanged?.Invoke();
            return true;
        }

        Debug.LogWarning($"Inventory full. Could not add {item.id} x{remaining}.");
        return false;
    }

    public bool UseItem(int index, CharacterStats stats)
    {
        InventoryItem item = GetItem(index);
        if (item == null)
        {
            return false;
        }

        ShopGoodsConfigDatabase.ShopGoodEffect[] itemEffects = item.effects;
        if (itemEffects == null || itemEffects.Length == 0)
        {
            GameCatalogDatabase.HydrateInventoryItem(item);
            itemEffects = item.effects;
        }

        bool used = false;
        if (itemEffects != null)
        {
            for (int i = 0; i < itemEffects.Length; i++)
            {
                ShopGoodsConfigDatabase.ShopGoodEffect effect = itemEffects[i];
                if (effect == null)
                {
                    continue;
                }

                switch (effect.type)
                {
                    case "restoreHealth":
                        used |= RestoreHealth(stats, effect.value);
                        break;
                    case "restoreMana":
                        if (stats != null)
                        {
                            stats.RestoreMana(effect.value);
                            used = true;
                        }
                        break;
                    case "addMaterial":
                    case "unlockSkill":
                        used = true;
                        break;
                    case "addBuff":
                        used = true;
                        break;
                    case "addDebuff":
                        DirectorSignalBridge.NotifyItemEffectUsed(transform, effect);
                        used = true;
                        break;
                }
            }
        }

        if (!used)
        {
            return false;
        }

        item.quantity--;
        if (item.quantity <= 0)
        {
            slots[index] = null;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    private bool RestoreHealth(CharacterStats stats, float amount)
    {
        if (stats == null || amount <= 0f || stats.CurrentHealth >= stats.maxHealth)
        {
            return false;
        }

        stats.RestoreHealth(amount);
        return true;
    }

    private void EnsureSlots()
    {
        int safeCapacity = Mathf.Max(1, capacity);
        if (slots == null || slots.Length != safeCapacity)
        {
            slots = new InventoryItem[safeCapacity];
        }
    }

    private static InventoryItem CloneInventoryItem(InventoryItem source, int quantity)
    {
        return new InventoryItem
        {
            id = source.id,
            displayName = source.displayName,
            category = source.category,
            rarity = source.rarity,
            description = source.description,
            quantity = Mathf.Max(1, quantity),
            maxStack = Mathf.Max(1, source.maxStack),
            effects = source.effects
        };
    }
}
