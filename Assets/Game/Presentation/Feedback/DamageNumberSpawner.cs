using System.Collections.Generic;
using FreedomJam.Game;
using FreedomJam.Game.Player;
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
        [SerializeField] bool isPlayer;
        readonly Stack<DamageNumber> available = new();
        readonly HashSet<DamageNumber> active = new();
        Renderer[] bodyRenderers;
        CharacterHealthSource source;
        PlayerCharacter player;
        EatStack subscribedEatStack;

        void Awake()
        {
            source = GetComponent<CharacterHealthSource>();
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
            if (isPlayer) player = GetComponent<PlayerCharacter>();
        }

        void OnEnable()
        {
            if (source == null) source = GetComponent<CharacterHealthSource>();
            if (source != null) source.Damaged += OnDamaged;
            SubscribeToEatStack();
        }

        void Start() => SubscribeToEatStack();

        void OnDisable()
        {
            if (source != null) source.Damaged -= OnDamaged;
            if (subscribedEatStack != null)
            {
                subscribedEatStack.TierReached -= OnEatStackTierReached;
                subscribedEatStack = null;
            }
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
            var spread = config != null ? Mathf.Max(0f, config.damageNumberSpread) : 0.25f;
            var position = GetBodyTopPosition() +
                new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
            var magnitude = Mathf.Max(0f, info.Amount);
            var minSize = config != null ? config.damageNumberMinSize : 22f;
            var maxSize = config != null ? config.damageNumberMaxSize : 42f;
            var scale = config != null ? config.damageNumberScalePerPoint : 2f;
            var fontSize = Mathf.Clamp(minSize + magnitude * scale, minSize, Mathf.Max(minSize, maxSize));
            if (isPlayer && config != null)
                fontSize *= Mathf.Max(1f, config.playerDamageNumberSizeMultiplier);
            var numberColor = isPlayer && config != null ? config.playerDamageNumberColor :
                config != null ? config.damageNumberColor : Color.white;
            ShowNumber(position, $"{magnitude:0.#}", fontSize, numberColor);
        }

        void SubscribeToEatStack()
        {
            if (!isPlayer || subscribedEatStack != null) return;
            if (player == null) player = GetComponent<PlayerCharacter>();
            if (player == null || player.EatStack == null) return;
            subscribedEatStack = player.EatStack;
            subscribedEatStack.TierReached += OnEatStackTierReached;
        }

        void OnEatStackTierReached(CreatureData _, EatStackTier tier, int __)
        {
            if (tier == null) return;
            var labelIndex = 0;
            foreach (var reward in tier.Rewards)
            {
                if (!(reward is StatMultiplierReward statReward) || !statReward.IsValid) continue;
                var label = statReward.Stat switch
                {
                    StatType.Size => "+Size",
                    StatType.MoveSpeed => "+Move Speed",
                    StatType.SteerSpeed => "+Steer Speed",
                    _ => null
                };
                if (label == null) continue;

                var position = GetBodyTopPosition() + Vector3.up * (0.3f * labelIndex++);
                var fontSize = Mathf.Max(32f, config != null ? config.damageNumberMinSize : 22f);
                var color = config != null ? config.upgradeFlashColor : new Color(0.2f, 1f, 0.3f, 1f);
                ShowNumber(position, label, fontSize, color);
            }
        }

        Vector3 GetBodyTopPosition()
        {
            var bounds = new Bounds(transform.position + Vector3.up, Vector3.one * 0.5f);
            var found = false;
            foreach (var bodyRenderer in bodyRenderers)
            {
                if (bodyRenderer == null) continue;
                if (bodyRenderer is ParticleSystemRenderer || bodyRenderer is TrailRenderer || bodyRenderer is LineRenderer)
                    continue;
                if (!found) { bounds = bodyRenderer.bounds; found = true; }
                else bounds.Encapsulate(bodyRenderer.bounds);
            }
            return bounds.center + Vector3.up * (bounds.extents.y + 0.2f);
        }

        void ShowNumber(Vector3 position, string text, float fontSize, Color numberColor)
        {
            var number = available.Count > 0 ? available.Pop() : CreateNumber();
            number.transform.SetParent(null, true);
            active.Add(number);
            number.Show(position, text, fontSize, config, numberColor, Release);
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
