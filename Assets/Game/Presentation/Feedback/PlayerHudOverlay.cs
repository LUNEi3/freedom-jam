using FreedomJam.Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Creates a screen-space XP bar and low-health vignette for the player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter), typeof(CharacterHealthSource))]
    public sealed class PlayerHudOverlay : MonoBehaviour
    {
        const int VignetteTextureSize = 256;

        [SerializeField] CharacterFeedbackConfig config;

        PlayerCharacter player;
        CharacterHealthSource health;
        Canvas canvas;
        Image experienceFill;
        Image vignette;
        Texture2D vignetteTexture;
        Sprite vignetteSprite;
        float textureAspect;

        void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            health = GetComponent<CharacterHealthSource>();
            BuildView();
        }

        void OnEnable()
        {
            if (canvas != null) canvas.enabled = true;
        }

        void OnDisable()
        {
            if (canvas != null) canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            if (vignetteSprite != null) Destroy(vignetteSprite);
            if (vignetteTexture != null) Destroy(vignetteTexture);
        }

        void LateUpdate()
        {
            if (player == null || canvas == null) return;
            UpdateVignetteTextureForAspect();
            UpdateExperienceBar();
            UpdateVignette();
        }

        void BuildView()
        {
            var canvasObject = new GameObject("Player HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var overlay = CreateImage("Low Health Vignette", canvas.transform);
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = false;
            vignette = overlay;
            RebuildVignetteTexture();

            var track = CreateImage("Experience Track", canvas.transform);
            var trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0f);
            trackRect.pivot = new Vector2(0.5f, 0f);
            trackRect.anchoredPosition = Vector2.zero;
            trackRect.sizeDelta = new Vector2(0f, config != null ? Mathf.Max(4f, config.experienceBarHeight) : 16f);
            track.color = config != null ? config.experienceBarBackgroundColor : new Color(0.025f, 0.035f, 0.05f, 0.95f);
            track.raycastTarget = false;

            var fill = CreateImage("Experience Fill", track.transform);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.color = config != null ? config.experienceBarColor : new Color(0.18f, 0.82f, 0.96f, 1f);
            fill.raycastTarget = false;
            experienceFill = fill;
        }

        void UpdateExperienceBar()
        {
            var progress = player.Experience != null ? Mathf.Clamp01(player.Experience.Progress) : 0f;
            var rect = experienceFill.rectTransform;
            rect.anchorMax = new Vector2(progress, 1f);
            rect.offsetMax = Vector2.zero;
        }

        void UpdateVignette()
        {
            var maxHp = health != null ? health.MaxHP : player.MaxHP;
            var currentHp = health != null ? health.CurrentHP : player.CurrentHP;
            var ratio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
            var threshold = config != null ? Mathf.Clamp(config.lowHealthVignetteStartRatio, 0.01f, 1f) : 0.5f;
            var darkness = Mathf.Clamp01((threshold - ratio) / threshold);
            var maximumAlpha = config != null ? Mathf.Clamp01(config.lowHealthVignetteMaximumAlpha) : 0.8f;
            vignette.color = new Color(0f, 0f, 0f, darkness * maximumAlpha);
        }

        void UpdateVignetteTextureForAspect()
        {
            if (Screen.height <= 0) return;
            var aspect = Screen.width / (float)Screen.height;
            if (Mathf.Abs(aspect - textureAspect) > 0.01f) RebuildVignetteTexture(aspect);
        }

        void RebuildVignetteTexture(float aspect = -1f)
        {
            if (aspect <= 0f) aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
            textureAspect = aspect;
            var pixels = new Color[VignetteTextureSize * VignetteTextureSize];
            var edgeStart = config != null ? Mathf.Clamp(config.lowHealthVignetteEdgeStart, 0.1f, 0.95f) : 0.55f;
            var falloff = config != null ? Mathf.Max(0.1f, config.lowHealthVignetteFalloff) : 1.5f;
            var cornerDistance = Mathf.Sqrt(aspect * aspect + 1f);
            for (var y = 0; y < VignetteTextureSize; y++)
            for (var x = 0; x < VignetteTextureSize; x++)
            {
                var nx = ((x / (float)(VignetteTextureSize - 1)) * 2f - 1f) * aspect;
                var ny = y / (float)(VignetteTextureSize - 1) * 2f - 1f;
                var radius = Mathf.Sqrt(nx * nx + ny * ny) / cornerDistance;
                var t = Mathf.Clamp01((radius - edgeStart) / (1f - edgeStart));
                t = t * t * (3f - 2f * t);
                pixels[y * VignetteTextureSize + x] = new Color(0f, 0f, 0f, Mathf.Pow(t, falloff));
            }

            if (vignetteSprite != null) Destroy(vignetteSprite);
            if (vignetteTexture != null) Destroy(vignetteTexture);
            vignetteTexture = new Texture2D(VignetteTextureSize, VignetteTextureSize, TextureFormat.RGBA32, false, true)
            {
                name = "Low Health Vignette Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            vignetteTexture.SetPixels(pixels);
            vignetteTexture.Apply(false, true);
            vignetteSprite = Sprite.Create(vignetteTexture, new Rect(0f, 0f, VignetteTextureSize, VignetteTextureSize), new Vector2(0.5f, 0.5f));
            vignette.sprite = vignetteSprite;
            vignette.type = Image.Type.Simple;
        }

        static Image CreateImage(string name, Transform parent)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            return imageObject.GetComponent<Image>();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
