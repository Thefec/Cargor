using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.SceneManagement;
using Steamworks;
using TMPro;

namespace NewCss
{
    public class GameStateManager : NetworkBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        [Header("Win/Lose Settings")]

        [Header("UI Panels - Assign your existing panels")]
        public GameObject winPanel;
        public GameObject losePanel;

        [Header("Exit Buttons - Assign your existing buttons")]
        public Button winExitButton;
        public Button loseExitButton;

        [Header("End Game Player Roster (opsiyonel)")]
        [SerializeField, Tooltip("Win panelinde oyuncu listesini gösterecek TMP metni (atanmazsa gösterilmez)")]
        private TextMeshProUGUI winPlayerListText;
        [SerializeField, Tooltip("Lose panelinde oyuncu listesini gösterecek TMP metni (atanmazsa gösterilmez)")]
        private TextMeshProUGUI losePlayerListText;

        #region Player Roster (server-authoritative)

        private const int ROSTER_NAME_MAX_UTF8_BYTES = 60;
        private const string ROSTER_FALLBACK_NAME = "Player";

        /// <summary>
        /// Server-authoritative oyuncu roster'ı. Break Room ve End Game UI'ları
        /// TEK bu kaynaktan oyuncu adı/sayısı okumalı — Steam lobi Members okuması
        /// client/host arasında tutarsız (persona henüz inmemiş olabilir, lobi
        /// referansı client'ta güvenilir dolmayabilir).
        /// </summary>
        private readonly NetworkList<PlayerRosterEntry> _playerRoster = new NetworkList<PlayerRosterEntry>();

        public event Action OnRosterChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _playerRoster.OnListChanged += HandleRosterListChanged;

