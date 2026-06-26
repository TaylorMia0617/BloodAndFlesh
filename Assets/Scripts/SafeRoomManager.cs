using UnityEngine;
using System.Collections.Generic;

public class SafeRoomManager : MonoBehaviour
{
    private static SafeRoomManager instance;
    private static bool isShuttingDown;

    [Header("Layout")]
    [SerializeField] private Vector2 safeRoomOrigin = new Vector2(0f, -120f);
    [SerializeField] private int roomWidth = 10;
    [SerializeField] private int roomHeight = 10;
    [SerializeField] private float blackMarketSpawnChance = 0.25f;

    [Header("Runtime")]
    [SerializeField] private bool wishStatueUsed;
    [SerializeField] private bool blackMarketSpawned;

    private Transform safeRoomRoot;
    private PlayerVisionMask cachedVisionMask;
    private Sprite fallbackPropSprite;
    private SafeRoomInterestVfx wishStatueVfx;
    private SafeRoomInteractable currentInteractable;
    private Transform currentPlayer;
    private readonly List<SafeRoomInteractable> nearbyInteractables = new List<SafeRoomInteractable>();
    private bool interactionPromptsSuppressed;

    public static SafeRoomManager Instance
    {
        get
        {
            if (isShuttingDown)
            {
                return null;
            }

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

            if (!Application.isPlaying)
            {
                return null;
            }

            GameObject managerObject = new GameObject("SafeRoomManager");
            instance = managerObject.AddComponent<SafeRoomManager>();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isShuttingDown = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        isShuttingDown = false;
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            isShuttingDown = true;
        }
    }

    private void Start()
    {
        PreloadSafeRoom();
    }

    private void Update()
    {
        if (!interactionPromptsSuppressed && currentPlayer != null && nearbyInteractables.Count > 0)
        {
            RefreshFocusedInteractable();
        }
    }

    private void PreloadSafeRoom()
    {
        if (safeRoomRoot != null)
        {
            return;
        }

        BuildSafeRoomTemplate();
        if (safeRoomRoot != null)
        {
            safeRoomRoot.gameObject.SetActive(false);
        }
    }

    public void EnterSafeRoom(Transform player, Transform entrance)
    {
        if (player == null)
        {
            return;
        }

        BuildSafeRoomTemplate();
        ResetInteractionFocus();
        RunLevelManager.Instance.EnterSafeRoom(entrance != null ? entrance.position : player.position);
        SetSafeRoomVisibilityMode(true);
        player.position = GridToWorld(5, 8) + Vector2.up * 0.15f;
        safeRoomRoot.gameObject.SetActive(true);
    }

    public void ExitSafeRoom(Transform player)
    {
        if (player == null)
        {
            return;
        }

        if (!wishStatueUsed)
        {
            Debug.Log("The exit is sealed. Use the wish statue before leaving the safe house.");
            return;
        }

        RunLevelManager.Instance.AdvanceStage();
        GridRouteMapGenerator generator = FindObjectOfType<GridRouteMapGenerator>();
        if (generator != null)
        {
            generator.GenerateMap();
            player.position = generator.PlayerSpawnPosition;
        }

        RunLevelManager.Instance.ExitSafeRoom();
        SetSafeRoomVisibilityMode(false);
        ResetInteractionFocus();
        if (safeRoomRoot != null)
        {
            safeRoomRoot.gameObject.SetActive(false);
        }
    }

    public void HandleInteraction(SafeRoomInteractable.SafeRoomAction action, Transform player, string description)
    {
        switch (action)
        {
            case SafeRoomInteractable.SafeRoomAction.Exit:
                ExitSafeRoom(player);
                break;
            case SafeRoomInteractable.SafeRoomAction.WishStatue:
                UseWishStatue(player);
                break;
            case SafeRoomInteractable.SafeRoomAction.Shopkeeper:
                GameFlowManager.Instance.ShowShopPanel("normalShop");
                break;
            case SafeRoomInteractable.SafeRoomAction.BlackMarket:
                GameFlowManager.Instance.ShowShopPanel("blackMarket");
                break;
            case SafeRoomInteractable.SafeRoomAction.TaskBoard:
                GameFlowManager.Instance.ShowTaskBoardPanel();
                break;
            default:
                Debug.Log(description);
                break;
        }
    }

