using UnityEngine;

public sealed class SafeRoomInterestVfx : MonoBehaviour
{
    private ParticleSystem particles;
    private SpriteRenderer lightBeam;
    private Color particleColor = Color.white;
    private bool isStatue;

    public void Configure(Color color, bool statue)
    {
        particleColor = color;
        isStatue = statue;
        EnsureParticles();
        if (isStatue)
        {
            EnsureLightBeam();
        }
    }

    public void SetUsed(bool used)
    {
        if (particles != null)
        {
            ParticleSystem.MainModule main = particles.main;
            Color color = particleColor;
            color.a = used ? 0.22f : particleColor.a;
            main.startColor = color;
        }

        if (lightBeam != null)
        {
            Color color = lightBeam.color;
            color.a = used ? 0.12f : 0.34f;
            lightBeam.color = color;
        }
    }

    private void EnsureParticles()
    {
        if (particles != null)
        {
            return;
        }

        GameObject particleObject = new GameObject("InterestParticles");
        particleObject.transform.SetParent(transform);
        particleObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        particles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = isStatue ? 0.85f : 0.65f;
        main.startSpeed = isStatue ? 0.42f : 0.22f;
        main.startSize = isStatue ? 0.08f : 0.055f;
        main.startColor = particleColor;
        main.maxParticles = isStatue ? 48 : 28;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = isStatue ? 18f : 8f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = isStatue ? 0.38f : 0.25f;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 36;
        renderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void EnsureLightBeam()
    {
        if (lightBeam != null)
        {
            return;
        }

        GameObject beamObject = new GameObject("DivineLightBeam");
        beamObject.transform.SetParent(transform);
        beamObject.transform.localPosition = new Vector3(0f, 0.82f, 0f);
        beamObject.transform.localScale = new Vector3(0.65f, 1.75f, 1f);
        lightBeam = beamObject.AddComponent<SpriteRenderer>();
        lightBeam.sprite = CreateBeamSprite();
        lightBeam.sortingOrder = 13;
        lightBeam.color = new Color(0.78f, 0.68f, 1f, 0.34f);
    }

    private static Sprite CreateBeamSprite()
    {
        const int width = 64;
        const int height = 160;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = Mathf.Abs((x + 0.5f) / width - 0.5f) * 2f;
                float vertical = 1f - Mathf.Clamp01(y / (float)height);
                float alpha = Mathf.Clamp01(1f - nx) * (0.15f + vertical * 0.85f);
                texture.SetPixel(x, y, new Color(0.82f, 0.76f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 100f);
    }
}
