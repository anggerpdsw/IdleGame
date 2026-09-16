namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Condition: Pet has a valid target.
    /// </summary>
    public class TargetExistsCondition : ICondition
    {
        public bool IsMet(IBehaviorContext context)
        {
            return context.HasValidTarget;
        }
    }
}
