using System;
using FreedomJam.Game.CameraRig;
using FreedomJam.Game.Presentation;
using FreedomJam.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FreedomJam.Game.PlayerSandbox.Editor
{
    /// <summary>
    /// Menus under Tools > Freedom Jam:
    /// - Build Player Sandbox: player + still dummies -> PlayerSandbox.unity
    /// - Build Integration Sandbox: player + the real enemy spawner and XP orbs -> IntegrationSandbox.unity
    /// Both create the player assets and prefab if they don't exist yet (never overwrite them),
    /// then rebuild their scene from scratch.
    /// </summary>
    public static class PlayerSandboxBuilder
    {
        const string PlayerDataFolder = "Assets/Game/Data/Player";
        const string SandboxDataFolder = "Assets/Game/PlayerSandbox/Data";
        const string PrefabFolder = "Assets/Game/Prefabs";
        const string FeedbackFolder = "Assets/Game/Presentation/Feedback";
        const string LevelUpFolder = "Assets/Game/Presentation/LevelUp";
        const string FeedbackPrefabFolder = FeedbackFolder + "/Prefabs";
        const string FeedbackConfigPath = FeedbackFolder + "/CharacterFeedbackConfig.asset";
        const string DamageNumberPrefabPath = FeedbackPrefabFolder + "/DamageNumber.prefab";
        const string PlayerHealthBarPrefabPath = FeedbackPrefabFolder + "/PlayerHealthBar.prefab";
        const string EnemyHealthBarPrefabPath = FeedbackPrefabFolder + "/EnemyHealthBar.prefab";
        const string LevelUpChoicePanelPrefabPath = LevelUpFolder + "/LevelUpChoicePanel.prefab";
        const string SceneFolder = "Assets/Game/PlayerSandbox";
        const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        const string ScenePath = SceneFolder + "/PlayerSandbox.unity";
        const string IntegrationScenePath = SceneFolder + "/IntegrationSandbox.unity";
        const string SpawnConfigPath = "Assets/Game/Data/Spawn/DefaultSpawn.asset";   // Track B asset, read only
        const string XpOrbPrefabPath = "Assets/Game/Prefabs/XpOrb.prefab";            // Track B asset, read only
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const int DummyCount = 6;
        const float DummyRingRadius = 7f;

        [MenuItem("Tools/Freedom Jam/Build Player Sandbox")]
        public static void Build() => BuildScene(ScenePath, withEnemies: false);

        [MenuItem("Tools/Freedom Jam/Build Integration Sandbox")]
        public static void BuildIntegration() => BuildScene(IntegrationScenePath, withEnemies: true);

        static void BuildScene(string scenePath, bool withEnemies)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play mode before building the player sandbox.");
                return;
            }

            var playerLayer = LayerMask.NameToLayer("Player");
            var enemyLayer = LayerMask.NameToLayer("Enemy");
            if (playerLayer < 0 || enemyLayer < 0)
            {
                Debug.LogError("Layers 'Player' and 'Enemy' are missing. They come from Step 0 (TagManager).");
                return;
            }

            var move = FindMoveAction();
            if (move == null)
            {
                Debug.LogError($"Could not find the Player/Move action reference in {InputActionsPath}.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // New scene FIRST. NewScene unloads unused assets, which turns any asset we loaded
            // before it into a dead reference that serializes as null.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            move = FindMoveAction();

            foreach (var folder in new[] { PlayerDataFolder, SandboxDataFolder, PrefabFolder, SceneFolder, FeedbackPrefabFolder, LevelUpFolder }) EnsureFolder(folder);

            var playerConfig = LoadOrCreate<PlayerConfig>(PlayerDataFolder + "/PlayerConfig.asset", null);
            var levelConfig = LoadOrCreate<LevelConfig>(PlayerDataFolder + "/LevelConfig.asset", null);
            var dummyCreature = LoadOrCreate<CreatureData>(SandboxDataFolder + "/DummyCreature.asset", c =>
            {
                c.displayName = "Dummy";
                c.maxHP = 1f;   // one bite: a forward-only player passes a still dummy in about one mouth tick
                c.xpValue = 3;
                c.eatMultipliers.Add(new StatBonus { stat = StatType.MoveSpeed, bonus = 0.1f });
                c.eatMultipliers.Add(new StatBonus { stat = StatType.BaseDamage, bonus = 0.2f });
            });
            var playerMaterial = LoadOrCreateMaterial(SandboxDataFolder + "/SandboxPlayer.mat", new Color(0.2f, 0.55f, 1f));
            var dummyMaterial = LoadOrCreateMaterial(SandboxDataFolder + "/SandboxDummy.mat", new Color(1f, 0.35f, 0.3f));
            var groundMaterial = LoadOrCreateMaterial(SandboxDataFolder + "/SandboxGround.mat", new Color(0.35f, 0.38f, 0.42f));
            var feedbackConfig = LoadOrCreate<CharacterFeedbackConfig>(FeedbackConfigPath, null);
            var damageNumberPrefab = CreateDamageNumberPrefab();
            var playerHealthBarPrefab = CreatePlayerHealthBarPrefab();
            var enemyHealthBarPrefab = CreateEnemyHealthBarPrefab();
            var levelUpPanelPrefab = CreateLevelUpChoicePanelPrefab();

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
                playerPrefab = CreatePlayerPrefab(playerLayer, enemyLayer, playerConfig, levelConfig, move, playerMaterial);
            else
                FillMissingPlayerReferences(playerConfig, levelConfig, move);
            ConfigureFeedbackPrefab(PlayerPrefabPath, player: true, feedbackConfig, damageNumberPrefab, playerHealthBarPrefab, enemyHealthBarPrefab);
            ConfigureLevelUpFlowPrefab(PlayerPrefabPath, levelUpPanelPrefab);

            CreateGround(groundMaterial);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetupCamera(player.transform);

            var overlay = new GameObject("Debug Overlay").AddComponent<PlayerDebugOverlay>();
            SetReference(overlay, "player", player.GetComponent<PlayerCharacter>());

            if (withEnemies)
            {
                var spawner = CreateEnemySystems();
                if (spawner == null) return;
                SetReference(overlay, "spawner", spawner);
            }
            else
            {
                for (var i = 0; i < DummyCount; i++) CreateDummy(i, enemyLayer, dummyCreature, dummyMaterial,
                    feedbackConfig, damageNumberPrefab, playerHealthBarPrefab, enemyHealthBarPrefab);
            }

            if (withEnemies)
            {
                ConfigureFeedbackPrefab(PrefabFolder + "/Enemy_Ember.prefab", player: false, feedbackConfig, damageNumberPrefab,
                    playerHealthBarPrefab, enemyHealthBarPrefab);
                ConfigureFeedbackPrefab(PrefabFolder + "/Enemy_Zipper.prefab", player: false, feedbackConfig, damageNumberPrefab,
                    playerHealthBarPrefab, enemyHealthBarPrefab);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Sandbox built: {scenePath}. Press Play in this scene to test (no MainMenu needed).");
        }

        /// <summary>The enemy track's real spawner and XP orbs, wired the same way as their EnemySandbox.</summary>
        static Enemy.EnemySpawner CreateEnemySystems()
        {
            var spawnConfig = AssetDatabase.LoadAssetAtPath<SpawnConfig>(SpawnConfigPath);
            var orbPrefab = AssetDatabase.LoadAssetAtPath<Pickups.XpOrb>(XpOrbPrefabPath);
            if (spawnConfig == null || orbPrefab == null)
            {
                Debug.LogError($"Integration sandbox needs {SpawnConfigPath} and {XpOrbPrefabPath} from the enemy track.");
                return null;
            }

            var systems = new GameObject("Enemy Systems");
            var spawner = systems.AddComponent<Enemy.EnemySpawner>();
            SetReference(spawner, "config", spawnConfig);
            SetReference(systems.AddComponent<Pickups.XpOrbSpawner>(), "orbPrefab", orbPrefab);
            return spawner;
        }

        static GameObject CreatePlayerPrefab(int playerLayer, int enemyLayer, PlayerConfig config, LevelConfig levelConfig,
            InputActionReference move, Material material)
        {
            var root = new GameObject("Player") { layer = playerLayer };

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;   // movement sets the transform

            var hurtbox = root.AddComponent<SphereCollider>();
            hurtbox.isTrigger = true;
            hurtbox.center = new Vector3(0f, 0.5f, 0f);
            hurtbox.radius = 0.5f;

            var steering = root.AddComponent<InputActionSteering>();
            var mouth = root.AddComponent<PlayerMouth>();
            var character = root.AddComponent<PlayerCharacter>();

            AddVisual(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 1f, 0f), Vector3.one, material, playerLayer);
            AddVisual(root, PrimitiveType.Cube, "Nose", new Vector3(0f, 1.2f, 0.55f), new Vector3(0.3f, 0.3f, 0.4f), material, playerLayer);

            SetReference(character, "config", config);
            SetReference(character, "levelConfig", levelConfig);
            SetReference(character, "mouth", mouth);
            SetReference(mouth, "player", character);
            SetLayerMask(mouth, "targetMask", 1 << enemyLayer);
            SetReference(steering, "move", move);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Fills only empty fields on an existing Player prefab, so manual edits are kept.</summary>
        static void FillMissingPlayerReferences(PlayerConfig config, LevelConfig levelConfig, InputActionReference move)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var changed = false;
                var character = root.GetComponent<PlayerCharacter>();
                if (character != null)
                {
                    changed |= SetReferenceIfEmpty(character, "config", config);
                    changed |= SetReferenceIfEmpty(character, "levelConfig", levelConfig);
                }
                var steering = root.GetComponent<InputActionSteering>();
                if (steering != null) changed |= SetReferenceIfEmpty(steering, "move", move);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    Debug.Log($"Filled empty references on {PlayerPrefabPath}.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool SetReferenceIfEmpty(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue != null) return false;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        static void AddVisual(GameObject parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material, int layer)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.layer = layer;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void CreateGround(Material material)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(20f, 1f, 20f);   // 200 x 200 units
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void SetupCamera(Transform target)
        {
            var camera = Camera.main;
            if (camera == null) camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.fieldOfView = 35f;

            var follow = camera.gameObject.AddComponent<FixedAngleFollowCamera>();
            SetReference(follow, "target", target);

            // Start where the follow script would put it, so there's no swoop on Play.
            var rotation = Quaternion.Euler(50f, 45f, 0f);
            camera.transform.SetPositionAndRotation(target.position - rotation * Vector3.forward * 25f, rotation);
        }

        static void CreateDummy(int index, int enemyLayer, CreatureData creature, Material material,
            CharacterFeedbackConfig feedbackConfig, DamageNumber damageNumberPrefab,
            PlayerHealthBarView playerHealthBarPrefab, EnemyHealthBarView enemyHealthBarPrefab)
        {
            var angle = index * Mathf.PI * 2f / DummyCount;
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dummy.name = $"Dummy {index + 1}";
            dummy.layer = enemyLayer;
            dummy.transform.position = new Vector3(Mathf.Sin(angle) * DummyRingRadius, 0.5f, Mathf.Cos(angle) * DummyRingRadius);
            dummy.GetComponent<SphereCollider>().isTrigger = true;
            dummy.GetComponent<Renderer>().sharedMaterial = material;

            var body = dummy.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            SetReference(dummy.AddComponent<DummyTarget>(), "creature", creature);
            ConfigureFeedback(dummy, player: false, feedbackConfig, damageNumberPrefab, playerHealthBarPrefab, enemyHealthBarPrefab);
        }

        static void ConfigureFeedbackPrefab(string path, bool player, CharacterFeedbackConfig config, DamageNumber damageNumberPrefab,
            PlayerHealthBarView playerHealthBarPrefab, EnemyHealthBarView enemyHealthBarPrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;
            try
            {
                ConfigureFeedback(root, player, config, damageNumberPrefab, playerHealthBarPrefab, enemyHealthBarPrefab);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ConfigureFeedback(GameObject root, bool player, CharacterFeedbackConfig config, DamageNumber damageNumberPrefab,
            PlayerHealthBarView playerHealthBarPrefab, EnemyHealthBarView enemyHealthBarPrefab)
        {
            if (root.GetComponent<CharacterHealthSource>() == null) root.AddComponent<CharacterHealthSource>();
            var visuals = root.GetComponent<CharacterVisualFeedback>() ?? root.AddComponent<CharacterVisualFeedback>();
            var numbers = root.GetComponent<DamageNumberSpawner>() ?? root.AddComponent<DamageNumberSpawner>();
            SetReference(visuals, "config", config);
            SetBool(visuals, "useWhiteDamageFlash", !player);
            SetReference(numbers, "config", config);
            SetReference(numbers, "numberPrefab", damageNumberPrefab);
            if (player)
            {
                if (root.GetComponent<PlayerAbilityController>() == null) root.AddComponent<PlayerAbilityController>();
                var bar = root.GetComponent<PlayerHealthBar>() ?? root.AddComponent<PlayerHealthBar>();
                SetReference(bar, "config", config);
                SetReference(bar, "viewPrefab", playerHealthBarPrefab);
            }
            else
            {
                var bar = root.GetComponent<EnemyHealthBar>() ?? root.AddComponent<EnemyHealthBar>();
                SetReference(bar, "config", config);
                SetReference(bar, "viewPrefab", enemyHealthBarPrefab);
            }
        }

        static PlayerHealthBarView CreatePlayerHealthBarPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerHealthBarView>(PlayerHealthBarPrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("PlayerHealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var bar = new GameObject("Player HP", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root.transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(18f, -18f);
            barRect.sizeDelta = new Vector2(250f, 28f);
            bar.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.09f, 0.9f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bar.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Simple;
            fillImage.color = new Color(0.2f, 0.85f, 0.3f);

            var label = PlayerHealthBar.CreateLabel(bar.transform, "HP Label", 18, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = Color.white;

            var view = root.AddComponent<PlayerHealthBarView>();
            SetReference(view, "canvas", canvas);
            SetReference(view, "bar", barRect);
            SetReference(view, "fill", fillImage);
            SetReference(view, "label", label);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerHealthBarPrefabPath);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab.GetComponent<PlayerHealthBarView>() : null;
        }

        static EnemyHealthBarView CreateEnemyHealthBarPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyHealthBarView>(EnemyHealthBarPrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("EnemyHealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5;
            canvas.enabled = false;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 16f);
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 20f;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.055f, 0.035f, 0.04f, 0.95f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fill.GetComponent<Image>().color = new Color(0.9f, 0.16f, 0.12f);

            var view = root.AddComponent<EnemyHealthBarView>();
            SetReference(view, "canvas", canvas);
            SetReference(view, "fill", fillRect);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyHealthBarPrefabPath);
            Object.DestroyImmediate(root);
            return prefab != null ? prefab.GetComponent<EnemyHealthBarView>() : null;
        }

        static DamageNumber CreateDamageNumberPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DamageNumber>(DamageNumberPrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("Damage Number", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 70f);
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            var damageNumber = root.AddComponent<DamageNumber>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DamageNumberPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab != null ? prefab.GetComponent<DamageNumber>() : damageNumber;
        }

        static LevelUpChoicePanel CreateLevelUpChoicePanelPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LevelUpChoicePanel>(LevelUpChoicePanelPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("LevelUpChoicePanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var modal = new GameObject("Modal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(root.transform, false);
            var modalRect = modal.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.035f, 0.78f);

            var card = new GameObject("Selection Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(modal.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(920f, 650f);
            card.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 1f);

            var title = PlayerHealthBar.CreateLabel(card.transform, "Title", 42, TextAnchor.MiddleCenter);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(820f, 76f);
            title.text = "LEVEL UP!";
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;

            var buttons = new Button[3];
            var titles = new Text[3];
            var descriptions = new Text[3];
            var yPositions = new[] { 135f, -5f, -145f };
            for (var i = 0; i < buttons.Length; i++)
            {
                var option = new GameObject($"Option {i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                option.transform.SetParent(card.transform, false);
                var optionRect = option.GetComponent<RectTransform>();
                optionRect.anchorMin = optionRect.anchorMax = new Vector2(0.5f, 0.5f);
                optionRect.pivot = new Vector2(0.5f, 0.5f);
                optionRect.anchoredPosition = new Vector2(0f, yPositions[i]);
                optionRect.sizeDelta = new Vector2(800f, 124f);

                var image = option.GetComponent<Image>();
                image.color = new Color(0.14f, 0.19f, 0.27f, 1f);
                var button = option.GetComponent<Button>();
                button.targetGraphic = image;
                buttons[i] = button;

                var optionTitle = PlayerHealthBar.CreateLabel(option.transform, "Name", 28, TextAnchor.MiddleLeft);
                var optionTitleRect = optionTitle.rectTransform;
                optionTitleRect.anchorMin = new Vector2(0f, 0.5f);
                optionTitleRect.anchorMax = new Vector2(1f, 1f);
                optionTitleRect.offsetMin = new Vector2(24f, 0f);
                optionTitleRect.offsetMax = new Vector2(-20f, -10f);
                optionTitle.fontStyle = FontStyle.Bold;
                optionTitle.color = new Color(0.9f, 0.95f, 1f);
                titles[i] = optionTitle;

                var description = PlayerHealthBar.CreateLabel(option.transform, "Description", 20, TextAnchor.UpperLeft);
                var descriptionRect = description.rectTransform;
                descriptionRect.anchorMin = new Vector2(0f, 0f);
                descriptionRect.anchorMax = new Vector2(1f, 0.58f);
                descriptionRect.offsetMin = new Vector2(24f, 12f);
                descriptionRect.offsetMax = new Vector2(-20f, 0f);
                description.color = new Color(0.72f, 0.79f, 0.88f);
                descriptions[i] = description;
            }

            var selector = root.AddComponent<LevelUpChoicePanel>();
            SetReference(selector, "panelRoot", modal);
            SetReferenceArray(selector, "optionButtons", buttons);
            SetReferenceArray(selector, "optionTitles", titles);
            SetReferenceArray(selector, "optionDescriptions", descriptions);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LevelUpChoicePanelPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab != null ? prefab.GetComponent<LevelUpChoicePanel>() : null;
        }

        static void ConfigureLevelUpFlowPrefab(string path, LevelUpChoicePanel panelPrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;
            try
            {
                var player = root.GetComponent<PlayerCharacter>();
                var abilities = root.GetComponent<PlayerAbilityController>();
                var flow = root.GetComponent<PlayerLevelUpFlow>() ?? root.AddComponent<PlayerLevelUpFlow>();
                SetReference(flow, "player", player);
                SetReference(flow, "panelPrefab", panelPrefab);
                EnsureReferenceInArray(flow, "choiceSources", abilities);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static InputActionReference FindMoveAction()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(InputActionsPath))
            {
                if (asset is InputActionReference reference && reference.action != null &&
                    reference.action.name == "Move" && reference.action.actionMap?.name == "Player")
                    return reference;
            }
            return null;
        }

        static T LoadOrCreate<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material LoadOrCreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetReferenceArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized array field '{field}'.", target);
                return;
            }
            property.arraySize = values != null ? values.Length : 0;
            for (var i = 0; i < property.arraySize; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureReferenceInArray(Object target, string field, Object value)
        {
            if (value == null) return;
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized array field '{field}'.", target);
                return;
            }

            for (var i = 0; i < property.arraySize; i++)
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == value) return;

            var newIndex = property.arraySize++;
            property.GetArrayElementAtIndex(newIndex).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayerMask(Object target, string field, int mask)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            property.intValue = mask;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
