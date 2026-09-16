namespace IdleDefenseSurvival.Pet.Behavior
{
    /// <summary>
    /// Triggers when specified skill cooldown is ready.
    /// </summary>
    public class CooldownReadyTrigger : ITrigger
    {
        private readonly string _skillId;

        public CooldownReadyTrigger(string skillId)
        {
            _skillId = skillId;
        }

        public bool ShouldTrigger(IBehaviorContext context)
        {
            if (string.IsNullOrEmpty(_skillId)) return true;
            return !context.Pet.IsSkillOnCooldown(_skillId);
        }
    }
}
