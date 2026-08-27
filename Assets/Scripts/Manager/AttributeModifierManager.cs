using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Applies the four main attributes (Constitution/Strength/Intelligence/Dexterity)
    /// as modifiers on top of the base player skills.
    ///
    /// Total attribute = base (dataPlayer.json "mainAttributes") + allocated points
    /// (AccountData). Which skills each attribute boosts and by how much comes from
    /// dataAttribute.json (parsed once by AttributeService — no re-parse on Apply).
    ///
    /// Auto-applies on: save load (OnSaveLoaded) and attribute change (AccountManager.OnAttributeChanged).
    /// Modifiers registered under ModifierSource.AccountLevel.
    /// </summary>
    public class AttributeModifierManager : MonoBehaviour
    {
        private static AttributeModifierManager _instance;
        public static AttributeModifierManager Instance => _instance;

        private const string PREFIX = "attr";

        // Always exactly four attributes; flat fields beat a Dictionary here.
        private float _constitution, _strength, _intelligence, _dexterity;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Apply();
        }

        private void OnEnable()
        {
            SaveManager.OnSaveLoaded += Apply;
            if (AccountManager.Instance != null)
                AccountManager.Instance.OnAttributeChanged += Apply;

            // Subscribe to equipment changes with fallback for init order.
            // If EquipmentService not ready yet, defer subscription via coroutine.
            SubscribeToEquipmentService();
        }

        private async void SubscribeToEquipmentService()
        {
            // Wait until EquipmentService is initialized (max 5 frames)
            for (int i = 0; i < 5 && EquipmentService.Instance == null; i++)
                await Awaitable.NextFrameAsync();

            if (EquipmentService.Instance != null)
            {
                EquipmentService.Instance.OnEquipmentChanged += OnEquipmentChanged;
            }
            else
            {
                Debug.LogWarning("[AttributeModifierManager] EquipmentService not found after 5 frames; equipment changes won't auto-refresh attributes.");
            }
        }

        private void OnDisable()
        {
            SaveManager.OnSaveLoaded -= Apply;
            if (AccountManager.Instance != null)
                AccountManager.Instance.OnAttributeChanged -= Apply;
            if (EquipmentService.Instance != null)
                EquipmentService.Instance.OnEquipmentChanged -= OnEquipmentChanged;
        }

        private void OnDestroy()
        {
            if (ModifierManager.Instance != null)
                ModifierManager.Instance.RemoveSource(ModifierSource.AccountLevel);
        }

        /// <summary>Equipment re-applies attribute pool on equip/swap/unequip/set-bonus changes.</summary>
        private void OnEquipmentChanged(EquipmentChangedEventArgs _) => Apply();

        /// <summary>
        /// (Re)apply attribute modifiers to skills. Called automatically on save load
        /// and attribute change; no manual calls needed.
        /// </summary>
        public void Apply()
        {
            if (ModifierManager.Instance == null) return;

            AttributeService.Initialize();
            var account = SaveManager.Instance != null ? SaveManager.Instance.GetAccountData() : null;

            // Equipment attribute bonuses (main stats + set bonuses) feed the same pool,
            // so the equipment pipeline is: equipment -> attribute -> skill modifier.
            var equipment = EquipmentService.Instance;
            var equipBonuses = equipment != null
                ? EquipmentStatCalculator.GetTotalAttributeBonuses(ItemDatabase.Instance,
                    equipment.EquippedItems, equipment.EquippedSetCounts)
                : null;

            _constitution = Allocated(account?.constitution ?? 0) +
                (equipBonuses?.GetValueOrDefault(MainAttribute.Constitution, 0) ?? 0);
            _strength = Allocated(account?.strength ?? 0) +
                (equipBonuses?.GetValueOrDefault(MainAttribute.Strength, 0) ?? 0);
            _intelligence = Allocated(account?.intelligence ?? 0) +
                (equipBonuses?.GetValueOrDefault(MainAttribute.Intelligence, 0) ?? 0);
            _dexterity = Allocated(account?.dexterity ?? 0) +
                (equipBonuses?.GetValueOrDefault(MainAttribute.Dexterity, 0) ?? 0);
            
            var modifiers = new List<StatModifier>(32);
            Collect(MainAttribute.Constitution, _constitution, modifiers);
            Collect(MainAttribute.Strength, _strength, modifiers);
            Collect(MainAttribute.Intelligence, _intelligence, modifiers);
            Collect(MainAttribute.Dexterity, _dexterity, modifiers);

            ModifierManager.Instance.SetSource(ModifierSource.AccountLevel, modifiers);
            PlayerStatsManager.Instance?.RefreshStats();
            // existing bridge; or add an OnAttributeChanged.Invoke() alias
            AccountManager.Instance?.NotifyDataLoaded();
        }

        private void Collect(MainAttribute attr, float total, List<StatModifier> modifiers)
        {
            if (total <= 0) return;

            var bonuses = AttributeService.GetBonuses(attr);
            if (bonuses == null) return;

            foreach (var bonus in bonuses)
            {
                float flat = bonus.Flat * total;
                float percent = bonus.Percent * total;

                // Flat and percent are separate modifiers so both can apply at once.
                if (flat != 0f) modifiers.Add(Create(attr, bonus.Stat, ModifierMode.Flat, flat));
                if (percent != 0f) modifiers.Add(Create(attr, bonus.Stat, ModifierMode.Percent, percent));
            }
        }

        private StatModifier Create(MainAttribute attr, SkillType stat, ModifierMode mode, float value) => new()
        {
            // Mode in the id so flat + percent of the same stat don't collide.
            Id = $"{PREFIX}_{attr}_{stat}_{mode}",
            Source = ModifierSource.AccountLevel,
            Stat = stat,
            Mode = mode,
            Value = value
        };

        /// <summary>
        /// Allocated attribute points (skip base). Account attribute starts at
        /// STARTING_STAT_POINTS from dataPlayer.json; per-point bonuses apply above that.
        /// Total attribute = allocated + equip, base contributes no bonus.
        /// </summary>
        private static float Allocated(int allocated) =>
            Mathf.Max(0, allocated - GameConstants.STARTING_STAT_POINTS);

    }
}