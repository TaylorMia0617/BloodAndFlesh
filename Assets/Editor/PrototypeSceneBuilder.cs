using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class PrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PlayerPrefabPath = "Assets/Resources/Arts/player.prefab";
    private const string InputActionsPath = "Assets/Scripts/PlayerInput.inputactions";
    private const string WeaponFolder = "Assets/Resources/Definitions/Weapons";
    private const string LevelRootName = "PrototypeLevel";
    private const string GeneratedMapName = "GeneratedGridRouteMap";

    [MenuItem("Tools/TopDownRogue/Setup Prototype Scene")]
    public static void SetupPrototypeScene()
    {
        EnsureFolder("Assets/Resources/Definitions");
        EnsureFolder(WeaponFolder);

        WeaponDefinition knife = EnsureWeapon("Knife", WeaponType.Knife, 10f, 1.5f, 1.2f, 0.45f, 0.6f, 0.15f, 0.2f, "Long blade crescent slash.", "Assets/Resources/Arts/Weapons/weapon_knife.png", new Vector2(0.42f, -0.1f), 0.6f, 162f, 0.34f);
        WeaponDefinition sword = EnsureWeapon("Sword", WeaponType.Sword, 16f, 2.5f, 1.6f, 0.55f, 0.85f, 0.22f, 0.28f, "Fast lightning thrust.", "Assets/Resources/Arts/Weapons/weapon_sword.png", new Vector2(0.5f, -0.14f), 0.68f, 105f, 0.35f);
        WeaponDefinition spear = EnsureWeapon("Spear", WeaponType.Spear, 13f, 3.5f, 2.1f, 0.35f, 0.75f, 0.18f, 0.24f, "Long piercing lunge.", "Assets/Resources/Arts/Weapons/weapon_spear.png", new Vector2(0.62f, -0.08f), 0.75f, 0f, 0.55f);
        WeaponDefinition spell = EnsureWeapon("Spell", WeaponType.Spell, 18f, 1.0f, 2.6f, 0.65f, 1.0f, 0.3f, 0.35f, "Mana orb projectile.", "Assets/Resources/Arts/Weapons/weapon_spell_orb.png", new Vector2(0.5f, 0f), 0.42f, 0f, 0.65f);
        ConfigureWeaponPresentation(knife, new Vector2(0.18f, 0f), false, true);
        ConfigureWeaponPresentation(sword, Vector2.zero, false, true);
        ConfigureWeaponPresentation(spear, Vector2.zero, false, true);
        ConfigureWeaponPresentation(spell, Vector2.zero, false, true);

        ConfigurePlayerPrefab(knife);

        EditorSceneManager.OpenScene(ScenePath);
        ClearExistingLevelRoot();
        RemoveMissingPrefabInstances();

        GameObject player = FindOrCreateScenePlayer();
        ConfigurePlayer(player, knife);
        player.transform.position = Vector3.zero;

        GridRouteMapGenerator mapGenerator = EnsureMapGenerator();
        GameFlowManager flowManager = EnsureGameFlowManager(player, mapGenerator);
        ConfigureCamera(player, mapGenerator);
        EnsureEventSystem();
        EnsureCanvas();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Prototype scene setup complete. Open SampleScene and press Play.");
    }

    private static void ConfigurePlayerPrefab(WeaponDefinition startingWeapon)
    {
        if (!File.Exists(PlayerPrefabPath))
        {
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        ConfigurePlayer(prefabRoot, startingWeapon);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static GameObject FindOrCreateScenePlayer()
    {
        PlayerInputManager existing = Object.FindObjectOfType<PlayerInputManager>();
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "player";
            return instance;
        }

        return new GameObject("player");
    }

    private static void ConfigurePlayer(GameObject player, WeaponDefinition startingWeapon)
    {
        RemoveDuplicateComponents<PlayerInputManager>(player);
        RemoveDuplicateComponents<PlayerInput>(player);

        SpriteRenderer renderer = EnsureComponent<SpriteRenderer>(player);
        renderer.sprite = LoadSprite("Assets/Resources/Arts/Characters/player_core.png");
        renderer.sortingOrder = 20;

        Rigidbody2D body = EnsureComponent<Rigidbody2D>(player);
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D collider = EnsureComponent<BoxCollider2D>(player);
        collider.size = new Vector2(0.8f, 0.8f);

        PlayerInput input = EnsureComponent<PlayerInput>(player);
        input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        input.defaultActionMap = "Player";
        input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

        EnsureComponent<PlayerInputManager>(player);
        PlayerInputManager movement = player.GetComponent<PlayerInputManager>();
        movement.moveSpeed = 6f;
        movement.mark = 0.5f;
        CharacterStats stats = EnsureComponent<CharacterStats>(player);
        stats.maxHealth = 70f;
        stats.armor = 1f;
        stats.attack = 0f;
        stats.damageMultiplier = 1f;
        stats.critChance = 0f;
        stats.critDamage = 0.5f;
        stats.usesMana = true;
        stats.maxMana = 60f;
        stats.moveSpeed = 6f;
        stats.moveDelay = 0.5f;
        EnsureComponent<HitVolumeFeedback>(player);
        EquippedWeaponView weaponView = EnsureComponent<EquippedWeaponView>(player);
        SerializedObject serializedWeaponView = new SerializedObject(weaponView);
        serializedWeaponView.FindProperty("sortingOrder").intValue = 34;
        serializedWeaponView.FindProperty("showWeaponSpriteInWorld").boolValue = false;
        serializedWeaponView.ApplyModifiedPropertiesWithoutUndo();
        PlayerCombatController combat = EnsureComponent<PlayerCombatController>(player);
        AttackWaveEffect attackWave = EnsureAttackWave(player);

        Transform attackOrigin = player.transform.Find("AttackOrigin");
        if (attackOrigin == null)
        {
            attackOrigin = new GameObject("AttackOrigin").transform;
            attackOrigin.SetParent(player.transform);
        }
        attackOrigin.localPosition = Vector3.zero;

        SerializedObject serializedCombat = new SerializedObject(combat);
        serializedCombat.FindProperty("startingWeapon").objectReferenceValue = startingWeapon;
        serializedCombat.FindProperty("attackOrigin").objectReferenceValue = attackOrigin;
        serializedCombat.FindProperty("attackWaveEffect").objectReferenceValue = attackWave;
        serializedCombat.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AttackWaveEffect EnsureAttackWave(GameObject player)
    {
        Transform existing = player.transform.Find("AttackWave");
        GameObject waveObject = existing != null ? existing.gameObject : new GameObject("AttackWave");
        waveObject.transform.SetParent(player.transform);
        waveObject.transform.localPosition = Vector3.zero;
        waveObject.transform.localRotation = Quaternion.identity;
        waveObject.transform.localScale = Vector3.one;
        return EnsureComponent<AttackWaveEffect>(waveObject);
    }

    private static GridRouteMapGenerator EnsureMapGenerator()
    {
        GridRouteMapGenerator mapGenerator = Object.FindObjectOfType<GridRouteMapGenerator>();
        if (mapGenerator != null)
        {
            return mapGenerator;
        }

        GameObject mapRoot = new GameObject(GeneratedMapName);
        return mapRoot.AddComponent<GridRouteMapGenerator>();
    }

    private static GameFlowManager EnsureGameFlowManager(GameObject player, GridRouteMapGenerator mapGenerator)
    {
        GameFlowManager flowManager = Object.FindObjectOfType<GameFlowManager>();
        if (flowManager == null)
        {
            GameObject flowObject = new GameObject("GameFlowManager");
            flowManager = flowObject.AddComponent<GameFlowManager>();
        }

        SerializedObject serializedFlow = new SerializedObject(flowManager);
        serializedFlow.FindProperty("playerInputManager").objectReferenceValue = player.GetComponent<PlayerInputManager>();
        serializedFlow.FindProperty("playerCombatController").objectReferenceValue = player.GetComponent<PlayerCombatController>();
        serializedFlow.FindProperty("mapGenerator").objectReferenceValue = mapGenerator;
        serializedFlow.FindProperty("runLevelManager").objectReferenceValue = EnsureRunLevelManager();
        serializedFlow.FindProperty("mainCamera").objectReferenceValue = Camera.main;
        serializedFlow.ApplyModifiedPropertiesWithoutUndo();

        return flowManager;
    }

    private static RunLevelManager EnsureRunLevelManager()
    {
        RunLevelManager manager = Object.FindObjectOfType<RunLevelManager>();
        if (manager != null)
        {
            return manager;
        }

        GameObject managerObject = new GameObject("RunLevelManager");
        return managerObject.AddComponent<RunLevelManager>();
    }

    private static void ConfigureCamera(GameObject player, GridRouteMapGenerator mapGenerator)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 8f;
        PlayerCameraFollow follow = EnsureComponent<PlayerCameraFollow>(camera.gameObject);
        follow.SetTarget(player.transform);

        PlayerVisionMask visionMask = EnsureComponent<PlayerVisionMask>(camera.gameObject);
        visionMask.SetTarget(player.transform);
        visionMask.SetMapGenerator(mapGenerator);
    }

    private static void EnsureCanvas()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Canvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private static void BuildFloor(Transform parent)
    {
        Sprite ground = LoadSprite("Assets/Resources/Arts/Tiles/tile_ground_grid.png");
        Sprite danger = LoadSprite("Assets/Resources/Arts/Tiles/tile_danger_floor.png");

        for (int y = -3; y <= 3; y++)
        {
            for (int x = -5; x <= 5; x++)
            {
                Sprite sprite = Mathf.Abs(x) >= 4 || Mathf.Abs(y) >= 3 ? danger : ground;
                GameObject tile = CreateSpriteObject($"Floor_{x}_{y}", sprite, parent, new Vector3(x * 1.28f, y * 1.28f, 0.2f), 0);
                tile.transform.localScale = Vector3.one;
            }
        }
    }

    private static void BuildWalls(Transform parent)
    {
        Sprite wall = LoadSprite("Assets/Resources/Arts/Tiles/tile_wall_block.png");
        for (int x = -6; x <= 6; x++)
        {
            CreateWall($"Wall_Top_{x}", wall, parent, new Vector3(x * 1.28f, 4.48f, 0f));
            CreateWall($"Wall_Bottom_{x}", wall, parent, new Vector3(x * 1.28f, -4.48f, 0f));
        }

        for (int y = -3; y <= 3; y++)
        {
            CreateWall($"Wall_Left_{y}", wall, parent, new Vector3(-7.68f, y * 1.28f, 0f));
            CreateWall($"Wall_Right_{y}", wall, parent, new Vector3(7.68f, y * 1.28f, 0f));
        }
    }

    private static void BuildEnemies(Transform parent)
    {
        CreateEnemy("Enemy_Triangle_Chaser", "Assets/Resources/Arts/Enemies/enemy_triangle_chaser.png", parent, new Vector3(-2.4f, 1.4f, -0.2f), 24f);
        CreateEnemy("Enemy_Square_Guard", "Assets/Resources/Arts/Enemies/enemy_square_guard.png", parent, new Vector3(2.2f, 1.2f, -0.2f), 45f);
        CreateEnemy("Enemy_Diamond_Rusher", "Assets/Resources/Arts/Enemies/enemy_diamond_rusher.png", parent, new Vector3(0f, 2.7f, -0.2f), 32f);
        CreateEnemy("Enemy_Circle_Spitter", "Assets/Resources/Arts/Enemies/enemy_circle_spitter.png", parent, new Vector3(3.5f, -1.8f, -0.2f), 28f);
    }

    private static void BuildPickupsAndVfx(Transform parent)
    {
        CreateSpriteObject("Pickup_EnergyShard", LoadSprite("Assets/Resources/Arts/Pickups/pickup_energy_shard.png"), parent, new Vector3(-1.5f, -2.4f, -0.3f), 8);
        CreateSpriteObject("Pickup_CoinHex", LoadSprite("Assets/Resources/Arts/Pickups/pickup_coin_hex.png"), parent, new Vector3(1.5f, -2.4f, -0.3f), 8);
        CreateSpriteObject("AttackArc_Reference", LoadSprite("Assets/Resources/Arts/VFX/attack_arc_wedge.png"), parent, new Vector3(0f, -3.2f, -0.3f), 6);
        CreateSpriteObject("DangerTelegraph_Reference", LoadSprite("Assets/Resources/Arts/VFX/danger_telegraph.png"), parent, new Vector3(3.8f, 2.6f, 0f), 3);
    }

    private static void CreateEnemy(string name, string spritePath, Transform parent, Vector3 position, float health)
    {
        GameObject enemy = CreateSpriteObject(name, LoadSprite(spritePath), parent, position, 12);
        CircleCollider2D collider = EnsureComponent<CircleCollider2D>(enemy);
        collider.radius = 0.42f;

        Rigidbody2D body = EnsureComponent<Rigidbody2D>(enemy);
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        PrototypeDamageable damageable = EnsureComponent<PrototypeDamageable>(enemy);
        SerializedObject serializedDamageable = new SerializedObject(damageable);
        serializedDamageable.FindProperty("maxHealth").floatValue = health;
        serializedDamageable.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateWall(string name, Sprite sprite, Transform parent, Vector3 position)
    {
        GameObject wall = CreateSpriteObject(name, sprite, parent, position, 5);
        BoxCollider2D collider = EnsureComponent<BoxCollider2D>(wall);
        collider.size = new Vector2(1.2f, 1.2f);
    }

    private static GameObject CreateSpriteObject(string name, Sprite sprite, Transform parent, Vector3 position, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        SpriteRenderer renderer = EnsureComponent<SpriteRenderer>(obj);
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return obj;
    }

    private static WeaponDefinition EnsureWeapon(string name, WeaponType type, float damage, float armorPiercing, float range, float radius, float cooldown, float windup, float recovery, string description, string spritePath, Vector2 equippedOffset, float equippedScale, float swingAngle, float thrustDistance)
    {
        string path = $"{WeaponFolder}/{name}.asset";
        WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(weapon, path);
        }

        weapon.weaponType = type;
        weapon.displayName = name;
        weapon.weaponSprite = LoadSprite(spritePath);
        weapon.damage = damage;
        weapon.armorPiercing = armorPiercing;
        weapon.attackRange = range;
        weapon.attackRadius = radius;
        weapon.cooldown = cooldown;
        weapon.windup = windup;
        weapon.recovery = recovery;
        weapon.description = description;
        weapon.equippedOffset = equippedOffset;
        weapon.equippedScale = equippedScale;
        weapon.swingAngle = swingAngle;
        weapon.thrustDistance = thrustDistance;
        weapon.useSweepArc = false;
        weapon.sweepLeftToRight = true;
        weapon.effectOffset = Vector2.zero;
        weapon.targetLayers = Physics2D.DefaultRaycastLayers;
        EditorUtility.SetDirty(weapon);
        return weapon;
    }

    private static void ConfigureWeaponPresentation(WeaponDefinition weapon, Vector2 effectOffset, bool useSweepArc, bool sweepLeftToRight)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.effectOffset = effectOffset;
        weapon.useSweepArc = useSweepArc;
        weapon.sweepLeftToRight = sweepLeftToRight;
        EditorUtility.SetDirty(weapon);
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError($"Missing sprite at {path}");
        }
        return sprite;
    }

    private static T EnsureComponent<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }

    private static void RemoveDuplicateComponents<T>(GameObject obj) where T : Component
    {
        T[] components = obj.GetComponents<T>();
        for (int i = components.Length - 1; i >= 1; i--)
        {
            Object.DestroyImmediate(components[i], true);
        }
    }

    private static void ClearExistingLevelRoot()
    {
        GameObject existing = GameObject.Find(LevelRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject generated = GameObject.Find(GeneratedMapName);
        if (generated != null)
        {
            Object.DestroyImmediate(generated);
        }

        string[] obsoleteNames =
        {
            "Default Scene",
            "SafeRoom_Left",
            "SafeRoom_Right",
            "SafeDoor_Left",
            "SafeDoor_Right"
        };

        foreach (string obsoleteName in obsoleteNames)
        {
            GameObject obsolete = GameObject.Find(obsoleteName);
            if (obsolete != null)
            {
                Object.DestroyImmediate(obsolete);
            }
        }
    }

    private static void RemoveMissingPrefabInstances()
    {
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = roots.Length - 1; i >= 0; i--)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            if (PrefabUtility.GetPrefabAssetType(root) == PrefabAssetType.MissingAsset)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
        string name = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}
