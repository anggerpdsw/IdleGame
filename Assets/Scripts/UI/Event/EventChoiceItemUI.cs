using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Events.Domain;

namespace IdleDefenseSurvival.UI.Event
{
    public sealed class EventChoiceItemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _description;

        private Action<EventChoice> _onSelected;
        private EventChoice _choice;

        public void Initialize(EventChoice choice, Action<EventChoice> onSelected)
        {
            _choice = choice;
            _onSelected = onSelected;

            _label.text = choice.displayName;

            string threatText = choice.threatDelta > 0
                ? $"+{choice.threatDelta}"
                : choice.threatDelta.ToString();

            string rewardText = $"{choice.rewardMultiplier:P0}";

            _description.text = $"Threat: {threatText} | Rewards: {rewardText}";

            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
            _button.interactable = true;
        }

        private void HandleClick() => _onSelected?.Invoke(_choice);

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);
        }
    }
}
