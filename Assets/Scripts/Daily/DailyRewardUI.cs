using System;
using DG.Tweening;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Reward;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI
{
    public class DailyRewardUI : MonoBehaviour
    {
        private const int TotalSlots = GameConstants.REWARD_COUNT;

        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _subtitleLabel;
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private GameObject _slotViewPrefab;
        [SerializeField] private Button _closeButton;
        private readonly DailyRewardSlot[] _slots = new DailyRewardSlot[TotalSlots];

        private DailyRewardService Service => DailyRewardManager.Instance?.Service;

        private void Awake()
        {
            BuildSlots();
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void BuildSlots()
        {
            if (_slotContainer == null || _slotViewPrefab == null)
            {
                Debug.LogError("[DailyRewardUI] Slot container or prefab not assigned");
                return;
            }

            for (int i = 0; i < TotalSlots; i++)
            {
                if (Instantiate(_slotViewPrefab, _slotContainer).TryGetComponent<DailyRewardSlot>(out var slot))
                {
                    slot.Initialize(i);
                    _slots[i] = slot;
                }
                else
                {
                    Debug.LogError($"[DailyRewardUI] Slot prefab missing DailyRewardSlot component at index {i}");
                }
            }
        }

        private void OnEnable()
        {
            RefreshUI();
            PlayEnterAnimation();
            StartCoroutine(CountdownUpdater());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private System.Collections.IEnumerator CountdownUpdater()
        {
            var wait = new WaitForSeconds(1f);

            while (true)
            {
                RefreshUI();
                yield return wait;
            }
        }

        public void Close() => UIManager.Instance.HidePopup(this);

        private void RefreshUI()
        {
            if (_panelRoot == null) return;

            var service = Service;
            if (service == null) return;

            var utcNow = DateTime.UtcNow;
            var state = service.GetState(utcNow);

            RefreshHeader(state);
            RefreshSlots(utcNow);
        }

        private void RefreshHeader(DailyRewardState state)
        {
            if (_subtitleLabel != null)
                _subtitleLabel.text = state == DailyRewardState.CompletedToday
                    ? "Your streak is complete for today."
                    : "Claim the next reward to keep the streak alive.";
        }

        private void RefreshSlots(DateTime utcNow)
        {
            var service = Service;
            if (service == null) return;

            var provider = service.RewardProvider;
            if (provider == null) return;

            for (int i = 0; i < TotalSlots; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                var reward = provider.GetReward(i);
                if (reward == null) continue;

                var slotState = service.GetSlotState(i, utcNow);
                var iconKey = reward.Type == RewardType.Item && !string.IsNullOrEmpty(reward.Id) ? reward.Id : reward.Type.ToString();
                var icon = ItemResources.GetItemSource(iconKey);
                var remaining = Service?.GetRemainingTime(DateTime.UtcNow);
                slot.Refresh(reward, slotState, i, icon, 
                    slotState == DailyRewardState.Waiting ? remaining : null);
            }
        }

        private void PlayEnterAnimation()
        {
            if (_panelRoot == null) return;

            _panelRoot.localScale = Vector3.one * 0.96f;
            _canvasGroup.alpha = 0f;

            _canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).SetLink(gameObject);
            _panelRoot.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetLink(gameObject);

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.Background == null || slot.Background.rectTransform == null) continue;

                var row = slot.Background.rectTransform;
                row.localScale = Vector3.one * 0.9f;
                var delay = 0.04f * i;
                row.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetDelay(delay).SetLink(gameObject);
            }
        }

        private static string GetCountdownText(TimeSpan? remainingTime, DailyRewardState state)
        {
            if (state == DailyRewardState.Claimable || remainingTime == null) return string.Empty;
            if (remainingTime.Value <= TimeSpan.Zero) return "Ready to claim";
            return $"Next reward in {Utilityku.FormatDuration(remainingTime.Value)}";
        }

    }
}