using System;
using System.Collections.Generic;
using UnityEngine;

public static class BuffConfigDatabase
{
    [Serializable]
    public sealed class BuffList
    {
        public BuffConfig[] buffs;
    }

    [Serializable]
    public sealed class BuffConfig
    {
        public string id;
        public string name;
        public string description;
        public string rarity;
        public int maxStacks;
        public BuffCondition[] conditions;
        public BuffEffect[] effects;
    }

    [Serializable]
    public sealed class BuffCondition
    {
        public string type;
        public float value;
    }

    [Serializable]
    public sealed class BuffEffect
    {
        public string target;
        public string stat;
        public string mode;
        public float value;
        public float duration;
    }

    private static BuffConfig[] cachedBuffs;

    public static IReadOnlyList<BuffConfig> AllBuffs
    {
        get
        {
            EnsureLoaded();
            return cachedBuffs;
        }
    }

    public static BuffConfig Get(string id)
    {
        EnsureLoaded();
        for (int i = 0; i < cachedBuffs.Length; i++)
        {
            if (cachedBuffs[i] != null && cachedBuffs[i].id == id)
            {
                return cachedBuffs[i];
            }
        }

        return null;
    }

    public static BuffConfig[] GetWishChoices(int count)
    {
        EnsureLoaded();
        List<BuffConfig> choices = new List<BuffConfig>(count);
        for (int i = 0; i < cachedBuffs.Length && choices.Count < count; i++)
        {
            if (cachedBuffs[i] != null)
            {
                choices.Add(cachedBuffs[i]);
            }
        }

        return choices.ToArray();
    }

    private static void EnsureLoaded()
    {
        if (cachedBuffs != null)
        {
            return;
        }

        TextAsset asset = Resources.Load<TextAsset>("Configs/buff_config");
        if (asset == null)
        {
            cachedBuffs = Array.Empty<BuffConfig>();
            return;
        }

        BuffList list = JsonUtility.FromJson<BuffList>(asset.text);
        cachedBuffs = list != null && list.buffs != null ? list.buffs : Array.Empty<BuffConfig>();
    }
}
