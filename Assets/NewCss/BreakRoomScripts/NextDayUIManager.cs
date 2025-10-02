using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using Steamworks.Data;

namespace NewCss
{
    public class NextDayUIManager : MonoBehaviour
    {
        [Header("Next Day UI Elements")]
        public GameObject[] playerUIElements; // Player1, Player2, Player3, Player4 UI elementleri
        public TextMeshProUGUI[] playerNameTexts; // Her oyuncu için isim text'leri
        public GameObject[] playerIcons; // Oyuncu ikonları (yeşil adamcıklar)
        
        [Header("Settings")]
        public string defaultPlayerName = "Player";

        void Start()
        {
            // Başlangıçta UI'ı güncelle
            UpdateNextDayUI();
        }

        void OnEnable()
        {
            // UI aktif olduğunda güncelle
            UpdateNextDayUI();
        }

        public void UpdateNextDayUI()
        {
            // Önce tüm UI elementlerini gizle
            HideAllPlayerElements();

            // Mevcut lobby'i al
            Lobby? currentLobby = GetCurrentLobby();
            if (!currentLobby.HasValue)
            {
                // Lobby yoksa sadece local oyuncuyu göster
                ShowLocalPlayer();
                return;
            }

            // Lobby üyelerini al ve göster
            var members = currentLobby.Value.Members.ToArray();
            for (int i = 0; i < members.Length && i < playerUIElements.Length; i++)
            {
                ShowPlayer(i, members[i].Name);
            }
        }

        private Lobby? GetCurrentLobby()
        {
            // LobbySaver'dan lobby bilgisini al
            if (LobbySaver.instance != null && LobbySaver.instance.CurrentLobby.HasValue)
            {
                return LobbySaver.instance.CurrentLobby.Value;
            }

            return null;
        }

        private void ShowLocalPlayer()
        {
            // Sadece local oyuncuyu göster
            if (SteamClient.IsValid && playerUIElements.Length > 0)
            {
                string localPlayerName = SteamClient.Name ?? defaultPlayerName;
                ShowPlayer(0, localPlayerName);
            }
        }

        private void ShowPlayer(int index, string playerName)
        {
            if (index >= 0 && index < playerUIElements.Length)
            {
                // UI elementini aktif et
                if (playerUIElements[index] != null)
                {
                    playerUIElements[index].SetActive(true);
                }

                // İsmi güncelle
                if (playerNameTexts[index] != null)
                {
                    playerNameTexts[index].text = playerName;
                }

                // İkonu göster
                if (playerIcons[index] != null)
                {
                    playerIcons[index].SetActive(true);
                }
            }
        }

        private void HideAllPlayerElements()
        {
            for (int i = 0; i < playerUIElements.Length; i++)
            {
                if (playerUIElements[i] != null)
                {
                    playerUIElements[i].SetActive(false);
                }
            }
        }

        // Next Day butonuna basıldığında çağır
        public void OnNextDayClicked()
        {
            // Burada next day logic'inizi ekleyin
            Debug.Log("Next Day clicked!");
            
            // Örnek: Oyun sahnesine geç
            // SceneManager.LoadScene("GameScene");
        }

        // Dışarıdan çağrılabilir - UI'ı manuel güncelle
        public void RefreshUI()
        {
            UpdateNextDayUI();
        }

        // Oyuncu sayısını döndür (debug için)
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
    }
}