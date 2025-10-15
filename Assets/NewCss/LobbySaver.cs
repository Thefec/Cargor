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
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ClearLobby()
    {
        if (CurrentLobby.HasValue)
        {
            try
            {
                CurrentLobby.Value.Leave();
            }
            catch (System.Exception ex)
            {
            }
            CurrentLobby = null;
        }
    }

    public void ForceClearLobby()
    {
        CurrentLobby = null;
    }

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
            ClearLobby();
            instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        ClearLobby();
    }
}