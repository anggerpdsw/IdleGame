namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Condition: Alive enemy count exceeds threshold.
    /// </summary>
    public class EnemyCountAboveCondition : ICondition
    {
        private readonly int _threshold;

        public EnemyCountAboveCondition(int threshold)
        {
            _threshold = threshold;
        }

        public bool IsMet(IBehaviorContext context)
        {
            return context.AliveEnemyCount > _threshold;
        }
    }
}
