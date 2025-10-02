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