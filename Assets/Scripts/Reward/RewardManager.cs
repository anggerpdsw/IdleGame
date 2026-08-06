using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Reward;
using System;

namespace IdleDefenseSurvival.Manager
{
    public class RewardManager : MonoBehaviour
    {
        public static RewardManager Instance { get; private set; }

        [SerializeField] private RewardPopup _popupPrefab;
        private RewardPopup _popupInstance;
        private Action _onClaim;
        private readonly List<RewardData> _pendingRewards = new();
        private const string PopupPath = "Reward/PanelReward";

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _popupPrefab = Resources.Load<RewardPopup>(PopupPath);
        }

        /// <summary>
        /// Show standard rewards (gold, gems, items, etc.)
        /// </summary>
        public void Show(List<RewardData> rewards, Action onClaimFinished = null)
        {
            _onClaim = onClaimFinished;
            _pendingRewards.Clear();
            _pendingRewards.AddRange(rewards);

            if (_popupInstance == null)
                _popupInstance = Instantiate(_popupPrefab, UIManager.Instance.PopupRoot, false);

            _popupInstance.Show(rewards, ClaimRewards);
        }

        /// <summary>
        /// Show card roll results with special animations for duplicates, upgrades, and pity cards
        /// </summary>
        public void Show(CardRollResult result, Action onClaimFinished = null)
        {
            _onClaim = onClaimFinished;

            // Convert CardRollResult to RewardData list for the popup
            var rewards = new List<RewardData>();

            foreach (var cardReward in result.Cards)
            {
                var cardData = CardDatabase.Instance.GetCard(cardReward.CardId);
                if (cardData == null) continue;

                var reward = new RewardData
                {
                    Type = RewardType.Item,
                    Id = cardReward.CardId,
                    Amount = cardReward.Quantity
                };

                rewards.Add(reward);
            }

            if (_popupInstance == null)
                _popupInstance = Instantiate(_popupPrefab, UIManager.Instance.PopupRoot, false);

            _popupInstance.ShowCardRollResult(result, rewards, ClaimRewards);
        }

        private void ClaimRewards()
        {
            foreach (RewardData reward in _pendingRewards)
            {
                switch (reward.Type)
                {
                    case RewardType.Gold:
                        EconomyManager.Instance.AddCurrency(CurrencyType.Gold, reward.Amount, "Reward");
                        break;

                    case RewardType.Gem:
                        EconomyManager.Instance.AddCurrency(CurrencyType.Gem, reward.Amount, "Reward");
                        break;

                    case RewardType.Meat:
                        EconomyManager.Instance.AddCurrency(CurrencyType.Meat, reward.Amount, "Reward");
                        break;

                    case RewardType.Item:
                        // InventoryManager.Instance.Add(...)
                        break;

                    case RewardType.Equipment:
                        break;

                    case RewardType.Hero:
                        break;
                }
            }

            _pendingRewards.Clear();
            _popupInstance.PlayHideAnimation(() =>
            {
                _onClaim?.Invoke();
                _onClaim = null;
            });
        }

        public void GiveEnemyReward(long goldReward, int expReward, string enemy)
        {
            if (goldReward > 0) {
                WaveManager.Instance.RecordGold(goldReward);
                EconomyManager.Instance.AddCurrency(CurrencyType.Gold, goldReward, $"From kill {enemy}");
            }

            // Add permanent account EXP reward
            if (expReward > 0) WaveManager.Instance.RecordExp(expReward);
            AccountManager.Instance.AddExp(expReward, $"From kill {enemy}");
        }
    }
}