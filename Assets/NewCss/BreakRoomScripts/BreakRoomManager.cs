using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace NewCss
{
    /// <summary>
    /// Break room yöneticisi - tüm oyuncuların gün sonu için toplanması gereken alanı yönetir.  
    /// Steam lobi entegrasyonu ve oyuncu takibi sağlar.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BreakRoomManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[BreakRoom]";
        private const string CHARACTER_TAG = "Character";

        // Localization Keys
        private const string LOC_KEY_PLAYERS = "Players";
        private const string LOC_KEY_BREAK_ROOM_COUNT = "BreakRoomCount";
        private const string DEFAULT_LOCAL_PLAYER_NAME = "Local Player";
        private const int DEFAULT_REQUIRED_PLAYERS = 1;

        #endregion

        #region Singleton

        public static BreakRoomManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("=== PLAYER SETTINGS ===")]
        [SerializeField, Tooltip("Break room için gerekli oyuncu sayısı")]
        public int requiredPlayers = DEFAULT_REQUIRED_PLAYERS;

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Sonraki gün UI yöneticisi")]
        public NextDayUIManager nextDayUI;

        [SerializeField, Tooltip("Oyuncu listesi text elementi")]
        public Text playerListText;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private readonly HashSet<GameObject> _playersInside = new();
        private List<string> _currentLobbyPlayerNames = new();
        private int _lastCheckedPlayerCount;
        private Collider _triggerCollider;

        #endregion

        #region Events

        /// <summary>
        /// Break room hazır olduğunda tetiklenir
        /// </summary>
        public event Action OnBreakRoomReady;

        /// <summary>
        /// Oyuncu break room'a girdiğinde tetiklenir
        /// </summary>
        public event Action<GameObject> OnPlayerEntered;

        /// <summary>
        /// Oyuncu break room'dan çıktığında tetiklenir
        /// </summary>
        public event Action<GameObject> OnPlayerExited;

        /// <summary>
        /// Oyuncu sayısı değiştiğinde tetiklenir
        /// </summary>
        public event Action<int, int> OnPlayerCountChanged;

        #endregion

        #region Public Properties

        /// <summary>
        /// Break room'daki oyuncu sayısı
        /// </summary>
        public int PlayersInRoomCount => _playersInside.Count;

        /// <summary>
        /// Gerekli oyuncu sayısı
        /// </summary>
        public int RequiredPlayersCount => requiredPlayers;

        /// <summary>
        /// Break room hazır mı? 
        /// </summary>
        public bool IsReady => _playersInside.Count >= requiredPlayers;

        /// <summary>
        /// Lobideki oyuncu isimleri
        /// </summary>
        public IReadOnlyList<string> LobbyPlayerNames => _currentLobbyPlayerNames;

        /// <summary>
        /// Break room'daki oyuncular
        /// </summary>
        public IReadOnlyCollection<GameObject> PlayersInRoom => _playersInside;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (!InitializeSingleton())
            {
                return;
            }

            SetupTriggerCollider();
        }

        private void Start()
        {
            FindNextDayUIManager();
            CheckAndUpdateLobbyPlayers();
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        private void OnDestroy()
        {
            CleanupSingleton();
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private void HandleLocaleChanged(Locale newLocale)
        {
            LogDebug($"Locale changed to: {newLocale?.Identifier.Code ?? "null"}");
            UpdateBreakRoomUI();
        }

        #endregion

        #region Singleton Management

        private bool InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
                return true;
            }

            if (Instance != this)
            {
                LogWarning("Duplicate instance detected, destroying.. .");
                Destroy(gameObject);
                return false;
            }

            return true;
        }

        private void CleanupSingleton()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        private void SetupTriggerCollider()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
        }

        private void FindNextDayUIManager()
        {
            if (nextDayUI == null)
            {
                nextDayUI = FindObjectOfType<NextDayUIManager>();
            }
        }

        #endregion

        #region Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(CHARACTER_TAG))
            {
                return;
            }

            if (_playersInside.Add(other.gameObject))
            {
                LogDebug($"Player entered break room.  Count: {_playersInside.Count}/{requiredPlayers}");

                OnPlayerEntered?.Invoke(other.gameObject);
                OnPlayerCountChanged?.Invoke(_playersInside.Count, requiredPlayers);

                CheckIfAllPlayersPresent();
                UpdateBreakRoomUI();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(CHARACTER_TAG))
            {
                return;
            }

            if (_playersInside.Remove(other.gameObject))
            {
                LogDebug($"Player exited break room.  Count: {_playersInside.Count}/{requiredPlayers}");

                OnPlayerExited?.Invoke(other.gameObject);
                OnPlayerCountChanged?.Invoke(_playersInside.Count, requiredPlayers);

                UpdateBreakRoomUI();
            }
        }

        #endregion

        #region Player Presence Check

        private void CheckIfAllPlayersPresent()
        {
            var dayCycleManager = DayCycleManager.Instance;
            if (dayCycleManager == null)
            {
                return;
            }

            CheckAndUpdateLobbyPlayers();

            if (dayCycleManager.IsTimeUp && IsReady)
            {
                dayCycleManager.isBreakRoomReady = true;

                LogDebug("All players present!  Break room ready.");

                OnBreakRoomReady?.Invoke();
                RefreshNextDayUI();
            }
        }

        #endregion

        #region Steam Lobby Integration

        /// <summary>
        /// Steam lobisinden güncel oyuncu sayısını ve isimlerini günceller
        /// </summary>
        public void CheckAndUpdateLobbyPlayers()
        {
            int currentPlayerCount = GetSteamLobbyPlayerCount();
            List<string> playerNames = GetSteamLobbyPlayerNames();

            if (currentPlayerCount != _lastCheckedPlayerCount)
            {
                LogDebug($"Player count changed: {_lastCheckedPlayerCount} -> {currentPlayerCount}");
                _lastCheckedPlayerCount = currentPlayerCount;
            }

            UpdateLobbyPlayers(playerNames);
        }

        /// <summary>
        /// Oyuncu listesini ve gerekli oyuncu sayısını günceller
        /// </summary>
        public void UpdateLobbyPlayers(List<string> lobbyPlayerNames)
        {
            if (lobbyPlayerNames == null || lobbyPlayerNames.Count == 0)
            {
                LogWarning("Player list is empty!");
                _currentLobbyPlayerNames = new List<string> { DEFAULT_LOCAL_PLAYER_NAME };
                requiredPlayers = DEFAULT_REQUIRED_PLAYERS;
            }
            else
            {
                _currentLobbyPlayerNames = new List<string>(lobbyPlayerNames);
                requiredPlayers = lobbyPlayerNames.Count;
            }

            LogDebug($"Updated: {requiredPlayers} players required");
            UpdateBreakRoomUI();
        }

        /// <summary>
        /// Steam lobisindeki oyuncu sayısını döndürür
        /// </summary>
        public int GetSteamLobbyPlayerCount()
        {
            if (!HasValidLobby())
            {
                return DEFAULT_REQUIRED_PLAYERS;
            }

            try
            {
                var lobby = LobbySaver.instance.CurrentLobby.Value;
                var members = lobby.Members?.ToArray();

                if (members != null && members.Length > 0)
                {
                    return members.Length;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error getting lobby player count: {ex.Message}");
            }

            return DEFAULT_REQUIRED_PLAYERS;
        }

        /// <summary>
        /// Steam lobisindeki oyuncu isimlerini döndürür
        /// </summary>
        public List<string> GetSteamLobbyPlayerNames()
        {
            var playerNames = new List<string>();

            if (!HasValidLobby())
            {
                playerNames.Add(DEFAULT_LOCAL_PLAYER_NAME);
                return playerNames;
            }

            try
            {
                var lobby = LobbySaver.instance.CurrentLobby.Value;
                var members = lobby.Members?.ToArray();

                if (members != null && members.Length > 0)
                {
                    foreach (var member in members)
                    {
                        playerNames.Add(member.Name);
                    }
                    return playerNames;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error getting lobby player names: {ex.Message}");
            }

            playerNames.Add(DEFAULT_LOCAL_PLAYER_NAME);
            return playerNames;
        }

        /// <summary>
        /// Geçerli bir lobi var mı kontrol eder
        /// </summary>
        private bool HasValidLobby()
        {
            return LobbySaver.instance != null &&
                   LobbySaver.instance.CurrentLobby.HasValue;
        }

        #endregion

        #region UI Update

        private void UpdateBreakRoomUI()
        {
            UpdateNextDayUIPlayers();
            UpdatePlayerListText();
        }

        private void UpdateNextDayUIPlayers()
        {
            if (nextDayUI != null)
            {
                nextDayUI.ShowPlayers(_currentLobbyPlayerNames);
            }
        }

        private void UpdatePlayerListText()
        {
            if (playerListText == null)
            {
                return;
            }

            string playerList = FormatPlayerList();
            string countTemplate = LocalizationHelper.GetLocalizedString(LOC_KEY_BREAK_ROOM_COUNT);
            string countText;
            try
            {
                countText = string.Format(countTemplate, _playersInside.Count, requiredPlayers);
            }
            catch
            {
                countText = $"({_playersInside.Count}/{requiredPlayers} in Break Room)";
            }

            playerListText.text = $"{playerList}\n\n{countText}";
        }

        private string FormatPlayerList()
        {
            string playersLabel = LocalizationHelper.GetLocalizedString(LOC_KEY_PLAYERS);
            return playersLabel + ":\n" + string.Join("\n", _currentLobbyPlayerNames);
        }

        private void RefreshNextDayUI()
        {
            if (nextDayUI != null)
            {
                nextDayUI.RefreshUI();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Break room'da kaç kişinin olduğunu döndürür
        /// </summary>
        public int GetPlayersInRoomCount()
        {
            return PlayersInRoomCount;
        }

        /// <summary>
        /// Break room'un hazır olup olmadığını kontrol eder
        /// </summary>
        public bool IsBreakRoomReady()
        {
            return IsReady;
        }

        /// <summary>
        /// Belirli bir oyuncunun break room'da olup olmadığını kontrol eder
        /// </summary>
        public bool IsPlayerInRoom(GameObject player)
        {
            return _playersInside.Contains(player);
        }

        /// <summary>
        /// Tüm oyuncuları break room'dan temizler
        /// </summary>
        public void ClearAllPlayers()
        {
            _playersInside.Clear();
            UpdateBreakRoomUI();

            LogDebug("All players cleared from break room");
        }

        /// <summary>
        /// Gerekli oyuncu sayısını manuel olarak ayarlar
        /// </summary>
        public void SetRequiredPlayers(int count)
        {
            requiredPlayers = Mathf.Max(1, count);

            LogDebug($"Required players set to: {requiredPlayers}");

            UpdateBreakRoomUI();
        }

        /// <summary>
        /// Break room durumunu zorla günceller
        /// </summary>
        public void ForceRefresh()
        {
            CheckAndUpdateLobbyPlayers();
            CheckIfAllPlayersPresent();
            UpdateBreakRoomUI();
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Force Refresh")]
        private void DebugForceRefresh()
        {
            ForceRefresh();
        }

        [ContextMenu("Clear All Players")]
        private void DebugClearAllPlayers()
        {
            ClearAllPlayers();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === BREAK ROOM STATE ===");
            Debug.Log($"Players Inside: {_playersInside.Count}");
            Debug.Log($"Required Players: {requiredPlayers}");
            Debug.Log($"Is Ready: {IsReady}");
            Debug.Log($"Last Checked Count: {_lastCheckedPlayerCount}");
            Debug.Log($"Lobby Player Names: {string.Join(", ", _currentLobbyPlayerNames)}");

            Debug.Log($"--- Players In Room ---");
            foreach (var player in _playersInside)
            {
                Debug.Log($"  - {player.name}");
            }
        }

        [ContextMenu("Simulate All Players Present")]
        private void DebugSimulateAllPresent()
        {
            var dayCycleManager = DayCycleManager.Instance;
            if (dayCycleManager != null)
            {
                dayCycleManager.isBreakRoomReady = true;
                RefreshNextDayUI();
                LogDebug("Simulated all players present");
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawTriggerGizmo();
            DrawStatusGizmo();
        }

        private void DrawTriggerGizmo()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = IsReady
                ? new Color(0f, 1f, 0f, 0.3f)
                : new Color(1f, 1f, 0f, 0.3f);

            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }

        private void DrawStatusGizmo()
        {
            Vector3 labelPos = transform.position + Vector3.up * 3f;
            string label = $"Break Room\n{_playersInside.Count}/{requiredPlayers}";

            UnityEditor.Handles.Label(labelPos, label);
        }
#endif

        #endregion
    }
}