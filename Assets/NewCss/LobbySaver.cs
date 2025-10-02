using System;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

public class LobbySaver : MonoBehaviour
{
    public Lobby? CurrentLobby;
    public static LobbySaver instance;
    
    private void Awake()
    {
        // Singleton pattern - eğer başka bir instance varsa yok et
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        Debug.Log("LobbySaver initialized");
    }
    
    // Lobby'yi temizleme metodu - geliştirilmiş
    public void ClearLobby()
    {
        if (CurrentLobby.HasValue)
        {
            Debug.Log($"LobbySaver: Clearing lobby {CurrentLobby.Value.Id}");
            
            try
            {
                // Lobby'den ayrıl
                CurrentLobby.Value.Leave();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error leaving lobby: {ex.Message}");
            }
            
            CurrentLobby = null;
        }
        else
        {
            Debug.Log("LobbySaver: No lobby to clear");
        }
    }
    
    // Zorlu temizlik metodu
    public void ForceClearLobby()
    {
        Debug.Log("LobbySaver: Force clearing lobby...");
        CurrentLobby = null;
    }
    
    // Application kapatılırken temizlik yap
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ClearLobby();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ClearLobby();
        }
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            ClearLobby(); // Temizlik yap
            instance = null;
        }
    }
    
    // Application kapatılırken son temizlik
    private void OnApplicationQuit()
    {
        ClearLobby();
    }
}