namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Executes the actual behavior effect (attack, heal, buff, etc.).
    /// Should integrate with existing systems (ProjectilePool, StatusEffectController).
    /// Returns true if action executed successfully.
    /// </summary>
    public interface IAction
    {
        bool Execute(IBehaviorContext context, ActionModifierData modifiers);
    }
}
