using UnityEngine;
using UnityEngine.UI;

namespace NewCss
{
    public class PhoneWaitBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject barContainer;

        // Salt gorsel geri-sayim (her client yerel calisir). ringDuration her yerde
        // ayni sabit + calma _isRinging ile ~eszamanli yayildigi icin barlar senkron;
        // server yine otorite (odul/durma), bu yalnizca gorsel dolgu animasyonu.
        private bool _counting = false;
        private float _countStartTime;
        private float _countDuration;

        private void Awake()
        {
            if (barContainer == null)
            {
                // Try to find a child if not assigned
                if (transform.childCount > 0)
                    barContainer = transform.GetChild(0).gameObject;
            }

            if (fillImage == null && barContainer != null)
            {
                fillImage = barContainer.GetComponentInChildren<Image>();
            }

            HideBar();
        }

        private void Update()
        {
            if (!_counting) return;

            float elapsed = Time.time - _countStartTime;
            float fill = Mathf.Clamp01(1f - elapsed / _countDuration);

            if (fillImage != null)
            {
                fillImage.fillAmount = fill;
            }

            // Bosaldi: yerel animasyon durur (bari server _isRinging=false ile gizler).
            if (fill <= 0f)
            {
                _counting = false;
            }
        }

        /// <summary>
        /// Bari gorunur yapip dolguyu duration boyunca 1 -> 0 azaltir (gorsel geri-sayim).
        /// </summary>
        public void StartCountdown(float duration)
        {
            if (barContainer != null && !barContainer.activeSelf)
            {
                barContainer.SetActive(true);
            }

            _countDuration = Mathf.Max(0.01f, duration);
            _countStartTime = Time.time;
            _counting = true;

            if (fillImage != null)
            {
                fillImage.fillAmount = 1f;
            }
        }

        public void SetFillAmount(float amount)
        {
            if (barContainer != null && !barContainer.activeSelf)
            {
                barContainer.SetActive(true);
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(amount);
            }
        }

        public void HideBar()
        {
            _counting = false;

            if (barContainer != null)
            {
                barContainer.SetActive(false);
            }
        }

        // Backward compatibility
        public void StartWaitBar(float duration) { }
        public float GetRemainingTime() { return 0f; }
    }
}
