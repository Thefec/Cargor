using System.Collections.Generic;
using System.Linq;
using Steamworks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace NewCss
{
    /// <summary>
    /// Sonraki gün UI yöneticisi - oyuncu listesi, break room durumu ve gün geçişi UI'ını yönetir.  
    /// Steam lobi entegrasyonu ile multiplayer oyuncu gösterimi sağlar. 
    /// </summary>
    public class NextDayUIManager : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[NextDayUI]";
        private const int MAX_PLAYERS = 4;

        // UI Text Templates (format strings - localized content will be injected)
        private const string PLAYER_LIST_ITEM_FORMAT = "<color=#4CAF50>►</color> <b>{0}. </b> {1}\n";
        private const string PLAYER_IN_ROOM_FORMAT = "<color=#4CAF50>►</color> <b>{0}.</b> {1} <color=#4CAF50>✓</color>\n";
        private const string PLAYER_NOT_IN_ROOM_FORMAT = "<color=#FF5722>►</color> <b>{0}.</b> {1} <color=#FF5722>✗</color>\n";
        private const string PLAYER_LIST_SEPARATOR = "\n<color=#FFC107>━━━━━━━━━━━━━━━━━</color>";
        private const string BREAK_ROOM_SEPARATOR = "\n\n<color=#FF5722>━━━━━━━━━━━━━━━━━</color>";

        // Localization Keys
        private const string LOC_TABLE = "StringTable";
        private const string LOC_KEY_LOBBY_PLAYERS = "LobbyPlayers";
        private const string LOC_KEY_TOTAL_PLAYERS = "TotalPlayers";
        private const string LOC_KEY_BREAK_ROOM_STATUS = "BreakRoomStatus";
        private const string LOC_KEY_PLAYERS_IN_ROOM = "PlayersInRoom";
        private const string LOC_KEY_EVERYONE_READY = "EveryoneReady";
        private const string LOC_KEY_WAITING_PLAYERS = "WaitingPlayers";
        private const string LOC_KEY_NO_PLAYERS = "NoPlayersFound";

        #endregion

        #region Serialized Fields - Main UI

        [Header("=== NEXT DAY UI ELEMENTS ===")]
        [SerializeField, Tooltip("Ana panel referansı")]
        public GameObject nextDayPanel;

        [SerializeField, Tooltip("Oyuncu UI elementleri (Player1-4)")]
        public GameObject[] playerUIElements;

        [SerializeField, Tooltip("Oyuncu isim text'leri")]
        public TextMeshProUGUI[] playerNameTexts;

        [SerializeField, Tooltip("Oyuncu ikonları")]
        public GameObject[] playerIcons;

        #endregion

        #region Serialized Fields - Settings

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Varsayılan oyuncu ismi")]
        public string defaultPlayerName = "Player";

        #endregion

        #region Serialized Fields - Break Room Display

        [Header("=== BREAK ROOM PLAYER LIST ===")]
        [SerializeField, Tooltip("Oyuncu listesi text elementi")]
        public TextMeshProUGUI playerListDisplay;

        [SerializeField, Tooltip("Oyuncu listesi paneli (opsiyonel)")]
        public GameObject playerListPanel;

        #endregion

        #region Private Fields

        private string _currentPlayerListText = string.Empty;
        private bool _wasActive = false;
        private List<string> _playersInBreakRoom = new List<string>();
        private bool _rosterSubscribed;
        private Coroutine _rosterSubscribeRoutine;

        #endregion

        #region Public Properties

        /// <summary>
        /// UI aktif mi?  
        /// </summary>
        public bool IsActive => IsUIActive();

        /// <summary>
        /// Aktif oyuncu sayısı
        /// </summary>
        public int ActivePlayerCount => GetActivePlayerCount();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            UpdateNextDayUI();
        }

        private void OnEnable()
        {
            UpdateNextDayUI();
            SetupCursor();
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

            TrySubscribeToRosterChanged();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;

            if (_rosterSubscribeRoutine != null)
            {
                StopCoroutine(_rosterSubscribeRoutine);
                _rosterSubscribeRoutine = null;
            }

            if (_rosterSubscribed && GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnRosterChanged -= UpdateNextDayUI;
            }
            _rosterSubscribed = false;

            // UI kapandığında hareketi aç
            if (_wasActive)
            {
                UnlockPlayerMovement();
                _wasActive = false;
            }
        }

        /// <summary>
        /// GameStateManager.Instance erken OnEnable sırasında henüz null olabilir; bu durumda
        /// abonelik hiç kurulmaz ve geç-katılan oyuncuda UI güncellenmez. Instance hazır
        /// olana kadar coroutine ile beklenip abonelik güvenceye alınıyor.
        /// </summary>
        private void TrySubscribeToRosterChanged()
        {
            if (_rosterSubscribed)
            {
                return;
            }

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnRosterChanged += UpdateNextDayUI;
                _rosterSubscribed = true;
                return;
            }

            if (_rosterSubscribeRoutine == null)
            {
                _rosterSubscribeRoutine = StartCoroutine(SubscribeToRosterWhenReady());
            }
        }

        private System.Collections.IEnumerator SubscribeToRosterWhenReady()
        {
            while (GameStateManager.Instance == null)
            {
                yield return null;
            }

            if (!_rosterSubscribed)
            {
                GameStateManager.Instance.OnRosterChanged += UpdateNextDayUI;
                _rosterSubscribed = true;
            }

            _rosterSubscribeRoutine = null;
        }

        private void Update()
        {
            if (!IsUIActive())
            {
                // UI kapandıysa hareketi aç
                if (_wasActive)
                {
                    UnlockPlayerMovement();
                    _wasActive = false;
                }
                return;
            }

            // UI açıldığında hareketi kilitle
            if (!_wasActive)
            {
                LockPlayerMovement();
                _wasActive = true;
            }

            SetupCursor();
            HandleEscapeInput();
        }

        #endregion

        #region Movement Lock/Unlock

        /// <summary>
        /// Local oyuncunun hareketini kilitler
        /// </summary>
        private void LockPlayerMovement()
        {
            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                var playerMovement = localPlayer.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.LockMovement(true);
                    LogDebug("Player movement locked - Next Day UI opened");
                }
            }
        }

        /// <summary>
        /// Local oyuncunun hareketini açar
        /// </summary>
        private void UnlockPlayerMovement()
        {
            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                var playerMovement = localPlayer.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.LockMovement(false);
                    LogDebug("Player movement unlocked - Next Day UI closed");
                }
            }

            // BreakRoomManager'a da bildir
            BreakRoomManager.Instance?.OnNextDayUIClosed();
        }

        /// <summary>
        /// Local player'ı döndürür
        /// </summary>
        private GameObject GetLocalPlayer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var playerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (playerObject != null)
                {
                    return playerObject.gameObject;
                }
            }

            // Fallback: Tag ile bul
            var players = GameObject.FindGameObjectsWithTag("Character");
            foreach (var player in players)
            {
                var networkObject = player.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsLocalPlayer)
                {
                    return player;
                }
            }

            return null;
        }

        #endregion

        #region Localization

        private void HandleLocaleChanged(Locale newLocale)
        {
            Debug.Log($"{LOG_PREFIX} Locale changed to:  {newLocale?.Identifier.Code ?? "null"}");
            RefreshUI();
        }

        #endregion

        #region Input Handling

        private void HandleEscapeInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LogDebug("ESC key blocked while Next Day UI is active");
                // ESC menüsü açılmasın
            }
        }

        #endregion

        #region UI State

        /// <summary>
        /// UI'ın aktif olup olmadığını kontrol eder
        /// </summary>
        public bool IsUIActive()
        {
            if (nextDayPanel != null)
            {
                return nextDayPanel.activeInHierarchy;
            }

            return gameObject.activeInHierarchy;
        }

        private void SetupCursor()
        {
            if (IsUIActive())
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        #endregion

        #region Main UI Update

        /// <summary>
        /// Next Day UI'ını günceller
        /// </summary>
        public void UpdateNextDayUI()
        {
            HideAllPlayerElements();

            // Tek doğruluk kaynağı: server-authoritative roster (GameStateManager).
            // Steam lobi Members okuması client/host arasında tutarsız olabiliyordu
            // (persona henüz inmemiş / lobi referansı client'ta boş dönebiliyor).
            var rosterNames = GameStateManager.Instance?.GetRosterPlayerNames();

            if (rosterNames == null || rosterNames.Count == 0)
            {
                ShowLocalPlayer();
                return;
            }

            ShowRosterPlayers(rosterNames);
        }

        private void ShowRosterPlayers(List<string> rosterNames)
        {
            int displayCount = Mathf.Min(rosterNames.Count, playerUIElements.Length);

            for (int i = 0; i < displayCount; i++)
            {
                ShowPlayer(i, rosterNames[i]);
            }

            LogDebug($"Updated UI - Showing {displayCount} players (roster)");
        }

        private void ShowLocalPlayer()
        {
            if (!SteamClient.IsValid || playerUIElements.Length == 0)
            {
                return;
            }

            string localPlayerName = SteamClient.Name ?? defaultPlayerName;
            ShowPlayer(0, localPlayerName);
        }

        #endregion

        #region Player Element Management

        private void ShowPlayer(int index, string playerName)
        {
            if (!IsValidPlayerIndex(index))
            {
                return;
            }

            SetPlayerElementActive(index, true);
            SetPlayerName(index, playerName);
            SetPlayerIconActive(index, true);
        }

        private void HideAllPlayerElements()
        {
            for (int i = 0; i < playerUIElements.Length; i++)
            {
                SetPlayerElementActive(i, false);
            }
        }

        private bool IsValidPlayerIndex(int index)
        {
            return index >= 0 && index < playerUIElements.Length;
        }

        private void SetPlayerElementActive(int index, bool active)
        {
            if (playerUIElements[index] != null)
            {
                playerUIElements[index].SetActive(active);
            }
        }

        private void SetPlayerName(int index, string playerName)
        {
            if (index < playerNameTexts.Length && playerNameTexts[index] != null)
            {
                playerNameTexts[index].text = playerName;
            }
        }

        private void SetPlayerIconActive(int index, bool active)
        {
            if (index < playerIcons.Length && playerIcons[index] != null)
            {
                playerIcons[index].SetActive(active);
            }
        }

        #endregion

        #region Public API - Next Day

        /// <summary>
        /// Next Day butonuna basıldığında çağrılır
        /// </summary>
        public void OnNextDayClicked()
        {
            LogDebug("Next Day clicked!");

            HidePanel();

            // Burada next day logic eklenebilir
            // DayCycleManager.Instance?.StartNextDay();
            // SceneManager.LoadScene("GameScene");
        }

        /// <summary>
        /// UI'ı manuel günceller
        /// </summary>
        public void RefreshUI()
        {
            UpdateNextDayUI();
        }

        /// <summary>
        /// Aktif oyuncu sayısını döndürür
        /// </summary>
        public int GetActivePlayerCount()
        {
            int count = 0;

            for (int i = 0; i < playerUIElements.Length; i++)
            {
                if (playerUIElements[i] != null && playerUIElements[i].activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Paneli gösterir ve hareketi kilitler
        /// </summary>
        public void ShowPanel()
        {
            if (nextDayPanel != null)
            {
                nextDayPanel.SetActive(true);
            }

            UpdateNextDayUI();
            LockPlayerMovement();
            _wasActive = true;

            LogDebug("Panel shown, movement locked");
        }

        /// <summary>
        /// Paneli gizler ve hareketi açar
        /// </summary>
        public void HidePanel()
        {
            if (nextDayPanel != null)
            {
                nextDayPanel.SetActive(false);
            }

            UnlockPlayerMovement();
            _wasActive = false;

            LogDebug("Panel hidden, movement unlocked");
        }

        #endregion

        #region Break Room Player List

        /// <summary>
        /// Break Room için oyuncu listesini UI'da gösterir (TÜM lobby oyuncuları)
        /// </summary>
        /// <param name="playerNames">Lobideki oyuncu isimleri</param>
        public void ShowPlayers(List<string> playerNames)
        {
            if (!ValidatePlayerList(playerNames))
            {
                ShowEmptyPlayerList();
                return;
            }

            string displayText = BuildPlayerListText(playerNames);
            DisplayPlayerList(displayText);

            LogDebug($"✅ Oyuncu listesi güncellendi: {playerNames.Count} oyuncu");
            LogDebug($"Oyuncular: {string.Join(", ", playerNames)}");
        }

        /// <summary>
        /// Break Room'daki oyuncuları işaretler
        /// </summary>
        /// <param name="allPlayers">Tüm lobby oyuncuları</param>
        /// <param name="playersInRoom">Break room'daki oyuncu isimleri</param>
        public void ShowPlayersWithRoomStatus(List<string> allPlayers, List<string> playersInRoom)
        {
            if (!ValidatePlayerList(allPlayers))
            {
                ShowEmptyPlayerList();
                return;
            }

            _playersInBreakRoom = playersInRoom ?? new List<string>();
            string displayText = BuildPlayerListTextWithStatus(allPlayers, _playersInBreakRoom);
            DisplayPlayerList(displayText);

            LogDebug($"✅ Oyuncu listesi güncellendi: {allPlayers.Count} oyuncu, {_playersInBreakRoom.Count} break room'da");
        }

        private bool ValidatePlayerList(List<string> playerNames)
        {
            return playerNames != null && playerNames.Count > 0;
        }

        private void ShowEmptyPlayerList()
        {
            LogWarning("Player list empty!");

            if (playerListDisplay != null)
            {
                playerListDisplay.text = GetLocalizedString(LOC_KEY_NO_PLAYERS);
            }

            SetPlayerListPanelActive(false);
        }

        private string BuildPlayerListText(List<string> playerNames)
        {
            var builder = new System.Text.StringBuilder();

            // Header - localized
            string headerText = GetLocalizedString(LOC_KEY_LOBBY_PLAYERS);
            builder.Append($"🎮 <b>{headerText}</b>\n\n");

            // Player entries
            for (int i = 0; i < playerNames.Count; i++)
            {
                builder.AppendFormat(PLAYER_LIST_ITEM_FORMAT, i + 1, playerNames[i]);
            }

            // Footer - localized
            builder.Append(PLAYER_LIST_SEPARATOR);
            string totalText = GetLocalizedString(LOC_KEY_TOTAL_PLAYERS);
            builder.Append($"\n<b>{string.Format(totalText, playerNames.Count)}</b>");

            return builder.ToString();
        }

        private string BuildPlayerListTextWithStatus(List<string> allPlayers, List<string> playersInRoom)
        {
            var builder = new System.Text.StringBuilder();

            // Header - localized
            string headerText = GetLocalizedString(LOC_KEY_LOBBY_PLAYERS);
            builder.Append($"🎮 <b>{headerText}</b>\n\n");

            // Player entries with status
            for (int i = 0; i < allPlayers.Count; i++)
            {
                string playerName = allPlayers[i];
                bool isInRoom = playersInRoom.Contains(playerName);

                if (isInRoom)
                {
                    builder.AppendFormat(PLAYER_IN_ROOM_FORMAT, i + 1, playerName);
                }
                else
                {
                    builder.AppendFormat(PLAYER_NOT_IN_ROOM_FORMAT, i + 1, playerName);
                }
            }

            // Footer - localized
            builder.Append(PLAYER_LIST_SEPARATOR);
            string totalText = GetLocalizedString(LOC_KEY_TOTAL_PLAYERS);
            builder.Append($"\n<b>{string.Format(totalText, allPlayers.Count)}</b>");

            return builder.ToString();
        }

        private void DisplayPlayerList(string displayText)
        {
            _currentPlayerListText = displayText;

            if (playerListDisplay != null)
            {
                playerListDisplay.text = displayText;
            }
            else
            {
                LogWarning("playerListDisplay atanmamış!  Inspector'dan TextMeshProUGUI ekleyin.");
            }

            SetPlayerListPanelActive(true);
        }

        /// <summary>
        /// Oyuncu listesini gizler
        /// </summary>
        public void HidePlayerList()
        {
            SetPlayerListPanelActive(false);

            if (playerListDisplay != null)
            {
                playerListDisplay.text = string.Empty;
            }

            _currentPlayerListText = string.Empty;
        }

        private void SetPlayerListPanelActive(bool active)
        {
            if (playerListPanel != null)
            {
                playerListPanel.SetActive(active);
            }
        }

        #endregion

        #region Break Room Status

        /// <summary>
        /// Break Room durumunu gösterir
        /// </summary>
        /// <param name="playersInRoom">Break Room'da olan oyuncu sayısı</param>
        /// <param name="requiredPlayers">Gerekli oyuncu sayısı</param>
        public void UpdateBreakRoomStatus(int playersInRoom, int requiredPlayers)
        {
            if (playerListDisplay == null)
            {
                return;
            }

            string statusText = BuildBreakRoomStatusText(playersInRoom, requiredPlayers);
            playerListDisplay.text = _currentPlayerListText + statusText;
        }

        private string BuildBreakRoomStatusText(int playersInRoom, int requiredPlayers)
        {
            var builder = new System.Text.StringBuilder();

            // Separator and header - localized
            builder.Append(BREAK_ROOM_SEPARATOR);
            string statusHeader = GetLocalizedString(LOC_KEY_BREAK_ROOM_STATUS);
            builder.Append($"\n<b>{statusHeader}:</b>");

            // Player count - localized
            string playersInRoomText = GetLocalizedString(LOC_KEY_PLAYERS_IN_ROOM);
            builder.Append($"\n<color=#4CAF50>►</color> {string.Format(playersInRoomText, playersInRoom, requiredPlayers)}");

            // Status message - localized
            if (playersInRoom >= requiredPlayers)
            {
                string readyText = GetLocalizedString(LOC_KEY_EVERYONE_READY);
                builder.Append($"\n<color=#4CAF50>✓ {readyText} 🎉</color>");
            }
            else
            {
                int waiting = requiredPlayers - playersInRoom;
                string waitingText = GetLocalizedString(LOC_KEY_WAITING_PLAYERS);
                builder.Append($"\n<color=#FFC107>⏳ {string.Format(waitingText, waiting)}</color>");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Break Room durumunu temizler
        /// </summary>
        public void ClearBreakRoomStatus()
        {
            if (playerListDisplay != null && !string.IsNullOrEmpty(_currentPlayerListText))
            {
                playerListDisplay.text = _currentPlayerListText;
            }
        }

        #endregion

        #region Localization

        /// <summary>
        /// Gets a localized string from the StringTable
        /// </summary>
        private string GetLocalizedString(string key)
        {
            try
            {
                if (!LocalizationSettings.InitializationOperation.IsDone)
                {
                    // Return key as fallback if localization not ready
                    return key;
                }

                var stringTable = LocalizationSettings.StringDatabase.GetTable(LOC_TABLE);
                if (stringTable != null)
                {
                    var entry = stringTable.GetEntry(key);
                    if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                    {
                        return entry.LocalizedValue;
                    }
                }

                // Return key as fallback
                return key;
            }
            catch (System.Exception e)
            {
                LogWarning($"Localization error for key '{key}':  {e.Message}");
                return key;
            }
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Refresh UI")]
        private void DebugRefreshUI()
        {
            RefreshUI();
        }

        [ContextMenu("Show Panel")]
        private void DebugShowPanel()
        {
            ShowPanel();
        }

        [ContextMenu("Hide Panel")]
        private void DebugHidePanel()
        {
            HidePanel();
        }

        [ContextMenu("Test:  Show 4 Players")]
        private void DebugShow4Players()
        {
            var testPlayers = new List<string> { "Player1", "Player2", "Player3", "Player4" };
            ShowPlayers(testPlayers);
        }

        [ContextMenu("Test: Show Players With Status (2/4 in room)")]
        private void DebugShowPlayersWithStatus()
        {
            var allPlayers = new List<string> { "Player1", "Player2", "Player3", "Player4" };
            var inRoom = new List<string> { "Player1", "Player3" };
            ShowPlayersWithRoomStatus(allPlayers, inRoom);
        }

        [ContextMenu("Test: Update Break Room Status (2/4)")]
        private void DebugUpdateBreakRoomStatus()
        {
            UpdateBreakRoomStatus(2, 4);
        }

        [ContextMenu("Test: Update Break Room Status (4/4)")]
        private void DebugUpdateBreakRoomStatusReady()
        {
            UpdateBreakRoomStatus(4, 4);
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === NEXT DAY UI STATE ===");
            Debug.Log($"Is Active: {IsUIActive()}");
            Debug.Log($"Was Active: {_wasActive}");
            Debug.Log($"Active Player Count: {GetActivePlayerCount()}");
            Debug.Log($"Has Panel: {nextDayPanel != null}");
            Debug.Log($"Player Elements: {playerUIElements?.Length ?? 0}");
            Debug.Log($"Player Texts: {playerNameTexts?.Length ?? 0}");
            Debug.Log($"Player Icons: {playerIcons?.Length ?? 0}");
            Debug.Log($"Has Player List Display: {playerListDisplay != null}");
            Debug.Log($"Current Player List Text Length: {_currentPlayerListText?.Length ?? 0}");
            Debug.Log($"Players In Break Room: {string.Join(", ", _playersInBreakRoom)}");
        }
#endif

        #endregion
    }
}