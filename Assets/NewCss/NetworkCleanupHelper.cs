using UnityEngine;
using Unity.Netcode;

public static class NetworkCleanupHelper
{
    /// <summary>
    /// NetworkManager'ın aktif ve listening durumda olup olmadığını kontrol eder
    /// </summary>
    public static bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && 
               (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient) &&
               NetworkManager.Singleton.IsListening;
    }

    /// <summary>
    /// Güvenli şekilde NetworkObject spawn eder
    /// </summary>
    public static bool SafeSpawn(NetworkObject networkObject)
    {
        if (!IsNetworkActive())
        {
            Debug.LogWarning("Cannot spawn NetworkObject - NetworkManager is not active");
            return false;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Cannot spawn NetworkObject - Only server can spawn objects");
            return false;
        }

        try
        {
            networkObject.Spawn();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to spawn NetworkObject: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Güvenli şekilde NetworkObject despawn eder
    /// </summary>
    public static bool SafeDespawn(NetworkObject networkObject)
    {
        if (!IsNetworkActive() || networkObject == null || !networkObject.IsSpawned)
        {
            return false;
        }

        try
        {
            networkObject.Despawn();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to despawn NetworkObject: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sahnedeki tüm NetworkObject'leri temizler
    /// </summary>
    public static void CleanupAllNetworkObjects()
    {
        if (!IsNetworkActive()) return;

        NetworkObject[] networkObjects = Object.FindObjectsOfType<NetworkObject>();
        foreach (NetworkObject networkObject in networkObjects)
        {
            if (networkObject.IsSpawned)
            {
                SafeDespawn(networkObject);
            }
        }
    }
}