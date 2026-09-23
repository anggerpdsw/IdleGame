using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Stats;
using UnityEngine;

using PlayerClass = IdleDefenseSurvival.Player.Player;

namespace IdleDefenseSurvival.Manager
{
    /// <summary>
    /// Central service for applying card modifiers to player stats
    /// and handling card effects.
    ///
    /// Modifier ID convention:
    ///     Card:{cardId}
    ///
    /// Dynamic modifier IDs use dedicated suffixes.
    /// </summary>
    public static class CardModifierService
    {
        #region Events
        public static event Action OnModifierChanged;
        #endregion

        #region Constants
        // ------------------------------------------------------------------
        // Modifier IDs
        // ------------------------------------------------------------------
        private const string BerserkerModifierId = "Card:BerserkerDynamic";
        private const string GamblerModifierId = "Card:CrazyGambler";
        private const string DesperadosModifierId = "Card:Desperados";
        private const string DeathChainModifierId = "Card:DeathChain";
        private const string VampiricFrenzyModifierId = "Card:VampiricFrenzy";
        private const string WarMachineModifierId = "Card:WarMachine";
        private const string ApocalypseEngineModifierId = "Card:ApocalypseEngine";
        private const string SoulHarvesterModifierId = "Card:SoulHarvester";
        // ------------------------------------------------------------------
        // Balance values loaded from Resources/Data/Card/dataCardConfig.json
        // ------------------------------------------------------------------
        private static CardConfig CardConfig => DatabaseJSONCache.CardConfig;
        private static float DeathChainWindow => CardConfig.DeathChainWindow;
        private static int DeathChainMaxStack => CardConfig.DeathChainMaxStack;
        private static float VampiricFrenzyDuration => CardConfig.VampiricFrenzyDuration;
        private static int VampiricFrenzyMaxStacks => CardConfig.VampiricFrenzyMaxStacks;
        private static float GuardianHPDrop => CardConfig.GuardianHPDrop;
        private static float GuardianCooldownDuration => CardConfig.GuardianCooldownDuration;
        private static float WarMachineContinuityThreshold => CardConfig.WarMachineContinuityThreshold;
        private static float WarMachineIdleThreshold => CardConfig.WarMachineIdleThreshold;
        private static int GamblerMaxCount => CardConfig.GamblerMaxCount;
        private static float GamblerPositiveValue => CardConfig.GamblerPositiveValue;
        private static float GamblerNegativeValue => CardConfig.GamblerNegativeValue;
        #endregion

        #region Card Effect State
        /// <summary>
        /// Current active value for each card effect type.
        /// </summary>
        private static readonly Dictionary<CardEffectType, CardEffectValue> _effectValues = new();
        /// <summary>
        /// Tracks card IDs that currently have a normal stat modifier.
        /// </summary>
        private static readonly HashSet<string> _cardsWithStatModifiers = new();
        #endregion

        #region Berserker State
        private static float _berserkerMaxPercent;
        private static bool _berserkerSubscribed;
        #endregion

        #region HealOnKill State
        private static bool _healOnKillSubscribed;
        #endregion

        #region Angel State
        private static float _angelCooldownRemaining;
        private static float _angelCooldownMax;
        private static int _angelImmunityWavesRemaining;
        #endregion

        #region CrazyGambler State
        private static float _crazyGamblerBonus;
        public static float GetCrazyGamblerBonus() => _crazyGamblerBonus;
        #endregion

        #region Desperados State
        private static float _desperadosBonus;
        public static float GetDesperadosBonus() => _desperadosBonus;
        #endregion

        #region DeathChain State
        private static readonly Queue<float> _deathChainKills = new();
        private static int _deathChainStack;
        #endregion

        #region BulletStorm State
        private static int _bulletStormCounter;
        #endregion

        #region VampiricFrenzy State
        private static float _vampiricFrenzyAccumulator;
        private static int _vampiricFrenzyStacks;
        private static float _vampiricFrenzyTimer;
        #endregion

        #region GuardianInstinct State
        private static float _guardianCooldownRemaining;
        private static bool _guardianActive;
        #endregion

        #region WarMachine State
        private static float _warMachineContinuityTimer;
        private static float _warMachineIdleTimer;
        private static bool _warMachineActive;
        #endregion

        #region ApocalypseEngine State
        private static int _apocalypseKillCount;
        private static int _apocalypseStacks;
        #endregion

