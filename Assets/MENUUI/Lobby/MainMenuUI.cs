using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Menu References")]
    public LobbyManager lobbyManager;
    public TMP_InputField playerNameInput;
    
    private void Start()
    {
        // Set default player name
        if (string.IsNullOrEmpty(playerNameInput.text))
        {
            playerNameInput.text = $"Player{Random.Range(1000, 9999)}";
        }
    }
    
    public void OnPlayerNameChanged()
    {
        // Save player name for networking
        PlayerPrefs.SetString("PlayerName", playerNameInput.text);
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}