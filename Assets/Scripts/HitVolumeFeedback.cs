using System.Collections;
using UnityEngine;

public class HitVolumeFeedback : MonoBehaviour
{
    [SerializeField] private float shrinkScale = 0.82f;
    [SerializeField] private float shrinkDuration = 0.055f;
    [SerializeField] private float recoverDuration = 0.16f;
    [SerializeField] private int particleCount = 18;

    private SpriteRenderer spriteRenderer;
    private ParticleSystem particles;
    private Coroutine scaleRoutine;
    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureParticles();
    }

    public void Play(Vector2 hitSource)
    {
        Play(hitSource, 1f);
    }

    public void Play(Vector2 hitSource, float intensity)
    {
        Vector2 away = (Vector2)transform.position - hitSource;
        if (away.sqrMagnitude < 0.001f)
        {
            away = Vector2.up;
        }
        away.Normalize();

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
        }
        scaleRoutine = StartCoroutine(ScaleRoutine(Mathf.Max(0.1f, intensity)));
        EmitLostVolume(away, Mathf.Max(0.1f, intensity));
    }

    private IEnumerator ScaleRoutine(float intensity)
    {
        Vector3 start = transform.localScale;
        float effectiveShrink = Mathf.Clamp(shrinkScale - (intensity - 1f) * 0.08f, 0.62f, 0.94f);
        Vector3 shrunken = baseScale * effectiveShrink;
        float elapsed = 0f;

        float fastShrink = shrinkDuration * Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(intensity - 1f));
        while (elapsed < fastShrink)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, fastShrink));
            transform.localScale = Vector3.Lerp(start, shrunken, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, recoverDuration));
            t = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.Lerp(shrunken, baseScale, t);
            yield return null;
        }

        transform.localScale = baseScale;
        scaleRoutine = null;
    }

    private void EmitLostVolume(Vector2 away, float intensity)
    {
        if (particles == null)
        {
            return;
        }

        Color color = spriteRenderer != null ? spriteRenderer.color : new Color(0.9f, 0.9f, 0.9f, 0.85f);
        color.a = Mathf.Max(0.65f, color.a);
        Vector2 side = new Vector2(-away.y, away.x);
        int count = Mathf.Clamp(Mathf.RoundToInt(particleCount * intensity), 8, 56);
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = (Vector2)transform.position + Random.insideUnitCircle * 0.34f;
            emitParams.velocity = away * Random.Range(1.2f, 3.6f) * intensity + side * Random.Range(-1.35f, 1.35f);
            emitParams.startColor = color;
            emitParams.startSize = Random.Range(0.075f, 0.2f) * Mathf.Lerp(1f, 1.35f, Mathf.Clamp01(intensity - 1f));
            emitParams.startLifetime = Random.Range(0.2f, 0.46f);
            particles.Emit(emitParams, 1);
        }
    }

    private void EnsureParticles()
    {
        if (particles != null)
        {
            return;
        }

        Transform existing = transform.Find("LostVolumeParticles");
        GameObject particleObject = existing != null ? existing.gameObject : new GameObject("LostVolumeParticles");
        particleObject.transform.SetParent(transform);
        particleObject.transform.localPosition = Vector3.zero;
        particleObject.transform.localRotation = Quaternion.identity;

        particles = particleObject.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = 0.2f;
        main.startSize = 0.1f;
        main.maxParticles = 192;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 46;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
    }
}
