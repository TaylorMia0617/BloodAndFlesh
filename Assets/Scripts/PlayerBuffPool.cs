using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerBuffPool : MonoBehaviour
{
    [Serializable]
    public sealed class ActiveBuff
    {
        public string id;
        public string displayName;
        public Sprite icon;
        public int stacks;
        public bool isDebuff;
        public float expiresAt;
    }

    private readonly List<ActiveBuff> buffs = new List<ActiveBuff>();

    public event Action OnBuffsChanged;
    public IReadOnlyList<ActiveBuff> Buffs => buffs;

    private void Update()
    {
        bool changed = false;
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            if (buffs[i].expiresAt > 0f && Time.time >= buffs[i].expiresAt)
            {
                buffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            OnBuffsChanged?.Invoke();
        }
    }

    public void AddBuff(string id, string displayName, Sprite icon)
    {
        AddStatus(id, displayName, icon, false, 0f);
    }

    public void AddDebuff(string id, string displayName, Sprite icon, float duration)
    {
        AddStatus(id, displayName, icon, true, Mathf.Max(0f, duration));
    }

    private void AddStatus(string id, string displayName, Sprite icon, bool isDebuff, float duration)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        ActiveBuff existing = buffs.Find(buff => buff.id == id && buff.isDebuff == isDebuff);
        if (existing != null)
        {
            if (isDebuff && duration > 0f)
            {
                existing.expiresAt = Mathf.Max(existing.expiresAt, Time.time + duration);
            }
            else
            {
                existing.stacks = Mathf.Max(1, existing.stacks + 1);
            }

            OnBuffsChanged?.Invoke();
            return;
        }

        buffs.Add(new ActiveBuff
        {
            id = id,
            displayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName,
            icon = icon,
            stacks = 1,
            isDebuff = isDebuff,
            expiresAt = isDebuff && duration > 0f ? Time.time + duration : 0f
        });
        OnBuffsChanged?.Invoke();
    }
}
