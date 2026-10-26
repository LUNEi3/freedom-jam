using FreedomJam.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreedomJam.Foundation.Editor
{
    public static class FoundationAssetBuilder
    {
        const string PrefabFolder = "Assets/GameJamFoundation/Prefabs";
        const string SettingsPath = PrefabFolder + "/SettingsPanel.prefab";

        static readonly Color BackgroundColor = new Color(0.055f, 0.07f, 0.105f, 1f);
        static readonly Color PanelColor = new Color(0.105f, 0.13f, 0.185f, 0.97f);
        static readonly Color ButtonColor = new Color(0.16f, 0.23f, 0.34f, 1f);
        static readonly Color AccentColor = new Color(0.28f, 0.78f, 0.92f, 1f);

        public static void BuildPrefabs()
        {
            EnsureFolder(PrefabFolder);
            var settingsPrefab = BuildSettingsPanel();
            BuildMainMenu(settingsPrefab);
            BuildPauseOverlay(settingsPrefab);
            BuildHud();
            BuildResult(settingsPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static GameObject BuildSettingsPanel()
        {
            var rootRect = NewRect("SettingsPanel");
            var root = rootRect.gameObject;
            SetCentered(rootRect, new Vector2(1040f, 900f));
            root.AddComponent<Image>().color = PanelColor;
            var layout = root.AddComponent<VerticalLayoutGroup>();
            ConfigureVertical(layout, new RectOffset(42, 42, 34, 28), 18f);

            CreateText("Title", root.transform, "SETTINGS", 34, TextAnchor.MiddleCenter, Color.white, 60f, FontStyle.Bold);

            Slider master;
            Toggle masterMute;
            CreateVolumeRow(root.transform, "MASTER", out master, out masterMute);
            Slider music;
            Toggle musicMute;
            CreateVolumeRow(root.transform, "MUSIC", out music, out musicMute);
            Slider sfx;
            Toggle sfxMute;
            CreateVolumeRow(root.transform, "SOUND EFFECTS", out sfx, out sfxMute);

            CreateText("BindingsTitle", root.transform, "KEYBOARD / GAMEPAD", 22, TextAnchor.MiddleLeft, AccentColor, 42f, FontStyle.Bold);
            var viewport = NewRect("BindingsViewport", root.transform);
            viewport.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.045f, 0.07f, 1f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
            viewportLayout.flexibleHeight = 1f;
            viewportLayout.minHeight = 160f;

            var content = NewRect("BindingsContent", viewport);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVertical(contentLayout, new RectOffset(8, 8, 8, 8), 6f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34f;

            var footer = NewRect("Footer", root.transform);
            var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 18f;
            footerLayout.childControlWidth = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandHeight = false;
            footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
            var reset = CreateButton("ResetBindings", footer, "RESET CONTROLS");
            var close = CreateButton("Close", footer, "DONE");

            var controller = root.AddComponent<SettingsMenuController>();
            controller.masterVolumeSlider = master;
            controller.musicVolumeSlider = music;
            controller.sfxVolumeSlider = sfx;
            controller.masterMuteToggle = masterMute;
            controller.musicMuteToggle = musicMute;
            controller.sfxMuteToggle = sfxMute;
            controller.bindingsRoot = content;
            controller.resetBindingsButton = reset;
            controller.closeButton = close;
            controller.defaultSelection = master;
            root.SetActive(false);
            return SavePrefabAndDestroy(root, SettingsPath);
        }

        static void BuildMainMenu(GameObject settingsPrefab)
        {
            var root = CreateCanvas("MainMenuCanvas", 0);
            CreateFullBackground(root.transform);
            var panel = NewRect("MainPanel", root.transform);
            SetCentered(panel.GetComponent<RectTransform>(), new Vector2(720f, 720f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVertical(layout, new RectOffset(68, 68, 62, 52), 20f);
            CreateText("Title", panel, "FREEDOM JAM", 54, TextAnchor.MiddleCenter, Color.white, 90f, FontStyle.Bold);
            CreateText("Subtitle", panel, "PC GAME FOUNDATION", 20, TextAnchor.MiddleCenter, AccentColor, 40f, FontStyle.Normal);
            var play = CreateButton("Play", panel, "PLAY");
            var settings = CreateButton("Settings", panel, "SETTINGS");
            var quit = CreateButton("Quit", panel, "QUIT");
            var nestedSettings = InstantiateNested(settingsPrefab, root.transform);

            var controller = root.AddComponent<MainMenuController>();
            controller.playButton = play;
            controller.settingsButton = settings;
            controller.quitButton = quit;
            controller.settingsMenu = nestedSettings.GetComponent<SettingsMenuController>();
            controller.settingsPanel = nestedSettings;
            controller.defaultSelection = play;
            SavePrefabAndDestroy(root, PrefabFolder + "/MainMenuCanvas.prefab");
        }

        static void BuildPauseOverlay(GameObject settingsPrefab)
        {
            var root = CreateCanvas("PauseOverlay", 20);
            var pausePanel = NewRect("PausePanel", root.transform);
            SetCentered(pausePanel.GetComponent<RectTransform>(), new Vector2(650f, 560f));
            pausePanel.gameObject.AddComponent<Image>().color = PanelColor;
            var layout = pausePanel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVertical(layout, new RectOffset(72, 72, 48, 42), 18f);
            CreateText("Title", pausePanel, "PAUSED", 42, TextAnchor.MiddleCenter, Color.white, 72f, FontStyle.Bold);
            var resume = CreateButton("Resume", pausePanel, "RESUME");
            var settings = CreateButton("Settings", pausePanel, "SETTINGS");
            var quit = CreateButton("QuitToMenu", pausePanel, "QUIT TO MAIN MENU");
            var nestedSettings = InstantiateNested(settingsPrefab, root.transform);

            var controller = root.AddComponent<PauseMenuController>();
            controller.pausePanel = pausePanel.gameObject;
            controller.resumeButton = resume;
            controller.settingsButton = settings;
            controller.quitToMenuButton = quit;
            controller.settingsMenu = nestedSettings.GetComponent<SettingsMenuController>();
            controller.defaultSelection = resume;
            pausePanel.gameObject.SetActive(false);
            SavePrefabAndDestroy(root, PrefabFolder + "/PauseOverlay.prefab");
        }

        static void BuildHud()
        {
            var root = CreateCanvas("GameHUD", 10);
            var bar = NewRect("HudBar", root.transform);
            StretchTop(bar.GetComponent<RectTransform>(), 112f);
            var background = bar.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.045f, 0.07f, 0.9f);
            background.raycastTarget = false;

            var score = CreateText("Score", bar, "0", 30, TextAnchor.MiddleLeft, Color.white, 0f, FontStyle.Bold);
            var scoreRect = score.rectTransform;
            scoreRect.anchorMin = new Vector2(0f, 0f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.offsetMin = new Vector2(52f, 0f);
            scoreRect.offsetMax = new Vector2(0f, 0f);
            var time = CreateText("Time", bar, "00:00", 30, TextAnchor.MiddleRight, AccentColor, 0f, FontStyle.Bold);
            var timeRect = time.rectTransform;
            timeRect.anchorMin = new Vector2(0.5f, 0f);
            timeRect.anchorMax = new Vector2(1f, 1f);
            timeRect.offsetMin = new Vector2(0f, 0f);
            timeRect.offsetMax = new Vector2(-52f, 0f);

            var controller = root.AddComponent<GameHudController>();
            controller.scoreLabel = score;
            controller.timeLabel = time;
            SavePrefabAndDestroy(root, PrefabFolder + "/GameHUD.prefab");
        }

        static void BuildResult(GameObject settingsPrefab)
        {
            var root = CreateCanvas("ResultCanvas", 0);
            CreateFullBackground(root.transform);
            var panel = NewRect("ResultPanel", root.transform);
            SetCentered(panel.GetComponent<RectTransform>(), new Vector2(760f, 760f));
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVertical(layout, new RectOffset(68, 68, 48, 40), 15f);
            CreateText("Title", panel, "RUN COMPLETE", 42, TextAnchor.MiddleCenter, Color.white, 72f, FontStyle.Bold);
            var score = CreateText("FinalScore", panel, "0", 36, TextAnchor.MiddleCenter, AccentColor, 50f, FontStyle.Bold);
            var best = CreateText("BestScore", panel, "BEST 0", 22, TextAnchor.MiddleCenter, Color.white, 38f, FontStyle.Normal);
            var time = CreateText("RunTime", panel, "00:00", 22, TextAnchor.MiddleCenter, Color.white, 38f, FontStyle.Normal);
            var again = CreateButton("PlayAgain", panel, "PLAY AGAIN");
            var menu = CreateButton("MainMenu", panel, "MAIN MENU");
            var settingsButton = CreateButton("Settings", panel, "SETTINGS");
            var quit = CreateButton("Quit", panel, "QUIT");
            var nestedSettings = InstantiateNested(settingsPrefab, root.transform);

            var controller = root.AddComponent<ResultMenuController>();
            controller.scoreLabel = score;
            controller.bestScoreLabel = best;
            controller.timeLabel = time;
            controller.playAgainButton = again;
            controller.mainMenuButton = menu;
            controller.settingsButton = settingsButton;
            controller.quitButton = quit;
            controller.settingsMenu = nestedSettings.GetComponent<SettingsMenuController>();
            controller.defaultSelection = again;
            SavePrefabAndDestroy(root, PrefabFolder + "/ResultCanvas.prefab");
        }

        static void CreateVolumeRow(Transform parent, string label, out Slider slider, out Toggle toggle)
        {
            var row = NewRect(label + "Row", parent);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            var labelText = CreateText(label + "Label", row, label, 18, TextAnchor.MiddleLeft, Color.white, 0f, FontStyle.Bold);
            labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;
            var sliderObject = NewRect(label + "Slider", row);
            sliderObject.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            slider = CreateSlider(sliderObject);
            var toggleRoot = NewRect(label + "Mute", row);
            toggleRoot.gameObject.AddComponent<LayoutElement>().preferredWidth = 118f;
            toggle = CreateToggle(toggleRoot);
            var muteLabel = CreateText("MuteLabel", toggleRoot, "MUTE", 14, TextAnchor.MiddleLeft, Color.white, 0f, FontStyle.Normal);
            muteLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            muteLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            muteLabel.rectTransform.offsetMin = new Vector2(38f, 0f);
            muteLabel.rectTransform.offsetMax = Vector2.zero;
        }

        static Slider CreateSlider(Transform parent)
        {
            var background = NewRect("Track", parent);
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.055f, 0.085f, 1f);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.34f);
            bgRect.anchorMax = new Vector2(1f, 0.66f);
            bgRect.offsetMin = new Vector2(4f, 0f);
            bgRect.offsetMax = new Vector2(-4f, 0f);

            var fillArea = NewRect("FillArea", parent);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.34f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.66f);
            fillAreaRect.offsetMin = new Vector2(4f, 0f);
            fillAreaRect.offsetMax = new Vector2(-4f, 0f);
            var fill = NewRect("Fill", fillArea);
            fill.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            fill.GetComponent<RectTransform>().anchorMax = Vector2.one;
            fill.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            fill.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = AccentColor;

            var handleArea = NewRect("HandleArea", parent);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(4f, 0f);
            handleAreaRect.offsetMax = new Vector2(-4f, 0f);
            var handle = NewRect("Handle", handleArea);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(24f, 34f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;

            var slider = parent.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImage;
            return slider;
        }

        static Toggle CreateToggle(Transform parent)
        {
            var background = parent.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.055f, 0.085f, 1f);
            var check = NewRect("Checkmark", parent);
            check.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.18f);
            check.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.82f);
            check.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 0f);
            check.GetComponent<RectTransform>().anchoredPosition = new Vector2(12f, 0f);
            check.gameObject.AddComponent<Image>().color = AccentColor;
            var toggle = parent.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check.GetComponent<Image>();
            return toggle;
        }

        static Button CreateButton(string name, Transform parent, string text)
        {
            var buttonObject = NewRect(name, parent);
            var image = buttonObject.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = buttonObject.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = ButtonColor,
                highlightedColor = new Color(0.2f, 0.34f, 0.48f, 1f),
                pressedColor = AccentColor,
                selectedColor = new Color(0.18f, 0.3f, 0.42f, 1f),
                disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f),
                colorMultiplier = 1f,
                fadeDuration = 0.12f
            };
            var layout = buttonObject.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 62f;
            layout.minHeight = 48f;
            var label = CreateText("Label", buttonObject, text, 22, TextAnchor.MiddleCenter, Color.white, 0f, FontStyle.Bold);
            label.raycastTarget = false;
            StretchToParent(label.rectTransform, 8f);
            return button;
        }

        static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment,
            Color color, float preferredHeight, FontStyle style)
        {
            var textObject = NewRect(name, parent);
            var text = textObject.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (preferredHeight > 0f) textObject.gameObject.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            return text;
        }

        static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return root;
        }

        static void CreateFullBackground(Transform parent)
        {
            var background = NewRect("Background", parent);
            StretchToParent(background.GetComponent<RectTransform>());
            var image = background.gameObject.AddComponent<Image>();
            image.color = BackgroundColor;
            image.raycastTarget = false;
        }

        static GameObject InstantiateNested(GameObject prefab, Transform parent)
        {
            var nested = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            nested.transform.SetParent(parent, false);
            return nested;
        }

        static RectTransform NewRect(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null) gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        static void SetCentered(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        static void StretchToParent(RectTransform rect, float horizontalInset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, 0f);
            rect.offsetMax = new Vector2(-horizontalInset, 0f);
        }

        static void StretchTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
        }

        static void ConfigureVertical(VerticalLayoutGroup layout, RectOffset padding, float spacing)
        {
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var folder = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        static GameObject SavePrefabAndDestroy(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
