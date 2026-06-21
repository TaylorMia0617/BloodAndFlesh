using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlayerVisionMask : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private GridRouteMapGenerator mapGenerator;
    [SerializeField] private float visionRadius = 5f;
    [SerializeField] private float feather = 0.75f;
    [SerializeField] private int rayCount = 192;
    [SerializeField] private float rayStep = 0.08f;

    private Camera targetCamera;
    private Material maskMaterial;
    private Texture2D rayDistanceTexture;
    private Color[] rayDistancePixels;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.backgroundColor = Color.black;
        EnsureMaterial();
        ResolveMapGenerator();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (maskMaterial == null || target == null || targetCamera == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        ResolveMapGenerator();
        UpdateRayDistanceTexture();
        maskMaterial.SetVector("_PlayerPosition", new Vector4(target.position.x, target.position.y, 0f, 0f));
        maskMaterial.SetVector("_CameraCenter", new Vector4(targetCamera.transform.position.x, targetCamera.transform.position.y, 0f, 0f));
        if (rayDistanceTexture != null)
        {
            maskMaterial.SetTexture("_RayDistanceTex", rayDistanceTexture);
            maskMaterial.SetFloat("_RayCount", rayCount);
            maskMaterial.SetFloat("_UseRayDistanceTex", 1f);
        }
        else
        {
            maskMaterial.SetFloat("_UseRayDistanceTex", 0f);
        }
        maskMaterial.SetFloat("_OrthographicSize", targetCamera.orthographicSize);
        maskMaterial.SetFloat("_Aspect", targetCamera.aspect);
        maskMaterial.SetFloat("_VisionRadius", visionRadius);
        maskMaterial.SetFloat("_Feather", feather);
        Graphics.Blit(source, destination, maskMaterial);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMapGenerator(GridRouteMapGenerator newMapGenerator)
    {
        mapGenerator = newMapGenerator;
    }

    private void EnsureMaterial()
    {
        Shader shader = Shader.Find("TopDownRogue/VisionMask");
        if (shader == null)
        {
            Debug.LogError("Missing shader: TopDownRogue/VisionMask");
            return;
        }

        maskMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        maskMaterial.SetColor("_MaskColor", Color.black);
    }

    private void ResolveMapGenerator()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindObjectOfType<GridRouteMapGenerator>();
        }
    }

    private void UpdateRayDistanceTexture()
    {
        EnsureRayDistanceTexture();
        if (target == null)
        {
            return;
        }

        Vector2 origin = target.position;
        float maxDistance = visionRadius + feather;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (i / (float)rayCount) * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float hitDistance = CastVisionRay(origin, direction, maxDistance);
            float normalizedDistance = Mathf.Clamp01(hitDistance / Mathf.Max(0.001f, maxDistance));
            rayDistancePixels[i] = new Color(normalizedDistance, normalizedDistance, normalizedDistance, 1f);
        }

        rayDistanceTexture.SetPixels(rayDistancePixels);
        rayDistanceTexture.Apply(false);
    }

    private void EnsureRayDistanceTexture()
    {
        int clampedRayCount = Mathf.Clamp(rayCount, 32, 512);
        if (rayDistanceTexture != null && rayDistanceTexture.width == clampedRayCount)
        {
            return;
        }

        rayCount = clampedRayCount;
        rayDistanceTexture = new Texture2D(rayCount, 1, TextureFormat.RFloat, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };
        rayDistancePixels = new Color[rayCount];
    }

    private float CastVisionRay(Vector2 origin, Vector2 direction, float maxDistance)
    {
        if (mapGenerator == null)
        {
            return maxDistance;
        }

        float distance = 0f;
        float step = Mathf.Max(0.02f, rayStep);
        while (distance < maxDistance)
        {
            distance += step;
            Vector2 sample = origin + direction * distance;
            Vector2Int cell = mapGenerator.WorldToGrid(sample);
            if (mapGenerator.BlocksVision(cell))
            {
                return distance;
            }
        }

        return maxDistance;
    }
}
