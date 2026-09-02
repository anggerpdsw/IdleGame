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
        public int LevelAlchemist => Data.alchemistLevel;
        public int LevelBlacksmith => Data.blacksmithLevel;
        /// <summary>EXP accumulated in the current level.</summary>
        public long CurrentExp => Data.currentExp;
        public long CurrentExpAlchemist => Data.alchemistCurrentExp;
        public long CurrentExpBlacksmith => Data.blacksmithCurrentExp;
        /// <summary>Total EXP ever earned (including spent on level‑ups).</summary>
        public long TotalExp => Data.totalExp;
        public long TotalExpAlchemist  => Data.alchemistTotalExp;
        public long TotalExpBlacksmith => Data.blacksmithTotalExp;

        /// <summary>
        /// EXP required to reach the *next* level.
        /// Formula: <c>100 * level^1.5</c> (rounded down).
        /// </summary>
        public long RequiredExp => GetRequiredExp(Data.level, LevelType.Level);
        public long RequiredExpAlchemist => GetRequiredExp(Data.alchemistLevel, LevelType.Alchemist);
        public long RequiredExpBlacksmith => GetRequiredExp(Data.blacksmithLevel, LevelType.Blacksmith);

        /// <summary>
        /// Normalised progress (0‑1) towards the next level.
        /// UI can bind to this value for a progress bar.
        /// </summary>
        public float Progress => (float)Data.currentExp / RequiredExp;
        public float ProgressAlchemist => (float)Data.alchemistCurrentExp / RequiredExpAlchemist;
        public float ProgressBlacksmith => (float)Data.blacksmithCurrentExp / RequiredExpBlacksmith;

        #endregion

        #region Events (UI hook)
        /// <summary>Fired when Player levels up.</summary>
        public event Action<int> OnLevelUp;
        /// <summary>Fired when Player EXP changes.</summary>
        public event Action OnExpChanged;
        /// <summary>Fired when any attribute changes (UI hook).</summary>
        public event Action OnAttributeChanged;
        public event Action OnDataLoaded;

        /// <summary>Fired when blacksmith levels up.</summary>
        public event Action<int> OnBlacksmithLevelUp;
        /// <summary>Fired when blacksmith EXP changes.</summary>
        public event Action OnBlacksmithExpChanged;

        /// <summary>Fired when alchemist levels up.</summary>
        public event Action<int> OnAlchemistLevelUp;
        /// <summary>Fired when alchemist EXP changes.</summary>
        public event Action OnAlchemistExpChanged;
        #endregion

        public void NotifyDataLoaded() => OnDataLoaded?.Invoke();

        #region EXP Management
        /// <summary>
        /// Adds EXP to the specified progression type.
        /// Handles multiple level-ups in a single call.
        /// </summary>
        /// <param name="amount">Amount of EXP to add.</param>
        /// <param name="type">Progression type receiving the EXP.</param>
        /// <param name="reason">Optional reason for debugging.</param>
        public void AddExp(long amount, LevelType type = LevelType.Level, string reason = "")
        {
            if (amount <= 0) return;
            switch (type)
            {
                case LevelType.Level: AddPlayerExp(amount, reason); break;
                case LevelType.Alchemist: AddAlchemistExp(amount, reason); break;
                case LevelType.Blacksmith: AddBlacksmithExp(amount, reason); break;
            }
        }

        private void AddPlayerExp(long amount, string reason)
        {
            Data.currentExp += amount;
            Data.totalExp += amount;
            while (Data.currentExp >= RequiredExp)
            {
                Data.currentExp -= RequiredExp;
                Data.level++;
                // Normal player level
                Data.unspentStatPoints += GameConstants.POINTS_PER_LEVEL;
                OnLevelUp?.Invoke(Data.level);
            }
            SaveManager.Instance.SaveAll();
            OnExpChanged?.Invoke();
            if (_debug) Debug.Log($"[AccountManager] +{amount} Player EXP {reason}");
        }

        private void AddAlchemistExp(long amount, string reason)
        {
            Data.alchemistCurrentExp += amount;
            Data.alchemistTotalExp += amount;
            while (Data.alchemistCurrentExp >= RequiredExpAlchemist)
            {
                Data.alchemistCurrentExp -= RequiredExpAlchemist;
                Data.alchemistLevel++;
                // Alchemist level-up logic
                // Tambahkan reward/bonus Alchemist di sini jika diperlukan.
                OnAlchemistLevelUp?.Invoke(Data.alchemistLevel);
            }
            SaveManager.Instance.SaveAll();
            OnAlchemistExpChanged?.Invoke();
            if (_debug) Debug.Log($"[AccountManager] +{amount} Alchemist EXP {reason}");
        }

        private void AddBlacksmithExp(long amount, string reason)
        {
            Data.blacksmithCurrentExp += amount;
            Data.blacksmithTotalExp += amount;
            while (Data.blacksmithCurrentExp >= RequiredExpBlacksmith)
            {
                Data.blacksmithCurrentExp -= RequiredExpBlacksmith;
                Data.blacksmithLevel++;
                // Blacksmith level-up logic
                // Tambahkan reward/bonus Blacksmith di sini jika diperlukan.
                OnBlacksmithLevelUp?.Invoke(Data.blacksmithLevel);
            }
            SaveManager.Instance.SaveAll();
            OnBlacksmithExpChanged?.Invoke();
            if (_debug) Debug.Log($"[AccountManager] +{amount} Blacksmith EXP {reason}");
        }

        /// <summary>
        /// Computes the required EXP for a given level using the formula:
        /// <c>RequiredExp = 100 * level^1.5</c>.
        /// Returns <c>0</c> for invalid (≤0) levels.
        /// </summary>
        /// <param name="level">Target level (must be &gt;0).</param>
        /// <returns>Required EXP as a <c>long</c>.</returns>
        public long GetRequiredExp(int level, LevelType type)
        {
            if (level <= 0) return 0;
            long baseValue = 1L;
            switch (type)
            {
                case LevelType.Alchemist: baseValue = GameConstants.BASE_LEVEL_ALCHEMIST; break;
                case LevelType.Blacksmith: baseValue = GameConstants.BASE_LEVEL_BLACKSMITH; break;
                case LevelType.Level: baseValue = GameConstants.BASE_LEVEL; break;
                default: return baseValue;
            }
            // BASE_LEVEL * level^1.5 – Math.Pow returns double, floor to long.
            double value = baseValue * Math.Pow(level, 1.5);
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
