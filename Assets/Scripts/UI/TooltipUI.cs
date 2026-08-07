using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using System.Linq;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Unified tooltip system for items, equipment, and comparisons.
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        #region Singleton
        private static TooltipUI _instance;
        public static TooltipUI Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            // Don't DontDestroyOnLoad - tooltip is scene-specific
        }
        #endregion

        [Header("Tooltip Root")]
        [SerializeField] private GameObject _tooltipRoot;
        [SerializeField] private Canvas _tooltipCanvas;

        [Header("Common Elements")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _rarityText;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _flavorText;

        [Header("Equipment Sections")]
        [SerializeField] private GameObject _equipmentSection;
        [SerializeField] private TextMeshProUGUI _equipTypeText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _enhanceText;
        [SerializeField] private Slider _durabilityBar;
        [SerializeField] private TextMeshProUGUI _durabilityText;
        [SerializeField] private Transform _mainStatsContainer;
        [SerializeField] private GameObject _statEntryPrefab;
        [SerializeField] private Transform _specialEffectsContainer;
        [SerializeField] private GameObject _effectEntryPrefab;
        [SerializeField] private Transform _setBonusContainer;
        [SerializeField] private GameObject _setBonusEntryPrefab;
        [SerializeField] private Transform _gemSocketsContainer;
        [SerializeField] private GameObject _gemSocketPrefab;

        [Header("Consumable Sections")]
        [SerializeField] private GameObject _consumableSection;
        [SerializeField] private TextMeshProUGUI _useEffectText;
        [SerializeField] private TextMeshProUGUI _cooldownText;

        [Header("Comparison")]
        [SerializeField] private GameObject _comparisonSection;
        [SerializeField] private Transform _comparisonStatsContainer;
        [SerializeField] private GameObject _comparisonStatPrefab;

        [Header("Positioning")]
        [SerializeField] private Vector2 _offset = new(10, -10);
        [SerializeField] private bool _followMouse = true;

        // State
        private InventoryItem _currentItem;
        private InventoryItem _comparisonItem;
        private RectTransform _tooltipRect;
        private bool _isVisible = false;

        private void Start()
        {
            _tooltipRect = _tooltipRoot.GetComponent<RectTransform>();
            Hide();
        }

        private void Update()
        {
            if (_isVisible && _followMouse)
            {
                UpdatePosition();
            }
        }

        #region Public API
        /// <summary>
        /// Shows tooltip for an equipment item.
        /// </summary>
        public void ShowEquipment(InventoryItem item, Vector3 screenPosition, InventoryItem comparisonItem = null)
        {
            if (item == null) return;

            _currentItem = item;
            _comparisonItem = comparisonItem;

            BuildEquipmentTooltip(item, comparisonItem);
            ShowAtPosition(screenPosition);
        }

        /// <summary>
        /// Shows tooltip for a consumable/item.
        /// </summary>
        public void ShowItem(InventoryItem item, Vector3 screenPosition)
        {
            if (item == null) return;

            _currentItem = item;
            _comparisonItem = null;

            BuildItemTooltip(item);
            ShowAtPosition(screenPosition);
        }

        /// <summary>
        /// Shows tooltip at a specific world position.
        /// </summary>
        public void ShowAtWorldPosition(InventoryItem item, Vector3 worldPosition, UnityEngine.Camera camera = null)
        {
            if (camera == null) camera = UnityEngine.Camera.main;
            Vector3 screenPos = camera.WorldToScreenPoint(worldPosition);
            ShowEquipment(item, screenPos);
        }

        /// <summary>
        /// Hides the tooltip.
        /// </summary>
        public void Hide()
        {
            _tooltipRoot.SetActive(false);
            _isVisible = false;
            _currentItem = null;
            _comparisonItem = null;
        }
        #endregion

        #region Tooltip Building
        private void BuildEquipmentTooltip(InventoryItem item, InventoryItem comparisonItem)
        {
            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) return;

            // Show/hide sections
            _equipmentSection?.SetActive(true);
            _consumableSection?.SetActive(false);
            _comparisonSection?.SetActive(comparisonItem != null);

            // Basic info
            SetBasicInfo(item, itemData);

            // Equipment specific
            SetEquipmentInfo(item, itemData);

            // Combat stats
            SetCombatStats(item, itemData, comparisonItem);

            // Special effects
            SetSpecialEffects(itemData);

            // Set bonuses
            SetSetBonuses(item);

            // Gem sockets
            SetGemSockets(item);

            // Comparison
            if (comparisonItem != null)
            {
                SetComparison(item, comparisonItem);
            }
        }

        private void BuildItemTooltip(InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId);
            if (itemData == null) return;

            _equipmentSection?.SetActive(false);
            _consumableSection?.SetActive(itemData.IsConsumable());
            _comparisonSection?.SetActive(false);

            SetBasicInfo(item, itemData);

            if (itemData.IsConsumable())
            {
                SetConsumableInfo(item, itemData);
            }
        }

        private void SetBasicInfo(InventoryItem item, ItemData itemData)
        {
            if (_iconImage != null && itemData.Icon != null)
            {
                _iconImage.sprite = itemData.Icon;
                _iconImage.enabled = true;
            }

            if (_nameText != null)
            {
                _nameText.text = itemData.Name;
                _nameText.color = itemData.ItemRarity.GetDefaultColor();
            }

            if (_rarityText != null)
            {
                _rarityText.text = itemData.ItemRarity.GetDisplayName();
                _rarityText.color = itemData.ItemRarity.GetDefaultColor();
            }

            if (_rarityBorder != null)
            {
                _rarityBorder.color = itemData.ItemRarity.GetDefaultColor();
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = itemData.Description;
            }

            if (_flavorText != null)
            {
                _flavorText.text = itemData.FlavorText;
                _flavorText.gameObject.SetActive(!string.IsNullOrEmpty(itemData.FlavorText));
            }
        }

        private void SetEquipmentInfo(InventoryItem item, EquipmentData itemData)
        {
            if (_equipTypeText != null)
            {
                _equipTypeText.text = itemData.EquipmentType.GetDisplayName();
            }

            if (_levelText != null)
            {
                _levelText.text = $"Level {item.Level}/{itemData.MaxLevel}";
            }

            if (_enhanceText != null)
            {
                _enhanceText.text = item.EnhanceLevel > 0 ? $"+{item.EnhanceLevel}" : "";
                _enhanceText.gameObject.SetActive(item.EnhanceLevel > 0);
            }

            if (_durabilityBar != null)
            {
                float p = item.GetDurabilityPercent();
                _durabilityBar.value = p;
                var fill = _durabilityBar.fillRect != null ? _durabilityBar.fillRect.GetComponent<Image>() : null;
                if (fill != null) fill.color = item.GetDurabilityColor();
            }

            if (_durabilityText != null)
            {
                _durabilityText.text = $"{item.CurrentDurability}/{item.MaxDurability}";
            }
        }

        private void SetCombatStats(InventoryItem item, EquipmentData itemData, InventoryItem comparisonItem)
        {
            if (_mainStatsContainer == null || _statEntryPrefab == null) return;

            // Clear existing
            foreach (Transform child in _mainStatsContainer)
                Destroy(child.gameObject);

            var bonuses = EquipmentComparer.GetTotalStatBonuses(item);
            var comparisonBonuses = comparisonItem != null ? EquipmentComparer.GetTotalStatBonuses(comparisonItem) : null;

            // Core attributes first (CON/STR/INT/DEX) — equipment identity.
            var attrBonuses = EquipmentStatCalculator.GetItemAttributeBonuses(item);
            var comparisonAttr = comparisonItem != null ? EquipmentStatCalculator.GetItemAttributeBonuses(comparisonItem) : null;

            foreach (var kvp in attrBonuses.OrderByDescending(k => Math.Abs(k.Value)))
            {
                var entryObj = Instantiate(_statEntryPrefab, _mainStatsContainer);
                var entryUI = entryObj.GetComponent<TooltipStatEntryUI>();
                if (entryUI != null)
                {
                    float compareValue = comparisonAttr?.GetValueOrDefault(kvp.Key, 0) ?? 0;
                    entryUI.Initialize(kvp.Key.GetDisplayName(), kvp.Value, compareValue);
                }
            }

            foreach (var kvp in bonuses.OrderByDescending(k => Math.Abs(k.Value)))
            {
                var entryObj = Instantiate(_statEntryPrefab, _mainStatsContainer);
                var entryUI = entryObj.GetComponent<TooltipStatEntryUI>();
                if (entryUI != null)
                {
                    float compareValue = comparisonBonuses?.GetValueOrDefault(kvp.Key, 0) ?? 0;
                    entryUI.Initialize(kvp.Key, kvp.Value, compareValue);
                }
            }
        }

        private void SetSpecialEffects(EquipmentData itemData)
        {
            if (_specialEffectsContainer == null || _effectEntryPrefab == null) return;

            foreach (Transform child in _specialEffectsContainer)
                Destroy(child.gameObject);

            if (itemData.SpecialEffects != null)
            {
                foreach (var effect in itemData.SpecialEffects)
                {
                    if (effect.IsActive)
                    {
                        var entryObj = Instantiate(_effectEntryPrefab, _specialEffectsContainer);
                        var entryUI = entryObj.GetComponent<TooltipEffectEntryUI>();
                        entryUI?.Initialize(effect);
                    }
                }
            }
        }

        private void SetSetBonuses(InventoryItem item)
        {
            if (_setBonusContainer == null || _setBonusEntryPrefab == null) return;

            foreach (Transform child in _setBonusContainer)
                Destroy(child.gameObject);

            string setId = item.GetSetId();
            if (string.IsNullOrEmpty(setId)) return;

            var setData = ItemDatabase.Instance?.GetSet(setId);
            if (setData?.Tiers == null) return;

            int pieceCount = EquipmentService.Instance?.GetSetPieceCount(setId) + 1 ?? 1;

            foreach (var tier in setData.Tiers.Where(t => t.IsActive(pieceCount)))
            {
                var entryObj = Instantiate(_setBonusEntryPrefab, _setBonusContainer);
                var entryUI = entryObj.GetComponent<TooltipSetBonusEntryUI>();
                entryUI?.Initialize(setData, tier, pieceCount);
            }
        }

        private void SetGemSockets(InventoryItem item)
        {
            if (_gemSocketsContainer == null || _gemSocketPrefab == null) return;

            foreach (Transform child in _gemSocketsContainer)
                Destroy(child.gameObject);

            if (item.Sockets == null) return;

            foreach (var socket in item.Sockets)
            {
                var entryObj = Instantiate(_gemSocketPrefab, _gemSocketsContainer);
                var entryUI = entryObj.GetComponent<TooltipGemSocketUI>();
                entryUI?.Initialize(socket, item);
            }
        }

        private void SetConsumableInfo(InventoryItem item, ItemData itemData)
        {
            if (_useEffectText != null)
            {
                _useEffectText.text = itemData.Description; // Would be specific use effect
            }

            if (_cooldownText != null)
            {
                // Check for cooldown
                _cooldownText.gameObject.SetActive(false);
            }
        }

        private void SetComparison(InventoryItem current, InventoryItem candidate)
        {
            if (_comparisonStatsContainer == null || _comparisonStatPrefab == null) return;

            foreach (Transform child in _comparisonStatsContainer)
                Destroy(child.gameObject);

            var comparison = EquipmentComparer.Compare(current, candidate, candidate.GetEquipmentType());

            foreach (var kvp in comparison.StatComparisons)
            {
                if (Math.Abs(kvp.Value.Difference) < 0.001f) continue;

                var entryObj = Instantiate(_comparisonStatPrefab, _comparisonStatsContainer);
                var entryUI = entryObj.GetComponent<TooltipComparisonStatUI>();
                entryUI?.Initialize(kvp.Value);
            }
        }
        #endregion

        #region Positioning
        private void ShowAtPosition(Vector3 screenPosition)
        {
            _tooltipRoot.SetActive(true);
            _isVisible = true;
            UpdatePosition(screenPosition);
        }

        private void UpdatePosition(Vector3? screenPosition = null)
        {
            Vector3 pos = screenPosition ?? Input.mousePosition;
            pos += (Vector3)_offset;

            // Keep on screen
            Vector2 size = _tooltipRect.sizeDelta;
            float canvasWidth = _tooltipCanvas.pixelRect.width;
            float canvasHeight = _tooltipCanvas.pixelRect.height;

            if (pos.x + size.x > canvasWidth) pos.x = canvasWidth - size.x;
            if (pos.y - size.y < 0) pos.y = size.y;

            _tooltipRect.position = pos;
        }
        #endregion
    }

    // ============ Tooltip Sub-Components ============

    public class TooltipStatEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _statValueText;
        [SerializeField] private TextMeshProUGUI _comparisonText;

        public void Initialize(SecondaryStat stat, float value, float comparisonValue = 0)
            => Initialize(stat.GetDisplayName(), value, comparisonValue);

        public void Initialize(string statName, float value, float comparisonValue = 0)
        {
            if (_statNameText != null)
                _statNameText.text = statName;

            if (_statValueText != null)
            {
                string sign = value >= 0 ? "+" : "";
                _statValueText.text = $"{sign}{value:F1}";
                _statValueText.color = value >= 0 ? Color.green : Color.red;
            }

            if (_comparisonText != null && comparisonValue != 0)
            {
                float diff = comparisonValue - value;
                string sign = diff >= 0 ? "+" : "";
                _comparisonText.text = $"({sign}{diff:F1})";
                _comparisonText.color = diff >= 0 ? Color.green : Color.red;
            }
            else if (_comparisonText != null)
            {
                _comparisonText.text = "";
            }
        }
    }

    public class TooltipEffectEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _effectNameText;
        [SerializeField] private TextMeshProUGUI _effectValueText;
        [SerializeField] private TextMeshProUGUI _effectChanceText;

        public void Initialize(SpecialEffectEntry effect)
        {
            if (_effectNameText != null)
                _effectNameText.text = effect.EffectType.GetDisplayName();

            if (_effectValueText != null)
            {
                _effectValueText.text = $"{effect.Value:F1}";
            }

            if (_effectChanceText != null && effect.Chance < 100)
            {
                _effectChanceText.text = $"{effect.Chance:F0}% chance";
            }
            else if (_effectChanceText != null)
            {
                _effectChanceText.text = "";
            }
        }
    }

    public class TooltipSetBonusEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _setNameText;
        [SerializeField] private TextMeshProUGUI _tierText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _progressText;

        public void Initialize(SetBonusData setData, SetBonusTier tier, int currentPieces)
        {
            if (_setNameText != null)
                _setNameText.text = setData.SetName;

            if (_tierText != null)
                _tierText.text = tier.TierName;

            if (_descriptionText != null)
                _descriptionText.text = tier.Description;

            if (_progressBar != null)
            {
                _progressBar.fillAmount = (float)currentPieces / tier.RequiredPieces;
            }

            if (_progressText != null)
            {
                _progressText.text = $"{currentPieces}/{tier.RequiredPieces}";
            }
        }
    }

    public class TooltipGemSocketUI : MonoBehaviour
    {
        [SerializeField] private Image _socketBackground;
        [SerializeField] private Image _gemIcon;
        [SerializeField] private Image _gemTypeColor;
        [SerializeField] private TextMeshProUGUI _gemLevelText;
        [SerializeField] private GameObject _lockedOverlay;

        public void Initialize(SocketData socket, InventoryItem parentItem)
        {
            if (socket.IsUnlocked)
            {
                if (_lockedOverlay != null) _lockedOverlay.SetActive(false);
            }
            else
            {
                if (_lockedOverlay != null) _lockedOverlay.SetActive(true);
            }

            if (socket.IsEmpty)
            {
                if (_gemIcon != null) _gemIcon.enabled = false;
                if (_gemTypeColor != null) _gemTypeColor.enabled = false;
                if (_gemLevelText != null) _gemLevelText.enabled = false;
            }
            else
            {
                var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
                if (gemData != null)
                {
                    if (_gemIcon != null && gemData.Icon != null)
                    {
                        _gemIcon.sprite = gemData.Icon;
                        _gemIcon.enabled = true;
                    }
                    if (_gemTypeColor != null)
                    {
                        _gemTypeColor.color = gemData.GemColor;
                        _gemTypeColor.enabled = true;
                    }
                    if (_gemLevelText != null)
                    {
                        _gemLevelText.text = $"Lv.{socket.GemLevel}";
                        _gemLevelText.enabled = true;
                    }
                }
            }
        }
    }

    public class TooltipComparisonStatUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _statNameText;
        [SerializeField] private TextMeshProUGUI _currentValueText;
        [SerializeField] private TextMeshProUGUI _newValueText;
        [SerializeField] private TextMeshProUGUI _differenceText;

        public void Initialize(StatComparison comp)
        {
            if (_statNameText != null)
                _statNameText.text = comp.Stat.GetShortName();

            if (_currentValueText != null)
                _currentValueText.text = comp.CurrentValue.ToString("F1");

            if (_newValueText != null)
                _newValueText.text = comp.NewValue.ToString("F1");

            if (_differenceText != null)
            {
                string sign = comp.Difference >= 0 ? "+" : "";
                _differenceText.text = $"{sign}{comp.Difference:F1} ({comp.PercentChange:+0.0;-0.0}%)";
                _differenceText.color = comp.Difference >= 0 ? Color.green : Color.red;
            }
        }
    }
}