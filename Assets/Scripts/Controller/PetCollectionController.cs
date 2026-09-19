using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleDefenseSurvival.Pet;
using IdleDefenseSurvival.UI;
using IdleDefenseSurvival.Core;

namespace IdleDefenseSurvival.Controller
{
    /// <summary>
    /// Pet Collection scene controller.
    /// Mirrors Card Collection pattern: show ALL pets (locked if unowned) + equipped section.
    /// </summary>
    public class PetCollectionController : MonoBehaviour
    {
        [Header("UI Content")]
        [SerializeField] private Transform _allPetContent;
        [SerializeField] private Transform _equippedPetContent;
        [SerializeField] private PetSlotUI _petSlotPrefab;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _addSlotButton;
        [SerializeField] private TextMeshProUGUI _slotMaxCount;
        [SerializeField] private TextMeshProUGUI _nextSlotCostGem;
        [SerializeField] private PetDetailUI _petDetailUI;

        private List<PetSlotUI> _allPetSlots = new();
        private List<PetSlotUI> _equippedPetSlots = new();

        public void OnBack() => SceneLoader.Instance.ReturnToMainMenuFromPetCollection();
        public void OnAddSlot() => PetManager.Instance?.ExpandSlot();

        private void Start()
        {
            RefreshSlotInfo();
            Refresh();
        }

        private void OnEnable()
        {
            _backButton?.onClick.AddListener(OnBack);
            _addSlotButton?.onClick.AddListener(OnAddSlot);

            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped += HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped += HandlePetUnequipped;
                PetManager.Instance.OnSlotExpanded += RefreshSlotInfo;
            }
        }

        private void OnDisable()
        {
            _backButton?.onClick.RemoveListener(OnBack);
            _addSlotButton?.onClick.RemoveListener(OnAddSlot);

            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped -= HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped -= HandlePetUnequipped;
                PetManager.Instance.OnSlotExpanded -= RefreshSlotInfo;
            }
        }

        private void Refresh()
        {
            ClearAllContent();
            ClearEquippedContent();
            BuildAllPetContent();
            BuildEquippedPetContent();
            RefreshSlotInfo();
        }

        private void RefreshSlotInfo()
        {
            if (PetManager.Instance == null) return;

            if (_slotMaxCount != null)
                _slotMaxCount.text = $"{PetManager.Instance.EquippedPetCount}/{PetManager.Instance.UnlockedSlotCount}";

            if (_nextSlotCostGem != null)
            {
                _nextSlotCostGem.text =
                    PetManager.Instance.UnlockedSlotCount == PetManager.Instance.MaxSlots
                    ? "MAX"
                    : $"{PetManager.Instance.NextSlotCostGem}";
            }
        }

        private void BuildAllPetContent()
        {
            if (_allPetContent == null || PetManager.Instance == null) return;

            var allDefinitions = PetManager.Instance.GetAllDefinitions();
            foreach (var pair in allDefinitions)
            {
                string petId = pair.Key;
                PetDefinition def = pair.Value;
                bool isOwned = PetManager.Instance.HasPet(petId);
                bool isEquipped = PetManager.Instance.IsPetEquipped(petId);

                var slot = Instantiate(_petSlotPrefab, _allPetContent);
                slot.Setup(petId, def, isOwned, PetResources.GetPetIcon(petId), HandlePetSelected);
                slot.RefreshEquipState();
                _allPetSlots.Add(slot);
            }
        }

        private void BuildEquippedPetContent()
        {
            if (_equippedPetContent == null || PetManager.Instance == null) return;

            var allDefinitions = PetManager.Instance.GetAllDefinitions();
            foreach (var pair in allDefinitions)
            {
                string petId = pair.Key;
                PetDefinition def = pair.Value;

                if (!PetManager.Instance.IsPetEquipped(petId)) continue;
                if (!PetManager.Instance.HasPet(petId)) continue;

                var slot = Instantiate(_petSlotPrefab, _equippedPetContent);
                slot.Setup(petId, def, true, PetResources.GetPetIcon(petId), HandlePetSelected);
                slot.RefreshEquipState();
                _equippedPetSlots.Add(slot);
            }
        }

        private void HandlePetSelected(string petId)
        {
            if (_petDetailUI == null || PetManager.Instance == null) return;

            var definition = PetManager.Instance.GetPetDefinition(petId);
            if (definition == null) return;

            bool isOwned = PetManager.Instance.HasPet(petId);
            Sprite icon = PetResources.GetPetIcon(petId);

            _petDetailUI.Show(petId, definition, isOwned, icon);
        }

        private void ClearAllContent()
        {
            foreach (var slot in _allPetSlots)
                if (slot != null) Destroy(slot.gameObject);
            _allPetSlots.Clear();
        }

        private void ClearEquippedContent()
        {
            foreach (var slot in _equippedPetSlots)
                if (slot != null) Destroy(slot.gameObject);
            _equippedPetSlots.Clear();
        }

        private void HandlePetEquipped(PetRuntime pet) => Refresh();
        private void HandlePetUnequipped(PetRuntime pet) => Refresh();

        [ContextMenu("Grant All Test Pets")]
        private void DebugGrantAllPets()
        {
            if (PetManager.Instance == null) return;

            PetManager.Instance.GrantPet("pet_voidling", 1);
            PetManager.Instance.GrantPet("pet_ember_fox", 1);
            PetManager.Instance.GrantPet("pet_thunder_pup", 1);
            PetManager.Instance.GrantPet("pet_blood_bat", 1);
            PetManager.Instance.GrantPet("pet_iron_tortoise", 1);

            Refresh();
        }

    }
}
