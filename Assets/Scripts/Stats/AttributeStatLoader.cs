using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Stats
{
    /// <summary>
    /// Loads attribute progression values from JSON databases.
    /// Main attributes from dataAttributeMainValuePerLevel.json.
    /// Secondary attributes now from dataPlayer.json via BaseStatLoader (single source of truth).
    /// </summary>
    public sealed class AttributeStatLoader : MonoBehaviour
    {
        [SerializeField] private bool _debug = false;
        private static AttributeStatLoader _instance;
        public static AttributeStatLoader Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAll();
        }

        // Main Attribute progression (CON/STR/INT/DEX)
        private readonly Dictionary<MainAttribute, AttributeProgression> _mainProgression = new();

        public bool IsLoaded { get; private set; } = false;

        public void LoadAll()
        {
            LoadMainAttributeProgression();
            IsLoaded = true;
            if (_debug) Debug.Log("[AttributeStatLoader] Loaded main attribute progression database. Secondary stats now from BaseStatLoader.");
        }

        private void LoadMainAttributeProgression()
        {
            var jsonAsset = Resources.Load<TextAsset>("Data/Player/dataAttributeMainValuePerLevel");
            if (jsonAsset == null)
            {
                Debug.LogError("[AttributeStatLoader] dataAttributeMainValuePerLevel.json not found in Resources/Data/Player/");
                return;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<Dictionary<string, AttributeProgression>>(jsonAsset.text);
                if (data == null) return;

                foreach (var kvp in data)
                {
                    if (Enum.TryParse<MainAttribute>(kvp.Key, true, out var attr))
                    {
                        _mainProgression[attr] = kvp.Value;
                    }
                    else
                    {
                        Debug.LogWarning($"[AttributeStatLoader] Unknown MainAttribute key in JSON: {kvp.Key}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AttributeStatLoader] Failed to parse dataAttributeMainValuePerLevel.json: {e.Message}");
            }
        }

        /// <summary>
        /// Gets progression for a MainAttribute. Returns default (0,0) if not found.
        /// </summary>
        public AttributeProgression GetMainProgression(MainAttribute attribute)
        {
            return _mainProgression.TryGetValue(attribute, out var prog) ? prog : default;
        }

        /// <summary>
        /// Gets progression for a SecondaryStat from BaseStatLoader (single source of truth).
        /// Returns default (0,0) if not found.
        /// </summary>
        public AttributeProgression GetSecondaryProgression(SecondaryStat stat)
        {
            if (BaseStatLoader.Instance == null) return default;

            var skillData = BaseStatLoader.Instance.GetSecondarySkillData(stat);
            if (skillData == null) return default;

            return new AttributeProgression
            {
                ValuePerLevel = skillData.ValuePerLevel,
                ValuePerEnhance = skillData.ValuePerEnhance
            };
        }

        /// <summary>
        /// Calculates final attribute value from base + level contributions.
        /// Uses (Level - 1) * ValuePerLevel + EnhanceLevel * ValuePerEnhance progression.
        /// </summary>
        public float CalculateFinalValue(MainAttribute attribute, float baseValue, int level)
        {
            var prog = GetMainProgression(attribute);
            return baseValue + prog.ValuePerLevel * (level - 1);
        }

        /// <summary>
        /// Calculates final secondary stat value from base + level contributions.
        /// Uses (Level - 1) * ValuePerLevel + EnhanceLevel * ValuePerEnhance progression.
        /// </summary>
        public float CalculateFinalValue(SecondaryStat stat, float baseValue, int level)
        {
            var prog = GetSecondaryProgression(stat);
            return baseValue + prog.ValuePerLevel * (level - 1);
        }

        /// <summary>
        /// Data structure matching JSON: { "ValuePerLevel": float, "ValuePerEnhance": float }
        /// </summary>
        [Serializable]
        public struct AttributeProgression
        {
            public float ValuePerLevel;
            public float ValuePerEnhance;
        }
    }
}