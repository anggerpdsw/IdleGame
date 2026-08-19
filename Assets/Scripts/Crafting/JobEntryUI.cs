using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace IdleDefenseSurvival.Crafting
{
    public class JobEntryUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private Button _claimButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        public string JobId { get; private set; }
        private Action<string> _onClaim;

        public void Initialize(string jobId, Sprite icon, string name, float progress, CraftJobStatus status, Action<string> onClaim)
        {
            JobId = jobId;
            _onClaim = onClaim;
            SetIcon(icon);
            SetRecipeName(name);
            SetProgress(progress);
            SetStatus(status);
            BindClaimButton();
        }

        private void BindClaimButton()
        {
            if (_claimButton == null)
            {
                Debug.LogError($"[JobEntryUI] Claim Button is NULL for JobId={JobId}", this);
                return;
            }
            _claimButton.onClick.RemoveListener(HandleClaimClicked);
            _claimButton.onClick.AddListener(HandleClaimClicked);
        }

        private void HandleClaimClicked()
        {
            Debug.Log($"[JobEntryUI] CLAIM CLICKED | JobId={JobId}", this);
            if (string.IsNullOrEmpty(JobId))
            {
                Debug.LogError("[JobEntryUI] Cannot claim because JobId is empty.", this);
                return;
            }
            if (_onClaim == null)
            {
                Debug.LogError($"[JobEntryUI] Claim callback is NULL | JobId={JobId}", this);
                return;
            }
            // Disable immediately to prevent double click.
            _claimButton.interactable = false;
            _onClaim.Invoke(JobId);
        }

        public void SetIcon(Sprite icon) => _iconImage.sprite = icon;
        public void SetRecipeName(string name) => _nameText.text = name;
        public void SetProgress(float p) => _progressSlider.value = p;

        public void SetStatus(CraftJobStatus status)
        {
            bool isReadyToClaim = status == CraftJobStatus.Complete;
            bool isCrafting = status == CraftJobStatus.Crafting;

            _progressSlider.gameObject.SetActive(isCrafting || !isReadyToClaim);
            _claimButton.gameObject.SetActive(isReadyToClaim);
            _claimButton.interactable = isReadyToClaim;

            if (_statusText != null)
                _statusText.text = status switch
                {
                    CraftJobStatus.Queued => "Queued",
                    CraftJobStatus.Crafting => "Crafting...",
                    CraftJobStatus.Complete => "Ready to Claim",
                    CraftJobStatus.Cancelled => "Cancelled",
                    CraftJobStatus.Failed => "Failed",
                    _ => status.ToString()
                };
        }

        public void SetClaimVisible(bool visible)
        {
            if (_claimButton == null) return;
            _claimButton.gameObject.SetActive(visible);
            if (visible) _claimButton.interactable = true;
        }
        
        private void OnDestroy()
        {
            if (_claimButton != null)
                _claimButton.onClick.RemoveListener(HandleClaimClicked);
        }
    }
}