using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Tutorial seviyesinde oyuncuyu spawn eden özel spawner.
/// Steam lobby olmadan, basit host modunda çalışır.
/// </summary>
public class TutorialPlayerSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform tutorialSpawnPoint;

    [Header("Tutorial Settings")]
    [SerializeField] private bool autoSpawnOnStart = true;
    [SerializeField] private float spawnDelay = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private GameObject spawnedPlayer;
    private bool hasSpawned = false;
    private bool isInitialized = false;

    private void Start()
    {
        if (showDebugLogs)
            Debug.Log("🎓 TutorialPlayerSpawner: Starting...");

        if (autoSpawnOnStart)
        {
            StartCoroutine(InitializeTutorial());
        }
    }

    private IEnumerator InitializeTutorial()
    {
        if (isInitialized)
        {
            if (showDebugLogs)
                Debug.LogWarning("⚠️ Tutorial already initialized!");
            yield break;
        }

        isInitialized = true;

        // NetworkManager'ı kontrol et
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("❌ NetworkManager not found in scene!");
            yield break;
        }

        if (showDebugLogs)
            Debug.Log("✅ NetworkManager found");

        // Eğer zaten network aktifse (örn: lobby'den gelindiyse), oyuncuyu bul
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
        {
            if (showDebugLogs)
                Debug.Log("⚠️ Network already active, finding existing player...");

            yield return new WaitForSeconds(0.5f);
            FindAndSetupExistingPlayer();
            yield break;
        }

        // Tutorial için host modunda başlat
        if (showDebugLogs)
            Debug.Log("🎮 Starting Tutorial Host...");

        bool hostStarted = NetworkManager.Singleton.StartHost();

        if (!hostStarted)
        {
            Debug.LogError("❌ Failed to start host for tutorial!");
            yield break;
        }

        if (showDebugLogs)
            Debug.Log("✅ Host started successfully");

        // Network'ün tamamen hazır olmasını bekle
        yield return new WaitForSeconds(spawnDelay);

        // Oyuncuyu spawn et
        SpawnTutorialPlayer();
    }

    private void FindAndSetupExistingPlayer()
    {
        // Eğer zaten bir PlayerObject varsa, onu kullan
        var localClient = NetworkManager.Singleton.LocalClient;

        if (localClient != null && localClient.PlayerObject != null)
        {
            spawnedPlayer = localClient.PlayerObject.gameObject;

            if (showDebugLogs)
                Debug.Log($"✅ Found existing player: {spawnedPlayer.name}");

            // Spawn noktasına taşı (eğer varsa)
            if (tutorialSpawnPoint != null)
            {
                spawnedPlayer.transform.position = tutorialSpawnPoint.position;
                spawnedPlayer.transform.rotation = tutorialSpawnPoint.rotation;

                if (showDebugLogs)
                    Debug.Log($"✅ Moved player to tutorial spawn point");
            }

            hasSpawned = true;
        }
        else
        {
            if (showDebugLogs)
                Debug.LogWarning("⚠️ No existing player found, attempting manual spawn...");

            SpawnTutorialPlayer();
        }
    }

    private void SpawnTutorialPlayer()
    {
        if (hasSpawned)
        {
            if (showDebugLogs)
                Debug.LogWarning("⚠️ Player already spawned!");
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("❌ Player prefab is not assigned!");
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogError("❌ Only host can spawn players!");
            return;
        }

        try
        {
            // Spawn pozisyonunu belirle
            Vector3 spawnPosition = Vector3.zero;
            Quaternion spawnRotation = Quaternion.identity;

            if (tutorialSpawnPoint != null)
            {
                spawnPosition = tutorialSpawnPoint.position;
                spawnRotation = tutorialSpawnPoint.rotation;

                if (showDebugLogs)
                    Debug.Log($"📍 Spawn position: {spawnPosition}");
            }
            else
            {
                Debug.LogWarning("⚠️ Tutorial spawn point not set, using default position (0,0,0)");
            }

            // Player'ı instantiate et
            GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, spawnRotation);

            if (showDebugLogs)
                Debug.Log($"✅ Player instance created: {playerInstance.name}");

            // NetworkObject component'ini al
            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("❌ Player prefab doesn't have NetworkObject component!");
                Destroy(playerInstance);
                return;
            }

            // Oyuncuyu network'e spawn et
            ulong clientId = NetworkManager.Singleton.LocalClientId;
            networkObject.SpawnAsPlayerObject(clientId, true);

            spawnedPlayer = playerInstance;
            hasSpawned = true;

            if (showDebugLogs)
                Debug.Log($"🎉 Player spawned successfully for client {clientId}!");

            // Tutorial Manager'a bildir
            NotifyTutorialManager();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ Error spawning tutorial player: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void NotifyTutorialManager()
    {
        // TutorialManager'ı bul ve player referansını ver
        TutorialManager tutorialManager = FindObjectOfType<TutorialManager>();

        if (tutorialManager != null && spawnedPlayer != null)
        {
            PlayerInventory playerInventory = spawnedPlayer.GetComponent<PlayerInventory>();

            if (playerInventory != null)
            {
                // TutorialManager'a player'ı manuel olarak ata
                StartCoroutine(AssignPlayerToTutorial(tutorialManager, playerInventory));

                if (showDebugLogs)
                    Debug.Log("✅ Tutorial Manager notified about player spawn");
            }
        }
    }

    private IEnumerator AssignPlayerToTutorial(TutorialManager manager, PlayerInventory player)
    {
        // Player'ın tamamen hazır olmasını bekle
        yield return new WaitForSeconds(0.2f);

        // Tutorial Manager'daki player referansını güncelle
        // (TutorialManager'da bu özelliği ekleyeceğiz)
        if (manager != null)
        {
            manager.SetPlayerInventory(player);

            if (showDebugLogs)
                Debug.Log("✅ Player assigned to TutorialManager");
        }
    }

    // Manuel spawn tetikleme (Inspector'dan test için)
    [ContextMenu("Force Spawn Player")]
    public void ForceSpawn()
    {
        if (!hasSpawned)
        {
            StartCoroutine(InitializeTutorial());
        }
    }

    // Player'ı yeniden spawn noktasına taşı
    [ContextMenu("Reset Player Position")]
    public void ResetPlayerPosition()
    {
        if (spawnedPlayer != null && tutorialSpawnPoint != null)
        {
            spawnedPlayer.transform.position = tutorialSpawnPoint.position;
            spawnedPlayer.transform.rotation = tutorialSpawnPoint.rotation;

            if (showDebugLogs)
                Debug.Log("✅ Player position reset to spawn point");
        }
    }

    private void OnDestroy()
    {
        // Cleanup
        isInitialized = false;
        hasSpawned = false;
    }
}