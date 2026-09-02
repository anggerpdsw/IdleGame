using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Manager;
using UnityEngine.UI;
using TMPro;

namespace IdleDefenseSurvival.SkillTree
{
    /// <summary>
    /// A single skill choice skill in the SkillTreeBonus UI.
    /// Displays skill name, allocated points, bonus info, and selection state.
    /// </summary>
    public class SkillTreeChoiceSkillUI : MonoBehaviour
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
            if (_manager != null)
                _manager.OnSelectionChanged -= RefreshDisplay;
        }

        /// <summary>
        /// Initialize this skill with a skill type.
        /// </summary>
        public void Initialize(SkillType skillType, SkillTreeBonusManager manager)
        {
            _skillType = skillType;
            _manager = manager;
            _isSelected = false;

            if (_manager != null)
                _manager.OnSelectionChanged += RefreshDisplay;

            RefreshDisplay();
        }

        /// <summary>
        /// Refresh the skill display based on current state.
        /// </summary>
        public void RefreshDisplay()
        {
            if (_manager == null) return;

            // Update skill name
            if (_skillNameText != null)
                _skillNameText.text = _skillType.GetSkillDisplayName();

            // Update allocated points
            var allocatedPoints = _manager.GetAllocatedPoints(_skillType);
            if (_allocatedPointsText != null)
                _allocatedPointsText.text = $"Points: {allocatedPoints}";

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
                _totalBonusText.text = $"Total: +{totalBonus:F1}";

            // Update selection indicator
            var selectedChoices = _manager.GetSelectedChoices();
            _isSelected = selectedChoices.Contains(_skillType);

            if (_selectionIndicator != null)
                _selectionIndicator.color = _isSelected ? _selectedColor : _unselectedColor;

            // Update button interactability
            // Selected: always interactable (can deselect)
            // Unselected: only interactable if under selection limit (< 3)
            if (_selectButton != null)
                _selectButton.interactable = _isSelected || _manager.CanSelectMoreSkills();
        }

        /// <summary>
        /// Called when the select button is clicked.
        /// </summary>
        private void OnSelectClicked()
        {
            if (_manager == null) return;

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