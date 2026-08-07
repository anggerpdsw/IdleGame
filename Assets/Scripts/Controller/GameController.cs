
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
        [SerializeField] private TextMeshProUGUI _attackPlayer;
        [SerializeField] private TextMeshProUGUI _defensePlayer;
        [SerializeField] private TextMeshProUGUI _regenPlayer;

        [Header("Enemy")]
        [SerializeField] private TextMeshProUGUI _countEnemy;
        [SerializeField] private TextMeshProUGUI _avgHealthEnemy;
        [SerializeField] private TextMeshProUGUI _avgAttackEnemy;
        [SerializeField] private TextMeshProUGUI _avgDefenseEnemy;
        [SerializeField] private TextMeshProUGUI _avgEvasionEnemy;

        private void Start()
        {
            RefreshPlayer();
        }

        private void OnEnable()
        {
            PlayerStatsManager.Instance.OnStatsChanged += RefreshPlayer;
        }

        private void OnDisable()
        {
            PlayerStatsManager.Instance.OnStatsChanged -= RefreshPlayer;
        }

        private void RefreshPlayer()
        {
            var stats = PlayerStatsManager.Instance;

            _coinMultiplier.text = "x1";
            _healthPlayer.text = "HP " + Utilityku.FormatNumber((long)Player.Player.Instance.CurrentHealth) + " / " + Utilityku.FormatNumber((long)stats.GetStat(SkillType.HealthPoint));
            _attackPlayer.text = FormatValue(stats.GetStat(SkillType.AttackDamage));
            _defensePlayer.text = FormatValue(stats.GetStat(SkillType.DefenseAmount));
            _regenPlayer.text = FormatValue(stats.GetStat(SkillType.HealthRegen)) + "/s";
        }
        
        private void RefreshEnemy()
        {
            // Use EnemyStatisticsService for real-time enemy stats
            var stats = EnemyStatisticsManager.Instance;
            if (stats != null)
            {
                _countEnemy.text      = stats.GetAliveCount().ToString();
                _avgHealthEnemy.text  = FormatValue(stats.GetAverageHealth());
                _avgAttackEnemy.text  = FormatValue(stats.GetAverageAttack());
                _avgDefenseEnemy.text = FormatValue(stats.GetAverageDefense());
                _avgEvasionEnemy.text = FormatValue(stats.GetAverageEvasion());
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

        private void Update()
        {
            RefreshEnemy();
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