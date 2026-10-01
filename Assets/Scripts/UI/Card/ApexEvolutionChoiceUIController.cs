using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Card.Behavior;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI
{
    public sealed class ApexEvolutionChoiceUIController : MonoBehaviour
    {
        private CardRuntimeManager _runtimeManager;
        private GameObject _panel;
        private string _visibleChoiceSignature;

        private void OnEnable()
        {
            _runtimeManager = CardRuntimeManager.Instance;
            if (_runtimeManager != null)
                _runtimeManager.OnApexEvolutionChoicesUpdated += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_runtimeManager != null)
                _runtimeManager.OnApexEvolutionChoicesUpdated -= Refresh;
            DestroyPanel();
        }

        private void Refresh()
        {
            if (_runtimeManager == null)
                _runtimeManager = CardRuntimeManager.Instance;
            if (_runtimeManager == null) return;

            var choices = _runtimeManager.GetApexEvolutionChoices();
            if (choices.Count == 0)
            {
                DestroyPanel();
                return;
            }

            Transform popupRoot = UIManager.Instance != null ? UIManager.Instance.PopupRoot : null;
            if (popupRoot == null) return;

            string signature = BuildSignature(choices);
            if (_panel != null && _visibleChoiceSignature == signature) return;

            DestroyPanel();
            CreatePanel(popupRoot, choices, signature);
        }

        private void CreatePanel(Transform parent, IReadOnlyList<CardMutationDefinition> choices, string signature)
        {
            _panel = new GameObject("ApexEvolutionChoicePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            _panel.transform.SetParent(parent, false);

            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            float availableWidth = 600f;
            float availableHeight = 520f;
            if (parent is RectTransform parentRect && parentRect.rect.width > 0f)
                availableWidth = Mathf.Max(300f, parentRect.rect.width - 48f);
            if (parent is RectTransform parentHeightRect && parentHeightRect.rect.height > 0f)
                availableHeight = Mathf.Max(280f, parentHeightRect.rect.height - 40f);
            panelRect.sizeDelta = new Vector2(
                Mathf.Min(600f, availableWidth),
                Mathf.Min(166f + choices.Count * 82f, availableHeight));

            var panelImage = _panel.GetComponent<Image>();
            panelImage.color = new Color(0.055f, 0.075f, 0.085f, 0.98f);

            var layout = _panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 24);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateLabel(_panel.transform, "Apex Evolution", 26f, FontStyles.Bold, 42f, TextAlignmentOptions.Center);
            CreateLabel(_panel.transform, "Choose one mutation. Each mutation can be selected once this battle.", 16f, FontStyles.Normal, 38f, TextAlignmentOptions.Center);

            foreach (var choice in choices)
                CreateChoiceButton(_panel.transform, choice);

            _visibleChoiceSignature = signature;
        }

        private void CreateChoiceButton(Transform parent, CardMutationDefinition choice)
        {
            var buttonObject = new GameObject($"Mutation_{choice.Id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.21f, 0.23f, 1f);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 76f;
            layoutElement.minHeight = 76f;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.21f, 0.23f, 1f);
            colors.highlightedColor = new Color(0.20f, 0.36f, 0.36f, 1f);
            colors.pressedColor = new Color(0.08f, 0.15f, 0.16f, 1f);
            colors.disabledColor = colors.normalColor;
            button.colors = colors;

            var text = CreateLabel(buttonObject.transform, $"{choice.Name}\n{choice.Description}", 17f, FontStyles.Normal, 0f, TextAlignmentOptions.Left);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 8f);
            textRect.offsetMax = new Vector2(-18f, -8f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;

            string mutationId = choice.Id;
            button.onClick.AddListener(() => _runtimeManager?.SelectApexEvolutionMutation(mutationId));
        }

        private static TextMeshProUGUI CreateLabel(
            Transform parent,
            string value,
            float fontSize,
            FontStyles fontStyle,
            float preferredHeight,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = new Color(0.91f, 0.95f, 0.92f, 1f);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;

            if (preferredHeight > 0f)
                textObject.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
            return text;
        }

        private static string BuildSignature(IReadOnlyList<CardMutationDefinition> choices)
        {
            var ids = new List<string>(choices.Count);
            for (int i = 0; i < choices.Count; i++)
                ids.Add(choices[i].Id);
            return string.Join("|", ids);
        }

        private void DestroyPanel()
        {
            if (_panel != null)
                Destroy(_panel);
            _panel = null;
            _visibleChoiceSignature = null;
        }
    }
}
