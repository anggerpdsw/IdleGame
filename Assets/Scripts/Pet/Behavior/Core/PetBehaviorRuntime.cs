using UnityEngine;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Runtime state for one behavior instance on one pet.
    /// Separate from definition - each pet gets its own runtime state.
    /// </summary>
    public class PetBehaviorRuntime
    {
        public PetBehaviorDefinition Definition { get; private set; }

        // Runtime state
        public float CooldownRemaining { get; set; }
        public bool IsEnabled { get; set; }
        public int ExecutionCount { get; set; }
        public float LastExecutionTime { get; set; }

        // Cached components (built once from definition)
        public ITargeter Targeter { get; private set; }
        public ITrigger Trigger { get; private set; }
        public ICondition Condition { get; private set; }
        public IAction Action { get; private set; }
        public IModifier[] Modifiers { get; private set; }

        public PetBehaviorRuntime(PetBehaviorDefinition definition)
        {
            Definition = definition;
            CooldownRemaining = 0f;
            IsEnabled = true;
            ExecutionCount = 0;
            LastExecutionTime = -999f;

            // Build components from config
            Targeter = BehaviorFactory.CreateTargeter(definition.targeter);
            Trigger = BehaviorFactory.CreateTrigger(definition.trigger);
            Condition = BehaviorFactory.CreateCondition(definition.condition);
            Action = BehaviorFactory.CreateAction(definition.action);

            if (definition.modifiers != null && definition.modifiers.Count > 0)
            {
                Modifiers = new IModifier[definition.modifiers.Count];
                for (int i = 0; i < definition.modifiers.Count; i++)
                {
                    Modifiers[i] = BehaviorFactory.CreateModifier(definition.modifiers[i]);
                }
            }
            else
            {
                Modifiers = System.Array.Empty<IModifier>();
            }
        }

        public bool IsOnCooldown => CooldownRemaining > 0f;

        public void TickCooldown(float deltaTime)
        {
            if (CooldownRemaining > 0f)
            {
                CooldownRemaining -= deltaTime;
                if (CooldownRemaining < 0f) CooldownRemaining = 0f;
            }
        }

        public void StartCooldown()
        {
            CooldownRemaining = Definition.cooldown;
            LastExecutionTime = Time.time;
            ExecutionCount++;
        }

        /// <summary>
        /// Evaluate if behavior should execute.
        /// Returns true if all gates pass: enabled, cooldown ready, trigger met, condition met.
        /// </summary>
        public bool ShouldExecute(IBehaviorContext context)
        {
            if (!IsEnabled) return false;
            if (IsOnCooldown) return false;
            if (Trigger != null && !Trigger.ShouldTrigger(context)) return false;
            if (Condition != null && !Condition.IsMet(context)) return false;
            return true;
        }

        /// <summary>
        /// Execute behavior: apply modifiers, run action, start cooldown.
        /// Returns true if action executed successfully.
        /// </summary>
        public bool Execute(IBehaviorContext context, ActionModifierData modifierData)
        {
            // Apply all modifiers
            if (Modifiers != null)
            {
                foreach (var modifier in Modifiers)
                {
                    modifier?.Apply(context, modifierData);
                }
            }

            // Execute action
            bool success = Action != null && Action.Execute(context, modifierData);

            // Start cooldown on success
            if (success && Definition.cooldown > 0f)
            {
                StartCooldown();
            }

            return success;
        }
    }
}
