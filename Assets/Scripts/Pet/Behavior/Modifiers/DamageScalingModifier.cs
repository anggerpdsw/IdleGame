namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Damage Scaling: Simple damage multiplier.
    /// Example: 2.0 = 200% damage, 0.5 = 50% damage
    /// </summary>
    public class DamageScalingModifier : IModifier
    {
        private readonly float _multiplier;

        public DamageScalingModifier(float multiplier)
        {
            _multiplier = multiplier;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            data.DamageMultiplier *= _multiplier;
        }
    }
}