    public void SetCurrentInteractable(SafeRoomInteractable interactable, Transform player)
    {
        currentPlayer = player;
        if (interactable != null && !nearbyInteractables.Contains(interactable))
        {
            nearbyInteractables.Add(interactable);
        }

        RefreshFocusedInteractable();
    }

    public void ClearCurrentInteractable(SafeRoomInteractable interactable)
    {
        if (interactable != null)
        {
            nearbyInteractables.Remove(interactable);
            interactable.SetFocused(false);
        }

        if (currentInteractable == interactable)
        {
            currentInteractable = null;
        }

        RefreshFocusedInteractable();
    }

    public bool TryInteractCurrent(Transform player)
    {
        currentPlayer = player;
        RefreshFocusedInteractable();
        if (currentInteractable == null || currentPlayer == null || player != currentPlayer)
        {
            return false;
        }

        currentInteractable.Interact(player);
        return true;
    }

    public void SetInteractionPromptsSuppressed(bool suppressed)
    {
        interactionPromptsSuppressed = suppressed;
        if (interactionPromptsSuppressed)
        {
            currentInteractable = null;
            SetAllInteractablePrompts(false);
            return;
        }

        RefreshFocusedInteractable();
    }

    private void RefreshFocusedInteractable()
    {
        currentInteractable = null;
        if (currentPlayer == null || interactionPromptsSuppressed)
        {
            SetAllInteractablePrompts(false);
            return;
        }

        SafeRoomInteractable best = null;
        float bestScore = float.MaxValue;
        PlayerInputManager inputManager = currentPlayer.GetComponent<PlayerInputManager>();
        Vector2 facing = inputManager != null ? inputManager.FacingDirection : (Vector2)currentPlayer.up;
        if (facing.sqrMagnitude < 0.001f)
        {
            facing = Vector2.up;
        }

        for (int i = nearbyInteractables.Count - 1; i >= 0; i--)
        {
            SafeRoomInteractable candidate = nearbyInteractables[i];
            if (candidate == null)
            {
                nearbyInteractables.RemoveAt(i);
                continue;
            }

            Vector2 toCandidate = candidate.transform.position - currentPlayer.position;
            float distance = toCandidate.magnitude;
            float dot = distance > 0.001f ? Vector2.Dot(facing.normalized, toCandidate / distance) : 1f;
            if (dot < -0.2f)
            {
                candidate.SetFocused(false);
                continue;
            }

            float score = distance - dot * 0.35f;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        currentInteractable = best;
        for (int i = 0; i < nearbyInteractables.Count; i++)
        {
            if (nearbyInteractables[i] != null)
            {
                nearbyInteractables[i].SetFocused(nearbyInteractables[i] == currentInteractable);
            }
        }
    }

    private void SetAllInteractablePrompts(bool focused)
    {
        for (int i = 0; i < nearbyInteractables.Count; i++)
        {
            if (nearbyInteractables[i] != null)
            {
                nearbyInteractables[i].SetFocused(focused);
            }
        }
    }

    private void ResetInteractionFocus()
    {
        SetAllInteractablePrompts(false);
        nearbyInteractables.Clear();
        currentInteractable = null;
        currentPlayer = null;
    }

    private void UseWishStatue(Transform player)
    {
        if (wishStatueUsed)
        {
            Debug.Log("The wish statue is silent. This statue can only be used once.");
            return;
        }

        ShowWishBuffChoicesFromConfig(player);
    }

    private void ShowWishBuffChoicesFromConfig(Transform player)
    {
        BuffConfigDatabase.BuffConfig[] configs = BuffConfigDatabase.GetWishChoices(3);
        GameFlowManager.BuffChoice[] choices;
        if (configs.Length > 0)
        {
            choices = new GameFlowManager.BuffChoice[configs.Length];
            for (int i = 0; i < configs.Length; i++)
            {
                choices[i] = new GameFlowManager.BuffChoice(configs[i].id, configs[i].name, configs[i].description, configs[i].rarity);
            }
        }
        else
        {
            choices = new[]
            {
                new GameFlowManager.BuffChoice("flat_max_hp_10", "石心祝福", "直接获得最大生命值 +10。"),
                new GameFlowManager.BuffChoice("weapon_damage_small", "锋刃祝福", "武器造成的伤害提高 12%。"),
                new GameFlowManager.BuffChoice("after_kill_speed", "猎杀余势", "击杀敌人后获得 3 秒移动速度 +25%。")
            };
        }

        GameFlowManager.Instance.ShowBuffChoicePanel(
            "神像祝福",
            choices,
            choice =>
            {
                GrantWishBuff(player, choice);
                wishStatueUsed = true;
                if (wishStatueVfx != null)
                {
                    wishStatueVfx.SetUsed(true);
                }
            });
    }

    private void ShowWishBuffChoices(Transform player)
    {
        GameFlowManager.Instance.ShowBuffChoicePanel(
            "Wish Statue",
            new[]
            {
                new GameFlowManager.BuffChoice("flat_max_hp_10", "坚韧祈愿", "最大生命值 +10。"),
                new GameFlowManager.BuffChoice("weapon_damage_small", "锋刃祈愿", "武器伤害 +12%。"),
                new GameFlowManager.BuffChoice("after_kill_speed", "猎杀余势", "击杀敌人后获得短暂移速。")
            },
            choice => GrantWishBuff(player, choice));
    }

    private void GrantWishBuff(Transform player, GameFlowManager.BuffChoice choice)
    {
        if (player == null)
        {
            return;
        }

        PlayerBuffPool buffPool = player.GetComponent<PlayerBuffPool>();
        if (buffPool == null)
        {
            buffPool = player.gameObject.AddComponent<PlayerBuffPool>();
        }

        Sprite icon = Resources.Load<Sprite>("Arts/UI/Buffs/buff_wish");
        buffPool.AddBuff(choice.id, choice.title, icon);
        ApplyWishBuffEffects(player, choice.id);
        Debug.Log($"Wish buff selected: {choice.title}. Safe house exit unlocked.");
    }

    private void ApplyWishBuffEffects(Transform player, string buffId)
    {
        BuffConfigDatabase.BuffConfig config = BuffConfigDatabase.Get(buffId);
        if (config == null || config.effects == null || config.effects.Length == 0)
        {
            Debug.LogWarning($"Wish buff {buffId} has no configured effects.");
            return;
        }

        CharacterStats stats = player.GetComponent<CharacterStats>();
        for (int i = 0; i < config.effects.Length; i++)
        {
            BuffConfigDatabase.BuffEffect effect = config.effects[i];
            if (effect == null)
            {
                continue;
            }

            if (effect.target == "player" && effect.stat == "maxHealth" && effect.mode == "add")
            {
                if (stats != null)
                {
                    stats.ModifyMaxHealth(effect.value, true);
                }
                continue;
            }

            Debug.LogWarning($"Wish buff effect is not supported yet: {buffId} {effect.target}/{effect.stat}/{effect.mode}.");
        }
    }

    private void BuildSafeRoomTemplate()
    {
        if (safeRoomRoot != null)
        {
            Destroy(safeRoomRoot.gameObject);
        }

        wishStatueUsed = false;
        wishStatueVfx = null;
        blackMarketSpawned = Random.value <= blackMarketSpawnChance;

        GameObject rootObject = new GameObject("SafeHouse_Template_10x10");
        safeRoomRoot = rootObject.transform;

        CreateFloorAndWalls();
        CreateInteractable("Entrance_Airlock", 5, 8, "SafeHouse/safehouse_entrance_door", "Entrance", SafeRoomInteractable.SafeRoomAction.Entrance, Vector2.one);
        CreateInteractable("Exit_DeeperRun", 5, 1, "SafeHouse/safehouse_exit_door", "Exit to next stage", SafeRoomInteractable.SafeRoomAction.Exit, Vector2.one);
        CreateProp("WishStatueFloor", 8, 6, "SafeHouse/safehouse_shrine_floor", Vector2.one, 8);
        GameObject statue = CreateInteractable("WishStatue", 8, 6, "SafeHouse/safehouse_wish_statue", "Wish statue", SafeRoomInteractable.SafeRoomAction.WishStatue, Vector2.one);
        wishStatueVfx = AddInterestVfx(statue, new Color(0.78f, 0.66f, 1f, 0.92f), true);
        AddInterestVfx(CreateInteractable("Shopkeeper", 5, 2, "SafeHouse/safehouse_shopkeeper", "Shopkeeper", SafeRoomInteractable.SafeRoomAction.Shopkeeper, Vector2.one), new Color(1f, 0.72f, 0.26f, 0.85f), false);
        AddInterestVfx(CreateInteractable("TaskBoard", 2, 4, "SafeHouse/safehouse_task_board", "Task board", SafeRoomInteractable.SafeRoomAction.TaskBoard, Vector2.one), new Color(1f, 0.9f, 0.62f, 0.82f), false);

        if (blackMarketSpawned)
        {
            AddInterestVfx(CreateInteractable("BlackMarketMerchant", 8, 8, "SafeHouse/safehouse_blackmarket", "Black market", SafeRoomInteractable.SafeRoomAction.BlackMarket, Vector2.one), new Color(0.72f, 0.28f, 1f, 0.82f), false);
            CreateProp("HiddenCornerLantern", 8, 7, new Color(0.44f, 0.2f, 0.58f, 1f), new Vector2(0.35f, 0.35f), 14);
        }

        CreateProp("CenterTable", 5, 5, "SafeHouse/safehouse_table", Vector2.one, 9);
        CreateProp("RestRug", 5, 6, new Color(0.38f, 0.08f, 0.07f, 1f), new Vector2(2.7f, 1.55f), 8);
        CreateProp("Workbench", 5, 2, "SafeHouse/safehouse_table", new Vector2(1.15f, 0.82f), 8);
        CreateProp("StorageCrates_A", 1, 2, "SafeHouse/safehouse_crates", Vector2.one, 8);
        CreateProp("StorageCrates_B", 1, 8, "SafeHouse/safehouse_crates", new Vector2(1.1f, 1.15f), 8);
        CreateProp("Shelf", 9, 4, new Color(0.25f, 0.17f, 0.11f, 1f), new Vector2(0.7f, 1.8f), 8);
        CreateProp("CandlePillar_A", 3, 4, new Color(0.58f, 0.38f, 0.22f, 1f), new Vector2(0.45f, 0.7f), 12);
        CreateProp("CandlePillar_B", 7, 4, new Color(0.58f, 0.38f, 0.22f, 1f), new Vector2(0.45f, 0.7f), 12);
        CreateProp("BloodStain_A", 8, 2, new Color(0.26f, 0.02f, 0.02f, 0.85f), new Vector2(1.1f, 0.55f), 7);

        CreateWanderer("WanderNpc_A", 4, 6, "SafeHouse/safehouse_wander_npc_a");
        CreateWanderer("WanderNpc_B", 7, 3, "SafeHouse/safehouse_wander_npc_b");
        CreateWanderer("WanderNpc_C", 3, 7, "SafeHouse/safehouse_wander_npc_a");
    }

    private void CreateFloorAndWalls()
    {
        Sprite floor = LoadSafeHouseSprite("safehouse_floor_stone") ?? CreateStoneSprite(new Color(0.16f, 0.16f, 0.15f, 1f), new Color(0.22f, 0.21f, 0.19f, 1f), 100);
        Sprite wall = LoadSafeHouseSprite("safehouse_wall_stone") ?? CreateStoneSprite(new Color(0.09f, 0.09f, 0.09f, 1f), new Color(0.2f, 0.18f, 0.16f, 1f), 100);

        for (int y = 0; y < roomHeight; y++)
        {
            for (int x = 0; x < roomWidth; x++)
            {
                bool isWall = x == 0 || y == 0 || x == roomWidth - 1 || y == roomHeight - 1;
                GameObject tile = new GameObject(isWall ? $"SafeWall_{x}_{y}" : $"SafeFloor_{x}_{y}");
                tile.transform.SetParent(safeRoomRoot);
                tile.transform.position = GridToWorld(x, y);
                SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = isWall ? wall : floor;
                renderer.sortingOrder = isWall ? 3 : 0;
                if (isWall)
                {
                    BoxCollider2D collider = tile.AddComponent<BoxCollider2D>();
                    collider.size = Vector2.one;
                }
            }
        }
    }

    private GameObject CreateInteractable(string name, int x, int y, string spriteResource, string description, SafeRoomInteractable.SafeRoomAction action, Vector2 scale)
    {
        GameObject marker = CreateProp(name, x, y, spriteResource, scale, 18);
        CircleCollider2D trigger = marker.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        bool doorAction = action == SafeRoomInteractable.SafeRoomAction.Entrance || action == SafeRoomInteractable.SafeRoomAction.Exit;
        trigger.radius = Mathf.Max(scale.x, scale.y) * (doorAction ? 0.9f : 0.45f);

        SafeRoomInteractable interactable = marker.AddComponent<SafeRoomInteractable>();
        interactable.Configure(description, action);
        return marker;
    }

    private SafeRoomInterestVfx AddInterestVfx(GameObject targetObject, Color color, bool statue)
    {
        SafeRoomInterestVfx vfx = targetObject.GetComponent<SafeRoomInterestVfx>();
        if (vfx == null)
        {
            vfx = targetObject.AddComponent<SafeRoomInterestVfx>();
        }

        vfx.Configure(color, statue);
        return vfx;
    }

    private GameObject CreateProp(string name, int x, int y, string spriteResource, Vector2 scale, int sortingOrder)
    {
        GameObject prop = new GameObject(name);
        prop.transform.SetParent(safeRoomRoot);
        prop.transform.position = GridToWorld(x, y);
        prop.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = Resources.Load<Sprite>($"Arts/{spriteResource}") ?? FallbackPropSprite;
        renderer.sortingOrder = sortingOrder;
        return prop;
    }

    private GameObject CreateProp(string name, int x, int y, Color color, Vector2 scale, int sortingOrder)
    {
        GameObject prop = new GameObject(name);
        prop.transform.SetParent(safeRoomRoot);
        prop.transform.position = GridToWorld(x, y);
        prop.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSoftPropSprite(color, 96);
        renderer.sortingOrder = sortingOrder;
        return prop;
    }

    private void CreateWanderer(string name, int x, int y, string spriteResource)
    {
        GameObject npc = CreateProp(name, x, y, spriteResource, Vector2.one, 20);
        npc.AddComponent<SafeRoomNpc>().Configure(GridToWorld(5, 5), new Vector2(3.2f, 2.6f));
    }

    private Vector2 GridToWorld(int x, int y)
    {
        float left = safeRoomOrigin.x - roomWidth * 0.5f + 0.5f;
        float top = safeRoomOrigin.y + roomHeight * 0.5f - 0.5f;
        return new Vector2(left + x, top - y);
    }

    private Sprite FallbackPropSprite
    {
        get
        {
            if (fallbackPropSprite == null)
            {
                fallbackPropSprite = CreateSoftPropSprite(new Color(0.55f, 0.55f, 0.55f, 1f), 96);
            }

            return fallbackPropSprite;
        }
    }

    private Sprite LoadSafeHouseSprite(string spriteName)
    {
        return Resources.Load<Sprite>($"Arts/SafeHouse/{spriteName}");
    }

    private Sprite CreateStoneSprite(Color baseColor, Color lineColor, int size)
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
                Color color = baseColor;
                if (x % 46 == 0 || y % 38 == 0)
                {
                    color = lineColor;
                }
                else if ((x + y * 3) % 31 == 0)
                {
                    color = Color.Lerp(baseColor, lineColor, 0.35f);
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateSoftPropSprite(Color color, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float diamond = Mathf.Abs(offset.x) / (size * 0.48f) + Mathf.Abs(offset.y) / (size * 0.48f);
                bool inside = diamond <= 1.1f;
                Color pixel = inside ? color : Color.clear;
                if (inside && diamond > 0.9f)
                {
                    pixel = Color.Lerp(color, Color.black, 0.35f);
                }

                texture.SetPixel(x, y, pixel);
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
