namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Heals player by fixed amount or percentage of max HP.
    /// </summary>
    public class HealAction : IAction
    {
        private readonly float _amount;
        private readonly bool _isPercentage;

        public HealAction(float amount, bool isPercentage = false)
        {
            _amount = amount;
            _isPercentage = isPercentage;
        }

        public bool Execute(IBehaviorContext context, ActionModifierData modifiers)
        {
            if (context.Player == null) return false;

            float healAmount = _isPercentage
                ? context.Player.MaxHealth * (_amount / 100f)
                : _amount;

            healAmount *= modifiers.DamageMultiplier; // Reuse damage multiplier for heal scaling
            context.Player.Heal(healAmount);

            return true;
        }
    }
}
