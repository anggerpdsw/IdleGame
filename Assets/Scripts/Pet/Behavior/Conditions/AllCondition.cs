using System.Collections.Generic;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Composite condition: ALL subconditions must be met (AND logic).
    /// </summary>
    public class AllCondition : ICondition
    {
        private readonly ICondition[] _subconditions;

        public AllCondition(ICondition[] subconditions)
        {
            _subconditions = subconditions ?? System.Array.Empty<ICondition>();
        }

        public bool IsMet(IBehaviorContext context)
        {
            foreach (var condition in _subconditions)
            {
                if (condition == null || !condition.IsMet(context))
                    return false;
            }
            return true;
        }
    }
}
