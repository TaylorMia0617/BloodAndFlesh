using System.Collections.Generic;
using UnityEngine;

public static class SensoryEventBus
{
    private struct SensoryEvent
    {
        public SensoryEventType type;
        public Vector2 position;
        public float radius;
        public float expiresAt;
    }

    private static readonly List<SensoryEvent> events = new List<SensoryEvent>();

    public static void Publish(SensoryEventType type, Vector2 position, float radius, float duration)
    {
        if (radius <= 0f || duration <= 0f)
        {
            return;
        }

        Prune();
        events.Add(new SensoryEvent
        {
            type = type,
            position = position,
            radius = radius,
            expiresAt = Time.time + duration
        });
    }

    public static bool TryGetLatest(SensoryEventType type, Vector2 listenerPosition, float listenerRange, out Vector2 eventPosition)
    {
        Prune();
        eventPosition = Vector2.zero;
        if (listenerRange <= 0f)
        {
            return false;
        }

        float newestExpiry = float.MinValue;
        bool found = false;
        for (int i = 0; i < events.Count; i++)
        {
            SensoryEvent sensoryEvent = events[i];
            if (sensoryEvent.type != type)
            {
                continue;
            }

            float effectiveRange = Mathf.Min(listenerRange, sensoryEvent.radius);
            if (Vector2.Distance(listenerPosition, sensoryEvent.position) > effectiveRange)
            {
                continue;
            }

            if (sensoryEvent.expiresAt > newestExpiry)
            {
                newestExpiry = sensoryEvent.expiresAt;
                eventPosition = sensoryEvent.position;
                found = true;
            }
        }

        return found;
    }

    private static void Prune()
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            if (events[i].expiresAt <= Time.time)
            {
                events.RemoveAt(i);
            }
        }
    }
}
