using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
    public readonly struct BuffChoice
    {
        public readonly string id;
        public readonly string title;
        public readonly string description;
        public readonly string rarity;

        public BuffChoice(string id, string title, string description, string rarity = "common")
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.rarity = rarity;
        }
    }

    private enum GameFlowState
    {
        StartMenu,
        WeaponSelect,
        Playing
    }

    [SerializeField] private PlayerInputManager playerInputManager;
    [SerializeField] private PlayerCombatController playerCombatController;
    [SerializeField] private GridRouteMapGenerator mapGenerator;
    [SerializeField] private RunLevelManager runLevelManager;
    [SerializeField] private Camera mainCamera;

    [Header("Safe Room Blessing UI Layout")]
    [SerializeField] private Vector2 blessingBackgroundSize = new Vector2(1080f, 660f);
    [SerializeField] private Vector2 blessingBackgroundPosition = new Vector2(210f, -8f);
    [SerializeField] private Vector2 blessingGoddessSize = new Vector2(500f, 740f);
    [SerializeField] private Vector2 blessingGoddessPosition = new Vector2(255f, 0f);
    [SerializeField] private Vector2 blessingHintPosition = new Vector2(208f, 600f);
    [SerializeField] private Vector2 blessingCardSize = new Vector2(760f, 148f);
    [SerializeField] private Vector2 blessingCardStartPosition = new Vector2(-700f, 150f);
    [SerializeField] private float blessingCardVerticalSpacing = 172f;
    [SerializeField] private Vector2 blessingCardTextOffset = new Vector2(112f, 0f);
    [SerializeField] private Vector2 blessingIconSize = new Vector2(72f, 72f);
    [SerializeField] private Vector2 blessingIconPosition = new Vector2(58f, 0f);
    [SerializeField] private Vector2 blessingConfirmButtonPosition = new Vector2(-500f, 82f);
    [SerializeField] private Vector2 blessingCancelButtonPosition = new Vector2(-300f, 82f);
    [SerializeField] private float blessingSelectionOutlinePadding = 8f;
    [SerializeField] private int blessingSelectionOutlineThickness = 5;

    private Canvas canvas;
    private RectTransform startPanel;
    private RectTransform weaponPanel;
    private RectTransform hudPanel;
    private Text weaponSlotLabel;
    private Image weaponSlotIcon;
    private Image primaryAttackIcon;
    private Image specialAttackIcon;
    private Image weaponSlotOutline;
    private Image[] itemSlotOutlines;
    private Image weaponCooldownFill;
    private Text weaponCooldownText;
    private Image primaryCooldownFill;
    private Text primaryCooldownText;
    private Image specialCooldownFill;
    private Text specialCooldownText;
    private Image healthBarFill;
    private RectTransform healthBarFillRect;
    private Text healthBarText;
    private Image[] manaDiamonds;
    private Image[] manaDiamondFills;
    private CharacterStats observedPlayerStats;
    private PlayerBuffPool observedBuffPool;
    private RectTransform buffHudRoot;
    private Image[] buffIcons;
    private Image[] buffSlotBacks;
    private Text[] buffStackLabels;
    private Sprite defaultBuffIcon;
    private RectTransform safeRoomPanel;
    private Text safeRoomPanelTitle;
    private Text safeRoomPanelBody;
    private readonly List<GameObject> safeRoomPanelButtons = new List<GameObject>();
    private GameFlowState state;
    private bool safeRoomModalOpen;

    private readonly string[] weaponNames = { "Knife", "Sword", "Spear", "Spell" };
    public static GameFlowManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        UnlockCursor();
        ResolveSceneReferences();
        EnsureEventSystem();
        BuildUi();
        BindCameraToPlayer();
    }

    private void Start()
    {
        ShowStartMenu();
    }

    private void Update()
    {
        UpdateWeaponCooldownHud();
        UpdateHotbarSelectionHud();
    }

    public void ShowStartMenu()
    {
        state = GameFlowState.StartMenu;
        UnlockCursor();
        SetPlayerEnabled(false);
        SetPanel(startPanel, true);
        SetPanel(weaponPanel, false);
        SetPanel(hudPanel, false);
    }

    private void ShowWeaponSelect()
    {
        state = GameFlowState.WeaponSelect;
        UnlockCursor();
        SetPlayerEnabled(false);
        SetPanel(startPanel, false);
        SetPanel(weaponPanel, true);
        SetPanel(hudPanel, false);
    }

    private void StartRun(WeaponDefinition weapon)
    {
        state = GameFlowState.Playing;
        UnlockCursor();
        runLevelManager.StartNewRun();
        playerCombatController.SetWeapon(weapon);
        UpdateWeaponSlotIcon(weapon);
        ObservePlayerStats(playerInputManager != null ? playerInputManager.GetComponent<CharacterStats>() : null);
        ObservePlayerBuffPool(playerInputManager != null ? EnsurePlayerBuffPool(playerInputManager.gameObject) : null);

        mapGenerator.GenerateMap();
        playerInputManager.transform.position = mapGenerator.PlayerSpawnPosition;

        SetPlayerEnabled(true);
        SetPanel(startPanel, false);
        SetPanel(weaponPanel, false);
        SetPanel(hudPanel, true);
        UpdatePlayerResourceHud();
        BindCameraToPlayer();
    }

    private void OnDestroy()
    {
        if (observedPlayerStats != null)
        {
            observedPlayerStats.OnHealthChanged -= RefreshHealthHud;
            observedPlayerStats.OnManaChanged -= RefreshManaHud;
            observedPlayerStats = null;
        }

        if (observedBuffPool != null)
        {
            observedBuffPool.OnBuffsChanged -= RefreshBuffHud;
            observedBuffPool = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void ResolveSceneReferences()
    {
        playerInputManager = playerInputManager != null ? playerInputManager : FindObjectOfType<PlayerInputManager>();
        playerCombatController = playerCombatController != null ? playerCombatController : FindObjectOfType<PlayerCombatController>();
        runLevelManager = runLevelManager != null ? runLevelManager : RunLevelManager.Instance;

        if (mapGenerator == null)
        {
            mapGenerator = FindObjectOfType<GridRouteMapGenerator>();
            if (mapGenerator == null)
            {
                GameObject mapRoot = new GameObject("GeneratedGridRouteMap");
                mapGenerator = mapRoot.AddComponent<GridRouteMapGenerator>();
            }
        }

        mainCamera = mainCamera != null ? mainCamera : Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
    }

    private void BindCameraToPlayer()
    {
        if (mainCamera == null || playerInputManager == null)
        {
            return;
        }

        PlayerCameraFollow follow = mainCamera.GetComponent<PlayerCameraFollow>();
        if (follow == null)
        {
            follow = mainCamera.gameObject.AddComponent<PlayerCameraFollow>();
        }

        follow.SetTarget(playerInputManager.transform);

        PlayerVisionMask visionMask = mainCamera.GetComponent<PlayerVisionMask>();
        if (visionMask == null)
        {
            visionMask = mainCamera.gameObject.AddComponent<PlayerVisionMask>();
        }

        visionMask.SetTarget(playerInputManager.transform);
        visionMask.SetMapGenerator(mapGenerator);
    }

    private void SetPlayerEnabled(bool enabled)
    {
        if (playerInputManager != null)
        {
            playerInputManager.canMove = enabled && state == GameFlowState.Playing;
        }
    }

    private void BuildUi()
    {
        canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        startPanel = CreatePanel("StartPanel", new Color(0.07f, 0.09f, 0.12f, 0.94f));
        CreateLabel(startPanel, "TOP DOWN ACT ROGUE", 42, new Vector2(0.5f, 0.62f), new Vector2(720f, 80f));
        CreateButton(startPanel, "START", new Vector2(0.5f, 0.45f), new Vector2(260f, 72f), ShowWeaponSelect);

        weaponPanel = CreatePanel("WeaponSelectPanel", new Color(0.06f, 0.10f, 0.12f, 0.94f));
        CreateLabel(weaponPanel, "Choose One Weapon", 38, new Vector2(0.5f, 0.72f), new Vector2(720f, 70f));
        for (int i = 0; i < weaponNames.Length; i++)
        {
            string weaponName = weaponNames[i];
            float x = 0.29f + i * 0.14f;
            CreateButton(weaponPanel, weaponName, new Vector2(x, 0.48f), new Vector2(210f, 80f), () => StartRun(LoadWeapon(weaponName)));
        }

        hudPanel = CreatePanel("HudPanel", Color.clear);
        weaponSlotLabel = CreateHudBox(hudPanel, "V", new Vector2(0.335f, 0.05f));
        RectTransform weaponSlot = weaponSlotLabel.transform.parent as RectTransform;
        if (weaponSlot != null)
        {
            weaponSlot.sizeDelta = new Vector2(132f, 64f);
        }
        CreateWeaponAbilitySlot(weaponSlot, "LMB", new Vector2(-33f, 0f), out primaryAttackIcon, out primaryCooldownFill, out primaryCooldownText);
        CreateWeaponAbilitySlot(weaponSlot, "RMB", new Vector2(33f, 0f), out specialAttackIcon, out specialCooldownFill, out specialCooldownText);
        weaponSlotOutline = CreateSlotOutline(weaponSlot);

        itemSlotOutlines = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            Text itemLabel = CreateHudBox(hudPanel, (i + 1).ToString(), new Vector2(0.39f + i * 0.055f, 0.05f));
            itemSlotOutlines[i] = CreateSlotOutline(itemLabel.transform.parent as RectTransform);
        }

        string[] skills = { "Q", "E", "R", "T" };
        for (int i = 0; i < skills.Length; i++)
        {
            CreateHudBox(hudPanel, skills[i], new Vector2(0.94f, 0.56f - i * 0.075f));
        }

        CreatePlayerResourceHud(hudPanel);
        CreateBuffHud(hudPanel);
        CreateSafeRoomPanel();
    }

    private RectTransform CreatePanel(string panelName, Color color)
    {
        GameObject panelObject = new GameObject(panelName);
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private Text CreateLabel(RectTransform parent, string text, int size, Vector2 anchor, Vector2 dimensions)
    {
        GameObject labelObject = new GameObject(text);
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        return label;
    }

    private void CreateButton(RectTransform parent, string text, Vector2 anchor, Vector2 dimensions, UnityEngine.Events.UnityAction onClick)
    {
        CreateButtonObject(parent, text, anchor, dimensions, onClick);
    }

    private GameObject CreateButtonObject(RectTransform parent, string text, Vector2 anchor, Vector2 dimensions, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(text + "Button");
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = Vector2.zero;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.17f, 0.68f, 0.64f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        CreateLabel(rect, text, 24, new Vector2(0.5f, 0.5f), dimensions);
        return buttonObject;
    }

    private Text CreateHudBox(RectTransform parent, string text, Vector2 anchor)
    {
        GameObject boxObject = new GameObject(text + "Box");
        boxObject.transform.SetParent(parent, false);
        RectTransform rect = boxObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(64f, 64f);
        rect.anchoredPosition = Vector2.zero;

        Image image = boxObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.12f, 0.16f, 0.9f);
        Text keyLabel = CreateLabel(rect, text, 15, new Vector2(0f, 1f), new Vector2(28f, 22f));
        RectTransform keyRect = keyLabel.rectTransform;
        keyRect.pivot = new Vector2(0f, 1f);
        keyRect.anchoredPosition = new Vector2(5f, -4f);
        keyLabel.alignment = TextAnchor.UpperLeft;
        keyLabel.fontStyle = FontStyle.Bold;
        keyLabel.raycastTarget = false;
        return keyLabel;
    }

    private Image CreateSlotOutline(RectTransform slot)
    {
        if (slot == null)
        {
            return null;
        }

        GameObject outlineObject = new GameObject("SelectionOutline");
        outlineObject.transform.SetParent(slot, false);
        RectTransform outlineRect = outlineObject.AddComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = new Vector2(-3f, -3f);
        outlineRect.offsetMax = new Vector2(3f, 3f);

        Image outline = outlineObject.AddComponent<Image>();
        outline.sprite = CreateSlotOutlineSprite();
        outline.type = Image.Type.Sliced;
        outline.color = new Color(1f, 0.9f, 0.42f, 0.96f);
        outline.raycastTarget = false;
        outline.enabled = false;
        return outline;
    }

    private void CreateWeaponCooldownHud(RectTransform weaponSlot)
    {
        if (weaponSlot == null)
        {
            return;
        }

        GameObject fillObject = new GameObject("WeaponCooldownFill");
        fillObject.transform.SetParent(weaponSlot, false);
        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        weaponCooldownFill = fillObject.AddComponent<Image>();
        weaponCooldownFill.sprite = CreateCooldownSprite();
        weaponCooldownFill.type = Image.Type.Filled;
        weaponCooldownFill.fillMethod = Image.FillMethod.Radial360;
        weaponCooldownFill.fillOrigin = (int)Image.Origin360.Top;
        weaponCooldownFill.fillClockwise = false;
        weaponCooldownFill.color = new Color(0f, 0f, 0f, 0.68f);
        weaponCooldownFill.raycastTarget = false;
        weaponCooldownFill.fillAmount = 0f;

        weaponCooldownText = CreateLabel(weaponSlot, string.Empty, 20, new Vector2(0.5f, 0.5f), weaponSlot.sizeDelta);
        weaponCooldownText.raycastTarget = false;
        weaponCooldownText.fontStyle = FontStyle.Bold;
        weaponCooldownText.color = Color.white;
        weaponCooldownText.transform.SetAsLastSibling();
    }

    private Sprite CreateSlotOutlineSprite()
    {
        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool border = x < 2 || x >= texture.width - 2 || y < 2 || y >= texture.height - 2;
                texture.SetPixel(x, y, border ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4f, 4f, 4f, 4f));
    }

    private void CreateWeaponSlotIcon(RectTransform weaponSlot)
    {
        if (weaponSlot == null)
        {
            return;
        }

        GameObject iconObject = new GameObject("WeaponIcon");
        iconObject.transform.SetParent(weaponSlot, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(46f, 46f);
        iconRect.anchoredPosition = Vector2.zero;

        weaponSlotIcon = iconObject.AddComponent<Image>();
        weaponSlotIcon.preserveAspect = true;
        weaponSlotIcon.raycastTarget = false;
        weaponSlotIcon.enabled = false;
    }

    private void CreateWeaponAbilitySlot(RectTransform weaponSlot, string label, Vector2 anchoredPosition, out Image icon, out Image cooldownFill, out Text cooldownText)
    {
        icon = null;
        cooldownFill = null;
        cooldownText = null;
        if (weaponSlot == null)
        {
            return;
        }

        GameObject slotObject = new GameObject(label + "AbilitySlot");
        slotObject.transform.SetParent(weaponSlot, false);
        RectTransform slotRect = slotObject.AddComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.sizeDelta = new Vector2(58f, 58f);
        slotRect.anchoredPosition = anchoredPosition;

        Image back = slotObject.AddComponent<Image>();
        back.color = new Color(0.035f, 0.055f, 0.075f, 0.92f);
        back.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(slotRect, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(4f, 4f);
        iconRect.offsetMax = new Vector2(-4f, -4f);
        icon = iconObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        GameObject fillObject = new GameObject("CooldownFill");
        fillObject.transform.SetParent(slotRect, false);
        RectTransform fillRect = fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
        cooldownFill = fillObject.AddComponent<Image>();
        cooldownFill.sprite = CreateCooldownSprite();
        cooldownFill.type = Image.Type.Filled;
        cooldownFill.fillMethod = Image.FillMethod.Radial360;
        cooldownFill.fillOrigin = (int)Image.Origin360.Top;
        cooldownFill.fillClockwise = false;
        cooldownFill.color = new Color(0f, 0f, 0f, 0.72f);
        cooldownFill.raycastTarget = false;
        cooldownFill.enabled = false;
        cooldownFill.fillAmount = 0f;

        Text keyText = CreateLabel(slotRect, label, 10, new Vector2(0f, 1f), new Vector2(30f, 16f));
        RectTransform keyRect = keyText.rectTransform;
        keyRect.pivot = new Vector2(0f, 1f);
        keyRect.anchoredPosition = new Vector2(4f, -3f);
        keyText.alignment = TextAnchor.UpperLeft;
        keyText.fontStyle = FontStyle.Bold;
        keyText.color = new Color(0.86f, 0.95f, 1f, 0.95f);
        keyText.raycastTarget = false;

        cooldownText = CreateLabel(slotRect, string.Empty, 17, new Vector2(0.5f, 0.5f), slotRect.sizeDelta);
        cooldownText.fontStyle = FontStyle.Bold;
        cooldownText.raycastTarget = false;
        cooldownText.enabled = false;
        cooldownText.transform.SetAsLastSibling();
    }

    private Sprite CreateCooldownSprite()
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(31.5f, 31.5f);
        float outerRadius = 31f;
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= outerRadius ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void CreatePlayerResourceHud(RectTransform parent)
    {
        GameObject resourceObject = new GameObject("PlayerResources");
        resourceObject.transform.SetParent(parent, false);
        RectTransform rect = resourceObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(42f, 42f);
        rect.sizeDelta = new Vector2(360f, 100f);

        GameObject healthBackObject = new GameObject("HealthBack");
        healthBackObject.transform.SetParent(rect, false);
        RectTransform healthBackRect = healthBackObject.AddComponent<RectTransform>();
        healthBackRect.anchorMin = new Vector2(0f, 0.62f);
        healthBackRect.anchorMax = new Vector2(0f, 0.62f);
        healthBackRect.pivot = new Vector2(0f, 0.5f);
        healthBackRect.anchoredPosition = Vector2.zero;
        healthBackRect.sizeDelta = new Vector2(300f, 24f);
        Image healthBack = healthBackObject.AddComponent<Image>();
        healthBack.color = new Color(0.08f, 0.08f, 0.09f, 0.92f);
        healthBack.raycastTarget = false;

        GameObject healthFillObject = new GameObject("HealthFill");
        healthFillObject.transform.SetParent(healthBackRect, false);
        healthBarFillRect = healthFillObject.AddComponent<RectTransform>();
        healthBarFillRect.anchorMin = Vector2.zero;
        healthBarFillRect.anchorMax = Vector2.one;
        healthBarFillRect.offsetMin = new Vector2(3f, 3f);
        healthBarFillRect.offsetMax = new Vector2(-3f, -3f);
        healthBarFill = healthFillObject.AddComponent<Image>();
        healthBarFill.color = new Color(0.78f, 0.08f, 0.07f, 0.95f);
        healthBarFill.raycastTarget = false;

        healthBarText = CreateLabel(healthBackRect, string.Empty, 16, new Vector2(0.5f, 0.5f), healthBackRect.sizeDelta);
        healthBarText.raycastTarget = false;
        healthBarText.fontStyle = FontStyle.Bold;

        manaDiamonds = new Image[3];
        manaDiamondFills = new Image[3];
        Sprite diamondSprite = CreateDiamondSprite();
        for (int i = 0; i < manaDiamonds.Length; i++)
        {
            GameObject manaObject = new GameObject($"ManaDiamond_{i + 1}");
            manaObject.transform.SetParent(rect, false);
            RectTransform manaRect = manaObject.AddComponent<RectTransform>();
            manaRect.anchorMin = new Vector2(0f, 0.18f);
            manaRect.anchorMax = new Vector2(0f, 0.18f);
            manaRect.pivot = new Vector2(0.5f, 0.5f);
            manaRect.anchoredPosition = new Vector2(18f + i * 36f, 0f);
            manaRect.sizeDelta = new Vector2(28f, 28f);
            manaDiamonds[i] = manaObject.AddComponent<Image>();
            manaDiamonds[i].sprite = diamondSprite;
            manaDiamonds[i].color = new Color(0.04f, 0.14f, 0.22f, 0.72f);
            manaDiamonds[i].raycastTarget = false;

            GameObject fillObject = new GameObject($"ManaDiamondFill_{i + 1}");
            fillObject.transform.SetParent(manaRect, false);
            RectTransform fillRect = fillObject.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            manaDiamondFills[i] = fillObject.AddComponent<Image>();
            manaDiamondFills[i].sprite = diamondSprite;
            manaDiamondFills[i].type = Image.Type.Filled;
            manaDiamondFills[i].fillMethod = Image.FillMethod.Horizontal;
            manaDiamondFills[i].fillOrigin = (int)Image.OriginHorizontal.Left;
            manaDiamondFills[i].color = new Color(0.16f, 0.66f, 1f, 0.96f);
            manaDiamondFills[i].raycastTarget = false;
        }
    }

    private void CreateBuffHud(RectTransform parent)
    {
        GameObject rootObject = new GameObject("BuffHud");
        rootObject.transform.SetParent(parent, false);
        buffHudRoot = rootObject.AddComponent<RectTransform>();
        buffHudRoot.anchorMin = new Vector2(0.5f, 0f);
        buffHudRoot.anchorMax = new Vector2(0.5f, 0f);
        buffHudRoot.pivot = new Vector2(0.5f, 0f);
        buffHudRoot.anchoredPosition = new Vector2(0f, 118f);
        buffHudRoot.sizeDelta = new Vector2(560f, 48f);

        buffIcons = new Image[12];
        buffSlotBacks = new Image[12];
        buffStackLabels = new Text[12];
        for (int i = 0; i < buffIcons.Length; i++)
        {
            GameObject slotObject = new GameObject($"BuffSlot_{i + 1}");
            slotObject.transform.SetParent(buffHudRoot, false);
            RectTransform slotRect = slotObject.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = new Vector2((i - (buffIcons.Length - 1) * 0.5f) * 44f, 0f);
            slotRect.sizeDelta = new Vector2(40f, 40f);

            buffSlotBacks[i] = slotObject.AddComponent<Image>();
            buffSlotBacks[i].color = new Color(0.02f, 0.04f, 0.08f, 0.72f);

            GameObject iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(slotRect, false);
            RectTransform iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(3f, 3f);
            iconRect.offsetMax = new Vector2(-3f, -3f);
            buffIcons[i] = iconObject.AddComponent<Image>();
            buffIcons[i].preserveAspect = true;
            buffIcons[i].raycastTarget = false;

            buffStackLabels[i] = CreateLabel(slotRect, string.Empty, 12, new Vector2(1f, 0f), new Vector2(24f, 18f));
            RectTransform labelRect = buffStackLabels[i].rectTransform;
            labelRect.pivot = new Vector2(1f, 0f);
            labelRect.anchoredPosition = new Vector2(-1f, 1f);

            slotObject.SetActive(false);
        }
    }

    private void CreateSafeRoomPanel()
    {
        safeRoomPanel = CreatePanel("SafeRoomModalPanel", new Color(0.02f, 0.025f, 0.032f, 0.92f));
        safeRoomPanel.anchorMin = new Vector2(0.5f, 0.5f);
        safeRoomPanel.anchorMax = new Vector2(0.5f, 0.5f);
        safeRoomPanel.pivot = new Vector2(0.5f, 0.5f);
        safeRoomPanel.sizeDelta = new Vector2(980f, 620f);
        safeRoomPanel.anchoredPosition = Vector2.zero;

        safeRoomPanelTitle = CreateLabel(safeRoomPanel, string.Empty, 30, new Vector2(0.5f, 0.9f), new Vector2(860f, 54f));
        safeRoomPanelBody = CreateLabel(safeRoomPanel, string.Empty, 20, new Vector2(0.5f, 0.68f), new Vector2(820f, 120f));
        safeRoomPanelBody.alignment = TextAnchor.MiddleCenter;
        Image panelImage = safeRoomPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_panel_bg");
            panelImage.type = Image.Type.Sliced;
        }

        SetPanel(safeRoomPanel, false);
    }

    public void ShowSafeRoomInfoPanel(string title, string body, string closeLabel)
    {
        ShowSafeRoomPanel(title, body, new[] { closeLabel }, index => HideSafeRoomPanel());
    }

    public void ShowBuffChoicePanel(string title, BuffChoice[] choices, Action<BuffChoice> onChosen)
    {
        if (safeRoomPanel == null)
        {
            return;
        }

        ConfigureSafeRoomPanelFrame(true);
        safeRoomPanelTitle.text = title.Replace("Buff", "祝福");
        safeRoomPanelBody.text = string.Empty;
        ClearSafeRoomPanelButtons();

        GameObject blessingBackground = CreatePanelImage("BlessingScreenBackground", new Vector2(0.5f, 0.5f), blessingBackgroundSize, blessingBackgroundPosition, Color.white);
        Image blessingBackgroundImage = blessingBackground.GetComponent<Image>();
        blessingBackgroundImage.sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_blessing_panel_from_mock") ?? Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_blessing_screen_bg");
        blessingBackgroundImage.type = Image.Type.Sliced;
        blessingBackground.transform.SetAsFirstSibling();

        GameObject portraitObject = CreatePanelImage("BlessingGoddessPortrait", new Vector2(0f, 0f), blessingGoddessSize, blessingGoddessPosition, Color.clear);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.pivot = new Vector2(0.5f, 0f);
        portraitRect.anchorMin = new Vector2(0f, 0f);
        portraitRect.anchorMax = new Vector2(0f, 0f);
        portraitRect.anchoredPosition = blessingGoddessPosition;
        Image portrait = portraitObject.GetComponent<Image>();
        portrait.sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_goddess_portrait") ?? CreateGoddessPortraitSprite();
        portrait.color = Color.white;
        portrait.preserveAspect = true;

        Text goddessName = CreateTrackedLabel("BlessingGoddessName", "无名神像", 26, new Vector2(0f, 1f), new Vector2(260f, 42f), new Vector2(208f, -42f));
        goddessName.alignment = TextAnchor.MiddleCenter;
        goddessName.color = new Color(1f, 0.88f, 0.58f, 1f);
        Text hint = CreateTrackedLabel("BlessingHint", "选择一项祝福。确认后神像会沉默，本层出口开启。", 18, new Vector2(0f, 0f), new Vector2(300f, 72f), blessingHintPosition);
        hint.alignment = TextAnchor.MiddleCenter;
        hint.color = new Color(0.82f, 0.86f, 0.88f, 0.9f);

        int selectedIndex = -1;
        Image[] cardImages = new Image[choices.Length];
        Image[] selectionOutlines = new Image[choices.Length];
        Button confirmButton = null;
        for (int i = 0; i < choices.Length; i++)
        {
            int capturedIndex = i;
            GameObject card = CreateButtonObject(safeRoomPanel, string.Empty, new Vector2(1f, 0.5f), blessingCardSize, () =>
            {
                selectedIndex = capturedIndex;
                for (int cardIndex = 0; cardIndex < cardImages.Length; cardIndex++)
                {
                    if (cardImages[cardIndex] != null)
                    {
                        cardImages[cardIndex].sprite = GetBlessingCardSprite(choices[cardIndex].rarity, cardIndex == selectedIndex);
                        cardImages[cardIndex].color = Color.white;
                    }

                    if (selectionOutlines[cardIndex] != null)
                    {
                        selectionOutlines[cardIndex].enabled = cardIndex == selectedIndex;
                    }
                }

                if (confirmButton != null)
                {
                    confirmButton.interactable = true;
                }
            });

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchoredPosition = blessingCardStartPosition - new Vector2(0f, i * blessingCardVerticalSpacing);
            cardImages[i] = card.GetComponent<Image>();
            cardImages[i].sprite = GetBlessingCardSprite(choices[i].rarity, false);
            cardImages[i].type = Image.Type.Sliced;
            cardImages[i].color = Color.white;
            safeRoomPanelButtons.Add(card);
            selectionOutlines[i] = CreateThickSelectionOutline(cardRect);
            selectionOutlines[i].enabled = false;

            Text cardTitle = CreateLabel(cardRect, choices[i].title, 28, new Vector2(0.5f, 0.68f), new Vector2(570f, 42f));
            cardTitle.rectTransform.pivot = new Vector2(0f, 0.5f);
            cardTitle.rectTransform.anchoredPosition = blessingCardTextOffset;
            cardTitle.alignment = TextAnchor.MiddleLeft;
            cardTitle.color = new Color(1f, 0.88f, 0.56f, 1f);
            Text cardDesc = CreateLabel(cardRect, choices[i].description, 20, new Vector2(0.5f, 0.34f), new Vector2(620f, 64f));
            cardDesc.rectTransform.pivot = new Vector2(0f, 0.5f);
            cardDesc.rectTransform.anchoredPosition = blessingCardTextOffset;
            cardDesc.alignment = TextAnchor.MiddleLeft;
            cardDesc.color = new Color(0.86f, 0.92f, 0.96f, 0.94f);
            CreateTrackedCardIcon(cardRect, i);
        }

        GameObject confirmObject = CreateButtonObject(safeRoomPanel, "确认祝福", new Vector2(1f, 0f), new Vector2(180f, 48f), () =>
        {
            if (selectedIndex >= 0 && selectedIndex < choices.Length)
            {
                onChosen?.Invoke(choices[selectedIndex]);
                HideSafeRoomPanel();
            }
        });
        confirmObject.GetComponent<RectTransform>().anchoredPosition = blessingConfirmButtonPosition;
        confirmButton = confirmObject.GetComponent<Button>();
        confirmButton.interactable = false;
        ApplySafeRoomButtonSprite(confirmObject);
        safeRoomPanelButtons.Add(confirmObject);

        GameObject cancelObject = CreateButtonObject(safeRoomPanel, "离开", new Vector2(1f, 0f), new Vector2(140f, 48f), HideSafeRoomPanel);
        cancelObject.GetComponent<RectTransform>().anchoredPosition = blessingCancelButtonPosition;
        ApplySafeRoomButtonSprite(cancelObject);
        safeRoomPanelButtons.Add(cancelObject);

        safeRoomPanel.SetAsLastSibling();
        SetSafeRoomModalOpen(true);
        SetPanel(safeRoomPanel, true);
    }

    public void ShowShopPanel(string shopTag)
    {
        if (safeRoomPanel == null)
        {
            return;
        }

        ConfigureSafeRoomPanelFrame(false);
        ShopGoodsConfigDatabase.ShopGoodConfig[] goods = ShopGoodsConfigDatabase.GetGoodsForShop(shopTag, 6);
        safeRoomPanelTitle.text = shopTag == "blackMarket" ? "黑市商铺" : "安全屋商铺";
        safeRoomPanelBody.text = string.Empty;
        ClearSafeRoomPanelButtons();

        Image shopPanel = CreatePanelImage("ShopPanelMockBackground", new Vector2(0.5f, 0.5f), new Vector2(520f, 620f), new Vector2(-30f, 0f), Color.white).GetComponent<Image>();
        shopPanel.sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_shop_panel_from_mock");
        shopPanel.type = Image.Type.Sliced;
        shopPanel.transform.SetAsFirstSibling();

        CreatePanelImage("ShopMerchantPortrait", new Vector2(0f, 0.5f), new Vector2(260f, 420f), new Vector2(170f, 30f), Color.clear).GetComponent<Image>().sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_merchant_portrait") ?? CreateMerchantPortraitSprite();
        Image speechBubble = CreatePanelImage("MerchantSpeechBubble", new Vector2(0f, 0f), new Vector2(310f, 106f), new Vector2(186f, 78f), Color.white).GetComponent<Image>();
        speechBubble.sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_shop_speech_mock") ?? Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_speech_bubble") ?? CreateRoundRectSprite(new Color(0.88f, 0.80f, 0.62f, 1f), new Color(0.28f, 0.20f, 0.12f, 1f));
        speechBubble.type = Image.Type.Sliced;
        Text speech = CreateTrackedLabel("MerchantSpeech", "看看吧，活着回来的人总该带点东西走。", 18, new Vector2(0f, 0f), new Vector2(286f, 84f), new Vector2(186f, 78f));
        speech.alignment = TextAnchor.MiddleCenter;
        speech.color = new Color(0.08f, 0.07f, 0.05f, 1f);
        speech.transform.SetAsLastSibling();

        Image[] shopSlotImages = new Image[goods.Length];
        Sprite goodsSlotSprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_shop_slot_mock") ?? Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_goods_slot");
        Sprite goodsSlotSelectedSprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_goods_slot_selected");

        for (int i = 0; i < goods.Length; i++)
        {
            ShopGoodsConfigDatabase.ShopGoodConfig good = goods[i];
            int capturedIndex = i;
            Vector2 position = new Vector2(-150f + (i % 3) * 170f, 100f - (i / 3) * 154f);
            GameObject slot = CreateButtonObject(safeRoomPanel, string.Empty, new Vector2(0.5f, 0.5f), new Vector2(150f, 130f), () =>
            {
                ShopGoodsConfigDatabase.ShopGoodConfig selected = goods[capturedIndex];
                if (selected == null)
                {
                    return;
                }

                for (int slotIndex = 0; slotIndex < shopSlotImages.Length; slotIndex++)
                {
                    if (shopSlotImages[slotIndex] != null)
                    {
                        shopSlotImages[slotIndex].sprite = goodsSlotSprite;
                        shopSlotImages[slotIndex].color = GetRarityColor(goods[slotIndex] != null ? goods[slotIndex].rarity : string.Empty);
                    }
                }

                if (shopSlotImages[capturedIndex] != null)
                {
                    shopSlotImages[capturedIndex].sprite = goodsSlotSelectedSprite != null ? goodsSlotSelectedSprite : goodsSlotSprite;
                    shopSlotImages[capturedIndex].color = Color.white;
                }

                speech.text = $"{selected.name}\n{selected.description}\n{ShopGoodsConfigDatabase.FormatEffectSummary(selected)}";
            });
            slot.GetComponent<RectTransform>().anchoredPosition = position;
            shopSlotImages[i] = slot.GetComponent<Image>();
            shopSlotImages[i].sprite = goodsSlotSprite;
            shopSlotImages[i].type = Image.Type.Sliced;
            shopSlotImages[i].color = GetRarityColor(good != null ? good.rarity : string.Empty);
            safeRoomPanelButtons.Add(slot);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            Text nameLabel = CreateLabel(slotRect, good != null ? good.name : "空位", 18, new Vector2(0.5f, 0.74f), new Vector2(142f, 34f));
            nameLabel.color = Color.white;
            Text categoryLabel = CreateLabel(slotRect, good != null ? $"{good.category} / {good.rarity}" : string.Empty, 13, new Vector2(0.5f, 0.5f), new Vector2(136f, 24f));
            categoryLabel.color = new Color(0.82f, 0.88f, 0.92f, 0.9f);
            Text priceLabel = CreateLabel(slotRect, good != null ? $"{good.price} G" : string.Empty, 18, new Vector2(0.5f, 0.24f), new Vector2(136f, 30f));
            priceLabel.color = new Color(1f, 0.86f, 0.35f, 1f);
        }

        GameObject buyButton = CreateButtonObject(safeRoomPanel, "购买占位", new Vector2(1f, 0f), new Vector2(170f, 48f), () => Debug.Log("Shop purchase placeholder."));
        buyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-212f, 58f);
        ApplySafeRoomButtonSprite(buyButton);
        safeRoomPanelButtons.Add(buyButton);
        GameObject closeButton = CreateButtonObject(safeRoomPanel, "关闭", new Vector2(1f, 0f), new Vector2(130f, 48f), HideSafeRoomPanel);
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-54f, 58f);
        ApplySafeRoomButtonSprite(closeButton);
        safeRoomPanelButtons.Add(closeButton);

        safeRoomPanel.SetAsLastSibling();
        SetSafeRoomModalOpen(true);
        SetPanel(safeRoomPanel, true);
    }

    public void ShowTaskBoardPanel()
    {
        if (safeRoomPanel == null)
        {
            return;
        }

        ConfigureSafeRoomPanelFrame(false);
        IReadOnlyList<TaskConfigDatabase.TaskConfig> tasks = TaskConfigDatabase.AllTasks;
        safeRoomPanelTitle.text = "任务栏";
        safeRoomPanelBody.text = string.Empty;
        ClearSafeRoomPanelButtons();

        Image taskPanel = CreatePanelImage("TaskPanelMockBackground", new Vector2(0.5f, 0.5f), new Vector2(420f, 620f), new Vector2(210f, 0f), Color.white).GetComponent<Image>();
        taskPanel.sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_task_panel_from_mock");
        taskPanel.type = Image.Type.Sliced;
        taskPanel.transform.SetAsFirstSibling();

        Text detailTitle = CreateTrackedLabel("TaskDetailTitle", string.Empty, 26, new Vector2(1f, 1f), new Vector2(560f, 44f), new Vector2(-314f, -112f));
        detailTitle.alignment = TextAnchor.MiddleLeft;
        detailTitle.color = new Color(1f, 0.86f, 0.52f, 1f);
        Text detailBody = CreateTrackedLabel("TaskDetailBody", string.Empty, 18, new Vector2(1f, 1f), new Vector2(560f, 290f), new Vector2(-314f, -284f));
        detailBody.alignment = TextAnchor.UpperLeft;
        detailBody.color = new Color(0.88f, 0.93f, 0.95f, 0.96f);
        Image[] taskButtonImages = new Image[tasks.Count];
        Sprite taskTabSprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_task_tab_mock") ?? Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_task_tab");
        Sprite taskTabSelectedSprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_task_tab_selected_mock") ?? Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_task_tab_selected");

        Action<int> showTask = taskIndex =>
        {
            TaskConfigDatabase.TaskConfig task = taskIndex >= 0 && taskIndex < tasks.Count ? tasks[taskIndex] : null;
            if (task == null)
            {
                detailTitle.text = "暂无任务";
                detailBody.text = "任务配置为空。";
                return;
            }

            for (int imageIndex = 0; imageIndex < taskButtonImages.Length; imageIndex++)
            {
                if (taskButtonImages[imageIndex] != null)
                {
                    taskButtonImages[imageIndex].sprite = imageIndex == taskIndex && taskTabSelectedSprite != null ? taskTabSelectedSprite : taskTabSprite;
                    taskButtonImages[imageIndex].color = imageIndex == taskIndex ? new Color(0.22f, 0.18f, 0.08f, 0.98f) : GetRarityColor(tasks[imageIndex].rarity);
                }
            }

            detailTitle.text = task.name;
            detailBody.text =
                $"{task.description}\n\n目标\n{TaskConfigDatabase.FormatObjective(task)}\n\n奖励\n{TaskConfigDatabase.FormatRewards(task)}\n\n失败代价\n{TaskConfigDatabase.FormatPenalties(task)}";
        };

        for (int i = 0; i < tasks.Count; i++)
        {
            TaskConfigDatabase.TaskConfig task = tasks[i];
            int capturedIndex = i;
            GameObject taskButton = CreateButtonObject(safeRoomPanel, task.name, new Vector2(0f, 1f), new Vector2(290f, 64f), () => showTask(capturedIndex));
            taskButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(190f, -118f - i * 76f);
            taskButtonImages[i] = taskButton.GetComponent<Image>();
            taskButtonImages[i].sprite = taskTabSprite;
            taskButtonImages[i].type = Image.Type.Sliced;
            taskButtonImages[i].color = GetRarityColor(task.rarity);
            safeRoomPanelButtons.Add(taskButton);
        }

        if (tasks.Count > 0)
        {
            showTask(0);
        }

        GameObject acceptButton = CreateButtonObject(safeRoomPanel, "接受占位", new Vector2(1f, 0f), new Vector2(170f, 48f), () => Debug.Log("Task accept placeholder."));
        acceptButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-212f, 58f);
        ApplySafeRoomButtonSprite(acceptButton);
        safeRoomPanelButtons.Add(acceptButton);
        GameObject closeButton = CreateButtonObject(safeRoomPanel, "关闭", new Vector2(1f, 0f), new Vector2(130f, 48f), HideSafeRoomPanel);
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-54f, 58f);
        ApplySafeRoomButtonSprite(closeButton);
        safeRoomPanelButtons.Add(closeButton);

        safeRoomPanel.SetAsLastSibling();
        SetSafeRoomModalOpen(true);
        SetPanel(safeRoomPanel, true);
    }

    private void ShowSafeRoomPanel(string title, string body, string[] buttonLabels, Action<int> onButton)
    {
        if (safeRoomPanel == null)
        {
            return;
        }

        ConfigureSafeRoomPanelFrame(false);
        safeRoomPanelTitle.text = title;
        safeRoomPanelBody.text = body;
        ClearSafeRoomPanelButtons();

        float startY = buttonLabels.Length > 1 ? -35f : -115f;
        for (int i = 0; i < buttonLabels.Length; i++)
        {
            int capturedIndex = i;
            GameObject buttonObject = CreateButtonObject(safeRoomPanel, buttonLabels[i], new Vector2(0.5f, 0.5f), new Vector2(500f, 54f), () => onButton?.Invoke(capturedIndex));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, startY - i * 62f);
            safeRoomPanelButtons.Add(buttonObject);
        }

        safeRoomPanel.SetAsLastSibling();
        SetSafeRoomModalOpen(true);
        SetPanel(safeRoomPanel, true);
    }

    private void HideSafeRoomPanel()
    {
        ClearSafeRoomPanelButtons();
        SetPanel(safeRoomPanel, false);
        SetSafeRoomModalOpen(false);
    }

    private void ClearSafeRoomPanelButtons()
    {
        for (int i = 0; i < safeRoomPanelButtons.Count; i++)
        {
            if (safeRoomPanelButtons[i] != null)
            {
                Destroy(safeRoomPanelButtons[i]);
            }
        }

        safeRoomPanelButtons.Clear();
    }

    private void ConfigureSafeRoomPanelFrame(bool fullscreen)
    {
        if (safeRoomPanel == null)
        {
            return;
        }

        if (fullscreen)
        {
            safeRoomPanel.anchorMin = Vector2.zero;
            safeRoomPanel.anchorMax = Vector2.one;
            safeRoomPanel.pivot = new Vector2(0.5f, 0.5f);
            safeRoomPanel.offsetMin = Vector2.zero;
            safeRoomPanel.offsetMax = Vector2.zero;
            safeRoomPanel.anchoredPosition = Vector2.zero;
            safeRoomPanelTitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            safeRoomPanelTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            safeRoomPanelTitle.rectTransform.anchoredPosition = new Vector2(0f, -82f);
            Image panelImage = safeRoomPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.02f, 0.018f, 0.022f, 0.78f);
            }
        }
        else
        {
            safeRoomPanel.anchorMin = new Vector2(0.5f, 0.5f);
            safeRoomPanel.anchorMax = new Vector2(0.5f, 0.5f);
            safeRoomPanel.pivot = new Vector2(0.5f, 0.5f);
            safeRoomPanel.sizeDelta = new Vector2(980f, 620f);
            safeRoomPanel.anchoredPosition = Vector2.zero;
            safeRoomPanelTitle.rectTransform.anchorMin = new Vector2(0.5f, 0.9f);
            safeRoomPanelTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.9f);
            safeRoomPanelTitle.rectTransform.anchoredPosition = Vector2.zero;
            Image panelImage = safeRoomPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.02f, 0.018f, 0.022f, 0.82f);
            }
        }
    }

    private void SetSafeRoomModalOpen(bool open)
    {
        safeRoomModalOpen = open;
        if (playerInputManager != null && state == GameFlowState.Playing)
        {
            playerInputManager.canMove = !safeRoomModalOpen;
        }
        UnlockCursor();
    }

    private void ApplySafeRoomButtonSprite(GameObject buttonObject)
    {
        if (buttonObject == null)
        {
            return;
        }

        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        image.sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_button_normal_mock") ?? Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_button_normal") ?? Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_confirm_button");
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    private Sprite GetBlessingCardSprite(string rarity, bool selected)
    {
        string normalized = string.IsNullOrEmpty(rarity) ? "common" : rarity.ToLowerInvariant();
        if (normalized == "lagendary")
        {
            normalized = "legendary";
        }

        if (normalized != "rare" && normalized != "legendary")
        {
            normalized = "common";
        }

        string suffix = selected ? "_selected" : string.Empty;
        Sprite sprite = Resources.Load<Sprite>($"Arts/UI/SafeHouse/ui_blessing_card_{normalized}{suffix}");
        if (sprite == null && normalized == "common")
        {
            sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_blessing_card_common");
        }
        if (sprite == null && normalized == "rare")
        {
            sprite = Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_blessing_card_rare");
        }
        if (sprite == null && normalized == "legendary")
        {
            sprite = Resources.Load<Sprite>(selected ? "Arts/UI/SafeHouse/ui_blessing_card_legendary_selected" : "Arts/UI/SafeHouse/ui_blessing_card_legendary");
        }
        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>($"Arts/UI/SafeHouse/ui_blessing_{normalized}{suffix}");
        }
        return sprite != null ? sprite : Resources.Load<Sprite>("Arts/UI/SafeHouse/ui_card_bg");
    }

    private Text CreateTrackedLabel(string name, string text, int size, Vector2 anchor, Vector2 dimensions, Vector2 anchoredPosition)
    {
        Text label = CreateLabel(safeRoomPanel, text, size, anchor, dimensions);
        label.name = name;
        label.rectTransform.anchoredPosition = anchoredPosition;
        safeRoomPanelButtons.Add(label.gameObject);
        return label;
    }

    private GameObject CreatePanelImage(string name, Vector2 anchor, Vector2 dimensions, Vector2 anchoredPosition, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(safeRoomPanel, false);
        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = anchoredPosition;
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        safeRoomPanelButtons.Add(imageObject);
        return imageObject;
    }

    private void CreateTrackedCardIcon(RectTransform cardRect, int index)
    {
        GameObject iconObject = new GameObject("BlessingIcon");
        iconObject.transform.SetParent(cardRect, false);
        RectTransform rect = iconObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = blessingIconSize;
        rect.anchoredPosition = blessingIconPosition;
        Image image = iconObject.AddComponent<Image>();
        image.sprite = index % 2 == 0 ? CreateDiamondSprite() : CreateCooldownSprite();
        image.color = new Color(1f, 0.78f, 0.22f, 0.92f);
        image.raycastTarget = false;
    }

    private Image CreateThickSelectionOutline(RectTransform parent)
    {
        GameObject outlineObject = new GameObject("ThickSelectionOutline");
        outlineObject.transform.SetParent(parent, false);
        RectTransform rect = outlineObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-blessingSelectionOutlinePadding, -blessingSelectionOutlinePadding);
        rect.offsetMax = new Vector2(blessingSelectionOutlinePadding, blessingSelectionOutlinePadding);

        Image outline = outlineObject.AddComponent<Image>();
        outline.sprite = CreateThickOutlineSprite();
        outline.type = Image.Type.Sliced;
        outline.color = new Color(1f, 0.86f, 0.26f, 1f);
        outline.raycastTarget = false;
        outlineObject.transform.SetAsLastSibling();
        return outline;
    }

    private Sprite CreateThickOutlineSprite()
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool border = x < blessingSelectionOutlineThickness || y < blessingSelectionOutlineThickness || x >= texture.width - blessingSelectionOutlineThickness || y >= texture.height - blessingSelectionOutlineThickness;
                texture.SetPixel(x, y, border ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect, new Vector4(6f, 6f, 6f, 6f));
    }

    private Color GetRarityColor(string rarity)
    {
        switch (rarity)
        {
            case "rare":
                return new Color(0.10f, 0.17f, 0.34f, 0.96f);
            case "cursed":
                return new Color(0.25f, 0.05f, 0.22f, 0.98f);
            case "legendary":
                return new Color(0.35f, 0.21f, 0.04f, 0.98f);
            default:
                return new Color(0.08f, 0.105f, 0.13f, 0.96f);
        }
    }

    private Sprite CreateGoddessPortraitSprite()
    {
        const int width = 192;
        const int height = 320;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color cloak = new Color(0.26f, 0.22f, 0.34f, 1f);
        Color gold = new Color(0.94f, 0.74f, 0.28f, 1f);
        Color skin = new Color(0.78f, 0.68f, 0.55f, 1f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = clear;
                float nx = (x - width * 0.5f) / width;
                float ny = (y - height * 0.48f) / height;
                if (Mathf.Abs(nx) * 1.35f + Mathf.Abs(ny) < 0.48f && y < height * 0.78f)
                {
                    pixel = cloak;
                }

                Vector2 head = new Vector2(x - width * 0.5f, y - height * 0.77f);
                if (head.magnitude < 24f)
                {
                    pixel = skin;
                }

                float halo = Mathf.Abs(head.magnitude - 38f);
                if (halo < 3.5f && y > height * 0.64f)
                {
                    pixel = gold;
                }

                if (Mathf.Abs(x - width * 0.5f) < 5f && y > height * 0.26f && y < height * 0.66f)
                {
                    pixel = gold;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateMerchantPortraitSprite()
    {
        const int width = 192;
        const int height = 288;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color coat = new Color(0.24f, 0.15f, 0.08f, 1f);
        Color face = new Color(0.68f, 0.52f, 0.38f, 1f);
        Color lamp = new Color(1f, 0.55f, 0.18f, 1f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = clear;
                float nx = (x - width * 0.5f) / width;
                float ny = (y - height * 0.44f) / height;
                if (Mathf.Abs(nx) * 1.2f + Mathf.Abs(ny) < 0.42f && y < height * 0.72f)
                {
                    pixel = coat;
                }

                Vector2 head = new Vector2(x - width * 0.48f, y - height * 0.74f);
                if (head.magnitude < 22f)
                {
                    pixel = face;
                }

                Vector2 lampPos = new Vector2(x - width * 0.72f, y - height * 0.44f);
                if (lampPos.magnitude < 16f)
                {
                    pixel = lamp;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateRoundRectSprite(Color fill, Color border)
    {
        const int width = 64;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 delta = new Vector2(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y));
                float rounded = Mathf.Max((delta.x - 21f) / 8f, (delta.y - 7f) / 8f);
                Color pixel = rounded <= 0f ? fill : clear;
                if (rounded > -0.28f && rounded <= 0f)
                {
                    pixel = border;
                }

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 16f);
    }

    private Sprite CreateDiamondSprite()
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2(15.5f, 15.5f);
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float manhattan = Mathf.Abs(x - center.x) + Mathf.Abs(y - center.y);
                texture.SetPixel(x, y, manhattan <= 15f ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void UpdateWeaponCooldownHud()
    {
        if (playerCombatController == null)
        {
            return;
        }

        UpdateCooldownPair(primaryCooldownFill, primaryCooldownText, playerCombatController.AttackCooldownRemaining, playerCombatController.AttackCooldownDuration);
        UpdateCooldownPair(specialCooldownFill, specialCooldownText, playerCombatController.SpecialCooldownRemaining, playerCombatController.SpecialCooldownDuration);
    }

    private void UpdateCooldownPair(Image fill, Text text, float remaining, float duration)
    {
        if (fill == null || text == null)
        {
            return;
        }

        bool isCoolingDown = state == GameFlowState.Playing && remaining > 0.01f;
        fill.enabled = isCoolingDown;
        text.enabled = isCoolingDown;
        if (!isCoolingDown)
        {
            fill.fillAmount = 0f;
            text.text = string.Empty;
            return;
        }

        fill.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(0.001f, duration));
        text.text = remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0");
        text.transform.SetAsLastSibling();
    }

    private void UpdatePlayerResourceHud()
    {
        if (observedPlayerStats == null && playerInputManager != null)
        {
            ObservePlayerStats(playerInputManager.GetComponent<CharacterStats>());
        }

        if (observedPlayerStats == null || healthBarFillRect == null || healthBarText == null || manaDiamonds == null || manaDiamondFills == null)
        {
            return;
        }

        RefreshHealthHud(observedPlayerStats.CurrentHealth, observedPlayerStats.maxHealth);
        RefreshManaHud(observedPlayerStats.CurrentMana, observedPlayerStats.maxMana);
    }

    private void ObservePlayerStats(CharacterStats nextStats)
    {
        if (observedPlayerStats == nextStats)
        {
            return;
        }

        if (observedPlayerStats != null)
        {
            observedPlayerStats.OnHealthChanged -= RefreshHealthHud;
            observedPlayerStats.OnManaChanged -= RefreshManaHud;
        }

        observedPlayerStats = nextStats;
        if (observedPlayerStats != null)
        {
            observedPlayerStats.OnHealthChanged += RefreshHealthHud;
            observedPlayerStats.OnManaChanged += RefreshManaHud;
            RefreshHealthHud(observedPlayerStats.CurrentHealth, observedPlayerStats.maxHealth);
            RefreshManaHud(observedPlayerStats.CurrentMana, observedPlayerStats.maxMana);
        }
    }

    private PlayerBuffPool EnsurePlayerBuffPool(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return null;
        }

        PlayerBuffPool pool = playerObject.GetComponent<PlayerBuffPool>();
        if (pool == null)
        {
            pool = playerObject.AddComponent<PlayerBuffPool>();
        }

        return pool;
    }

    private void ObservePlayerBuffPool(PlayerBuffPool nextPool)
    {
        if (observedBuffPool == nextPool)
        {
            RefreshBuffHud();
            return;
        }

        if (observedBuffPool != null)
        {
            observedBuffPool.OnBuffsChanged -= RefreshBuffHud;
        }

        observedBuffPool = nextPool;
        if (observedBuffPool != null)
        {
            observedBuffPool.OnBuffsChanged += RefreshBuffHud;
        }

        RefreshBuffHud();
    }

    private void RefreshBuffHud()
    {
        if (buffIcons == null || buffStackLabels == null)
        {
            return;
        }

        IReadOnlyList<PlayerBuffPool.ActiveBuff> buffs = observedBuffPool != null ? observedBuffPool.Buffs : null;
        for (int i = 0; i < buffIcons.Length; i++)
        {
            if (buffIcons[i] == null)
            {
                continue;
            }

            Transform slot = buffIcons[i].transform.parent;
            if (slot == null)
            {
                continue;
            }

            bool show = buffs != null && i < buffs.Count;
            slot.gameObject.SetActive(show);
            if (!show)
            {
                continue;
            }

            PlayerBuffPool.ActiveBuff buff = buffs[i];
            buffIcons[i].sprite = buff.icon != null ? buff.icon : DefaultBuffIcon;
            if (buffSlotBacks != null && i < buffSlotBacks.Length && buffSlotBacks[i] != null)
            {
                buffSlotBacks[i].color = buff.isDebuff ? new Color(0.28f, 0.04f, 0.08f, 0.84f) : new Color(0.03f, 0.12f, 0.18f, 0.78f);
            }
            if (buffStackLabels[i] != null)
            {
                buffStackLabels[i].text = buff.stacks > 1 ? buff.stacks.ToString() : string.Empty;
            }
        }
    }

    private Sprite DefaultBuffIcon
    {
        get
        {
            if (defaultBuffIcon == null)
            {
                defaultBuffIcon = Resources.Load<Sprite>("Arts/UI/Buffs/buff_wish");
            }

            return defaultBuffIcon;
        }
    }

    private void RefreshHealthHud(float currentHealth, float maxHealth)
    {
        if (healthBarFillRect == null || healthBarText == null)
        {
            return;
        }

        float safeMaxHealth = Mathf.Max(1f, maxHealth);
        float healthRatio = Mathf.Clamp01(currentHealth / safeMaxHealth);
        healthBarFillRect.anchorMax = new Vector2(healthRatio, 1f);
        healthBarFillRect.offsetMax = new Vector2(-3f, -3f);
        healthBarText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(safeMaxHealth)}";
        PlayerVisionMask visionMask = mainCamera != null ? mainCamera.GetComponent<PlayerVisionMask>() : null;
        if (visionMask != null)
        {
            visionMask.SetHealthRatio(healthRatio);
        }
    }

    private void RefreshManaHud(float currentMana, float maxMana)
    {
        if (manaDiamonds == null || manaDiamondFills == null)
        {
            return;
        }

        float crystalValue = Mathf.Max(0.001f, maxMana / Mathf.Max(1, manaDiamonds.Length));
        float manaUnits = maxMana > 0f ? Mathf.Clamp(currentMana / crystalValue, 0f, manaDiamonds.Length) : 0f;
        for (int i = 0; i < manaDiamonds.Length; i++)
        {
            float fill = Mathf.Clamp01(manaUnits - i);
            manaDiamonds[i].color = new Color(0.04f, 0.14f, 0.22f, 0.72f);
            manaDiamondFills[i].fillAmount = fill;
            manaDiamondFills[i].enabled = fill > 0.001f;
        }
    }

    private void UpdateHotbarSelectionHud()
    {
        if (playerCombatController == null)
        {
            return;
        }

        bool weaponSelected = playerCombatController.CurrentHotbarSelection == PlayerCombatController.HotbarSelectionMode.Weapon;
        if (weaponSlotOutline != null)
        {
            weaponSlotOutline.enabled = state == GameFlowState.Playing && weaponSelected;
            weaponSlotOutline.transform.SetAsLastSibling();
        }

        if (itemSlotOutlines == null)
        {
            return;
        }

        int selectedItem = playerCombatController.SelectedItemSlot;
        for (int i = 0; i < itemSlotOutlines.Length; i++)
        {
            if (itemSlotOutlines[i] == null)
            {
                continue;
            }

            itemSlotOutlines[i].enabled = state == GameFlowState.Playing && !weaponSelected && selectedItem == i;
            itemSlotOutlines[i].transform.SetAsLastSibling();
        }

        if (weaponCooldownText != null)
        {
            weaponCooldownText.transform.SetAsLastSibling();
        }
        if (primaryCooldownText != null)
        {
            primaryCooldownText.transform.SetAsLastSibling();
        }
        if (specialCooldownText != null)
        {
            specialCooldownText.transform.SetAsLastSibling();
        }
    }

    private WeaponDefinition LoadWeapon(string weaponName)
    {
        WeaponDefinition weapon = Resources.Load<WeaponDefinition>($"Definitions/Weapons/{weaponName}");
        if (weapon != null)
        {
            WeaponConfigDatabase.ApplyTo(weapon);
            return weapon;
        }

        WeaponType type = System.Enum.TryParse(weaponName, out WeaponType parsedType) ? parsedType : WeaponType.Knife;
        return WeaponConfigDatabase.CreateRuntimeDefinition(type);
    }

    private void UpdateWeaponSlotIcon(WeaponDefinition weapon)
    {
        if (weaponSlotLabel == null)
        {
            return;
        }

        bool hasIcon = weapon != null && weapon.weaponSprite != null && weaponSlotIcon != null;
        if (weaponSlotIcon != null)
        {
            weaponSlotIcon.sprite = hasIcon ? weapon.weaponSprite : null;
            weaponSlotIcon.enabled = hasIcon;
            weaponSlotIcon.transform.SetAsLastSibling();
        }

        if (weapon != null)
        {
            SetAbilityIcon(primaryAttackIcon, LoadWeaponSkillIcon(weapon.weaponType, false));
            SetAbilityIcon(specialAttackIcon, LoadWeaponSkillIcon(weapon.weaponType, true));
        }

        weaponSlotLabel.text = "V";
        weaponSlotLabel.transform.SetAsLastSibling();
        if (weaponCooldownFill != null)
        {
            weaponCooldownFill.transform.SetAsLastSibling();
        }
        if (weaponCooldownText != null)
        {
            weaponCooldownText.transform.SetAsLastSibling();
        }
    }

    private void SetAbilityIcon(Image target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
    }

    private Sprite LoadWeaponSkillIcon(WeaponType weaponType, bool special)
    {
        string prefix = special ? "special" : "primary";
        string typeName = weaponType.ToString().ToLowerInvariant();
        return Resources.Load<Sprite>($"Arts/UI/WeaponSkills/{prefix}_{typeName}");
    }

    private void SetPanel(RectTransform panel, bool visible)
    {
        panel.gameObject.SetActive(visible);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void UnlockCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
