using System;
using IdleDefenseSurvival.Player;
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

        public float GetStat(SkillType stat)
        {
            return ModifierManager.Instance.ApplyModifiers(stat, GetBaseStat(stat));
        }

        public int GetStatInt(SkillType stat) => Mathf.RoundToInt(GetStat(stat));

        public void SetBaseStat(SkillType stat, float value) => _stats.SetBaseStat(stat, value);

        public float GetBaseStat(SkillType stat) => _stats.GetBaseStat(stat);

        public void RefreshStats() => OnStatsChanged?.Invoke();

    }
}
