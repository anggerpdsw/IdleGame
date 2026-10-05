using UnityEngine;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Ultimate;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [SerializeField] private Joystick _joystick;

        private Player _player;
        private Rigidbody2D _rb;
        private float _manaAccumulator;

        private void Awake() => _rb = GetComponent<Rigidbody2D>();

        public void Configure(Player player) => _player = player;
        public void Initialize() => _manaAccumulator = 0f;

        public void FixedTick()
        {
            if (_joystick == null) return;

            Vector2 direction = _joystick.joyStickVec;
            if (direction.sqrMagnitude <= .001f)
            {
                _rb.linearVelocity = Vector2.zero;
                _manaAccumulator = 0f;
                return;
            }

            if (!UltimateManager.Instance.TryGetUltimate("Movement", out var ultimate))
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            float speed = PlayerStatsManager.Instance.GetStat(SkillType.MoveSpeed);
            _rb.linearVelocity = direction * speed;

            _manaAccumulator += ultimate.manaCost * Time.fixedDeltaTime;
            int cost = Mathf.FloorToInt(_manaAccumulator);

            if (cost <= 0) return;

            if (!_player.CanAfford(cost))
            {
                _rb.linearVelocity = Vector2.zero;
                _manaAccumulator = 0f;
                return;
            }

            _player.SpendMana(cost);
            _manaAccumulator -= cost;
        }
    }
}