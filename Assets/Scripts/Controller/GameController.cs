
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;

namespace IdleDefenseSurvival.Controller
{
    public class GameController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private TextMeshProUGUI _coinMultiplier;
        [SerializeField] private TextMeshProUGUI _healthPlayer;
        [SerializeField] private TextMeshProUGUI _manaPlayer;
        [SerializeField] private TextMeshProUGUI _attackPlayer;
        [SerializeField] private TextMeshProUGUI _defensePlayer;
        [SerializeField] private TextMeshProUGUI _regenPlayer;

        [Header("Enemy")]
        [SerializeField] private TextMeshProUGUI _countEnemy;
        [SerializeField] private TextMeshProUGUI _avgHealthEnemy;
        [SerializeField] private TextMeshProUGUI _avgAttackEnemy;
        [SerializeField] private TextMeshProUGUI _avgDefenseEnemy;
        [SerializeField] private TextMeshProUGUI _avgEvasionEnemy;

        private Player.Player _player;
        private PlayerStatsManager _playerStats;
        private EnemyStatisticsManager _enemyStats;

        private void Start()
        {
            Bind();
            RefreshPlayer();
            RefreshEnemy();
        }

        private void OnDisable() => Unbind();

        private void Bind()
        {
            _player = Player.Player.Instance;
            _playerStats = PlayerStatsManager.Instance;
            _enemyStats = EnemyStatisticsManager.Instance;

            if (_playerStats != null) _playerStats.OnStatsChanged += RefreshPlayer;
            if (_enemyStats != null) _enemyStats.OnStatisticsChanged += RefreshEnemy;

            if (_player != null)
            {
                _player.OnHealthChanged += RefreshPlayerHealth;
                _player.OnManaChanged += RefreshPlayerMana;
            }
        }

        private void Unbind()
        {
            if (_playerStats != null) _playerStats.OnStatsChanged -= RefreshPlayer;
            if (_enemyStats != null) _enemyStats.OnStatisticsChanged -= RefreshEnemy;
            if (_player != null)
            {
                _player.OnHealthChanged -= RefreshPlayerHealth;
                _player.OnManaChanged -= RefreshPlayerMana;
            }
        }

        private void RefreshPlayer()
        {
            _coinMultiplier.text = "x1";
            RefreshPlayerHealth();
            RefreshPlayerMana();
            _attackPlayer.text = FormatValue(_playerStats.GetStat(SkillType.AttackDamage));
            _defensePlayer.text = FormatValue(_playerStats.GetStat(SkillType.DefenseAmount));
            _regenPlayer.text = FormatValue(_playerStats.GetStat(SkillType.HealthRegen)) + "/s";
        }
        private void RefreshPlayerHealth()
        {
            _healthPlayer.text = "HP " + Utilityku.FormatNumber((long)Player.Player.Instance.CurrentHealth) + " / " + Utilityku.FormatNumber((long)PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint));
        }
        private void RefreshPlayerMana()
        {
            _manaPlayer.text = "MP " + Utilityku.FormatNumber((long)Player.Player.Instance.CurrentMana) + " / " + Utilityku.FormatNumber((long)PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint));
        }
        private void RefreshEnemy()
        {
            // Use EnemyStatisticsService for real-time enemy stats
            if (_enemyStats != null)
            {
                _countEnemy.text      = _enemyStats.GetAliveCount().ToString();
                _avgHealthEnemy.text  = FormatValue(_enemyStats.GetAverageHealth());
                _avgAttackEnemy.text  = FormatValue(_enemyStats.GetAverageAttack());
                _avgDefenseEnemy.text = FormatValue(_enemyStats.GetAverageDefense());
                _avgEvasionEnemy.text = FormatValue(_enemyStats.GetAverageEvasion());
            }
            else
            {
                _countEnemy.text      = "";
                _avgHealthEnemy.text  = "";
                _avgAttackEnemy.text  = "";
                _avgDefenseEnemy.text = "";
                _avgEvasionEnemy.text = "";
            }
        }

        private static string FormatValue(float value)
        {
            // Percentage-based stats display as whole numbers
            if (Mathf.Approximately(value, Mathf.Floor(value)))
                return value.ToString("0");
            return value.ToString("F1");
        }
    }
}