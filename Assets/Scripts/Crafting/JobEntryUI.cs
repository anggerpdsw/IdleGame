using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IdleDefenseSurvival.Crafting
{
    public class JobEntryUI : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private Button _claimButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        public string JobId { get; private set; }
        public System.Action<string> OnClaimClicked { get; set; }

        public void Initialize(string jobId, Sprite icon, string name, float progress, CraftJobStatus status, System.Action<string> onClaim)
        {
            JobId = jobId;
            _iconImage.sprite = icon;
            _nameText.text = name;
            _progressSlider.value = progress;
            SetStatus(status);
            OnClaimClicked = onClaim;
            if (_claimButton != null)
                _claimButton.onClick.AddListener(() => onClaim?.Invoke(jobId));
        }

        public void SetIcon(Sprite icon) => _iconImage.sprite = icon;
        public void SetRecipeName(string name) => _nameText.text = name;
        public void SetProgress(float p) => _progressSlider.value = p;

        public void SetStatus(CraftJobStatus status)
        {
            bool isCrafting = status == CraftJobStatus.Crafting;
            bool isReadyToClaim = status == CraftJobStatus.Complete;

            _progressSlider.gameObject.SetActive(isCrafting || !isReadyToClaim);
            _claimButton.gameObject.SetActive(isReadyToClaim);

            if (_statusText != null)
            {
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
        }

        public void SetClaimVisible(bool v) => _claimButton.gameObject.SetActive(v);
    }
}