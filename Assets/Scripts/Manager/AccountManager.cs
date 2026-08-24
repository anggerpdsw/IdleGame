using System;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Handles the permanent account progression (level & EXP) for the player.
    /// This system is *production ready* – it follows the project’s coding conventions,
    /// uses a clean singleton pattern, fires UnityEvents for UI subscription,
    /// and persists data through the existing <see cref="SaveManager"/>.
    /// </summary>
    public class AccountManager : MonoBehaviour
    {
        #region Singleton

        private static AccountManager _instance;
        /// <summary>Global access point.</summary>
        public static AccountManager Instance => _instance;

        [SerializeField] private bool _debug = false;

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

        #endregion

        #region Data Model

        /// <summary>Current runtime copy of the persisted data.</summary>
        private AccountData Data => SaveManager.Instance.GetAccountData();

        #endregion

        #region Public Read‑Only API

        /// <summary>Current player level.</summary>
        public int Level => Data.level;
        /// <summary>EXP accumulated in the current level.</summary>
        public long CurrentExp => Data.currentExp;
        /// <summary>Total EXP ever earned (including spent on level‑ups).</summary>
        public long TotalExp => Data.totalExp;

        /// <summary>
        /// EXP required to reach the *next* level.
        /// Formula: <c>100 * level^1.5</c> (rounded down).
        /// </summary>
        public long RequiredExp => GetRequiredExp(Data.level);

        /// <summary>
        /// Normalised progress (0‑1) towards the next level.
        /// UI can bind to this value for a progress bar.
        /// </summary>
        public float Progress => (float)Data.currentExp / RequiredExp;

        #endregion

        #region Events (UI hook)
        public event Action OnExpChanged;
        public event Action<int> OnLevelUp;
        /// <summary>Fired when any attribute changes (UI hook).</summary>
        public event Action OnAttributeChanged;
        public event Action OnDataLoaded;
        #endregion

        public void NotifyDataLoaded() => OnDataLoaded?.Invoke();

        #region EXP Management
        /// <summary>
        /// Add permanent account EXP. Handles multi‑level‑ups in a single call.
        /// The method persists the updated data immediately via <see cref="SaveManager"/>.
        /// </summary>
        /// <param name="amount">Amount of EXP to add (must be positive).</param>
        public void AddExp(long amount, string reason = "")
        {
            if (amount <= 0) return;

            Data.currentExp += amount;
            Data.totalExp   += amount;

            // Process possible multiple level‑ups.
            while (Data.currentExp >= RequiredExp)
            {
                Data.currentExp -= RequiredExp;
                Data.level++;

                // Each level-up grants 5 allocatable attribute points.
                Data.unspentStatPoints += GameConstants.POINTS_PER_LEVEL;

                // Notify listeners about the new level.
                OnLevelUp?.Invoke(Data.level);
            }

            SaveManager.Instance.SaveAll();

            // Notify listeners that EXP changed (either amount or level changed).
            OnExpChanged?.Invoke();

            if(_debug) Debug.Log($"[AccountManager] +{amount} Exp {reason}");
        }

        /// <summary>
        /// Computes the required EXP for a given level using the formula:
        /// <c>RequiredExp = 100 * level^1.5</c>.
        /// Returns <c>0</c> for invalid (≤0) levels.
        /// </summary>
        /// <param name="level">Target level (must be &gt;0).</param>
        /// <returns>Required EXP as a <c>long</c>.</returns>
        public long GetRequiredExp(int level)
        {
            if (level <= 0) return 0;
            // BASE_LEVEL * level^1.5 – Math.Pow returns double, floor to long.
            double value = GameConstants.BASE_LEVEL * Math.Pow(level, 1.5);
            return (long)Math.Floor(value);
        }

        #endregion

        #region Attribute Allocation
        /// <summary>
        /// Current unspent attribute points.
        /// </summary>
        public int UnspentStatPoints => Data.unspentStatPoints;

        /// <summary>
        /// Number of attribute points allocated by the player.
        /// Does not include external bonuses.
        /// </summary>
        private int GetAttributeRaw(MainAttribute attribute) => attribute switch
        {
            MainAttribute.Constitution  => Data.constitution,
            MainAttribute.Strength      => Data.strength,
            MainAttribute.Intelligence  => Data.intelligence,
            MainAttribute.Dexterity     => Data.dexterity,
            _ => 0
        };

        public int GetAttributeAllocated(MainAttribute attribute)
            => Mathf.Max(0, GetAttributeRaw(attribute) - GameConstants.STARTING_STAT_POINTS);


        /// <summary>
        /// External bonus added from equipment, set bonuses,
        /// buffs, passive effects, or other attribute modifier systems.
        /// Does not include the player's allocated attribute points.
        /// </summary>
        public int GetAttributeBonus(MainAttribute attribute)
        {
            return Mathf.RoundToInt(GetBonusValue(attribute));
        }

        /// <summary>
        /// Total main attribute currently obtained by the player.
        /// Total = Base + Allocated Points + External Bonuses.
        /// </summary>
        public int GetAttributeValue(MainAttribute attribute)
        {
            int baseValue = GameConstants.STARTING_STAT_POINTS;
            int allocated = GetAttributeAllocated(attribute);
            int bonus = GetAttributeBonus(attribute);
            return baseValue + allocated + bonus;
        }

        public float GetBonusValue(MainAttribute attribute)
        {
            var equip = EquipmentService.Instance;
            if (equip == null) return 0f;
            return equip.GetAttributeBonus(attribute);
        }

        /// <summary>
        /// Spend one unspent point on an attribute.
        /// </summary>
        public bool SpendPoint(MainAttribute attribute)
        {
            if (Data.unspentStatPoints <= 0) return false;
            switch (attribute)
            {
                case MainAttribute.Constitution: Data.constitution++; break;
                case MainAttribute.Strength: Data.strength++; break;
                case MainAttribute.Intelligence: Data.intelligence++; break;
                case MainAttribute.Dexterity: Data.dexterity++; break;
                default: return false;
            }
            Data.unspentStatPoints--;
            OnAttributeChanged?.Invoke();
            SaveManager.Instance.SaveAll();
            return true;
        }

        #endregion

    }
}
