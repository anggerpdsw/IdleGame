using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleDefenseSurvival.Pet
{
    /// <summary>
    /// Static pet definition loaded from dataPet.json.
    /// Contains base stats, skills, behavior configuration.
    /// Immutable reference data - do not modify at runtime.
    /// </summary>
    [Serializable]
    public class PetDefinition
    {
        public string id;
        public string name;
        public string description;
        public string role; // Support, DPS, Tank, Utility
        public string rarity; // Common, Rare, Epic, Legendary, Mythic

        public PetBaseStats baseStats;
        public PetGrowth growth;

        public string behaviorType; // Guardian, Aggressive, Defensive, Balanced (legacy)
        public List<string> targetPriority; // ClosestToPlayer, Elite, HighestHp, LowestHp, etc. (legacy)

        public PetSkillIds skills;
        public PetEvolutionRequirement evolutionRequirement;

        // Behavior configuration
        public float orbitRadius = 2.5f;
        public float emergencyThreshold = 0.3f; // Trigger emergency at 30% player HP

        // NEW: Data-driven behavior definitions
        public List<Behavior.PetBehaviorDefinition> behaviorDefinitions;

        /// <summary>
        /// Get rarity multiplier for stat scaling.
        /// </summary>
        public float GetRarityMultiplier()
        {
            return rarity switch
            {
                "Common" => 1.0f,
                "Rare" => 1.15f,
                "Epic" => 1.3f,
                "Legendary" => 1.5f,
                "Mythic" => 1.8f,
                _ => 1.0f
            };
        }
    }

    [Serializable]
    public class PetBaseStats
    {
        public float attack = 10f;
        public float attackSpeed = 1.5f;
        public float skillDamage = 1.5f;
        public float moveSpeed = 5f;
        public float targetRange = 8f;
        public float health = 100f; // Optional, most pets invulnerable
    }

    [Serializable]
    public class PetGrowth
    {
        public float attackPerLevel = 2f;
        public float skillDamagePerLevel = 0.1f;
        public float healthPerLevel = 10f;
    }

    [Serializable]
    public class PetSkillIds
    {
        public string basic;      // Basic attack skill id
        public string active;     // Active skill id
        public string passive;    // Passive ability id
        public string evolution;  // Evolution skill id (unlocked at level threshold)
    }

    [Serializable]
    public class PetEvolutionRequirement
    {
        public int level = 10;
    }
}
