using UnityEngine;
using UnityEngine.UI;

namespace NewCss
{
    public class StaminaBarUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Stamina fill image (Image Type = Filled)")]
        public Image fillImage;

        [Header("Player")]
        [Tooltip("Reference to PlayerMovement script")]
        public PlayerMovement playerMovement;

        void Start()
        {
            if (fillImage == null)
            {
                Debug.LogError("Fill Image is not assigned!");
            }

            if (playerMovement == null)
            {
                playerMovement = Object.FindObjectOfType<PlayerMovement>();
                if (playerMovement == null)
                    Debug.LogError("PlayerMovement script not found!");
            }

            // Owner-gating yoktu -> her spawn edilen karakterin kendi tam-ekran
            // Screen-Space-Overlay Canvas'ı non-owner client'larda da aktif kalıyordu
            // (host stamina bar'ının azalmadığı gözlemlendi). Aynı desen
            // NetworkStaminaBarUI.cs:68-80'de zaten uygulanmış; sahibi olmayan bir
            // instance'ın stamina bar'ı hiç görünmemeli.
            if (playerMovement != null && !playerMovement.IsOwner)
            {
                var canvas = GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = GetComponentInChildren<Canvas>(true);
                }
                if (canvas == null)
                {
                    // Gerçek prefab hiyerarşisinde (Character.prefab) Screen-Space-Overlay
                    // Canvas, StaminaBar/BarFrame üzerinde değil PARENT'te duruyor.
                    canvas = GetComponentInParent<Canvas>(true);
                }

                if (canvas != null)
                {
                    canvas.enabled = false;
                }
            }
        }

        void Update()
        {
            if (fillImage == null || playerMovement == null)
                return;

            float amount;

            if (playerMovement.IsInCooldown)
            {
                // During cooldown, fill increases in reverse logic
                amount = 1f - (playerMovement.CooldownTime / playerMovement.MaxCooldown);
            }
            else
            {
                // Normal stamina logic
                amount = playerMovement.CurrentStamina / playerMovement.MaxStamina;
            }

            // Clamp value to keep it between 0 and 1
            fillImage.fillAmount = Mathf.Clamp01(amount);
        }
    }
}