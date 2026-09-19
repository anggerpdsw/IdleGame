using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Pet;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Shows detailed info for selected pet.
    /// Mirrors CardDetailUI pattern.
    ///
    /// Displays:
    /// - Pet identity (name, description, role, rarity)
    /// - Current stats (level, experience, stamina)
    /// - Base stats (attack, attack speed, health, move speed)
    /// - Skills (basic/active/passive/evolution)
    /// - Equipped state
    ///
    /// Does NOT modify pet data or perform equip/unequip.
    /// </summary>
    public class PetDetailUI : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private Image _petIcon;
        [SerializeField] private Image _petFrame;
        [SerializeField] private TextMeshProUGUI _petName;
        [SerializeField] private TextMeshProUGUI _petDescription;
        [SerializeField] private TextMeshProUGUI _petRole;
        [SerializeField] private TextMeshProUGUI _petRarity;

        [Header("Progression")]
        [SerializeField] private TextMeshProUGUI _petLevel;
        [SerializeField] private TextMeshProUGUI _petExperience;
        [SerializeField] private TextMeshProUGUI _evolutionStage;

        [Header("Base Stats")]
        [SerializeField] private TextMeshProUGUI _attackText;
        [SerializeField] private TextMeshProUGUI _attackSpeedText;
        [SerializeField] private TextMeshProUGUI _healthText;
        [SerializeField] private TextMeshProUGUI _moveSpeedText;
        [SerializeField] private TextMeshProUGUI _targetRangeText;

        [Header("Skills")]
        [SerializeField] private TextMeshProUGUI _basicSkillText;
        [SerializeField] private TextMeshProUGUI _activeSkillText;
        [SerializeField] private TextMeshProUGUI _passiveSkillText;
        [SerializeField] private TextMeshProUGUI _evolutionSkillText;

        [Header("State")]
        [SerializeField] private GameObject _equippedIndicator;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private GameObject _detailRoot;

        [Header("Actions")]
        [SerializeField] private Button _equipButton;
        [SerializeField] private TextMeshProUGUI _equipButtonText;

        private string _currentPetId;
        private bool _isOwned;

        private void OnEnable()
        {
            _equipButton?.onClick.AddListener(OnEquipButtonClick);

            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped += HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped += HandlePetUnequipped;
            }
        }

        private void OnDisable()
        {
            _equipButton?.onClick.RemoveListener(OnEquipButtonClick);

            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped -= HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped -= HandlePetUnequipped;
            }
        }

        public void Show(string petId, PetDefinition definition, bool isOwned, Sprite icon)
        {
            if (definition == null) { Hide(); return; }

            _currentPetId = petId;
            _isOwned = isOwned;

            if (_detailRoot != null) _detailRoot.SetActive(true);

            RefreshIdentity(definition, icon);
            RefreshProgression(petId, isOwned);
            RefreshBaseStats(definition);
            RefreshSkills(definition);
            RefreshState(petId, isOwned);
        }

        public void Hide()
        {
            _currentPetId = null;
            if (_detailRoot != null) _detailRoot.SetActive(false);
        }

        private void RefreshIdentity(PetDefinition definition, Sprite icon)
        {
            if (_petIcon != null) _petIcon.sprite = icon;
            if (_petName != null) _petName.text = definition.name;
            if (_petDescription != null) _petDescription.text = definition.description;
            if (_petRole != null) _petRole.text = $"Role: {definition.role}";
            if (_petRarity != null)
            {
                _petRarity.text = definition.rarity;
                _petRarity.color = GetRarityColor(definition.rarity);
            }
        }

        private void RefreshProgression(string petId, bool isOwned)
        {
            if (!isOwned)
            {
                if (_petLevel != null) _petLevel.text = "Locked";
                if (_petExperience != null) _petExperience.text = string.Empty;
                if (_evolutionStage != null) _evolutionStage.text = string.Empty;
                return;
            }

            var saveData = PetManager.Instance?.GetSaveData();
            var entry = saveData?.Find(e => e.petId == petId);

            if (entry != null)
            {
                if (_petLevel != null) _petLevel.text = $"Lv. {entry.level}";
                if (_petExperience != null) _petExperience.text = $"XP: {entry.experience:N0}";
                if (_evolutionStage != null)
                    _evolutionStage.text = entry.evolutionStage > 0 ? "Evolved" : "Base Form";
            }
        }

        private void RefreshBaseStats(PetDefinition definition)
        {
            var stats = definition.baseStats;
            if (_attackText != null) _attackText.text = $"ATK: {stats.attack}";
            if (_attackSpeedText != null) _attackSpeedText.text = $"ATK Speed: {stats.attackSpeed:F1}";
            if (_healthText != null) _healthText.text = $"HP: {stats.health}";
            if (_moveSpeedText != null) _moveSpeedText.text = $"Move: {stats.moveSpeed}";
            if (_targetRangeText != null) _targetRangeText.text = $"Range: {stats.targetRange}";
        }

        private void RefreshSkills(PetDefinition definition)
        {
            var skills = definition.skills;
            if (_basicSkillText != null) _basicSkillText.text = skills.basic ?? "None";
            if (_activeSkillText != null) _activeSkillText.text = skills.active ?? "None";
            if (_passiveSkillText != null) _passiveSkillText.text = skills.passive ?? "None";
            if (_evolutionSkillText != null)
                _evolutionSkillText.text = $"{skills.evolution ?? "None"} (Lv.{definition.evolutionRequirement.level})";
        }

        private void RefreshState(string petId, bool isOwned)
        {
            if (_lockedOverlay != null) _lockedOverlay.SetActive(!isOwned);

            bool isEquipped = isOwned && (PetManager.Instance?.IsPetEquipped(petId) ?? false);
            if (_equippedIndicator != null) _equippedIndicator.SetActive(isEquipped);

            if (_equipButton != null) _equipButton.interactable = isOwned;
            if (_equipButtonText != null)
            {
                if (!isOwned) _equipButtonText.text = "Locked";
                else _equipButtonText.text = isEquipped ? "Unequip" : "Equip";
            }
        }

        private void OnEquipButtonClick()
        {
            if (!_isOwned || string.IsNullOrEmpty(_currentPetId) || PetManager.Instance == null) return;

            var saveData = PetManager.Instance.GetSaveData();
            var entry = saveData.Find(e => e.petId == _currentPetId);
            if (entry == null) return;

            bool isEquipped = PetManager.Instance.IsPetEquipped(_currentPetId);
            if (isEquipped)
                PetManager.Instance.UnequipPet(entry.instanceId);
            else
                PetManager.Instance.EquipPet(entry.instanceId);
        }

        private void HandlePetEquipped(PetRuntime pet)
        {
            if (pet.PetId == _currentPetId)
                RefreshState(_currentPetId, _isOwned);
        }

        private void HandlePetUnequipped(PetRuntime pet)
        {
            if (pet.PetId == _currentPetId)
                RefreshState(_currentPetId, _isOwned);
        }

        private Color GetRarityColor(string rarity)
        {
            return rarity switch
            {
                "Common" => Color.white,
                "Rare" => new Color(0.2f, 0.6f, 1f), // Blue
                "Epic" => new Color(0.6f, 0.2f, 1f), // Purple
                "Legendary" => new Color(1f, 0.5f, 0f), // Orange
                "Mythic" => new Color(1f, 0.2f, 0.2f), // Red
                _ => Color.white
            };
        }
    }
}