        #region InfiniteArsenal State
        private static int _infiniteArsenalCounter;
        #endregion

        #region SoulHarvester State
        private static int _soulCount;
        private static int _soulMaxStack = 100;
        #endregion

        #region DeathReversal State
        private static float _deathReversalSnapshotTimer;
        private static Vector3 _deathReversalPosition;
        private static float _deathReversalHP;
        private static bool _deathReversalUsedThisWave;
        private const float DeathReversalSnapshotInterval = 0.5f;
        #endregion

        #region VoidOverlord State
        private static float _voidOverlordTimer;
        private static float _voidOverlordDuration;
        private static bool _voidOverlordActive;
        #endregion

        #region Refresh
        /// <summary>
        /// Clears all existing card modifiers and re-applies modifiers
        /// from currently equipped cards.
        ///
        /// Called when cards are equipped, unequipped, upgraded,
        /// or when the game is loaded.
        /// </summary>
        public static void Refresh()
        {
            ResetAngel();
            ResetStatModifiers();
            ResetBerserker();
            ResetCrazyGambler();
            ResetDesperados();
            ResetDeathChain();
            ResetBulletStorm();
            ResetVampiricFrenzy();
            ResetGuardianInstinct();
            ResetWarMachine();
            ResetApocalypseEngine();
            ResetInfiniteArsenal();
            ResetSoulHarvester();
            ResetDeathReversal();
            ResetVoidOverlord();

            bool hasHealOnKill = ApplyEquippedCards();

            UpdatePlayerVisualEffects(hasHealOnKill);

            OnModifierChanged?.Invoke();
        }

        private static void ResetStatModifiers()
        {
            foreach (string cardId in _cardsWithStatModifiers)
            {
                RemoveCardModifier(cardId);
            }
            _cardsWithStatModifiers.Clear();
            _effectValues.Clear();
        }

        private static void ResetAngel()
        {
            _angelCooldownRemaining = 0f;
            _angelCooldownMax = 0f;
            _angelImmunityWavesRemaining = 0;
        }

        private static void ResetBerserker()
        {
            _berserkerMaxPercent = 0f;
            ModifierManager.Instance.RemoveModifier(BerserkerModifierId);
        }

        private static void ResetCrazyGambler()
        {
            _crazyGamblerBonus = 0f;
            ModifierManager.Instance.RemoveModifier($"{GamblerModifierId}_AttackDamage");
        }

        private static void ResetDesperados()
        {
            _desperadosBonus = 0f;
            ModifierManager.Instance.RemoveModifier($"{DesperadosModifierId}_HealthPoint");
        }

        private static void ResetDeathChain()
        {
            _deathChainKills.Clear();
            _deathChainStack = 0;
            ModifierManager.Instance.RemoveModifier(DeathChainModifierId);
        }

        private static void ResetBulletStorm()
        {
            _bulletStormCounter = 0;
        }

        private static void ResetVampiricFrenzy()
        {
            _vampiricFrenzyAccumulator = 0f;
            _vampiricFrenzyStacks = 0;
            _vampiricFrenzyTimer = 0f;

            ModifierManager.Instance.RemoveModifier(VampiricFrenzyModifierId);
        }

        private static void ResetGuardianInstinct()
        {
            _guardianCooldownRemaining = 0f;
            _guardianActive = false;
        }

        private static void ResetWarMachine()
        {
            _warMachineContinuityTimer = 0f;
            _warMachineIdleTimer = 0f;
            _warMachineActive = false;
            RemoveWarMachineModifiers();
        }

        private static void ResetApocalypseEngine()
        {
            _apocalypseKillCount = 0;
            _apocalypseStacks = 0;
            RemoveApocalypseModifiers();
        }

        private static void ResetInfiniteArsenal()
        {
            _infiniteArsenalCounter = 0;
        }

        private static void ResetSoulHarvester()
        {
            _soulCount = 0;
            _soulMaxStack = 100;
            ModifierManager.Instance.RemoveModifier(SoulHarvesterModifierId);
        }

        private static void ResetDeathReversal()
        {
            _deathReversalSnapshotTimer = 0f;
            _deathReversalPosition = Vector3.zero;
            _deathReversalHP = 0f;
            _deathReversalUsedThisWave = false;
        }

