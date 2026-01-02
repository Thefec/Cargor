using UnityEngine;
using TMPro;
using Unity.Netcode;
using Steamworks.Data;
using Color = UnityEngine.Color;

namespace NewCss
{
    /// <summary>
    /// Displays difficulty level on the lobby screen.
    /// Shows difficulty based on player count.
    /// </summary>
    public class LobbyDifficultyDisplay : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[LobbyDifficulty]";

        #endregion

        #region Serialized Fields

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Zorluk göstergesi text'i")]
        private TextMeshProUGUI difficultyText;

        [Header("=== UPDATE SETTINGS ===")]
        [SerializeField, Tooltip("Update interval (seconds)")]
        private float updateInterval = 1f;

        [Header("=== LOCALIZATION ===")]
        [SerializeField, Tooltip("Format string: {0}=PlayerCount, {1}=DifficultyName")]
        private string displayFormat = "Current Difficulty: {0} Player(s) ({1})";

        #endregion

        #region Private Fields

        private float _lastUpdateTime;
        private int _lastPlayerCount = -1;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            UpdateDisplay();
        }

        private void Update()
        {
            if (Time.time - _lastUpdateTime >= updateInterval)
            {
                UpdateDisplay();
                _lastUpdateTime = Time.time;
            }
        }

        #endregion

        #region Display Update

        private void UpdateDisplay()
        {
            if (difficultyText == null) return;

            int playerCount = GetCurrentPlayerCount();

            // Only update if player count changed
            if (playerCount == _lastPlayerCount) return;
            _lastPlayerCount = playerCount;

            string difficultyName = GetDifficultyName(playerCount);

            // Try to get localized format
            string localizedFormat = LocalizationHelper.GetLocalizedString("DifficultyIndicator");
            if (string.IsNullOrEmpty(localizedFormat) || localizedFormat == "DifficultyIndicator")
            {
                localizedFormat = displayFormat;
            }

            try
            {
                difficultyText.text = string.Format(localizedFormat, playerCount, difficultyName);
            }
            catch
            {
                difficultyText.text = $"Current Difficulty: {playerCount} Player(s) ({difficultyName})";
            }

            // Set color based on difficulty
            difficultyText.color = GetDifficultyColor(playerCount);

            Debug.Log($"{LOG_PREFIX} Updated display: {playerCount} players ({difficultyName})");
        }

        private int GetCurrentPlayerCount()
        {
            // First try DifficultyManager
            if (DifficultyManager.Instance != null)
            {
                return DifficultyManager.Instance.PlayerCount;
            }

            // Then try Steam lobby
            if (LobbySaver.instance != null && LobbySaver.instance.CurrentLobby.HasValue)
            {
                try
                {
                    Lobby lobby = LobbySaver.instance.CurrentLobby.Value;
                    int count = 0;
                    foreach (var member in lobby.Members)
                    {
                        count++;
                    }
                    if (count > 0) return count;
                }
                catch (System.Exception ex)
                {
                    // Steam lobby not accessible - this is expected in some scenarios
                    Debug.LogWarning($"{LOG_PREFIX} Could not access Steam lobby: {ex.Message}");
                }
            }

            // Then try NetworkManager
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                return NetworkManager.Singleton.ConnectedClientsList.Count;
            }

            return 1;
        }

        private string GetDifficultyName(int playerCount)
        {
            string localizedName = playerCount switch
            {
                1 => LocalizationHelper.GetLocalizedString("DifficultyEasy"),
                2 => LocalizationHelper.GetLocalizedString("DifficultyNormal"),
                3 => LocalizationHelper.GetLocalizedString("DifficultyHard"),
                4 => LocalizationHelper.GetLocalizedString("DifficultyExpert"),
                _ => "Unknown"
            };

            // Fallback if localization fails
            if (string.IsNullOrEmpty(localizedName) || localizedName.StartsWith("Difficulty"))
            {
                return playerCount switch
                {
                    1 => "Easy",
                    2 => "Normal",
                    3 => "Hard",
                    4 => "Expert",
                    _ => "Unknown"
                };
            }

            return localizedName;
        }

        private UnityEngine.Color GetDifficultyColor(int playerCount)
{
    return playerCount switch
    {
        1 => UnityEngine.Color.green,
        2 => UnityEngine.Color.yellow,
        3 => new UnityEngine.Color(1f, 0.5f, 0f), // Orange
        4 => UnityEngine.Color.red,
        _ => UnityEngine.Color.white
    };
}

        #endregion

        #region Public API

        /// <summary>
        /// Forces immediate display update
        /// </summary>
        public void ForceUpdate()
        {
            _lastPlayerCount = -1;
            UpdateDisplay();
        }

        /// <summary>
        /// Sets the text reference for the display
        /// </summary>
        public void SetTextReference(TextMeshProUGUI text)
        {
            difficultyText = text;
            ForceUpdate();
        }

        #endregion
    }
}
