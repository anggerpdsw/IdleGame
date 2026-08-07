using System;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Reward
{
    public class DailyRewardSlot : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _state;

        public Image Background => _background;

        private int _slotIndex;

        public void Initialize(int slotIndex)
        {
            _slotIndex = slotIndex;

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnButtonClicked);
            }
        }

        public void Refresh(DailyRewardData reward, DailyRewardState state, int index, Sprite icon, TimeSpan? remainingTime)
        {
            if (_label != null)
                _label.text = GetSlotText(reward, state, index);

            if (_background != null)
                _background.color = GetSlotColor(state);

            if (_icon != null)
                _icon.sprite = icon;

            if (_button != null)
            {
                bool isCurrent = index == _slotIndex;
                _button.interactable = isCurrent && state == DailyRewardState.Claimable;
                string color = state switch
                {
                    DailyRewardState.Claimable when isCurrent => "Green",
                    DailyRewardState.Waiting when isCurrent => "Yellow",
                    DailyRewardState.Locked => "Red",
                    _ => "Grey"
                };
                _button.image.sprite = ButtonResources.GetColor(color);
            }

            if (_state != null)
            {
                _state.text = state switch
                {
                    DailyRewardState.Locked => "Locked",
                    DailyRewardState.Waiting when remainingTime.HasValue
                        => Utilityku.FormatDuration(remainingTime.Value),
                    DailyRewardState.Claimable => "Claim",
                    DailyRewardState.Claimed => "Claimed",
                    DailyRewardState.CompletedToday => "Completed",
                    _ => string.Empty
                };
            }
        }

        private void OnButtonClicked()
        {
            DailyRewardManager.Instance.TryClaimCurrentReward(DateTime.UtcNow);
        }

        private static string GetSlotText(DailyRewardData reward, DailyRewardState state, int index)
        {
            string prefix = state == DailyRewardState.Claimed ? "✓"
                : state == DailyRewardState.Claimable ? "▶"
                : "•";

            string rewardText = reward.Type switch
            {
                RewardType.Gold => $"{Utilityku.FormatNumber(reward.Amount)} Gold",
                RewardType.Gem => $"{Utilityku.FormatNumber(reward.Amount)} Gems",
                RewardType.Meat => $"{Utilityku.FormatNumber(reward.Amount)} Meat",
                RewardType.Exp => $"{Utilityku.FormatNumber(reward.Amount)} EXP",
                RewardType.Item => GetItemDisplayName(reward),
                _ => reward.Id
            };

            return $"{prefix} Reward {index + 1}: {rewardText}";
        }

        private static string GetItemDisplayName(DailyRewardData reward)
        {
            return reward.Id switch
            {
                "CardRoll" => "Free Card Roll",
                "UltimateStone" => "Ultimate Stone",
                "SkinShard" => "Skin Shard",
                _ => reward.Id
            };
        }

        private static Color GetSlotColor(DailyRewardState state)
        {
            return state switch
            {
                DailyRewardState.Claimable => GameColors.dailyClaimableGreen.WithAlpha(0.95f),
                DailyRewardState.Claimed => GameColors.dailyClaimedGreen.WithAlpha(0.95f),
                _ => GameColors.dailyDefault.WithAlpha(0.95f)
            };
        }
    }
}