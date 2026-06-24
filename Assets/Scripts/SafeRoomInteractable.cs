using UnityEngine;

public class SafeRoomInteractable : MonoBehaviour
{
    public enum SafeRoomAction
    {
        Entrance,
        Exit,
        WishStatue,
        Shopkeeper,
        BlackMarket,
        TaskBoard,
        WanderNpc,
        Prop
    }

    [SerializeField] private string description;
    [SerializeField] private SafeRoomAction action;
    private TextMesh promptText;
    private SpriteRenderer promptBackground;

    public SafeRoomAction Action => action;

    public void Configure(string nextDescription, bool isExit)
    {
        Configure(nextDescription, isExit ? SafeRoomAction.Exit : SafeRoomAction.Prop);
    }

    public void Configure(string nextDescription, SafeRoomAction nextAction)
    {
        description = nextDescription;
        action = nextAction;
        EnsurePrompt();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerInputManager player = other.GetComponent<PlayerInputManager>();
        if (player == null)
        {
            return;
        }

        SafeRoomManager.Instance.SetCurrentInteractable(this, player.transform);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerInputManager player = other.GetComponent<PlayerInputManager>();
        if (player == null)
        {
            return;
        }

        SafeRoomManager.Instance.ClearCurrentInteractable(this);
    }

    public void Interact(Transform player)
    {
        SafeRoomManager.Instance.HandleInteraction(action, player, description);
    }

    private void EnsurePrompt()
    {
        if (promptText != null)
        {
            return;
        }

        GameObject promptObject = new GameObject("InteractPrompt");
        promptObject.transform.SetParent(transform);
        promptObject.transform.localPosition = new Vector3(0f, 0.86f, 0f);

        GameObject backgroundObject = new GameObject("PromptBackground");
        backgroundObject.transform.SetParent(promptObject.transform);
        backgroundObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        backgroundObject.transform.localScale = new Vector3(0.60f, 0.46f, 1f);
        promptBackground = backgroundObject.AddComponent<SpriteRenderer>();
        promptBackground.sprite = CreatePromptBackgroundSprite();
        promptBackground.color = new Color(0.94f, 0.84f, 0.55f, 0.92f);
        promptBackground.sortingOrder = 69;

        promptText = promptObject.AddComponent<TextMesh>();
        promptText.text = "F";
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.characterSize = 0.50f;
        promptText.color = new Color(0.02f, 0.018f, 0.014f, 1f);
        MeshRenderer renderer = promptObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 70;
        }

        SetPromptVisible(false);
    }

    public void SetFocused(bool focused)
    {
        SetPromptVisible(focused);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptText != null)
        {
            promptText.gameObject.SetActive(visible);
        }
    }

    private Sprite CreatePromptBackgroundSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        Vector2 center = new Vector2(15.5f, 15.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float rounded = Mathf.Max(Mathf.Abs(delta.x) / 13f, Mathf.Abs(delta.y) / 10f);
                texture.SetPixel(x, y, rounded <= 1f ? fill : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
