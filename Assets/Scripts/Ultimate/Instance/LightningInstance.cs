using UnityEngine;
using IdleDefenseSurvival.Enemy;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using System.Collections;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// LightningInstance handles the chain lightning visual and damage logic.
    /// Visual flow:
    ///
    /// Kill 20 → Lightning strikes from sky
    /// ↓ 0.05s
    /// Enemy A (first target) ⚡
    /// ↓ 0.05s
    /// Enemy B ⚡────⚡
    ///               │
    ///               ⚡ Enemy C
    ///
    ///                        ⚡ Enemy D
    ///                        │
    ///                        ⚡ Enemy E
    ///                                │
    ///                                ⚡ Enemy F (CLIMAX STRIKE - 2x damage, longer stun)
    ///                                    💥
    ///
    /// Each bounce finds the nearest valid enemy to the previous target.
    /// Final strike is the "climax" with double damage and extended stun.
    /// </summary>
    public class LightningInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Lightning.ToString();

        [Header("Chain Settings")]

        [Tooltip("Delay between each chain bounce in seconds (0.05s for visible chain effect)")]
        [SerializeField] private float _chainDelay = 0.05f;

        [Tooltip("Search radius to find next chain target from previous target")]
        [SerializeField] private float _chainSearchRadius = 8f;

        [Tooltip("Climax strike damage multiplier (2x = double damage on final target)")]
        [SerializeField] private float _climaxDamageMultiplier = 2f;

        [Tooltip("Climax strike stun duration multiplier")]
        [SerializeField] private float _climaxStunMultiplier = 2f;

        [Header("Visual/Audio")]
        [Tooltip("Rotation speed of the lightning effect (degrees per second)")]
        [SerializeField] private float _rotationSpeed = 350f;

        [Tooltip("Particle effect prefab for lightning bolt between targets")]
        [SerializeField] private GameObject _lightningBoltEffect;

        [Tooltip("Particle effect for climax strike on final target")]
        [SerializeField] private GameObject _climaxEffect;

        [Tooltip("Audio clip for each lightning strike")]
        [SerializeField] private AudioClip _lightningSoundClip;

        [Tooltip("Audio clip for the final climax strike")]
        [SerializeField] private AudioClip _climaxSoundClip;

        [SerializeField] private float _soundVolume = 1f;

        // Runtime references
        private Player.Player _player;
        private DamageData _baseDamageData;
        private int _currentChainIndex = 0;
        private int _maxChains;
        private int _enemyLayerMask;
        private bool _isChaining = false;
        private EnemyAi _lastTarget = null;
        private readonly List<EnemyAi> _hitEnemies = new();
        private Coroutine _chainCoroutine;
        private WaitForSeconds _chainWait;

        private void Awake()
        {
            _enemyLayerMask = LayerMask.GetMask("Enemy");
            _chainWait = new WaitForSeconds(_chainDelay);
        }

        public void Initialize(Player.Player player, UltimateData lightningData)
        {
            _player = player;
            _maxChains = lightningData.GetChain(7);
            _chainDelay = 0.05f;
            _chainWait = new WaitForSeconds(_chainDelay);

            float ultimateAttack = PlayerStatsManager.Instance.GetStat(SkillType.UltimateAttack);
            float baseDamage = PlayerStatsManager.Instance.GetStat(SkillType.AttackDamage) * lightningData.GetDamageMultiplier() * ultimateAttack;
            float stunMultiplier = lightningData.GetStuntMultiplier();
            float defenseBreak = lightningData.GetDefenseBreak();

            _baseDamageData = new DamageData(baseDamage, DamageType.Normal, CriticalType.None, UltimateID)
            {
                Element = lightningData.GetElement(),
                StuntMultiplier = stunMultiplier,
                DefenseBreak = defenseBreak,
            };

            // Start the chain lightning sequence
            StartChainLightning();
        }

        private void StartChainLightning()
        {
            _isChaining = true;
            _currentChainIndex = 0;
            _hitEnemies.Clear();
            _lastTarget = null;

            // Find first target (nearest enemy to player within attack range + buffer)
            EnemyAi firstTarget = FindFirstTarget();
            if (firstTarget != null)
            {
                _chainCoroutine = StartCoroutine(ChainLightningRoutine(firstTarget));
            }
            else
            {
                // No targets found, destroy self
                Debug.Log("[Lightning] No first target found, destroying instance");
                Destroy(gameObject);
            }
        }

        private EnemyAi FindFirstTarget()
        {
            if (_player == null) return null;

            float attackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            float searchRadius = attackRange + 5f; // Search a bit beyond attack range

            Collider2D[] hits = Physics2D.OverlapCircleAll(_player.transform.position, searchRadius, _enemyLayerMask);

            EnemyAi nearest = null;
            float nearestDist = float.MaxValue;

            foreach (Collider2D col in hits)
            {
                if (col.TryGetComponent(out EnemyAi enemy) && enemy.gameObject.activeInHierarchy)
                {
                    float dist = Vector2.Distance(_player.transform.position, enemy.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = enemy;
                    }
                }
            }

            return nearest;
        }

        private EnemyAi FindNextTargetFromPosition(Vector3 fromPosition)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(fromPosition, _chainSearchRadius, _enemyLayerMask);

            EnemyAi nearest = null;
            float nearestDist = float.MaxValue;

            foreach (Collider2D col in hits)
            {
                if (col.TryGetComponent(out EnemyAi enemy) && enemy.gameObject.activeInHierarchy && !_hitEnemies.Contains(enemy))
                {
                    float dist = Vector2.Distance(fromPosition, enemy.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = enemy;
                    }
                }
            }

            return nearest;
        }

        private IEnumerator ChainLightningRoutine(EnemyAi firstTarget)
        {
            EnemyAi currentTarget = firstTarget;

            while (_isChaining && currentTarget != null && _currentChainIndex < _maxChains)
            {
                _currentChainIndex++;
                _hitEnemies.Add(currentTarget);

                bool isClimaxStrike = _currentChainIndex == _maxChains;

                // Find NEXT target BEFORE striking current target (current target will be destroyed by strike)
                EnemyAi nextTarget = null;
                Vector3 currentTargetPos = currentTarget.transform.position; // Save position before strike

                if (_currentChainIndex < _maxChains)
                {
                    nextTarget = FindNextTargetFromPosition(currentTargetPos);
                }

                // Strike current target (may destroy it)
                yield return StartCoroutine(StrikeTarget(currentTarget, isClimaxStrike));

                // Move to next target (already found)
                if (_currentChainIndex < _maxChains)
                {
                    if (nextTarget != null)
                    {
                        currentTarget = nextTarget;

                        // Wait for chain delay before next strike
                        yield return _chainWait;
                    }
                    else
                    {
                        // No more valid targets, end chain early
                        break;
                    }
                }
                else
                {
                    // Reached max chains, climax strike already happened
                    break;
                }
            }

            // Chain complete, destroy this instance
            _isChaining = false;
            UltimateFactory.OnInstanceDestroyed(UltimateID);
            Destroy(gameObject);
        }

        private IEnumerator StrikeTarget(EnemyAi target, bool isClimaxStrike)
        {
            if (target == null || !target.gameObject.activeInHierarchy) yield break;

            // Calculate damage for this strike
            float damageMultiplier = isClimaxStrike ? _climaxDamageMultiplier : 1f;
            float stunMultiplier = isClimaxStrike ? _baseDamageData.StuntMultiplier * _climaxStunMultiplier : _baseDamageData.StuntMultiplier;
            float defenseBreak = isClimaxStrike ? _baseDamageData.DefenseBreak * _climaxDamageMultiplier : _baseDamageData.DefenseBreak;

            DamageData strikeDamage = new(_baseDamageData.Damage * damageMultiplier, _baseDamageData.Type, _baseDamageData.Critical, _baseDamageData.Source)
            {
                Element = _baseDamageData.Element,
                StuntMultiplier = stunMultiplier,
                DefenseBreak = defenseBreak,
            };

            // Apply damage
            target.TakeDamage(strikeDamage, false);

            // Spawn visual effect from last target (or player for first) to current target
            Vector3 fromPos = (_lastTarget != null && _lastTarget != target) ? _lastTarget.transform.position : _player.transform.position;
            Vector3 toPos = target.transform.position;

            SpawnLightningBolt(fromPos, toPos, isClimaxStrike);

            // Play sound
            AudioClip clip = isClimaxStrike ? _climaxSoundClip : _lightningSoundClip;
            if (clip != null && _player != null)
            {
                Utilityku.PlaySfx(_player.SfxSource, clip, _soundVolume * (isClimaxStrike ? 1.5f : 1f));
            }

            // Brief pause on climax for impact feel
            if (isClimaxStrike)
                yield return new WaitForSeconds(0.1f);
        }

        private void SpawnLightningBolt(Vector3 from, Vector3 to, bool isClimax)
        {
            GameObject effectPrefab = isClimax ? _climaxEffect : _lightningBoltEffect;

            if (effectPrefab != null)
            {
                GameObject bolt = Instantiate(effectPrefab, from, Quaternion.identity, _player.transform);

                // Position and scale the bolt between from and to
                Vector3 direction = to - from;
                float distance = direction.magnitude;

                if (distance > 0.01f)
                {
                    bolt.transform.position = from + direction * 0.5f; // Center
                    bolt.transform.right = direction.normalized; // Align with direction
                    bolt.transform.localScale = new Vector3(distance, 1f, 1f); // Stretch to fit
                }
                else
                {
                    bolt.transform.position = to;
                }

                // Auto-destroy after short duration
                Destroy(bolt, 0.3f);
            }

            // Also create a simple LineRenderer fallback if no particle effect
            if (effectPrefab == null)
            {
                GameObject lineObj = new("LightningBolt");
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, from);
                lr.SetPosition(1, to);
                lr.startWidth = isClimax ? 0.3f : 0.15f;
                lr.endWidth = isClimax ? 0.15f : 0.05f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = isClimax ? GameColors.debugLightningGold : GameColors.debugLightningPurple.WithAlpha(0.8f); // Gold for climax, purple for normal
                lr.endColor = isClimax ? GameColors.debugOrangeGizmo.WithAlpha(0f) : GameColors.debugDarkPurple.WithAlpha(0f);
                lr.sortingOrder = 10;

                // Add some jaggedness for lightning look
                AddLightningJaggedness(lr, from, to, isClimax ? 3 : 2);

                Destroy(lineObj, 0.15f);
            }
        }

        private void AddLightningJaggedness(LineRenderer lr, Vector3 start, Vector3 end, int segments)
        {
            if (segments <= 0) return;

            int totalPoints = segments + 2;
            lr.positionCount = totalPoints;

            Vector3[] points = new Vector3[totalPoints];
            points[0] = start;
            points[totalPoints - 1] = end;

            Vector3 direction = (end - start).normalized;
            Vector3 perpendicular = new(-direction.y, direction.x, 0f);
            float distance = Vector3.Distance(start, end);

            for (int i = 1; i < totalPoints - 1; i++)
            {
                float t = (float)i / (totalPoints - 1);
                Vector3 basePoint = Vector3.Lerp(start, end, t);

                // Add random perpendicular offset
                float maxOffset = distance * 0.15f * (1f - Mathf.Abs(t - 0.5f) * 2f); // Max in middle, zero at ends
                float offset = Random.Range(-maxOffset, maxOffset);

                points[i] = basePoint + perpendicular * offset;
            }

            lr.SetPositions(points);
        }

        private void Update()
        {
            // Rotate the lightning center visual if needed
            if (_isChaining)
            {
                transform.Rotate(0, 0, _rotationSpeed * Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy()
        {
            if (_chainCoroutine != null)
            {
                StopCoroutine(_chainCoroutine);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = GameColors.debugLightningPurple.WithAlpha(0.3f);
            Gizmos.DrawWireSphere(transform.position, _chainSearchRadius);

            if (_lastTarget != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_lastTarget.transform.position, _chainSearchRadius);
            }
        }
#endif
    }
}