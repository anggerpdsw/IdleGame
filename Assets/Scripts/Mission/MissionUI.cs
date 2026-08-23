using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Mission;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.UI
{
    /// <summary>
    /// Renders the active mission slots. Slot count tracks
    /// <see cref="AccountData.maxMission"/> via MissionService — the pool grows
    /// but never shrinks so the layout stays stable when the cap changes.
    ///</summary>
    public class MissionUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _subtitleLabel;
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private GameObject _slotViewPrefab;
        [SerializeField] private Button _closeButton;
        [SerializeField, Tooltip("Soft cap on pool size; additional slots are instantiated as needed.")]
        private int _initialPoolSize = 1;

        private readonly List<MissionSlot> _slots = new();

        private MissionService Service => MissionService.Instance;

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            EnsurePool(Service?.GetMaxMission() ?? Mathf.Max(1, _initialPoolSize));
            Subscribe();
            RefreshUI();
            PlayEnterAnimation();
            StartCoroutine(CountdownUpdater());
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopAllCoroutines();
        }

        private void Subscribe()
        {
            if (Service == null) return;
            Service.OnMissionsChanged += OnMissionsChanged;
            Service.OnMissionStatusChanged += OnMissionStatusChanged;
            Service.OnMissionProgressChanged += OnMissionProgressChanged;
        }

        private void Unsubscribe()
        {
            if (Service == null) return;
            Service.OnMissionsChanged -= OnMissionsChanged;
            Service.OnMissionStatusChanged -= OnMissionStatusChanged;
            Service.OnMissionProgressChanged -= OnMissionProgressChanged;
        }

        private void OnMissionsChanged()
        {
            EnsurePool(Service?.GetMaxMission() ?? 0);
            RefreshUI();
        }

        private void OnMissionStatusChanged(MissionInstance _) => RefreshUI();
        private void OnMissionProgressChanged(MissionInstance _) => RefreshUI();

        private void EnsurePool(int desired)
        {
            if (_slotContainer == null || _slotViewPrefab == null)
            {
                Debug.LogError("[MissionUI] Slot container or prefab not assigned");
                return;
            }

            int target = Mathf.Max(0, desired);
            while (_slots.Count < target)
            {
                if (Instantiate(_slotViewPrefab, _slotContainer).TryGetComponent<MissionSlot>(out var slot))
                {
                    slot.Initialize(_slots.Count, HandleClaim, HandleCancel);
                    _slots.Add(slot);
                }
                else
                {
                    Debug.LogError($"[MissionUI] Slot prefab missing MissionSlot component at index {_slots.Count}");
                    break;
                }
            }
        }

        private void HandleClaim(int slotIndex, string instanceId)
        {
            Service?.ClaimMission(instanceId);
            Close();
        }

        private void HandleCancel(int slotIndex, string instanceId)
        {
            Service?.CancelMission(instanceId);
            RefreshUI();
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
            var max = service.GetMaxMission();

            RefreshHeader(service);
            RefreshSlots(service, max);
        }

        private void RefreshHeader(MissionService service)
        {
            if (_subtitleLabel == null) return;
            var active = service.GetActiveMissions().Count;
            int max = service.GetMaxMission();
            int completed = service.GetAllMissions().Count(m => m.status == MissionStatus.Completed);
            _subtitleLabel.text = completed > 0
                ? $"{completed} ready to claim — {active}/{max} active."
                : $"{active}/{max} missions active.";
        }

        private void RefreshSlots(MissionService service, int max)
        {
            var missions = service.GetAllMissions();

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                bool inRange = i < max;
                if (slot.gameObject.activeSelf != inRange)
                    slot.gameObject.SetActive(inRange);
                if (!inRange) continue;

                var mission = missions.FirstOrDefault(m => m.slotIndex == i);
                if (mission == null)
                {
                    slot.Refresh(null, null, null);
                    continue;
                }

                var template = service.GetTemplate(mission.missionId);
                slot.Refresh(mission, template, GetMissionIcon(mission, template));
            }
        }

        private static Sprite GetMissionIcon(MissionInstance m, MissionTemplate t)
        {
            if (t == null) return null;

            switch (t.type)
            {
                case MissionEventType.EnemyKilled:
                case MissionEventType.BossKilled:
                case MissionEventType.SpecificEnemyKilled:
                    string enemyId = !string.IsNullOrEmpty(m.targetId) ? m.targetId : t.targetId;
                    return string.IsNullOrEmpty(enemyId) ? null : EnemyResources.GetEnemySprite(enemyId);

                case MissionEventType.CurrencyEarned:
                    string currencyId = !string.IsNullOrEmpty(t.targetId) ? t.targetId : "Gold";
                    return ItemResources.GetItemSource(currencyId);

                case MissionEventType.Blacksmithing:
                    string eqId = !string.IsNullOrEmpty(m.targetId) ? m.targetId : null;
                    return string.IsNullOrEmpty(eqId) ? null : ItemResources.GetItemSource($"Equipment/{eqId}");

                case MissionEventType.WaveCompleted:
                    return null;

                default:
                    return null;
            }
        }

        private void PlayEnterAnimation()
        {
            if (_panelRoot == null || _canvasGroup == null) return;

            _panelRoot.localScale = Vector3.one * 0.96f;
            _canvasGroup.alpha = 0f;

            _canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).SetLink(gameObject);
            _panelRoot.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetLink(gameObject);

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.Background == null) continue;

                var row = slot.Background.rectTransform;
                row.localScale = Vector3.one * 0.9f;
                var delay = 0.04f * i;
                row.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetDelay(delay).SetLink(gameObject);
            }
        }
    }
}
