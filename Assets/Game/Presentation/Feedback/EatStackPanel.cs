using System.Collections.Generic;
using FreedomJam.Game.Player;
using UnityEngine;

namespace FreedomJam.Game.Presentation
{
    /// <summary>
    /// Shows, for each creature in the player's eat stack table, how many are eaten and a bar to the next tier
    /// (MAX after the last). The look lives in the Panel Prefab (a canvas with EatStackPanelView), editable in
    /// Prefab Mode; this fills it in. Rows come from the table, so new creatures show up on their own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EatStackPanel : MonoBehaviour
    {
        [SerializeField] PlayerCharacter player;
        [Tooltip("Canvas prefab with an EatStackPanelView on its root (Presentation/Feedback/EatStackPanel.prefab).")]
        [SerializeField] EatStackPanelView panelPrefab;

        [Header("States (the rest of the look is in the prefab)")]
        [Tooltip("Count text and bar colour once every tier is reached.")]
        [SerializeField] Color maxColor = new Color(0.45f, 1f, 0.55f, 1f);
        [Tooltip("Row background tint when a creature reaches a new tier; fades out over Flash Seconds.")]
        [SerializeField] Color flashColor = new Color(1f, 0.85f, 0.3f, 0.55f);
        [SerializeField, Min(0.05f)] float flashSeconds = 0.6f;

        sealed class Row
        {
            public CreatureData creature;
            public EatStackRowView view;
            public Color countColor;
            public Color fillColor;
            public Color backgroundColor;
            public float flashUntil;
        }

        readonly List<Row> rows = new List<Row>(4);
        EatStack stack;
        EatStackPanelView panel;

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerCharacter>();
            if (panelPrefab == null) Debug.LogWarning("EatStackPanel has no Panel Prefab, so it shows nothing.", this);
        }

        void OnDestroy()
        {
            if (stack != null) stack.TierReached -= OnTierReached;
            if (panel != null) Destroy(panel.gameObject);
        }

        void LateUpdate()
        {
            if (stack == null && !TryBuild()) return;
            foreach (var row in rows) Render(row, stack.GetProgress(row.creature));
        }

        // The player creates its EatStack in Awake, so wait for it, then copy the row template per tracked creature.
        bool TryBuild()
        {
            if (player == null || player.EatStack == null || panelPrefab == null) return false;
            stack = player.EatStack;
            stack.TierReached += OnTierReached;
            panel = Instantiate(panelPrefab);
            var template = panel.rowTemplate;
            if (template == null || panel.rowsParent == null)
            {
                Debug.LogWarning("EatStackPanel prefab needs Rows Parent and Row Template set on its EatStackPanelView.", panel);
                return true;
            }
            template.gameObject.SetActive(false);
            foreach (var creature in stack.TrackedCreatures)
            {
                var view = Instantiate(template, panel.rowsParent);
                view.name = "Row " + creature.name;
                view.gameObject.SetActive(true);
                rows.Add(new Row
                {
                    creature = creature,
                    view = view,
                    countColor = view.countLabel != null ? view.countLabel.color : Color.white,
                    fillColor = view.fillImage != null ? view.fillImage.color : Color.white,
                    backgroundColor = view.background != null ? view.background.color : Color.clear,
                });
            }
            return true;
        }

        void OnTierReached(CreatureData creature, EatStackTier tier, int tierNumber)
        {
            foreach (var row in rows)
                if (row.creature == creature) row.flashUntil = Time.unscaledTime + flashSeconds;
        }

        void Render(Row row, EatProgress progress)
        {
            var view = row.view;
            if (view.nameLabel != null) view.nameLabel.text = progress.Name;
            if (view.countLabel != null)
            {
                view.countLabel.text = progress.IsMax ? $"{progress.Count}  MAX" : $"{progress.Count} / {progress.NextThreshold}";
                view.countLabel.color = progress.IsMax ? maxColor : row.countColor;
            }
            if (view.fill != null)
            {
                view.fill.anchorMax = new Vector2(progress.Fraction, view.fill.anchorMax.y);
                view.fill.offsetMax = new Vector2(0f, view.fill.offsetMax.y);
            }
            if (view.fillImage != null) view.fillImage.color = progress.IsMax ? maxColor : row.fillColor;
            if (view.background != null)
            {
                var flash = row.flashUntil > Time.unscaledTime ? (row.flashUntil - Time.unscaledTime) / flashSeconds : 0f;
                view.background.color = Color.Lerp(row.backgroundColor, flashColor, flash);
            }
        }
    }
}
