using UnityEngine;
using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Displays player skills as a static list.
    /// Skills have no levels and cannot be upgraded — they are fixed values
    /// influenced by main stats (Constitution/Strength/Intelligence/Dexterity).
    /// Each row shows skill name, base value, and description.
    /// </summary>
    public class UpgradeMenuUI : MonoBehaviour
    {
        [SerializeField] private bool debug;
        [Header("UI References")]
        [SerializeField] private GameObject _menuPanel;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private GameObject _skillRowPrefab; // Row prefab with a SkillRowUI component

        private List<SkillRowUI> _spawnedRows = new();
        private bool _isMenuOpen = false;

        private PlayerData _playerData;

        private void Start()
        {
            LoadSkillData();

            if (_menuPanel != null)
            {
                _menuPanel.SetActive(false);
                _isMenuOpen = false;
            }

            SpawnSkillRows();
        }

        [Obsolete("Skills have no levels; kept as a no-op for compatibility.")]
        private void Update() { }

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
        /// Spawn one static skill display row per SkillData field in each group.
        /// </summary>
        private void SpawnSkillRows()
        {
            if (_skillRowPrefab == null)
            {
                if (debug) Debug.LogError("[UpgradeMenuUI] Skill row prefab not assigned!");
                return;
            }

            if (_buttonContainer == null)
            {
                if (debug) Debug.LogError("[UpgradeMenuUI] Button container not assigned!");
                return;
            }

            if (_playerData?.skills == null)
            {
                if (debug) Debug.LogError("[UpgradeMenuUI] Skill data not loaded. Cannot spawn rows.");
                return;
            }

            foreach (var row in _spawnedRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _spawnedRows.Clear();

            SpawnGroupRows(_playerData.skills.attack);
            SpawnGroupRows(_playerData.skills.defense);
            SpawnGroupRows(_playerData.skills.currency);

            if (debug) Debug.Log($"[UpgradeMenuUI] Spawned {_spawnedRows.Count} skill rows.");
        }

        /// <summary>
        /// Called by OpenMenu before showing — re-reads current values from SkillLoader
        /// (which reflects the latest dataPlayer.json state).
        /// </summary>
        private void RefreshDisplayedValues()
        {
            SkillLoader.Initialize();

            foreach (var row in _spawnedRows)
            {
                if (row == null) continue;
                row.RefreshValue(SkillLoader.GetBaseValue(row.SkillId));
            }
        }

        private void SpawnGroupRows(object group)
        {
            if (group == null) return;

            var fields = group.GetType().GetFields();
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(SkillData)) continue;

                var skill = (SkillData)field.GetValue(group);
                var rowObj = Instantiate(_skillRowPrefab, _buttonContainer);
                if (rowObj.TryGetComponent<SkillRowUI>(out var row))
                {
                    row.Initialize(
                        field.Name,
                        string.IsNullOrEmpty(skill.displayName) ? field.Name : skill.displayName,
                        skill.baseValue,
                        skill.description
                    );
                    _spawnedRows.Add(row);
                }
                else
                {
                    if (debug) Debug.LogWarning("[UpgradeMenuUI] SkillRowUI component not found on prefab!");
                    Destroy(rowObj);
                }
            }
        }

        /// <summary>
        /// Open menu and refresh displayed values (base stats + any modded stat sources).
        /// </summary>
        public void ToggleMenu()
        {
            if (_isMenuOpen) CloseMenu();
            else OpenMenu();
        }

        public void OpenMenu()
        {
            if (_menuPanel == null) return;
            _menuPanel.SetActive(true);
            _isMenuOpen = true;
            RefreshDisplayedValues();
            if (debug) Debug.Log("[UpgradeMenuUI] Menu opened.");
        }

        public void CloseMenu()
        {
            if (_menuPanel == null) return;
            _menuPanel.SetActive(false);
            _isMenuOpen = false;
            if (debug) Debug.Log("[UpgradeMenuUI] Menu closed.");
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Refresh Rows")]
        private void DebugRefreshRows()
        {
            SpawnSkillRows();
        }

        private void OnValidate() => _skillRowPrefab?.GetComponent<SkillRowUI>(); // hint the prefab component
#endif
    }
}