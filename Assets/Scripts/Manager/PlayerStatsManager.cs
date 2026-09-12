using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Stats;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    public class PlayerStatsManager : MonoBehaviour
    {
        #region Singleton
        private static PlayerStatsManager _instance;
        /// <summary>Global access point.</summary>
        public static PlayerStatsManager Instance => _instance;
        public event Action OnStatsChanged;
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            ModifierManager.Instance.OnModifierChanged += RefreshStats;
        }
        #endregion

        private readonly PlayerStats _stats = new();

        public float GetStat(SkillType stat) =>
            ModifierManager.Instance.Calculate(stat, GetBaseStat(stat));

        public int GetStatInt(SkillType stat) => Mathf.RoundToInt(GetStat(stat));

        public void SetBaseStat(SkillType stat, float value) => _stats.SetBaseStat(stat, value);

        public float GetBaseStat(SkillType stat) => _stats.GetBaseStat(stat);

        public void RefreshStats() => OnStatsChanged?.Invoke();

        // Accumulator for fractional counts across pooled projectiles.
        // Persists across projectile pool resets because PlayerStatsManager
        // is a DontDestroyOnLoad singleton.
        private float _bounceFractionAccumulator = 0f;
        private float _multiShootFractionAccumulator = 0f;

        /// <summary>
        /// Returns an integer count for one approved action.
        /// The whole part is guaranteed, while the fractional part is
        /// accumulated across multiple actions.
        ///
        /// Example:
        /// BounceCount = 3.02
        /// → Normally returns 3
        /// → Every accumulated 0.02 eventually produces +1 extra bounce.
        ///
        /// MultiCount = 4.03
        /// → Normally returns 4
        /// → Every accumulated 0.03 eventually produces +1 extra multi-shot.
        ///
        /// For bounce, call only when bounce has been approved.
        /// For multi, call only when multiShoot chance has been true.
        /// </summary>
        public int GetAccumulatedCount(float rawCount, AccumulatedCountType type)
        {
            int wholeCount = Mathf.FloorToInt(rawCount);
            float fraction = rawCount - wholeCount;
            return type switch
            {
                AccumulatedCountType.Bounce =>
                    AccumulateFraction(wholeCount, fraction, ref _bounceFractionAccumulator),
                AccumulatedCountType.Multi =>
                    AccumulateFraction(wholeCount, fraction, ref _multiShootFractionAccumulator),
                _ => wholeCount,
            };
        }

        private int AccumulateFraction(int wholeCount, float fraction, ref float accumulator)
        {
            accumulator += fraction;
            int extraCount = Mathf.FloorToInt(accumulator);
            if (extraCount > 0)
            {
                accumulator -= extraCount;
                wholeCount += extraCount;
            }
            return wholeCount;
        }

        /// <summary>
        /// Applies a temporary percent modifier to a stat.
        /// Used for effects like enemy slow that temporarily modify move speed.
        /// </summary>
        public void ApplyTemporaryModifier(SkillType stat, float multiplierPercent, string sourceId, float durationSec = 1f)
        {
            if (multiplierPercent <= 0f) return;

            // multiplierPercent = current speed percent (e.g., 90 = 90% speed)
            // Percent modifier needed = multiplierPercent - 100  → 90 → -10
            // ModifierCalculator expects raw percent value (-10), not fraction (-0.1)
            float percentValue = multiplierPercent - 100f;

            var modifier = new StatModifier
            {
                Id = $"Temp_{sourceId}_{stat}",
                Source = ModifierSource.Buff,
                Stat = stat,
                Mode = ModifierMode.Percent,
                Value = percentValue,
                Permanent = false,
                ExpireTime = Time.time + durationSec   // Unity time
            };

            ModifierManager.Instance.AddModifier(modifier);
        }

        // Overload preserving original 1s default
        public void ApplyTemporaryModifier(SkillType stat, float multiplierPercent, string sourceId)
            => ApplyTemporaryModifier(stat, multiplierPercent, sourceId, 1f);

        /// <summary>
        /// Applies a temporary percent modifier to multiple stats simultaneously.
        /// Used for effects like enemy slow that affect both move speed and attack speed.
        /// </summary>
        public void ApplyTemporaryModifier(IEnumerable<SkillType> stats, float multiplierPercent, string sourceId, float durationSec = 1f)
        {
            if (multiplierPercent <= 0f) return;
            if (stats == null) return;

            float percentValue = multiplierPercent - 100f;

            foreach (var stat in stats)
            {
                var modifier = new StatModifier
                {
                    Id = $"Temp_{sourceId}_{stat}",
                    Source = ModifierSource.Buff,
                    Stat = stat,
                    Mode = ModifierMode.Percent,
                    Value = percentValue,
                    Permanent = false,
                    ExpireTime = Time.time + durationSec
                };

                ModifierManager.Instance.AddModifier(modifier);
            }
        }

        /// <summary>
        /// Applies a temporary percent modifier to multiple stats (params overload for convenience).
        /// </summary>
        public void ApplyTemporaryModifier(float multiplierPercent, string sourceId, float durationSec, params SkillType[] stats)
            => ApplyTemporaryModifier(stats, multiplierPercent, sourceId, durationSec);

        /// <summary>
        /// Applies a temporary percent modifier to multiple stats (1s default duration).
        /// </summary>
        public void ApplyTemporaryModifier(float multiplierPercent, string sourceId, params SkillType[] stats)
            => ApplyTemporaryModifier(stats, multiplierPercent, sourceId, 1f);
    }

    public enum AccumulatedCountType { Bounce, Multi }
}
