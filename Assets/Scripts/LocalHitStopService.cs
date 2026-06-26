using UnityEngine;

public static class LocalHitStopService
{
    private static readonly Collider2D[] hitResults = new Collider2D[12];

    public static void Request(float duration, GameObject attacker, Vector2 hitPoint)
    {
        if (duration <= 0f)
        {
            return;
        }

        Push(attacker, duration);

        int count = Physics2D.OverlapCircleNonAlloc(hitPoint, 0.35f, hitResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitResults[i];
            if (hit == null)
            {
                continue;
            }

            DamageableHurtbox hurtbox = hit.GetComponent<DamageableHurtbox>() ?? hit.GetComponentInParent<DamageableHurtbox>();
            if (hurtbox != null)
            {
                Component ownerComponent = hurtbox.Owner as Component;
                if (ownerComponent != null)
                {
                    Push(ownerComponent.gameObject, duration);
                    continue;
                }
            }

            Push(hit.gameObject, duration);
        }
    }

    private static void Push(GameObject root, float duration)
    {
        if (root == null)
        {
            return;
        }

        ILocalFreezable[] freezables = root.GetComponentsInChildren<ILocalFreezable>(true);
        for (int i = 0; i < freezables.Length; i++)
        {
            freezables[i]?.PushHitStop(duration);
        }
    }
}
