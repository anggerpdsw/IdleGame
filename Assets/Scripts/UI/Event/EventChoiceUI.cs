using System.Collections.Generic;
using UnityEngine;
using TMPro;
using IdleDefenseSurvival.Events;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Dynamic choice dialog. Instantiates EventChoiceItemUI based on event definition.
    /// Scalable: supports 2-10+ choices without code changes.
    /// </summary>
    public sealed class EventChoiceUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _dialogRoot;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Transform _choicesContainer;
        [SerializeField] private EventChoiceItemUI _choicePrefab;

        private readonly List<EventChoiceItemUI> _activeItems = new();
        private IEventService _eventService;
        private EventDefinition _currentEvent;

        private void Awake()
        {
            _eventService = ServiceLocator.EventService;
            if (_dialogRoot != null)
                _dialogRoot.SetActive(false);
        }

        public void Show()
        {
            _eventService ??= ServiceLocator.EventService;
            if (_eventService == null) return;

            _currentEvent = _eventService.GetActiveEvent();
            if (_currentEvent?.choices == null) return;

            ClearChoices();

            if (_dialogRoot != null) _dialogRoot.SetActive(true);
            if (_titleText != null) _titleText.text = "Rift Detected - Choose Your Path";

            foreach (var choice in _currentEvent.choices)
            {
                if (choice == null) continue;

                var item = Instantiate(_choicePrefab, _choicesContainer);
                item.Initialize(choice, OnChoiceSelected);
                _activeItems.Add(item);
            }
        }

        public void Hide()
        {
            if (_dialogRoot != null) _dialogRoot.SetActive(false);
        }

        private void OnChoiceSelected(EventChoice choice)
        {
            if (_currentEvent == null || choice == null) return;

            if (_eventService.MakeChoice(choice.choiceId))
                Hide();
        }

        private void ClearChoices()
        {
            foreach (var item in _activeItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _activeItems.Clear();
        }

        private void OnDestroy()
        {
            ClearChoices();
        }
    }
}
