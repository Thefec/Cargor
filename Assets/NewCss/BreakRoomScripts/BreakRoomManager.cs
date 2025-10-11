using System.Collections.Generic;
using System.Linq;
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

            // Steam lobisindeki oyuncu sayısını güncelle
            requiredPlayers = GetSteamLobbyPlayerCount();

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

        public int GetSteamLobbyPlayerCount()
        {
            // Steam lobisinden oyuncu sayısını al
            if (LobbySaver.instance != null && LobbySaver.instance.CurrentLobby.HasValue)
            {
                var lobby = LobbySaver.instance.CurrentLobby.Value;
                return lobby.Members.Count();
            }
            // Steam lobisi yoksa local player
            return 1;
        }

        
    }
}