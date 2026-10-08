using System;
using System.Collections;
using UnityEngine;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Card.Behavior;

namespace IdleDefenseSurvival.Player
{
    public sealed class PlayerVitalsController : MonoBehaviour
    {
        private static readonly WaitForSeconds OneSecond = new(1f);

        public event Action OnHealthChanged;
        public event Action OnManaChanged;

        public float CurrentHealth { get; private set; }
        public float CurrentMana { get; private set; }

        public float MaxHealth => PlayerStatsManager.Instance != null
            ? PlayerStatsManager.Instance.GetStat(SkillType.HealthPoint) : 0f;
        public float MaxMana => PlayerStatsManager.Instance != null
            ? PlayerStatsManager.Instance.GetStat(SkillType.ManaPoint) : 0f;

        private Player _player;
        private float _regenTimer;
        private bool _immune;
        private string _lastDamageSource;

        public void Configure(Player player) => _player = player;

        public void ResetToMax()
        {
            CurrentHealth = MaxHealth;
            CurrentMana = MaxMana;
            NotifyHealth();
            NotifyMana();
        }

        public void TickRegeneration()
        {
            if (_player.Shield.IsUnregenerationActive()) return;

            _regenTimer += Time.deltaTime;
            while (_regenTimer >= 1f)
            {
                _regenTimer -= 1f;

                if (CurrentHealth < MaxHealth)
                    Heal(Mathf.Min(
                        PlayerStatsManager.Instance.GetStat(SkillType.HealthRegen),
                        MaxHealth - CurrentHealth));

                if (CurrentMana < MaxMana)
                    GainMana(Mathf.Min(
                        PlayerStatsManager.Instance.GetStat(SkillType.ManaRegen),
                        MaxMana - CurrentMana));
            }
        }

        public bool CanAfford(float amount) => CurrentMana >= amount;

        public bool SpendMana(float amount)
        {
            if (amount < 0f || !CanAfford(amount)) return false;
            CurrentMana -= amount;
            NotifyMana();
            return true;
        }

        public void GainMana(float amount)
        {
            if (amount <= 0f || CurrentMana >= MaxMana) return;
            CurrentMana = Mathf.Min(CurrentMana + amount, MaxMana);
            NotifyMana();
        }

        public float TakeDamage(DamageData data, bool canEvade = true)
        {
            if (canEvade && Utilityku.Chance(
                PlayerStatsManager.Instance.GetStat(SkillType.Evasion)))
                return Popup(0f, DamageType.Miss, CriticalType.None);

            if (_immune || CardModifierService.HasAngelImmunity())
                return Popup(0f, DamageType.Miss, CriticalType.None);

            float defense = PlayerStatsManager.Instance.GetStat(SkillType.DefenseAmount);
            float damage = Mathf.Max(0f,
                Utilityku.FinalDamage(Mathf.Max(0f, data.Damage), defense));

            damage -= _player.Shield.Absorb(Mathf.Max(0f, damage));
            damage -= _player.Shield.AbsorbGuardian(Mathf.Max(0f, damage));

            if (damage <= 0f)
            {
                _player.UI.RefreshAll();
                return 0f;
            }

            float before = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, MaxHealth);
            _lastDamageSource = data.Source;

            Popup(damage, DamageType.Normal, CriticalType.None);
            NotifyHealth();

            CardRuntimeManager.Instance?.DispatchPlayerDamaged(
                before - CurrentHealth, CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f)
                Die();

            return damage;
        }

        public void Heal(float amount, bool lifeSteal = false)
        {
            if (amount <= 0f || CurrentHealth >= MaxHealth) return;

            float before = CurrentHealth;
            CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
            float actual = CurrentHealth - before;
            if (actual <= 0f) return;

            CardRuntimeManager.Instance?.DispatchPlayerHealed(
                actual, CurrentHealth, MaxHealth);

            if (lifeSteal)
                CardRuntimeManager.Instance?.DispatchPlayerLifeSteal(
                    actual, CurrentHealth, MaxHealth);

            NotifyHealth();
            if (actual >= 1f)
                Popup(actual, DamageType.Heal, CriticalType.None, "+");
        }

        public void StartHealOverTime(float total, float duration = 10f)
        {
            if (total > 0f && duration > 0f)
                StartCoroutine(HealRoutine(total / duration, duration));
        }

        public void StartManaOverTime(float total, float duration = 10f)
        {
            if (total > 0f && duration > 0f)
                StartCoroutine(ManaRoutine(total / duration, duration));
        }

        private IEnumerator HealRoutine(float tick, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += 1f)
            {
                yield return OneSecond;
                Heal(tick);
                Popup(tick, DamageType.Heal, CriticalType.None, "+");
            }
        }

        private IEnumerator ManaRoutine(float tick, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += 1f)
            {
                yield return OneSecond;
                GainMana(tick);
                Popup(tick, DamageType.Mana, CriticalType.None, "+");
            }
        }

        private void Die()
        {
            if (Utilityku.Chance(PlayerStatsManager.Instance.GetStat(SkillType.DeathDefy)))
            {
                Heal(MaxHealth * .12f);
                StartCoroutine(ImmunityRoutine(22f));
                return;
            }

            if (CardModifierService.TryTriggerAngel())
            {
                CurrentHealth = MaxHealth;
                NotifyHealth();
                Popup(MaxHealth, DamageType.Heal, CriticalType.None, "⚕ ");
                return;
            }

            WaveManager.Instance.Defeat(_lastDamageSource);
        }

        private IEnumerator ImmunityRoutine(float duration)
        {
            _immune = true;
            _player.Effects.SetBarrier(true);
            yield return new WaitForSeconds(duration);
            _immune = false;
            _player.Effects.SetBarrier(false);
        }

        private void NotifyHealth()
        {
            _player.UI.RefreshHealth(CurrentHealth, MaxHealth);
            OnHealthChanged?.Invoke();
        }

        private void NotifyMana()
        {
            _player.UI.RefreshMana(CurrentMana, MaxMana);
            OnManaChanged?.Invoke();
        }

        private float Popup(float amount, DamageType type, CriticalType critical, string prefix = "")
        {
            if (DamagePopupManager.Instance != null)
            {
                var data = new DamagePopupData(amount, type, critical, prefix);
                DamagePopupManager.Instance.ShowDamage(
                    transform.position + Vector3.up * .5f, data, transform);
            }
            return 0f;
        }
    }
}