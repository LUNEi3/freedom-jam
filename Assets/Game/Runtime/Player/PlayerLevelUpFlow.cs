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

        readonly Queue<int> pendingLevels = new Queue<int>();
        readonly List<LevelUpChoice> availableChoices = new List<LevelUpChoice>(3);
        readonly List<ILevelUpChoiceSource> sources = new List<ILevelUpChoiceSource>(3);

        LevelUpChoicePanel panel;
        GameInputController gameInput;
        float previousTimeScale = 1f;
        bool previousPauseInputEnabled = true;
        bool ownsPause;
        bool ownsPauseInputSuppression;
        bool waitingForChoice;
        bool started;
        bool subscribed;

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerCharacter>();
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
            pendingLevels.Enqueue(level);
            if (!ownsPause)
            {
                previousTimeScale = Time.timeScale;
                var foundation = GameFoundationBootstrap.Instance;
                gameInput = foundation != null ? foundation.InputController : null;
                if (gameInput != null)
                {
                    previousPauseInputEnabled = gameInput.PauseInputEnabled;
                    gameInput.SetPauseInputEnabled(false);
                    ownsPauseInputSuppression = true;
                }
                Time.timeScale = 0f;
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

                if (panel == null || !panel.IsConfigured)
                {
                    Debug.LogError("PlayerLevelUpFlow has no fully configured level-up panel.", this);
                    pendingLevels.Clear();
                    break;
                }

                waitingForChoice = true;
                panel.Show(availableChoices, ApplyChoice);
                return;
            }

            ResumeAfterChoices();
        }

        void ApplyChoice(string id)
        {
            foreach (var source in sources)
                if (source.TryApplyChoice(id)) break;

            waitingForChoice = false;
            ShowNextLevelChoices();
        }

        void ResumeAfterChoices()
        {
            waitingForChoice = false;
            if (!ownsPause) return;
            Time.timeScale = previousTimeScale;
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
    }
}
