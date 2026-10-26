using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace FreedomJam.Foundation.Editor
{
    public static class FoundationProjectBuilder
    {
        const string ConfigFolder = "Assets/GameJamFoundation/Config";
        const string SceneFolder = "Assets/GameJamFoundation/Scenes";
        const string ConfigPath = ConfigFolder + "/GameJamDefaults.asset";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string MixerPath = "Assets/GameJamFoundation/Audio/GameJamAudioMixer.mixer";
        const string MainMenuPath = SceneFolder + "/MainMenu.unity";
        const string GamePath = SceneFolder + "/Game.unity";
        const string ResultPath = SceneFolder + "/Result.unity";

        public static void BuildScenesAndConfig()
        {
            EnsureFolder(ConfigFolder);
            EnsureFolder(SceneFolder);

            var config = CreateOrUpdateConfig();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (actions == null) throw new InvalidOperationException("Input actions asset not found: " + ActionsPath);
            if (mixer == null) throw new InvalidOperationException("Audio mixer not found: " + MixerPath);

            BuildMainMenu(config, actions, mixer);
            BuildGame(actions);
            BuildResult(actions);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
                new EditorBuildSettingsScene(ResultPath, true)
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        static GameConfig CreateOrUpdateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.mainMenuSceneName = "MainMenu";
            config.gameSceneName = "Game";
            config.resultSceneName = "Result";
            config.initialScore = 0;
            config.timerMode = TimerMode.Countdown;
            config.countdownSeconds = 90f;
            config.bestScoreKey = "game-jam-foundation";
            config.bestScoreDefault = 0;
            config.scoreDirection = ScoreDirection.HigherIsBetter;
            EditorUtility.SetDirty(config);
            return config;
        }

        static void BuildMainMenu(GameConfig config, InputActionAsset actions, AudioMixer mixer)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCameraAndDirectionalLight("Main Menu");
            CreateEventSystem(actions);

            var bootstrapObject = new GameObject("GameFoundationBootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameFoundationBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("inputActions").objectReferenceValue = actions;
            serialized.FindProperty("audioMixer").objectReferenceValue = mixer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            InstantiatePrefab("Assets/GameJamFoundation/Prefabs/MainMenuCanvas.prefab");
            SaveScene(scene, MainMenuPath);
        }

        static void BuildGame(InputActionAsset actions)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCameraAndDirectionalLight("Game");
            CreateEventSystem(actions);
            InstantiatePrefab("Assets/GameJamFoundation/Prefabs/GameHUD.prefab");
            InstantiatePrefab("Assets/GameJamFoundation/Prefabs/PauseOverlay.prefab");
            SaveScene(scene, GamePath);
        }

        static void BuildResult(InputActionAsset actions)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCameraAndDirectionalLight("Result");
            CreateEventSystem(actions);
            InstantiatePrefab("Assets/GameJamFoundation/Prefabs/ResultCanvas.prefab");
            SaveScene(scene, ResultPath);
        }

        static void CreateEventSystem(InputActionAsset actions)
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var eventSystem = eventSystemObject.GetComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;

            var module = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            module.actionsAsset = actions;
            module.point = FindActionReference(actions, "UI/Point");
            module.leftClick = FindActionReference(actions, "UI/Click");
            module.rightClick = FindActionReference(actions, "UI/RightClick");
            module.middleClick = FindActionReference(actions, "UI/MiddleClick");
            module.scrollWheel = FindActionReference(actions, "UI/ScrollWheel");
            module.move = FindActionReference(actions, "UI/Navigate");
            module.submit = FindActionReference(actions, "UI/Submit");
            module.cancel = FindActionReference(actions, "UI/Cancel");
        }

        static InputActionReference FindActionReference(InputActionAsset actions, string actionName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(ActionsPath);
            foreach (var item in assets)
            {
                var reference = item as InputActionReference;
                if (reference != null && reference.name == actionName) return reference;
            }

            throw new InvalidOperationException("Input action reference not found: " + actionName);
        }

        static void AddCameraAndDirectionalLight(string sceneName)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.105f, 1f);

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.name = sceneName + " Key Light";
        }

        static void InstantiatePrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Prefab not found: " + path);
            PrefabUtility.InstantiatePrefab(prefab);
        }

        static void SaveScene(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Could not save scene: " + path);
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
