using System.Collections;
using UnityEngine;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerShieldController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _shieldRenderer;

        private Player _player;
        private float _currentShield;
        private float _maxShield;
        private float _guardianShield;
        private bool _granted;
        private float _cooldown;
        private bool _coolingDown;

        public void Configure(Player player) => _player = player;

        public void Initialize()
        {
            _player.UI.RefreshShieldCooldown(false, 0f, 1f);
        }

        public void Tick()
        {
            if (_coolingDown)
            {
                _cooldown -= Time.deltaTime;
                _player.UI.RefreshShieldCooldown(true, _cooldown, CardModifierService.GetShieldCooldown());

                if (_cooldown <= 0f)
                {
                    _coolingDown = false;
                    _player.UI.RefreshShieldCooldown(false, 0f, 1f);
                }
                return;
            }

            float maxHp = PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint);

            if (_player.CurrentHealth >= maxHp && !_granted)
            {
                float percent = CardModifierService.GetEffectResult(CardEffectType.Shield, 0f);
                _maxShield = maxHp * percent;
                _currentShield = _maxShield;
                _granted = true;
                RefreshVisual();
            }
            else if (_player.CurrentHealth < maxHp)
                _granted = false;
        }

        public float Absorb(float damage)
        {
            float absorbed = Mathf.Min(_currentShield, Mathf.Max(0f, damage));
            _currentShield -= absorbed;

            if (_currentShield <= 0f && absorbed > 0f)
            {
                _currentShield = 0f;
                _granted = false;
                _coolingDown = true;
                _cooldown = CardModifierService.GetShieldCooldown();
            }

            RefreshVisual();
            return absorbed;
        }

        public float AbsorbGuardian(float damage)
        {
            float absorbed = Mathf.Min(_guardianShield, Mathf.Max(0f, damage));
            _guardianShield -= absorbed;
            RefreshVisual();
            return absorbed;
        }

        public void GrantGuardianShield(float amount)
        {
            _guardianShield = Mathf.Max(0f, amount);
            RefreshVisual();
        }

        public void AddShield(float amount, float duration = 4f)
        {
            if (amount <= 0f) return;

            _currentShield += amount;
            _maxShield = _currentShield;
            _granted = true;
            _coolingDown = false;

            StartCoroutine(ExpireShield(amount, duration));
            RefreshVisual();
        }

        private IEnumerator ExpireShield(float amount, float duration)
        {
            yield return new WaitForSeconds(duration);
            _currentShield = Mathf.Max(0f, _currentShield - amount);

            if (_currentShield <= 0f)
            {
                _currentShield = 0f;
                _granted = false;
            }

            RefreshVisual();
        }

        public bool IsUnregenerationActive()
        {
            bool active = PlayerStatusEffectManager.Instance?.IsUnregenerationActive ?? false;
            _player.UI.SetUnregeneration(active);
            return active;
        }

        private void RefreshVisual()
        {
            if (_shieldRenderer == null) return;

            float total = _currentShield + _guardianShield;
            bool visible = total > 0f;
            _shieldRenderer.gameObject.SetActive(visible);
            if (!visible) return;

            float max = _maxShield + _guardianShield;
            float percent = max > 0f ? total / max : 0f;

            _shieldRenderer.transform.localScale =
                new Vector3(1f, Mathf.Lerp(0f, .5f, percent), 1f);

            Color color = GameColors.red;
            if (percent > .75f)
                color = Color.Lerp(GameColors.yellow, GameColors.green,
                    Mathf.InverseLerp(.75f, 1f, percent));
            else if (percent > .3f)
                color = Color.Lerp(GameColors.red, GameColors.yellow,
                    Mathf.InverseLerp(.3f, .75f, percent));

            color.a = Mathf.Lerp(.3f, .7f, percent);
            _shieldRenderer.color = color;
        }
    }
}