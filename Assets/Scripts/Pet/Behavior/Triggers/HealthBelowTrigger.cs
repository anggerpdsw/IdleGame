namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Triggers when player HP drops below threshold (0-1 range).
    /// </summary>
    public class HealthBelowTrigger : ITrigger
    {
        private readonly float _threshold;

        public HealthBelowTrigger(float threshold)
        {
            _threshold = threshold;
        }

        public bool ShouldTrigger(IBehaviorContext context)
        {
            return context.PlayerHealthPercent <= _threshold;
        }
    }
}
