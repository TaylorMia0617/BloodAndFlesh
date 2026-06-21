using UnityEngine;

public class PrototypeDamageable : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private Color hitColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    private float currentHealth;
    private SpriteRenderer spriteRenderer;
    private CharacterStats stats;
    private SimpleEnemyAI enemyAI;
    private HitVolumeFeedback volumeFeedback;
    private Color baseColor;
    private float flashUntil;

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.maxHealth = Mathf.Max(stats.maxHealth, maxHealth);
            stats.ResetStats();
            currentHealth = stats.CurrentHealth;
        }
        else
        {
            currentHealth = maxHealth;
        }
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyAI = GetComponent<SimpleEnemyAI>();
        volumeFeedback = GetComponent<HitVolumeFeedback>();
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }
    }

    private void Update()
    {
        if (spriteRenderer != null && Time.time >= flashUntil)
        {
            spriteRenderer.color = baseColor;
        }
    }

    public void TakeDamage(float amount, float armorPiercing = 0f, Vector2 hitSource = default, CharacterStats attacker = null)
    {
        if (stats != null)
        {
            stats.ApplyDamage(amount, armorPiercing, attacker);
            currentHealth = stats.CurrentHealth;
        }
        else
        {
            currentHealth -= amount;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitColor;
            flashUntil = Time.time + flashDuration;
        }

        Vector2 source = hitSource == default ? (Vector2)transform.position + Vector2.down : hitSource;
        if (volumeFeedback == null)
        {
            volumeFeedback = GetComponent<HitVolumeFeedback>();
        }
        if (volumeFeedback != null)
        {
            volumeFeedback.Play(source, enemyAI != null ? 1.65f : 1f);
        }

        if (enemyAI == null)
        {
            enemyAI = GetComponent<SimpleEnemyAI>();
        }
        if (enemyAI != null)
        {
            enemyAI.OnDamaged(source, 1f);
        }

        if ((stats != null && !stats.IsAlive) || currentHealth <= 0f)
        {
            gameObject.SetActive(false);
        }
    }
}