        private static void ResetVoidOverlord()
        {
            _voidOverlordTimer = 0f;
            _voidOverlordDuration = 0f;
            _voidOverlordActive = false;
        }
        #endregion

        #region Card Application
        private static bool ApplyEquippedCards()
        {
            bool hasHealOnKill = false;
            var equippedCards = CardEquipmentService.Instance.EquippedCards;
            foreach (string cardId in equippedCards)
            {
                if (string.IsNullOrEmpty(cardId)) continue;
                var cardData = CardDatabase.Instance.GetCard(cardId);
                if (cardData == null) continue;

                var inventory = CardInventory.Instance.GetOwnedCard(cardId);
                int level = inventory?.Level ?? 1;

                float value = cardData.CalculateValue(level);

                ApplyCardEffect(cardData, value, ref hasHealOnKill);
                ApplyCardStatModifier(cardId, cardData, value);
            }
            return hasHealOnKill;
        }

        private static void ApplyCardEffect(CardData cardData, float value, ref bool hasHealOnKill)
        {
            if (string.IsNullOrEmpty(cardData.EffectType)) return;
            CardEffectType effectType = ParseEffectType(cardData.EffectType);
            if (effectType == CardEffectType.None) return;
            _effectValues[effectType] = new CardEffectValue
            {
                Mode = ParseModifierMode(cardData.Mode),
                Value = value
            };

            switch (effectType)
            {
                case CardEffectType.Berserker:
                    _berserkerMaxPercent = value;
                    break;

                case CardEffectType.HealOnKill:
                    EnsureHealOnKillSubscription();
                    hasHealOnKill = true;
                    break;

                case CardEffectType.Immortal:
                    _angelCooldownMax = value;
                    break;
            }
        }

        private static void ApplyCardStatModifier(string cardId, CardData cardData, float value)
        {
            if (string.IsNullOrEmpty(cardData.SkillType)) return;
            SkillType statType = ParseSkillType(cardData.SkillType);
            if (statType == SkillType.None) return;

            string modifierId = GetCardModifierId(cardId);
            var modifier = CreateStatModifier(
                modifierId,
                statType,
                ParseModifierMode(cardData.Mode),
                value,
                true
            );
            ModifierManager.Instance.AddModifier(modifier);
            _cardsWithStatModifiers.Add(cardId);
        }

        private static void UpdatePlayerVisualEffects(bool hasHealOnKill)
        {
            var player = PlayerClass.Instance;
            if (player != null)
                player.SetVampireEffect(hasHealOnKill);
        }
        #endregion

        #region Timer Update
        /// <summary>
        /// Updates all card timers.
        /// Call from Player.Update or equivalent runtime update.
        /// </summary>
        public static void UpdateCardTimers(float deltaTime)
        {
            UpdateAngelCooldown(deltaTime);
            UpdateDeathChainDecay();
            UpdateVampiricFrenzyTimer(deltaTime);
            UpdateGuardianCooldown(deltaTime);
            UpdateWarMachineTimer(deltaTime);
            UpdateDeathReversalSnapshot(deltaTime);
            UpdateVoidOverlordTimer(deltaTime);
        }

        /// <summary>
        /// Updates Angel cooldown timer.
        /// </summary>
        public static void UpdateAngelCooldown(float deltaTime)
        {
            if (_angelCooldownRemaining > 0f)
                _angelCooldownRemaining -= deltaTime;
        }

        private static void UpdateGuardianCooldown(float deltaTime)
        {
            if (_guardianCooldownRemaining > 0f)
                _guardianCooldownRemaining -= deltaTime;
        }
        #endregion

        #region DeathChain
        private static void UpdateDeathChainDecay()
        {
            if (!HasEffect(CardEffectType.DeathChain)) return;
            float now = Time.time;
            while (
                _deathChainKills.Count > 0 &&
                now - _deathChainKills.Peek() > DeathChainWindow)
            {
                _deathChainKills.Dequeue();
            }

            int newStack = _deathChainKills.Count;
            if (newStack != _deathChainStack)
            {
                _deathChainStack = newStack;
                RefreshDeathChainModifier();
            }
        }

        public static void OnEnemyKilledDeathChain()
        {
            if (!HasEffect(CardEffectType.DeathChain)) return;
            float now = Time.time;
            _deathChainKills.Enqueue(now);
            if (_deathChainKills.Count > DeathChainMaxStack)
            {
                _deathChainKills.Dequeue();
            }
            _deathChainStack = _deathChainKills.Count;
            RefreshDeathChainModifier();
        }

