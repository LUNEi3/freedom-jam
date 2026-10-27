using System.Collections.Generic;
using FreedomJam.Game;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Red accepted-hit flash and player-only transparency during invulnerability.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealthSource))]
    public sealed class CharacterVisualFeedback : MonoBehaviour, ICharacterFlashFeedback
    {
        sealed class RendererState
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Runtime;
            public Color[] BaseColors;
            public string[] ColorProperties;
            public Texture[] BaseTextures;
            public string[] TextureProperties;
            public bool[] PlayerBodyMaterials;
            public bool[] Enabled;
        }

        [SerializeField] CharacterFeedbackConfig config;
        [SerializeField] bool useWhiteDamageFlash;
        [SerializeField] Material playerBodyMaterial;
        CharacterHealthSource source;
        PlayerCharacter player;
        EatStack subscribedEatStack;
        IInvincibilityState invincibility;
        readonly List<RendererState> states = new();
        float flashRemaining;
        float activeFlashDuration;
        Color activeFlashColor;
        Color pendingPlayerBodyColor;
        float playerBodyColorFlashRemaining;
        bool hasPendingPlayerBodyColor;
        bool started;
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
            if (started) SubscribeToEatStack();
            flashRemaining = 0f;
            EnsureMaterials();
            AssignRuntimeMaterials();
            Apply(1f, 0f);
        }

        void Start()
        {
            started = true;
            SubscribeToEatStack();
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
            if (subscribedEatStack != null)
            {
                subscribedEatStack.TierReached -= OnEatStackTierReached;
                subscribedEatStack = null;
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
            if (hasPendingPlayerBodyColor)
            {
                playerBodyColorFlashRemaining = Mathf.Max(0f, playerBodyColorFlashRemaining - Time.unscaledDeltaTime);
                if (playerBodyColorFlashRemaining <= 0f) ApplyPendingPlayerBodyColor();
            }
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

        void SubscribeToEatStack()
        {
            if (player == null || subscribedEatStack != null || player.EatStack == null) return;
            subscribedEatStack = player.EatStack;
            subscribedEatStack.TierReached += OnEatStackTierReached;
        }

        void OnEatStackTierReached(CreatureData _, EatStackTier tier, int __)
        {
            if (tier == null || playerBodyMaterial == null) return;

            Color? latestColor = null;
            foreach (var reward in tier.Rewards)
            {
                if (!(reward is StatMultiplierReward statReward) || !statReward.IsValid) continue;
                switch (statReward.Stat)
                {
                    case StatType.Size:
                        latestColor = config != null ? config.sizeEatUpgradeBodyColor : Color.red;
                        break;
                    case StatType.MoveSpeed:
                        latestColor = config != null ? config.moveSpeedEatUpgradeBodyColor : Color.cyan;
                        break;
                    case StatType.SteerSpeed:
                        latestColor = config != null ? config.steerSpeedEatUpgradeBodyColor : Color.green;
                        break;
                }
            }

            if (!latestColor.HasValue) return;
            BeginPlayerBodyColorChange(latestColor.Value);
        }

        void BeginPlayerBodyColorChange(Color color)
        {
            if (!materialsReady) EnsureMaterials();
            var hasPlayerBodyMaterial = false;
            foreach (var state in states)
                for (var i = 0; i < state.Original.Length; i++)
                    if (state.Original[i] == playerBodyMaterial) hasPlayerBodyMaterial = true;
            if (!hasPlayerBodyMaterial) return;

            pendingPlayerBodyColor = color;
            playerBodyColorFlashRemaining = config != null ? config.SafeEatUpgradeBodyColorFlashDuration : 0.2f;
            hasPendingPlayerBodyColor = playerBodyColorFlashRemaining > 0f;
            if (!hasPendingPlayerBodyColor) ApplyPendingPlayerBodyColor();
            else Apply(1f, 0f);
        }

        void ApplyPendingPlayerBodyColor()
        {
            foreach (var state in states)
                for (var i = 0; i < state.Original.Length; i++)
                    if (state.PlayerBodyMaterials[i]) state.BaseColors[i] = pendingPlayerBodyColor;
            hasPendingPlayerBodyColor = false;
            playerBodyColorFlashRemaining = 0f;
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
                var baseTextures = new Texture[original.Length];
                var textureProperties = new string[original.Length];
                var playerBodyMaterials = new bool[original.Length];
                for (var i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    playerBodyMaterials[i] = original[i] == playerBodyMaterial;
                    runtime[i] = new Material(original[i]) { name = original[i].name + " (Feedback Instance)" };
                    properties[i] = runtime[i].HasProperty("_BaseColor") ? "_BaseColor" :
                        runtime[i].HasProperty("_Color") ? "_Color" : null;
                    if (properties[i] != null) baseColors[i] = runtime[i].GetColor(properties[i]);
                    textureProperties[i] = runtime[i].HasProperty("_BaseMap") ? "_BaseMap" :
                        runtime[i].HasProperty("_MainTex") ? "_MainTex" : null;
                    if (textureProperties[i] != null) baseTextures[i] = runtime[i].GetTexture(textureProperties[i]);
                }
                states.Add(new RendererState
                {
                    Renderer = renderer,
                    Original = original,
                    Runtime = runtime,
                    BaseColors = baseColors,
                    ColorProperties = properties,
                    BaseTextures = baseTextures,
                    TextureProperties = textureProperties,
                    PlayerBodyMaterials = playerBodyMaterials,
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
                    if (material == null) continue;
                    var whiteBodyColorFlash = state.PlayerBodyMaterials[i] && hasPendingPlayerBodyColor;
                    if (property != null)
                    {
                        var original = state.BaseColors[i];
                        var blended = whiteBodyColorFlash ? Color.white : Color.Lerp(original, FlashColor, flash);
                        blended.a = original.a * alphaMultiplier;
                        material.SetColor(property, blended);
                    }

                    if ((useWhiteDamageFlash && flash > 0f || whiteBodyColorFlash) && state.TextureProperties[i] != null)
                    {
                        var textureProperty = state.TextureProperties[i];
                        var texture = Texture2D.whiteTexture;
                        if (material.GetTexture(textureProperty) != texture)
                            material.SetTexture(textureProperty, texture);
                    }
                    else if (state.TextureProperties[i] != null && material.GetTexture(state.TextureProperties[i]) != state.BaseTextures[i])
                        material.SetTexture(state.TextureProperties[i], state.BaseTextures[i]);
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
