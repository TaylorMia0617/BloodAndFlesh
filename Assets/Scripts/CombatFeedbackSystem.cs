using UnityEngine;

public sealed class CombatFeedbackSystem : MonoBehaviour
{
    private static CombatFeedbackSystem instance;
    [SerializeField] private bool useGlobalHitStopFallback;
    private CameraShakeFeedback cameraShake;

    public static CombatFeedbackSystem Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<CombatFeedbackSystem>();
            if (instance != null)
            {
                return instance;
            }

            GameObject systemObject = new GameObject("CombatFeedbackSystem");
            instance = systemObject.AddComponent<CombatFeedbackSystem>();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        CombatFeedbackBus.HitConfirmed += OnHitConfirmed;
    }

    private void OnDisable()
    {
        CombatFeedbackBus.HitConfirmed -= OnHitConfirmed;
    }

    private void OnHitConfirmed(DamageContext context, HitResult result)
    {
        if (result.dealtDamage || result.feedback.hitStopDuration > 0f)
        {
            if (useGlobalHitStopFallback)
            {
                HitStopManager.Request(result.feedback.hitStopDuration, result.feedback.hitStopTimeScale);
            }
            else
            {
                LocalHitStopService.Request(result.feedback.hitStopDuration, context.instigator, context.hitPoint);
            }
        }

        if (result.feedback.cameraShake > 0f)
        {
            CameraShakeFeedback shake = EnsureCameraShake();
            if (shake != null)
            {
                shake.Shake(result.feedback.cameraShake, Mathf.Max(0.04f, result.feedback.hitStopDuration * 1.8f));
            }
        }
    }

    private CameraShakeFeedback EnsureCameraShake()
    {
        if (cameraShake != null)
        {
            return cameraShake;
        }

        Camera camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (camera == null)
        {
            return null;
        }

        cameraShake = camera.GetComponent<CameraShakeFeedback>();
        if (cameraShake == null)
        {
            cameraShake = camera.gameObject.AddComponent<CameraShakeFeedback>();
        }

        return cameraShake;
    }
}
