using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
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

    private Canvas canvas;
    private RectTransform startPanel;
    private RectTransform weaponPanel;
    private RectTransform hudPanel;
    private Text weaponSlotLabel;
    private Image weaponSlotIcon;
    private Image weaponSlotOutline;
    private Image[] itemSlotOutlines;
    private Image weaponCooldownFill;
    private Text weaponCooldownText;
    private Image healthBarFill;
    private RectTransform healthBarFillRect;
    private Text healthBarText;
    private Image[] manaDiamonds;
    private Image[] manaDiamondFills;
    private GameFlowState state;

    private readonly string[] weaponNames = { "Knife", "Sword", "Spear", "Spell" };

    private void Awake()
    {
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
        UpdatePlayerResourceHud();
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

        mapGenerator.GenerateMap();
        playerInputManager.transform.position = mapGenerator.PlayerSpawnPosition;

        SetPlayerEnabled(true);
        SetPanel(startPanel, false);
        SetPanel(weaponPanel, false);
        SetPanel(hudPanel, true);
        BindCameraToPlayer();
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
        CreateWeaponSlotIcon(weaponSlot);
        CreateWeaponCooldownHud(weaponSlot);
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
        if (weaponCooldownFill == null || weaponCooldownText == null || playerCombatController == null)
        {
            return;
        }

        float remaining = playerCombatController.AttackCooldownRemaining;
        float duration = playerCombatController.AttackCooldownDuration;
        bool isCoolingDown = state == GameFlowState.Playing && remaining > 0.01f;
        weaponCooldownFill.enabled = isCoolingDown;
        weaponCooldownText.enabled = isCoolingDown;
        if (!isCoolingDown)
        {
            weaponCooldownFill.fillAmount = 0f;
            weaponCooldownText.text = string.Empty;
            return;
        }

        weaponCooldownFill.fillAmount = Mathf.Clamp01(remaining / duration);
        weaponCooldownText.text = remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0");
        weaponCooldownText.transform.SetAsLastSibling();
    }

    private void UpdatePlayerResourceHud()
    {
        if (playerInputManager == null || healthBarFillRect == null || healthBarText == null || manaDiamonds == null || manaDiamondFills == null)
        {
            return;
        }

        CharacterStats stats = playerInputManager.GetComponent<CharacterStats>();
        if (stats == null)
        {
            return;
        }

        float maxHealth = Mathf.Max(1f, stats.maxHealth);
        float healthRatio = Mathf.Clamp01(stats.CurrentHealth / maxHealth);
        healthBarFillRect.anchorMax = new Vector2(healthRatio, 1f);
        healthBarFillRect.offsetMax = new Vector2(-3f, -3f);
        healthBarText.text = $"{Mathf.CeilToInt(stats.CurrentHealth)} / {Mathf.CeilToInt(maxHealth)}";

        float manaUnits = stats.usesMana && stats.maxMana > 0f ? Mathf.Clamp(stats.CurrentMana / stats.GetManaCrystalValue(manaDiamonds.Length), 0f, manaDiamonds.Length) : 0f;
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
    }

    private WeaponDefinition LoadWeapon(string weaponName)
    {
        WeaponDefinition weapon = Resources.Load<WeaponDefinition>($"Definitions/Weapons/{weaponName}");
        if (weapon != null)
        {
            return weapon;
        }

        WeaponDefinition fallback = ScriptableObject.CreateInstance<WeaponDefinition>();
        fallback.displayName = weaponName;
        fallback.weaponType = System.Enum.TryParse(weaponName, out WeaponType type) ? type : WeaponType.Knife;
        fallback.targetLayers = Physics2D.DefaultRaycastLayers;
        return fallback;
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
