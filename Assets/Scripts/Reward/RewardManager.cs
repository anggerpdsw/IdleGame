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

        private void OnDestroy()
        {
            if (_popupInstance != null) Destroy(_popupInstance.gameObject);
            _popupInstance = null;
            _onClaim = null;
        }
        
        /// <summary>
        /// Show standard rewards (gold, gems, items, etc.)
        /// </summary>
        public void Show(List<RewardData> rewards, Action onClaimFinished = null)
        {
            _onClaim = onClaimFinished;
            _pendingRewards.Clear();
            _pendingRewards.AddRange(rewards);

            GetOrCreatePopup();

            _popupInstance.Show(rewards, ClaimRewards);
        }

        private RewardPopup GetOrCreatePopup()
        {
            // Destroy stale instance (parent changed across scene)
            if (_popupInstance != null)
            {
                if (_popupInstance.transform.parent != UIManager.Instance.PopupRoot)
                    Destroy(_popupInstance.gameObject);
                else
                    return _popupInstance;
            }

            _popupInstance = Instantiate(_popupPrefab, UIManager.Instance.PopupRoot, false);
            _popupInstance.transform.localScale = Vector3.one;
            _popupInstance.transform.localPosition = Vector3.zero;
            _popupInstance.transform.localRotation = Quaternion.identity;
            return _popupInstance;
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

            GetOrCreatePopup();

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
            AccountManager.Instance.AddExp(expReward, LevelType.Level, $"From kill {enemy}");
        }


    }
}