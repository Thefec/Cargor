using System;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

/// <summary>
/// Steam lobi referansýný sahneler arasý korur ve uygulama yaþam döngüsü olaylarýnda lobi temizliðini yönetir. 
/// Singleton pattern ile DontDestroyOnLoad kullanarak kalýcýlýk saðlar.
/// </summary>
public class LobbySaver : MonoBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[LobbySaver]";

    #endregion

    #region Singleton

    /// <summary>
    /// Singleton instance
    /// </summary>
    public static LobbySaver instance { get; private set; }

    #endregion

    #region Public Fields

    /// <summary>
    /// Mevcut aktif lobi referansý (nullable)
    /// </summary>
    public Lobby? CurrentLobby;

    #endregion

    #region Public Properties

    /// <summary>
    /// Aktif bir lobi var mý?
    /// </summary>
    public bool HasActiveLobby => CurrentLobby.HasValue && CurrentLobby.Value.Id != 0;

    /// <summary>
    /// Mevcut lobi ID'si (yoksa 0)
    /// </summary>
    public ulong CurrentLobbyId => CurrentLobby?.Id ?? 0;

    /// <summary>
    /// Mevcut lobideki üye sayýsý
    /// </summary>
    public int MemberCount
    {
        get
        {
            if (!HasActiveLobby) return 0;

            try
            {
                return CurrentLobby.Value.MemberCount;
            }
            catch
            {
                return 0;
            }
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeSingleton();
    }

    private void OnDestroy()
    {
        CleanupOnDestroy();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            HandleApplicationPause();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            HandleApplicationLostFocus();
        }
    }

    private void OnApplicationQuit()
    {
        HandleApplicationQuit();
    }

    #endregion

    #region Initialization

    private void InitializeSingleton()
    {
        if (instance != null && instance != this)
        {
            LogWarning("Duplicate instance detected, destroying.. .");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        LogDebug("Singleton initialized");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Mevcut lobiyi temizler ve lobiden ayrýlýr
    /// </summary>
    public void ClearLobby()
    {
        if (!CurrentLobby.HasValue)
        {
            LogDebug("No lobby to clear");
            return;
        }

        TryLeaveLobby();
        CurrentLobby = null;

        LogDebug("Lobby cleared");
    }

    /// <summary>
    /// Lobi referansýný Leave çaðýrmadan temizler (zorla temizleme)
    /// </summary>
    public void ForceClearLobby()
    {
        CurrentLobby = null;
        LogDebug("Lobby force cleared (no Leave call)");
    }

    /// <summary>
    /// Yeni lobi atar
    /// </summary>
    /// <param name="lobby">Atanacak lobi</param>
    public void SetLobby(Lobby lobby)
    {
        // Önceki lobiyi temizle
        if (HasActiveLobby && CurrentLobby.Value.Id != lobby.Id)
        {
            LogDebug($"Replacing lobby {CurrentLobbyId} with {lobby.Id}");
            TryLeaveLobby();
        }

        CurrentLobby = lobby;
        LogDebug($"Lobby set: {lobby.Id}");
    }

    /// <summary>
    /// Lobi geçerli mi kontrol eder
    /// </summary>
    public bool ValidateLobby()
    {
        if (!CurrentLobby.HasValue)
        {
            return false;
        }

        try
        {
            // Lobi hala aktif mi kontrol et
            var memberCount = CurrentLobby.Value.MemberCount;
            return memberCount > 0;
        }
        catch (Exception ex)
        {
            LogWarning($"Lobby validation failed: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Private Methods

    private void TryLeaveLobby()
    {
        if (!CurrentLobby.HasValue)
        {
            return;
        }

        try
        {
            CurrentLobby.Value.Leave();
            LogDebug($"Left lobby: {CurrentLobby.Value.Id}");
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to leave lobby: {ex.Message}");
        }
    }

    #endregion

    #region Application Event Handlers

    private void HandleApplicationPause()
    {
        LogDebug("Application paused - clearing lobby");
        ClearLobby();
    }

    private void HandleApplicationLostFocus()
    {
        LogDebug("Application lost focus - clearing lobby");
        ClearLobby();
    }

    private void HandleApplicationQuit()
    {
        LogDebug("Application quitting - clearing lobby");
        ClearLobby();
    }

    private void CleanupOnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        LogDebug("Instance destroyed - cleaning up");
        ClearLobby();
        instance = null;
    }

    #endregion

    #region Logging

    private static void LogDebug(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"{LOG_PREFIX} {message}");
#endif
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"{LOG_PREFIX} {message}");
    }

    #endregion

    #region Editor Debug

#if UNITY_EDITOR
    [ContextMenu("Debug: Print Lobby State")]
    private void DebugPrintLobbyState()
    {
        Debug.Log($"{LOG_PREFIX} === LOBBY STATE ===");
        Debug.Log($"Has Active Lobby: {HasActiveLobby}");
        Debug.Log($"Current Lobby ID: {CurrentLobbyId}");
        Debug.Log($"Member Count: {MemberCount}");
        Debug.Log($"Is Valid: {ValidateLobby()}");
    }

    [ContextMenu("Debug: Clear Lobby")]
    private void DebugClearLobby()
    {
        ClearLobby();
    }

    [ContextMenu("Debug: Force Clear Lobby")]
    private void DebugForceClearLobby()
    {
        ForceClearLobby();
    }
#endif

    #endregion
}