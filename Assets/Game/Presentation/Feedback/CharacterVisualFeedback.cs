using System.Collections.Generic;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Red accepted-hit flash and player-only transparency during invulnerability.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealthSource))]
    public sealed class CharacterVisualFeedback : MonoBehaviour
    {
        sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Runtime;
            public Color[] BaseColors;
            public string[] ColorProperties;
            public bool[] Enabled;
        }

        [SerializeField] CharacterFeedbackConfig config;
        [SerializeField] bool useWhiteDamageFlash;
        CharacterHealthSource source;
        IInvincibilityState invincibility;
        readonly List<RendererState> states = new();
        float flashRemaining;
        bool materialsReady;

        float FlashDuration => config != null ? config.SafeFlashDuration : 0.12f;
        float BlinkFrequency => config != null ? config.SafeBlinkFrequency : 10f;
        float MinimumAlpha => config != null ? Mathf.Clamp01(config.minimumInvincibilityAlpha) : 0.25f;
        Color FlashColor => useWhiteDamageFlash ? Color.white : config != null ? config.damageFlashColor : Color.red;

        void Awake()
        {
            source = GetComponent<CharacterHealthSource>();
            FindInvincibilityState();
        }

        void OnEnable()
        {
            if (source == null) source = GetComponent<CharacterHealthSource>();
            if (invincibility == null) FindInvincibilityState();
            if (source != null)
            {
                source.Damaged += OnDamaged;
                source.LifeStarted += OnLifeStarted;
                source.LifeEnded += OnLifeEnded;
            }
            flashRemaining = 0f;
            EnsureMaterials();
            AssignRuntimeMaterials();
            Apply(1f, 0f);
        }

        void OnDisable()
        {
            if (source != null)
            {
                source.Damaged -= OnDamaged;
                source.LifeStarted -= OnLifeStarted;
                source.LifeEnded -= OnLifeEnded;
            }
            RestoreOriginals();
        }

        void OnDestroy()
        {
            RestoreOriginals();
            DisposeRuntimeMaterials();
        }

        void Update()
        {
            if (!materialsReady) EnsureMaterials();
            if (FlashDuration > 0f) flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
            var blink = invincibility != null && invincibility.IsInvincible && BlinkFrequency > 0f;
            SetTransparent(blink);
            var alpha = blink && Mathf.Sin(Time.time * BlinkFrequency * Mathf.PI * 2f) < 0f ? MinimumAlpha : 1f;
            var flash = FlashDuration > 0f ? Mathf.Clamp01(flashRemaining / FlashDuration) : 0f;
            Apply(alpha, flash);
        }

        void OnDamaged(ICharacterHealthSource _, FreedomJam.Game.DamageInfo __) => flashRemaining = FlashDuration;

        void FindInvincibilityState()
        {
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IInvincibilityState state)
                {
                    invincibility = state;
                    return;
                }
        }

        void OnLifeStarted(ICharacterHealthSource _)
        {
            flashRemaining = 0f;
            RestoreEnabledStates();
            Apply(1f, 0f);
        }

        void OnLifeEnded(ICharacterHealthSource _)
        {
            flashRemaining = 0f;
            Apply(1f, 0f);
        }

        void EnsureMaterials()
        {
            if (materialsReady) return;
            var renderers = GetComponentsInChildren<Renderer>(true);
            states.Clear();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var original = renderer.sharedMaterials;
                var runtime = new Material[original.Length];
                var baseColors = new Color[original.Length];
                var properties = new string[original.Length];
                for (var i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    runtime[i] = new Material(original[i]) { name = original[i].name + " (Feedback Instance)" };
                    properties[i] = runtime[i].HasProperty("_BaseColor") ? "_BaseColor" :
                        runtime[i].HasProperty("_Color") ? "_Color" : null;
                    if (properties[i] != null) baseColors[i] = runtime[i].GetColor(properties[i]);
                }
                states.Add(new RendererState
                {
                    Renderer = renderer,
                    Original = original,
                    Runtime = runtime,
                    BaseColors = baseColors,
                    ColorProperties = properties,
                    Enabled = new[] { renderer.enabled }
                });
            }
            materialsReady = true;
        }

        void AssignRuntimeMaterials()
        {
            foreach (var state in states)
                if (state.Renderer != null) state.Renderer.sharedMaterials = state.Runtime;
        }

        static void MakeTransparent(Material material)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        void SetTransparent(bool transparent)
        {
            foreach (var state in states)
            {
                for (var i = 0; i < state.Runtime.Length; i++)
                {
                    var runtime = state.Runtime[i];
                    var original = state.Original[i];
                    if (runtime == null || original == null) continue;
                    if (transparent) MakeTransparent(runtime);
                    else RestoreRenderSettings(runtime, original);
                }
            }
        }

        static void RestoreRenderSettings(Material target, Material original)
        {
            CopyFloat(target, original, "_Surface");
            CopyFloat(target, original, "_Blend");
            CopyFloat(target, original, "_SrcBlend");
            CopyFloat(target, original, "_DstBlend");
            CopyFloat(target, original, "_ZWrite");
            target.SetOverrideTag("RenderType", original.GetTag("RenderType", false, ""));
            if (original.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else target.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (original.IsKeywordEnabled("_ALPHATEST_ON")) target.EnableKeyword("_ALPHATEST_ON");
            else target.DisableKeyword("_ALPHATEST_ON");
            target.renderQueue = original.renderQueue;
        }

        static void CopyFloat(Material target, Material original, string property)
        {
            if (target.HasProperty(property) && original.HasProperty(property)) target.SetFloat(property, original.GetFloat(property));
        }

        void Apply(float alphaMultiplier, float flash)
        {
            foreach (var state in states)
            {
                if (state.Renderer == null) continue;
                for (var i = 0; i < state.Runtime.Length; i++)
                {
                    var material = state.Runtime[i];
                    var property = state.ColorProperties[i];
                    if (material == null || property == null) continue;
                    var original = state.BaseColors[i];
                    var blended = Color.Lerp(original, FlashColor, flash);
                    blended.a = original.a * alphaMultiplier;
                    material.SetColor(property, blended);
                }
            }
        }

        void RestoreEnabledStates()
        {
            foreach (var state in states)
                if (state.Renderer != null && state.Enabled.Length > 0) state.Renderer.enabled = state.Enabled[0];
        }

        void RestoreOriginals()
        {
            foreach (var state in states)
            {
                if (state.Renderer == null) continue;
                state.Renderer.sharedMaterials = state.Original;
                if (state.Enabled.Length > 0) state.Renderer.enabled = state.Enabled[0];
            }
        }

        void DisposeRuntimeMaterials()
        {
            foreach (var state in states)
                foreach (var material in state.Runtime)
                    if (material != null) Destroy(material);
            states.Clear();
            materialsReady = false;
        }
    }
}
