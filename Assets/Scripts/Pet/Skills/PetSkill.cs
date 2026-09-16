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
        /// Check if skill can be executed (cooldown, mana, conditions).
        /// </summary>
        public virtual bool CanExecute()
        {
            if (Pet == null) return false;
            if (Pet.IsSkillOnCooldown(SkillId)) return false;
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
