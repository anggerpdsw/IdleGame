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
using IdleDefenseSurvival.Core;

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
        [SerializeField] private Slider _durabilityBar;
        [SerializeField] private TextMeshProUGUI _durabilityText;
        [SerializeField] private Transform _mainStatsContainer;
        [SerializeField] private GameObject _mainStatEntryPrefab;
        [SerializeField] private Transform _secondaryStatsContainer;
        [SerializeField] private GameObject _secondaryStatEntryPrefab;
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
        [SerializeField] private Vector2 _offset = new(10, -240);
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
            if (item.GetEquipmentData() is not EquipmentData itemData) return;

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
                string eq = "";
                if(item.IsEquippable()) eq = "/" + item.EquipmentType.GetDisplayName();

                _iconImage.sprite = ItemResources.GetItemSource(
                    $"{item.GetItemCategory()}{eq}/{item.ItemId}");

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
                _equipTypeText.text = item.EquipmentType.GetDisplayName();

            if (_levelText != null)
                _levelText.text = $"Lv. {item.Level}/{item.MaxLevel}";

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
            if (_mainStatsContainer == null || _mainStatEntryPrefab == null) return;

            // Clear existing Main Attribute
            foreach (Transform child in _mainStatsContainer)
                Destroy(child.gameObject);

            var bonuses = EquipmentComparer.GetTotalStatBonuses(item);
            var comparisonBonuses = comparisonItem != null ? EquipmentComparer.GetTotalStatBonuses(comparisonItem) : null;

            // Core attributes first (CON/STR/INT/DEX) — equipment identity.
            var attrBonuses = EquipmentStatCalculator.GetItemAttributeBonuses(item);
            var comparisonAttr = comparisonItem != null ? EquipmentStatCalculator.GetItemAttributeBonuses(comparisonItem) : null;

            foreach (var kvp in attrBonuses.OrderByDescending(k => Math.Abs(k.Value)))
            {
                var entryObj = Instantiate(_mainStatEntryPrefab, _mainStatsContainer);
                if (entryObj.TryGetComponent<TooltipStatEntryUI>(out var entryUI))
                {
                    float compareValue = comparisonAttr?.GetValueOrDefault(kvp.Key, 0) ?? 0;
                    entryUI.Initialize(kvp.Key.GetMainShortName(), kvp.Value, compareValue);
                }
            }

            if (_secondaryStatsContainer == null || _secondaryStatEntryPrefab == null) return;

            // Clear existing Secondary Attribute
            foreach (Transform child in _secondaryStatsContainer)
                Destroy(child.gameObject);

            foreach (var kvp in bonuses.OrderByDescending(k => Math.Abs(k.Value)))
            {
                var entryObj = Instantiate(_secondaryStatEntryPrefab, _secondaryStatsContainer);
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

            _specialEffectsContainer.gameObject.SetActive(false);
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
                _specialEffectsContainer.gameObject.SetActive(true);
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

            _setBonusContainer.gameObject.SetActive(pieceCount > 1);
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

            bool show = item.Sockets == null;
            _gemSocketsContainer.gameObject.SetActive(!show);
            if (show) return;

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
                : Pointer.current != null
                    ? Pointer.current.position.ReadValue()
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

            // Actual tooltip size after ContentSizeFitter/LayoutGroup
            Vector2 size = _tooltipRect.rect.size;
            // Canvas boundaries
            Rect canvasBounds = canvasRect.rect;

            float pivotX = _tooltipRect.pivot.x;
            float pivotY = _tooltipRect.pivot.y;
            float tooltipWidth = size.x;
            float tooltipHeight = size.y;

            // --- Horizontal: prefer right of cursor, flip left if no room ---
            float desiredX = localPos.x + _offset.x;
            float rightEdge = desiredX + tooltipWidth * (1f - pivotX);
            float leftEdge = desiredX - tooltipWidth * pivotX;

            if (rightEdge > canvasBounds.xMax)
                desiredX = canvasBounds.xMax - tooltipWidth * (1f - pivotX);
            else if (leftEdge < canvasBounds.xMin)
                desiredX = canvasBounds.xMin + tooltipWidth * pivotX;

            // --- Vertical: prefer below cursor (offset.y negative), flip above if no room ---
            float offsetY = _offset.y; // negative = below cursor
            float spaceBelow = canvasBounds.yMax - localPos.y;      // space from cursor to bottom of canvas
            float spaceAbove = localPos.y - canvasBounds.yMin;      // space from cursor to top of canvas
            float neededSpace = tooltipHeight + Mathf.Abs(offsetY); // tooltip height + gap from cursor

            bool flipUp = offsetY < 0 && spaceBelow < neededSpace && spaceAbove >= neededSpace;

            float desiredY = flipUp
                ? localPos.y - offsetY          // place above cursor (offsetY is negative, so minus = plus)
                : localPos.y + offsetY;         // place below cursor

            // Clamp vertical so whole tooltip stays inside canvas (fallback if both sides tight)
            float topEdge = desiredY + tooltipHeight * (1f - pivotY);
            float bottomEdge = desiredY - tooltipHeight * pivotY;

            if (topEdge > canvasBounds.yMax)
                desiredY = canvasBounds.yMax - tooltipHeight * (1f - pivotY);
            else if (bottomEdge < canvasBounds.yMin)
                desiredY = canvasBounds.yMin + tooltipHeight * pivotY;

            _tooltipRect.localPosition = new Vector2(desiredX, desiredY);
        }
        #endregion

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

}