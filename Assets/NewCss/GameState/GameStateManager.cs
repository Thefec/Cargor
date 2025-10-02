using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
using UnityEngine.SceneManagement;

namespace NewCss
{
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        [Header("Win/Lose Settings")]
        [Tooltip("How many successful rent payments needed to win (5 for 30 days)")]
        public int requiredRentPayments = 5;
        
        [Header("UI Panels - Assign your existing panels")]
        public GameObject winPanel;
        public GameObject losePanel;
        
        [Header("Exit Buttons - Assign your existing buttons")]
        public Button winExitButton;
        public Button loseExitButton;
        
        private bool gameEnded = false;
        private bool playerWon = false;
        private int successfulRentPayments = 0;
        private bool hasGameEverStarted = false; 
        
        public bool HasGameEverStarted => hasGameEverStarted;
        
        // Scene management
        private string currentGameScene = "";
        private bool isReturningFromMenu = false;

        // Public property for checking game state
        public bool IsDayOver => gameEnded;

        void Awake()
        {
            Debug.Log("GameStateManager Awake called");
            
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
            successfulRentPayments = 0;  // ZORUNLU RESET
            Time.timeScale = 1f;         // ZORUNLU RESET
    
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
            successfulRentPayments = 0;
            // hasGameEverStarted'ı resetlemeyin - sadece oyun gerçekten ilk kez başladığında true yapılacak
    
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

        public void CheckGameState()
        {
            Debug.Log("=== CheckGameState called ===");
            Debug.Log($"Current gameEnded status: {gameEnded}");
    
            if (gameEnded) 
            {
                Debug.Log("Game already ended, skipping check");
                return;
            }

            int currentDay = DayCycleManager.Instance?.currentDay ?? 1;

            Debug.Log($"CheckGameState - Day: {currentDay}, Rent Payments: {successfulRentPayments}");

            // Kira günü kontrolü
            bool isRentDay = (currentDay % 6 == 0);

            if (isRentDay)
            {
                // Kira başarıyla ödendi
                successfulRentPayments++;
                Debug.Log($"Rent paid successfully! Total payments: {successfulRentPayments}/{requiredRentPayments}");

                // Win koşulu kontrolü
                if (currentDay >= 30 || successfulRentPayments >= requiredRentPayments)
                {
                    Debug.Log("VICTORY! Survived 30 days / 5 rent payments!");
                    TriggerWin();
                }
                else
                {
                    Debug.Log($"Continue playing... Need {requiredRentPayments - successfulRentPayments} more rent payments");
                }
            }
        }
        
        public void CheckMoneyAtDayEnd()
        {
            Debug.Log("=== CheckMoneyAtDayEnd called ===");
            Debug.Log($"Current gameEnded status: {gameEnded}");
    
            if (gameEnded) 
            {
                Debug.Log("Game already ended, skipping money check");
                return;
            }

            int currentDay = DayCycleManager.Instance?.currentDay ?? 1;

            // Yarın kira günü mü kontrol et
            int tomorrow = currentDay + 1;
            bool isTomorrowRentDay = (tomorrow % 6 == 0);

            Debug.Log($"END OF DAY {currentDay} - Tomorrow is day {tomorrow}");

            if (isTomorrowRentDay)
            {
                // Yarın kira günü - para yeterli mi kontrol et
                int weeklyCost = GetCurrentWeeklyCost();
                int currentMoney = MoneySystem.Instance?.CurrentMoney ?? 0;

                Debug.Log($"Tomorrow is rent day! Money Check:");
                Debug.Log($"Current Money: {currentMoney}, Required for tomorrow's rent: {weeklyCost}");

                if (currentMoney < weeklyCost)
                {
                    // Para yetmiyor - LOSE
                    Debug.Log("NOT ENOUGH MONEY FOR TOMORROW'S RENT - GAME OVER!");
                    TriggerLose();
                }
                else
                {
                    Debug.Log("Money sufficient for tomorrow's rent. Continue playing.");
                }
            }
            else
            {
                Debug.Log($"Tomorrow (day {tomorrow}) is not a rent day - no money check needed");
            }
        }

        private int GetCurrentWeeklyCost()
        {
            if (DayCycleManager.Instance != null)
            {
                return DayCycleManager.Instance.WeeklyCost;
            }
            return 1000;
        }

        private void TriggerWin()
        {
            Debug.Log("=== GAME WON ===");
            gameEnded = true;
            playerWon = true;
            
            // Zamanı durdur
            Time.timeScale = 0f;
            ShowWinScreen();
        }

        private void TriggerLose()
        {
            Debug.Log("=== GAME LOST ===");
            gameEnded = true;
            playerWon = false;
            
            // Zamanı durdur
            Time.timeScale = 0f;
            ShowLoseScreen();
        }

        private void ShowWinScreen()
        {
            Debug.Log("=== SHOWING WIN SCREEN ===");
            HidePanelsOnly();
            
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
                Debug.Log($"Weekly Cost: {DayCycleManager.Instance.WeeklyCost}");
                Debug.Log($"Successful Rent Payments: {successfulRentPayments}/{requiredRentPayments}");
                Debug.Log($"Game Ended: {gameEnded}");
                Debug.Log($"Player Won: {playerWon}");
                Debug.Log($"Current Scene: {SceneManager.GetActiveScene().name}");
                Debug.Log($"Win Panel Active: {winPanel?.activeInHierarchy}");
                Debug.Log($"Lose Panel Active: {losePanel?.activeInHierarchy}");
            }
        }

        [ContextMenu("Force Check Game State")]
        public void ForceCheckGameState()
        {
            Debug.Log("=== FORCE CHECKING GAME STATE ===");
            CheckGameState();
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