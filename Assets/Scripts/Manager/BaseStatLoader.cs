using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Stats;
using Newtonsoft.Json;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Loads player skill base values and progression from dataPlayer.json into PlayerStatsManager.
    /// Single source of truth for all stat base values and progression (ValuePerLevel, ValuePerEnhance).
    /// </summary>
    public class BaseStatLoader : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton Pattern
        // -------------------------------------------------------------------
        private static BaseStatLoader _instance;
        public static BaseStatLoader Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        // Cached progression data for secondary stats
        private readonly Dictionary<SkillType, SkillData> _skillDataCache = new();
        private readonly Dictionary<SecondaryStat, SkillData> _secondaryStatDataCache = new();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadBaseStats();
        }

        /// <summary>
        /// Reload base skill values and progression from dataPlayer.json.
        /// Called on init and after save load.
        /// </summary>
        public void LoadBaseStats()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/Player/dataPlayer");
            if (jsonAsset == null)
            {
                Debug.LogError("[BaseStatLoader] dataPlayer.json not found!");
                return;
            }

            PlayerData playerData = JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
            LoadAllStats(playerData);
        }

        public void LoadAllStats(PlayerData playerData)
        {
            if (playerData?.skills == null) return;

            _skillDataCache.Clear();
            _secondaryStatDataCache.Clear();

            foreach (var group in playerData.skills.GetType().GetFields())
                ProcessGroup(group.GetValue(playerData.skills));

            PlayerStatsManager.Instance.RefreshStats();
        }

        private void ProcessGroup(object group)
        {
            if (group == null) return;

            foreach (var field in group.GetType().GetFields())
            {
                if (field.FieldType != typeof(SkillData)) continue;

                var skill = (SkillData)field.GetValue(group);
                if (skill == null) continue;

                // Try to map to SkillType (primary stats)
                if (TryGetStatType(field.Name, out var statType))
                {
                    _skillDataCache[statType] = skill;
                    PlayerStatsManager.Instance.SetBaseStat(statType, skill.baseValue);
                }

                // Also try to map to SecondaryStat (secondary/specialization stats)
                if (Enum.TryParse<SecondaryStat>(ToPascalCase(field.Name), true, out var secStat)
                    && secStat != SecondaryStat.None)
                {
                    _secondaryStatDataCache[secStat] = skill;
                }
            }
        }

        /// <summary>
        /// Gets the SkillData for a primary stat type.
        /// </summary>
        public SkillData GetSkillData(SkillType statType)
        {
            return _skillDataCache.TryGetValue(statType, out var skill) ? skill : null;
        }

        /// <summary>
        /// Gets the SkillData for a secondary stat.
        /// </summary>
        public SkillData GetSecondarySkillData(SecondaryStat stat)
        {
            return _secondaryStatDataCache.TryGetValue(stat, out var skill) ? skill : null;
        }

        /// <summary>
        /// Gets ValuePerLevel for a secondary stat.
        /// </summary>
        public float GetSecondaryValuePerLevel(SecondaryStat stat)
        {
            return GetSecondarySkillData(stat)?.ValuePerLevel ?? 0f;
        }

        /// <summary>
        /// Gets ValuePerEnhance for a secondary stat.
        /// </summary>
        public float GetSecondaryValuePerEnhance(SecondaryStat stat)
        {
            return GetSecondarySkillData(stat)?.ValuePerEnhance ?? 0f;
        }

        private bool TryGetStatType(string skillName, out SkillType statType)
        {
            statType = default;
            if (string.IsNullOrWhiteSpace(skillName)) return false;

            string enumName = char.ToUpperInvariant(skillName[0]) + skillName[1..];
            return Enum.TryParse(enumName, out statType);
        }

        private static string ToPascalCase(string camelCase)
        {
            if (string.IsNullOrEmpty(camelCase)) return camelCase;
            return char.ToUpperInvariant(camelCase[0]) + camelCase.Substring(1);
        }
    }
}
