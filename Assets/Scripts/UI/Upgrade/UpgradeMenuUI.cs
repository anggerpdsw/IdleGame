using UnityEngine;
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Manages the upgrade menu UI.
    /// Spawns upgrade buttons for all 18 skills and handles menu show/hide.
    /// </summary>
    public class UpgradeMenuUI : MonoBehaviour
    {
        [SerializeField] private bool debug;
        [Header("UI References")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _upgradeButtonPrefab;

        private List<UpgradeButton> _spawnedButtons = new();
        private bool _isMenuOpen = false;

        private PlayerData _playerData;

        private void Start()
        {
            // Load skill data from dataPlayer.json
            LoadSkillData();

            // Initial state
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
                _isMenuOpen = false;
            }

            // Spawn all upgrade buttons
            SpawnUpgradeButtons();
        }

        private void Update()
        {
        }

        private void OnDestroy()
        {
        }

        /// <summary>
        /// Loads skill data from dataPlayer.json.
        /// </summary>
        private void LoadSkillData()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Data/dataPlayer");
            if (jsonAsset == null)
            {
                if (debug) Debug.LogError("[UpgradeMenuUI] dataPlayer.json not found in Resources/Data!");
                return;
            }

            try
            {
                _playerData = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerData>(jsonAsset.text);
                if (_playerData?.skills == null)
                {
                    if (debug) Debug.LogError("[UpgradeMenuUI] Failed to deserialize dataPlayer.json or skills data is missing!");
                }
            }
            catch (Exception e)
            {
                if (debug) Debug.LogError($"[UpgradeMenuUI] Error loading dataPlayer.json: {e.Message}");
            }
        }

        /// <summary>
        /// Process a skill group and create buttons for each skill.
        /// Uses reflection to iterate through SkillData fields in the group.
        /// </summary>
        private void ProcessSkillGroup(object group)
        {
            if (group == null) return;

            var fields = group.GetType().GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(SkillData))
                {
                    var skill = (SkillData)field.GetValue(group);

                    // Use flat skill ID (e.g., "attackDamage") for compatibility with UpgradeManager
                    string skillId = field.Name;
                    string displayName = string.IsNullOrEmpty(skill.displayName) ? field.Name : skill.displayName;

                    GameObject buttonObj = Instantiate(_upgradeButtonPrefab, _buttonContainer);
                    if (buttonObj.TryGetComponent<UpgradeButton>(out var button))
                    {
                        button.Initialize(skillId, displayName);
                        _spawnedButtons.Add(button);
                    }
                    else
                    {
                        if (debug) Debug.LogWarning("[UpgradeMenuUI] UpgradeButton component not found on prefab!");
                        Destroy(buttonObj);
                    }
                }
            }
        }

        /// <summary>
        /// Spawn upgrade buttons for all skills found in dataPlayer.json.
        /// </summary>
        private void SpawnUpgradeButtons()
        {
            if (_upgradeButtonPrefab == null)
            {
                if (debug) Debug.LogError("[UpgradeMenuUI] Upgrade button prefab not assigned!");
                return;
            }

            if (_buttonContainer == null)
            {
                if (debug) Debug.LogError("[UpgradeMenuUI] Button container not assigned!");
                return;
            }

            if (_playerData?.skills == null)
            {
                if (debug) Debug.LogError("[UpgradeMenuUI] Skill data not loaded. Cannot spawn buttons.");
                return;
            }

            // Clear existing buttons
            foreach (var button in _spawnedButtons)
            {
                if (button != null) Destroy(button.gameObject);
            }
            _spawnedButtons.Clear();

            // Iterate grouped skills using reflection
            ProcessSkillGroup(_playerData.skills.attack);
            ProcessSkillGroup(_playerData.skills.defense);
            ProcessSkillGroup(_playerData.skills.currency);

            if (debug) Debug.Log($"[UpgradeMenuUI] Spawned {_spawnedButtons.Count} upgrade buttons.");
        }

        /// <summary>
        /// Toggle menu open/close.
        /// </summary>
        public void ToggleMenu()
        {
            if (_isMenuOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        /// <summary>
        /// Open the upgrade menu.
        /// </summary>
        public void OpenMenu()
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(true);
                _isMenuOpen = true;
                Time.timeScale = 0f; // Pause game
                if (debug) Debug.Log("[UpgradeMenuUI] Menu opened.");
            }
        }

        /// <summary>
        /// Close the upgrade menu.
        /// </summary>
        public void CloseMenu()
        {
            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
                _isMenuOpen = false;
                Time.timeScale = 1f; // Resume game
                if (debug) Debug.Log("[UpgradeMenuUI] Menu closed.");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Refresh Buttons")]
        private void DebugRefreshButtons()
        {
            SpawnUpgradeButtons();
        }
#endif
    }
}
