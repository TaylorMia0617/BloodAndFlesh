using UnityEngine;

public class SafeRoomManager : MonoBehaviour
{
    private static SafeRoomManager instance;

    [SerializeField] private Vector2 safeRoomOrigin = new Vector2(0f, -120f);
    [SerializeField] private Vector2 returnOffset = new Vector2(0f, -1.2f);

    private Transform safeRoomRoot;
    private Vector3 returnPosition;
    private PlayerVisionMask cachedVisionMask;
    private bool built;

    public static SafeRoomManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            SafeRoomManager existing = FindObjectOfType<SafeRoomManager>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            GameObject managerObject = new GameObject("SafeRoomManager");
            instance = managerObject.AddComponent<SafeRoomManager>();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void EnterSafeRoom(Transform player, Transform entrance)
    {
        if (player == null)
        {
            return;
        }

        BuildSafeRoom();
        returnPosition = entrance != null ? entrance.position + (Vector3)returnOffset : player.position;
        SetSafeRoomVisibilityMode(true);
        player.position = safeRoomOrigin + new Vector2(0f, -2.2f);
        safeRoomRoot.gameObject.SetActive(true);
    }

    public void ExitSafeRoom(Transform player)
    {
        if (player == null)
        {
            return;
        }

        player.position = returnPosition;
        SetSafeRoomVisibilityMode(false);
    }

    private void BuildSafeRoom()
    {
        if (built)
        {
            return;
        }

        GameObject rootObject = new GameObject("SafeRoom_Page");
        safeRoomRoot = rootObject.transform;
        built = true;

        CreateFloor();
        CreateMarker("SafeRoomExit", safeRoomOrigin + new Vector2(0f, -3.8f), new Color(0.68f, 0.78f, 0.82f, 1f), "Exit Safe Room", true);
        CreateMarker("Shopkeeper", safeRoomOrigin + new Vector2(-3.5f, 1.7f), new Color(0.92f, 0.86f, 0.58f, 1f), "Shop: healing, temp buffs, weapon upgrades", false);
        CreateMarker("QuestNpc_Kill", safeRoomOrigin + new Vector2(3.2f, 1.8f), new Color(0.82f, 0.92f, 1f, 1f), "Quest: kill target enemies", false);
        CreateMarker("QuestNpc_Gather", safeRoomOrigin + new Vector2(3.6f, -0.7f), new Color(0.76f, 0.9f, 0.72f, 1f), "Quest: collect resources", false);
        CreateMarker("BuffChoice", safeRoomOrigin + new Vector2(0f, 2.6f), new Color(0.64f, 0.72f, 1f, 1f), "Buff choice: HP / Mana / Attack / Armor / Speed / Cooldown", false);

        CreateWanderer("WanderNpc_A", safeRoomOrigin + new Vector2(-1.7f, 0.7f));
        CreateWanderer("WanderNpc_B", safeRoomOrigin + new Vector2(1.4f, 0.4f));
        CreateWanderer("WanderNpc_C", safeRoomOrigin + new Vector2(-0.2f, -1.2f));
    }

    private void CreateFloor()
    {
        Sprite sprite = CreateSprite(new Color(0.17f, 0.18f, 0.18f, 1f), 96);
        for (int y = -3; y <= 3; y++)
        {
            for (int x = -5; x <= 5; x++)
            {
                GameObject tile = new GameObject($"SafeFloor_{x}_{y}");
                tile.transform.SetParent(safeRoomRoot);
                tile.transform.position = safeRoomOrigin + new Vector2(x, y);
                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 0;
            }
        }
    }

    private void CreateMarker(string name, Vector2 position, Color color, string description, bool exit)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(safeRoomRoot);
        marker.transform.position = position;
        SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite(color, 64);
        renderer.sortingOrder = 18;
        marker.transform.localScale = exit ? Vector3.one * 1.2f : Vector3.one * 0.75f;

        CircleCollider2D trigger = marker.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = exit ? 0.65f : 0.55f;

        SafeRoomInteractable interactable = marker.AddComponent<SafeRoomInteractable>();
        interactable.Configure(description, exit);
    }

    private void CreateWanderer(string name, Vector2 position)
    {
        GameObject npc = new GameObject(name);
        npc.transform.SetParent(safeRoomRoot);
        npc.transform.position = position;
        SpriteRenderer renderer = npc.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite(new Color(0.84f, 0.84f, 0.9f, 1f), 64);
        renderer.sortingOrder = 20;
        npc.transform.localScale = Vector3.one * 0.62f;
        npc.AddComponent<SafeRoomNpc>().Configure(safeRoomOrigin, new Vector2(4.2f, 2.4f));
    }

    private Sprite CreateSprite(Color color, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void SetSafeRoomVisibilityMode(bool inSafeRoom)
    {
        if (cachedVisionMask == null && Camera.main != null)
        {
            cachedVisionMask = Camera.main.GetComponent<PlayerVisionMask>();
        }

        if (cachedVisionMask != null)
        {
            cachedVisionMask.enabled = !inSafeRoom;
        }
    }
}
