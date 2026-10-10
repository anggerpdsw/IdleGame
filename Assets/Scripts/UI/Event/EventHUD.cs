using UnityEngine;
using TMPro;
using IdleDefenseSurvival.Events;
using IdleDefenseSurvival.Events.Domain;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Top HUD for active event (threat meter, timer, score).
    /// </summary>
    public class EventHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _hudRoot;
        [SerializeField] private EventThreatBar _threatBar;
        [SerializeField] private TextMeshProUGUI _eventNameText;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _currencyText;

        private IEventService _eventService;

        private void Awake()
        {
            if (_hudRoot != null) _hudRoot.SetActive(false);
        }

        private void Start()
        {
            _eventService = ServiceLocator.EventService;

            // Sync awal jika event sudah aktif sebelum HUD ready
            if (_eventService != null && _eventService.IsEventActive())
            {
                if (_hudRoot != null) _hudRoot.SetActive(true);
                UpdateThreat(_eventService.CurrentThreat);
            }
        }

        private void OnEnable()
        {
            _eventService ??= ServiceLocator.EventService;
            if (_eventService == null) return;

            _eventService.OnEventStarted += HandleEventStarted;
            _eventService.OnEventEnded += HandleEventEnded;
            _eventService.OnThreatChanged += HandleThreatChanged;
        }

        private void OnDisable()
        {
            if (_eventService == null) return;
            _eventService.OnEventStarted -= HandleEventStarted;
            _eventService.OnEventEnded -= HandleEventEnded;
            _eventService.OnThreatChanged -= HandleThreatChanged;
        }

        private void Update()
        {
            if (_eventService == null || !_eventService.IsEventActive()) return;

            var evt = _eventService.GetActiveEvent();
            if (evt == null) return;

            UpdateTimer(evt);
            UpdateScore();
        }

        private void HandleEventStarted(string eventId)
        {
            var evt = _eventService.GetActiveEvent();
            if (evt == null) return;

            if (_hudRoot != null) _hudRoot.SetActive(true);
            if (_eventNameText != null) _eventNameText.text = evt.displayName;

            UpdateThreat(_eventService.CurrentThreat);
        }

        private void HandleEventEnded(string eventId, bool success)
        {
            if (_hudRoot != null) _hudRoot.SetActive(false);
        }

        private void HandleThreatChanged(int oldThreat, int newThreat)
        {
            UpdateThreat(newThreat);
        }

        private void UpdateThreat(int threat)
        {
            var evt = _eventService?.GetActiveEvent();
            int max = evt?.threat?.max ?? 100; // Fallback 100
            if (_threatBar != null) _threatBar.SetThreat(threat, max);
        }

        private void UpdateTimer(EventDefinition evt)
        {
            if (_timerText == null) return;

            var runtimeState = ServiceLocator.EventService as EventService;
            if (runtimeState == null) return;

            // Calculate remaining time
            var saveData = runtimeState.GetSaveData();
            if (saveData == null || saveData.eventEndsAt == 0)
            {
                _timerText.text = "--:--:--";
                return;
            }

            var endTime = new System.DateTime(saveData.eventEndsAt);
            var remaining = endTime - System.DateTime.UtcNow;

            if (remaining.TotalSeconds <= 0)
            {
                _timerText.text = "00:00:00";
                return;
            }

            _timerText.text = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        private void UpdateScore()
        {
            if (_scoreText != null)
                _scoreText.text = $"Score: {_eventService.EventScore}";

            if (_currencyText != null)
            {
                var saveData = (_eventService as EventService)?.GetSaveData();
                if (saveData != null)
                    _currencyText.text = $"{saveData.eventCurrency}";
            }
        }
    }
}
