using System.Collections.Generic;
using FreedomJam.Game;
using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Creates detached floating numbers for accepted damage, pooling up to 24 labels.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterHealthSource))]
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] CharacterFeedbackConfig config;
        [SerializeField] DamageNumber numberPrefab;
        readonly Stack<DamageNumber> available = new();
        readonly HashSet<DamageNumber> active = new();
        Renderer[] bodyRenderers;
        CharacterHealthSource source;

        void Awake()
        {
            source = GetComponent<CharacterHealthSource>();
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable()
        {
            if (source == null) source = GetComponent<CharacterHealthSource>();
            if (source != null) source.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (source != null) source.Damaged -= OnDamaged;
        }

        void OnDestroy()
        {
            foreach (var number in active) if (number != null) Destroy(number.gameObject);
            active.Clear();
            foreach (var number in available) if (number != null) Destroy(number.gameObject);
            available.Clear();
        }

        void OnDamaged(ICharacterHealthSource _, DamageInfo info)
        {
            var bounds = new Bounds(transform.position + Vector3.up, Vector3.one * 0.5f);
            var found = false;
            foreach (var bodyRenderer in bodyRenderers)
            {
                if (bodyRenderer == null) continue;
                if (!found) { bounds = bodyRenderer.bounds; found = true; }
                else bounds.Encapsulate(bodyRenderer.bounds);
            }
            var spread = config != null ? Mathf.Max(0f, config.damageNumberSpread) : 0.25f;
            var position = bounds.center + Vector3.up * (bounds.extents.y + 0.2f) +
                new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
            var magnitude = Mathf.Max(0f, info.Amount);
            var minSize = config != null ? config.damageNumberMinSize : 22f;
            var maxSize = config != null ? config.damageNumberMaxSize : 42f;
            var scale = config != null ? config.damageNumberScalePerPoint : 2f;
            var fontSize = Mathf.Clamp(minSize + magnitude * scale, minSize, Mathf.Max(minSize, maxSize));
            var number = available.Count > 0 ? available.Pop() : CreateNumber();
            number.transform.SetParent(null, true);
            active.Add(number);
            number.Show(position, $"{magnitude:0.#}", fontSize, config, Release);
        }

        DamageNumber CreateNumber()
        {
            if (numberPrefab != null) return Instantiate(numberPrefab);
            var go = new GameObject("Floating Damage Number", typeof(RectTransform), typeof(DamageNumber));
            return go.GetComponent<DamageNumber>();
        }

        void Release(DamageNumber number)
        {
            active.Remove(number);
            if (number == null) return;
            if (available.Count < 24) available.Push(number);
            else Destroy(number.gameObject);
        }
    }
}
