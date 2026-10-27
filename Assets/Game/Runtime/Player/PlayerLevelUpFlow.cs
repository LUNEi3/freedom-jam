using System.Collections.Generic;
using FreedomJam.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace FreedomJam.Game.Player
{
    /// <summary>Queues one upgrade selection per player level gained and pauses play while choosing.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLevelUpFlow : MonoBehaviour
    {
        [SerializeField] PlayerCharacter player;
        [SerializeField] LevelUpChoicePanel panelPrefab;
        [SerializeField] MonoBehaviour[] choiceSources;
        [SerializeField] GameplayTimeScaleCoordinator timeScaleCoordinator;

        PlayerAudioFeedback audioFeedback;

        readonly Queue<int> pendingLevels = new Queue<int>();
        readonly List<LevelUpChoice> availableChoices = new List<LevelUpChoice>(3);
        readonly List<ILevelUpChoiceSource> sources = new List<ILevelUpChoiceSource>(3);

        LevelUpChoicePanel panel;
        GameInputController gameInput;
        float previousTimeScale = 1f;
        int pauseRequestId;
        bool previousPauseInputEnabled = true;
        bool ownsPause;
        bool ownsPauseInputSuppression;
        bool waitingForChoice;
        bool started;
        bool subscribed;

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerCharacter>();
            if (timeScaleCoordinator == null) timeScaleCoordinator = GetComponent<GameplayTimeScaleCoordinator>();
            audioFeedback = GetComponent<PlayerAudioFeedback>();
            if (panelPrefab != null)
                panel = Instantiate(panelPrefab);

            if (choiceSources == null) return;
            foreach (var source in choiceSources)
                if (source is ILevelUpChoiceSource choiceSource)
                    sources.Add(choiceSource);
        }

        void Start()
        {
            started = true;
            Subscribe();
            EnsureEventSystem();
            if (panelPrefab != null && (panel == null || !panel.IsConfigured))
                Debug.LogError("PlayerLevelUpFlow has an incomplete level-up panel prefab.", this);
        }

        void OnEnable()
        {
            if (started) Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
            CancelPendingChoices();
        }

        void OnDestroy()
        {
            Unsubscribe();
            CancelPendingChoices();
            if (panel != null) Destroy(panel.gameObject);
        }

        void Subscribe()
        {
            if (subscribed || player == null || player.Experience == null) return;
            player.Experience.LeveledUp += OnLeveledUp;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed || player == null || player.Experience == null) return;
            player.Experience.LeveledUp -= OnLeveledUp;
            subscribed = false;
        }

        void OnLeveledUp(int level)
        {
            // XP can still be picked up in the moment after the run ends; don't open a choice over the result.
            var flow = GameFoundationBootstrap.Instance != null ? GameFoundationBootstrap.Instance.Flow : null;
            if (flow != null && flow.CurrentRun != null && flow.State == GameState.Result) return;

            pendingLevels.Enqueue(level);
            if (!ownsPause)
            {
                if (timeScaleCoordinator != null)
                    pauseRequestId = timeScaleCoordinator.Acquire(0f);
                else
                {
                    previousTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                var foundation = GameFoundationBootstrap.Instance;
                gameInput = foundation != null ? foundation.InputController : null;
                if (gameInput != null)
                {
                    previousPauseInputEnabled = gameInput.PauseInputEnabled;
                    gameInput.SetPauseInputEnabled(false);
                    ownsPauseInputSuppression = true;
                }
                ownsPause = true;
            }
            ShowNextLevelChoices();
        }

        void ShowNextLevelChoices()
        {
            if (waitingForChoice) return;

            while (pendingLevels.Count > 0)
            {
                pendingLevels.Dequeue();
                availableChoices.Clear();
                foreach (var source in sources)
                    source.AppendAvailableChoices(availableChoices);
                if (availableChoices.Count == 0) continue;

                var displayedChoices = SelectChoices(availableChoices, 3);

                if (panel == null || !panel.IsConfigured)
                {
                    Debug.LogError("PlayerLevelUpFlow has no fully configured level-up panel.", this);
                    pendingLevels.Clear();
                    break;
                }

                waitingForChoice = true;
                panel.Show(displayedChoices, ApplyChoice);
                if (audioFeedback != null) audioFeedback.PlayLevelUp();
                return;
            }

            ResumeAfterChoices();
        }

        void ApplyChoice(string id)
        {
            var applied = false;
            foreach (var source in sources)
                if (source.TryApplyChoice(id)) { applied = true; break; }

            if (applied)
            {
                if (player != null) player.NotifyUpgradeApplied();
                if (audioFeedback != null) audioFeedback.PlayAbilitySelected();
            }

            waitingForChoice = false;
            ShowNextLevelChoices();
        }

        void ResumeAfterChoices()
        {
            waitingForChoice = false;
            if (!ownsPause) return;
            if (timeScaleCoordinator != null && pauseRequestId > 0)
                timeScaleCoordinator.Release(pauseRequestId);
            else
                Time.timeScale = previousTimeScale;
            pauseRequestId = 0;
            ownsPause = false;
            if (ownsPauseInputSuppression && gameInput != null)
                gameInput.SetPauseInputEnabled(previousPauseInputEnabled);
            ownsPauseInputSuppression = false;
            gameInput = null;
        }

        void CancelPendingChoices()
        {
            pendingLevels.Clear();
            availableChoices.Clear();
            waitingForChoice = false;
            if (panel != null) panel.Hide();
            ResumeAfterChoices();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static List<LevelUpChoice> SelectChoices(List<LevelUpChoice> choices, int maximum)
        {
            var selected = new List<LevelUpChoice>(Mathf.Min(maximum, choices.Count));
            if (choices.Count <= maximum)
            {
                selected.AddRange(choices);
                return selected;
            }

            var shuffled = new List<LevelUpChoice>(choices);
            for (var i = shuffled.Count - 1; i > 0; i--)
            {
                var swapIndex = Random.Range(0, i + 1);
                (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
            }
            for (var i = 0; i < maximum; i++) selected.Add(shuffled[i]);
            return selected;
        }
    }
}