        private static void RefreshDeathChainModifier()
        {
            ModifierManager.Instance.RemoveModifier(DeathChainModifierId);
            if (_deathChainStack <= 0) return;
            float percentPerStack = GetEffectResult(CardEffectType.DeathChain, 0f) * 100f;
            float totalBonus = _deathChainStack * percentPerStack;
            var modifier = CreateStatModifier(
                DeathChainModifierId,
                SkillType.AttackDamage,
                ModifierMode.Percent,
                totalBonus,
                false
            );
            ModifierManager.Instance.AddModifier(modifier);
        }

        public static int GetDeathChainStack() => _deathChainStack;
        #endregion

        #region BulletStorm
        public static void OnAttackBulletStorm()
        {
            if (!HasEffect(CardEffectType.BulletStorm)) return;
            _bulletStormCounter++;
            float triggerCount = GetEffectResult(CardEffectType.BulletStorm, 8f);
            if (_bulletStormCounter >= (int)triggerCount)
            {
                _bulletStormCounter = 0;
                TriggerBulletStorm();
            }
        }

        private static void TriggerBulletStorm()
        {
            var player = PlayerClass.Instance;
            if (player == null) return;
            player.SpawnBulletStormBurst();
        }
        #endregion

        #region VampiricFrenzy
        private static void UpdateVampiricFrenzyTimer(float deltaTime)
        {
            if (_vampiricFrenzyStacks <= 0) return;
            _vampiricFrenzyTimer -= deltaTime;
            if (_vampiricFrenzyTimer <= 0f)
            {
                _vampiricFrenzyStacks = 0;
                _vampiricFrenzyTimer = 0f;
                RefreshVampiricFrenzyModifier();
            }
        }

        public static void OnLifeStealHealVampiricFrenzy(float healAmount)
        {
            if (!HasEffect(CardEffectType.VampiricFrenzy)) return;
            var player = PlayerClass.Instance;
            if (player == null) return;

            _vampiricFrenzyAccumulator += healAmount;

            float maxHp = player.MaxHealth;
            float threshold = maxHp * 0.1f;

            while (
                _vampiricFrenzyAccumulator >= threshold &&
                _vampiricFrenzyStacks < VampiricFrenzyMaxStacks)
            {
                _vampiricFrenzyAccumulator -= threshold;
                _vampiricFrenzyStacks++;
                _vampiricFrenzyTimer = VampiricFrenzyDuration;
            }

            RefreshVampiricFrenzyModifier();
        }

        private static void RefreshVampiricFrenzyModifier()
        {
            ModifierManager.Instance.RemoveModifier(VampiricFrenzyModifierId);
            if (_vampiricFrenzyStacks <= 0) return;
            float percentPerStack = GetEffectResult(CardEffectType.VampiricFrenzy, 0f) * 100f;
            float totalBonus = _vampiricFrenzyStacks * percentPerStack;
            var modifier = CreateStatModifier(
                VampiricFrenzyModifierId,
                SkillType.AttackSpeed,
                ModifierMode.Percent,
                totalBonus,
                false
            );
            ModifierManager.Instance.AddModifier(modifier);
        }

        public static int GetVampiricFrenzyStacks() => _vampiricFrenzyStacks;
        #endregion

        #region GuardianInstinct
        public static bool TryTriggerGuardianInstinct(float currentHp, float maxHp)
        {
            if (!HasEffect(CardEffectType.GuardianInstinct)) return false;
            if (_guardianCooldownRemaining > 0f) return false;
            if (currentHp > maxHp * GuardianHPDrop) return false;
            float shieldPercent = GetEffectResult(CardEffectType.GuardianInstinct, 0f);
            float shieldAmount = maxHp * shieldPercent;
            var player = PlayerClass.Instance;
            if (player != null)
            {
                player.GrantGuardianShield(shieldAmount);
                _guardianCooldownRemaining = GuardianCooldownDuration;
                _guardianActive = true;
            }
            return true;
        }

        public static float GetGuardianCooldownRemaining() 
            => Mathf.Max(_guardianCooldownRemaining, 0f);
        public static bool IsGuardianActive() => _guardianActive;
        #endregion

