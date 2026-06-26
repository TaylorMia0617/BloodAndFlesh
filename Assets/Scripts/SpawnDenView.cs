using UnityEngine;

public sealed class SpawnDenView
{
    private readonly GameObject owner;
    private readonly SpriteRenderer spriteRenderer;

    public SpawnDenView(GameObject owner, SpriteRenderer spriteRenderer)
    {
        this.owner = owner;
        this.spriteRenderer = spriteRenderer;
    }

    public void ShowDamageFeedback()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.Lerp(Color.white, Color.red, 0.35f);
        }
    }

    public void ShowDestroyed()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.gray;
        }

        if (owner != null)
        {
            owner.SetActive(false);
        }
    }
}
