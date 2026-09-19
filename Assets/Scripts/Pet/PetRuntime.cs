using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IdleDefenseSurvival.Pet
{
    /// <summary>
    /// Runtime instance state for an equipped pet.
    /// Tracks level, XP, cooldowns, target, position, state machine, and behaviors.
    /// Mutable - changes during gameplay.
    /// </summary>
    public class PetRuntime
    {
        // Identity
        public string InstanceId { get; private set; }
        public string PetId { get; private set; }
        public PetDefinition Definition { get; private set; }

        // Progression
        public int Level { get; set; }
        public long Experience { get; set; }
        public int EvolutionStage { get; set; } // 0 = base, 1 = evolved

        // Combat state
        public float CurrentHealth { get; set; }
        public Transform Target { get; set; }
        public PetState CurrentState { get; set; }

        // Stamina state
        public float CurrentStamina { get; private set; }
        public float MaxStamina { get; private set; }

        // Cooldowns (skill id -> remaining time)
        public Dictionary<string, float> Cooldowns { get; private set; }

        // Position
        public Vector3 Position { get; set; }
        public int OrbitIndex { get; set; } // For multi-pet orbit positioning

        // Timers
        public float TargetScanTimer { get; set; }
        public float AttackTimer { get; set; }

        // GameObject reference
        public GameObject GameObject { get; set; }
        public Transform Transform { get; set; }

        // Emergency mode tracking
        public bool IsEmergencyMode { get; set; }
        public float EmergencyModeTimer { get; set; }

        // NEW: Data-driven behaviors
        public List<Behavior.PetBehaviorRuntime> Behaviors { get; private set; }

        public PetRuntime(string instanceId, string petId, PetDefinition definition, int level = 1)
        {
            InstanceId = instanceId;
            PetId = petId;
            Definition = definition;
            Level = level;
            Experience = 0;
            EvolutionStage = 0;
            CurrentState = PetState.Idle;
            Cooldowns = new Dictionary<string, float>();
            TargetScanTimer = 0f;
            AttackTimer = 0f;
            IsEmergencyMode = false;
            Behaviors = new List<Behavior.PetBehaviorRuntime>();

            // Initialize health
            CurrentHealth = CalculateStat(definition.baseStats.health, definition.growth.healthPerLevel);

            // Initialize stamina
            MaxStamina = definition.maxStamina;
            CurrentStamina = MaxStamina;
        }

        /// <summary>
        /// Initialize behaviors from pet definition.
        /// Called once after PetRuntime is created.
        /// </summary>
        public void InitializeBehaviors(PetDefinition definition)
        {
            if (definition.behaviorDefinitions == null || definition.behaviorDefinitions.Count == 0)
            {
                Debug.LogWarning($"[PetRuntime] No behaviors defined for {definition.id}");
                return;
            }

            Behaviors.Clear();
            foreach (var behaviorDef in definition.behaviorDefinitions)
            {
                var runtime = new Behavior.PetBehaviorRuntime(behaviorDef);
                Behaviors.Add(runtime);
            }

            // Sort by priority (highest first)
            Behaviors.Sort((a, b) => b.Definition.priority.CompareTo(a.Definition.priority));
        }

        /// <summary>
        /// Execute behaviors with priority order and execution mode handling.
        /// Returns true if any behavior executed successfully.
        /// </summary>
        public bool ExecuteBehaviors(Behavior.IBehaviorContext context, Behavior.ActionModifierData sharedModifierData)
        {
            if (Behaviors == null || Behaviors.Count == 0) return false;

            bool anyExecuted = false;

            foreach (var behavior in Behaviors)
            {
                if (!behavior.ShouldExecute(context)) continue;

                // Reset modifier data for this behavior
                sharedModifierData.Reset();

                // Execute behavior
                bool success = behavior.Execute(context, sharedModifierData);

                if (success)
                {
                    anyExecuted = true;

                    // Check execution mode
                    if (behavior.Definition.executionMode == "Exclusive")
                    {
                        // Stop after first successful exclusive behavior
                        break;
                    }
                }
            }

            return anyExecuted;
        }

        /// <summary>
        /// Calculate final stat value with level and rarity scaling.
        /// </summary>
        public float CalculateStat(float baseValue, float growthPerLevel)
        {
            float levelBonus = (Level - 1) * growthPerLevel;
            float rarityMultiplier = Definition.GetRarityMultiplier();
            return (baseValue + levelBonus) * rarityMultiplier;
        }

        /// <summary>
        /// Get current attack stat.
        /// </summary>
        public float GetAttack()
        {
            return CalculateStat(Definition.baseStats.attack, Definition.growth.attackPerLevel);
        }

        /// <summary>
        /// Get current skill damage multiplier.
        /// </summary>
        public float GetSkillDamage()
        {
            return CalculateStat(Definition.baseStats.skillDamage, Definition.growth.skillDamagePerLevel);
        }

        /// <summary>
        /// Check if skill is on cooldown.
        /// </summary>
        public bool IsSkillOnCooldown(string skillId)
        {
            return Cooldowns.TryGetValue(skillId, out float remaining) && remaining > 0f;
        }

        /// <summary>
        /// Start skill cooldown.
        /// </summary>
        public void StartCooldown(string skillId, float duration)
        {
            Cooldowns[skillId] = duration;
        }

        /// <summary>
        /// Tick cooldowns (called from PetManager.Update).
        /// Also ticks behavior cooldowns.
        /// </summary>
        public void TickCooldowns(float deltaTime)
        {
            // Tick skill cooldowns
            var keys = new List<string>(Cooldowns.Keys);
            foreach (var key in keys)
            {
                if (Cooldowns[key] > 0f)
                {
                    Cooldowns[key] -= deltaTime;
                    if (Cooldowns[key] <= 0f)
                        Cooldowns[key] = 0f;
                }
            }

            // Tick behavior cooldowns
            if (Behaviors != null)
            {
                foreach (var behavior in Behaviors)
                {
                    behavior.TickCooldown(deltaTime);
                }
            }
        }
                
        public float GetActiveSkillCooldown()
        {
            if (Definition?.behaviorDefinitions == null) return 0f;
            string activeSkillId = Definition.skills?.active;
            if (string.IsNullOrEmpty(activeSkillId)) return 0f;
            // Cari behavior yang trigger.skillId match
            foreach (var behavior in Definition.behaviorDefinitions)
            {
                if (behavior.trigger?.type == "CooldownReady" && 
                    behavior.trigger?.skillId == activeSkillId)
                {
                    return behavior.cooldown;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Check if target is valid (exists, active, has EnemyAi).
        /// </summary>
        public bool IsTargetValid()
        {
            if (Target == null) return false;
            if (!Target.gameObject.activeInHierarchy) return false;
            var enemy = Target.GetComponent<Enemy.EnemyAi>();
            return enemy != null && enemy.CurrentHealth > 0;
        }

        /// <summary>
        /// Clear invalid target.
        /// </summary>
        public void ClearInvalidTarget()
        {
            if (!IsTargetValid())
                Target = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // STAMINA SYSTEM
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Get stamina as percentage (0-1). Used for potion targeting.
        /// </summary>
        public float StaminaPercent => MaxStamina > 0f ? CurrentStamina / MaxStamina : 0f;

        /// <summary>
        /// Check if pet has enough stamina for skill.
        /// </summary>
        public bool CanConsumeStamina(float amount)
        {
            return CurrentStamina >= amount;
        }

        /// <summary>
        /// Consume stamina for skill cast. Clamps to [0, MaxStamina].
        /// </summary>
        public void ConsumeStamina(float amount)
        {
            CurrentStamina = Mathf.Clamp(CurrentStamina - amount, 0f, MaxStamina);
        }

        /// <summary>
        /// Restore stamina (regen or potion). Clamps to MaxStamina.
        /// </summary>
        public void RestoreStamina(float amount)
        {
            CurrentStamina = Mathf.Clamp(CurrentStamina + amount, 0f, MaxStamina);
        }

        /// <summary>
        /// Tick stamina regeneration per frame.
        /// Called from PetManager.Update.
        /// </summary>
        public void TickStaminaRegen(float deltaTime)
        {
            if (Definition == null) return;
            float regen = Definition.staminaRegen;
            if (regen > 0f)
                RestoreStamina(regen * deltaTime);
        }
    }
}
