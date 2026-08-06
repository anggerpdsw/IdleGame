using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Manager
{
    public class DailyRewardService
    {
        private DailyRewardSaveData _saveData = new();
        private readonly DailyRewardProvider _rewardProvider = new();

        public DailyRewardProvider RewardProvider => _rewardProvider;

        private static readonly string[] UltimateStoneVariants =
        {
            "UltimateStone_None",
            "UltimateStone_Metal",
            "UltimateStone_Wood",
            "UltimateStone_Fire",
            "UltimateStone_Water",
            "UltimateStone_Earth",
            "UltimateStone_Lightning",
            "UltimateStone_Wind"
        };

        public int CurrentRewardIndex => Math.Clamp(_saveData.currentRewardIndex, 0, GameConstants.REWARD_COUNT);
        public bool IsCompletedToday => _saveData.completedToday;
        public DateTime? NextUnlockUtc => _saveData.nextUnlockUtcTicks > 0 ? new DateTime(_saveData.nextUnlockUtcTicks, DateTimeKind.Utc) : null;
        public DateTime? LastUnlockUtc { get; private set; }
        public int ClaimedToday => _saveData.claimedToday;
        public string LastResetDate => _saveData.lastResetDate;

        public DailyRewardState GetState(DateTime utcNow)
        {
            if (_saveData.completedToday || _saveData.currentRewardIndex >= GameConstants.REWARD_COUNT)
                return DailyRewardState.CompletedToday;

            if (!NextUnlockUtc.HasValue)
                return DailyRewardState.Claimable;

            bool isVIP = SaveManager.Instance.IsDailyEnabled();
            return utcNow >= NextUnlockUtc.Value
                ? DailyRewardState.Claimable
                : (isVIP ? DailyRewardState.Claimable : DailyRewardState.Waiting);
        }

        public DailyRewardState GetSlotState(int slotIndex, DateTime utcNow)
        {
            var state = GetState(utcNow);

            if (slotIndex < _saveData.currentRewardIndex)
                return DailyRewardState.Claimed;

            if (slotIndex == _saveData.currentRewardIndex)
            {
                return state == DailyRewardState.CompletedToday
                    ? DailyRewardState.Claimed
                    : state;
            }

            return DailyRewardState.Locked;
        }

        public TimeSpan? GetRemainingTime(DateTime utcNow)
        {
            if (!NextUnlockUtc.HasValue) return null;
            if (utcNow >= NextUnlockUtc.Value) return TimeSpan.Zero;
            return NextUnlockUtc.Value - utcNow;
        }

        public void Initialize(DateTime utcNow, DailyRewardSaveData existingData)
        {
            if (existingData != null)
                _saveData = existingData.Clone();

            EnsureReset(utcNow);
            EnsureStateValidity();
        }

        public void EnsureReset(DateTime utcNow)
        {
            string today = utcNow.ToString(GameConstants.DATE_FORMAT);
            if (string.IsNullOrEmpty(_saveData.lastResetDate))
            {
                // First login
                _saveData.lastResetDate = today;
            }
            else if (_saveData.lastResetDate != today)
            {
                // New day - reset progress
                _saveData.currentRewardIndex = 0;
                _saveData.completedToday = false;
                _saveData.nextUnlockUtcTicks = 0;
                _saveData.lastResetDate = today;
                _saveData.claimedToday = 0;
                LastUnlockUtc = null;
            }
            // Same day - do nothing
        }

        public bool HasClaimableReward => CanClaimCurrentReward(DateTime.UtcNow);
        private bool CanClaimCurrentReward(DateTime utcNow)
        { 
            return GetState(utcNow) == DailyRewardState.Claimable;
        }
        
        public bool ClaimCurrentReward(DateTime utcNow)
        {
            if (!CanClaimCurrentReward(utcNow)) return false;

            var reward = _rewardProvider.GetReward(_saveData.currentRewardIndex);
            if (!ApplyReward(reward)) return false;

            _saveData.claimedToday++;
            _saveData.currentRewardIndex++;
            LastUnlockUtc = utcNow;

            if (_saveData.currentRewardIndex >= GameConstants.REWARD_COUNT)
            {
                _saveData.completedToday = true;
                _saveData.nextUnlockUtcTicks = 0;
            }
            else
            {
                _saveData.nextUnlockUtcTicks = utcNow.AddMinutes(GameConstants.COOLDOWN_MINUTES).ToUniversalTime().Ticks;
            }

            return true;
        }

        public DailyRewardSaveData SaveState() => _saveData.Clone();

        public void SetSaveData(DailyRewardSaveData data)
        {
            _saveData = (data ?? new DailyRewardSaveData()).Clone();
            EnsureStateValidity();
        }

        private void EnsureStateValidity()
        {
            if (_saveData.currentRewardIndex < 0)
                _saveData.currentRewardIndex = 0;
            if (_saveData.currentRewardIndex >= GameConstants.REWARD_COUNT)
            {
                _saveData.currentRewardIndex = GameConstants.REWARD_COUNT;
                _saveData.completedToday = true;
                _saveData.nextUnlockUtcTicks = 0;
                return;
            }
            if (_saveData.completedToday) return;
            if (_saveData.currentRewardIndex == 0 && _saveData.nextUnlockUtcTicks == 0) return;
        }

        private const string RewardSource = "Daily reward";
        private bool ApplyReward(DailyRewardData reward)
        {
            switch (reward.Type)
            {
                case RewardType.Gold:
                    EconomyManager.Instance?.AddCurrency(CurrencyType.Gold, reward.Amount, RewardSource);
                    break;
                case RewardType.Gem:
                    EconomyManager.Instance?.AddCurrency(CurrencyType.Gem, reward.Amount, RewardSource);
                    break;
                case RewardType.Meat:
                    EconomyManager.Instance?.AddCurrency(CurrencyType.Meat, reward.Amount, RewardSource);
                    break;
                case RewardType.Exp:
                    AccountManager.Instance?.AddExp(reward.Amount, RewardSource);
                    break;
                case RewardType.Item:
                    if (reward.Id == "UltimateStone")
                    {
                        ApplyUltimateStoneReward((int)reward.Amount);
                    }
                    else
                    {
                        var inventory = InventoryManager.Instance;
                        inventory?.AddItem(reward.Id, reward.Amount);
                    }
                    break;
                default:
                    return false;
            }

            return true;
        }

        private void ApplyUltimateStoneReward(int count)
        {
            if (count <= 0) return;
            if (InventoryManager.Instance == null) return;

            var inventory = InventoryManager.Instance;
            if (inventory == null) return;
            for (int i = 0; i < count; i++)
            {
                var variant = UltimateStoneVariants[UnityEngine.Random.Range(0, UltimateStoneVariants.Length)];
                inventory.AddItem(variant, 1);
            }
        }
    }

}