        #region WarMachine
        private static void UpdateWarMachineTimer(float deltaTime)
        {
            if (!HasEffect(CardEffectType.WarMachine)) return;
            _warMachineIdleTimer += deltaTime;
            if (_warMachineIdleTimer > WarMachineIdleThreshold && _warMachineActive)
            {
                _warMachineActive = false;
                _warMachineContinuityTimer = 0f;
                RefreshWarMachineModifier();
            }
        }

        public static void OnAttackWarMachine()
        {
            if (!HasEffect(CardEffectType.WarMachine)) return;
            _warMachineIdleTimer = 0f;
            _warMachineContinuityTimer += Time.deltaTime;
            bool shouldBeActive = _warMachineContinuityTimer >= WarMachineContinuityThreshold;
            if (shouldBeActive != _warMachineActive)
            {
                _warMachineActive = shouldBeActive;
                RefreshWarMachineModifier();
            }
        }

        private static void RefreshWarMachineModifier()
        {
            RemoveWarMachineModifiers();
            if (!_warMachineActive) return;
            float bonusPercent = GetEffectResult(CardEffectType.WarMachine, 0f) * 100f;
            ModifierManager.Instance.AddStatModifiers(
                ModifierSource.Card,
                WarMachineModifierId,
                (SkillType.AttackDamage, ModifierMode.Percent, bonusPercent),
                (SkillType.AttackSpeed, ModifierMode.Percent, bonusPercent),
                (SkillType.CriticalChance, ModifierMode.Percent, bonusPercent)
            );
        }

        private static void RemoveWarMachineModifiers()
        {
            ModifierManager.Instance.RemoveModifier($"{WarMachineModifierId}_AttackDamage");
            ModifierManager.Instance.RemoveModifier($"{WarMachineModifierId}_AttackSpeed");
            ModifierManager.Instance.RemoveModifier($"{WarMachineModifierId}_CriticalChance");
        }

        public static bool IsWarMachineActive() => _warMachineActive;
        #endregion

        #region ApocalypseEngine
        public static void OnEnemyKilledApocalypse()
        {
            if (!HasEffect(CardEffectType.ApocalypseEngine)) return;
            _apocalypseKillCount++;
            if (_apocalypseKillCount >= 50)
            {
                _apocalypseKillCount = 0;
                _apocalypseStacks++;
                RefreshApocalypseModifiers();
            }
        }

        private static void RefreshApocalypseModifiers()
        {
            RemoveApocalypseModifiers();
            if (_apocalypseStacks <= 0) return;

            float atkPerStack = GetEffectResult(CardEffectType.ApocalypseEngine, 0f) * 100f;
            float asPerStack = atkPerStack * 0.5f;
            float critDmgPerStack = atkPerStack * 2f;

            ModifierManager.Instance.AddStatModifiers(
                ModifierSource.Card,
                ApocalypseEngineModifierId,
                (SkillType.AttackDamage, ModifierMode.Percent, _apocalypseStacks * atkPerStack),
                (SkillType.AttackSpeed, ModifierMode.Percent, _apocalypseStacks * asPerStack),
                (SkillType.CriticalDamage, ModifierMode.Percent, _apocalypseStacks * critDmgPerStack)
            );
        }

        private static void RemoveApocalypseModifiers()
        {
            ModifierManager.Instance.RemoveModifier($"{ApocalypseEngineModifierId}_AttackDamage");
            ModifierManager.Instance.RemoveModifier($"{ApocalypseEngineModifierId}_AttackSpeed");
            ModifierManager.Instance.RemoveModifier($"{ApocalypseEngineModifierId}_CriticalDamage");
        }

        public static int GetApocalypseStacks() => _apocalypseStacks;
        #endregion

        #region InfiniteArsenal
        public static void OnAttackInfiniteArsenal()
        {
            if (!HasEffect(CardEffectType.InfiniteArsenal)) return;
            _infiniteArsenalCounter++;
            float triggerCount = GetEffectResult(CardEffectType.InfiniteArsenal, 12f);
            if (_infiniteArsenalCounter >= (int)triggerCount)
            {
                _infiniteArsenalCounter = 0;
                TriggerInfiniteArsenal();
            }
        }

        private static void TriggerInfiniteArsenal()
        {
            var player = PlayerClass.Instance;
            player?.SpawnInfiniteArsenalProjectile();
        }

