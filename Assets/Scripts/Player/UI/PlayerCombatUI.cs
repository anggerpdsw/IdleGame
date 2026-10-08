using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerCombatUI : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _attackRangeRenderer;
        [SerializeField] private float _rotationSpeed = 2f;

        private Player _player;

        public void Configure(Player player) => _player = player;

        public void RefreshAttackRange()
        {
            if (_attackRangeRenderer == null || PlayerStatsManager.Instance == null)
                return;

            float range = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            float diameter = range * 2f;
            _attackRangeRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
            _attackRangeRenderer.color = GameColors.debugAtkRangeCyan.WithAlpha(.09f);
        }

        public void Tick()
        {
            if (_attackRangeRenderer != null)
                _attackRangeRenderer.transform.Rotate(0f, 0f, _rotationSpeed * Time.deltaTime);
        }
    }
}
