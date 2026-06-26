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
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 0.72f, 0f);
    [SerializeField] private Vector3 promptScale = new Vector3(0.72f, 0.72f, 1f);
    [SerializeField] private string promptSpriteResource = "Arts/UI/SafeHouse/ui_interact_prompt_f_comic";
    private GameObject promptObject;
    private SpriteRenderer promptRenderer;

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

        SafeRoomManager manager = SafeRoomManager.Instance;
        if (manager != null)
        {
            manager.SetCurrentInteractable(this, player.transform);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerInputManager player = other.GetComponent<PlayerInputManager>();
        if (player == null)
        {
            return;
        }

        SafeRoomManager manager = SafeRoomManager.Instance;
        if (manager != null)
        {
            manager.ClearCurrentInteractable(this);
        }
    }

    public void Interact(Transform player)
    {
        SafeRoomManager manager = SafeRoomManager.Instance;
        if (manager != null)
        {
            manager.HandleInteraction(action, player, description);
        }
    }

    private void LateUpdate()
    {
        if (promptObject == null || !promptObject.activeSelf)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera != null)
        {
            promptObject.transform.rotation = targetCamera.transform.rotation;
        }

        promptObject.transform.localScale = promptScale;
    }

    private void EnsurePrompt()
    {
        if (promptRenderer != null)
        {
            return;
        }

        promptObject = new GameObject("InteractPrompt");
        promptObject.transform.SetParent(transform);
        promptObject.transform.localPosition = promptOffset;
        promptObject.transform.localScale = promptScale;

        promptRenderer = promptObject.AddComponent<SpriteRenderer>();
        promptRenderer.sprite = Resources.Load<Sprite>(promptSpriteResource) ?? CreatePromptFallbackSprite();
        promptRenderer.sortingOrder = 70;

        SetPromptVisible(false);
    }

    public void SetFocused(bool focused)
    {
        SetPromptVisible(focused);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptObject != null)
        {
            promptObject.SetActive(visible);
        }
    }

    private Sprite CreatePromptFallbackSprite()
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
