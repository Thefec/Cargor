using UnityEngine;
using TMPro;
using System;
using Unity.Netcode;

public class PrestigeManager : NetworkBehaviour
{
    public static PrestigeManager Instance { get; private set; }

    public static event Action<float> OnPrestigeChanged;

    [Header("Prestige Settings")]
    [Tooltip("Initial prestige value")]
    public float startingPrestige = 0f;

    [Tooltip("UI Text displaying the prestige value")]
    public TMP_Text prestigeText;

    // NetworkVariable to sync prestige across all clients
    private NetworkVariable<float> currentPrestige = new NetworkVariable<float>(
        0f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
            if (IsOwner)
                NetworkObject.Despawn();
            return;
        }
        Instance = this;

        // Subscribe to network variable changes
        currentPrestige.OnValueChanged += OnPrestigeValueChanged;

        // Initialize prestige value if we're the server
        if (IsServer)
        {
            currentPrestige.Value = startingPrestige;
        }

        UpdatePrestigeUI();
    }

    public override void OnNetworkDespawn()
    {
        if (currentPrestige != null)
        {
            currentPrestige.OnValueChanged -= OnPrestigeValueChanged;
        }
    }

    private void OnPrestigeValueChanged(float oldValue, float newValue)
    {
        UpdatePrestigeUI();
        OnPrestigeChanged?.Invoke(newValue);
    }

    void Start()
    {
        // Log temizlendi
    }

    // Client calls this to request prestige modification
    public void ModifyPrestige(float amount)
    {
        if (IsServer)
        {
            // Server can modify directly
            ModifyPrestigeServerRpc(amount);
        }
        else
        {
            // Client requests modification from server
            RequestModifyPrestigeServerRpc(amount);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestModifyPrestigeServerRpc(float amount)
    {
        ModifyPrestigeServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ModifyPrestigeServerRpc(float amount)
    {
        if (!IsServer) return;

        float oldPrestige = currentPrestige.Value;
        currentPrestige.Value += amount;
    }

    public void AddPrestige(float amount)
    {
        ModifyPrestige(amount);
    }

    public void SubtractPrestige(float amount)
    {
        ModifyPrestige(-Mathf.Abs(amount));
    }

    private void UpdatePrestigeUI()
    {
        if (prestigeText != null)
        {
            prestigeText.text = $"Prestige: {currentPrestige.Value:F2}";
        }
    }

    public float GetPrestige()
    {
        return currentPrestige.Value;
    }

    public bool HasMinimumPrestige(float minimumRequired)
    {
        return currentPrestige.Value >= minimumRequired;
    }

    public void ResetPrestige()
    {
        if (IsServer)
        {
            ResetPrestigeServerRpc();
        }
        else
        {
            RequestResetPrestigeServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestResetPrestigeServerRpc()
    {
        ResetPrestigeServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetPrestigeServerRpc()
    {
        if (!IsServer) return;

        currentPrestige.Value = startingPrestige;
    }

    public void SetPrestige(float newValue)
    {
        if (IsServer)
        {
            SetPrestigeServerRpc(newValue);
        }
        else
        {
            RequestSetPrestigeServerRpc(newValue);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetPrestigeServerRpc(float newValue)
    {
        SetPrestigeServerRpc(newValue);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPrestigeServerRpc(float newValue)
    {
        if (!IsServer) return;

        currentPrestige.Value = newValue;
    }

    // Utility method to check if this client can modify prestige
    public bool CanModifyPrestige()
    {
        return IsServer;
    }

    // Method to get current network status
    public bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}