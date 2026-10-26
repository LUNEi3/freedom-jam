using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Reusable UGUI view used by the PlayerHealthBar character adapter.</summary>
    public sealed class PlayerHealthBarView : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform bar;
        [SerializeField] Image fill;
        [SerializeField] Text label;
        ICharacterHealthSource source;

        public void Bind(ICharacterHealthSource health, CharacterFeedbackConfig feedback)
        {
            if (source != null) source.Changed -= OnChanged;
            source = health;
            if (source != null) source.Changed += OnChanged;
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (feedback != null)
            {
                bar.sizeDelta = feedback.playerBarSize;
                bar.anchoredPosition = new Vector2(feedback.playerBarPadding.x, -feedback.playerBarPadding.y);
                fill.color = feedback.playerHealthColor;
            }
            Render();
        }

        void OnDestroy()
        {
            if (source != null) source.Changed -= OnChanged;
        }

        void OnChanged(ICharacterHealthSource _) => Render();

        public void SetVisible(bool visible)
        {
            if (canvas != null) canvas.enabled = visible;
        }

        void Render()
        {
            if (source == null || fill == null) return;
            var max = source.MaxHP;
            var current = source.CurrentHP;
            var ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            if (label != null) label.text = $"{Mathf.Max(0f, current):0.#} / {Mathf.Max(0f, max):0.#}";
        }
    }
}
