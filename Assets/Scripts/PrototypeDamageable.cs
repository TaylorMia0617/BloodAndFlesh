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
    private KnockbackReceiver knockbackReceiver;
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
        knockbackReceiver = GetComponent<KnockbackReceiver>();
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

    public HitResult ApplyDamage(in DamageContext context)
    {
        bool wasAlive = stats != null ? stats.IsAlive : currentHealth > 0f;
        DamageResult damageResult = new DamageResult(context.baseDamage, context.baseDamage, context.baseDamage, false);
        if (stats != null)
        {
            HitResult statResult = stats.ApplyDamage(context);
            currentHealth = stats.CurrentHealth;
            damageResult = new DamageResult(context.baseDamage, statResult.finalDamage, statResult.finalDamage, statResult.critical);
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - context.baseDamage);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitColor;
            flashUntil = Time.time + flashDuration;
        }

        Vector2 source = context.origin == default ? (Vector2)transform.position + Vector2.down : context.origin;
        if (volumeFeedback == null)
        {
            volumeFeedback = GetComponent<HitVolumeFeedback>();
        }
        if (volumeFeedback != null)
        {
            float intensity = context.feedback.intensity * (enemyAI != null ? 1.65f : 1f);
            volumeFeedback.Play(source, intensity);
        }

        if (enemyAI == null)
        {
            enemyAI = GetComponent<SimpleEnemyAI>();
        }
        if (enemyAI != null)
        {
            enemyAI.OnDamaged(source, context.feedback.knockbackDistance, context.feedback.knockbackDuration);
        }

        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
        if (knockbackReceiver != null)
        {
            knockbackReceiver.Apply(source, context.feedback.knockbackDistance, context.feedback.knockbackDuration);
        }

        bool killed = (stats != null && !stats.IsAlive) || currentHealth <= 0f;
        if (killed)
        {
            if (wasAlive && enemyAI != null)
            {
                DropTableResolver.ResolveEnemyDrops(EnemyConfigDatabase.Get(enemyAI.Archetype), transform.position);
            }

            gameObject.SetActive(false);
        }

        bool dealtDamage = stats != null ? damageResult.finalDamage > 0f : context.baseDamage > 0f;
        return new HitResult(true, dealtDamage, wasAlive && killed, damageResult.isCritical, stats != null ? damageResult.finalDamage : context.baseDamage, context.feedback);
    }

    public void TakeDamage(float amount, float armorPiercing = 0f, Vector2 hitSource = default, CharacterStats attacker = null)
    {
        ApplyDamage(DamageContext.Legacy(amount, armorPiercing, hitSource, attacker));
    }
}
