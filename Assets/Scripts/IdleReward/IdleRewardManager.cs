using System;
using IdleDefenseSurvival.Data;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    public class IdleRewardManager : MonoBehaviour
    {
        private static IdleRewardManager _instance;
        public static IdleRewardManager Instance => _instance;

        private IdleRewardData Data => SaveManager.Instance.GetIdleRewardData();

        public float Progress => GetProgress();
        public bool CanClaim => IsClaimAvailable();
        public long GoldReward => CalculateGoldReward();
        public long MeatReward => CalculateMeatReward();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public float GetProgress()
        {
            return (float)(GetAccumulatedSeconds() / Data.minimumClaimSeconds);
        }

        public bool IsClaimAvailable()
        {
            return GetAccumulatedSeconds() >= Data.minimumClaimSeconds;
        }

        public double GetAccumulatedSeconds()
        {
            var data = Data;
            DateTime lastClaim = new(data.lastClaimUtcTicks, DateTimeKind.Utc);
            double seconds = (DateTime.UtcNow - lastClaim).TotalSeconds;
            return Math.Min(seconds, data.maxDurationSeconds);
        }

        public long CalculateGoldReward()
        {
            double minute = GetAccumulatedSeconds() / 60.0;

            int highestTier = SaveManager.Instance.GetHighestUnlockedTier();
            int currentWave = SaveManager.Instance.GetHighestWave(highestTier);

            // Total progress wave sepanjang perjalanan player
            int totalWaveProgress =
                ((highestTier - 1) * GameConstants.MAX_WAVE_PER_TIER) + currentWave;
            
            double waveMultiplier = 1.0 + ((double)totalWaveProgress / GameConstants.MAX_WAVE_PER_TIER);
            double tierMultiplier = Mathf.Pow(1.35f, highestTier - 1);
            double goldPerMinute = 15.0 * tierMultiplier * waveMultiplier;

            return (long)Math.Round(goldPerMinute * minute * Data.rewardMultiplier);
        }

        public long CalculateMeatReward()
        {
            return Mathf.RoundToInt(CalculateGoldReward() / 30f);
        }

        public void ResetCount()
        {
            if (!CanClaim) return;

            var data = Data;
            data.lastClaimUtcTicks = DateTime.UtcNow.Ticks;

            SaveManager.Instance.SetIdleRewardData(data);
            SaveManager.Instance.SaveAll();
        }
    }
}