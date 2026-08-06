using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Player
{
    /// <summary>
    /// Reads skill base values and metadata from dataPlayer.json.
    /// Skills have no levels — static values, later influenced by main stats.
    /// </summary>
    public static class SkillLoader
    {
        private static Dictionary<string, SkillData> _skills;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _skills = new Dictionary<string, SkillData>();

            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataPlayer");
            if (jsonAsset == null)
            {
                Debug.LogWarning("[SkillLoader] dataPlayer.json not found.");
                _initialized = true;
                return;
            }

            try
            {
                PlayerData playerData = JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
                if (playerData?.skills == null) return;

                foreach (var groupField in playerData.skills.GetType().GetFields())
                {
                    var group = groupField.GetValue(playerData.skills);
                    if (group == null) continue;

                    foreach (var skillField in group.GetType().GetFields())
                    {
                        if (skillField.FieldType != typeof(SkillData)) continue;
                        _skills[skillField.Name] = (SkillData)skillField.GetValue(group);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SkillLoader] Failed to parse dataPlayer.json: {e.Message}");
            }

            _initialized = true;
        }

        public static float GetBaseValue(string skillId)
        {
            if (!_initialized) Initialize();
            return _skills.TryGetValue(skillId, out var skill) ? skill.baseValue : 0f;
        }

        public static string GetDisplayName(string skillId)
        {
            if (!_initialized) Initialize();
            return _skills.TryGetValue(skillId, out var skill)
                ? (string.IsNullOrEmpty(skill.displayName) ? skillId : skill.displayName)
                : skillId;
        }

        public static string GetDescription(string skillId)
        {
            if (!_initialized) Initialize();
            return _skills.TryGetValue(skillId, out var skill) ? skill.description : "";
        }
    }
}
