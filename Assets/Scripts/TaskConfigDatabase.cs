using System;
using System.Collections.Generic;
using UnityEngine;

public static class TaskConfigDatabase
{
    [Serializable]
    public sealed class TaskList
    {
        public TaskConfig[] tasks;
    }

    [Serializable]
    public sealed class TaskConfig
    {
        public string id;
        public string name;
        public string description;
        public string rarity;
        public TaskReward[] rewards;
        public TaskPenalty[] failurePenalties;
        public TaskCondition[] completionConditions;
        public TaskCondition[] failureConditions;
    }

    [Serializable]
    public sealed class TaskReward
    {
        public string type;
        public string id;
        public int amount;
        public float value;
    }

    [Serializable]
    public sealed class TaskPenalty
    {
        public string type;
        public string mode;
        public string id;
        public int amount;
        public float value;
    }

    [Serializable]
    public sealed class TaskCondition
    {
        public string type;
        public string enemyArchetype;
        public string itemId;
        public int count;
    }

    private static TaskConfig[] cachedTasks;

    public static IReadOnlyList<TaskConfig> AllTasks
    {
        get
        {
            EnsureLoaded();
            return cachedTasks;
        }
    }

    public static string FormatObjective(TaskConfig task)
    {
        if (task == null || task.completionConditions == null || task.completionConditions.Length == 0)
        {
            return "完成安全屋记录员交代的目标。";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < task.completionConditions.Length; i++)
        {
            TaskCondition condition = task.completionConditions[i];
            if (condition == null)
            {
                continue;
            }

            switch (condition.type)
            {
                case "killEnemy":
                    parts.Add($"击杀 {condition.count} 个{FormatEnemyName(condition.enemyArchetype)}");
                    break;
                case "collectItem":
                    parts.Add($"收集 {condition.count} 个{FormatItemName(condition.itemId)}");
                    break;
                default:
                    parts.Add("完成特殊目标");
                    break;
            }
        }

        return string.Join("\n", parts);
    }

    public static string FormatRewards(TaskConfig task)
    {
        if (task == null || task.rewards == null || task.rewards.Length == 0)
        {
            return "暂无";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < task.rewards.Length; i++)
        {
            TaskReward reward = task.rewards[i];
            if (reward == null)
            {
                continue;
            }

            switch (reward.type)
            {
                case "currency":
                    parts.Add($"金币 x{reward.amount}");
                    break;
                case "material":
                    parts.Add($"{FormatItemName(reward.id)} x{reward.amount}");
                    break;
                case "buffChoice":
                    parts.Add("祝福选择 x1");
                    break;
                case "shopDiscount":
                    parts.Add($"商店折扣 {Mathf.RoundToInt(reward.value * 100f)}%");
                    break;
                default:
                    parts.Add("特殊奖励");
                    break;
            }
        }

        return parts.Count > 0 ? string.Join(" / ", parts) : "暂无";
    }

    public static string FormatPenalties(TaskConfig task)
    {
        if (task == null || task.failurePenalties == null || task.failurePenalties.Length == 0)
        {
            return "无";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < task.failurePenalties.Length; i++)
        {
            TaskPenalty penalty = task.failurePenalties[i];
            if (penalty == null)
            {
                continue;
            }

            switch (penalty.type)
            {
                case "worldHostility":
                    parts.Add($"世界敌意 +{penalty.value:0.##}");
                    break;
                case "spawnModifier":
                    parts.Add("下一关额外巡逻");
                    break;
                default:
                    parts.Add("未知代价");
                    break;
            }
        }

        return parts.Count > 0 ? string.Join(" / ", parts) : "无";
    }

    private static string FormatEnemyName(string enemyArchetype)
    {
        switch (enemyArchetype)
        {
            case "Attacker":
                return "战士";
            case "Sentinel":
                return "侦察兵";
            case "Ranged":
                return "远程敌人";
            case "Shield":
                return "盾兵";
            default:
                return "敌人";
        }
    }

    private static string FormatItemName(string itemId)
    {
        switch (itemId)
        {
            case "iron_shard":
                return "铁片";
            case "unstable_crystal":
                return "不稳定魔晶";
            default:
                return string.IsNullOrEmpty(itemId) ? "物资" : itemId;
        }
    }

    private static void EnsureLoaded()
    {
        if (cachedTasks != null)
        {
            return;
        }

        TextAsset asset = Resources.Load<TextAsset>("Configs/task_config");
        if (asset == null)
        {
            cachedTasks = Array.Empty<TaskConfig>();
            return;
        }

        TaskList list = JsonUtility.FromJson<TaskList>(asset.text);
        cachedTasks = list != null && list.tasks != null ? list.tasks : Array.Empty<TaskConfig>();
    }
}
