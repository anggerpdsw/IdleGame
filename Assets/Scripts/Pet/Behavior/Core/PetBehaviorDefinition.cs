using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Immutable behavior configuration loaded from JSON.
    /// Shared across multiple pets - do not store runtime state here.
    /// </summary>
    [Serializable]
    public class PetBehaviorDefinition
    {
        public string id;
        public int priority = 50; // Higher executes first
        public string executionMode = "Exclusive"; // Exclusive, Concurrent, Interruptible

        public TargeterConfig targeter;
        public TriggerConfig trigger;
        public ConditionConfig condition;
        public ActionConfig action;
        public List<ModifierConfig> modifiers;

        public float cooldown = 0f; // Behavior cooldown in seconds
    }

    [Serializable]
    public class TargeterConfig
    {
        public string type; // "Nearest", "LowestHP", "HighestHP", "Cluster", "Priority"
        public float range = 8f;
        public List<string> priorities; // For Priority type: ["ClosestToPlayer", "Elite", "HighestHp"]
        public int maxCandidates = 10;
    }

    [Serializable]
    public class TriggerConfig
    {
        public string type; // "Always", "CooldownReady", "OnAttack", "HealthBelow", etc.
        public float value; // Threshold value for health/count triggers
        public string skillId; // For skill-based triggers
    }

    [Serializable]
    public class ConditionConfig
    {
        public string type; // "PlayerHPBelow", "EnemyCountAbove", "TargetInRange", "ALL", "ANY", "NOT"
        public float value;
        public List<ConditionConfig> subconditions; // For composite conditions (ALL/ANY/NOT)
    }

    [Serializable]
    public class ActionConfig
    {
        public string type; // "Attack", "AOEAttack", "Projectile", "ApplyStatus", "Heal", etc.
        public float damageMultiplier = 1f;
        public float radius = 0f; // For AOE actions
        public string statusType; // For status actions
        public float statusDuration;
        public float statusValue;
    }

    [Serializable]
    public class ModifierConfig
    {
        public string type; // "Finisher", "Crit", "Chain", "Leech", "Execute", "Berserker"
        public float value;
        public int count; // For chain modifiers
    }
}
