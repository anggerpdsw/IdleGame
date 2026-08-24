using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Ultimate;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Controller;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.UI.Game
{
    /// <summary>
    /// Controls the ultimate ability panel showing available ultimates with cooldowns.
    /// Supports both auto-cast (when AutoCastUltimate setting is on) and manual cast (user clicks button).
    /// Cooldowns are read directly from UltimateManager (single source of truth).
    /// </summary>
    public class UltimatePanelController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private UltimateUI _slotPrefab;
        [SerializeField] private RectTransform _slotContainer;
        [SerializeField] private GameObject _manualCastOverlay; // Optional: shows "Auto-cast OFF" hint

        private readonly List<UltimateUI> _slots = new();
        private readonly Dictionary<string, UltimateUI> _slotByUltimateId = new();
        private bool _isInitialized;

        private void Start()
        {
            Initialize();
            SubscribeToSettings();
            RefreshAll();
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            var ultimateManager = UltimateManager.Instance;
            if (ultimateManager == null) return;

            // Create a slot for each registered ultimate
            foreach (var ultimateId in ultimateManager.GetAllUltimateIds())
            {
                CreateUltimateSlot(ultimateId);
            }
        }

        private void SubscribeToSettings()
        {
            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.AutoCastUltimateChanged += OnAutoCastChanged;
                UpdateManualCastOverlay();
            }
        }

        private void OnAutoCastChanged(bool autoCast)
        {
            UpdateManualCastOverlay();
            UpdateAllSlotInteractable();
        }

        private void UpdateManualCastOverlay()
        {
            if (_manualCastOverlay != null)
            {
                bool autoCast = SettingsController.Instance != null && SettingsController.Instance.AutoCastUltimate;
                _manualCastOverlay.SetActive(!autoCast);
            }
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                SubscribeToSettings();
                RefreshAll();
            }
        }

        private void OnDisable()
        {
            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.AutoCastUltimateChanged -= OnAutoCastChanged;
            }
        }

        private void CreateUltimateSlot(string ultimateId)
        {
            var ultimateManager = UltimateManager.Instance;
            if (ultimateManager == null) return;

            if (!ultimateManager.TryGetUltimate(ultimateId, out var ultimateData)) return;

            var slotObject = _slotPrefab != null
                ? Instantiate(_slotPrefab.gameObject, _slotContainer)
                : new GameObject($"Ultimate_{ultimateId}", typeof(RectTransform));

            if (!slotObject.TryGetComponent<UltimateUI>(out var slot))
                slot = slotObject.AddComponent<UltimateUI>();

            slot.Initialize(ultimateId);
            slot.BindClick(() => OnUltimateClicked(ultimateId));
            slot.name = ultimateId;

            _slots.Add(slot);
            _slotByUltimateId[ultimateId] = slot;

            // Initialize cooldown visual from UltimateManager
            UpdateSlotCooldown(ultimateId);
        }

        private void OnUltimateClicked(string ultimateId)
        {
            // Only allow manual cast when AutoCastUltimate is OFF
            bool autoCast = SettingsController.Instance != null && SettingsController.Instance.AutoCastUltimate;
            if (autoCast) return;

            var player = Player.Player.Instance;
            if (player == null) return;

            player.ManualCastUltimate(ultimateId);
            // UltimateManager handles cooldown internally; just refresh visual next frame
        }

        private void RefreshAll()
        {
            var ultimateManager = UltimateManager.Instance;
            if (ultimateManager == null) return;

            // Ensure all ultimates have slots
            foreach (var ultimateId in ultimateManager.GetAllUltimateIds())
            {
                if (!_slotByUltimateId.ContainsKey(ultimateId))
                {
                    CreateUltimateSlot(ultimateId);
                }
            }

            UpdateAllSlotCooldowns();
            UpdateAllSlotInteractable();
        }

        private void UpdateAllSlotInteractable()
        {
            foreach (var slot in _slots)
            {
                UpdateSlotInteractable(slot);
            }
        }

        private void UpdateSlotInteractable(UltimateUI slot)
        {
            if (slot == null) return;

            bool autoCast = SettingsController.Instance != null && SettingsController.Instance.AutoCastUltimate;
            // When auto-cast is on, buttons are non-interactable (visual only)
            // When auto-cast is off, buttons are interactable if off cooldown
            if (slot.Button != null)
            {
                slot.Button.interactable = !autoCast && slot.IsReady;
            }
        }

        /// <summary>Updates cooldown for a single slot from UltimateManager.</summary>
        private void UpdateSlotCooldown(string ultimateId)
        {
            var ultimateManager = UltimateManager.Instance;
            if (ultimateManager == null) return;
            if (!_slotByUltimateId.TryGetValue(ultimateId, out var slot)) return;
            if (!ultimateManager.TryGetUltimate(ultimateId, out var ultimateData)) return;

            float cooldown = ultimateData.GetCooldown();
            if (cooldown <= 0f)
            {
                slot.SetCooldown(0f);
                return;
            }

            float remaining = ultimateManager.GetCooldownRemaining(ultimateId);
            float fill = remaining > 0f ? remaining / cooldown : 0f;
            slot.SetCooldown(fill);
        }

        private void UpdateAllSlotCooldowns()
        {
            var ultimateManager = UltimateManager.Instance;
            if (ultimateManager == null) return;

            foreach (var ultimateId in _slotByUltimateId.Keys)
            {
                UpdateSlotCooldown(ultimateId);
            }
        }

        private void Update()
        {
            var ultimateManager = UltimateManager.Instance;
            if (ultimateManager == null) return;

            // Read cooldown from UltimateManager every frame (single source of truth)
            foreach (var ultimateId in _slotByUltimateId.Keys)
            {
                UpdateSlotCooldown(ultimateId);
            }
        }

        private void OnDestroy()
        {
            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.AutoCastUltimateChanged -= OnAutoCastChanged;
            }
        }
    }
}