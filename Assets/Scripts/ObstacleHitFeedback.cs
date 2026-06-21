using UnityEngine;

public class ObstacleHitFeedback : MonoBehaviour, IDamageable
{
    [SerializeField] private Color hitColor = new Color(1f, 0.86f, 0.52f, 1f);
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private float scalePunch = 0.06f;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private Vector3 baseScale;
    private float flashUntil;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }
    }

    private void Update()
    {
        if (Time.time < flashUntil)
        {
            float t = Mathf.InverseLerp(flashUntil - flashDuration, flashUntil, Time.time);
            float punch = Mathf.Sin(t * Mathf.PI) * scalePunch;
            transform.localScale = baseScale * (1f + punch);
            return;
        }

        transform.localScale = baseScale;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseColor;
        }
    }

    public void TakeDamage(float amount, float armorPiercing = 0f, Vector2 hitSource = default, CharacterStats attacker = null)
    {
        flashUntil = Time.time + flashDuration;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitColor;
        }
    }
}
