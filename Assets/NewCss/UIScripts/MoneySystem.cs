using System;
using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    public class MoneySystem : NetworkBehaviour
    {
        public static MoneySystem Instance { get; private set; }

        [Header("Starting Amount")] 
        public int startingMoney = 100000; // Test için 100000

        // NetworkVariable'ı başlangıç değeri ile initialize et
        private NetworkVariable<int> _currentMoney = new NetworkVariable<int>(
            100000, // Default value
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

        /// <summary>Adds (positive) or subtracts (negative) money.</summary>
        public void ModifyMoney(int delta)
        {
            if (IsServer)
            {
                int newValue = Mathf.Max(0, _currentMoney.Value + delta);
                _currentMoney.Value = newValue;
                Debug.Log($"Server: Money modified by {delta}, new value: {newValue}");
            }
            else
            {
                Debug.Log($"Client: Requesting money modification by {delta}");
                ModifyMoneyServerRpc(delta);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ModifyMoneyServerRpc(int delta)
        {
            int newValue = Mathf.Max(0, _currentMoney.Value + delta);
            _currentMoney.Value = newValue;
            Debug.Log($"ServerRpc: Money modified by {delta}, new value: {newValue}");
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

        /// <summary>Reset money to starting amount (Server only)</summary>
        public void ResetMoney()
        {
            if (IsServer)
            {
                _currentMoney.Value = startingMoney;
            }
            else
            {
                ResetMoneyServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ResetMoneyServerRpc()
        {
            _currentMoney.Value = startingMoney;
        }

        /// <summary>Set money to specific amount (Server only)</summary>
        public void SetMoney(int amount)
        {
            if (IsServer)
            {
                _currentMoney.Value = Mathf.Max(0, amount);
            }
            else
            {
                SetMoneyServerRpc(amount);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetMoneyServerRpc(int amount)
        {
            _currentMoney.Value = Mathf.Max(0, amount);
        }
    }
}