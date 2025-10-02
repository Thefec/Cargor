using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Steamworks;
using System.Collections;

/// <summary>
/// NetworkBehaviour'dan baðýmsýz exit iþlemlerini yöneten helper sýnýf
/// Shutdown sonrasý da çalýþmaya devam eder
/// </summary>
public class ExitHelper : MonoBehaviour
{
    private static ExitHelper _instance;
    public static ExitHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ExitHelper");
                _instance = go.AddComponent<ExitHelper>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartExitProcess(bool isHost, bool isHostShutdown)
    {
        StartCoroutine(ExitCoroutine(isHost, isHostShutdown));
    }

    private IEnumerator ExitCoroutine(bool isHost, bool isHostShutdown)
    {
        Debug.Log($"[ExitHelper] Starting exit process. IsHost={isHost}, HostShutdown={isHostShutdown}");

        // 1. Zamaný normale döndür
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.1f);

        // 2. Host ise cleanup yap
        if (isHost)
        {
            Debug.Log("[ExitHelper] Host cleanup - Resetting managers");
            ResetAllGameManagers();
            yield return new WaitForSecondsRealtime(0.2f);

            // Network cleanup
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                Debug.Log("[ExitHelper] Cleaning network objects");
                NetworkCleanupHelper.CleanupAllNetworkObjects();
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }

        // 3. Network Shutdown
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[ExitHelper] Shutting down NetworkManager");
            NetworkManager.Singleton.Shutdown();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // 4. Steam Cleanup
        LeaveSteamLobby();
        yield return new WaitForSecondsRealtime(0.2f);

        // 5. Scene yükle
        Debug.Log("[ExitHelper] Loading MainMenu");
        SceneManager.LoadScene("MainMenu");
    }

    private void ResetAllGameManagers()
    {
        Debug.Log("=== RESETTING ALL GAME MANAGERS ===");

        if (NewCss.GameStateManager.Instance != null)
        {
            NewCss.GameStateManager.Instance.ResetGameState();
        }

        if (NewCss.DayCycleManager.Instance != null)
        {
            NewCss.DayCycleManager.Instance.ResetDayCycle();
        }

        Debug.Log("All game managers reset completed");
    }

    private void LeaveSteamLobby()
    {
        SteamManager steamManager = FindObjectOfType<SteamManager>();
        if (steamManager != null)
        {
            Debug.Log("Leaving Steam lobby");
            steamManager.LeaveLobby();
        }

        if (LobbySaver.instance != null)
        {
            LobbySaver.instance.CurrentLobby = null;
        }
    }
}