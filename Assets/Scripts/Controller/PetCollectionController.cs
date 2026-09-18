using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Pet;
using IdleDefenseSurvival.UI;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Pet Collection scene controller.
    /// Displays owned pets, allows equip/unequip.
    /// </summary>
    public class PetCollectionController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform _petSlotParent;
        [SerializeField] private PetSlotUI _petSlotPrefab;
        [SerializeField] private Button _backButton;

        [Header("Pet Icons")]
        [SerializeField] private Sprite _voidlingIcon;
        [SerializeField] private Sprite _emberFoxIcon;
        [SerializeField] private Sprite _thunderPupIcon;
        [SerializeField] private Sprite _bloodBatIcon;
        [SerializeField] private Sprite _ironTortoiseIcon;

        private List<PetSlotUI> _slots = new();

        public void OnBack() => SceneLoader.Instance.ReturnToMainMenuFromPetCollection();

        private void Start()
        {
            RefreshPetList();
        }

        private void OnEnable()
        {
            _backButton?.onClick.AddListener(OnBack);

            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped += HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped += HandlePetUnequipped;
            }
        }

        private void OnDisable()
        {
            _backButton?.onClick.RemoveListener(OnBack);

            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped -= HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped -= HandlePetUnequipped;
            }
        }

        private void RefreshPetList()
        {
            // Clear existing slots
            foreach (var slot in _slots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            _slots.Clear();

            if (PetManager.Instance == null) return;

            // Get all owned pets via save data (no public GetAllOwnedPets method)
            var saveData = PetManager.Instance.GetSaveData();
            foreach (var entry in saveData)
            {
                var pet = PetManager.Instance.GetOwnedPet(entry.instanceId);
                if (pet == null) continue;

                var slot = Instantiate(_petSlotPrefab, _petSlotParent);
                slot.Setup(pet, GetPetIcon(pet.PetId));
                _slots.Add(slot);
            }
        }

        private void HandlePetEquipped(PetRuntime pet)
        {
            RefreshAllSlots();
        }

        private void HandlePetUnequipped(PetRuntime pet)
        {
            RefreshAllSlots();
        }

        private void RefreshAllSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot != null) slot.RefreshEquipState();
            }
        }

        // Debug: grant test pets
        [ContextMenu("Grant All Test Pets")]
        private void DebugGrantAllPets()
        {
            if (PetManager.Instance == null) return;

            PetManager.Instance.GrantPet("pet_voidling", 1);
            PetManager.Instance.GrantPet("pet_ember_fox", 1);
            PetManager.Instance.GrantPet("pet_thunder_pup", 1);
            PetManager.Instance.GrantPet("pet_blood_bat", 1);
            PetManager.Instance.GrantPet("pet_iron_tortoise", 1);

            RefreshPetList();
        }

        private Sprite GetPetIcon(string petId)
        {
            return petId switch
            {
                "pet_voidling" => _voidlingIcon,
                "pet_ember_fox" => _emberFoxIcon,
                "pet_thunder_pup" => _thunderPupIcon,
                "pet_blood_bat" => _bloodBatIcon,
                "pet_iron_tortoise" => _ironTortoiseIcon,
                _ => null
            };
        }
    }
}
