using UnityEngine;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Condition: Current target is within specified range.
    /// </summary>
    public class TargetInRangeCondition : ICondition
    {
        private readonly float _range;

        public TargetInRangeCondition(float range)
        {
            _range = range;
        }

        public bool IsMet(IBehaviorContext context)
        {
            if (!context.HasValidTarget) return false;
            float distance = Vector2.Distance(context.PetPosition, context.CurrentTarget.position);
            return distance <= _range;
        }
    }
}
