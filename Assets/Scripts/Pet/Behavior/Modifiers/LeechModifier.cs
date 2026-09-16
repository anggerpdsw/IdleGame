namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Leech/LifeSteal: Heals attacker for percentage of damage dealt.
    /// Action must read LifeStealPercent and apply heal after damage.
    /// </summary>
    public class LeechModifier : IModifier
    {
        private readonly float _lifestealPercent;

        public LeechModifier(float lifestealPercent)
        {
            _lifestealPercent = lifestealPercent;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            data.LifeStealPercent += _lifestealPercent;
        }
    }
}