        public static bool IsInfiniteArsenalProjectile { get; set; }
        #endregion

        #region SoulHarvester
        public static void OnEnemyKilledSoulHarvester()
        {
            if (!HasEffect(CardEffectType.SoulHarvester)) return;
            _soulCount++;

            // Every 100 souls: increase max stack by 50
            while (_soulCount > _soulMaxStack)
            {
                _soulMaxStack += 50;
            }

            RefreshSoulHarvesterModifier();
        }

        private static void RefreshSoulHarvesterModifier()
        {
            ModifierManager.Instance.RemoveModifier(SoulHarvesterModifierId);
            if (_soulCount <= 0) return;

            float percentPerSoul = GetEffectResult(CardEffectType.SoulHarvester, 0f) * 100f;
            float totalBonus = _soulCount * percentPerSoul;

            var modifier = CreateStatModifier(
                SoulHarvesterModifierId,
                SkillType.AttackDamage,
                ModifierMode.Percent,
                totalBonus,
                false
            );
            ModifierManager.Instance.AddModifier(modifier);
        }

        public static int GetSoulCount() => _soulCount;
        public static int GetSoulMaxStack() => _soulMaxStack;
        #endregion

        #region DeathReversal
        private const float DeathReversalClearRadius = 8f;

        private static void UpdateDeathReversalSnapshot(float deltaTime)
        {
            if (!HasEffect(CardEffectType.DeathReversal)) return;

            _deathReversalSnapshotTimer -= deltaTime;
            if (_deathReversalSnapshotTimer <= 0f)
            {
                _deathReversalSnapshotTimer = DeathReversalSnapshotInterval;
                var player = PlayerClass.Instance;
                if (player != null)
                {
                    _deathReversalPosition = player.transform.position;
                    _deathReversalHP = player.CurrentHealth;
                }
            }
        }

        public static bool CanTriggerDeathReversal()
        {
            if (_deathReversalUsedThisWave) return false;
            return HasEffect(CardEffectType.DeathReversal);
        }

        public static bool TriggerDeathReversal()
        {
            if (!CanTriggerDeathReversal()) return false;

            var player = PlayerClass.Instance;
            if (player == null) return false;

            // Rewind position
            player.transform.position = _deathReversalPosition;

            // Restore HP
            float restorePercent = GetEffectResult(CardEffectType.DeathReversal, 0f);
            float maxHP = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);
            float healAmount = maxHP * restorePercent;
            player.Heal(healAmount);

            // Mark as used
            _deathReversalUsedThisWave = true;

            // Clear enemy projectiles in radius
            Vector3 playerPos = player.transform.position;
            ProjectilePool.Instance?.ClearWhere(proj =>
                proj.Owner == ProjectileOwner.Enemy &&
                Vector3.Distance(proj.transform.position, playerPos) <= DeathReversalClearRadius
            );

            return true;
        }

        public static void OnWaveStartDeathReversal()
        {
            _deathReversalUsedThisWave = false;
        }

        public static void OnBattleStartApocalypse()
        {
            // Reset stacks when battle starts (entering Game scene)
            _apocalypseKillCount = 0;
            _apocalypseStacks = 0;
            RemoveApocalypseModifiers();
        }
        #endregion

        #region VoidOverlord
        private static void UpdateVoidOverlordTimer(float deltaTime)
        {
            if (!HasEffect(CardEffectType.VoidOverlord)) return;

            if (_voidOverlordActive)
            {
                _voidOverlordDuration -= deltaTime;
                if (_voidOverlordDuration <= 0f)
                {
                    _voidOverlordActive = false;
                }
            }
            else
            {
                _voidOverlordTimer += deltaTime;
                float interval = GetEffectResult(CardEffectType.VoidOverlord, 100f);
                if (_voidOverlordTimer >= interval)
                {
                    _voidOverlordTimer = 0f;
                    float duration = GetEffectResult(CardEffectType.VoidOverlord, 15f);
                    _voidOverlordDuration = duration;
                    _voidOverlordActive = true;
                }
            }
        }

        public static bool IsVoidOverlordActive() => _voidOverlordActive;
        #endregion

