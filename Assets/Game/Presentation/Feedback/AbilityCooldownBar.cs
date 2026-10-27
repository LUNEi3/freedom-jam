using System.Collections.Generic;
using FreedomJam.Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Presentation
{
    /// <summary>
    /// Bottom-centre row of ability slots (one per ability reported by IAbilityStatusSource): the ability's name (or icon)
    /// and key, a radial cooldown sweep with seconds left, LOCKED until unlocked, and a short pop when ready again.
    /// Builds its own screen-space canvas, like PlayerHudOverlay; new abilities get a slot automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityCooldownBar : MonoBehaviour
    {

        [Header("Layout (reference 1920 x 1080)")]
        [SerializeField, Min(16f)] float slotSize = 96f;
        [SerializeField, Min(0f)] float spacing = 24f;
        [Tooltip("Distance from the bottom edge, above the XP bar.")]
        [SerializeField, Min(0f)] float bottomOffset = 64f;
        [SerializeField] int sortingOrder = 21;

        [Header("Look")]
        [SerializeField] Color slotColor = new Color(0.12f, 0.16f, 0.24f, 0.92f);
        [SerializeField] Color lockedColor = new Color(0.12f, 0.12f, 0.14f, 0.75f);
        [SerializeField] Color cooldownOverlayColor = new Color(0f, 0f, 0f, 0.65f);
        [SerializeField] Color readyTextColor = Color.white;
        [SerializeField] Color lockedTextColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] Color keyBadgeColor = new Color(0.95f, 0.8f, 0.3f, 1f);
        [Tooltip("Optional icons in ability order (Dash, Suck, …). Missing ones show the ability's name.")]
        [SerializeField] Sprite[] icons = new Sprite[0];
        [Tooltip("Scale of the 'ready again' pop.")]
        [SerializeField, Min(1f)] float readyPopScale = 1.2f;
        [SerializeField, Min(0.01f)] float readyPopSeconds = 0.18f;

        sealed class Slot
        {
            public RectTransform root;
            public Image background;
            public Image icon;
            public Text label;
            public Image cooldown;
            public Text seconds;
            public Text key;
            public bool wasReady = true;
            public float popUntil;
        }

        const float BadgeHalf = 14f;

        readonly List<AbilityStatus> abilities = new List<AbilityStatus>(4);
        readonly List<Slot> slots = new List<Slot>(4);
        // Every ability component on this object that reports status (Dash/Suck, Explosion, …), in component order.
        IAbilityStatusSource[] statusSources;
        Canvas canvas;
        RectTransform row;
        Font font;
        Sprite whiteSprite;

        void Awake()
        {
            statusSources = GetComponents<IAbilityStatusSource>();
            if (statusSources.Length == 0) Debug.LogWarning("AbilityCooldownBar found no IAbilityStatusSource on this object, so it shows nothing.", this);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            BuildCanvas();
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            if (whiteSprite != null) Destroy(whiteSprite);
        }

        void LateUpdate()
        {
            if (statusSources == null || statusSources.Length == 0 || canvas == null) return;
            abilities.Clear();
            foreach (var source in statusSources) source.AppendAbilityStatus(abilities);
            while (slots.Count < abilities.Count) slots.Add(CreateSlot(slots.Count));
            for (var i = 0; i < slots.Count; i++)
            {
                var visible = i < abilities.Count;
                slots[i].root.gameObject.SetActive(visible);
                if (visible) Render(slots[i], abilities[i]);
            }
        }

        void Render(Slot slot, AbilityStatus ability)
        {
            var locked = ability.IsLocked;
            slot.background.color = locked ? lockedColor : slotColor;
            slot.cooldown.fillAmount = locked ? 0f : ability.CooldownFraction;
            slot.seconds.text = locked ? "LOCKED" : AbilityStatus.FormatSeconds(ability.CooldownRemaining);
            slot.seconds.fontSize = locked ? 16 : 30;
            slot.key.text = ability.Key;
            var textColor = locked ? lockedTextColor : readyTextColor;
            slot.label.color = textColor;
            if (slot.icon.sprite != null) slot.icon.color = locked ? lockedTextColor : Color.white;
            var showSymbol = ability.IsReady;
            slot.label.text = !showSymbol || slot.icon.sprite != null ? string.Empty : ability.Name;
            slot.icon.enabled = showSymbol && slot.icon.sprite != null;

            // Pop once when an unlocked ability comes off cooldown.
            if (ability.IsReady && !slot.wasReady) slot.popUntil = Time.unscaledTime + readyPopSeconds;
            slot.wasReady = ability.IsReady || locked;
            var pop = slot.popUntil > Time.unscaledTime ? Mathf.Lerp(1f, readyPopScale, (slot.popUntil - Time.unscaledTime) / readyPopSeconds) : 1f;
            slot.root.localScale = Vector3.one * pop;
        }

        void BuildCanvas()
        {
            var canvasObject = new GameObject("Ability Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            row = new GameObject("Slots", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(canvas.transform, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, bottomOffset);
            row.sizeDelta = new Vector2(0f, slotSize + BadgeHalf);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        Slot CreateSlot(int index)
        {
            var slot = new Slot();
            slot.root = new GameObject("Ability " + (index + 1), typeof(RectTransform)).GetComponent<RectTransform>();
            slot.root.SetParent(row, false);
            slot.root.sizeDelta = new Vector2(slotSize, slotSize + BadgeHalf);
            slot.root.pivot = new Vector2(0.5f, 0f);

            slot.background = CreateImage("Background", slot.root, new Vector2(0f, BadgeHalf), new Vector2(slotSize, slotSize));
            slot.icon = CreateImage("Icon", slot.background.rectTransform, Vector2.zero, new Vector2(slotSize * 0.7f, slotSize * 0.7f), centered: true);
            slot.icon.sprite = index < icons.Length ? icons[index] : null;
            slot.icon.enabled = slot.icon.sprite != null;
            slot.icon.preserveAspect = true;
            slot.label = CreateText("Name", slot.background.rectTransform, Mathf.RoundToInt(slotSize * 0.27f), TextAnchor.MiddleCenter, FontStyle.Bold);

            slot.cooldown = CreateImage("Cooldown", slot.background.rectTransform, Vector2.zero, Vector2.zero, stretch: true);
            slot.cooldown.color = cooldownOverlayColor;
            slot.cooldown.type = Image.Type.Filled;
            slot.cooldown.fillMethod = Image.FillMethod.Radial360;
            slot.cooldown.fillOrigin = (int)Image.Origin360.Top;
            slot.cooldown.fillClockwise = false;
            slot.cooldown.fillAmount = 0f;
            slot.seconds = CreateText("Seconds", slot.background.rectTransform, 30, TextAnchor.MiddleCenter, FontStyle.Bold);

            var badge = CreateImage("Key Badge", slot.background.rectTransform, Vector2.zero, new Vector2(34f, BadgeHalf * 2f), centered: true);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            badge.color = keyBadgeColor;
            slot.key = CreateText("Key", badge.rectTransform, 20, TextAnchor.MiddleCenter, FontStyle.Bold);
            slot.key.color = Color.black;

            return slot;
        }

        Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size, bool centered = false, bool stretch = false)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = whiteSprite;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                return image;
            }
            rect.anchorMin = rect.anchorMax = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            rect.pivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        Text CreateText(string name, Transform parent, int size, TextAnchor alignment, FontStyle style)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = readyTextColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return text;
        }
    }
}
