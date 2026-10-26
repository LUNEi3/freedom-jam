using System;
using FreedomJam.Game.CameraRig;
using FreedomJam.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace FreedomJam.Game.Sandbox.Editor
{
    /// <summary>
    /// Menu: Freedom Jam > Build Player Sandbox.
    /// Creates the player assets and prefab if they don't exist yet (never overwrites them),
    /// then rebuilds Assets/Game/Scenes/PlayerSandbox.unity from scratch.
    /// </summary>
    public static class PlayerSandboxBuilder
    {
        const string PlayerDataFolder = "Assets/Game/Data/Player";
        const string SandboxDataFolder = "Assets/Game/Data/Sandbox";
        const string PrefabFolder = "Assets/Game/Prefabs";
        const string SceneFolder = "Assets/Game/Scenes";
        const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        const string ScenePath = SceneFolder + "/PlayerSandbox.unity";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const int DummyCount = 6;
        const float DummyRingRadius = 7f;

        [MenuItem("Freedom Jam/Build Player Sandbox")]
        public static void Build()
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

            foreach (var folder in new[] { PlayerDataFolder, SandboxDataFolder, PrefabFolder, SceneFolder }) EnsureFolder(folder);

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

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
                playerPrefab = CreatePlayerPrefab(playerLayer, enemyLayer, playerConfig, levelConfig, move, playerMaterial);
            else
                FillMissingPlayerReferences(playerConfig, levelConfig, move);

            CreateGround(groundMaterial);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetupCamera(player.transform);
            for (var i = 0; i < DummyCount; i++) CreateDummy(i, enemyLayer, dummyCreature, dummyMaterial);

            var overlay = new GameObject("Debug Overlay").AddComponent<PlayerDebugOverlay>();
            SetReference(overlay, "player", player.GetComponent<PlayerCharacter>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Player sandbox built: {ScenePath}. Press Play in this scene to test (no MainMenu needed).");
        }

        static GameObject CreatePlayerPrefab(int playerLayer, int enemyLayer, PlayerConfig config, LevelConfig levelConfig,
            InputActionReference move, Material material)
        {
            var root = new GameObject("Player") { layer = playerLayer };

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

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

        static void CreateDummy(int index, int enemyLayer, CreatureData creature, Material material)
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
    }
}
