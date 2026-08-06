using UnityEngine;
using UnityEngine.UI;
using IdleDefenseSurvival.Manager;
using DG.Tweening;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.UI
{
    public class IdleRewardUI : MonoBehaviour
    {
        [SerializeField] private Button _claimButton;
        [SerializeField] private Image _radialProgress;
        [SerializeField] private GameObject _notif;
    
        private IdleRewardManager _reward => IdleRewardManager.Instance;
        private bool _lastCanClaim;
        private Sequence _sequence;
        private Vector2 _originalPos;
        private Vector3 _originalScale;
        private RectTransform rt;

        private void Awake()
        {
            rt = _radialProgress.rectTransform;
            _originalPos = rt.anchoredPosition;
            _originalScale = rt.localScale;
        }

        private void Start()
        {
            Initialize();
            _lastCanClaim = !_reward.CanClaim;
            RefreshUI();
        }

        public void Initialize()
        {
            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(OnClaim);
        }

        private void Update()
        {
            if (_reward == null) return;
            RefreshUI();
        }

        private void RefreshUI()
        {
            bool canClaim = _reward.CanClaim;

            _radialProgress.fillAmount = _reward.Progress;

            if (canClaim != _lastCanClaim)
            {
                _lastCanClaim = canClaim;

                _claimButton.interactable = canClaim;
                _notif.SetActive(canClaim);

                if (canClaim)
                    StartShake();
                else
                    StopShake();
            }
        }

        private void OnClaim()
        {
            RewardManager.Instance.Show(
                new List<RewardData>() {
                    new(RewardType.Gold, _reward.GoldReward),
                    new(RewardType.Meat, _reward.MeatReward)
                },
                () => { _reward.ResetCount(); });
        }

        private void StartShake()
        {
            StopShake();

            _sequence = DOTween.Sequence()
                .Append(rt.DOScale(1.08f, 0.18f)
                    .SetLoops(2, LoopType.Yoyo))
                .AppendInterval(2f)
                .SetLoops(-1)
                .SetLink(gameObject)
                .SetUpdate(true);
        }

        private void StopShake()
        {
            _sequence?.Kill();
            _sequence = null;

            rt.anchoredPosition = _originalPos;
            rt.localScale = _originalScale;
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }
    }
}