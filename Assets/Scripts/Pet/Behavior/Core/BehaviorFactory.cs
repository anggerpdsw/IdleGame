using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Static factory for creating behavior components from JSON config.
    /// No reflection in hot paths - all mappings registered at startup.
    /// Extensible: new components register themselves here.
    /// </summary>
    public static class BehaviorFactory
    {
        private static readonly Dictionary<string, Func<TargeterConfig, ITargeter>> _targeters = new();
        private static readonly Dictionary<string, Func<TriggerConfig, ITrigger>> _triggers = new();
        private static readonly Dictionary<string, Func<ConditionConfig, ICondition>> _conditions = new();
        private static readonly Dictionary<string, Func<ActionConfig, IAction>> _actions = new();
        private static readonly Dictionary<string, Func<ModifierConfig, IModifier>> _modifiers = new();

        static BehaviorFactory()
        {
            RegisterTargeters();
            RegisterTriggers();
            RegisterConditions();
            RegisterActions();
            RegisterModifiers();
        }

        #region Targeter Registration

        private static void RegisterTargeters()
        {
            _targeters["Nearest"] = cfg => new NearestTargeter(cfg.range);
            _targeters["LowestHP"] = cfg => new LowestHPTargeter(cfg.range);
            _targeters["HighestHP"] = cfg => new HighestHPTargeter(cfg.range);
            _targeters["Cluster"] = cfg => new ClusterTargeter(cfg.range, cfg.range * 0.4f);
            _targeters["Priority"] = cfg => new PriorityTargeter(cfg.range, cfg.priorities);
        }

        public static ITargeter CreateTargeter(TargeterConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[BehaviorFactory] Null targeter config, using Nearest default");
                return new NearestTargeter(8f);
            }

            if (_targeters.TryGetValue(config.type, out var factory))
                return factory(config);

            Debug.LogWarning($"[BehaviorFactory] Unknown targeter type: {config.type}, using Nearest default");
            return new NearestTargeter(config.range);
        }

        #endregion

        #region Trigger Registration

        private static void RegisterTriggers()
        {
            _triggers["Always"] = cfg => new AlwaysTrigger();
            _triggers["CooldownReady"] = cfg => new CooldownReadyTrigger(cfg.skillId);
            _triggers["HealthBelow"] = cfg => new HealthBelowTrigger(cfg.value);
        }

        public static ITrigger CreateTrigger(TriggerConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[BehaviorFactory] Null trigger config, using Always default");
                return new AlwaysTrigger();
            }

            if (_triggers.TryGetValue(config.type, out var factory))
                return factory(config);

            Debug.LogWarning($"[BehaviorFactory] Unknown trigger type: {config.type}, using Always default");
            return new AlwaysTrigger();
        }

        #endregion

        #region Condition Registration

        private static void RegisterConditions()
        {
            _conditions["PlayerHPBelow"] = cfg => new PlayerHPBelowCondition(cfg.value);
            _conditions["EnemyCountAbove"] = cfg => new EnemyCountAboveCondition((int)cfg.value);
            _conditions["TargetExists"] = cfg => new TargetExistsCondition();
            _conditions["TargetInRange"] = cfg => new TargetInRangeCondition(cfg.value);
            _conditions["BossExists"] = cfg => new BossExistsCondition();
            _conditions["ALL"] = cfg => new AllCondition(CreateSubconditions(cfg.subconditions));
            _conditions["ANY"] = cfg => new AnyCondition(CreateSubconditions(cfg.subconditions));
            _conditions["NOT"] = cfg => new NotCondition(CreateSubconditions(cfg.subconditions)?[0]);
        }

        public static ICondition CreateCondition(ConditionConfig config)
        {
            if (config == null) return null; // Conditions are optional

            if (_conditions.TryGetValue(config.type, out var factory))
                return factory(config);

            Debug.LogWarning($"[BehaviorFactory] Unknown condition type: {config.type}");
            return null;
        }

        private static ICondition[] CreateSubconditions(List<ConditionConfig> configs)
        {
            if (configs == null || configs.Count == 0) return Array.Empty<ICondition>();

            var conditions = new ICondition[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                conditions[i] = CreateCondition(configs[i]);
            }
            return conditions;
        }

        #endregion

        #region Action Registration

        private static void RegisterActions()
        {
            _actions["BasicAttack"] = cfg => new BasicAttackAction();
            _actions["AOEAttack"] = cfg => new AOEAttackAction(cfg.radius, true);
            _actions["AOEAttackSelf"] = cfg => new AOEAttackAction(cfg.radius, false);
            _actions["ApplyStatus"] = cfg => new ApplyStatusAction(
                ParseStatusType(cfg.statusType),
                cfg.statusDuration,
                cfg.statusValue
            );
            _actions["Heal"] = cfg => new HealAction(cfg.damageMultiplier, false);
            _actions["HealPercent"] = cfg => new HealAction(cfg.damageMultiplier, true);
        }

        public static IAction CreateAction(ActionConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[BehaviorFactory] Null action config");
                return null;
            }

            if (_actions.TryGetValue(config.type, out var factory))
                return factory(config);

            Debug.LogError($"[BehaviorFactory] Unknown action type: {config.type}");
            return null;
        }

        private static StatusEffectType ParseStatusType(string typeStr)
        {
            if (Enum.TryParse<StatusEffectType>(typeStr, true, out var result))
                return result;

            Debug.LogWarning($"[BehaviorFactory] Unknown status type: {typeStr}, defaulting to Slow");
            return StatusEffectType.Slow;
        }

        #endregion

        #region Modifier Registration

        private static void RegisterModifiers()
        {
            _modifiers["Finisher"] = cfg => new FinisherModifier(cfg.value, cfg.value);
            _modifiers["Execute"] = cfg => new ExecuteModifier(cfg.value);
            _modifiers["Crit"] = cfg => new CritModifier(cfg.value, cfg.value);
            _modifiers["Chain"] = cfg => new ChainModifier(cfg.count, cfg.value);
            _modifiers["Leech"] = cfg => new LeechModifier(cfg.value);
            _modifiers["DamageScaling"] = cfg => new DamageScalingModifier(cfg.value);
            _modifiers["Berserker"] = cfg => new BerserkerModifier(cfg.value, cfg.value);
            _modifiers["Splash"] = cfg => new SplashModifier(cfg.value);
        }

        public static IModifier CreateModifier(ModifierConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[BehaviorFactory] Null modifier config");
                return null;
            }

            if (_modifiers.TryGetValue(config.type, out var factory))
                return factory(config);

            Debug.LogWarning($"[BehaviorFactory] Unknown modifier type: {config.type}");
            return null;
        }

        #endregion
    }
}
