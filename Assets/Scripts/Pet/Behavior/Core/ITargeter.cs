using UnityEngine;

namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Determines which enemy the pet should interact with.
    /// Pure function: given context + configuration, returns best target.
    /// Null return means no valid target found.
    /// </summary>
    public interface ITargeter
    {
        Transform FindTarget(IBehaviorContext context);
    }
}
