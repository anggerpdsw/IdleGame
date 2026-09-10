using System;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Defines a special effect owned by an enemy.
    /// </summary>
    [Serializable]
    public class EnemyEffect
    {
        /// <summary>
        /// Effect type, for example:
        /// Frost, Fire, Poison, Stun, etc.
        /// </summary>
        public string type;
        /// <summary>
        /// Effect triggered when enemy successfully hits player.
        /// </summary>
        public EnemyEffectAction[] onHit;
        /// <summary>
        /// Effect continuously applied within radius.
        /// </summary>
        public EnemyEffectAction[] aura;
        /// <summary>
        /// Effect triggered when enemy receives direct damage.
        /// </summary>
        public EnemyEffectAction[] onTakeDamage;
    }

    [Serializable]
    public class EnemyEffectAction
    {
        public string effect;
        public float value;
        public float duration;
        public float radius;
    }
}