namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Composite condition: ANY subcondition must be met (OR logic).
    /// </summary>
    public class AnyCondition : ICondition
    {
        private readonly ICondition[] _subconditions;

        public AnyCondition(ICondition[] subconditions)
        {
            _subconditions = subconditions ?? System.Array.Empty<ICondition>();
        }

        public bool IsMet(IBehaviorContext context)
        {
            if (_subconditions.Length == 0) return false;

            foreach (var condition in _subconditions)
            {
                if (condition != null && condition.IsMet(context))
                    return true;
            }
            return false;
        }
    }
}
