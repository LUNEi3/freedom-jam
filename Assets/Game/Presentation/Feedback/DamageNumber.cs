using System;
using UnityEngine;
using UnityEngine.UI;

namespace FreedomJam.Game.Presentation
{
    /// <summary>Pooled world-space damage label with its own detached lifetime.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumber : MonoBehaviour
    {
        Text label;
        float age;
        float lifetime;
        float rise;
        Vector3 startPosition;
        Color color;
        Camera worldCamera;
        Action<DamageNumber> finished;

        void Awake()
        {
            var canvas = gameObject.GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            var scaler = gameObject.GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            var rect = transform as RectTransform;
            if (rect != null) rect.sizeDelta = new Vector2(180f, 70f);
            label = GetComponentInChildren<Text>(true);
            if (label == null) label = PlayerHealthBar.CreateLabel(transform, "Damage", 32, TextAnchor.MiddleCenter);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        public void Show(Vector3 position, string value, float fontSize, CharacterFeedbackConfig config, Action<DamageNumber> onFinished)
        {
            worldCamera = Camera.main;
            lifetime = config != null ? config.SafeNumberLifetime : 0.8f;
            rise = config != null ? config.damageNumberRise : 0.8f;
            color = config != null ? config.damageNumberColor : Color.white;
            finished = onFinished;
            age = 0f;
            startPosition = position;
            transform.SetPositionAndRotation(position, worldCamera != null
                ? worldCamera.transform.rotation
                : Quaternion.identity);
            transform.localScale = Vector3.one * 0.01f;
            label.text = value;
            label.fontSize = Mathf.RoundToInt(fontSize);
            label.color = color;
            var outline = label.GetComponent<Outline>() ?? label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.25f, -1.25f);
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / lifetime);
            transform.position = startPosition + Vector3.up * (rise * t);
            if (worldCamera != null)
                transform.rotation = worldCamera.transform.rotation;
            var c = color;
            c.a = 1f - t;
            label.color = c;
            if (age >= lifetime)
            {
                gameObject.SetActive(false);
                finished?.Invoke(this);
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
