using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace NewCss.UIScripts
{
    [RequireComponent(typeof(Collider))]
    public class NetworkCharacterCusUI : MonoBehaviour
    {
        [Tooltip("UI Panel that will open and close")]
        public GameObject uiPanel;

        [Tooltip("Button that will appear when character enters trigger")]
        public GameObject interactionButton;

        [Tooltip("Close button inside the UI panel")]
        public Button closeButton;

        [Tooltip("Character rotation speed toward camera (0 = instant)")]
        public float rotationSpeed = 0f;

        [SerializeField] private int CurrentTime = 14;

        // NEW: Animator for the UI panel
        [Tooltip("Animator on the uiPanel (must have Open and Close triggers)")]
        public Animator uiAnimator;

        // Name of the close animation clip (used to determine length if no animation event used)
        public string closeAnimationClipName = "SlideOut";
        public string openAnimationClipName = "SlideIn";

        private NetworkObject currentCharacterNetwork;
        private Transform currentCharacter;
        private Button buttonComponent;
        private PlayerMovement currentPlayerMovement;
        private bool isPanelOpen = false;
        private bool isAnimating = false;

        // YENI: Referans NetworkCharacterMeshSwapper için
        private NetworkCharacterMeshSwapper characterMeshSwapper;

        private void Awake()
        {
            // IMPORTANT: if uiPanel is null -> warn
            if (uiPanel == null)
            {
                Debug.LogWarning("uiPanel not assigned!", this);
            }
            else
            {
                // Panel'i başlangıçta gizle
                uiPanel.SetActive(false);
            }

            if (interactionButton != null)
            {
                interactionButton.SetActive(false);
                buttonComponent = interactionButton.GetComponent<Button>();

                if (buttonComponent != null)
                {
                    buttonComponent.onClick.AddListener(OnInteractionButtonClicked);
                }
                else
                {
                    Debug.LogWarning("Interaction button doesn't have a Button component!", this);
                }
            }

            // Setup close button
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
            else
            {
                Debug.LogWarning("Close button not assigned! Panel won't be closable.", this);
            }

            // If animator not assigned, try to get it from uiPanel
            if (uiAnimator == null && uiPanel != null)
            {
                uiAnimator = uiPanel.GetComponent<Animator>();
                if (uiAnimator == null)
                    Debug.LogWarning("uiAnimator not assigned and uiPanel has no Animator!", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null) return;

            if (!networkObject.IsOwner) return;

            if (!other.CompareTag("Character")) return;

            currentCharacterNetwork = networkObject;
            currentCharacter = other.transform;

            // YENI: NetworkCharacterMeshSwapper referansını al
            characterMeshSwapper = other.GetComponent<NetworkCharacterMeshSwapper>();
            if (characterMeshSwapper == null)
            {
                characterMeshSwapper = other.GetComponentInChildren<NetworkCharacterMeshSwapper>();
            }

            // Cache PlayerMovement component
            currentPlayerMovement = other.GetComponent<PlayerMovement>();
            if (currentPlayerMovement == null)
            {
                currentPlayerMovement = other.GetComponentInChildren<PlayerMovement>();
            }
            if (currentPlayerMovement == null)
            {
                currentPlayerMovement = other.GetComponentInParent<PlayerMovement>();
            }

            int currentHour = DayCycleManager.Instance.CurrentHour;
            Debug.Log($"Current Hour: {currentHour}, Required Time: {CurrentTime}");

            if (currentHour <= CurrentTime)
            {
                if (interactionButton != null && !isPanelOpen)
                    interactionButton.SetActive(true);
                else
                    Debug.LogWarning("interactionButton not assigned!", this);
            }
            else
            {
                Debug.Log($"Button not shown - Current hour ({currentHour}) is greater than required time ({CurrentTime})");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            NetworkObject networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null || networkObject != currentCharacterNetwork) return;

            if (!networkObject.IsOwner) return;

            if (!other.CompareTag("Character")) return;

            // If panel is open, close it first and unlock movement
            if (isPanelOpen)
            {
                CloseUI();
            }

            currentCharacterNetwork = null;
            currentCharacter = null;
            currentPlayerMovement = null;
            
            // YENI: MeshSwapper referansını temizle
            characterMeshSwapper = null;

            if (interactionButton != null)
                interactionButton.SetActive(false);
        }

        private void OnInteractionButtonClicked()
        {
            if (currentCharacterNetwork == null || !currentCharacterNetwork.IsOwner)
            {
                Debug.LogWarning("Cannot interact - no valid local player character");
                return;
            }

            // Animasyon sırasında tıklamaları engelle
            if (isAnimating)
            {
                Debug.Log("Animation in progress, ignoring interaction");
                return;
            }

            // Panel zaten açıksa tekrar açmaya çalışma
            if (isPanelOpen)
            {
                Debug.Log("Panel already open, ignoring interaction");
                return;
            }

            if (currentCharacter != null)
            {
                RotateCharacterToCamera(currentCharacter);
            }

            // Lock player movement
            LockPlayerMovement(true);
            isPanelOpen = true;
            isAnimating = true;

            if (uiPanel != null)
            {
                // DÜZELTME: Panel'i aktif etmeden önce animator state'ini sıfırla
                if (uiAnimator != null)
                {
                    // Animator'ı tamamen resetle
                    uiAnimator.Rebind();
                    uiAnimator.Update(0f);
                    
                    // Tüm trigger'ları temizle
                    uiAnimator.ResetTrigger("Open");
                    uiAnimator.ResetTrigger("Close");
                }

                // Panel'i aktif et
                uiPanel.SetActive(true);

                // Kısa bir gecikme sonrası animasyonu başlat
                StartCoroutine(DelayedOpenAnimation());

                if (interactionButton != null)
                    interactionButton.SetActive(false);

                var customizationUI = uiPanel.GetComponent<NetworkCharacterCustomizationUI>();
                if (customizationUI != null)
                {
                    customizationUI.OnCharacterSpawned(currentCharacterNetwork.gameObject);
                }
            }
            else
            {
                Debug.LogWarning("uiPanel not assigned!", this);
                isAnimating = false;
            }
        }

        // YENİ: Geciktirilmiş açılma animasyonu
        private IEnumerator DelayedOpenAnimation()
        {
            // Bir frame bekle ki panel düzgün aktif olsun
            yield return null;
            
            // Play Open animation via Animator trigger if available
            if (uiAnimator != null)
            {
                uiAnimator.SetTrigger("Open");
                
                // Açılma animasyonu bitene kadar bekle
                yield return StartCoroutine(WaitForOpenAnimation());
            }
            else
            {
                // Fallback: if no animator, just show it immediately
                isAnimating = false;
            }
        }

        // NEW: Handle close button click
        private void OnCloseButtonClicked()
        {
            // Animasyon sırasında kapatma işlemini engelle
            if (isAnimating)
            {
                Debug.Log("Animation in progress, ignoring close request");
                return;
            }

            CloseUI();
        }

        // PUBLIC: call this to close the UI (animasyonlu)
        public void CloseUI()
        {
            if (uiPanel == null || !isPanelOpen)
            {
                Debug.Log("Panel not open or null, cannot close");
                return;
            }

            // Animasyon sırasında kapatmaya çalışırsa engelle
            if (isAnimating)
            {
                Debug.Log("Animation in progress, ignoring close request");
                return;
            }

            // YENI: UI kapatılmadan önce karakter kustomizasyonunu kaydet
            SaveCharacterCustomization();

            isPanelOpen = false;
            isAnimating = true;

            if (uiAnimator != null)
            {
                // trigger close anim; coroutine will disable the panel at the end
                uiAnimator.ResetTrigger("Open");
                uiAnimator.SetTrigger("Close");
                StartCoroutine(PlayCloseAndUnlockMovement());
            }
            else
            {
                uiPanel.SetActive(false);
                // Unlock movement immediately if no animation
                LockPlayerMovement(false);
                isAnimating = false;
            }

            int currentHour = DayCycleManager.Instance.CurrentHour;
            if (currentCharacterNetwork != null && currentCharacterNetwork.IsOwner &&
                interactionButton != null && currentHour <= CurrentTime)
            {
                interactionButton.SetActive(true);
            }
        }

        // YENI: Karakter kustomizasyonunu kaydet
        private void SaveCharacterCustomization()
        {
            if (characterMeshSwapper != null && currentCharacterNetwork != null && currentCharacterNetwork.IsOwner)
            {
                characterMeshSwapper.SaveCustomizationData();
                Debug.Log("Character customization saved via UI close!");
            }
            else if (characterMeshSwapper == null)
            {
                Debug.LogWarning("NetworkCharacterMeshSwapper not found - cannot save customization!");
            }
        }

        // YENI: Manuel kaydetme butonu için public metod
        public void SaveCustomizationManually()
        {
            SaveCharacterCustomization();
        }

        // YENI: Customization sıfırlama butonu için
        public void ResetCustomization()
        {
            if (characterMeshSwapper != null && currentCharacterNetwork != null && currentCharacterNetwork.IsOwner)
            {
                characterMeshSwapper.ResetToDefaults();
                Debug.Log("Character customization reset to defaults!");
            }
        }

        public void CheckTimeCondition()
        {
            int currentHour = DayCycleManager.Instance.CurrentHour;

            if (currentCharacterNetwork != null && currentCharacterNetwork.IsOwner && interactionButton != null)
            {
                bool shouldShowButton = currentHour <= CurrentTime;

                if (uiPanel != null && !isPanelOpen)
                {
                    interactionButton.SetActive(shouldShowButton);
                }
            }
        }

        // NEW: Method to lock/unlock player movement
        private void LockPlayerMovement(bool lockMovement)
        {
            if (currentPlayerMovement != null)
            {
                if (lockMovement)
                {
                    currentPlayerMovement.LockMovement(true);
                    currentPlayerMovement.LockAllInteractions(true);
                    Debug.Log("Player movement locked for UI interaction");
                }
                else
                {
                    currentPlayerMovement.LockMovement(false);
                    currentPlayerMovement.LockAllInteractions(false);
                    Debug.Log("Player movement unlocked");
                }
            }
            else
            {
                Debug.LogWarning("PlayerMovement component not found!");
            }
        }

        // DÜZELTME: Wait for open animation to complete
        private IEnumerator WaitForOpenAnimation()
        {
            float waitTime = 0.5f; // default fallback

            if (uiAnimator != null)
            {
                // Mevcut state bilgisini al
                AnimatorStateInfo stateInfo = uiAnimator.GetCurrentAnimatorStateInfo(0);
                
                // Animation clip uzunluğunu bul
                var ac = uiAnimator.runtimeAnimatorController;
                if (ac != null)
                {
                    foreach (var clip in ac.animationClips)
                    {
                        if (clip.name.Contains("Open") || clip.name.Contains("SlideIn") || 
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
        }

        // NEW: Updated coroutine that unlocks movement after close animation
        private IEnumerator PlayCloseAndUnlockMovement()
        {
            float waitTime = 0.25f; // default fallback

            if (uiAnimator != null)
            {
                // Try to find animation clip length by name
                var ac = uiAnimator.runtimeAnimatorController;
                if (ac != null)
                {
                    foreach (var clip in ac.animationClips)
                    {
                        if (clip.name == closeAnimationClipName)
                        {
                            waitTime = clip.length;
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(waitTime);

            // After close animation, disable panel and unlock movement
            if (uiPanel != null)
                uiPanel.SetActive(false);

            // Unlock player movement after animation completes
            LockPlayerMovement(false);
            
            // Kapatma animasyonu bitti
            isAnimating = false;
        }

        private void RotateCharacterToCamera(Transform character)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("Main Camera not found!");
                return;
            }

            Vector3 direction = cam.transform.position - character.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            if (rotationSpeed <= 0f)
            {
                character.rotation = targetRotation;
            }
            else
            {
                character.rotation = Quaternion.Slerp(character.rotation, targetRotation,
                    rotationSpeed * Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (buttonComponent != null)
                buttonComponent.onClick.RemoveListener(OnInteractionButtonClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);

            // YENI: Destroy edilmeden önce kaydet
            if (isPanelOpen)
            {
                SaveCharacterCustomization();
                LockPlayerMovement(false);
            }
            
            // Reset animation state
            isAnimating = false;
        }

        // NEW: Public method to force close and unlock (for external scripts)
        public void ForceCloseAndUnlock()
        {
            if (isPanelOpen)
            {
                // YENI: Force close'da da kaydet
                SaveCharacterCustomization();
                
                isPanelOpen = false;
                isAnimating = false;
                LockPlayerMovement(false);
                
                if (uiPanel != null)
                    uiPanel.SetActive(false);

                int currentHour = DayCycleManager.Instance.CurrentHour;
                if (currentCharacterNetwork != null && currentCharacterNetwork.IsOwner &&
                    interactionButton != null && currentHour <= CurrentTime)
                {
                    interactionButton.SetActive(true);
                }
            }
        }

        // YENİ: Animation Event için callback (opsiyonel)
        public void OnOpenAnimationComplete()
        {
            isAnimating = false;
            Debug.Log("Open animation completed via Animation Event");
        }

        public void OnCloseAnimationComplete()
        {
            if (uiPanel != null)
                uiPanel.SetActive(false);

            LockPlayerMovement(false);
            isAnimating = false;
            Debug.Log("Close animation completed via Animation Event");
        }
    }
}