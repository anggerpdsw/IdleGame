namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Condition: At least one boss enemy is alive.
    /// </summary>
    public class BossExistsCondition : ICondition
    {
        public bool IsMet(IBehaviorContext context)
        {
            return context.BossCount > 0;
        }
    }
}
