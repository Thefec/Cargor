using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using Unity.Collections;

public class LobbyManager : NetworkBehaviour
{
    [Header("UI References")]
    public Button createLobbyButton;
    public Button joinLobbyButton;
    public TMP_InputField lobbyIdInput;
    public TMP_Text lobbyIdDisplay;
    public GameObject lobbyUI;
    public GameObject mainMenuUI;
    
    [Header("Player Slots")]
    public PlayerSlot[] playerSlots = new PlayerSlot[4];
    
    [Header("Game Settings")]
    public Button startGameButton;
    public TMP_Text playerCountText;
    
    // Network Variables
    private NetworkVariable<int> connectedPlayers = new NetworkVariable<int>(0);
    private NetworkVariable<FixedString128Bytes> currentLobbyId = new NetworkVariable<FixedString128Bytes>("");
    
    // Local variables
    private string generatedLobbyId;
    private const int MAX_PLAYERS = 4;
    
    private void Start()
    {
        // UI Event listeners
        createLobbyButton.onClick.AddListener(CreateLobby);
        joinLobbyButton.onClick.AddListener(JoinLobby);
        startGameButton.onClick.AddListener(StartGame);
        
        // Initialize UI
        lobbyUI.SetActive(false);
        mainMenuUI.SetActive(true);
        startGameButton.interactable = false;
        
        // Network events
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }
    
    public void CreateLobby()
    {
        // Generate unique lobby ID
        generatedLobbyId = GenerateLobbyId();
        
        // Start host
        NetworkManager.Singleton.StartHost();
        
        // Switch to lobby scene
        SwitchToLobbyScene();
    }
    
    public void JoinLobby()
    {
        string lobbyId = lobbyIdInput.text.Trim().ToUpper();
        
        if (string.IsNullOrEmpty(lobbyId))
        {
            Debug.LogWarning("Lobby ID cannot be empty!");
            return;
        }
        
        if (lobbyId.Length != 6)
        {
            Debug.LogWarning("Lobby ID must be 6 characters!");
            return;
        }
        
        // Start client and try to connect
        NetworkManager.Singleton.StartClient();
        
        // Client bağlandığında UI'ı değiştir
        StartCoroutine(WaitForConnection());
        
        // Note: In a real implementation, you'd need a relay service
        // For now, this assumes local connection
    }
    
    private System.Collections.IEnumerator WaitForConnection()
    {
        // Bağlantı kurulana kadar bekle
        while (!NetworkManager.Singleton.IsConnectedClient)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Client bağlandı, UI'ı değiştir
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            SwitchToLobbySceneAsClient();
        }
    }
    
    private void SwitchToLobbySceneAsClient()
    {
        Debug.Log("Switching to lobby scene as client");
        mainMenuUI.SetActive(false);
        lobbyUI.SetActive(true);
        
        // Client için lobby ID'yi göster (host'tan gelecek)
        StartCoroutine(WaitForLobbyId());
    }
    
    private System.Collections.IEnumerator WaitForLobbyId()
    {
        while (string.IsNullOrEmpty(currentLobbyId.Value.ToString()))
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        lobbyIdDisplay.text = $"Lobby ID: {currentLobbyId.Value}";
        UpdatePlayerSlots();
    }
    
    private void SwitchToLobbyScene()
    {
        mainMenuUI.SetActive(false);
        lobbyUI.SetActive(true);
        
        // Display lobby ID
        lobbyIdDisplay.text = $"Lobby ID: {generatedLobbyId}";
        
        // Initialize host as first player
        if (IsHost)
        {
            UpdatePlayerSlots();
        }
    }
    
    private string GenerateLobbyId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] result = new char[6];
        
        for (int i = 0; i < 6; i++)
        {
            result[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        }
        
        return new string(result);
    }
    
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected. IsServer: {IsServer}, IsHost: {IsHost}");
        
        if (IsServer)
        {
            connectedPlayers.Value++;
            // Server'da slot'ları güncelle ve tüm client'lara gönder
            UpdatePlayerSlotsClientRpc();
        }
        
        if (IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Host connected, set lobby ID
            currentLobbyId.Value = generatedLobbyId;
            // Host bağlandığında slot'ları güncelle
            Invoke("UpdatePlayerSlots", 0.1f);
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected. IsServer: {IsServer}");
        
        if (IsServer)
        {
            connectedPlayers.Value--;
            // Server'da slot'ları güncelle ve tüm client'lara gönder
            UpdatePlayerSlotsClientRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerCountServerRpc()
    {
        // Bu method artık kullanılmıyor, UpdatePlayerSlotsClientRpc kullanıyoruz
        UpdatePlayerSlotsClientRpc();
    }
    
    [ClientRpc]
    private void UpdatePlayerSlotsClientRpc()
    {
        Debug.Log($"UpdatePlayerSlotsClientRpc called. Connected clients: {NetworkManager.Singleton.ConnectedClients.Count}");
        UpdatePlayerSlots();
        UpdatePlayerCountText();
        UpdateStartButtonState();
    }
    
    private void UpdatePlayerCountText()
    {
        if (playerCountText != null)
        {
            int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            playerCountText.text = $"Players: {currentPlayers}/{MAX_PLAYERS}";
            Debug.Log($"Updated player count: {currentPlayers}/{MAX_PLAYERS}");
        }
    }
    
    private void UpdatePlayerSlots()
    {
        Debug.Log($"UpdatePlayerSlots called. Connected clients: {NetworkManager.Singleton.ConnectedClients.Count}");
        
        // Clear all slots first
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null)
            {
                playerSlots[i].ClearSlot();
            }
        }
        
        // Fill slots with connected players
        int slotIndex = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            if (slotIndex < playerSlots.Length && playerSlots[slotIndex] != null)
            {
                bool isHost = client.Key == NetworkManager.Singleton.LocalClientId && IsHost;

                string playerName = $"Player {client.Key}";
                
                Debug.Log($"Setting slot {slotIndex}: {playerName} (Host: {isHost})");
                playerSlots[slotIndex].SetPlayer(playerName, isHost);
                slotIndex++;
            }
        }
    }
    
    private void UpdateStartButtonState()
    {
        if (IsHost)
        {
            startGameButton.interactable = NetworkManager.Singleton.ConnectedClients.Count >= 2;
        }
        else
        {
            startGameButton.gameObject.SetActive(false);
        }
    }
    
    private void StartGame()
    {
        if (!IsHost) return;
        
        StartGameServerRpc();
    }
    
    [ServerRpc]
    private void StartGameServerRpc()
    {
        // Load game scene for all clients
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }
    
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}