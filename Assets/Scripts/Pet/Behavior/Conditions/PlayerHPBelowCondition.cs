namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Condition: Player HP is below threshold (0-1 range).
    /// </summary>
    public class PlayerHPBelowCondition : ICondition
    {
        private readonly float _threshold;

        public PlayerHPBelowCondition(float threshold)
        {
            _threshold = threshold;
        }

        public bool IsMet(IBehaviorContext context)
        {
            return context.PlayerHealthPercent <= _threshold;
        }
    }
}
