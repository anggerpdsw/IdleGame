using IdleDefenseSurvival.Enemy.StatusEffects;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Stun modifier: Applies stun status effect to target on hit.
    /// Action must read StunDuration and apply status after damage.
    /// </summary>
    public class StunModifier : IModifier
    {
        private readonly float _stunDuration;

        public StunModifier(float stunDuration)
        {
            _stunDuration = stunDuration;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            data.ApplyStun = true;
            data.StunDuration = _stunDuration;
        }
    }
}