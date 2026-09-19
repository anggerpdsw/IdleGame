using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Pet;
using TMPro;
using IdleDefenseSurvival.Core;

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
        [SerializeField] private SpriteRenderer _petIcon;
        [SerializeField] private SpriteRenderer _skillCooldownOverlay;
        [SerializeField] private GameObject _emergencyIndicator;
        [SerializeField] private SpriteRenderer _staminaBar;
        [SerializeField] private TextMeshProUGUI _staminaText;

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
            _petIcon.sprite = PetResources.GetPetIcon(_currentPet.PetId);

            SetVisible(true);
        }

        private void UpdateCooldownOverlay()
        {
            if (_currentPet == null || _skillCooldownOverlay == null) return;

            // Display active skill cooldown (VoidPulse for Voidling)
            string activeSkillId = _currentPet.Definition?.skills?.active;
            if (string.IsNullOrEmpty(activeSkillId))
            {
                _skillCooldownOverlay.transform.localScale = Vector3.zero;
                return;
            }

            // Calculate cooldown percentage
            if (_currentPet.Cooldowns.TryGetValue(activeSkillId, out float remaining))
            {
                // Get base cooldown from skill definition
                float baseCooldown = _currentPet.GetActiveSkillCooldown();
                if (baseCooldown > 0f)
                {
                    float fillAmount = remaining / baseCooldown;
                    Vector3 scale = _skillCooldownOverlay.transform.localScale;
                    scale.x = Mathf.Clamp01(fillAmount);
                    _skillCooldownOverlay.transform.localScale = scale;
                }
                else
                {
                    _skillCooldownOverlay.transform.localScale = Vector3.zero;
                }
            }
            else
            {
                _skillCooldownOverlay.transform.localScale = Vector3.zero;
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

        public void RefreshStamina(PetRuntime pet)
        {
            if (_staminaBar == null) return;
            // SpriteRenderer support fillAmount kalau sprite type = Filled
            // Atau pakai scale X:
            Vector3 scale = _staminaBar.transform.localScale;
            scale.x = pet.StaminaPercent;
            _staminaBar.transform.localScale = scale;

            _staminaText.text = $"{pet.CurrentStamina:F0}/{pet.MaxStamina:F0}";
        }

    }
}
