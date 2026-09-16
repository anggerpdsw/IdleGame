namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Splash: Adds AOE radius to single-target attacks.
    /// Action must implement splash logic - applies damage to enemies within radius of impact point.
    /// </summary>
    public class SplashModifier : IModifier
    {
        private readonly float _splashRadius;

        public SplashModifier(float splashRadius)
        {
            _splashRadius = splashRadius;
        }

        public void Apply(IBehaviorContext context, ActionModifierData data)
        {
            data.SplashRadius = _splashRadius;
        }
    }
}
