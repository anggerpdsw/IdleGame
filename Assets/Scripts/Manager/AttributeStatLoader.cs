using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Loads attribute progression values from JSON databases.
    /// Single source of truth for ValuePerLevel for both Main and Secondary attributes.
    /// </summary>
    public sealed class AttributeStatLoader : MonoBehaviour
    {
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

        // Secondary Attribute progression (SecondaryStat enum)
        private readonly Dictionary<SecondaryStat, AttributeProgression> _secondaryProgression = new();

        public bool IsLoaded { get; private set; } = false;

        public void LoadAll()
        {
            LoadMainAttributeProgression();
            LoadSecondaryAttributeProgression();
            IsLoaded = true;
            Debug.Log("[AttributeStatLoader] Loaded main and secondary attribute progression databases.");
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

        private void LoadSecondaryAttributeProgression()
        {
            var jsonAsset = Resources.Load<TextAsset>("Data/Player/dataAttributeSecondValuePerLevel");
            if (jsonAsset == null)
            {
                Debug.LogError("[AttributeStatLoader] dataAttributeSecondValuePerLevel.json not found in Resources/Data/Player/");
                return;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<Dictionary<string, AttributeProgression>>(jsonAsset.text);
                if (data == null) return;

                foreach (var kvp in data)
                {
                    // JSON uses camelCase (e.g., "attackRange"), enum uses PascalCase (e.g., AttackRange)
                    string enumName = ToPascalCase(kvp.Key);
                    if (Enum.TryParse<SecondaryStat>(enumName, true, out var stat))
                    {
                        _secondaryProgression[stat] = kvp.Value;
                    }
                    else
                    {
                        Debug.LogWarning($"[AttributeStatLoader] Unknown SecondaryStat key in JSON: {kvp.Key} (tried {enumName})");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AttributeStatLoader] Failed to parse dataAttributeSecondValuePerLevel.json: {e.Message}");
            }
        }

        private static string ToPascalCase(string camelCase)
        {
            if (string.IsNullOrEmpty(camelCase)) return camelCase;
            return char.ToUpperInvariant(camelCase[0]) + camelCase.Substring(1);
        }

        /// <summary>
        /// Gets progression for a MainAttribute. Returns default (0,0) if not found.
        /// </summary>
        public AttributeProgression GetMainProgression(MainAttribute attribute)
        {
            return _mainProgression.TryGetValue(attribute, out var prog) ? prog : default;
        }

        /// <summary>
        /// Gets progression for a SecondaryStat. Returns default (0,0) if not found.
        /// </summary>
        public AttributeProgression GetSecondaryProgression(SecondaryStat stat)
        {
            return _secondaryProgression.TryGetValue(stat, out var prog) ? prog : default;
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
        }
    }
}