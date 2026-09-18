using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Pet;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Single pet slot in collection view.
    /// Shows pet icon, name, level, equipped state.
    /// Click to equip/unequip.
    /// </summary>
    public class PetSlotUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private GameObject _equippedIndicator;
        [SerializeField] private Button _button;
        [SerializeField] private Image _rarityBorder;

        private PetRuntime _pet;

        public void Setup(PetRuntime pet, Sprite icon)
        {
            _pet = pet;

            if (_icon != null) _icon.sprite = icon;
            if (_nameText != null) _nameText.text = pet.Definition.name;
            if (_levelText != null) _levelText.text = $"Lv.{pet.Level}";

            RefreshEquipState();

            _button?.onClick.RemoveAllListeners();
            _button?.onClick.AddListener(OnClick);
        }

        public void RefreshEquipState()
        {
            bool equipped = PetManager.Instance?.GetActivePets()?.Contains(_pet) ?? false;
            _equippedIndicator?.SetActive(equipped);
        }

        private void OnClick()
        {
            if (_pet == null || PetManager.Instance == null) return;

            bool equipped = PetManager.Instance.GetActivePets().Contains(_pet);
            if (equipped)
                PetManager.Instance.UnequipPet(_pet.InstanceId);
            else
                PetManager.Instance.EquipPet(_pet.InstanceId);
        }
    }
}
