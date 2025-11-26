using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    [RequireComponent(typeof(Collider))]
    public class BreakRoomManager : NetworkBehaviour
    {
        public static BreakRoomManager Instance { get; private set; }

        [Tooltip("Number of players required to enter the break room")]
        public int requiredPlayers = 1;

        [Header("UI Reference")]
        public NextDayUIManager nextDayUI;

        [Header("Player Info Display")]
        public UnityEngine.UI.Text playerListText; // UI Text element to show player names

        private readonly HashSet<GameObject> playersInside = new HashSet<GameObject>();
        private List<string> currentLobbyPlayerNames = new List<string>();
        private int lastCheckedPlayerCount = 0;

        void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void Start()
        {
            // Next Day UI Manager'ı bul
            if (nextDayUI == null)
            {
                nextDayUI = FindObjectOfType<NextDayUIManager>();
            }

            // İlk oyuncu sayısını al
            CheckAndUpdateLobbyPlayers();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Character")) return;
            playersInside.Add(other.gameObject);
            CheckIfAllPlayersPresent();
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Character")) return;
            playersInside.Remove(other.gameObject);
        }

        private void CheckIfAllPlayersPresent()
        {
            var day = DayCycleManager.Instance;
            if (day == null) return;

            // Oyuncu sayısını güncelle
            CheckAndUpdateLobbyPlayers();

            if (day.IsTimeUp && playersInside.Count >= requiredPlayers)
            {
                day.isBreakRoomReady = true;

                // Next Day UI'ını güncelle
                if (nextDayUI != null)
                {
                    nextDayUI.RefreshUI();
                }
            }
        }

        /// <summary>
        /// Steam lobisinden güncel oyuncu sayısını ve isimlerini kontrol eder
        /// Gün başı, ortası ve sonunda çağrılmalı
        /// </summary>
        public void CheckAndUpdateLobbyPlayers()
        {
            int currentPlayerCount = GetSteamLobbyPlayerCount();
            List<string> playerNames = GetSteamLobbyPlayerNames();

            // Oyuncu sayısı değişti mi kontrol et
            if (currentPlayerCount != lastCheckedPlayerCount)
            {
                Debug.Log($"[BreakRoom] Oyuncu sayısı değişti: {lastCheckedPlayerCount} -> {currentPlayerCount}");
                lastCheckedPlayerCount = currentPlayerCount;
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
                Debug.LogWarning("[BreakRoom] Oyuncu listesi boş!");
                requiredPlayers = 1;
                currentLobbyPlayerNames = new List<string> { "Local Player" };
            }
            else
            {
                currentLobbyPlayerNames = lobbyPlayerNames;
                requiredPlayers = lobbyPlayerNames.Count;
            }

            Debug.Log($"[BreakRoom] Güncellendi: {requiredPlayers} oyuncu gerekli");

            // UI'yı güncelle
            UpdateBreakRoomUI();
        }

        /// <summary>
        /// Break room UI'ını günceller - oyuncu isimlerini gösterir
        /// </summary>
        private void UpdateBreakRoomUI()
        {
            // NextDayUI'da oyuncu isimlerini göster
            if (nextDayUI != null)
            {
                nextDayUI.ShowPlayers(currentLobbyPlayerNames);
            }

            // Eğer ayrı bir Text UI elementi varsa orada da göster
            if (playerListText != null)
            {
                string playerList = "Oyuncular:\n" + string.Join("\n", currentLobbyPlayerNames);
                playerListText.text = $"{playerList}\n\n({playersInside.Count}/{requiredPlayers} Break Room'da)";
            }
        }

        /// <summary>
        /// Steam lobisindeki oyuncu sayısını döndürür
        /// </summary>
        public int GetSteamLobbyPlayerCount()
        {
            // Steam lobisinden oyuncu sayısını al
            if (LobbySaver.instance != null && LobbySaver.instance.CurrentLobby.HasValue)
            {
                try
                {
                    var lobby = LobbySaver.instance.CurrentLobby.Value;
                    var members = lobby.Members?.ToArray();
                    if (members != null && members.Length > 0)
                    {
                        return members.Length;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BreakRoom] Lobi üyelerini alırken hata: {ex.Message}");
                }
            }

            // Steam lobisi yoksa local player
            return 1;
        }

        /// <summary>
        /// Steam lobisindeki oyuncu isimlerini döndürür
        /// </summary>
        public List<string> GetSteamLobbyPlayerNames()
        {
            List<string> playerNames = new List<string>();

            if (LobbySaver.instance != null && LobbySaver.instance.CurrentLobby.HasValue)
            {
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
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BreakRoom] Oyuncu isimlerini alırken hata: {ex.Message}");
                }
            }

            // Steam lobisi yoksa local player ekle
            playerNames.Add("Local Player");
            return playerNames;
        }

        /// <summary>
        /// Break room'da kaç kişinin olduğunu döndürür
        /// </summary>
        public int GetPlayersInRoomCount()
        {
            return playersInside.Count;
        }

        /// <summary>
        /// Break room'un hazır olup olmadığını kontrol eder
        /// </summary>
        public bool IsBreakRoomReady()
        {
            return playersInside.Count >= requiredPlayers;
        }
    }
}