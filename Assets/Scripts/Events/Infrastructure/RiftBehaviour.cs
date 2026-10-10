using UnityEngine;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.UI.Event;

namespace IdleDefenseSurvival.Events.Infrastructure
{
    /// <summary>
    /// Rift collision handler.
    /// Shows choice UI when player touches, destroys after choice made.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RiftBehaviour : MonoBehaviour
    {
        private bool _triggered = false;

        public void Initialize()
        {
            if (TryGetComponent<Collider2D>(out var collider))
            {
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;

            if (!other.CompareTag("Player") && other.GetComponent<Player.Player>() == null)
                return;

            _triggered = true;
            ShowChoiceUI();
        }

        private void ShowChoiceUI()
        {
            var choiceUI = FindFirstObjectByType<EventChoiceUI>();
            if (choiceUI != null)
            {
                choiceUI.Show();

                var eventService = ServiceLocator.EventService;
                if (eventService != null)
                {
                    void onChoiceMade(string choiceId)
                    {
                        eventService.OnChoiceMade -= onChoiceMade;
                        Destroy(gameObject);
                    }
                    eventService.OnChoiceMade += onChoiceMade;
                }
            }
            else
            {
                Debug.LogWarning("[RiftBehaviour] EventChoiceUI not found!");
                Destroy(gameObject);
            }
        }
    }
}