        #region Wave
        /// <summary>
        /// Notify wave completion.
        /// Decrements Angel immunity counter.
        /// </summary>
        public static void OnWaveCompleted()
        {
            if (_angelImmunityWavesRemaining > 0)
                _angelImmunityWavesRemaining--;

            if (_angelImmunityWavesRemaining == 0 && PlayerClass.Instance != null)
                PlayerClass.Instance.SetBarrierEffect(false);

            // Reset DeathReversal for new wave
            OnWaveStartDeathReversal();
        }

        public static void OnAfterWave150()
        {
            ApplyCrazyGamblerWaveEffect();
            ApplyDesperadosWaveEffect();

            OnModifierChanged?.Invoke();
        }

        private static void ApplyCrazyGamblerWaveEffect()
        {
            if (!HasEffect(CardEffectType.CrazyGambler)) return;
            float chancePercent = GetEffectResult(CardEffectType.CrazyGambler, 0f) * 100f;
            float bonusPercent = 
                Utilityku.Chance(chancePercent) ? GamblerPositiveValue : GamblerNegativeValue;
            _crazyGamblerBonus += bonusPercent;
            _crazyGamblerBonus = Mathf.Clamp(
                _crazyGamblerBonus,
                GamblerNegativeValue * GamblerMaxCount,
                GamblerPositiveValue * GamblerMaxCount
            );

            string modifierId = $"{GamblerModifierId}_AttackDamage";
            ModifierManager.Instance.RemoveModifier(modifierId);
            var modifier = CreateStatModifier(
                GamblerModifierId,
                SkillType.AttackDamage,
                ModifierMode.Percent,
                _crazyGamblerBonus,
                false
            );
            ModifierManager.Instance.AddModifier(modifier);
        }

        private static void ApplyDesperadosWaveEffect()
        {
            if (!HasEffect(CardEffectType.Desperados)) return;
            float chancePercent = GetEffectResult(CardEffectType.Desperados, 0f) * 100f;
            float bonusPercent =
                Utilityku.Chance(chancePercent) ? GamblerPositiveValue : GamblerNegativeValue;
            _desperadosBonus += bonusPercent;
            _desperadosBonus = Mathf.Clamp(
                _desperadosBonus,
                GamblerNegativeValue * GamblerMaxCount,
                GamblerPositiveValue * GamblerMaxCount
            );

            string modifierId = $"{DesperadosModifierId}_HealthPoint";
            ModifierManager.Instance.RemoveModifier(modifierId);
            var modifier = CreateStatModifier(
                DesperadosModifierId,
                SkillType.HealthPoint,
                ModifierMode.Percent,
                _desperadosBonus,
                false
            );
            ModifierManager.Instance.AddModifier(modifier);
        }
        #endregion

        #region Angel
        /// <summary>
        /// Checks if Angel can currently trigger.
        /// </summary>
        public static bool CanTriggerAngel()
        {
            if (_angelCooldownRemaining > 0f) return false;
            return HasEffect(CardEffectType.Immortal);
        }

        /// <summary>
        /// Triggers Angel immunity for one wave
        /// and starts the cooldown.
        /// </summary>
        public static bool TriggerAngel()
        {
            if (!CanTriggerAngel()) return false;
            float cooldownSeconds = GetEffectResult(CardEffectType.Immortal, 660f);
            _angelCooldownRemaining = cooldownSeconds;
            _angelImmunityWavesRemaining = 1;
            return true;
        }

        public static bool HasAngelImmunity() => _angelImmunityWavesRemaining > 0;
        public static float GetAngelCooldownRemaining()
            => Mathf.Max(_angelCooldownRemaining, 0f);
        public static float GetAngelMaxCooldown() => _angelCooldownMax;
        #endregion

        #region Berserker
        /// <summary>
        /// Ensures the Berserker health-change event subscription exists.
        /// </summary>
        public static void EnsureBerserkerSubscription()
        {
            var player = PlayerClass.Instance;
            if (player == null)
            {
                Debug.LogWarning("[Berserker] Player.Instance still null");
                return;
            }
            if (_berserkerSubscribed) return;
            player.OnHealthChanged += UpdateBerserkerModifier;
            _berserkerSubscribed = true;
            UpdateBerserkerModifier();
        }

