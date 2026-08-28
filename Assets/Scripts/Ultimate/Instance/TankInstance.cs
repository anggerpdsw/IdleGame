using UnityEngine;
using IdleDefenseSurvival.Player;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// TankInstance handles the actual tank behavior.
    /// It's a combat unit that moves to a position and attacks enemies.
    /// This is the actual GameObject component, separate from TankHandler (factory).
    /// </summary>
    public class TankInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Tank.ToString();
        [Header("Movement")]
        [Tooltip("Speed at which the tank moves toward its destination.")]
        [SerializeField] private float _moveSpeed = 0.127f;
        [Tooltip("Distance threshold to consider the tank has arrived at its destination.")]
        [SerializeField] private float _arrivalThreshold = 0.15f;

        [Header("Combat Stats")]
        [Tooltip("Attack range of the tank (1/3 of player's attack range).")]
        [SerializeField] private float _tankAttackRange;
        [Tooltip("Attack damage of the tank (2x player's attack damage).")]
        [SerializeField] private float _tankAttackDamage;
        [Tooltip("Attack speed of the tank (1/2 of player's attack speed).")]
        [SerializeField] private float _tankAttackSpeed;

        [Header("Visualization")]
        [SerializeField] private SpriteRenderer _attackRangeRenderer;
        [SerializeField] private float _rotationSpeed = 1.5f;
        [SerializeField] private float _turnSpeed = 720f;

        // Runtime references
        private Player.Player _player;
        private Vector2 _destination;
        private float _elapsed;
        private bool _hasArrived;
        private int _enemyLayerMask;
        private float _attackTimer;
        private Transform _currentTarget;
        private float _duration;
        private float _ultimateAttack;

        public float TankAttackRange => PlayerStatsManager.Instance.GetStat(SkillType.AttackRange) * 0.47f;
        public float TankAttackDamage => PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage) * 2.3f * _ultimateAttack;
        public float TankAttackSpeed => PlayerStatsManager.Instance.GetStat(SkillType.AttackSpeed) * 0.51f;
        public float TankDamagePerRange => PlayerStatsManager.Instance.GetStat(SkillType.DamagePerRange) * 1.5f;

        /// <summary>
        /// Fired when the tank's duration expires (not called for manual Destroy).
        /// </summary>
        public event System.Action OnExpired;

        private void Awake()
        {
            _enemyLayerMask = LayerMask.GetMask("Enemy");
            _attackTimer = 0f;
        }

        /// <summary>
        /// Initialize the tank with player reference, destination, and duration.
        /// Called by TankHandler after instantiation.
        /// </summary>
        public void Initialize(Player.Player player, Vector2 destination, float duration)
        {
            _player = player;
            _destination = destination;
            _duration = duration;
            _elapsed = 0f;
            _hasArrived = false;
            _ultimateAttack = PlayerStatsManager.Instance.GetStat(SkillType.UltimateAttack);

            // Initialize combat stats from player
            if (player != null)
            {
                _tankAttackRange = TankAttackRange;
                _tankAttackDamage = TankAttackDamage;
                _tankAttackSpeed = TankAttackSpeed;
            }

            DrawAttackRange();
        }

        private void Update()
        {
            if (_player == null)
            {
                Destroy(gameObject);
                return;
            }

            RotateToTarget(_currentTarget);

            if (!_hasArrived)
            {
                // Move toward the destination at the attack range boundary
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    _destination,
                    _moveSpeed * Time.deltaTime
                );

                // Check if we've arrived at the destination
                if (Vector2.Distance(transform.position, _destination) <= _arrivalThreshold)
                {
                    _hasArrived = true;
                }
            }
            else
            {
                // Countdown until expiry (duration starts after arrival)
                _elapsed += Time.deltaTime;
                if (_elapsed >= _duration)
                {
                    OnExpired?.Invoke();
                    // Notify handler and factory
                    var handler = UltimateFactory.GetHandler(UltimateID) as TankHandler;
                    if (handler != null) handler.OnInstanceDestroyed();
                    Destroy(gameObject);
                    return;
                }
            }

            // Attack logic
            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                TankAttack();
                _attackTimer = 1f / _tankAttackSpeed;
            }

            _attackRangeRenderer.transform.Rotate(0, 0, _rotationSpeed * Time.deltaTime);
        }

        private void RotateToTarget(Transform target)
        {
            if (target == null) return;

            Vector2 dir = target.position - transform.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
        }

        private void FindTarget()
        {
            // Target lama masih ada dan masih dalam range
            if (_currentTarget != null)
            {
                float distance = Vector2.Distance(transform.position, _currentTarget.position);
                if (distance <= _tankAttackRange) return;
            }
            
            // Find all enemies within tank's attack range
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _tankAttackRange, _enemyLayerMask);
            if (hits.Length == 0)
            {
                _currentTarget = null;
                return;
            }

            _currentTarget = hits[0].transform;
        }

        /// <summary>
        /// Tank attack method (mirroring Player.cs Attack() method)
        /// </summary>
        private void TankAttack()
        {
            FindTarget();
            if (_currentTarget == null) return;

            // Get projectile from pool - don't parent to tank since tank moves
            Projectile projectile = ProjectilePool.Instance.Get();
            if (projectile != null)
            {
                projectile.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
                projectile.InitializeFromTank(_currentTarget, this);
            }
        }

        private void DrawAttackRange()
        {
            // Attack range visualization is handled by SpriteRenderer's sprite size
            if (_attackRangeRenderer == null) return;
            if (_tankAttackRange <= 0f) return;
            float diameter = _tankAttackRange * 2f;
            Sprite sprite = _attackRangeRenderer.sprite;
            if (sprite != null)
            {
                float range = diameter * 3;
                _attackRangeRenderer.transform.localScale = new Vector3(range, range, 1f);
            }
            _attackRangeRenderer.color = GameColors.debugAtkRangeCyan.WithAlpha(0.09f);
        }
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_tankAttackRange <= 0f) return;

            // Draw attack range as green circle in Scene view
            Gizmos.color = GameColors.green.WithAlpha(0.5f); // Green with transparency
            DrawCircleGizmo(transform.position, _tankAttackRange, 32);
        }

        private static void DrawCircleGizmo(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments * Mathf.Deg2Rad;
            Vector3 prevPos = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep;
                Vector3 newPos = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0
                );
                Gizmos.DrawLine(prevPos, newPos);
                prevPos = newPos;
            }
        }
#endif

    }
}
