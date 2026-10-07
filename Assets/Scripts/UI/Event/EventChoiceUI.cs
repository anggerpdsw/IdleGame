using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Events;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Choice dialog (Seal / Harvest / Feed).
    /// Displays threat delta and reward multiplier per choice.
    /// </summary>
    public class EventChoiceUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _dialogRoot;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button[] _choiceButtons = new Button[3];
        [SerializeField] private TextMeshProUGUI[] _choiceLabels = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] _choiceDescriptions = new TextMeshProUGUI[3];

        private IEventService _eventService;
        private EventDefinition _currentEvent;

        private void Awake()
        {
            _eventService = ServiceLocator.EventService;
            if (_dialogRoot != null) _dialogRoot.SetActive(false);

            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                int index = i;
                if (_choiceButtons[i] != null)
                    _choiceButtons[i].onClick.AddListener(() => OnChoiceClicked(index));
            }
        }

        public void Show()
        {
            if (_eventService == null) return;

            _currentEvent = _eventService.GetActiveEvent();
            if (_currentEvent == null) return;

            if (_dialogRoot != null) _dialogRoot.SetActive(true);
            if (_titleText != null) _titleText.text = "Rift Detected - Choose Your Path";

            PopulateChoices();
        }

        public void Hide()
        {
            if (_dialogRoot != null) _dialogRoot.SetActive(false);
        }

        private void PopulateChoices()
        {
            if (_currentEvent?.choices == null) return;

            for (int i = 0; i < Mathf.Min(_currentEvent.choices.Length, _choiceButtons.Length); i++)
            {
                var choice = _currentEvent.choices[i];
                if (_choiceLabels[i] != null)
                    _choiceLabels[i].text = choice.displayName;

                if (_choiceDescriptions[i] != null)
                {
                    string threatText = choice.threatDelta > 0 ? $"+{choice.threatDelta}" : choice.threatDelta.ToString();
                    string rewardText = $"{choice.rewardMultiplier:P0}";
                    _choiceDescriptions[i].text = $"Threat: {threatText} | Rewards: {rewardText}";
                }
            }
        }

        private void OnChoiceClicked(int index)
        {
            if (_currentEvent == null || _currentEvent.choices == null) return;
            if (index < 0 || index >= _currentEvent.choices.Length) return;

            var choice = _currentEvent.choices[index];
            bool success = _eventService.MakeChoice(choice.choiceId);

            if (success)
            {
                Hide();
            }
        }
    }
}
