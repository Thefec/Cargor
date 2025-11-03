using UnityEngine;
using TMPro;
using System;
using Unity.Netcode;

public class PrestigeManager : NetworkBehaviour
{
    public static PrestigeManager Instance { get; private set; }

    public static event Action<float> OnPrestigeChanged;
    public static event Action<int> OnCustomerCapacityChanged; // Yeni event

    [Header("Prestige Settings")]
    [Tooltip("Initial prestige value")]
    public float startingPrestige = 0f;

    [Tooltip("UI Text displaying the prestige value")]
    public TMP_Text prestigeText;

    [Header("Customer Capacity Settings")]
    [Tooltip("Prestige required per additional customer")]
    public float prestigePerCustomer = 10f;

    [Tooltip("Base customer capacity")]
    public int baseCustomerCapacity = 1;

    [Tooltip("Maximum customer capacity")]
    public int maxCustomerCapacity = 20;

    [Tooltip("UI Text displaying customer capacity (optional)")]
    public TMP_Text customerCapacityText;

    // NetworkVariable to sync prestige across all clients
    private NetworkVariable<float> currentPrestige = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // NetworkVariable to sync customer capacity
    private NetworkVariable<int> currentCustomerCapacity = new NetworkVariable<int>(
        1,
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
        currentCustomerCapacity.OnValueChanged += OnCustomerCapacityValueChanged;

        // Initialize prestige value if we're the server
        if (IsServer)
        {
            currentPrestige.Value = startingPrestige;
            UpdateCustomerCapacity();
        }

        UpdatePrestigeUI();
        UpdateCustomerCapacityUI();
    }

    public override void OnNetworkDespawn()
    {
        if (currentPrestige != null)
        {
            currentPrestige.OnValueChanged -= OnPrestigeValueChanged;
        }
        if (currentCustomerCapacity != null)
        {
            currentCustomerCapacity.OnValueChanged -= OnCustomerCapacityValueChanged;
        }
    }

    private void OnPrestigeValueChanged(float oldValue, float newValue)
    {
        UpdatePrestigeUI();
        OnPrestigeChanged?.Invoke(newValue);

        // Server updates customer capacity when prestige changes
        if (IsServer)
        {
            UpdateCustomerCapacity();
        }
    }

    private void OnCustomerCapacityValueChanged(int oldValue, int newValue)
    {
        UpdateCustomerCapacityUI();
        OnCustomerCapacityChanged?.Invoke(newValue);

        Debug.Log($"Customer capacity changed from {oldValue} to {newValue}");
    }

    private void UpdateCustomerCapacity()
    {
        if (!IsServer) return;

        // Calculate new capacity based on prestige
        // Formula: baseCapacity + (prestige / prestigePerCustomer)
        int calculatedCapacity = baseCustomerCapacity + Mathf.FloorToInt(currentPrestige.Value / prestigePerCustomer);

        // Clamp to max capacity
        int newCapacity = Mathf.Clamp(calculatedCapacity, baseCustomerCapacity, maxCustomerCapacity);

        // Only update if changed
        if (currentCustomerCapacity.Value != newCapacity)
        {
            currentCustomerCapacity.Value = newCapacity;
        }
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
            prestigeText.text = $"{currentPrestige.Value:F2}";
        }
    }

    private void UpdateCustomerCapacityUI()
    {
        if (customerCapacityText != null)
        {
            customerCapacityText.text = $"Capacity: {currentCustomerCapacity.Value}";
        }
    }

    public float GetPrestige()
    {
        return currentPrestige.Value;
    }

    /// <summary>
    /// Returns the current customer capacity based on prestige
    /// Formula: Base + (Prestige / 10)
    /// Example: 50 prestige = 1 base + 5 bonus = 6 customers
    /// </summary>
    public int GetCustomerCapacity()
    {
        return currentCustomerCapacity.Value;
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
        UpdateCustomerCapacity();
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

    /// <summary>
    /// Get how much prestige is needed for the next customer slot
    /// </summary>
    public float GetPrestigeForNextCustomer()
    {
        if (currentCustomerCapacity.Value >= maxCustomerCapacity)
            return -1; // Max capacity reached

        float nextThreshold = (currentCustomerCapacity.Value - baseCustomerCapacity + 1) * prestigePerCustomer;
        return nextThreshold - currentPrestige.Value;
    }

    /// <summary>
    /// Get progress towards next customer (0-1)
    /// </summary>
    public float GetProgressToNextCustomer()
    {
        if (currentCustomerCapacity.Value >= maxCustomerCapacity)
            return 1f; // Max capacity reached

        float currentThreshold = (currentCustomerCapacity.Value - baseCustomerCapacity) * prestigePerCustomer;
        float nextThreshold = currentThreshold + prestigePerCustomer;

        return Mathf.Clamp01((currentPrestige.Value - currentThreshold) / prestigePerCustomer);
    }
}