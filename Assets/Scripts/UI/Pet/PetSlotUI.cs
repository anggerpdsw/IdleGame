using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Pet;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Single pet slot in collection view.
    /// Shows pet icon, name, level, equipped state, owned/locked state.
    /// </summary>
    public class PetSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private GameObject _equippedIndicator;
        [SerializeField] private GameObject _lockIcon;
        [SerializeField] private Button _button;
        [SerializeField] private Image _rarityBorder;
        [SerializeField] private CanvasGroup _canvasGroup;

        private string _petId;
        private PetRuntime _pet;
        private bool _isOwned;
        private string _rarity;
        private Action<string> _onSelected;

        public void Setup(string petId, PetDefinition definition, bool isOwned, Sprite icon, Action<string> onSelected = null)
        {
            _petId = petId;
            _isOwned = isOwned;
            _onSelected = onSelected;
            _rarity = definition.rarity;

            if (_icon != null) _icon.sprite = icon;
            if (_nameText != null) _nameText.text = definition.name;

            RefreshOwnershipVisual();
            RefreshLevel();
            RefreshEquipState();
            RefreshRarity();

            _button?.onClick.RemoveAllListeners();
            _button?.onClick.AddListener(OnClick);
        }

        public void SetupRuntime(PetRuntime pet, Sprite icon, Action<string> onSelected = null)
        {
            _pet = pet;
            _petId = pet.PetId;
            _isOwned = true;
            _onSelected = onSelected;
            _rarity = pet.Definition.rarity;

            if (_icon != null) _icon.sprite = icon;
            if (_nameText != null) _nameText.text = pet.Definition.name;
            if (_levelText != null) _levelText.text = $"Lv.{pet.Level}";

            RefreshOwnershipVisual();
            RefreshEquipState();
            RefreshRarity();

            _button?.onClick.RemoveAllListeners();
            _button?.onClick.AddListener(OnClick);
        }

        private void RefreshOwnershipVisual()
        {
            if (_lockIcon != null) _lockIcon.SetActive(!_isOwned);
            if (_icon != null) _icon.gameObject.SetActive(_isOwned);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _isOwned ? 1f : 0.65f;
                _canvasGroup.interactable = _isOwned;
            }
        }

        private void RefreshLevel()
        {
            if (_levelText == null) return;

            if (!_isOwned)
            {
                _levelText.text = string.Empty;
                return;
            }

            if (_pet != null)
                _levelText.text = $"Lv.{_pet.Level}";
            else
                _levelText.text = string.Empty;
        }

        public void RefreshEquipState()
        {
            if (!_isOwned)
            {
                _equippedIndicator?.SetActive(false);
                return;
            }

            bool equipped = PetManager.Instance?.IsPetEquipped(_petId) ?? false;
            _equippedIndicator?.SetActive(equipped);
        }

        private void RefreshRarity()
        {
            if (_rarityBorder == null || string.IsNullOrEmpty(_rarity)) return;
            _rarityBorder.sprite = CardResources.GetFrame(_rarity);
        }

        private void OnClick()
        {
            // Only show detail panel - equip/unequip handled by PetDetailUI button
            _onSelected?.Invoke(_petId);
        }
    }
}
