using System;
using UnityEngine;

public sealed class TaskRunState : MonoBehaviour
{
    private static TaskRunState current;

    [SerializeField] private string activeTaskId;
    [SerializeField] private bool completed;
    [SerializeField] private bool failed;

    private TaskConfigDatabase.TaskConfig activeTask;
    private int[] completionProgress;
    private int[] failureProgress;

    public static TaskRunState Current
    {
        get
        {
            if (current != null)
            {
                return current;
            }

            current = FindObjectOfType<TaskRunState>();
            if (current != null)
            {
                return current;
            }

            GameObject obj = new GameObject("TaskRunState");
            current = obj.AddComponent<TaskRunState>();
            return current;
        }
    }

    public static TaskRunState Existing
    {
        get
        {
            if (current != null)
            {
                return current;
            }

            current = FindObjectOfType<TaskRunState>();
            return current;
        }
    }

    public TaskConfigDatabase.TaskConfig ActiveTask => activeTask;
    public string ActiveTaskId => activeTaskId;
    public bool HasActiveTask => activeTask != null && !completed && !failed;
    public bool IsCompleted => completed;
    public bool IsFailed => failed;

    private void Awake()
    {
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }

        current = this;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    public bool AcceptTask(TaskConfigDatabase.TaskConfig task)
    {
        if (task == null || string.IsNullOrEmpty(task.id))
        {
            return false;
        }

        activeTask = task;
        activeTaskId = task.id;
        completed = false;
        failed = false;
        completionProgress = new int[task.completionConditions != null ? task.completionConditions.Length : 0];
        failureProgress = new int[task.failureConditions != null ? task.failureConditions.Length : 0];
        EvaluateCompletion(Vector2.zero);
        return true;
    }

    public void ClearTask()
    {
        activeTask = null;
        activeTaskId = string.Empty;
        completed = false;
        failed = false;
        completionProgress = Array.Empty<int>();
        failureProgress = Array.Empty<int>();
    }

    public void NotifyEnemyKilled(EnemyArchetype archetype, Vector2 worldPosition)
    {
        if (!HasActiveTask)
        {
            return;
        }

        IncrementMatchingConditions(activeTask.completionConditions, completionProgress, "killEnemy", condition => MatchesArchetype(condition.enemyArchetype, archetype));
        EvaluateCompletion(worldPosition);
    }

    public void NotifyItemCollected(string itemId, int amount, Vector2 worldPosition)
    {
        if (!HasActiveTask || string.IsNullOrEmpty(itemId))
        {
            return;
        }

        int safeAmount = Mathf.Max(1, amount);
        IncrementMatchingConditions(activeTask.completionConditions, completionProgress, "collectItem", condition => MatchesItem(condition.itemId, itemId), safeAmount);
        EvaluateCompletion(worldPosition);
    }

    public void NotifySentinelAlarm(Vector2 worldPosition)
    {
        if (!HasActiveTask)
        {
            return;
        }

        IncrementMatchingConditions(activeTask.failureConditions, failureProgress, "sentinelAlarmCount", condition => true);
        EvaluateFailure(worldPosition);
    }

    public void NotifyPlayerDeath(Vector2 worldPosition)
    {
        if (!HasActiveTask)
        {
            return;
        }

        IncrementMatchingConditions(activeTask.failureConditions, failureProgress, "playerDeath", condition => true);
        EvaluateFailure(worldPosition);
    }

    public void NotifyStageEnded(Vector2 worldPosition)
    {
        if (!HasActiveTask)
        {
            return;
        }

        IncrementMatchingConditions(activeTask.failureConditions, failureProgress, "stageEndedWithoutCompletion", condition => true);
        EvaluateFailure(worldPosition);
    }

    private void EvaluateCompletion(Vector2 worldPosition)
    {
        if (!HasActiveTask || !ConditionsMet(activeTask.completionConditions, completionProgress, emptyMeansComplete: false))
        {
            return;
        }

        completed = true;
        GrantRewards(activeTask);
        DirectorSignalBridge.NotifyTaskCompleted(activeTask, worldPosition);
    }

    private void EvaluateFailure(Vector2 worldPosition)
    {
        if (!HasActiveTask || !ConditionsMet(activeTask.failureConditions, failureProgress, emptyMeansComplete: false))
        {
            return;
        }

        failed = true;
        DirectorSignalBridge.NotifyTaskFailed(activeTask, worldPosition);
    }

    private static void IncrementMatchingConditions(TaskConfigDatabase.TaskCondition[] conditions, int[] progress, string type, Func<TaskConfigDatabase.TaskCondition, bool> predicate, int amount = 1)
    {
        if (conditions == null || progress == null)
        {
            return;
        }

        for (int i = 0; i < conditions.Length && i < progress.Length; i++)
        {
            TaskConfigDatabase.TaskCondition condition = conditions[i];
            if (condition == null || condition.type != type || !predicate(condition))
            {
                continue;
            }

            progress[i] += Mathf.Max(1, amount);
        }
    }

    private static bool ConditionsMet(TaskConfigDatabase.TaskCondition[] conditions, int[] progress, bool emptyMeansComplete)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return emptyMeansComplete;
        }

        for (int i = 0; i < conditions.Length; i++)
        {
            TaskConfigDatabase.TaskCondition condition = conditions[i];
            if (condition == null)
            {
                continue;
            }

            int target = Mathf.Max(1, condition.count);
            int currentProgress = progress != null && i < progress.Length ? progress[i] : 0;
            if (currentProgress < target)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesArchetype(string configuredArchetype, EnemyArchetype actual)
    {
        return string.IsNullOrEmpty(configuredArchetype) || string.Equals(configuredArchetype, actual.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesItem(string configuredItemId, string actualItemId)
    {
        if (string.IsNullOrEmpty(configuredItemId))
        {
            return true;
        }

        if (string.Equals(configuredItemId, actualItemId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return actualItemId.EndsWith(configuredItemId, StringComparison.OrdinalIgnoreCase);
    }

    private static void GrantRewards(TaskConfigDatabase.TaskConfig task)
    {
        if (task == null || task.rewards == null)
        {
            return;
        }

        PlayerInventory inventory = FindObjectOfType<PlayerInventory>();
        for (int i = 0; i < task.rewards.Length; i++)
        {
            TaskConfigDatabase.TaskReward reward = task.rewards[i];
            if (reward == null)
            {
                continue;
            }

            int amount = Mathf.Max(1, reward.amount);
            switch (reward.type)
            {
                case "currency":
                    inventory?.AddGold(amount);
                    break;
                case "material":
                    inventory?.TryAddItem(GameCatalogDatabase.CreateInventoryItem("material", reward.id, amount));
                    break;
            }
        }
    }
}
