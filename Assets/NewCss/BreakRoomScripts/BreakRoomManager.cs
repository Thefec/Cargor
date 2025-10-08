using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class BreakRoomManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject nextDayUI;
    [SerializeField] private TextMeshProUGUI playersInBreakRoomText;

    // Network variable to track players in break room
    private NetworkVariable<int> playersInBreakRoom = new NetworkVariable<int>(0);
    private NetworkVariable<int> totalActivePlayers = new NetworkVariable<int>(0);

    // Local cache of players who entered the break room
    private HashSet<ulong> playersInRoom = new HashSet<ulong>();

    public static BreakRoomManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Server başlatıldığında toplam oyuncu sayısını güncelle
            totalActivePlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;

            // Bağlantı/kopma olaylarını dinle
            NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnected;
        }

        // UI'ı başlangıçta gizle
        if (nextDayUI != null)
        {
            nextDayUI.SetActive(false);
        }

        // Network değişkenlerini dinle
        playersInBreakRoom.OnValueChanged += OnPlayersInBreakRoomChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnPlayerConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnPlayerDisconnected;
        }

        playersInBreakRoom.OnValueChanged -= OnPlayersInBreakRoomChanged;
        base.OnNetworkDespawn();
    }

    private void OnPlayerConnected(ulong clientId)
    {
        if (IsServer)
        {
            totalActivePlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;
            Debug.Log($"Player connected. Total players: {totalActivePlayers.Value}");
        }
    }

    private void OnPlayerDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            totalActivePlayers.Value = NetworkManager.Singleton.ConnectedClients.Count;

            // Eğer ayrılan oyuncu break room'daysa, onu listeden çıkar
            if (playersInRoom.Contains(clientId))
            {
                playersInRoom.Remove(clientId);
                playersInBreakRoom.Value = playersInRoom.Count;
            }

            Debug.Log($"Player disconnected. Total players: {totalActivePlayers.Value}");
        }
    }

    private void OnPlayersInBreakRoomChanged(int previous, int current)
    {
        UpdateBreakRoomUI();
        CheckAndShowNextDayUI();
    }

    private void UpdateBreakRoomUI()
    {
        if (playersInBreakRoomText != null)
        {
            playersInBreakRoomText.text = $"Players in Break Room: {playersInBreakRoom.Value}/{totalActivePlayers.Value}";
        }
    }

    private void CheckAndShowNextDayUI()
    {
        if (nextDayUI != null)
        {
            bool shouldShow = playersInBreakRoom.Value >= totalActivePlayers.Value && totalActivePlayers.Value > 0;
            nextDayUI.SetActive(shouldShow);

            if (shouldShow)
            {
                Debug.Log("All players are in break room - showing Next Day UI");
            }
        }
    }

    // Oyuncu break room'a girdiğinde çağrılacak method
    [ServerRpc(RequireOwnership = false)]
    public void PlayerEnteredBreakRoomServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var clientId = serverRpcParams.Receive.SenderClientId;

        if (!playersInRoom.Contains(clientId))
        {
            playersInRoom.Add(clientId);
            playersInBreakRoom.Value = playersInRoom.Count;
            Debug.Log($"Player {clientId} entered break room. Total in room: {playersInBreakRoom.Value}");
        }
    }

    // Oyuncu break room'dan çıktığında çağrılacak method
    [ServerRpc(RequireOwnership = false)]
    public void PlayerExitedBreakRoomServerRpc(ServerRpcParams serverRpcParams = default)
    {
        var clientId = serverRpcParams.Receive.SenderClientId;

        if (playersInRoom.Contains(clientId))
        {
            playersInRoom.Remove(clientId);
            playersInBreakRoom.Value = playersInRoom.Count;
            Debug.Log($"Player {clientId} exited break room. Total in room: {playersInBreakRoom.Value}");
        }
    }
}