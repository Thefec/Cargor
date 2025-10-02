using System.Collections.Generic;
using UnityEngine;

namespace NewCss
{
    [RequireComponent(typeof(Collider))]
    public class BreakRoomManager : MonoBehaviour
    {
        [Tooltip("Number of players required to enter the break room")]
        public int requiredPlayers = 1;

        [Header("UI Reference")]
        public NextDayUIManager nextDayUI;

        private readonly HashSet<GameObject> playersInside = new HashSet<GameObject>();

        void Awake()
        {
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

            // Lobby'deki oyuncu sayısını kontrol et
            int lobbyPlayerCount = GetLobbyPlayerCount();
            int actualRequiredPlayers = Mathf.Max(requiredPlayers, lobbyPlayerCount);

            if (day.IsTimeUp && playersInside.Count >= actualRequiredPlayers)
            {
                day.isBreakRoomReady = true;
                
                // Next Day UI'ını güncelle
                if (nextDayUI != null)
                {
                    nextDayUI.RefreshUI();
                }
            }
        }

        private int GetLobbyPlayerCount()
        {
            // LobbySaver'dan lobby bilgisini al
            if (LobbySaver.instance != null && LobbySaver.instance.CurrentLobby.HasValue)
            {
                return LobbySaver.instance.CurrentLobby.Value.MemberCount;
            }
            
            // Lobby yoksa 1 oyuncu (local player)
            return 1;
        }

        // Debug metodları
        public int GetPlayersInside()
        {
            return playersInside.Count;
        }

        public int GetLobbyMemberCount()
        {
            return GetLobbyPlayerCount();
        }
    }
}