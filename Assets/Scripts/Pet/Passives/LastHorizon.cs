using UnityEngine;
using IdleDefenseSurvival.Player;

namespace IdleDefenseSurvival.Pet.Passives
{
    /// <summary>
    /// Voidling passive ability - LastHorizon.
    /// Activates when player HP drops below emergency threshold.
    /// Grants shield and damage reduction to player.
    /// </summary>
    public class LastHorizon
    {
        private const float SHIELD_PERCENT = 0.08f; // 8% player max HP
        private const float DAMAGE_REDUCTION_PERCENT = 0.20f; // 20% DR
        private const float EFFECT_DURATION = 3f;
        private const float PASSIVE_COOLDOWN = 25f;

        private PetRuntime _pet;
        private bool _isActive;
        private float _cooldownTimer;

        public LastHorizon(PetRuntime pet)
        {
            _pet = pet;
            _isActive = false;
            _cooldownTimer = 0f;
        }

        public void Update(float deltaTime)
        {
            // Tick cooldown
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= deltaTime;
                if (_cooldownTimer < 0f)
                    _cooldownTimer = 0f;
            }
        }

        public bool CanActivate()
        {
            return !_isActive && _cooldownTimer <= 0f;
        }

        public void Activate()
        {
            if (!CanActivate()) return;

            var player = Player.Player.Instance;
            if (player == null) return;

            _isActive = true;
            _cooldownTimer = PASSIVE_COOLDOWN;

            // Calculate shield amount
            float shieldAmount = player.MaxHealth * SHIELD_PERCENT;

            // Apply shield to player
            // Note: Current Player class has shield system but it's tied to card effects
            // For pet passive, we apply a temporary "Last Horizon" buff
            // This requires extending Player to support external shields
            // Workaround: Apply direct heal instead of shield for now
            player.Heal(shieldAmount);

            // TODO: Apply damage reduction buff to player
            // Requires PlayerStatusEffectController or modifier system extension

            // Visual feedback
            player.SetBarrierEffect(true);

            // Start duration timer
            PetManager.Instance?.StartCoroutine(LastHorizonDurationCoroutine(player));

            Debug.Log($"[LastHorizon] Activated! Shield: {shieldAmount:F0}, Duration: {EFFECT_DURATION}s");
        }

        private System.Collections.IEnumerator LastHorizonDurationCoroutine(Player.Player player)
        {
            yield return new UnityEngine.WaitForSeconds(EFFECT_DURATION);

            _isActive = false;

            // Remove visual effect
            if (player != null)
            {
                player.SetBarrierEffect(false);
            }

            // TODO: Remove damage reduction buff

            Debug.Log("[LastHorizon] Effect expired");
        }

        public float GetCooldownRemaining()
        {
            return _cooldownTimer;
        }

        public float GetCooldownPercent()
        {
            return PASSIVE_COOLDOWN > 0f ? _cooldownTimer / PASSIVE_COOLDOWN : 0f;
        }
    }
}
