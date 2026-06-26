using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class KnockbackReceiver : MonoBehaviour, ILocalFreezable
{
    [SerializeField] private float receivedDistanceMultiplier = 1f;
    [SerializeField] private float receivedDurationMultiplier = 1f;
    [SerializeField] private float minimumDistance = 0.04f;
    [SerializeField] private float minimumDuration = 0.04f;

    private Rigidbody2D body;
    private Coroutine routine;
    private float activeUntil;
    private float localHitStopUntil;

    public bool IsActive => Time.time < activeUntil;
    private bool IsLocallyHitStopped => Time.unscaledTime < localHitStopUntil;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Configure(float distanceMultiplier, float durationMultiplier)
    {
        receivedDistanceMultiplier = Mathf.Max(0f, distanceMultiplier);
        receivedDurationMultiplier = Mathf.Max(0f, durationMultiplier);
    }

    public void Apply(Vector2 source, float weaponDistance, float weaponDuration)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (body == null)
        {
            return;
        }

        Vector2 direction = body.position - source;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Random.insideUnitCircle;
        }

        ApplyDirection(direction.normalized, weaponDistance, weaponDuration);
    }

    public void ApplyDirection(Vector2 direction, float weaponDistance, float weaponDuration)
    {
        if (direction.sqrMagnitude < 0.001f || body == null)
        {
            return;
        }

        float distance = Mathf.Max(0f, weaponDistance) * receivedDistanceMultiplier;
        float duration = Mathf.Max(0f, weaponDuration) * Mathf.Max(0.01f, receivedDurationMultiplier);
        if (distance < minimumDistance || duration < minimumDuration)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        activeUntil = Time.time + duration;
        routine = StartCoroutine(ApplyRoutine(direction.normalized, distance, duration));
    }

    private IEnumerator ApplyRoutine(Vector2 direction, float distance, float duration)
    {
        Vector2 start = body.position;
        Vector2 end = start + direction * distance;
        body.velocity = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsLocallyHitStopped)
            {
                body.velocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
                continue;
            }

            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            body.MovePosition(Vector2.Lerp(start, end, eased));
            yield return new WaitForFixedUpdate();
        }

        body.velocity = Vector2.zero;
        activeUntil = 0f;
        routine = null;
    }

    public void PushHitStop(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        localHitStopUntil = Mathf.Max(localHitStopUntil, Time.unscaledTime + duration);
        if (body != null)
        {
            body.velocity = Vector2.zero;
        }
    }
}
