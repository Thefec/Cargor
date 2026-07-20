using UnityEngine;
using TMPro;
using System;
using Unity.Netcode;
using NewCss;

public class PrestigeManager : NetworkBehaviour
{
    public static PrestigeManager Instance { get; private set; }

    public static event Action<float> OnPrestigeChanged;
    public static event Action<int> OnCustomerCapacityChanged; // Yeni event

    [Header("Prestige Settings")]
    [Tooltip("Initial prestige value")]
    public float startingPrestige = 6f;

    [Tooltip("Maximum prestige (üst tavan / görünür 0-100 skalası). Kutu-başı ödül tier'ının sınırsız şişmesini önler. Tüm prestij ekonomisi 240→100'e küçültüldü (miktarlar ×0.4, eşikler 10→4); pratik tavan ~96, 100 bilinçli güvenlik payı. bkz. .claude/agent-memory/economist/prestige_100_rescale_2026-07-20.md")]
    public float maxPrestige = 100f;

    [Tooltip("UI Text displaying the prestige value")]
    public TMP_Text prestigeText;

    [Header("Customer Capacity Settings")]
    [Tooltip("Prestige required per additional customer")]
    public float prestigePerCustomer = 4f;

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

    /// <summary>
    /// Prestij ekler/çıkarır. SERVER-ONLY (bkz. MoneySystem.ModifyMoney G1-b deseni).
    /// Önceki client-branch ham delta'yı RequestModifyPrestigeServerRpc/ModifyPrestigeServerRpc
    /// (ikisi de [ServerRpc(RequireOwnership=false)]) üzerinden server'a yolluyordu — sınırsız
    /// negatif amount ile herhangi bir client TriggerLose() tetikleyebiliyordu (F5 exploit).
    /// Tüm 7 meşru çağıran (CustomerAI.HandleSuccessfulInteraction/HandleFailedInteraction,
    /// DayCycleManager.TryProcessMoneyCheck, BoxFallPenalty.ApplyPenalty, GameStateManager.OnCustomerLost,
    /// Truck.ProcessWrongDelivery, PhoneCallManager.AnswerServerRpc, QuestManager.ApplyRewardOrPenalty)
    /// zaten server-context'te çalışıyor (gameplay F5 turu doğruladı) — client-context'ten çağrılan
    /// meşru bir yol yok, RPC'ler kaldırıldı.
    /// </summary>
    public void ModifyPrestige(float amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning("PrestigeManager.ModifyPrestige client'tan cagrildi — yok sayildi (server-only).");
            return;
        }

        float newPrestige = currentPrestige.Value + amount;

        // Alt sınır: ham değer <=0 ise game over (clamp ÖNCESİ kontrol — 0'a kırpmak
        // iflas sinyalini yutmasın).
        if (newPrestige <= 0f && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.TriggerLose();
        }

        // Üst tavan: prestij maxPrestige'i aşamaz; alt sınır 0.
        currentPrestige.Value = Mathf.Clamp(newPrestige, 0f, maxPrestige);
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
            prestigeText.text = $"{currentPrestige.Value:F0}";
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
    /// Formula: Base + (Prestige / prestigePerCustomer)  [prestigePerCustomer=4, 100-skala]
    /// Example: 40 prestige = 1 base + 10 bonus = 11 customers
    /// </summary>
    public int GetCustomerCapacity()
    {
        return currentCustomerCapacity.Value;
    }

    public bool HasMinimumPrestige(float minimumRequired)
    {
        return currentPrestige.Value >= minimumRequired;
    }

    /// <summary>
    /// SERVER-ONLY (MoneySystem.ResetMoney G1-a deseni). Önceki client-branch
    /// [ServerRpc(RequireOwnership=false)] üzerinden herhangi bir client'a açıktı;
    /// dış çağıran yok (grep 0) — RPC'ler kaldırıldı, API korundu.
    /// </summary>
    public void ResetPrestige()
    {
        if (!IsServer)
        {
            Debug.LogWarning("PrestigeManager.ResetPrestige client'tan cagrildi — yok sayildi (server-only).");
            return;
        }

        currentPrestige.Value = startingPrestige;
        UpdateCustomerCapacity();
    }

    /// <summary>
    /// SERVER-ONLY (MoneySystem.SetMoney G1-a deseni). Önceki client-branch
    /// [ServerRpc(RequireOwnership=false)] üzerinden sınırsız değere açıktı;
    /// dış çağıran yok (grep 0) — RPC'ler kaldırıldı, clamp eklendi.
    /// </summary>
    public void SetPrestige(float newValue)
    {
        if (!IsServer)
        {
            Debug.LogWarning("PrestigeManager.SetPrestige client'tan cagrildi — yok sayildi (server-only).");
            return;
        }

        currentPrestige.Value = Mathf.Clamp(newValue, 0f, maxPrestige);
        UpdateCustomerCapacity();
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