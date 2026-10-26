using System;
using FreedomJam.Game.Enemy;
using FreedomJam.Game.Pickups;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreedomJam.Game.Sandbox.Editor
{
    /// <summary>
    /// Builds the Enemy track's placeholder assets and the Enemy sandbox scene.
    /// Only creates what is missing, so tuned values in existing assets are never overwritten.
    /// </summary>
    public static class EnemySandboxBuilder
    {
        const string MaterialFolder = "Assets/Game/Art/Materials";
        const string CreatureFolder = "Assets/Game/Data/Creatures";
        const string SpawnFolder = "Assets/Game/Data/Spawn";
        const string PrefabFolder = "Assets/Game/Prefabs";
        const string SandboxFolder = "Assets/Game/Sandbox";
        const string ScenePath = SandboxFolder + "/EnemySandbox.unity";
        const string LitShader = "Universal Render Pipeline/Lit";

        [MenuItem("Tools/Freedom Jam/Build Enemy Sandbox")]
        public static void Build()
        {
            foreach (var folder in new[] { MaterialFolder, CreatureFolder, SpawnFolder, PrefabFolder, SandboxFolder }) EnsureFolder(folder);

            var emberMaterial = GetOrCreateMaterial("Ember", new Color(1f, 0.45f, 0.1f));
            var zipperMaterial = GetOrCreateMaterial("Zipper", new Color(0.1f, 0.85f, 1f));
            var orbMaterial = GetOrCreateMaterial("XpOrb", new Color(0.3f, 1f, 0.35f));
            var playerMaterial = GetOrCreateMaterial("SandboxPlayer", new Color(0.75f, 0.45f, 1f));
            var groundMaterial = GetOrCreateMaterial("SandboxGround", new Color(0.16f, 0.18f, 0.22f));
            var noseMaterial = GetOrCreateMaterial("Nose", new Color(0.1f, 0.1f, 0.12f));

            var ember = GetOrCreateCreature("Ember", c =>
            {
                c.displayName = "Ember"; c.maxHP = 8f; c.moveSpeed = 2.5f; c.bodySize = 1f;
                c.contactDamage = 1f; c.contactDamageInterval = 1f; c.xpValue = 2; c.scoreValue = 10;
                c.eatMultipliers.Add(new StatBonus { stat = StatType.FireDamage, bonus = 0.1f });
            });
            var zipper = GetOrCreateCreature("Zipper", c =>
            {
                c.displayName = "Zipper"; c.maxHP = 4f; c.moveSpeed = 4.5f; c.bodySize = 0.7f;
                c.contactDamage = 0.5f; c.contactDamageInterval = 0.8f; c.xpValue = 1; c.scoreValue = 10;
                c.eatMultipliers.Add(new StatBonus { stat = StatType.MoveSpeed, bonus = 0.1f });
            });

            AssignPrefab(ember, GetOrCreateEnemyPrefab("Enemy_Ember", emberMaterial, noseMaterial));
            AssignPrefab(zipper, GetOrCreateEnemyPrefab("Enemy_Zipper", zipperMaterial, noseMaterial));
            var orbPrefab = GetOrCreateOrbPrefab(orbMaterial);

            var spawnConfig = GetOrCreateAsset<SpawnConfig>(SpawnFolder + "/DefaultSpawn.asset", s =>
            {
                s.creatures.Add(new SpawnConfig.CreatureEntry { creature = ember, weight = AnimationCurve.Constant(0f, 120f, 1f) });
                s.creatures.Add(new SpawnConfig.CreatureEntry { creature = zipper, weight = AnimationCurve.Linear(0f, 0.3f, 120f, 1.5f) });
            });
            var playerData = GetOrCreateAsset<SandboxPlayerData>(SandboxFolder + "/SandboxPlayerData.asset", p =>
            {
                p.maxHP = 20f; p.moveSpeed = 4f; p.bodySize = 1f;
            });

            if (!System.IO.File.Exists(ScenePath)) BuildScene(spawnConfig, orbPrefab, playerData, playerMaterial, groundMaterial, noseMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemySandboxBuilder] Done. Open " + ScenePath + " and press Play.");
        }

        static void BuildScene(SpawnConfig spawnConfig, XpOrb orbPrefab, SandboxPlayerData playerData,
            Material playerMaterial, Material groundMaterial, Material noseMaterial)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(200f, 1f, 200f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());

                var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = "SandboxPlayer";
                player.layer = LayerMask.NameToLayer("Player");
                player.transform.position = new Vector3(0f, 1f, 0f);
                player.GetComponent<Renderer>().sharedMaterial = playerMaterial;
                player.GetComponent<Collider>().isTrigger = true;
                MakeKinematic(player.AddComponent<Rigidbody>());
                AddNose(player.transform, noseMaterial, 0.5f);
                var sandboxPlayer = player.AddComponent<SandboxPlayer>();
                SetFields(sandboxPlayer, ("data", playerData), ("enemyMask", (object)(int)LayerMask.GetMask("Enemy")));

                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.07f, 0.105f);
                cameraObject.AddComponent<AudioListener>();
                SetFields(cameraObject.AddComponent<SandboxFollowCamera>(), ("target", player.transform));
                cameraObject.transform.position = player.transform.position + new Vector3(0f, 20f, -12f);
                cameraObject.transform.LookAt(player.transform);

                var systems = new GameObject("Sandbox Systems");
                var spawner = systems.AddComponent<EnemySpawner>();
                SetFields(spawner, ("config", spawnConfig));
                SetFields(systems.AddComponent<XpOrbSpawner>(), ("orbPrefab", orbPrefab));
                SetFields(systems.AddComponent<SandboxHud>(), ("player", sandboxPlayer), ("spawner", spawner));

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Could not save scene: " + ScenePath);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static GameObject GetOrCreateEnemyPrefab(string name, Material bodyMaterial, Material noseMaterial)
        {
            var path = PrefabFolder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                root.name = name;
                root.layer = LayerMask.NameToLayer("Enemy");
                root.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
                root.GetComponent<Collider>().isTrigger = true;
                MakeKinematic(root.AddComponent<Rigidbody>());
                AddNose(root.transform, noseMaterial, 0.45f);
                root.AddComponent<BasicEnemy>();
                root.AddComponent<EnemyContactDamage>();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static XpOrb GetOrCreateOrbPrefab(Material material)
        {
            var path = PrefabFolder + "/XpOrb.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<XpOrb>(path);
            if (existing != null) return existing;

            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                root.name = "XpOrb";
                root.layer = LayerMask.NameToLayer("Pickup");
                root.transform.localScale = Vector3.one * 0.35f;
                root.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
                root.AddComponent<XpOrb>();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<XpOrb>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static CreatureData GetOrCreateCreature(string name, Action<CreatureData> setup) =>
            GetOrCreateAsset(CreatureFolder + "/" + name + ".asset", setup);

        static T GetOrCreateAsset<T>(string path, Action<T> setup) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            setup(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void AssignPrefab(CreatureData creature, GameObject prefab)
        {
            if (creature.prefab != null) return;
            creature.prefab = prefab;
            EditorUtility.SetDirty(creature);
        }

        static Material GetOrCreateMaterial(string name, Color color)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find(LitShader);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + LitShader);
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void AddNose(Transform parent, Material material, float forward)
        {
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            UnityEngine.Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().sharedMaterial = material;
            nose.transform.SetParent(parent, false);
            nose.transform.localPosition = new Vector3(0f, 0.1f, forward);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            nose.layer = parent.gameObject.layer;
        }

        static void MakeKinematic(Rigidbody body)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        static void SetFields(UnityEngine.Object target, params (string name, object value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach (var (name, value) in fields)
            {
                var property = serialized.FindProperty(name) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field '{name}'.");
                if (value is int mask) property.intValue = mask;
                else property.objectReferenceValue = (UnityEngine.Object)value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var folder = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
