using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class ImpactBlurFeedback : MonoBehaviour
{
    [SerializeField] private float maxIntensity = 0.75f;

    private Material material;
    private Coroutine routine;
    private float intensity;

    public void Pulse(float duration, float strength)
    {
        if (duration <= 0f || strength <= 0f)
        {
            return;
        }

        EnsureMaterial();
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PulseRoutine(duration, Mathf.Min(maxIntensity, strength)));
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null || intensity <= 0.001f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetFloat("_Intensity", intensity);
        Graphics.Blit(source, destination, material);
    }

    private void EnsureMaterial()
    {
        if (material != null)
        {
            return;
        }

        Shader shader = Shader.Find("TopDownRogue/ImpactBlur");
        if (shader == null)
        {
            Debug.LogError("Missing shader: TopDownRogue/ImpactBlur");
            return;
        }

        material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private IEnumerator PulseRoutine(float duration, float strength)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            intensity = strength * (1f - t);
            yield return null;
        }

        intensity = 0f;
        routine = null;
    }
}
