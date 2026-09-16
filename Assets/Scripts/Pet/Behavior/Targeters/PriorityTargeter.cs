using UnityEngine;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Reuses existing PetTargeting scoring system with configured priorities.
    /// Bridge between old and new architecture.
    /// </summary>
    public class PriorityTargeter : ITargeter
    {
        private readonly float _range;
        private readonly System.Collections.Generic.List<string> _priorities;

        public PriorityTargeter(float range, System.Collections.Generic.List<string> priorities)
        {
            _range = range;
            _priorities = priorities ?? new System.Collections.Generic.List<string> { "ClosestToPet" };
        }

        public Transform FindTarget(IBehaviorContext context)
        {
            // Delegate to existing PetTargeting system
            return PetTargeting.FindBestTarget(
                context.PetPosition,
                context.PlayerPosition,
                _range,
                _priorities
            );
        }
    }
}
