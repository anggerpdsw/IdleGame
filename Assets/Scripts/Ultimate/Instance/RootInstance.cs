using UnityEngine;
using IdleDefenseSurvival.Enemy;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using System;
using DG.Tweening;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// RootInstance handles the actual root behavior.
    /// It's an instant radial effect emanating from the player.
    /// This is the actual GameObject component, separate from RootHandler (factory).
    /// </summary>
    public class RootInstance : MonoBehaviour
    {
        private readonly string UltimateID = UltimateDMG.Root.ToString();
        [Header("Effect Duration")]
        [SerializeField] private float _effectDuration = 3f;

        [Header("Visual/Audio")]
        [Tooltip("Particle effect prefab for the root")]
        [SerializeField] private GameObject _rootEffect;
        [Tooltip("Audio clip for root")]
        [SerializeField] private AudioClip _rootSoundClip;
        [SerializeField] private float _soundVolume = 1f;

        // Runtime references
        private Player.Player _player;
        private Sequence _sequence;
        private const float GrowScaleDuration = 0.25f;
        private const float SettleDuration = 0.10f;
        private const float DestroyDuration = 0.20f;
        private bool _modifierRemoved;

        /// <summary>
        /// Initialize root with player reference and max radius.
        /// Called by RootHandler after instantiation.
        /// </summary>
        public void Initialize(Player.Player player, UltimateData rootData)
        {
            _player = player;
            _effectDuration = rootData.GetDuration();
            _modifierRemoved = false;
            transform.localScale = Vector3.zero;

            ModifierManager.Instance.AddStatModifiers(ModifierSource.Ultimate, UltimateID,
                (SkillType.DefenseAmount, ModifierMode.Percent, 100f),
                (SkillType.Evasion, ModifierMode.Flat, 10f)
            );

            // Spawn visual effects
            SpawnRootEffects();
        }

        /// <summary>
        /// Spawn visual and audio effects at root center.
        /// </summary>
        private void SpawnRootEffects()
        {
            // Show effect if assigned
            if (_rootEffect != null) _rootEffect.SetActive(true);

            PlayEffect();
                
            // Play sound effect if assigned
            Utilityku.PlaySfx(_player.SfxSource, _rootSoundClip, _soundVolume);
        }

        private void PlayEffect()
        {
            _sequence?.Kill();

            _sequence = DOTween.Sequence();
            _sequence
                .Append(transform.DOScale(Vector3.one * 1.08f, GrowScaleDuration)
                    .SetEase(Ease.OutBack))
                .Append(transform.DOScale(Vector3.one, SettleDuration)
                    .SetEase(Ease.OutQuad))
                .AppendInterval(_effectDuration)
                .Append(transform.DOScale(Vector3.zero, DestroyDuration)
                    .SetEase(Ease.InBack))
                .OnComplete(EndUltimate)
                .SetLink(gameObject);
        }

        private void EndUltimate()
        {
            if (_modifierRemoved) return;
            _modifierRemoved = true;
            RemoveModifiers();
            Destroy(gameObject);
        }

        private void RemoveModifiers()
        {
            ModifierManager.Instance.RemoveStatModifiers(UltimateID,
                SkillType.DefenseAmount,
                SkillType.Evasion
            );
        }

        private void OnDestroy()
        {
            if (!_modifierRemoved) RemoveModifiers();
        }
    }
}
