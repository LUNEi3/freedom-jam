using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Player
{
    /// <summary>Displays up to three level-up choices in a reusable uGUI modal panel.</summary>
    public sealed class LevelUpChoicePanel : MonoBehaviour
    {
        const int MaximumVisibleChoices = 3;

        [SerializeField] GameObject panelRoot;
        [SerializeField] Button[] optionButtons = new Button[MaximumVisibleChoices];
        [SerializeField] Text[] optionTitles = new Text[MaximumVisibleChoices];
        [SerializeField] Text[] optionDescriptions = new Text[MaximumVisibleChoices];
        [SerializeField, Min(0.01f)] float popDuration = 0.28f;
        [SerializeField, Range(0.5f, 0.95f)] float initialScale = 0.72f;

        Transform animatedCard;
        float animationTime;
        bool animating;

        public bool IsConfigured
        {
            get
            {
                if (panelRoot == null || optionButtons == null || optionTitles == null || optionDescriptions == null ||
                    optionButtons.Length < MaximumVisibleChoices || optionTitles.Length < MaximumVisibleChoices ||
                    optionDescriptions.Length < MaximumVisibleChoices) return false;

                for (var i = 0; i < MaximumVisibleChoices; i++)
                    if (optionButtons[i] == null || optionTitles[i] == null || optionDescriptions[i] == null) return false;
                return true;
            }
        }

        void Awake()
        {
            if (panelRoot != null)
            {
                var card = panelRoot.transform.Find("Selection Card");
                if (card != null) animatedCard = card;
            }
            Hide();
        }

        void Update()
        {
            if (!animating || animatedCard == null) return;
            animationTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(animationTime / Mathf.Max(0.01f, popDuration));
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            var overshoot = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
            animatedCard.localScale = Vector3.one * Mathf.Lerp(initialScale, 1f, eased) * overshoot;
            if (t >= 1f)
            {
                animatedCard.localScale = Vector3.one;
                animating = false;
            }
        }

        public void Show(IReadOnlyList<LevelUpChoice> choices, Action<string> onSelected)
        {
            if (panelRoot == null) return;
            var visibleCount = choices != null ? Mathf.Min(choices.Count, MaximumVisibleChoices) : 0;
            panelRoot.SetActive(visibleCount > 0);
            if (visibleCount > 0 && animatedCard != null)
            {
                animatedCard.localScale = Vector3.one * Mathf.Clamp(initialScale, 0.05f, 1f);
                animationTime = 0f;
                animating = true;
            }

            for (var i = 0; i < MaximumVisibleChoices; i++)
            {
                var visible = i < visibleCount;
                if (optionButtons != null && i < optionButtons.Length && optionButtons[i] != null)
                {
                    var button = optionButtons[i];
                    button.onClick.RemoveAllListeners();
                    button.gameObject.SetActive(visible);
                    if (visible)
                    {
                        var choiceId = choices[i].Id;
                        button.onClick.AddListener(() =>
                        {
                            Hide();
                            onSelected?.Invoke(choiceId);
                        });
                    }
                }

                if (!visible) continue;
                if (optionTitles != null && i < optionTitles.Length && optionTitles[i] != null)
                    optionTitles[i].text = choices[i].Title;
                if (optionDescriptions != null && i < optionDescriptions.Length && optionDescriptions[i] != null)
                    optionDescriptions[i].text = choices[i].Description;
            }
        }

        public void Hide()
        {
            animating = false;
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
