using UnityEngine;

public readonly struct FeedbackPayload
{
    public readonly float hitStopDuration;
    public readonly float hitStopTimeScale;
    public readonly float knockbackDistance;
    public readonly float knockbackDuration;
    public readonly float intensity;
    public readonly float cameraShake;

    public FeedbackPayload(float hitStopDuration, float hitStopTimeScale, float knockbackDistance, float knockbackDuration, float intensity, float cameraShake)
    {
        this.hitStopDuration = Mathf.Max(0f, hitStopDuration);
        this.hitStopTimeScale = Mathf.Clamp01(hitStopTimeScale);
        this.knockbackDistance = Mathf.Max(0f, knockbackDistance);
        this.knockbackDuration = Mathf.Max(0.01f, knockbackDuration);
        this.intensity = Mathf.Max(0f, intensity);
        this.cameraShake = Mathf.Max(0f, cameraShake);
    }

    public static FeedbackPayload None => new FeedbackPayload(0f, 1f, 0f, 0.01f, 1f, 0f);

    public static FeedbackPayload FromWeapon(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return None;
        }

        return new FeedbackPayload(
            weapon.hitStopDuration,
            weapon.hitStopTimeScale,
            weapon.knockbackDistance,
            weapon.knockbackDuration,
            weapon.feedbackIntensity,
            weapon.cameraShake);
    }
}
