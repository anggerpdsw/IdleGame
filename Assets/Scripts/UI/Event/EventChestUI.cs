using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Events;

namespace IdleDefenseSurvival.UI.Event
{
    /// <summary>
    /// Event Chest UI with pity display.
    /// </summary>
    public class EventChestUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _chestRoot;
        [SerializeField] private Button _rollButton;
        [SerializeField] private TextMeshProUGUI _pityText;
        [SerializeField] private GameObject _rewardPanel;
        [SerializeField] private TextMeshProUGUI _rewardText;

        private void Awake()
        {
            if (_chestRoot != null) _chestRoot.SetActive(false);
            if (_rollButton != null) _rollButton.onClick.AddListener(OnRollClicked);
            if (_rewardPanel != null) _rewardPanel.SetActive(false);
        }

        public void Show()
        {
            if (_chestRoot != null) _chestRoot.SetActive(true);
            UpdatePityDisplay();
        }

        public void Hide()
        {
            if (_chestRoot != null) _chestRoot.SetActive(false);
        }

        private void OnRollClicked()
        {
            var reward = EventChestService.RollChest();
            if (reward != null)
            {
                ShowReward(reward);
                UpdatePityDisplay();
            }
        }

        private void UpdatePityDisplay()
        {
            if (_pityText == null) return;

            int pity = EventChestService.GetPityCount();
            _pityText.text = $"Pity: {pity}/20";
        }

        private void ShowReward(ChestReward reward)
        {
            if (_rewardPanel != null) _rewardPanel.SetActive(true);
            if (_rewardText != null)
            {
                string rewardString = reward.type == "EventRelic"
                    ? $"{reward.itemId} x{reward.amount}"
                    : $"{reward.type} x{reward.amount}";
                _rewardText.text = $"Reward: {rewardString}";
            }

            // Auto-hide after 3 seconds
            Invoke(nameof(HideRewardPanel), 3f);
        }

        private void HideRewardPanel()
        {
            if (_rewardPanel != null) _rewardPanel.SetActive(false);
        }
    }
}
