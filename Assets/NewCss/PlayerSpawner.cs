using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Collections;

public class PlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject PlayerPrefab;
    
    // Spawn pozisyonları - Map1 scene'inde uygun pozisyonları ayarlayın
    [SerializeField] private Transform[] spawnPoints;
    
    // Spawn edilmiş oyuncuları takip et
    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    
    // Client'ların hazır olma durumunu takip et
    private Dictionary<ulong, bool> clientsReady = new Dictionary<ulong, bool>();

    void Start()
    {
        // REMOVED: DontDestroyOnLoad - PlayerSpawner artık scene-specific
        // Bu objeyi sadece Map1 scene'inde kullan
        if (SceneManager.GetActiveScene().name != "The Main Office")
        {
            Debug.LogWarning("PlayerSpawner should only exist in Map1 scene");
        }
    }

    void OnEnable()
    {
        // Scene değişikliklerini dinle
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Eğer MainMenu'ye dönülürse, bu objeyi temizle
        if (scene.name == "MainMenu")
        {
            Debug.Log("PlayerSpawner: MainMenu loaded, cleaning up...");
            CleanupAndDestroy();
        }
    }

    private void CleanupAndDestroy()
    {
        // Tüm spawn edilmiş oyuncuları temizle
        if (IsHost)
        {
            foreach (var kvp in spawnedPlayers)
            {
                if (kvp.Value != null && kvp.Value.GetComponent<NetworkObject>() != null)
                {
                    kvp.Value.GetComponent<NetworkObject>().Despawn();
                }
            }
        }
        
        spawnedPlayers.Clear();
        clientsReady.Clear();
        
        // Network event'lerden ayrıl
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
            }
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        // Objeyi yok et
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"PlayerSpawner NetworkSpawn - Scene: {SceneManager.GetActiveScene().name}");
        
        // Sadece Map1'de çalış
        if (SceneManager.GetActiveScene().name != "The Main Office")
        {
            Debug.LogWarning("PlayerSpawner spawned in wrong scene, will be destroyed");
            return;
        }
        
        // Scene yükleme olayına abone ol
        if (NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadEventCompleted;
        }
        
        // Client connection olaylarına abone ol
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        
        Debug.Log($"PlayerSpawner NetworkSpawn - IsHost: {IsHost}, IsClient: {IsClient}");
        
        // Eğer zaten Map1'deysek ve host ise
        if (IsHost && SceneManager.GetActiveScene().name == "The Main Office")
        {
            StartCoroutine(DelayedSpawnAllPlayers());
        }
        
        // Client ise, host'a hazır olduğunu bildir
        if (IsClient && !IsHost)
        {
            StartCoroutine(NotifyHostWhenReady());
        }
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log("PlayerSpawner NetworkDespawn called");
        
        // Event'lerden abone olmayı iptal et
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadEventCompleted;
            }
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        clientsReady[clientId] = false;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        clientsReady.Remove(clientId);
        
        if (spawnedPlayers.ContainsKey(clientId))
        {
            if (spawnedPlayers[clientId] != null)
            {
                spawnedPlayers[clientId].GetComponent<NetworkObject>().Despawn();
            }
            spawnedPlayers.Remove(clientId);
        }
    }

    private IEnumerator NotifyHostWhenReady()
    {
        // Client'ın tamamen hazır olmasını bekle
        yield return new WaitForSeconds(1f);
        
        // Scene tamamen yüklenmiş mi kontrol et
        while (SceneManager.GetActiveScene().name != "The Main Office")
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Host'a hazır olduğunu bildir
        NotifyHostClientReadyServerRpc(NetworkManager.Singleton.LocalClientId);
        Debug.Log("Client notified host that it's ready");
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyHostClientReadyServerRpc(ulong clientId)
    {
        Debug.Log($"Host received ready notification from client: {clientId}");
        clientsReady[clientId] = true;
        
        // Bu client için oyuncu spawn et
        SpawnPlayerForClient(clientId);
    }

    private void OnSceneLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, 
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"Scene load completed: {sceneName}");
        Debug.Log($"Clients completed: {clientsCompleted.Count}");
        Debug.Log($"Clients timed out: {clientsTimedOut.Count}");
        
        if (IsHost && sceneName == "The Main Office")
        {
            // Timed out olan client'ları logla
            foreach (ulong timedOutClient in clientsTimedOut)
            {
                Debug.LogWarning($"Client {timedOutClient} timed out during scene load");
            }
            
            // Tüm client'ları ready olarak işaretle (scene load tamamlandığı için)
            foreach (ulong completedClient in clientsCompleted)
            {
                clientsReady[completedClient] = true;
            }
            
            // Spawn işlemini başlat
            StartCoroutine(DelayedSpawnAllPlayers());
        }
    }

    private IEnumerator DelayedSpawnAllPlayers()
    {
        // Host'un da hazır olmasını bekle
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("Starting delayed spawn process...");
        
        // Önce mevcut oyuncuları kontrol et ve temizle
        CleanupExistingPlayers();
        
        // Tüm bağlı client'lar için spawn et
        yield return new WaitForSeconds(0.5f);
        SpawnAllPlayers();
    }

    private void CleanupExistingPlayers()
    {
        if (!IsHost) return;

        var connectedClients = NetworkManager.Singleton.ConnectedClients;
        List<ulong> toRemove = new List<ulong>();

        foreach (var kvp in spawnedPlayers)
        {
            ulong clientId = kvp.Key;
            GameObject playerObj = kvp.Value;

            // Eğer client artık bağlı değilse veya player object null ise
            if (!connectedClients.ContainsKey(clientId) || playerObj == null)
            {
                toRemove.Add(clientId);
                if (playerObj != null)
                {
                    playerObj.GetComponent<NetworkObject>().Despawn();
                }
            }
        }

        foreach (ulong clientId in toRemove)
        {
            spawnedPlayers.Remove(clientId);
        }
    }

    private void SpawnAllPlayers()
    {
        if (!IsHost) return;

        Debug.Log("Spawning all players...");
        
        var connectedClients = NetworkManager.Singleton.ConnectedClients;

        foreach (var kvp in connectedClients)
        {
            ulong clientId = kvp.Key;
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (!IsHost) return;

        // Bu client için zaten spawn edilmiş bir oyuncu var mı?
        if (spawnedPlayers.ContainsKey(clientId) && spawnedPlayers[clientId] != null)
        {
            Debug.Log($"Client {clientId} already has a spawned player, skipping...");
            return;
        }

        // Client bağlı mı kontrol et
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogWarning($"Client {clientId} is not connected, cannot spawn player");
            return;
        }

        var client = NetworkManager.Singleton.ConnectedClients[clientId];
        
        // Client'ın zaten bir player object'i var mı?
        if (client.PlayerObject != null)
        {
            Debug.Log($"Client {clientId} already has a player object assigned, skipping...");
            return;
        }

        try
        {
            // Player prefab'ı instantiate et
            GameObject playerInstance = Instantiate(PlayerPrefab);
            
            // Spawn pozisyonu ayarla
            int spawnIndex = (int)(clientId % (ulong)spawnPoints.Length);
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform spawnPoint = spawnPoints[spawnIndex];
                playerInstance.transform.position = spawnPoint.position;
                playerInstance.transform.rotation = spawnPoint.rotation;
            }
            else
            {
                // Default spawn pozisyonu
                playerInstance.transform.position = Vector3.zero + Vector3.right * (float)clientId * 2f;
            }
            
            // NetworkObject'i bu client'a ata ve spawn et
            var networkObject = playerInstance.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(clientId, true);
            
            // Spawn edilmiş oyuncuları takip et
            spawnedPlayers[clientId] = playerInstance;
            
            Debug.Log($"Successfully spawned player for client: {clientId} at position: {playerInstance.transform.position}");
            
            // Client'a spawn edildiğini bildir
            NotifyClientPlayerSpawnedClientRpc(clientId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error spawning player for client {clientId}: {ex.Message}");
        }
    }

    [ClientRpc]
    private void NotifyClientPlayerSpawnedClientRpc(ulong clientId)
    {
        Debug.Log($"Client {NetworkManager.Singleton.LocalClientId} notified that player spawned for client {clientId}");
        
        // Eğer bu bizim client'ımızsa
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("My player has been spawned!");
            
            // Kamerayı player'a odakla veya diğer client-side ayarları yap
            StartCoroutine(SetupLocalPlayerAfterSpawn());
        }
    }

    private IEnumerator SetupLocalPlayerAfterSpawn()
    {
        yield return new WaitForSeconds(0.1f);
        
        // Local player'ı bul
        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient.PlayerObject != null)
        {
            Debug.Log("Local player found and ready!");
            // Burada kamera ayarları veya diğer local player ayarları yapabilirsiniz
        }
    }

    // Manual olarak spawn tetiklemek için (debug amaçlı)
    [ContextMenu("Force Spawn All Players")]
    private void ForceSpawnAllPlayers()
    {
        if (IsHost)
        {
            SpawnAllPlayers();
        }
    }

    // Manual olarak cleanup yapmak için (debug amaçlı)
    [ContextMenu("Cleanup Players")]
    private void ForceCleanup()
    {
        if (IsHost)
        {
            CleanupExistingPlayers();
        }
    }
}