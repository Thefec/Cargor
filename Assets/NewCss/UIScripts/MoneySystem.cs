using System;
using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    public class MoneySystem : NetworkBehaviour
    {
        public static MoneySystem Instance { get; private set; }

        [Header("Starting Amount")] 
        public int startingMoney = 500;

        // NetworkVariable'ı başlangıç değeri ile initialize et
        private NetworkVariable<int> _currentMoney = new NetworkVariable<int>(
            500, // Default value
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        public int _CurrentMoney => _currentMoney.Value;
        public int CurrentMoney => _currentMoney.Value;

        /// <summary>Invoked when money amount changes.</summary>
        public event Action<int> OnMoneyChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // … singleton atama vb.
            FindObjectOfType<MoneyUI>()?.Initialize(this);
            // Singleton pattern'i burada yap, network spawn olduktan sonra
            if (Instance != null && Instance != this)
            {
                if (IsOwner)
                    NetworkObject.Despawn();
                return;
            }
            Instance = this;

            // Subscribe to network variable changes
            _currentMoney.OnValueChanged += OnNetworkMoneyChanged;
            
            // Initialize starting money on server (sadece değer farklıysa)
            if (IsServer && _currentMoney.Value != startingMoney)
            {
                _currentMoney.Value = startingMoney;
            }

            // İlk event'i tetikle
            OnMoneyChanged?.Invoke(_currentMoney.Value);
            
            // Debug log ekle
            Debug.Log($"MoneySystem Network Spawned - IsServer: {IsServer}, Current Money: {_currentMoney.Value}");
        }

        public override void OnNetworkDespawn()
        {
            if (_currentMoney != null)
            {
                _currentMoney.OnValueChanged -= OnNetworkMoneyChanged;
            }
            
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnNetworkMoneyChanged(int previousValue, int newValue)
        {
            // Trigger the event when network value changes
            OnMoneyChanged?.Invoke(newValue);
            Debug.Log($"Money changed from {previousValue} to {newValue}");
        }

        /// <summary>
        /// Adds (positive) or subtracts (negative) money. SERVER-ONLY (bkz.
        /// ResetMoney/SetMoney). Onceki client-branch ham delta'yi
        /// ModifyMoneyServerRpc (RequireOwnership=false) uzerinden server'a
        /// yolluyordu (G1-b exploit) — kaldirildi. Yedi mesru cagiranin
        /// hepsi zaten server-context'te calisiyor (qa dogruladi).
        /// </summary>
        public void ModifyMoney(int delta)
        {
            if (!IsServer)
            {
                Debug.LogWarning("MoneySystem.ModifyMoney client'tan cagrildi — yok sayildi (server-only).");
                return;
            }
            int newValue = Mathf.Max(0, _currentMoney.Value + delta);
            _currentMoney.Value = newValue;
            Debug.Log($"Server: Money modified by {delta}, new value: {newValue}");
        }

        /// <summary>Readable method to add rewards.</summary>
        public void AddMoney(int amount) => ModifyMoney(amount);

        /// <summary>Readable method for spending/penalties.</summary>
        public void SpendMoney(int amount) => ModifyMoney(-amount);

        /// <summary>Check if player has enough money</summary>
        public bool HasEnoughMoney(int amount)
        {
            return _currentMoney.Value >= amount;
        }

        /// <summary>
        /// Reset money to starting amount. SERVER-ONLY: para NetworkVariable'i
        /// server-authoritative; client'tan cagrilirsa no-op (deger zaten
        /// server'dan replike olur). Tum cagiranlar server-context dogrulandi.
        /// </summary>
        public void ResetMoney()
        {
            if (!IsServer)
            {
                Debug.LogWarning("MoneySystem.ResetMoney client'tan cagrildi — yok sayildi (server-only).");
                return;
            }
            _currentMoney.Value = startingMoney;
        }

        /// <summary>
        /// Set money to specific amount. SERVER-ONLY (bkz. ResetMoney). Onceki
        /// SetMoneyServerRpc (RequireOwnership=false) herhangi client'in parayi
        /// istedigi degere set etmesine izin veriyordu (G1 exploit) — kaldirildi.
        /// Tek mesru cagiran DifficultyManager.ApplyMoneySettings zaten server-only.
        /// </summary>
        public void SetMoney(int amount)
        {
            if (!IsServer)
            {
                Debug.LogWarning("MoneySystem.SetMoney client'tan cagrildi — yok sayildi (server-only).");
                return;
            }
            _currentMoney.Value = Mathf.Max(0, amount);
        }
    }
}