using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Presentation
{
    /// <summary>
    /// Lives on the root of the EatStackPanel canvas prefab. Lay the panel out freely in Prefab Mode;
    /// the row template is copied once per creature at runtime (and stays hidden itself).
    /// </summary>
    public sealed class EatStackPanelView : MonoBehaviour
    {
        [Tooltip("Copies of the row template are added here, after any existing children (e.g. the title).")]
        public RectTransform rowsParent;
        [Tooltip("One styled row; hidden at runtime and copied for every creature.")]
        public EatStackRowView rowTemplate;
    }
}
