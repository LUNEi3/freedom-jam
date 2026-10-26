using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Attaches a reusable world-space health bar, revealed after accepted damage.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealthSource))]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] CharacterFeedbackConfig config;
        [SerializeField] EnemyHealthBarView viewPrefab;
        CharacterHealthSource source;
        EnemyHealthBarView view;
        Renderer[] bodyRenderers;
        bool revealed;

        void Awake()
        {
            source = GetComponent<CharacterHealthSource>();
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
            if (viewPrefab != null)
            {
                view = Instantiate(viewPrefab, transform, false);
                view.Bind(source, config);
            }
            else Debug.LogError("EnemyHealthBar needs an EnemyHealthBarView prefab.", this);
            Hide();
        }

        void OnEnable()
        {
            if (source == null) source = GetComponent<CharacterHealthSource>();
            if (source == null) return;
            source.Changed += OnChanged;
            source.Damaged += OnDamaged;
            source.LifeStarted += OnLifeStarted;
            source.LifeEnded += OnLifeEnded;
            Hide();
        }

        void OnDisable()
        {
            if (source == null) return;
            source.Changed -= OnChanged;
            source.Damaged -= OnDamaged;
            source.LifeStarted -= OnLifeStarted;
            source.LifeEnded -= OnLifeEnded;
            Hide();
        }

        void LateUpdate()
        {
            if (!revealed || view == null) return;
            var camera = Camera.main;
            if (camera == null) { view.SetVisible(false); return; }

            var bounds = new Bounds(transform.position, Vector3.one * 0.5f);
            var found = false;
            foreach (var bodyRenderer in bodyRenderers)
            {
                if (bodyRenderer == null || !bodyRenderer.enabled) continue;
                if (!found) { bounds = bodyRenderer.bounds; found = true; }
                else bounds.Encapsulate(bodyRenderer.bounds);
            }
            var position = bounds.center + Vector3.up * (bounds.extents.y + HeadOffset);
            var screenPoint = camera.WorldToViewportPoint(position);
            if (screenPoint.z <= 0f || screenPoint.x < 0f || screenPoint.x > 1f || screenPoint.y < 0f || screenPoint.y > 1f)
            {
                view.SetVisible(false);
                return;
            }
            view.SetVisible(true);
            view.transform.position = position;
            view.transform.rotation = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);
            var desired = config != null ? config.enemyBarSize : new Vector2(1.5f, 0.14f);
            var parentScale = transform.lossyScale;
            view.transform.localScale = new Vector3(
                desired.x / Mathf.Max(0.001f, parentScale.x) / 160f,
                desired.y / Mathf.Max(0.001f, parentScale.y) / 16f,
                1f / Mathf.Max(0.001f, parentScale.z));
        }

        float HeadOffset => config != null ? config.enemyBarHeadOffset : 0.18f;

        void OnDamaged(ICharacterHealthSource _, FreedomJam.Game.DamageInfo __) => Show();
        void OnLifeStarted(ICharacterHealthSource _) => Hide();
        void OnLifeEnded(ICharacterHealthSource _) => Hide();
        void OnChanged(ICharacterHealthSource value) => view?.Render(value);

        void Show()
        {
            if (view == null || source == null || !source.IsAlive) return;
            revealed = true;
            view.Render(source);
            view.SetVisible(true);
        }

        void Hide()
        {
            revealed = false;
            if (view != null) view.SetVisible(false);
        }
    }
}
