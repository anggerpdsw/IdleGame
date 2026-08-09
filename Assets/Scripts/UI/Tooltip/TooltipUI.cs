using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using System.Linq;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.UI.Tooltip
{
    /// <summary>
    /// Unified tooltip system for items, equipment, and comparisons.
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        #region Singleton scene-local
        public static TooltipUI Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance.gameObject.scene == gameObject.scene)
                {
                    // Duplicate TooltipUI in the same scene.
                    Destroy(gameObject);
                    return;
                }
                // TooltipUI from previous scene. Replace it with this scene's instance.
                Instance = this;
            }
            else
            {
                Instance = this;
            }
            if (_tooltipRoot != null)
                _tooltipRect = _tooltipRoot.GetComponent<RectTransform>();
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
        [SerializeField] private Vector2 _offset = new(10, -80);
        [SerializeField] private bool _followMouse = true;

        // State
        private InventoryItem _currentItem;
        private InventoryItem _comparisonItem;
        private RectTransform _tooltipRect;
        private bool _isVisible = false;

        private void Start()
        {
            Hide();
        }

        private void Update()
        {
            if (_isVisible && _followMouse)
                UpdatePosition();
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
        /// Shows a plain-text tooltip (no item sections). Used for generic help/hover info.
        /// </summary>
        public void ShowText(string text, Vector3 screenPosition)
        {
            _currentItem = null;
            _comparisonItem = null;

            if (_equipmentSection != null) _equipmentSection.SetActive(false);
            if (_consumableSection != null) _consumableSection.SetActive(false);
            if (_comparisonSection != null) _comparisonSection.SetActive(false);
            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
            if (_nameText != null) _nameText.text = "";
            if (_rarityText != null) _rarityText.text = "";
            if (_rarityBorder != null) _rarityBorder.enabled = false;
            if (_flavorText != null)
            {
                _flavorText.text = "";
                _flavorText.gameObject.SetActive(false);
            }
            if (_descriptionText != null) _descriptionText.text = text;

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
            if (_tooltipRoot != null) _tooltipRoot.SetActive(false);
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
            if (_equipmentSection != null)
                _equipmentSection.SetActive(true);
            if (_consumableSection != null)
                _consumableSection.SetActive(false);
            if (_comparisonSection != null)
                _comparisonSection.SetActive(comparisonItem != null);

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
                SetComparison(item, comparisonItem);
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
                _rarityBorder.color = itemData.ItemRarity.GetDefaultColor();

            if (_descriptionText != null)
                _descriptionText.text = itemData.Description;

            if (_flavorText != null)
            {
                _flavorText.text = itemData.FlavorText;
                _flavorText.gameObject.SetActive(!string.IsNullOrEmpty(itemData.FlavorText));
            }
        }

        private void SetEquipmentInfo(InventoryItem item, EquipmentData itemData)
        {
            if (_equipTypeText != null)
                _equipTypeText.text = itemData.EquipmentType.GetDisplayName();

            if (_levelText != null)
                _levelText.text = $"Level {item.Level}/{itemData.MaxLevel}";

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
                _durabilityText.text = $"{item.CurrentDurability}/{item.MaxDurability}";
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
                if (entryObj.TryGetComponent<TooltipStatEntryUI>(out var entryUI))
                {
                    float compareValue = comparisonAttr?.GetValueOrDefault(kvp.Key, 0) ?? 0;
                    entryUI.Initialize(kvp.Key.GetDisplayName(), kvp.Value, compareValue);
                }
            }

            foreach (var kvp in bonuses.OrderByDescending(k => Math.Abs(k.Value)))
            {
                var entryObj = Instantiate(_statEntryPrefab, _mainStatsContainer);
                if (entryObj.TryGetComponent<TooltipStatEntryUI>(out var entryUI))
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
            if (_tooltipRoot == null)
            {
                Debug.LogError("[TooltipUI] Tooltip Root is NULL!");
                return;
            }

            _tooltipRoot.SetActive(true);
            
            // Force ContentSizeFitter / LayoutGroup to calculate final size
            Canvas.ForceUpdateCanvases();
            if (_tooltipRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect);

            _isVisible = true;
            UpdatePosition(screenPosition);
        }

        private void UpdatePosition(Vector3? screenPosition = null)
        {
            if (_tooltipRect == null || _tooltipCanvas == null) return;
            Vector2 screenPos = screenPosition.HasValue
                ? (Vector2)screenPosition.Value
                : Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : Vector2.zero;

            RectTransform canvasRect = _tooltipCanvas.transform as RectTransform;
            if (canvasRect == null) return;

            UnityEngine.Camera uiCamera = _tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _tooltipCanvas.worldCamera;

            // Convert mouse screen position -> canvas local position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                uiCamera,
                out Vector2 localPos
            );

            // Mouse offset
            localPos += _offset;
            // Actual tooltip size after ContentSizeFitter/LayoutGroup
            Vector2 size = _tooltipRect.rect.size;
            // Canvas boundaries
            Rect canvasBounds = canvasRect.rect;

            float halfWidth = size.x * _tooltipRect.pivot.x;
            float halfHeight = size.y * _tooltipRect.pivot.y;

            // Keep right side inside canvas
            if (localPos.x + (size.x * (1f - _tooltipRect.pivot.x)) > canvasBounds.xMax)
                localPos.x = canvasBounds.xMax - size.x * (1f - _tooltipRect.pivot.x);
            // Keep left side inside canvas
            if (localPos.x - halfWidth < canvasBounds.xMin)
                localPos.x = canvasBounds.xMin + halfWidth;
            // Keep top side inside canvas
            if (localPos.y + (size.y * (1f - _tooltipRect.pivot.y)) > canvasBounds.yMax)
                localPos.y = canvasBounds.yMax - size.y * (1f - _tooltipRect.pivot.y);
            // Keep bottom side inside canvas
            if (localPos.y - halfHeight < canvasBounds.yMin)
                localPos.y = canvasBounds.yMin + halfHeight;

            _tooltipRect.localPosition = localPos;
        }
        #endregion

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

}