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
        List<BuffConfig> available = new List<BuffConfig>();
        for (int i = 0; i < cachedBuffs.Length; i++)
        {
            if (cachedBuffs[i] != null)
            {
                available.Add(cachedBuffs[i]);
            }
        }

        List<BuffConfig> choices = new List<BuffConfig>(count);
        WorldHostilityDirector director = WorldHostilityDirector.Current;
        while (choices.Count < count && available.Count > 0)
        {
            float total = 0f;
            for (int i = 0; i < available.Count; i++)
            {
                total += GetWeight(available[i], director);
            }

            if (total <= 0f)
            {
                break;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            for (int i = 0; i < available.Count; i++)
            {
                BuffConfig buff = available[i];
                roll -= GetWeight(buff, director);
                if (roll > 0f)
                {
                    continue;
                }

                choices.Add(buff);
                available.RemoveAt(i);
                break;
            }
        }

        return choices.ToArray();
    }

    private static float GetWeight(BuffConfig buff, WorldHostilityDirector director)
    {
        if (buff == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, director.GetBuffRarityWeightMultiplier(buff.rarity));
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
