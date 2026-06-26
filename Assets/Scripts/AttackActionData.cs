using UnityEngine;

[System.Serializable]
public struct AttackActionData
{
    public bool useCustomTiming;
    [Min(0f)] public float windup;
    [Min(0f)] public float active;
    [Min(0f)] public float recovery;
    [Min(0f)] public float inputBuffer;
    [Range(0f, 0.15f)] public float hitStopDuration;
    [Range(0f, 1f)] public float hitStopTimeScale;
    [Min(0f)] public float knockbackDistance;
    [Min(0.01f)] public float knockbackDuration;
    [Range(0f, 2f)] public float cameraShake;
}
