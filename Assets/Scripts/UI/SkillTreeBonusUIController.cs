using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.SkillTree;
using IdleDefenseSurvival.Manager;
using UnityEngine.UI;
using TMPro;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// UI Controller for the SkillTreeBonus system.
    /// 
    /// Responsibilities:
    /// - Display skill choices (6 options per batch)
    /// - Handle skill selection (up to 3)
    /// - Show skill details (name, allocated points, bonus info)
    /// - Confirm selection
    /// - Close UI
    /// 
    /// Architecture:
    /// - Listens to SkillTreeBonusManager events
    /// - Updates UI state based on manager state
    /// - Sends user input to manager
    /// - Does not own business logic
    /// </summary>
    public class SkillTreeBonusUIController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Transform _choicesContainer;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _selectionCountText;
        [SerializeField] private Button _closeButton;

        /// <summary>Prefab for a single skill choice card.</summary>
        [SerializeField] private SkillTreeChoiceCardUI _choiceCardPrefab;

        private SkillTreeBonusManager _manager;
        private List<SkillTreeChoiceCardUI> _choiceCards = new();
        private bool _isOpen = false;

        private void OnEnable()
        {
            // Subscribe to manager events
            if (SkillTreeBonusManager.Instance != null)
            {
                _manager = SkillTreeBonusManager.Instance;
                _manager.OnPendingChoicesUpdated += RefreshChoicesDisplay;
                _manager.OnSelectionChanged += RefreshSelectionCount;
                _manager.OnConfirmed += OnConfirmedInternal;
            }

            // Setup button listeners
            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDisable()
        {
            // Unsubscribe
            if (_manager != null)
            {
                _manager.OnPendingChoicesUpdated -= RefreshChoicesDisplay;
                _manager.OnSelectionChanged -= RefreshSelectionCount;
                _manager.OnConfirmed -= OnConfirmedInternal;
            }

            // Remove button listeners
            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(OnConfirmClicked);

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary>
        /// Open the SkillTreeBonus selection UI.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
                return;

            if (_manager == null)
                return;

            if (!_manager.OpenSkillTreeSelection())
            {
                Debug.LogWarning("[SkillTreeBonusUI] Failed to open skill tree selection");
                return;
            }

            _isOpen = true;

            if (_panelRoot != null)
                _panelRoot.SetActive(true);

            RefreshChoicesDisplay();
            RefreshSelectionCount();
        }

        /// <summary>
        /// Close the SkillTreeBonus selection UI.
        /// </summary>
        public void Close()
        {
            _isOpen = false;

            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        /// <summary>
        /// Refresh the display of available choices.
        /// </summary>
        private void RefreshChoicesDisplay()
        {
            if (_manager == null)
                return;

            var choices = _manager.GetPendingChoices();

            // Clear old cards
            foreach (var card in _choiceCards)
            {
                Destroy(card.gameObject);
            }
            _choiceCards.Clear();

            if (_choicesContainer == null)
                return;

            // Create cards for each choice
            foreach (var choice in choices)
            {
                var card = Instantiate(_choiceCardPrefab, _choicesContainer);
                card.Initialize(choice, _manager);
                _choiceCards.Add(card);
            }

            // Update title
            if (_titleText != null)
            {
                _titleText.text = $"Skill Tree Bonus (Select up to 3)";
            }
        }

        /// <summary>
        /// Refresh the selection count display.
        /// Shows: selected / 3 | unspent / total earned | allocated
        /// </summary>
        private void RefreshSelectionCount()
        {
            if (_manager == null)
                return;

            var selected = _manager.GetSelectedCount();
            var totalEarned = _manager.TotalEarnedSkillPoints;
            var totalAllocated = _manager.GetTotalAllocatedSkillPoints();
            var unspent = _manager.UnspentSkillPoints;
            
            if (_selectionCountText != null)
            {
                _selectionCountText.text = $"Selected: {selected} / 3 | Unspent: {unspent} / {totalEarned} (Allocated: {totalAllocated})";
            }

            // Update confirm button state
            if (_confirmButton != null)
            {
                _confirmButton.interactable = selected >= 1 && selected <= 3;
            }
        }

        /// <summary>
        /// Called when Confirm button is clicked.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (_manager == null)
                return;

            if (!_manager.ConfirmSelection())
            {
                Debug.LogWarning("[SkillTreeBonusUI] Failed to confirm selection");
                return;
            }

            // If more points available, generate new batch
            if (_manager.HasUnspentSkillPoints())
            {
                RefreshChoicesDisplay();
                RefreshSelectionCount();
            }
            else
            {
                // All points used, close UI
                Close();
            }
        }

        /// <summary>
        /// Called when Confirm is successfully processed by manager.
        /// </summary>
        private void OnConfirmedInternal()
        {
            // Manager has already applied bonuses and refreshed stats
            // UI will be updated via RefreshChoicesDisplay/RefreshSelectionCount
        }

        /// <summary>
        /// Called when Close button is clicked.
        /// </summary>
        private void OnCloseClicked()
        {
            Close();
        }
    }

    /// <summary>
    /// A single skill choice card in the SkillTreeBonus UI.
    /// Displays skill name, allocated points, bonus info, and selection state.
    /// </summary>
    public class SkillTreeChoiceCardUI : MonoBehaviour
    {
        [SerializeField] private Button _selectButton;
        [SerializeField] private TextMeshProUGUI _skillNameText;
        [SerializeField] private TextMeshProUGUI _allocatedPointsText;
        [SerializeField] private TextMeshProUGUI _bonusPerPointText;
        [SerializeField] private TextMeshProUGUI _totalBonusText;
        [SerializeField] private Image _selectionIndicator;
        [SerializeField] private Color _selectedColor = Color.green;
        [SerializeField] private Color _unselectedColor = Color.white;

        private SkillType _skillType = SkillType.None;
        private SkillTreeBonusManager _manager;
        private bool _isSelected = false;

        private void OnEnable()
        {
            if (_selectButton != null)
                _selectButton.onClick.AddListener(OnSelectClicked);
        }

        private void OnDisable()
        {
            if (_selectButton != null)
                _selectButton.onClick.RemoveListener(OnSelectClicked);
        }

        /// <summary>
        /// Initialize this card with a skill type.
        /// </summary>
        public void Initialize(SkillType skillType, SkillTreeBonusManager manager)
        {
            _skillType = skillType;
            _manager = manager;
            _isSelected = false;

            RefreshDisplay();
        }

        /// <summary>
        /// Refresh the card display based on current state.
        /// </summary>
        public void RefreshDisplay()
        {
            if (_manager == null)
                return;

            // Update skill name
            if (_skillNameText != null)
            {
                _skillNameText.text = _skillType.GetSkillDisplayName();
            }

            // Update allocated points
            var allocatedPoints = _manager.GetAllocatedPoints(_skillType);
            if (_allocatedPointsText != null)
            {
                _allocatedPointsText.text = $"Points: {allocatedPoints}";
            }

            // Update bonus per point
            var loader = BaseStatLoader.Instance;
            if (loader != null)
            {
                var skillData = loader.GetSkillData(_skillType);
                if (skillData != null && _bonusPerPointText != null)
                {
                    _bonusPerPointText.text = $"Bonus/Pt: +{skillData.bonusPerPoint}";
                }
            }

            // Update total bonus
            var totalBonus = _manager.GetTotalBonus(_skillType);
            if (_totalBonusText != null)
            {
                _totalBonusText.text = $"Total: +{totalBonus:F1}";
            }

            // Update selection indicator
            var selectedChoices = new List<SkillType>(_manager.GetSelectedChoices());
            _isSelected = selectedChoices.Contains(_skillType);

            if (_selectionIndicator != null)
            {
                _selectionIndicator.color = _isSelected ? _selectedColor : _unselectedColor;
            }

            // Update button interactability
            if (_selectButton != null)
            {
                _selectButton.interactable = _isSelected || _manager.CanSelectMoreSkills();
            }
        }

        /// <summary>
        /// Called when the select button is clicked.
        /// </summary>
        private void OnSelectClicked()
        {
            if (_manager == null)
                return;

            if (_isSelected)
            {
                // Deselect
                _manager.DeselectSkill(_skillType);
            }
            else
            {
                // Select
                _manager.SelectSkill(_skillType);
            }

            RefreshDisplay();
        }
    }
}
