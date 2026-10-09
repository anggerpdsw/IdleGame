using UnityEngine;
using TMPro;
using DG.Tweening;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Events;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Incident alert popup banner.
    /// Fades in, displays for 3s, fades out.
    /// </summary>
    public class EventIncidentUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _bannerRoot;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _incidentText;

        [Header("Animation")]
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private float _displayDuration = 3f;

        private void Awake()
        {
            if (_bannerRoot != null) _bannerRoot.SetActive(false);
        }

        private void OnEnable()
        {
            var eventService = ServiceLocator.EventService as EventService;
            if (eventService != null)
            {
                eventService.OnIncidentTriggered += ShowIncident;
            }
        }

        private void OnDisable()
        {
            var eventService = ServiceLocator.EventService as EventService;
            if (eventService != null)
            {
                eventService.OnIncidentTriggered -= ShowIncident;
            }
        }

        public void ShowIncident(string message)
        {
            if (_bannerRoot != null) _bannerRoot.SetActive(true);
            if (_incidentText != null) _incidentText.text = message;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.DOFade(1f, _fadeDuration)
                    .OnComplete(() =>
                    {
                        DOVirtual.DelayedCall(_displayDuration, () =>
                        {
                            _canvasGroup.DOFade(0f, _fadeDuration)
                                .OnComplete(() =>
                                {
                                    if (_bannerRoot != null) _bannerRoot.SetActive(false);
                                });
                        });
                    });
            }
        }
    }
}
