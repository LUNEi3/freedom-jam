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

        void Awake() => Hide();

        public void Show(IReadOnlyList<LevelUpChoice> choices, Action<string> onSelected)
        {
            if (panelRoot == null) return;
            var visibleCount = choices != null ? Mathf.Min(choices.Count, MaximumVisibleChoices) : 0;
            panelRoot.SetActive(visibleCount > 0);

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
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
