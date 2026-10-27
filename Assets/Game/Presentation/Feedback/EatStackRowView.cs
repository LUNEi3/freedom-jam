using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Presentation
{
    /// <summary>One row of the eat-stack panel. The fill grows by widening its RectTransform (anchor max X = progress).</summary>
    public sealed class EatStackRowView : MonoBehaviour
    {
        [Tooltip("Optional; tinted briefly when a tier is reached.")]
        public Image background;
        public Text nameLabel;
        public Text countLabel;
        [Tooltip("Stretch it to the bar; its right anchor is set to the progress (0–1).")]
        public RectTransform fill;
        [Tooltip("Optional; recoloured for MAX.")]
        public Image fillImage;
    }
}
