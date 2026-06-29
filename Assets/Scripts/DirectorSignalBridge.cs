using UnityEngine;

public static class DirectorSignalBridge
{
    public static void NotifyPlayerDamaged(CharacterStats stats, in DamageContext context, in HitResult result)
    {
        if (stats == null || !result.accepted || result.finalDamage <= 0f)
        {
            return;
        }

        if (stats.GetComponent<PlayerInputManager>() == null)
        {
            return;
        }

        Vector2 position = context.hitPoint != Vector2.zero ? context.hitPoint : (Vector2)stats.transform.position;
        WorldHostilityDirector.Current.Notify(new DirectorEvent(
            DirectorEventType.PlayerDamaged,
            position,
            Mathf.Max(0.1f, result.finalDamage / 20f),
            "player_damage"));
    }

    public static void NotifyItemEffectUsed(Transform source, ShopGoodsConfigDatabase.ShopGoodEffect effect)
    {
        if (effect == null)
        {
            return;
        }

        if (effect.type != "addDebuff" && effect.type != "worldHostility")
        {
            return;
        }

        Vector2 position = source != null ? (Vector2)source.position : Vector2.zero;
        float magnitude = Mathf.Max(0.25f, Mathf.Max(effect.value, effect.stacks));
        WorldHostilityDirector.Current.Notify(new DirectorEvent(
            DirectorEventType.ValuableLootPicked,
            position,
            magnitude,
            string.IsNullOrEmpty(effect.debuffId) ? effect.type : effect.debuffId));
    }

    public static void NotifyTaskCompleted(TaskConfigDatabase.TaskConfig task, Vector2 worldPosition)
    {
        WorldHostilityDirector.Current.Notify(new DirectorEvent(
            DirectorEventType.TaskCompleted,
            worldPosition,
            EstimateTaskMagnitude(task),
            task != null ? task.id : string.Empty));
    }

    public static void NotifyTaskFailed(TaskConfigDatabase.TaskConfig task, Vector2 worldPosition)
    {
        WorldHostilityDirector.Current.Notify(new DirectorEvent(
            DirectorEventType.TaskFailed,
            worldPosition,
            EstimateTaskMagnitude(task),
            task != null ? task.id : string.Empty));
    }

    public static void NotifyLootAccepted(string category, string id, string rarity, int quantity, Vector2 worldPosition)
    {
        float magnitude = EstimateLootMagnitude(category, rarity, quantity);
        if (magnitude <= 0f)
        {
            return;
        }

        WorldHostilityDirector.Current.Notify(new DirectorEvent(
            DirectorEventType.ValuableLootPicked,
            worldPosition,
            magnitude,
            id));
    }

    private static float EstimateTaskMagnitude(TaskConfigDatabase.TaskConfig task)
    {
        if (task == null)
        {
            return 1f;
        }

        float magnitude = 0.75f;
        if (task.rewards != null)
        {
            magnitude += task.rewards.Length * 0.15f;
        }

        if (task.failurePenalties != null)
        {
            for (int i = 0; i < task.failurePenalties.Length; i++)
            {
                TaskConfigDatabase.TaskPenalty penalty = task.failurePenalties[i];
                if (penalty != null && penalty.type == "worldHostility")
                {
                    magnitude += Mathf.Max(0f, penalty.value);
                }
            }
        }

        return Mathf.Clamp(magnitude, 0.25f, 3f);
    }

    private static float EstimateLootMagnitude(string category, string rarity, int quantity)
    {
        string safeRarity = (rarity ?? string.Empty).ToLowerInvariant();
        if (safeRarity == "cursed")
        {
            return 1.2f;
        }

        if (safeRarity == "rare")
        {
            return 0.8f;
        }

        if ((category ?? string.Empty).ToLowerInvariant() == "material" && quantity >= 3)
        {
            return 0.35f;
        }

        return 0f;
    }
}
