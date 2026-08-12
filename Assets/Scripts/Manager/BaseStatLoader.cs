using System;
using IdleDefenseSurvival.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Loads player skill base values from dataPlayer.json into PlayerStatsManager.
    /// No levels, no upgrades — skills are static values from JSON.
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
        /// Reload base skill values from dataPlayer.json and push them into PlayerStatsManager.
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
            LoadBaseStats(playerData);
        }

        public void LoadBaseStats(PlayerData playerData)
        {
            if (playerData?.skills == null) return;

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
                if (!TryGetStatType(field.Name, out var statType)) continue;

                var skill = (SkillData)field.GetValue(group);
                PlayerStatsManager.Instance.SetBaseStat(statType, skill.baseValue);
            }
        }

        private bool TryGetStatType(string skillName, out SkillType statType)
        {
            statType = default;
            if (string.IsNullOrWhiteSpace(skillName)) return false;

            string enumName = char.ToUpperInvariant(skillName[0]) + skillName[1..];
            return Enum.TryParse(enumName, out statType);
        }
    }
}
