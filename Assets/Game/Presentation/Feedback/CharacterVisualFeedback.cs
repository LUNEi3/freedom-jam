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
        PlayerCharacter player;
        IInvincibilityState invincibility;
        readonly List<RendererState> states = new();
        float flashRemaining;
        float activeFlashDuration;
        Color activeFlashColor;
        bool materialsReady;

        float FlashDuration => config != null ? config.SafeFlashDuration : 0.12f;
        float BlinkFrequency => config != null ? config.SafeBlinkFrequency : 10f;
        float MinimumAlpha => config != null ? Mathf.Clamp01(config.minimumInvincibilityAlpha) : 0.25f;
        Color FlashColor => activeFlashColor;

        void Awake()
        {
            source = GetComponent<CharacterHealthSource>();
            player = GetComponent<PlayerCharacter>();
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
            if (player != null) player.UpgradeApplied += OnUpgradeApplied;
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
            if (player != null) player.UpgradeApplied -= OnUpgradeApplied;
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
            flashRemaining = Mathf.Max(0f, flashRemaining - Time.unscaledDeltaTime);
            var blink = invincibility != null && invincibility.IsInvincible && BlinkFrequency > 0f;
            SetTransparent(blink);
            var alpha = blink && Mathf.Sin(Time.time * BlinkFrequency * Mathf.PI * 2f) < 0f ? MinimumAlpha : 1f;
            var flash = activeFlashDuration > 0f ? Mathf.Clamp01(flashRemaining / activeFlashDuration) : 0f;
            Apply(alpha, flash);
        }

        void OnDamaged(ICharacterHealthSource _, FreedomJam.Game.DamageInfo __)
        {
            activeFlashColor = useWhiteDamageFlash ? Color.white : config != null ? config.damageFlashColor : Color.red;
            flashRemaining = FlashDuration;
            activeFlashDuration = FlashDuration;
            if (useWhiteDamageFlash) Apply(1f, 1f);
        }

        public void PlayFlash(Color color, float duration)
        {
            activeFlashColor = color;
            flashRemaining = Mathf.Max(0f, duration);
            activeFlashDuration = Mathf.Max(0f, duration);
        }

        void OnUpgradeApplied()
        {
            var green = config != null ? config.upgradeFlashColor : new Color(0.2f, 1f, 0.3f, 1f);
            PlayFlash(green, config != null ? config.upgradeFlashDuration : 0.3f);

            var particleObject = new GameObject("Upgrade Particles");
            particleObject.transform.position = transform.position + Vector3.up * 0.5f;
            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = green;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var count = config != null ? Mathf.Clamp(config.upgradeParticleCount, 0, short.MaxValue) : 18;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            particles.Play();
        }

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
            activeFlashDuration = 0f;
            activeFlashColor = Color.clear;
            RestoreEnabledStates();
            Apply(1f, 0f);
        }

        void OnLifeEnded(ICharacterHealthSource _)
        {
            // Keep a dying enemy's lethal-hit flash visible during its short death linger.
            if (useWhiteDamageFlash) return;
            flashRemaining = 0f;
            activeFlashDuration = 0f;
            activeFlashColor = Color.clear;
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
