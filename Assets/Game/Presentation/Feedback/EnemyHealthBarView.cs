using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Reusable world-space UGUI view used by the EnemyHealthBar character adapter.</summary>
    public sealed class EnemyHealthBarView : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform fill;

        public void Bind(ICharacterHealthSource source, CharacterFeedbackConfig config)
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            var image = fill != null ? fill.GetComponent<Image>() : null;
            if (config != null && image != null) image.color = config.enemyHealthColor;
            Render(source);
            SetVisible(false);
        }

        public void Render(ICharacterHealthSource source)
        {
            if (source == null || fill == null) return;
            var max = source.MaxHP;
            fill.anchorMax = new Vector2(max > 0f ? Mathf.Clamp01(source.CurrentHP / max) : 0f, 1f);
        }

        public void SetVisible(bool visible)
        {
            if (canvas != null) canvas.enabled = visible;
        }
    }
}
