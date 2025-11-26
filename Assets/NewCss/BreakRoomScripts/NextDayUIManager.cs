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

        [Header("Player List Display - BREAK ROOM")]
        [Tooltip("Break Room için oyuncu listesini gösteren Text elementi")]
        public TextMeshProUGUI playerListDisplay; // Break Room oyuncu listesi
        [Tooltip("Oyuncu listesinin gösterileceği panel (opsiyonel)")]
        public GameObject playerListPanel; // Oyuncu listesi paneli (opsiyonel)

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

        // ============================================
        // BREAK ROOM İÇİN OYUNCU LİSTESİ FONKSİYONU
        // ============================================

        /// <summary>
        /// Break Room için oyuncu listesini UI'da gösterir
        /// BreakRoomManager tarafından çağrılır
        /// </summary>
        /// <param name="playerNames">Lobideki oyuncu isimleri</param>
        public void ShowPlayers(List<string> playerNames)
        {
            if (playerNames == null || playerNames.Count == 0)
            {
                Debug.LogWarning("[NextDayUI] Oyuncu listesi boş!");

                if (playerListDisplay != null)
                {
                    playerListDisplay.text = "❌ Oyuncu bulunamadı";
                }

                // Panel varsa gizle
                if (playerListPanel != null)
                {
                    playerListPanel.SetActive(false);
                }

                return;
            }

            // Oyuncu listesini oluştur
            string displayText = "🎮 <b>Lobideki Oyuncular</b>\n\n";

            for (int i = 0; i < playerNames.Count; i++)
            {
                // Her oyuncu için numara ve isim
                displayText += $"<color=#4CAF50>►</color> <b>{i + 1}.</b> {playerNames[i]}\n";
            }

            displayText += $"\n<color=#FFC107>━━━━━━━━━━━━━━━━━</color>";
            displayText += $"\n<b>Toplam:</b> <color=#2196F3>{playerNames.Count}</color> oyuncu";

            // UI'da göster
            if (playerListDisplay != null)
            {
                playerListDisplay.text = displayText;
            }
            else
            {
                Debug.LogWarning("[NextDayUI] playerListDisplay atanmamış! Inspector'dan TextMeshProUGUI ekleyin.");
            }

            // Panel varsa aktif et
            if (playerListPanel != null)
            {
                playerListPanel.SetActive(true);
            }

            Debug.Log($"[NextDayUI] ✅ Oyuncu listesi güncellendi: {playerNames.Count} oyuncu");

            // Debug için oyuncu isimlerini de logla
            string debugList = string.Join(", ", playerNames);
            Debug.Log($"[NextDayUI] Oyuncular: {debugList}");
        }

        /// <summary>
        /// Oyuncu listesini gizler
        /// </summary>
        public void HidePlayerList()
        {
            if (playerListPanel != null)
            {
                playerListPanel.SetActive(false);
            }

            if (playerListDisplay != null)
            {
                playerListDisplay.text = "";
            }
        }

        /// <summary>
        /// Break Room durumunu gösterir (kaç kişi içerde)
        /// </summary>
        /// <param name="playersInRoom">Break Room'da olan oyuncu sayısı</param>
        /// <param name="requiredPlayers">Gerekli oyuncu sayısı</param>
        public void UpdateBreakRoomStatus(int playersInRoom, int requiredPlayers)
        {
            if (playerListDisplay == null) return;

            string statusText = playerListDisplay.text;

            // Mevcut metne durum bilgisi ekle
            statusText += $"\n\n<color=#FF5722>━━━━━━━━━━━━━━━━━</color>";
            statusText += $"\n<b>Break Room Durumu:</b>";
            statusText += $"\n<color=#4CAF50>►</color> İçeride: <b>{playersInRoom}</b> / <b>{requiredPlayers}</b> oyuncu";

            // Eğer herkes içerdeyse
            if (playersInRoom >= requiredPlayers)
            {
                statusText += $"\n<color=#4CAF50>✓ Herkes hazır! 🎉</color>";
            }
            else
            {
                int waiting = requiredPlayers - playersInRoom;
                statusText += $"\n<color=#FFC107>⏳ {waiting} oyuncu bekleniyor...</color>";
            }

            playerListDisplay.text = statusText;
        }
    }
}