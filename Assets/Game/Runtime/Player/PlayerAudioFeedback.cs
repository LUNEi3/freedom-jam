using FreedomJam.Foundation;
using UnityEngine;

namespace FreedomJam.Game.Player
{
    /// <summary>Routes player and gameplay one-shot feedback through the foundation SFX bus.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerAudioFeedback : MonoBehaviour
    {
        [SerializeField] PlayerCharacter player;
        [SerializeField] AudioClip enemyHitSfx;
        [SerializeField] AudioClip playerHitSfx;
        [SerializeField] AudioClip lowHpSfx;
        [SerializeField] AudioClip levelUpSfx;
        [SerializeField] AudioClip dashSfx;
        [SerializeField] AudioClip collectSfx;
        [SerializeField] AudioClip abilitySelectedSfx;
        [SerializeField] AudioClip suckSfx;
        [SerializeField] AudioClip eatStackFullSfx;
        [SerializeField] AudioClip explosionChargeSfx;
        [SerializeField] AudioClip explosionSfx;
        [SerializeField] AudioClip reviveSfx;

        [Header("Sound Volumes (SFX bus volume multiplies these)")]
        [SerializeField, Range(0f, 1f)] float enemyHitVolume = 1f;
        [SerializeField, Range(0f, 1f)] float playerHitVolume = 1f;
        [SerializeField, Range(0f, 1f)] float lowHpVolume = 1f;
        [SerializeField, Range(0f, 1f)] float levelUpVolume = 1f;
        [SerializeField, Range(0f, 1f)] float dashVolume = 1f;
        [SerializeField, Range(0f, 1f)] float collectVolume = 1f;
        [SerializeField, Range(0f, 1f)] float abilitySelectedVolume = 1f;
        [SerializeField, Range(0f, 1f)] float suckVolume = 1f;
        [SerializeField, Range(0f, 1f)] float eatStackFullVolume = 1f;
        [SerializeField, Range(0f, 1f)] float explosionChargeVolume = 1f;
        [SerializeField, Range(0f, 1f)] float explosionVolume = 1f;
        [SerializeField, Range(0f, 1f)] float reviveVolume = 1f;
        [Tooltip("AudioSource pitch also controls playback speed.")]
        [SerializeField, Range(0.1f, 3f)] float explosionChargePitch = 1f;

        AudioSource fallbackSource;
        bool started;
        bool subscribed;

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerCharacter>();
        }

        void Start()
        {
            started = true;
            Subscribe();
        }

        void OnEnable()
        {
            if (started) Subscribe();
        }

        void OnDisable() => Unsubscribe();

        void OnDestroy() => Unsubscribe();

        public void PlayEnemyHit() => Play(enemyHitSfx, enemyHitVolume);

        public void PlayDash() => Play(dashSfx, dashVolume);

        public void PlayLevelUp() => Play(levelUpSfx, levelUpVolume);

        public void PlayCollect() => Play(collectSfx, collectVolume);

        public void PlayAbilitySelected() => Play(abilitySelectedSfx, abilitySelectedVolume);

        public void PlaySuck() => Play(suckSfx, suckVolume);

        public void PlayEatStackFull() => Play(eatStackFullSfx, eatStackFullVolume);

        public void PlayExplosionCharge() => Play(explosionChargeSfx, explosionChargeVolume, explosionChargePitch);

        public void PlayExplosion() => Play(explosionSfx, explosionVolume);

        public void PlayRevive() => Play(reviveSfx, reviveVolume);

        void Subscribe()
        {
            if (subscribed || player == null) return;
            player.Damaged += OnPlayerDamaged;
            if (player.EatStack != null) player.EatStack.TierReached += OnEatStackTierReached;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed || player == null) return;
            player.Damaged -= OnPlayerDamaged;
            if (player.EatStack != null) player.EatStack.TierReached -= OnEatStackTierReached;
            subscribed = false;
        }

        void OnEatStackTierReached(CreatureData _, EatStackTier __, int ___) => PlayEatStackFull();

        void OnPlayerDamaged(Character character, DamageInfo _)
        {
            if (character != player) return;

            Play(playerHitSfx, playerHitVolume);
            if (player.MaxHP > 0f && player.CurrentHP <= player.MaxHP * 0.2f)
                Play(lowHpSfx, lowHpVolume);
        }

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            volume = Mathf.Clamp01(volume);
            if (volume <= 0f) return;
            var bootstrap = GameFoundationBootstrap.Instance;
            if (bootstrap != null && bootstrap.Audio != null)
            {
                bootstrap.Audio.PlaySfx(clip, volume, pitch);
                return;
            }

            if (fallbackSource == null)
            {
                fallbackSource = GetComponent<AudioSource>();
                if (fallbackSource == null) fallbackSource = gameObject.AddComponent<AudioSource>();
                fallbackSource.playOnAwake = false;
                fallbackSource.loop = false;
                fallbackSource.spatialBlend = 0f;
            }

            var previousPitch = fallbackSource.pitch;
            fallbackSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            fallbackSource.PlayOneShot(clip, volume);
            fallbackSource.pitch = previousPitch;
        }
    }
}
