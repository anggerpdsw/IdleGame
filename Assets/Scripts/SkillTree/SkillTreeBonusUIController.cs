using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IdleDefenseSurvival.SkillTree
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
        [SerializeField] private bool _debug = false;
        [SerializeField] private GameObject _skillTreeBonus;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Transform _choicesSkillContainer;
        [SerializeField] private TextMeshProUGUI _selectionCountText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private RectTransform _content;

        /// <summary>Prefab for a single skill choice skill.</summary>
        [SerializeField] private SkillTreeChoiceSkillUI _choiceSkillPrefab;

        private SkillTreeBonusManager _manager;
        private List<SkillTreeChoiceSkillUI> _choiceSkills = new();
        private bool _isOpen = false;

        private void Start() => RefreshSkillTreeBonus();   
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

        private void RefreshSkillTreeBonus()
        {
            if (_skillTreeBonus != null) _skillTreeBonus.SetActive(_isOpen);
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (_content == null) return;
            Canvas.ForceUpdateCanvases();

            if (_debug) Debug.Log(
                $"[SkillTreeBonusUI] " +
                $"_skillTreeBonus Active: {_skillTreeBonus != null && _skillTreeBonus.activeSelf}, " +
                $"Content Height: {_content.rect.height}"
            );

            if (_content != null)
            {
                for (int i = 0; i < _content.childCount; i++)
                {
                    var child = _content.GetChild(i) as RectTransform;

                    if (_debug) Debug.Log(
                        $"[Content Child] {child.name} | " +
                        $"Active={child.gameObject.activeSelf} | " +
                        $"Height={child.rect.height} | " +
                        $"Preferred={LayoutUtility.GetPreferredHeight(child)} | " +
                        $"Min={LayoutUtility.GetMinHeight(child)}"
                    );
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        /// <summary>
        /// Open the SkillTreeBonus selection UI.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;
            if (_manager == null) return;

            if (!_manager.OpenSkillTreeSelection())
            {
                if (_debug) Debug.LogWarning("[SkillTreeBonusUI] Failed to open skill tree selection");
                return;
            }

            _isOpen = true;

            RefreshSkillTreeBonus();
            RefreshChoicesDisplay();
            RefreshSelectionCount();
        }

        /// <summary>
        /// Close the SkillTreeBonus selection UI.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            RefreshSkillTreeBonus();
        }

        /// <summary>
        /// Refresh the display of available choices.
        /// </summary>
        private void RefreshChoicesDisplay()
        {
            if (_manager == null) return;
            var choices = _manager.GetPendingChoices();
            // Clear old skills
            foreach (var skill in _choiceSkills)
                if (skill != null) Destroy(skill.gameObject);
            _choiceSkills.Clear();

            if (_choicesSkillContainer == null) return;

            // Clear existing skills
            foreach (Transform child in _choicesSkillContainer)
                Destroy(child.gameObject);

            // Create skills for each choice
            foreach (var choice in choices)
            {
                var skill = Instantiate(_choiceSkillPrefab, _choicesSkillContainer);
                skill.Initialize(choice, _manager);
                _choiceSkills.Add(skill);
            }

            // Update title
            if (_titleText != null)
                _titleText.text = $"Skill Tree Bonus (Select up to 3)";

            RefreshLayout();
        }

        /// <summary>
        /// Refresh the selection count display.
        /// Confirm button only enabled when EXACTLY 3 skills are selected.
        /// </summary>
        private void RefreshSelectionCount()
        {
            if (_manager == null) return;
            var selected = _manager.GetSelectedCount();
            if (_selectionCountText != null)
                _selectionCountText.text = $"Confirm: {selected} / 3";
            // Update confirm button state - only enabled when EXACTLY 3 selected
            if (_confirmButton != null)
                _confirmButton.interactable = selected == 3;
        }

        /// <summary>
        /// Called when Confirm button is clicked.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (_manager == null) return;
            if (!_manager.ConfirmSelection())
            {
                if (_debug) Debug.LogWarning("[SkillTreeBonusUI] Failed to confirm selection");
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
        private void OnCloseClicked() => Close();
    }

}