            if (IsServer)
            {
                // Yeni spawn = yeni oturum; önceki oturumdan sızmış roster girdisi olmasın.
                // Bu, roster temizliğinin TEK deterministik noktası: senkron olarak burada
                // çalışır, hemen altındaki IsClient bloğundaki SubmitLocalNameServerRpc
                // (host için bile) ancak bir sonraki network tick'inde işlenir - yani Clear
                // her zaman submit'ten önce garantili biter.
                ClearPlayerRoster();
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            if (IsClient)
            {
                SubmitLocalNameServerRpc(BuildLocalRosterName());
            }

            _gameEndState.OnValueChanged += HandleGameEndStateChanged;

            // Late-join / geç subscribe güvenliği: OnValueChanged yalnızca DEĞİŞİKLİKTE
            // tetiklenir, spawn anındaki mevcut değeri kapsamaz. Oyun zaten bitmiş
            // durumdaysa (örn. geç katılan bir client) ekranı burada uygula.
            if (_gameEndState.Value != GameEndState.None)
            {
                ApplyGameEndState(_gameEndState.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _playerRoster.OnListChanged -= HandleRosterListChanged;
            _gameEndState.OnValueChanged -= HandleGameEndStateChanged;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }

        private void HandleGameEndStateChanged(GameEndState previous, GameEndState current)
        {
            ApplyGameEndState(current);
        }

        private void ApplyGameEndState(GameEndState state)
        {
            switch (state)
            {
                case GameEndState.Won:
                    gameEnded = true;
                    playerWon = true;
                    Time.timeScale = 0f;
                    ShowWinScreen();
                    break;
                case GameEndState.Lost:
                    gameEnded = true;
                    playerWon = false;
                    Time.timeScale = 0f;
                    ShowLoseScreen();
                    break;
                case GameEndState.None:
                    // Server ResetGameState() ile _gameEndState.Value'yu None'a çekince
                    // NetworkVariable.OnValueChanged TÜM client'larda (host dahil) tetiklenir.
                    // Bu case olmadan uzak client'larda win/lose paneli ve gameEnded/timeScale
                    // eski haliyle sızıyordu (F6). ResetGameState()'teki davranışla aynı.
                    gameEnded = false;
                    playerWon = false;
                    Time.timeScale = 1f;
                    HidePanelsOnly();
                    break;
            }
        }

        private void HandleRosterListChanged(NetworkListEvent<PlayerRosterEntry> _)
        {
            OnRosterChanged?.Invoke();
        }

        private string BuildLocalRosterName()
        {
            string raw = SteamClient.IsValid ? SteamClient.Name : null;
            return string.IsNullOrWhiteSpace(raw) ? ROSTER_FALLBACK_NAME : raw;
        }

        private static FixedString64Bytes ToRosterFixedName(string raw)
        {
            string safe = string.IsNullOrWhiteSpace(raw) ? ROSTER_FALLBACK_NAME : raw;

            while (Encoding.UTF8.GetByteCount(safe) > ROSTER_NAME_MAX_UTF8_BYTES && safe.Length > 0)
            {
                int newLength = safe.Length - 1;

                // Kesim noktası bir surrogate pair'i ortadan bölmesin: kesimden sonra
                // son kalan karakter lone high surrogate olacaksa (düşük eşi az önce
                // atıldıysa) çifti birlikte at.
                if (newLength > 0 && char.IsHighSurrogate(safe[newLength - 1]))
                {
                    newLength--;
                }

                safe = safe.Substring(0, newLength);
            }

            if (safe.Length == 0)
            {
                safe = ROSTER_FALLBACK_NAME;
            }

            try
            {
                return new FixedString64Bytes(safe);
            }
            catch (Exception ex)
            {
                // Beklenmedik bir kod noktası ctor'da patlarsa oyuncu roster'dan
                // tamamen düşmesin - güvenli fallback isme dön.
                Debug.LogWarning($"ToRosterFixedName: FixedString64Bytes ctor failed for '{safe}', falling back. {ex.Message}");
                return new FixedString64Bytes(ROSTER_FALLBACK_NAME);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitLocalNameServerRpc(string rawName, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            FixedString64Bytes fixedName = ToRosterFixedName(rawName);

            for (int i = 0; i < _playerRoster.Count; i++)
            {
                if (_playerRoster[i].ClientId == senderClientId)
                {
                    // Idempotent: aynı client tekrar bildirirse (reconnect) günceller, çoğaltmaz.
                    if (!_playerRoster[i].Name.Equals(fixedName))
                    {
                        _playerRoster[i] = new PlayerRosterEntry { ClientId = senderClientId, Name = fixedName };
                    }
                    return;
                }
            }

            _playerRoster.Add(new PlayerRosterEntry { ClientId = senderClientId, Name = fixedName });
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            for (int i = _playerRoster.Count - 1; i >= 0; i--)
            {
                if (_playerRoster[i].ClientId == clientId)
                {
                    _playerRoster.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Yeni bir oyun/lobi oturumu başlarken roster'ı temizler. Yalnızca server çağırmalı
        /// (idempotent, client'ta no-op).
        /// </summary>
        public void ClearPlayerRoster()
        {
            // NetworkObject henüz spawn olmadıysa IsServer güvenilir değil (InitializeForCurrentScene
            // NetworkSpawn'dan önce de tetiklenebilir) - spawn olmamışsa sessizce çık.
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            _playerRoster.Clear();
        }

        /// <summary>
        /// Roster'daki oyuncu isimlerini döndürür (server-authoritative, tüm makinelerde tutarlı).
        /// </summary>
        public List<string> GetRosterPlayerNames()
        {
            var names = new List<string>(_playerRoster.Count);
            foreach (var entry in _playerRoster)
            {
                names.Add(entry.Name.ToString());
            }
            return names;
        }

        public int RosterPlayerCount => _playerRoster.Count;

        #endregion

        [Header("Economy Settings")]
        [SerializeField, Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        private GameEconomySettings economySettings;

        private bool gameEnded = false;
        private bool playerWon = false;
        private bool hasGameEverStarted = false;

        /// <summary>
        /// Server-authoritative oyun sonu durumu (Win/Lose/None). ClientRpc yerine
        /// NetworkVariable kullanılır ki state, spawn/late-join zamanlamasından
        /// bağımsız olarak her makineye güvenilir şekilde replike olsun — fire-and-forget
        /// bir RPC'nin aksine, bu değer NetworkObject'in normal state senkronizasyonunun
        /// bir parçasıdır.
        /// </summary>
        private enum GameEndState : byte { None = 0, Won = 1, Lost = 2 }

        private readonly NetworkVariable<GameEndState> _gameEndState = new NetworkVariable<GameEndState>(
            GameEndState.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        
        public bool HasGameEverStarted => hasGameEverStarted;

        /// <summary>
        /// Oyunun (harita/gameplay) gerçekten başladığını server-authoritative olarak işaretler.
        /// Çağıran taraf (SteamManager), oyun sahnesi tam yüklendikten SONRA çağırmalı —
        /// erken çağrılırsa DifficultyManager.ApplyMoneySettings başlangıç parasını
        /// atlayabilir (bkz. DifficultyManager.ApplyMoneySettings). Idempotent.
        /// </summary>
        public void MarkGameStarted()
        {
            hasGameEverStarted = true;
        }

        /// <summary>
        /// Yeni bir host oturumu (StartHost) başlarken hasGameEverStarted'ı run-scoped
        /// sıfırlar. GameStateManager DontDestroyOnLoad olduğundan bu bayrak normalde
        /// resetlenmez; aynı process içinde menüye dönüp ikinci bir oyun başlatılırsa
        /// eski true değeri kalır ve ApplyMoneySettings/IsFirstGameLoad yanlış davranır.
        /// SteamManager.ExecuteHostStart başında (LateJoinGuard.ResetGuard ile birlikte)
        /// çağrılmalı.
        /// </summary>
        public void ResetGameStartedFlag()
        {
            hasGameEverStarted = false;
        }
        
        // Scene management
        private string currentGameScene = "";
        private bool isReturningFromMenu = false;

        // Public property for checking game state
        public bool IsDayOver => gameEnded;

        /// <summary>
        /// Oyunun (win ya da lose ile) bitip bitmediği. DayCycleManager.NextDay() (F7) bu
        /// bayrağı CheckWinCondition() sonrası okuyup zafer sonrası "Day 17"ye sızmayı önler.
        /// IsDayOver ile aynı alanı okur ama isim DayCycleManager.IsDayOver (gün-bitti, farklı
        /// anlam) ile karışmasın diye ayrı bir isimle eklendi.
        /// </summary>
        public bool GameEnded => gameEnded;

        void Awake()
        {
            Debug.Log("GameStateManager Awake called");

            if (economySettings == null)
            {
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }
            
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple GameStateManager instances found! Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("GameStateManager Instance set successfully");
                
                // Scene change listener'ını ekle
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        void Start()
        {
            InitializeForCurrentScene();
        }

        void OnDestroy()
        {
            // Listener'ı kaldır
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            _playerRoster.Dispose();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"Scene loaded: {scene.name}");
            
            // Eğer MainMenu'den oyun scene'ine geçtiyse
            if (scene.name != "MainMenu" && currentGameScene != scene.name)
            {
                currentGameScene = scene.name;
                isReturningFromMenu = true;
                Debug.Log("New game scene detected, will reset state");
            }
            
            // Yeni sahne yüklendiğinde bir frame bekle sonra initialize et
            StartCoroutine(InitializeAfterFrame());
        }

        private IEnumerator InitializeAfterFrame()
        {
            yield return null; // Bir frame bekle
            InitializeForCurrentScene();
        }

        
        private void InitializeForCurrentScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            Debug.Log($"Initializing GameStateManager for scene: {currentScene}");
    
            if (currentScene == "MainMenu")
            {
                // MainMenu'deyiz, panelleri gizle ama state'i resetleme
                HidePanelsOnly();
                return;
            }
    
            // *** HER OYUN SCENE'İNDE ZORUNLU RESET ***
            Debug.Log("FORCE RESETTING game state for any game scene load");
            gameEnded = false;           // ZORUNLU RESET
            playerWon = false;           // ZORUNLU RESET
            Time.timeScale = 1f;         // ZORUNLU RESET
            // Roster'ı burada TEKRAR temizlemiyoruz: bu metod sahne-load event'i ile
            // bir frame gecikmeli tetiklenir ve OnNetworkSpawn'daki (server dalı) senkron
            // Clear + client'ların SubmitLocalNameServerRpc'si arasına girip host'un
            // kendi roster girdisini silebilirdi (sıralama garantisizdi). Roster temizliği
            // artık TEK noktada, OnNetworkSpawn'ın server dalında, herhangi bir submit
            // RPC'si işlenmeden ÖNCE yapılıyor - bkz. yukarısı ClearPlayerRoster().
    
            // UI referanslarını tekrar bul (çünkü yeni sahne)
            FindUIReferences();
            SetupExitButtons();
            HidePanelsOnly();
    
            Debug.Log("FORCE RESET completed - game can now lose properly");
        }

        private void FindUIReferences()
        {
            Debug.Log("=== SEARCHING FOR UI REFERENCES ===");
            
            // Eğer referanslar kayıpsa, sahne içinde bulmaya çalış
            if (winPanel == null)
            {
                // Önce direkt isimle ara
                winPanel = GameObject.Find("WinPanel");
                
                if (winPanel == null)
                {
                    // Alternatif isimleri dene
                    string[] possibleWinNames = { "Win Panel", "WinScreen", "Win Screen", "Victory Panel", "VictoryPanel" };
                    foreach (string name in possibleWinNames)
                    {
                        winPanel = GameObject.Find(name);
                        if (winPanel != null)
                        {
                            Debug.Log($"Win panel found with name: {name}");
                            break;
                        }
                    }
                }
                
                if (winPanel == null)
                {
                    // Tüm Canvas'ları ara
                    Canvas[] allCanvases = FindObjectsOfType<Canvas>();
                    foreach (Canvas canvas in allCanvases)
                    {
                        Debug.Log($"Searching in canvas: {canvas.name}");
                        
                        // Canvas'ın tüm çocuklarını kontrol et
                        for (int i = 0; i < canvas.transform.childCount; i++)
                        {
                            Transform child = canvas.transform.GetChild(i);
                            Debug.Log($"Canvas child: {child.name}");
                            
                            if (child.name.ToLower().Contains("win"))
                            {
                                winPanel = child.gameObject;
                                Debug.Log($"Win panel found as canvas child: {child.name}");
                                break;
                            }
                        }
                        
                        if (winPanel != null) break;
                    }
                }
            }
            
            if (losePanel == null)
            {
                // Önce direkt isimle ara
                losePanel = GameObject.Find("LosePanel");
                
                if (losePanel == null)
                {
                    // Alternatif isimleri dene
                    string[] possibleLoseNames = { "Lose Panel", "LoseScreen", "Lose Screen", "GameOver Panel", "GameOverPanel", "Defeat Panel", "DefeatPanel" };
                    foreach (string name in possibleLoseNames)
                    {
                        losePanel = GameObject.Find(name);
                        if (losePanel != null)
                        {
                            Debug.Log($"Lose panel found with name: {name}");
                            break;
                        }
                    }
                }
                
                if (losePanel == null)
                {
                    // Tüm Canvas'ları ara
                    Canvas[] allCanvases = FindObjectsOfType<Canvas>();
                    foreach (Canvas canvas in allCanvases)
                    {
                        // Canvas'ın tüm çocuklarını kontrol et
                        for (int i = 0; i < canvas.transform.childCount; i++)
                        {
                            Transform child = canvas.transform.GetChild(i);
                            
                            if (child.name.ToLower().Contains("lose") || child.name.ToLower().Contains("gameover") || child.name.ToLower().Contains("defeat"))
                            {
                                losePanel = child.gameObject;
                                Debug.Log($"Lose panel found as canvas child: {child.name}");
                                break;
                            }
                        }
                        
                        if (losePanel != null) break;
                    }
                }
            }

            // Button referanslarını da bul
            if (winExitButton == null && winPanel != null)
            {
                winExitButton = winPanel.GetComponentInChildren<Button>();
                if (winExitButton != null)
                    Debug.Log($"Win exit button found: {winExitButton.name}");
            }
            
            if (loseExitButton == null && losePanel != null)
            {
                loseExitButton = losePanel.GetComponentInChildren<Button>();
                if (loseExitButton != null)
                    Debug.Log($"Lose exit button found: {loseExitButton.name}");
            }
            
            Debug.Log($"=== UI SEARCH COMPLETE ===");
            Debug.Log($"Win Panel: {winPanel != null} ({(winPanel != null ? winPanel.name : "null")})");
            Debug.Log($"Lose Panel: {losePanel != null} ({(losePanel != null ? losePanel.name : "null")})");
            Debug.Log($"Win Button: {winExitButton != null}");
            Debug.Log($"Lose Button: {loseExitButton != null}");
            
            // Eğer hâlâ bulamazsak, debug için tüm Canvas objelerini listele
            if (winPanel == null || losePanel == null)
            {
                Debug.LogWarning("=== LISTING ALL CANVAS OBJECTS FOR DEBUG ===");
                Canvas[] allCanvases = FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in allCanvases)
                {
                    Debug.LogWarning($"Canvas: {canvas.name}");
                    for (int i = 0; i < canvas.transform.childCount; i++)
                    {
                        Transform child = canvas.transform.GetChild(i);
                        Debug.LogWarning($"  - Child: {child.name} (Active: {child.gameObject.activeInHierarchy})");
                        
                        // Derinlemesine ara
                        for (int j = 0; j < child.childCount; j++)
                        {
                            Transform grandChild = child.GetChild(j);
                            Debug.LogWarning($"    - Grandchild: {grandChild.name} (Active: {grandChild.gameObject.activeInHierarchy})");
                        }
                    }
                }
            }
        }

        // RESET FUNCTIONALITY - Ana çözüm burada
        public void ResetGameState()
        {
            Debug.Log("=== RESETTING GAME STATE ===");
    
            gameEnded = false;
            playerWon = false;
            // hasGameEverStarted'ı resetlemeyin - sadece oyun gerçekten ilk kez başladığında true yapılacak

            // Aynı process içinde yeni bir oturum başlarsa (DontDestroyOnLoad) önceki
            // Won/Lost durumu NetworkVariable'da sızmasın - yoksa yeni bir client
            // OnNetworkSpawn'da eski sonucu tekrar uygular.
            if (IsSpawned && IsServer)
            {
                _gameEndState.Value = GameEndState.None;
            }

            // Time scale'i normale döndür
            Time.timeScale = 1f;
    
            // Panelleri gizle
            HidePanelsOnly();
    
            Debug.Log("Game state reset completed");
        }


        private void SetupExitButtons()
        {
            if (winExitButton != null)
            {
                winExitButton.onClick.RemoveAllListeners();
                winExitButton.onClick.AddListener(ExitToMenu);
                Debug.Log("Win exit button setup complete");
            }
            else
            {
                Debug.LogWarning("Win exit button not found!");
            }
            
            if (loseExitButton != null)
            {
                loseExitButton.onClick.RemoveAllListeners();
                loseExitButton.onClick.AddListener(ExitToMenu);
                Debug.Log("Lose exit button setup complete");
            }
            else
            {
                Debug.LogWarning("Lose exit button not found!");
            }
        }

        /// <summary>
        /// Called when a customer is lost (wait bar expired and customer exits).
        /// Applies prestige penalty instead of instant game over.
        /// </summary>
        public void OnCustomerLost()
        {
            if (gameEnded)
            {
                Debug.Log("=== Game already ended, ignoring customer loss ===");
                return;
            }

            Debug.Log("=== CUSTOMER LOST - Prestige penalty ===");
            
            if (PrestigeManager.Instance != null)
            {
                float penalty = economySettings != null
                    ? economySettings.customerLostPrestigePenalty
                    : -2f;
                // SURPRISE AUDIT günü tüm cezalar 2× (yoksa 1×).
                float penaltyMult = EventEffectManager.Instance != null
                    ? EventEffectManager.Instance.GetPenaltyMultiplier() : 1f;
                penalty *= penaltyMult;
                PrestigeManager.Instance.ModifyPrestige(penalty);
                Debug.Log($"Prestige penalty applied: {penalty}");
            }
            else
            {
                Debug.LogWarning("PrestigeManager not found! Cannot apply penalty.");
            }
        }

        /// <summary>
        /// Checks if the player has won after completing day 16 with prestige > 0 and rent paid.
        /// completedDayOverride: DayCycleManager.NextDay kazanma kontrolünü gün sayacını
        /// İLERLETMEDEN önce çalıştırır (zafer anında NetworkVariable mutasyonu replike olmasın
        /// diye); ulaşılacak gün değerini buradan geçer. null ise canlı sayaç okunur (debug yolu).
        /// </summary>
        public void CheckWinCondition(int? completedDayOverride = null)
        {
            if (gameEnded)
            {
                Debug.Log("=== Game already ended, skipping win check ===");
                return;
            }

            int currentDay = completedDayOverride ?? DayCycleManager.Instance?.currentDay ?? 1;
            
            // Win condition: Reach day 16 with prestige > 0
            if (currentDay >= DayCycleManager.MAX_DAYS)
            {
                Debug.Log($"=== Day {currentDay} completed - VICTORY! ===");
                TriggerWin();
            }
        }

        private void TriggerWin()
        {
            Debug.Log("=== GAME WON ===");

            // Server-authoritative: karar burada, tüm makineler _gameEndState NetworkVariable'ının
            // OnValueChanged callback'i üzerinden AYNI sonucu görür (bkz. ApplyGameEndState).
            // Yalnızca IsSpawned && IsServer iken yazılabilir; bu metodun tüm çağıranları zaten
            // server-gate'li (DayCycleManager.Update / NextDay / CustomerAI.OnCustomerLost).
            if (IsSpawned && IsServer)
            {
                _gameEndState.Value = GameEndState.Won;
            }
        }

        public void TriggerLose()
        {
            Debug.Log("=== GAME LOST ===");

            if (IsSpawned && IsServer)
            {
                _gameEndState.Value = GameEndState.Lost;
            }
        }

        /// <summary>
        /// Roster'daki (server-authoritative) oyuncu isimlerini verilen TMP metnine yazar.
        /// Alan Inspector'dan atanmadıysa no-op (mevcut davranış bozulmaz).
        /// </summary>
        private void RenderRosterInto(TextMeshProUGUI target)
        {
            if (target == null)
            {
                return;
            }

            var names = GetRosterPlayerNames();
            target.text = names.Count > 0 ? string.Join("\n", names) : string.Empty;
        }

        private void ShowWinScreen()
        {
            Debug.Log("=== SHOWING WIN SCREEN ===");
            HidePanelsOnly();
            RenderRosterInto(winPlayerListText);

            // Eğer panel yoksa tekrar bulmaya çalış
            if (winPanel == null)
            {
                Debug.LogWarning("Win panel is null, trying to find it again...");
                FindUIReferences();
            }
            
            if (winPanel != null)
            {
                winPanel.SetActive(true);
                Debug.Log($"Win screen displayed: {winPanel.name}");
            }
            else
            {
                Debug.LogError("Win panel is STILL not found! Cannot show win screen!");
                // Fallback olarak tüm win ile ilgili objeleri aktifleştir
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.ToLower().Contains("win") && obj.GetComponent<Canvas>() == null)
                    {
                        if (obj.transform.parent != null && obj.transform.parent.GetComponent<Canvas>() != null)
                        {
                            obj.SetActive(true);
                            Debug.Log($"Fallback: Activated potential win panel: {obj.name}");
                            break;
                        }
                    }
                }
            }
        }

        private void ShowLoseScreen()
        {
            Debug.Log("=== SHOWING LOSE SCREEN ===");
            HidePanelsOnly();
            RenderRosterInto(losePlayerListText);

            // Eğer panel yoksa tekrar bulmaya çalış
            if (losePanel == null)
            {
                Debug.LogWarning("Lose panel is null, trying to find it again...");
                FindUIReferences();
            }
            
            if (losePanel != null)
            {
                losePanel.SetActive(true);
                Debug.Log($"Lose screen displayed: {losePanel.name}");
            }
            else
            {
                Debug.LogError("Lose panel is STILL not found! Cannot show lose screen!");
                // Fallback olarak tüm lose ile ilgili objeleri aktifleştir
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if ((obj.name.ToLower().Contains("lose") || obj.name.ToLower().Contains("gameover") || obj.name.ToLower().Contains("defeat")) && obj.GetComponent<Canvas>() == null)
                    {
                        if (obj.transform.parent != null && obj.transform.parent.GetComponent<Canvas>() != null)
                        {
                            obj.SetActive(true);
                            Debug.Log($"Fallback: Activated potential lose panel: {obj.name}");
                            break;
                        }
                    }
                }
            }
        }

        private void HidePanelsOnly()
        {
            Debug.Log("=== HIDING ALL PANELS ===");
            
            if (winPanel != null) 
            {
                winPanel.SetActive(false);
                Debug.Log($"Win panel hidden: {winPanel.name}");
            }
            else
            {
                Debug.LogWarning("Win panel is null when trying to hide");
            }
            
            if (losePanel != null) 
            {
                losePanel.SetActive(false);
                Debug.Log($"Lose panel hidden: {losePanel.name}");
            }
            else
            {
                Debug.LogWarning("Lose panel is null when trying to hide");
            }
            
            // Ekstra güvenlik: Tüm potansiyel panelleri gizle
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.ToLower().Contains("win") || obj.name.ToLower().Contains("lose") || 
                    obj.name.ToLower().Contains("gameover") || obj.name.ToLower().Contains("defeat"))
                {
                    if (obj.transform.parent != null && obj.transform.parent.GetComponent<Canvas>() != null && 
                        obj.GetComponent<Canvas>() == null && obj.activeInHierarchy)
                    {
                        obj.SetActive(false);
                        Debug.Log($"Extra safety: Hidden potential panel: {obj.name}");
                    }
                }
            }
        }

        public void ExitToMenu()
        {
            Debug.Log("=== EXITING TO MENU ===");
            
            // Zamanı geri başlat
            Time.timeScale = 1f;
            
            // Panelleri gizle
            HidePanelsOnly();
            
            // Menu'ye dön
            StartCoroutine(ExitToMenuCoroutine());
        }

        private IEnumerator ExitToMenuCoroutine()
        {
            // Network'ü kapat
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                else if (NetworkManager.Singleton.IsClient)
                {
                    NetworkManager.Singleton.Shutdown();
                }
            }
            
            yield return new WaitForSecondsRealtime(0.1f);
            
            // Transport'u resetle
            if (NetworkManager.Singleton != null)
            {
                var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
                if (transport != null)
                {
                    transport.SetConnectionData("127.0.0.1", 7777);
                }
            }
            
            // Menu scene'ini yükle
            SceneManager.LoadScene("MainMenu");
        }

        // Debug için helper metod
        public void DebugGameState()
        {
            if (DayCycleManager.Instance != null && MoneySystem.Instance != null)
            {
                Debug.Log($"=== Game State Debug ===");
                Debug.Log($"Day: {DayCycleManager.Instance.currentDay}");
                Debug.Log($"Money: {MoneySystem.Instance.CurrentMoney}");
                Debug.Log($"Game Ended: {gameEnded}");
                Debug.Log($"Player Won: {playerWon}");
                Debug.Log($"Current Scene: {SceneManager.GetActiveScene().name}");
                Debug.Log($"Win Panel Active: {winPanel?.activeInHierarchy}");
                Debug.Log($"Lose Panel Active: {losePanel?.activeInHierarchy}");
            }
        }

        [ContextMenu("Force Check Win Condition")]
        public void ForceCheckWinCondition()
        {
            Debug.Log("=== FORCE CHECKING WIN CONDITION ===");
            CheckWinCondition();
            DebugGameState();
        }

        [ContextMenu("Test Win")]
        public void TestWin()
        {
            TriggerWin();
        }

        [ContextMenu("Test Lose")]
        public void TestLose()
        {
            TriggerLose();
        }
        
        [ContextMenu("Debug State")]
        public void TestDebugState()
        {
            DebugGameState();
        }

        [ContextMenu("Reset Game State")]
        public void TestResetGameState()
        {
            ResetGameState();
        }
        
        [ContextMenu("Hide All Panels")]
        public void TestHidePanels()
        {
            HidePanelsOnly();
        }
        
        [ContextMenu("Find UI References")]
        public void TestFindUIReferences()
        {
            FindUIReferences();
        }
    }
}