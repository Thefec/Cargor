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
        public GameObject nextDayPanel; // Ana panel referansı - INSPECTOR'DAN ATANMALI
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

            // Mouse kontrolü
            SetupCursor();
        }

        void Update()
        {
            // Next Day UI aktifken ESC tuşunu engelle
            if (IsUIActive())
            {
                // Mouse'u her frame güncelle
                SetupCursor();

                // ESC tuşunu yakala ve devre dışı bırak
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("[NextDayUI] ESC key blocked while Next Day UI is active");
                    // Hiçbir şey yapma - ESC menüsü açılmasın
                }
            }
        }

        /// <summary>
        /// UI'ın aktif olup olmadığını kontrol eder
        /// </summary>
        public bool IsUIActive()
        {
            // nextDayPanel aktif mi kontrol et
            if (nextDayPanel != null)
            {
                return nextDayPanel.activeInHierarchy;
            }

            // Panel referansı yoksa gameObject'in kendisine bak
            return gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Mouse cursor ayarlarını yapar
        /// </summary>
        private void SetupCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
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

            Debug.Log($"[NextDayUI] Updated UI - Showing {members.Length} players");
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

            // UI'ı kapat
            if (nextDayPanel != null)
            {
                nextDayPanel.SetActive(false);
            }

            // Mouse'u geri kilitle (FPS oyunu ise)
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

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

        void OnDisable()
        {
            // UI kapandığında mouse'u geri kilitle
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}