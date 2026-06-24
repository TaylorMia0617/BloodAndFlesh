using System.Collections;
using UnityEngine;

public sealed class CameraShakeFeedback : MonoBehaviour
{
    private Coroutine routine;
    private Vector3 baseLocalPosition;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    public void Shake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            transform.localPosition = baseLocalPosition;
        }

        routine = StartCoroutine(ShakeRoutine(amplitude, duration));
    }

    private IEnumerator ShakeRoutine(float amplitude, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float strength = amplitude * (1f - t);
            Vector2 offset = Random.insideUnitCircle * strength;
            transform.localPosition = baseLocalPosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        transform.localPosition = baseLocalPosition;
        routine = null;
    }
}
