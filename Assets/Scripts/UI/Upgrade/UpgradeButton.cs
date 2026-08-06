using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Upgrade;
using IdleDefenseSurvival.Economy;
using System.Collections.Generic;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Reusable button component for upgrading a single skill.
    /// Shows skill name, current level, cost, and handles upgrade action.
    /// </summary>
    public class UpgradeButton : MonoBehaviour
    {
        [SerializeField] private bool debug;
        [Header("Skill Configuration")]
        [SerializeField] private string _skillId;
        [SerializeField] private string _displayName;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private Button _button;
        [SerializeField] private Image _buttonImage;

        private UpgradeManager _upgradeManager;
        private EconomyManager _economyManager;

        private void Start()
        {
            _upgradeManager = UpgradeManager.Instance;
            _economyManager = EconomyManager.Instance;

            if (_upgradeManager == null)
            {
                if (debug) Debug.LogError("[UpgradeButton] UpgradeManager not found!");
                return;
            }

            if (_economyManager == null)
            {
                if (debug) Debug.LogError("[UpgradeButton] EconomyManager not found!");
                return;
            }

            // Setup button click
            if (_button != null)
            {
                _button.onClick.AddListener(OnUpgradeClicked);
            }

            // Subscribe to events (guard: UnityEvent may be null if not initialized)
            if (_economyManager.OnCurrencyChanged != null)
                _economyManager.OnCurrencyChanged.AddListener(OnCurrencyChanged);

            if (_upgradeManager.OnSkillUpgraded != null)
                _upgradeManager.OnSkillUpgraded.AddListener(OnSkillUpgraded);

            // Initial refresh
            RefreshDisplay();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (_economyManager != null)
                _economyManager.OnCurrencyChanged.RemoveListener(OnCurrencyChanged);

            if (_upgradeManager != null)
                _upgradeManager.OnSkillUpgraded.RemoveListener(OnSkillUpgraded);

            if (_button != null)
                _button.onClick.RemoveListener(OnUpgradeClicked);
        }

        /// <summary>
        /// Initialize button with skill ID and display name.
        /// Called by UpgradeMenuUI when spawning buttons.
        /// </summary>
        public void Initialize(string skillId, string displayName)
        {
            _skillId = skillId;
            _displayName = displayName;
            RefreshDisplay();
        }

        private void OnUpgradeClicked()
        {
            if (_upgradeManager == null) return;

            // Jika skill masih terkunci, lakukan unlock dulu
            if (_upgradeManager.IsSkillLocked(_skillId))
            {
                bool success = _upgradeManager.UnlockSkill(_skillId);
                if (success)
                {
                    if (debug) Debug.Log($"[UpgradeButton] Berhasil unlock {_skillId}");
                    RefreshDisplay();
                }
                else
                {
                    if (debug) Debug.LogWarning($"[UpgradeButton] Gagal unlock {_skillId}");
                }
            }
            else
            {
                bool success = _upgradeManager.UpgradeSkill(_skillId, "Player upgrade via UI");
                if (success)
                {
                    if (debug) Debug.Log($"[UpgradeButton] Successfully upgraded {_skillId}");
                    RefreshDisplay();
                }
                else
                {
                    if (debug) Debug.LogWarning($"[UpgradeButton] Failed to upgrade {_skillId}");
                }
            }
        }

        private void OnCurrencyChanged(CurrencyType type, long oldValue, long newValue)
        {
            // Refresh display when currency changes
            RefreshDisplay();
        }

        private void OnSkillUpgraded(string skillId, int oldLevel, int newLevel)
        {
            // Refresh display when any skill is upgraded
            if (skillId == _skillId)
            {
                RefreshDisplay();
            }
        }

        private void RefreshDisplay()
        {
            if (_upgradeManager == null) return;

            int currentLevel = _upgradeManager.GetSkillLevel(_skillId);
            int maxLevel = _upgradeManager.GetSkillMaxLevel(_skillId);
            bool isLocked = _upgradeManager.IsSkillLocked(_skillId);
            bool isMaxLevel = currentLevel >= maxLevel;
            
            // Update name
            if (_nameText != null)
            {
                _nameText.text = string.IsNullOrEmpty(_displayName) ? _skillId : _displayName;
            }

            // Update level
            if (_levelText != null)
            {
                _levelText.text = $"Lv {currentLevel}/{maxLevel}";
            }

            // Update button text and state based on lock status
            if (isLocked)
            {
                // Tampilkan info tombol unlock
                var unlockCosts = UpgradeCostCalculator.CalculateUnlockCost(_skillId);
                if (unlockCosts != null && unlockCosts.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (var kvp in unlockCosts)
                    {
                        parts.Add($"{Utilityku.FormatNumber(kvp.Value)} {kvp.Key}");
                    }
                    _costText.text = "Unlock: " + string.Join(", ", parts);
                }
                else
                {
                    _costText.text = "Unlock: Free";
                }
                bool canUnlock = UpgradeCostCalculator.CanAffordUnlock(_skillId);
                _button.interactable = canUnlock;
            }
            else
            {
                if (isMaxLevel)
                {
                    _costText.text = "MAX";
                    _button.interactable = false;
                }
                else
                {
                    string costString = UpgradeCostCalculator.GetCostString(_skillId, currentLevel);
                    _costText.text = costString;
                    bool canUpgrade = _upgradeManager.CanUpgrade(_skillId);
                    _button.interactable = canUpgrade;
                }
            }

            // Update visual color
            if (_buttonImage != null)
            {
                if (isLocked)
                {
                    bool canUnlock = UpgradeCostCalculator.CanAffordUnlock(_skillId);
                    _buttonImage.sprite = canUnlock ? ButtonResources.GetColor("Green") : ButtonResources.GetColor("Grey");
                }
                else if (isMaxLevel)
                {
                    _buttonImage.sprite = ButtonResources.GetColor("Yellow");
                }
                else if (_upgradeManager.CanUpgrade(_skillId))
                {
                    _buttonImage.sprite = ButtonResources.GetColor("Green");
                }
                else
                {
                    _buttonImage.sprite = ButtonResources.GetColor("Grey");
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-refresh in editor when values change
            if (Application.isPlaying)
            {
                RefreshDisplay();
            }
        }
#endif
    }
}
