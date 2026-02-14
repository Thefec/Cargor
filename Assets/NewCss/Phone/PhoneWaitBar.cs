using UnityEngine;
using UnityEngine.UI;

namespace NewCss
{
    public class PhoneWaitBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private GameObject barContainer;

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
