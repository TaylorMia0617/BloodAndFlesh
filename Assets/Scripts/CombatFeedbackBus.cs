using System;

public static class CombatFeedbackBus
{
    public static event Action<DamageContext, HitResult> HitConfirmed;

    public static void PublishHit(in DamageContext context, in HitResult result)
    {
        if (!result.accepted)
        {
            return;
        }

        HitConfirmed?.Invoke(context, result);
    }
}
