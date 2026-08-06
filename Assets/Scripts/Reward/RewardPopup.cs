using System;
using System.Collections.Generic;
using DG.Tweening;
using IdleDefenseSurvival.Data;
using IdleDefenseSurvival.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleDefenseSurvival.Reward
{
    public class RewardPopup : MonoBehaviour
    {
        [Header("References All")]
        [SerializeField] private CanvasGroup background;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI resultTitle;
        [SerializeField] private Button claimButton;

        // Reward-specific UI elements
        [Header("Reward Specific")]
        [SerializeField] private Transform content;
        [SerializeField] private RewardSlot slotPrefab;
        [SerializeField] private int maxSlots = 9;

        // Card-specific UI elements
        [Header("Card Roll Specific")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private RectTransform cardContent;
        [SerializeField] private CardRewardSlot cardSlotPrefab;
        [SerializeField] private int maxCardSlots = 10;

        private Action onClaim;
        private readonly List<RewardSlot> _slotPool = new();
        private readonly List<CardRewardSlot> _cardSlotPool = new();
        private Sequence _showSequence;
        private Sequence _hideSequence;

        private bool _isClosing;

        private void Awake()
        {
            // Initialize standard reward slots
            for (int i = 0; i < maxSlots; i++)
            {
                RewardSlot slot = Instantiate(slotPrefab, content);
                slot.gameObject.SetActive(false);
                _slotPool.Add(slot);
            }

            // Initialize card reward slots
            if (cardSlotPrefab != null && cardContent != null)
            {
                for (int i = 0; i < maxCardSlots; i++)
                {
                    CardRewardSlot slot = Instantiate(cardSlotPrefab, cardContent);
                    slot.gameObject.SetActive(false);
                    _cardSlotPool.Add(slot);
                }
            }
        }

        private void HideAllSlots()
        {
            foreach (RewardSlot slot in _slotPool)
            {
                slot.transform.DOKill();
                slot.gameObject.SetActive(false);
                slot.transform.localScale = Vector3.one;
            }

            foreach (CardRewardSlot slot in _cardSlotPool)
            {
                slot.transform.DOKill();
                slot.gameObject.SetActive(false);
                slot.transform.localScale = Vector3.one;
            }
        }

        #region Public

        public void Show(List<RewardData> rewards, Action callback)
        {
            KillTweens();

            gameObject.SetActive(true);

            _isClosing = false;
            onClaim = callback;

            background.alpha = 0f;

            panel.localScale = Vector3.one * .6f;
            panel.anchoredPosition = new Vector2(0, -80);

            claimButton.transform.localScale = Vector3.zero;
            claimButton.interactable = true;

            HideAllSlots();

            // Show standard reward UI
            if (content != null) content.gameObject.SetActive(true);
            if (cardContainer != null) cardContainer.gameObject.SetActive(false);

            // Set title based on reward result
            if (resultTitle != null) 
                resultTitle.text = "You got this rewards!";

            List<RewardSlot> activeSlots = new();

            int count = Mathf.Min(rewards.Count, maxSlots);

            for (int i = 0; i < count; i++)
            {
                RewardSlot slot = _slotPool[i];

                slot.gameObject.SetActive(true);
                slot.Setup(rewards[i]);

                slot.transform.localScale = Vector3.zero;

                activeSlots.Add(slot);
            }

            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimClicked);

            PlayShowAnimation(activeSlots);
        }

        /// <summary>
        /// Show card roll results with special animations for duplicates, upgrades, and pity cards
        /// </summary>
        public void ShowCardRollResult(CardRollResult result, List<RewardData> rewards, Action callback)
        {
            KillTweens();

            gameObject.SetActive(true);

            _isClosing = false;
            onClaim = callback;

            background.alpha = 0f;

            panel.localScale = Vector3.one * .6f;
            panel.anchoredPosition = new Vector2(0, -80);

            claimButton.transform.localScale = Vector3.zero;
            claimButton.interactable = true;

            HideAllSlots();

            // Show card-specific UI
            if (content != null) content.gameObject.SetActive(false);
            if (cardContainer != null) cardContainer.gameObject.SetActive(true);

            // Set title based on roll result
            if (resultTitle != null)
            {
                if (result.IsLucky)
                    resultTitle.text = "LUCKY ROLL!";
                else if (result.HasNewCard)
                    resultTitle.text = "NEW CARD ACQUIRED!";
                else
                    resultTitle.text = "ROLL RESULT";
            }

            List<CardRewardSlot> activeCardSlots = new();

            int count = Mathf.Min(result.Cards.Count, maxCardSlots);

            for (int i = 0; i < count; i++)
            {
                var cardReward = result.Cards[i];
                CardRewardSlot slot = _cardSlotPool[i];

                slot.gameObject.SetActive(true);
                slot.Setup(cardReward, result);

                slot.transform.localScale = Vector3.zero;

                activeCardSlots.Add(slot);
            }

            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimClicked);

            PlayShowCardAnimation(activeCardSlots);
        }

        #endregion

        #region Animation

        private void PlayShowAnimation(List<RewardSlot> slots)
        {
            _showSequence = DOTween.Sequence();

            _showSequence.Append(
                background.DOFade(1f, .2f));

            _showSequence.Join(
                panel.DOAnchorPosY(0, .35f)
                    .SetEase(Ease.OutBack));

            _showSequence.Join(
                panel.DOScale(1, .35f)
                    .SetEase(Ease.OutBack));

            foreach (RewardSlot slot in slots)
            {
                _showSequence.Append(
                    slot.transform
                        .DOScale(1f, .18f)
                        .SetEase(Ease.OutBack));
            }

            _showSequence.Append(
                claimButton.transform
                    .DOScale(1f, .25f)
                    .SetEase(Ease.OutBack));

            _showSequence.Append(
                claimButton.transform
                    .DOPunchScale(
                        Vector3.one * .15f,
                        .25f,
                        8,
                        .5f));
        }

        private void PlayShowCardAnimation(List<CardRewardSlot> slots)
        {
            _showSequence = DOTween.Sequence();

            _showSequence.Append(
                background.DOFade(1f, .2f));

            _showSequence.Join(
                panel.DOAnchorPosY(0, .35f)
                    .SetEase(Ease.OutBack));

            _showSequence.Join(
                panel.DOScale(1, .35f)
                    .SetEase(Ease.OutBack));

            // Staggered card appearance with special effects
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                _showSequence.Append(
                    slot.transform
                        .DOScale(1f, .18f)
                        .SetEase(Ease.OutBack));

                // Small delay between cards
                if (i < slots.Count - 1)
                {
                    _showSequence.AppendInterval(.05f);
                }
            }

            _showSequence.Append(
                claimButton.transform
                    .DOScale(1f, .25f)
                    .SetEase(Ease.OutBack));

            _showSequence.Append(
                claimButton.transform
                    .DOPunchScale(
                        Vector3.one * .15f,
                        .25f,
                        8,
                        .5f));
        }

        public void PlayHideAnimation(Action callback)
        {
            KillTweens();

            claimButton.interactable = false;
            cardContent.anchoredPosition = new Vector2(cardContent.anchoredPosition.x, 0f);

            _hideSequence = DOTween.Sequence();

            _hideSequence.Append(
                claimButton.transform
                    .DOScale(0.9f, 0.08f));

            _hideSequence.Append(
                panel.DOScale(0.7f, 0.22f)
                    .SetEase(Ease.InBack));

            _hideSequence.Join(
                panel.DOAnchorPosY(-80f, 0.22f));

            _hideSequence.Join(
                background.DOFade(0f, 0.22f));

            _hideSequence.OnComplete(() =>
            {
                HideAllSlots();
                gameObject.SetActive(false);
                callback?.Invoke();
            });
        }

        private void KillTweens()
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();

            _showSequence = null;
            _hideSequence = null;

            panel.DOKill();
            background.DOKill();
            claimButton.transform.DOKill();

            foreach (RewardSlot slot in _slotPool)
                slot.transform.DOKill();

            foreach (CardRewardSlot slot in _cardSlotPool)
                slot.transform.DOKill();
        }

        #endregion

        #region Events

        private void OnClaimClicked()
        {
            if (_isClosing) return;
            _isClosing = true;

            PlayHideAnimation(() =>
            {
                onClaim?.Invoke();
                onClaim = null;
            });
        }

        #endregion

        private void OnDisable() => KillTweens();

        private void OnDestroy() => KillTweens();
    }
}