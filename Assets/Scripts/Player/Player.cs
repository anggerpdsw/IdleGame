using System;
using System.Collections;
using UnityEngine;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Card;
using IdleDefenseSurvival.Card.Behavior;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerCombatController))]
    [RequireComponent(typeof(PlayerVitalsController))]
    [RequireComponent(typeof(PlayerShieldController))]
    [RequireComponent(typeof(PlayerMovementController))]
    [RequireComponent(typeof(PlayerUltimateController))]
    [RequireComponent(typeof(PlayerEffectsView))]
    [RequireComponent(typeof(PlayerUIController))]
    public sealed class Player : MonoBehaviour
    {
        private static Player _instance;
        public static Player Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        public event Action OnHealthChanged
        {
            add => Vitals.OnHealthChanged += value;
            remove => Vitals.OnHealthChanged -= value;
        }

        public event Action OnManaChanged
        {
            add => Vitals.OnManaChanged += value;
            remove => Vitals.OnManaChanged -= value;
        }

        [Header("Core")]
        [SerializeField] private Transform _visual;

        [Header("Projectile")]
        [SerializeField] private float _projectileSpacingRadius = .25f;
        [SerializeField] private float _projectileSpreadAngle = 15f;

        public PlayerCombatController Combat { get; private set; }
        public PlayerVitalsController Vitals { get; private set; }
        public PlayerShieldController Shield { get; private set; }
        public PlayerMovementController Movement { get; private set; }
        public PlayerUltimateController Ultimate { get; private set; }
        public PlayerEffectsView Effects { get; private set; }
        public PlayerUIController UI { get; private set; }

        public float CurrentHealth => Vitals.CurrentHealth;
        public float MaxHealth => Vitals.MaxHealth;
        public float CurrentMana => Vitals.CurrentMana;
        public float MaxMana => Vitals.MaxMana;
        public float AttackRange { get; private set; }
        public AudioSource SfxSource => UI.SfxSource;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            Combat = GetComponent<PlayerCombatController>();
            Vitals = GetComponent<PlayerVitalsController>();
            Shield = GetComponent<PlayerShieldController>();
            Movement = GetComponent<PlayerMovementController>();
            Ultimate = GetComponent<PlayerUltimateController>();
            Effects = GetComponent<PlayerEffectsView>();
            UI = GetComponent<PlayerUIController>();

            Combat.Configure(this, _projectileSpacingRadius, _projectileSpreadAngle);
            Vitals.Configure(this);
            Shield.Configure(this);
            Movement.Configure(this);
            Ultimate.Configure(this);
            Effects.Configure(this);
            UI.Configure(this);

            Effects.DisableAll();
        }

        private void Start() => StartCoroutine(InitializePlayer());

        private IEnumerator InitializePlayer()
        {
            yield return new WaitUntil(() => BootstrapController.IsInitialized);
            yield return new WaitUntil(() =>
                PlayerStatsManager.Instance != null &&
                BaseStatLoader.Instance != null);

            if (SaveManager.Instance != null)
                yield return new WaitUntil(() => SaveManager.Instance.IsSaveLoaded);

            ReloadStats();

            Combat.Initialize();
            Movement.Initialize();
            Ultimate.Initialize();
            Shield.Initialize();

            CardModifierService.Refresh();
            CardModifierService.OnModifierChanged += UI.RefreshCardBonusUI;
            if (CardRuntimeManager.Instance != null)
                CardRuntimeManager.Instance.OnBehaviorsUpdated += UI.RefreshCardBonusUI;

            UI.RefreshCardBonusUI();
        }

        private void Update()
        {
            Combat.Tick();
            Ultimate.Tick();
            Vitals.TickRegeneration();
            Shield.Tick();
            UI.Tick();

            FaceTarget(Combat.CurrentTarget);
        }

        private void FixedUpdate() => Movement.FixedTick();

        public void ReloadStats()
        {
            BaseStatLoader.Instance.LoadBaseStats();
            AttackRange = PlayerStatsManager.Instance.GetStat(SkillType.AttackRange);
            Vitals.ResetToMax();
            UI.RefreshAll();
            UI.DrawAttackRange(AttackRange);
        }

        private void FaceTarget(Transform target)
        {
            if (target == null || _visual == null) return;
            Vector3 scale = _visual.localScale;
            scale.x = target.position.x < transform.position.x ? -1f : 1f;
            _visual.localScale = scale;
        }

        // Compatibility facade: existing callers do not need to know the new components.
        public void SetNextProjectileAsLastBullet(bool value) => Combat.SetNextProjectileAsLastBullet(value);
        public bool GetNextAttackGuaranteedHit() => Combat.NextAttackGuaranteedHit;
        public float GetNextAttackDamageMultiplier() => Combat.NextAttackDamageMultiplier;
        public float GetNextAttackCriticalChanceBonus() => Combat.NextAttackCriticalChanceBonus;
        public void SpawnBulletStormBurst() => Combat.SpawnBulletStormBurst();
        public void SpawnInfiniteArsenalProjectile() => Combat.SpawnInfiniteArsenalProjectile();

        public bool CanAfford(float amount) => Vitals.CanAfford(amount);
        public bool SpendMana(float amount) => Vitals.SpendMana(amount);
        public void GainMana(float amount) => Vitals.GainMana(amount);
        public void Heal(float amount, bool lifeSteal = false) => Vitals.Heal(amount, lifeSteal);
        public void StartHealOverTime(float amount, float duration = 10f) => Vitals.StartHealOverTime(amount, duration);
        public void StartManaOverTime(float amount, float duration = 10f) => Vitals.StartManaOverTime(amount, duration);

        public float TakeDamage(DamageData data, bool canEvade = true) => Vitals.TakeDamage(data, canEvade);
        public void GrantGuardianShield(float amount) => Shield.GrantGuardianShield(amount);
        public void AddShield(float amount, float duration = 4f) => Shield.AddShield(amount, duration);
        public bool IsUnregenerationActive() => Shield.IsUnregenerationActive();

        public bool ManualCastUltimate(string id) => Ultimate.ManualCastUltimate(id);
        public void SpawnTank() => Ultimate.SpawnTank();
        public bool TryGetTankSpawnPosition(out Vector3 position) => Ultimate.TryGetTankSpawnPosition(out position);

        public void SetBarrierEffect(bool enabled) => Effects.SetBarrier(enabled);
        public void SetIceEffect(bool enabled) => Effects.SetIce(enabled);
        public void SetBurnEffect(bool enabled) => Effects.SetBurn(enabled);
        public void SetBerserkerEffect(bool enabled) => Effects.SetBerserker(enabled);
        public void SetVampireEffect(bool enabled) => Effects.SetVampire(enabled);

        private void OnDestroy()
        {
            CardModifierService.OnModifierChanged -= UI.RefreshCardBonusUI;
            if (CardRuntimeManager.Instance != null)
                CardRuntimeManager.Instance.OnBehaviorsUpdated -= UI.RefreshCardBonusUI;

            if (_instance == this) _instance = null;
        }
    }
}