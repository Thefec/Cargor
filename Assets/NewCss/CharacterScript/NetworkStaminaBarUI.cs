using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace NewCss
{
    public class NetworkStaminaBarUI : NetworkBehaviour
    {
        [Header("UI References")]
        [Tooltip("Stamina fill image (Image Type = Filled)")]
        public Image fillImage;
        
        [Header("Stamina Bar Settings")]
        public float updateSpeed = 30f;
        
        [Header("Player Reference")]
        [Tooltip("Reference to PlayerMovement script")]
        public PlayerMovement playerMovement;
        
        // Network variables for stamina synchronization
        private NetworkVariable<float> networkCurrentStamina = new NetworkVariable<float>();
        private NetworkVariable<float> networkMaxStamina = new NetworkVariable<float>();
        private NetworkVariable<float> networkCooldownTime = new NetworkVariable<float>();
        private NetworkVariable<float> networkMaxCooldown = new NetworkVariable<float>();
        private NetworkVariable<bool> networkIsInCooldown = new NetworkVariable<bool>();
        
        // Local UI values for smooth animation
        private float displayStamina;
        private float targetStamina;
        
        void Start()
        {
            if (fillImage == null)
            {
                Debug.LogError("Fill Image is not assigned!");
            }
            
            // Her oyuncu kendi PlayerMovement'ını bulur
            if (playerMovement == null)
            {
                playerMovement = GetComponent<PlayerMovement>();
                if (playerMovement == null)
                {
                    // Eğer aynı GameObject'te yoksa, parent'te ara
                    playerMovement = GetComponentInParent<PlayerMovement>();
                }
                
                if (playerMovement == null)
                {
                    Debug.LogError("PlayerMovement script not found!");
                }
            }
            
            // Initialize display values
            displayStamina = 1f;
            targetStamina = 1f;
        }
        
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            
            // Sadece owner kendi stamina değerlerini güncelleyebilir
            if (IsOwner)
            {
                // Initial values set up
                if (playerMovement != null)
                {
                    UpdateStaminaValues();
                }
            }
            
            // Subscribe to network variable changes for all clients
            networkCurrentStamina.OnValueChanged += OnStaminaChanged;
            networkMaxStamina.OnValueChanged += OnMaxStaminaChanged;
            networkCooldownTime.OnValueChanged += OnCooldownChanged;
            networkMaxCooldown.OnValueChanged += OnMaxCooldownChanged;
            networkIsInCooldown.OnValueChanged += OnCooldownStateChanged;
        }
        
        public override void OnNetworkDespawn()
        {
            // Unsubscribe from network variable changes
            networkCurrentStamina.OnValueChanged -= OnStaminaChanged;
            networkMaxStamina.OnValueChanged -= OnMaxStaminaChanged;
            networkCooldownTime.OnValueChanged -= OnCooldownChanged;
            networkMaxCooldown.OnValueChanged -= OnMaxCooldownChanged;
            networkIsInCooldown.OnValueChanged -= OnCooldownStateChanged;
            
            base.OnNetworkDespawn();
        }
        
        void Update()
        {
            if (fillImage == null) return;
            
            // Sadece owner kendi değerlerini güncelleyebilir
            if (IsOwner && playerMovement != null)
            {
                UpdateStaminaValues();
            }
            
            // Tüm clientler UI animasyonunu günceller
            UpdateUI();
        }
        
        private void UpdateStaminaValues()
        {
            // Network variables'ı güncelle (sadece owner)
            networkCurrentStamina.Value = playerMovement.CurrentStamina;
            networkMaxStamina.Value = playerMovement.MaxStamina;
            networkCooldownTime.Value = playerMovement.CooldownTime;
            networkMaxCooldown.Value = playerMovement.MaxCooldown;
            networkIsInCooldown.Value = playerMovement.IsInCooldown;
        }
        
        private void UpdateUI()
        {
            // Target stamina amount hesapla
            float amount;
            
            if (networkIsInCooldown.Value)
            {
                // Cooldown sırasında, cooldown time'ına göre tersine fill
                if (networkMaxCooldown.Value > 0)
                {
                    amount = 1f - (networkCooldownTime.Value / networkMaxCooldown.Value);
                }
                else
                {
                    amount = 1f;
                }
            }
            else
            {
                // Normal stamina logic
                if (networkMaxStamina.Value > 0)
                {
                    amount = networkCurrentStamina.Value / networkMaxStamina.Value;
                }
                else
                {
                    amount = 1f;
                }
            }
            
            targetStamina = Mathf.Clamp01(amount);
            
            // Smooth animation towards target
            if (Mathf.Abs(displayStamina - targetStamina) > 0.01f)
            {
                displayStamina = Mathf.MoveTowards(displayStamina, targetStamina, updateSpeed * Time.deltaTime);
            }
            else
            {
                displayStamina = targetStamina;
            }
            
            // Update UI
            fillImage.fillAmount = displayStamina;
        }
        
        // Network variable change handlers
        private void OnStaminaChanged(float previousValue, float newValue)
        {
            // Stamina değiştiğinde UI güncellenir
        }
        
        private void OnMaxStaminaChanged(float previousValue, float newValue)
        {
            // Max stamina değiştiğinde UI güncellenir
        }
        
        private void OnCooldownChanged(float previousValue, float newValue)
        {
            // Cooldown time değiştiğinde UI güncellenir
        }
        
        private void OnMaxCooldownChanged(float previousValue, float newValue)
        {
            // Max cooldown değiştiğinde UI güncellenir
        }
        
        private void OnCooldownStateChanged(bool previousValue, bool newValue)
        {
            // Cooldown state değiştiğinde UI güncellenir
        }
        
        // Public method to manually set player reference (optional)
        public void SetPlayerMovement(PlayerMovement player)
        {
            playerMovement = player;
        }
        
        // Debug method to check network values
        [System.Obsolete("Debug use only")]
        public void DebugPrintValues()
        {
            Debug.Log($"Current Stamina: {networkCurrentStamina.Value}, Max: {networkMaxStamina.Value}, Cooldown: {networkIsInCooldown.Value}");
        }
    }
}