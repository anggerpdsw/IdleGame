using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Pet;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Pet UI panel - displays pet icon, skill cooldown, emergency indicator.
    /// Zero gameplay logic - pure presentation layer.
    /// Subscribes to PetManager events for updates.
    /// </summary>
    public class PetUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _petIcon;
        [SerializeField] private Image _skillCooldownOverlay;
        [SerializeField] private GameObject _emergencyIndicator;

        [Header("Configuration")]
        [SerializeField] private Sprite _voidlingIcon;

        private PetRuntime _currentPet;
        private bool _isVisible;

        private void Start()
        {
            // Subscribe to PetManager events
            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped += HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped += HandlePetUnequipped;
                PetManager.Instance.OnPetStateChanged += HandlePetStateChanged;
            }

            // Initialize hidden
            SetVisible(false);

            // Check if any pet already equipped
            RefreshPetDisplay();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (PetManager.Instance != null)
            {
                PetManager.Instance.OnPetEquipped -= HandlePetEquipped;
                PetManager.Instance.OnPetUnequipped -= HandlePetUnequipped;
                PetManager.Instance.OnPetStateChanged -= HandlePetStateChanged;
            }
        }

        private void Update()
        {
            if (!_isVisible || _currentPet == null) return;

            // Update skill cooldown overlay
            UpdateCooldownOverlay();

            // Update emergency indicator
            UpdateEmergencyIndicator();
        }

        private void HandlePetEquipped(PetRuntime pet)
        {
            _currentPet = pet;
            RefreshPetDisplay();
            SetVisible(true);
        }

        private void HandlePetUnequipped(PetRuntime pet)
        {
            if (_currentPet == pet)
            {
                _currentPet = null;
                SetVisible(false);
            }
        }

        private void HandlePetStateChanged(PetRuntime pet)
        {
            if (_currentPet == pet)
            {
                // Visual feedback for state changes
                UpdateEmergencyIndicator();
            }
        }

        private void RefreshPetDisplay()
        {
            var activePets = PetManager.Instance?.GetActivePets();
            if (activePets == null || activePets.Count == 0)
            {
                _currentPet = null;
                SetVisible(false);
                return;
            }

            // Display first equipped pet (can be extended for multiple pets)
            _currentPet = activePets[0];

            // Set pet icon based on pet ID
            if (_currentPet.PetId == "pet_voidling" && _voidlingIcon != null)
            {
                _petIcon.sprite = _voidlingIcon;
            }

            SetVisible(true);
        }

        private void UpdateCooldownOverlay()
        {
            if (_currentPet == null || _skillCooldownOverlay == null) return;

            // Display active skill cooldown (VoidPulse for Voidling)
            string activeSkillId = _currentPet.Definition?.skills?.active;
            if (string.IsNullOrEmpty(activeSkillId))
            {
                _skillCooldownOverlay.fillAmount = 0f;
                return;
            }

            // Calculate cooldown percentage
            if (_currentPet.Cooldowns.TryGetValue(activeSkillId, out float remaining))
            {
                // Get base cooldown from skill definition
                float baseCooldown = GetSkillCooldown(activeSkillId);
                if (baseCooldown > 0f)
                {
                    float fillAmount = remaining / baseCooldown;
                    _skillCooldownOverlay.fillAmount = Mathf.Clamp01(fillAmount);
                }
                else
                {
                    _skillCooldownOverlay.fillAmount = 0f;
                }
            }
            else
            {
                _skillCooldownOverlay.fillAmount = 0f;
            }
        }

        private void UpdateEmergencyIndicator()
        {
            if (_currentPet == null || _emergencyIndicator == null) return;

            // Show red glow when pet in emergency mode
            bool isEmergency = _currentPet.IsEmergencyMode;
            _emergencyIndicator.SetActive(isEmergency);
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }
        }

        private float GetSkillCooldown(string skillId)
        {
            // Hardcoded skill cooldowns - ideally load from skill definitions
            return skillId switch
            {
                "void_pulse" => 12f,
                "black_hole" => 25f,
                _ => 0f
            };
        }
    }
}
