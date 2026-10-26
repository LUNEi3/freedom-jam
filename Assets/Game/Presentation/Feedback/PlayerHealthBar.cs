using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Attaches a reusable screen-space health bar view to a player character.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealthSource))]
    public sealed class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] CharacterFeedbackConfig config;
        [SerializeField] PlayerHealthBarView viewPrefab;
        CharacterHealthSource source;
        PlayerHealthBarView view;

        void Awake()
        {
            source = GetComponent<CharacterHealthSource>();
            if (viewPrefab != null)
            {
                view = Instantiate(viewPrefab);
                view.transform.SetParent(null, false);
                view.Bind(source, config);
            }
            else Debug.LogError("PlayerHealthBar needs a PlayerHealthBarView prefab.", this);
        }

        void OnEnable()
        {
            if (view != null) view.SetVisible(true);
        }

        void OnDisable()
        {
            if (view != null) view.SetVisible(false);
        }

        void OnDestroy()
        {
            if (view != null) Destroy(view.gameObject);
        }

        public static UnityEngine.UI.Text CreateLabel(Transform parent, string name, int fontSize, UnityEngine.TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = UnityEngine.HorizontalWrapMode.Overflow;
            text.verticalOverflow = UnityEngine.VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
