using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreedomJam.Foundation
{
    public sealed class SettingsMenuController : MonoBehaviour
    {
        public Slider masterVolumeSlider;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;
        public Toggle masterMuteToggle;
        public Toggle musicMuteToggle;
        public Toggle sfxMuteToggle;
        public Button resetBindingsButton;
        public Button closeButton;
        public Transform bindingsRoot;
        public Selectable defaultSelection;

        readonly List<GameObject> bindingRows = new List<GameObject>();
        SettingsStore store;
        AudioService audio;
        InputBindingService input;
        GameSettings currentSettings;
        Action onClose;
        bool initialized;
        bool listenersBound;

        public void Initialize(SettingsStore store, AudioService audio, InputBindingService input, GameSettings currentSettings)
        {
            UnbindControls();
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.currentSettings = currentSettings ?? store.LoadSettings(new GameSettings());
            initialized = true;
            LoadSettingsIntoControls();
            RebuildBindingRows();
            BindControls();
        }

        void Start() => EnsureInitialized();

        public void Open(Action onClose)
        {
            EnsureInitialized();
            this.onClose = onClose;
            SyncSettingsFromStore();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            LoadSettingsIntoControls();
            RebuildBindingRows();
            BindControls();
            SelectDefault();
        }

        public void Close()
        {
            SaveSettings();
            var closeCallback = onClose;
            onClose = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            closeCallback?.Invoke();
        }

        public void SaveSettings()
        {
            if (store == null || currentSettings == null) return;
            var latest = store.LoadSettings(new GameSettings());
            latest.MasterVolume = currentSettings.MasterVolume;
            latest.MusicVolume = currentSettings.MusicVolume;
            latest.SfxVolume = currentSettings.SfxVolume;
            latest.MasterMuted = currentSettings.MasterMuted;
            latest.MusicMuted = currentSettings.MusicMuted;
            latest.SfxMuted = currentSettings.SfxMuted;
            store.SaveSettings(latest);
            CopySettings(latest, currentSettings);
            if (GameFoundationBootstrap.Instance != null)
                GameFoundationBootstrap.Instance.SaveSettings(latest);
        }

        public void ResetBindings()
        {
            if (input == null) return;
            input.ResetOverrides();
            if (store != null && currentSettings != null)
                currentSettings.BindingOverridesJson = store.LoadSettings(new GameSettings()).BindingOverridesJson;
            SaveSettings();
            RebuildBindingRows();
        }

        void OnMasterVolumeChanged(float value)
        {
            currentSettings.MasterVolume = value;
            audio?.SetVolume(AudioBus.Master, value);
        }

        void OnMusicVolumeChanged(float value)
        {
            currentSettings.MusicVolume = value;
            audio?.SetVolume(AudioBus.Music, value);
        }

        void OnSfxVolumeChanged(float value)
        {
            currentSettings.SfxVolume = value;
            audio?.SetVolume(AudioBus.SFX, value);
        }

        void OnMasterMuteChanged(bool value)
        {
            currentSettings.MasterMuted = value;
            audio?.SetMuted(AudioBus.Master, value);
        }

        void OnMusicMuteChanged(bool value)
        {
            currentSettings.MusicMuted = value;
            audio?.SetMuted(AudioBus.Music, value);
        }

        void OnSfxMuteChanged(bool value)
        {
            currentSettings.SfxMuted = value;
            audio?.SetMuted(AudioBus.SFX, value);
        }

        void OnCloseClicked() => Close();

        void OnResetBindingsClicked() => ResetBindings();

        void OnEnable() => BindControls();

        void OnDisable()
        {
            UnbindControls();
            input?.CancelRebind();
        }

        void OnDestroy()
        {
            UnbindControls();
            input?.CancelRebind();
            ClearBindingRows();
        }

        void EnsureInitialized()
        {
            if (initialized) return;
            var bootstrap = GameFoundationBootstrap.Instance;
            if (bootstrap == null) return;
            Initialize(bootstrap.SettingsStore, bootstrap.Audio, bootstrap.InputBindings, bootstrap.CurrentSettings);
        }

        void SyncSettingsFromStore()
        {
            if (store == null) return;
            var stored = store.LoadSettings(new GameSettings());
            if (currentSettings == null) currentSettings = stored;
            else CopySettings(stored, currentSettings);
        }

        void LoadSettingsIntoControls()
        {
            if (currentSettings == null) return;
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(currentSettings.MasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(currentSettings.MusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(currentSettings.SfxVolume);
            if (masterMuteToggle != null) masterMuteToggle.SetIsOnWithoutNotify(currentSettings.MasterMuted);
            if (musicMuteToggle != null) musicMuteToggle.SetIsOnWithoutNotify(currentSettings.MusicMuted);
            if (sfxMuteToggle != null) sfxMuteToggle.SetIsOnWithoutNotify(currentSettings.SfxMuted);
        }

        void BindControls()
        {
            if (!initialized || listenersBound) return;
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (masterMuteToggle != null) masterMuteToggle.onValueChanged.AddListener(OnMasterMuteChanged);
            if (musicMuteToggle != null) musicMuteToggle.onValueChanged.AddListener(OnMusicMuteChanged);
            if (sfxMuteToggle != null) sfxMuteToggle.onValueChanged.AddListener(OnSfxMuteChanged);
            if (resetBindingsButton != null) resetBindingsButton.onClick.AddListener(OnResetBindingsClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            listenersBound = true;
        }

        void UnbindControls()
        {
            if (!listenersBound) return;
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            if (masterMuteToggle != null) masterMuteToggle.onValueChanged.RemoveListener(OnMasterMuteChanged);
            if (musicMuteToggle != null) musicMuteToggle.onValueChanged.RemoveListener(OnMusicMuteChanged);
            if (sfxMuteToggle != null) sfxMuteToggle.onValueChanged.RemoveListener(OnSfxMuteChanged);
            if (resetBindingsButton != null) resetBindingsButton.onClick.RemoveListener(OnResetBindingsClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
            listenersBound = false;
        }

        void RebuildBindingRows()
        {
            ClearBindingRows();
            if (bindingsRoot == null || input == null) return;

            foreach (var entry in input.GetRebindableBindings())
            {
                var row = new GameObject(entry.ActionMap + "_" + entry.ActionName + "_" + entry.BindingIndex + "_" + entry.ControlScheme,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                row.transform.SetParent(bindingsRoot, false);
                var button = row.GetComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(row.transform, false);
                var label = labelObject.GetComponent<Text>();
                label.font = GetBuiltinFont();
                label.alignment = TextAnchor.MiddleLeft;
                label.color = Color.white;
                label.fontSize = 16;
                StretchToParent(label.rectTransform);
                button.onClick.AddListener(() => BeginRebind(entry, label));
                bindingRows.Add(row);
                UpdateBindingLabel(entry, label, false);
            }
        }

        void BeginRebind(InputBindingEntry entry, Text label)
        {
            if (input == null) return;
            label.text = entry.ActionMap + " / " + entry.ActionName + " / " + entry.DisplayName + " [" + entry.ControlScheme + "]: press a control (Esc cancels)";
            input.BeginRebind(entry.ActionMap, entry.ActionName, entry.BindingIndex, () =>
            {
                UpdateBindingLabel(entry, label, false);
                SaveSettings();
            });
        }

        void UpdateBindingLabel(InputBindingEntry entry, Text label, bool waiting)
        {
            if (label == null) return;
            var control = input != null ? input.GetBindingDisplayString(entry) : "Unbound";
            label.text = waiting
                ? entry.ActionMap + " / " + entry.ActionName + " / " + entry.DisplayName + " [" + entry.ControlScheme + "]: press a control (Esc cancels)"
                : entry.ActionMap + " / " + entry.ActionName + " / " + entry.DisplayName + " [" + entry.ControlScheme + "]: " + control;
        }

        void ClearBindingRows()
        {
            foreach (var row in bindingRows)
                if (row != null) Destroy(row);
            bindingRows.Clear();
        }

        void SelectDefault()
        {
            if (defaultSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(defaultSelection.gameObject);
        }

        static Font GetBuiltinFont()
            => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 0f);
            rect.offsetMax = new Vector2(-12f, 0f);
        }

        static void CopySettings(GameSettings source, GameSettings destination)
        {
            destination.MasterVolume = source.MasterVolume;
            destination.MusicVolume = source.MusicVolume;
            destination.SfxVolume = source.SfxVolume;
            destination.MasterMuted = source.MasterMuted;
            destination.MusicMuted = source.MusicMuted;
            destination.SfxMuted = source.SfxMuted;
            destination.BindingOverridesJson = source.BindingOverridesJson;
        }
    }
}
