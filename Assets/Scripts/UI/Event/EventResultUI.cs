using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Events;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// End-of-event summary (rewards, score, pet unlock).
    /// </summary>
    public class EventResultUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _resultRoot;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _rewardsText;
        [SerializeField] private Button _closeButton;

        [Header("Pet Unlock")]
        [SerializeField] private GameObject _petUnlockPanel;
        [SerializeField] private TextMeshProUGUI _petNameText;

        private void Awake()
        {
            if (_resultRoot != null) _resultRoot.SetActive(false);
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        public void Show(EventDefinition evt, long finalScore, bool success)
        {
            if (_resultRoot != null) _resultRoot.SetActive(true);

            if (_titleText != null)
                _titleText.text = success ? "Event Complete!" : "Event Collapsed...";

            if (_scoreText != null)
                _scoreText.text = $"Final Score: {finalScore}";

            if (_rewardsText != null)
            {
                string rewardsSummary = "Rewards:\n";
                if (evt?.rewards?.relics != null)
                {
                    foreach (var relic in evt.rewards.relics)
                    {
                        rewardsSummary += $"• {relic}\n";
                    }
                }
                _rewardsText.text = rewardsSummary;
            }

            // Show pet unlock if success
            if (_petUnlockPanel != null)
                _petUnlockPanel.SetActive(success && evt?.pet != null);

            if (success && evt?.pet != null && _petNameText != null)
                _petNameText.text = $"Unlocked: {evt.pet.petId}";
        }

        public void Hide()
        {
            if (_resultRoot != null) _resultRoot.SetActive(false);
        }
    }
}
