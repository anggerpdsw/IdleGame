using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    /// <summary>HUD synchronization only.</summary>
    public sealed class PlayerUIController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource _sfxSource;
        public AudioSource SfxSource => _sfxSource;

        [Header("Range")]
        [SerializeField] private SpriteRenderer _attackRangeRenderer;
        [SerializeField] private float _attackRangeRotationSpeed = 2f;

        [Header("Bars")]
        [SerializeField] private Slider _healthBar;
        [SerializeField] private Image _fillHealth;
        [SerializeField] private Slider _manaBar;
        [SerializeField] private Image _fillMana;

        [Header("Cooldowns")]
        [SerializeField] private Image _iceCooldown;
        [SerializeField] private Image _burnCooldown;
        [SerializeField] private Image _unregenCooldown;

        [Header("Card Bonus")]
        [SerializeField] private Image _shield;
        [SerializeField] private Image _angel;
        [SerializeField] private GameObject _crazyGambler;
        [SerializeField] private TextMeshProUGUI _crazyGamblerText;
        [SerializeField] private GameObject _desperados;
        [SerializeField] private TextMeshProUGUI _desperadosText;
        [SerializeField] private GameObject _deathChain;
        [SerializeField] private TextMeshProUGUI _deathChainText;

        private Player _player;

        public void Configure(Player player) => _player = player;

        public void Tick()
        {
            UpdateStatusCooldown(_iceCooldown,
                PlayerStatusEffectManager.Instance?.GetIceCooldownFill() ?? 0f);
            UpdateStatusCooldown(_burnCooldown,
                PlayerStatusEffectManager.Instance?.GetBurnCooldownFill() ?? 0f);

            _player.Effects.SetIce(_iceCooldown != null && _iceCooldown.gameObject.activeSelf);
            _player.Effects.SetBurn(_burnCooldown != null && _burnCooldown.gameObject.activeSelf);

            UpdateAngelCooldown();

            if (_attackRangeRenderer != null)
                _attackRangeRenderer.transform.Rotate(
                    0f, 0f, _attackRangeRotationSpeed * Time.deltaTime);
        }

        private void OnEnable()
        {
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged += HandleStatsChanged;
        }

        private void OnDisable()
        {
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnStatsChanged -= HandleStatsChanged;
        }

        private void HandleStatsChanged()
        {
            if (_player != null)
                DrawAttackRange(PlayerStatsManager.Instance.GetStat(SkillType.AttackRange)); // GetStat = include temporary
        }

        public void RefreshAll()
        {
            RefreshHealth(_player.CurrentHealth, _player.MaxHealth);
            RefreshMana(_player.CurrentMana, _player.MaxMana);
        }

        public void RefreshHealth(float current, float max)
        {
            if (_healthBar == null) return;
            _healthBar.maxValue = max;
            _healthBar.value = current;

            float percent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (_fillHealth != null)
                _fillHealth.color = percent > .75f
                    ? Color.Lerp(GameColors.yellow, GameColors.green,
                        Mathf.InverseLerp(.75f, 1f, percent))
                    : Color.Lerp(GameColors.red, GameColors.yellow,
                        Mathf.InverseLerp(0f, .75f, percent));
        }

        public void RefreshMana(float current, float max)
        {
            if (_manaBar == null) return;
            _manaBar.maxValue = max;
            _manaBar.value = current;

            if (_fillMana != null)
            {
                float percent = max > 0f ? Mathf.Clamp01(current / max) : 0f;
                _fillMana.color = Color.Lerp(GameColors.empty, GameColors.blue, percent);
            }
        }

        public void RefreshShieldCooldown(bool active, float remaining, float duration)
        {
            if (_shield == null) return;

            bool visible = active && duration > 0f;
            _shield.gameObject.SetActive(visible);
            _shield.fillAmount = visible
                ? 1f - Mathf.Clamp01(remaining / duration)
                : 0f;
        }

        public void SetUnregeneration(bool active)
        {
            if (_unregenCooldown != null)
                _unregenCooldown.gameObject.SetActive(active);
        }

        public void DrawAttackRange(float range)
        {
            if (_attackRangeRenderer == null) return;
            float diameter = range * 2f;
            _attackRangeRenderer.transform.localScale =
                new Vector3(diameter, diameter, 1f);
            _attackRangeRenderer.color =
                GameColors.debugAtkRangeCyan.WithAlpha(.09f);
        }

        private void UpdateStatusCooldown(Image image, float fill)
        {
            if (image == null) return;
            image.fillAmount = Mathf.Clamp01(fill);
            image.gameObject.SetActive(fill > 0f);
            if (fill <= 0f) image.fillAmount = 0f;
        }

        private void UpdateAngelCooldown()
        {
            if (_angel == null) return;

            if (!CardModifierService.HasEffect(CardEffectType.Immortal))
            {
                _angel.gameObject.SetActive(false);
                return;
            }

            float max = CardModifierService.GetAngelMaxCooldown();
            if (max <= 0f)
            {
                _angel.gameObject.SetActive(false);
                return;
            }

            float remaining = CardModifierService.GetAngelCooldownRemaining();
            _angel.fillAmount = 1f - Mathf.Clamp01(remaining / max);
            _angel.gameObject.SetActive(true);

            if (!CardModifierService.HasAngelImmunity())
                _player.Effects.SetBarrier(false);
        }

        public void RefreshCardBonusUI()
        {
            bool gambler = CardModifierService.HasEffect(CardEffectType.CrazyGambler);
            SetBonus(_crazyGambler, _crazyGamblerText, gambler,
                $"{CardModifierService.GetCrazyGamblerBonus():0}%");

            bool desperados = CardModifierService.HasEffect(CardEffectType.Desperados);
            SetBonus(_desperados, _desperadosText, desperados,
                $"{CardModifierService.GetDesperadosBonus():0}%");

            bool chain = CardModifierService.HasEffect(CardEffectType.DeathChain);
            SetBonus(_deathChain, _deathChainText, chain,
                $"s{CardModifierService.GetDeathChainStack():0}");
        }

        private static void SetBonus(
            GameObject root, TextMeshProUGUI text, bool active, string value)
        {
            if (root != null) root.SetActive(active);
            if (text != null) text.text = active ? value : string.Empty;
        }
    }
}