        private static void UpdateBerserkerModifier()
        {
            var player = PlayerClass.Instance;
            if (player == null)
            {
                Debug.LogWarning("[Berserker] UpdateBerserkerModifier: Player.Instance is null");
                return;
            }
            if (_berserkerMaxPercent <= 0f)
            {
                player.SetBerserkerEffect(false);
                return;
            }
            float maxHp = player.MaxHealth;
            float currentHp = player.CurrentHealth;
            if (maxHp <= 0f) return;
            float missingPercent = (maxHp - currentHp) / maxHp * 100f;
            float bonusPercent = Mathf.Min(missingPercent, _berserkerMaxPercent);
            ModifierManager.Instance.RemoveModifier(BerserkerModifierId);
            bool isActive = bonusPercent > 0f;
            if (isActive)
            {
                var modifier = CreateStatModifier(
                    BerserkerModifierId,
                    SkillType.AttackDamage,
                    ModifierMode.Percent,
                    bonusPercent,
                    false
                );
                ModifierManager.Instance.AddModifier(modifier);
            }
            player.SetBerserkerEffect(isActive);
        }
        #endregion

        #region HealOnKill
        /// <summary>
        /// Ensures HealOnKill event subscription exists.
        /// </summary>
        private static void EnsureHealOnKillSubscription()
        {
            if (_healOnKillSubscribed) return;
            EnemyDeathHandler.OnEnemyKilled += OnEnemyKilledHandler;
            _healOnKillSubscribed = true;
        }

        /// <summary>
        /// Handles enemy death for HealOnKill.
        /// Only player kills are counted.
        /// </summary>
        private static void OnEnemyKilledHandler(EnemyAi enemy, string damageSource)
        {
            if (enemy == null) return;
            if (damageSource != UltimateDMG.Player.ToString()) return;
            var equippedCards = CardEquipmentService.Instance.EquippedCards;
            foreach (string cardId in equippedCards)
            {
                if (string.IsNullOrEmpty(cardId)) continue;
                var cardData = CardDatabase.Instance.GetCard(cardId);
                if (cardData == null) continue;
                CardEffectType effectType = ParseEffectType(cardData.EffectType);
                if (effectType != CardEffectType.HealOnKill) continue;
                var inventory = CardInventory.Instance.GetOwnedCard(cardId);
                int level = inventory?.Level ?? 1;
                float percent = cardData.CalculateValue(level);
                float healAmount = enemy.MaxHealth * (percent * 0.01f);
                PlayerClass.Instance?.Heal(healAmount);
            }
        }
        #endregion

        #region Effect Queries
        /// <summary>
        /// Returns true when the requested card effect is active.
        /// </summary>
        public static bool HasEffect(CardEffectType effect)
            => _effectValues.TryGetValue(effect, out var data) && data.Value > 0f;
        public static bool HasAuraEffect()
        {
            return HasEffect(CardEffectType.FrostAura);
        }

        /// <summary>
        /// Gets the calculated effect value.
        ///
        /// Percent values are converted from:
        ///     25 -> 0.25
        ///
        /// Flat values remain unchanged.
        /// </summary>
        public static float GetEffectResult(CardEffectType effect, float fallback = 0f)
        {
            if (!_effectValues.TryGetValue(effect, out var data)) return fallback;
            return data.Mode switch
            {
                ModifierMode.Percent => data.Value * 0.01f,
                ModifierMode.Flat => data.Value,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(data.Mode),
                    data.Mode,
                    null
                )
            };
        }
        #endregion

        #region Parsing
        private static SkillType ParseSkillType(string skillType)
            => Enum.TryParse(skillType, true, out SkillType result) ? result : SkillType.None;
        private static CardEffectType ParseEffectType(string effectType)
            => Enum.TryParse(effectType, true, out CardEffectType result) ? result : CardEffectType.None;
        private static ModifierMode ParseModifierMode(string mode)
            => Enum.TryParse(mode, true, out ModifierMode result) ? result : ModifierMode.Percent;
        #endregion

        #region Modifier Helpers
        private static string GetCardModifierId(string cardId) => $"Card:{cardId}";
        private static void RemoveCardModifier(string cardId)
            => ModifierManager.Instance.RemoveModifier(GetCardModifierId(cardId));
        private static StatModifier CreateStatModifier(
            string id,
            SkillType stat,
            ModifierMode mode,
            float value,
            bool permanent)
        {
            return new StatModifier
            {
                Id = id,
                Source = ModifierSource.Card,
                Stat = stat,
                Mode = mode,
                Value = value,
                Permanent = permanent
            };
        }
        #endregion
    }
}