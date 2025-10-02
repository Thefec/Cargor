using Unity.Netcode;
using UnityEngine;

public class NetworkDebugger : NetworkBehaviour
{
    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"=== CLIENT CONNECTED: {clientId} ===");
        
        if (IsServer)
        {
            // 2 saniye bekle ve sonra player'ları kontrol et
            Invoke(nameof(CheckAllPlayers), 2f);
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"=== CLIENT DISCONNECTED: {clientId} ===");
    }
    
    void CheckAllPlayers()
    {
        var allPlayers = FindObjectsOfType<NewCss.PlayerMovement>();
        Debug.Log($"=== SERVER CHECK - TOTAL PLAYERS: {allPlayers.Length} ===");
        
        foreach (var player in allPlayers)
        {
            Debug.Log($"Player {player.OwnerClientId}: Spawned={player.IsSpawned}, Active={player.gameObject.activeInHierarchy}, Position={player.transform.position}");
        }
        
        // Tüm clientlere bilgi gönder
        CheckPlayersClientRpc();
    }
    
    [ClientRpc]
    void CheckPlayersClientRpc()
    {
        var allPlayers = FindObjectsOfType<NewCss.PlayerMovement>();
        Debug.Log($"=== CLIENT CHECK - TOTAL PLAYERS: {allPlayers.Length} ===");
        
        foreach (var player in allPlayers)
        {
            Debug.Log($"Client sees Player {player.OwnerClientId}: Active={player.gameObject.activeInHierarchy}, Position={player.transform.position}");
        }
    }
    
    // Inspector'dan test etmek için
    [ContextMenu("Force Check Players")]
    void ForceCheckPlayers()
    {
        if (IsServer)
            CheckAllPlayers();
    }
}