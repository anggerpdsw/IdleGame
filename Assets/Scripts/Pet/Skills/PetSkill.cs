using UnityEngine;

namespace IdleDefenseSurvival.Pet
{
    /// <summary>
    /// Base class for pet skills (basic attacks, active abilities, passives).
    /// Provides common cooldown, damage calculation, and execution hooks.
    /// Extend this for pet-specific skill implementations.
    /// </summary>
    public abstract class PetSkill
    {
        public string SkillId { get; protected set; }
        public float BaseCooldown { get; protected set; }
        public float BaseDamageMultiplier { get; protected set; }
        public float StaminaCost { get; protected set; } // Stamina required to cast

        protected PetRuntime Pet { get; private set; }

        public void Initialize(PetRuntime pet)
        {
            Pet = pet;
            OnInitialize();
        }

        /// <summary>
        /// Override to perform skill-specific initialization.
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// Check if skill can be executed (cooldown, stamina, conditions).
        /// </summary>
        public virtual bool CanExecute()
        {
            if (Pet == null) return false;
            if (Pet.IsSkillOnCooldown(SkillId)) return false;
            if (!Pet.CanConsumeStamina(StaminaCost)) return false;
            return CheckCustomConditions();
        }

        /// <summary>
        /// Override to add custom execution conditions.
        /// </summary>
        protected virtual bool CheckCustomConditions()
        {
            return true;
        }

        /// <summary>
        /// Execute the skill.
        /// </summary>
        public void Execute()
        {
            if (!CanExecute()) return;

            // Consume stamina before execution
            Pet.ConsumeStamina(StaminaCost);

            OnExecute();

            // Start cooldown after successful execution
            if (BaseCooldown > 0f)
            {
                Pet.StartCooldown(SkillId, BaseCooldown);
            }
        }

        /// <summary>
        /// Override to implement skill execution logic.
        /// </summary>
        protected abstract void OnExecute();

        /// <summary>
        /// Calculate final skill damage.
        /// </summary>
        protected float CalculateDamage()
        {
            float baseAttack = Pet.GetAttack();
            float skillDamageMultiplier = Pet.GetSkillDamage();
            return baseAttack * BaseDamageMultiplier * skillDamageMultiplier;
        }
    }
}
