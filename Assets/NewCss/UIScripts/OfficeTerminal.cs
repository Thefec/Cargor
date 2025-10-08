using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace NewCss.UIScripts
{
    [RequireComponent(typeof(Collider))]
    public class OfficeTerminal : MonoBehaviour
    {
        public GameObject upgradePanel;

        [Tooltip("Animator component for panel animations")]
        public Animator panelAnimator;

        [Tooltip("Close button to close the panel")]
        public Button closeButton;

        [Tooltip("Name of the close animation clip (used to determine length)")]
        public string closeAnimationClipName = "ShopExit";

        [Tooltip("Name of the open animation clip (used to determine length)")]
        public string openAnimationClipName = "ShopOpening";

        private bool isPanelOpen = false;
        private bool isAnimating = false;

        // Her oyuncu için ayrı PlayerMovement referansı tutuyoruz
        private PlayerMovement localPlayerMovement = null;

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

            // Setup close button
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
            else
            {
                Debug.LogWarning("Close button not assigned! Please assign it in the inspector.", this);
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

            // Network kontrolü - sadece local player için aç
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogWarning("Character has no NetworkObject component!");
                return;
            }

            // Sadece kendi karakterimiz ise devam et
            if (!networkObject.IsOwner)
            {
                Debug.Log("-> Not the local player, ignoring");
                return;
            }

            Debug.Log("-> Local player entered range");

            if (upgradePanel == null)
            {
                Debug.LogError("upgradePanel is not assigned!");
                return;
            }

            // Eğer animasyon sırasındaysa veya panel zaten açıksa bekle
            if (isAnimating || isPanelOpen)
            {
                Debug.Log("Animation in progress or panel already open, ignoring trigger enter");
                return;
            }

            // Local player'ın PlayerMovement referansını al
            localPlayerMovement = other.GetComponent<PlayerMovement>();
            if (localPlayerMovement == null)
            {
                Debug.LogWarning("PlayerMovement component not found on character!");
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

            // Network kontrolü
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsOwner)
            {
                return;
            }

            Debug.Log("-> Local player exited range");

            // Artık collider'dan çıkınca kapanmıyor, sadece buton ile kapanacak
        }

        private void OnCloseButtonClicked()
        {
            Debug.Log("Close button clicked");
            ClosePanel();
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

            // Karakterin hareketini kilitle (sadece local player için)
            if (localPlayerMovement != null)
            {
                localPlayerMovement.LockMovement(true);
                Debug.Log("Local player movement locked");
            }

            // Panel'i aktif et
            upgradePanel.SetActive(true);

            // Cursor'u görünür yap ve kilidi aç (UI ile etkileşim için)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

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

            // Karakterin hareketini kilidini aç (sadece local player için)
            if (localPlayerMovement != null)
            {
                localPlayerMovement.LockMovement(false);
                Debug.Log("Local player movement unlocked");
            }

            // Cursor'u tekrar kilitle (oyun modu için)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

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

            // Reference'ı temizle
            localPlayerMovement = null;
        }

        // Geciktirilmiş açılma animasyonu
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

        private void OnDestroy()
        {
            // Listener'ı temizle
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }
        }
    }
}