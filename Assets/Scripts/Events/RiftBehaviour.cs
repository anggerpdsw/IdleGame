using UnityEngine;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.UI.Event;

namespace IdleDefenseSurvival.Events
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
            // Setup collider as trigger
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;

            // Check player collision
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

                // Subscribe to choice made event to destroy rift
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
