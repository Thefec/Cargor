using System.Collections;
using UnityEngine;

namespace NewCss.UIScripts
{
    [RequireComponent(typeof(Collider))]
    public class OfficeTerminal : MonoBehaviour
    {
        public GameObject upgradePanel;
        
        [Tooltip("Animator component for panel animations")]
        public Animator panelAnimator;
        
        [Tooltip("Name of the close animation clip (used to determine length)")]
        public string closeAnimationClipName = "ShopExit";
        
        [Tooltip("Name of the open animation clip (used to determine length)")]
        public string openAnimationClipName = "ShopOpening";
        
        private bool playerInRange = false;
        private bool isPanelOpen = false;
        private bool isAnimating = false;

        private void Awake()
        {
            // Make the collider a trigger
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            if (upgradePanel != null)
                upgradePanel.SetActive(false);
                
            // Auto-assign animator if not set
            if (panelAnimator == null && upgradePanel != null)
            {
                panelAnimator = upgradePanel.GetComponent<Animator>();
                if (panelAnimator == null)
                    Debug.LogWarning("panelAnimator not assigned and upgradePanel has no Animator!", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"TriggerEnter: {other.name}, tag={other.tag}");
            if (!other.CompareTag("Character"))
            {
                Debug.Log("-> Tag mismatch");
                return;
            }

            playerInRange = true;
            Debug.Log("-> Character entered range");

            if (upgradePanel == null)
            {
                Debug.LogError("upgradePanel is not assigned!");
                return;
            }

            // Eğer animasyon sırasındaysa bekle
            if (isAnimating)
            {
                Debug.Log("Animation in progress, ignoring trigger enter");
                return;
            }

            int hour = DayCycleManager.Instance.CurrentHour;
            Debug.Log($"CurrentHour = {hour}");

            if (hour >= 10)
            {
                Debug.Log("-> Hour is 10 or above, opening panel");
                OpenPanel();
            }
            else
            {
                Debug.Log("-> Hour is less than 10, panel not opening");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Character")) return;

            playerInRange = false;
            Debug.Log("-> Character exited range");

            // Eğer panel açıksa animasyonlu kapat
            if (isPanelOpen)
            {
                ClosePanel();
            }
        }
        
        private void OpenPanel()
        {
            if (isPanelOpen || isAnimating) 
            {
                Debug.Log("Panel already open or animating, ignoring open request");
                return;
            }
            
            isPanelOpen = true;
            isAnimating = true;
            
            // Panel'i aktif et
            upgradePanel.SetActive(true);
            
            if (panelAnimator != null)
            {
                // Animator state'ini temizle
                panelAnimator.ResetTrigger("Open");
                panelAnimator.ResetTrigger("Close");
                
                // Açılma animasyonunu başlat
                StartCoroutine(DelayedOpenAnimation());
            }
            else
            {
                // Animator yoksa direkt aç
                isAnimating = false;
                Debug.Log("Panel opened without animation");
            }
        }
        
        private void ClosePanel()
        {
            if (!isPanelOpen || isAnimating) 
            {
                Debug.Log("Panel not open or animating, ignoring close request");
                return;
            }
            
            isPanelOpen = false;
            isAnimating = true;
            
            if (panelAnimator != null)
            {
                // Kapatma animasyonunu başlat
                panelAnimator.ResetTrigger("Open");
                panelAnimator.SetTrigger("Close");
                
                // Animasyon bitene kadar bekle, sonra panel'i deaktif et
                StartCoroutine(DisablePanelAfterAnimation());
            }
            else
            {
                // Animator yoksa direkt kapat
                upgradePanel.SetActive(false);
                isAnimating = false;
                Debug.Log("Panel closed without animation");
            }
        }
        
        // Geciktirilmiş açılma animasyonu (NetworkCharacterCusUI'dan alındı)
        private IEnumerator DelayedOpenAnimation()
        {
            // Bir frame bekle ki panel düzgün aktif olsun
            yield return null;
            
            if (panelAnimator != null)
            {
                panelAnimator.SetTrigger("Open");
                
                // Açılma animasyonu bitene kadar bekle
                yield return StartCoroutine(WaitForOpenAnimation());
            }
            else
            {
                isAnimating = false;
            }
        }
        
        // Açılma animasyonunun bitmesini bekle
        private IEnumerator WaitForOpenAnimation()
        {
            float waitTime = 0.5f; // default fallback

            if (panelAnimator != null)
            {
                // Animation clip uzunluğunu bul
                var ac = panelAnimator.runtimeAnimatorController;
                if (ac != null)
                {
                    foreach (var clip in ac.animationClips)
                    {
                        if (clip.name.Contains("Open") || clip.name.Contains("Opening") || 
                            clip.name == openAnimationClipName)
                        {
                            waitTime = clip.length;
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(waitTime);

            // Açılma animasyonu bitti
            isAnimating = false;
            Debug.Log("Open animation completed");
        }
        
        // Kapatma animasyonu bitince panel'i deaktif et
        private IEnumerator DisablePanelAfterAnimation()
        {
            float waitTime = 0.3f; // default fallback

            if (panelAnimator != null)
            {
                // Kapatma animasyon clip uzunluğunu bul
                var ac = panelAnimator.runtimeAnimatorController;
                if (ac != null)
                {
                    foreach (var clip in ac.animationClips)
                    {
                        if (clip.name.Contains("Exit") || clip.name.Contains("Close") || 
                            clip.name == closeAnimationClipName)
                        {
                            waitTime = clip.length;
                            break;
                        }
                    }
                }
            }

            Debug.Log($"Waiting {waitTime} seconds for close animation...");
            yield return new WaitForSeconds(waitTime);
            
            // Animasyon bittikten sonra panel'i deaktif et
            if (upgradePanel != null)
            {
                upgradePanel.SetActive(false);
                Debug.Log("Panel disabled after close animation");
            }
            
            isAnimating = false;
        }
        
        // BONUS: Animation Event callback'leri (opsiyonel)
        public void OnOpenAnimationComplete()
        {
            isAnimating = false;
            Debug.Log("Open animation completed via Animation Event");
        }

        public void OnCloseAnimationComplete()
        {
            if (upgradePanel != null)
                upgradePanel.SetActive(false);

            isAnimating = false;
            Debug.Log("Close animation completed via Animation Event");
        }
    }
}