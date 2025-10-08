using UnityEngine;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using System.Collections.Generic;
using System.Linq;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SteamManager : MonoBehaviour
{
    private Lobby CurrentLobby;
    [SerializeField] private TMP_InputField LobbyIDInputField;
    [SerializeField] private TextMeshProUGUI LobbyID;
    [SerializeField] private GameObject MainMenu;
    [SerializeField] private GameObject InLobbyMenu;
    private bool wasInMainMenu = true;

    [Header("Join Error Messages")]
    [SerializeField] private TextMeshProUGUI ErrorMessageText;
    [SerializeField] private float ErrorMessageDuration = 3f;

    [Header("Start Game Button")]
    [SerializeField] private Button StartGameButton;

    [Header("Player Slots")]
    [SerializeField] private PlayerSlot[] playerSlots = new PlayerSlot[4];

    [Header("Slot Background Images")]
    [SerializeField] private Sprite occupiedSlotSprite;
    [SerializeField] private Sprite emptySlotSprite;

    private const int MAX_PLAYERS = 4;
    private bool isJoiningLobby = false;
    private bool isLobbyJoinValid = false; // YENİ: Lobiye başarıyla katılıp katılmadığımızı takip eder

    [System.Serializable]
    public class PlayerSlot
    {
        public TextMeshProUGUI playerNameText;
        public UnityEngine.UI.Image backgroundImage;
    }

    void OnEnable()
    {
        SteamMatchmaking.OnLobbyCreated += LobbyCreated;
        SteamMatchmaking.OnLobbyEntered += LobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested += GameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SteamMatchmaking.OnLobbyCreated -= LobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= LobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested -= GameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene loaded: {scene.name}");

        if (scene.name == "Map1" || scene.name.Contains("Game") || scene.name.Contains("Map"))
        {
            Debug.Log($"Game scene '{scene.name}' loaded - Checking if reset needed");

            if (wasInMainMenu || IsFirstGameLoad())
            {
                Debug.Log("Resetting all managers for new game session");
                StartCoroutine(ResetManagersAfterSceneLoad());
            }
            else
            {
                Debug.Log("Scene reload detected during active game - skipping manager reset");
            }
        }
        else if (scene.name == "MainMenu")
        {
            wasInMainMenu = true;
        }
    }

    private bool IsFirstGameLoad()
    {
        if (NewCss.GameStateManager.Instance == null)
        {
            return true;
        }

        return !NewCss.GameStateManager.Instance.HasGameEverStarted;
    }

    private System.Collections.IEnumerator ResetManagersAfterSceneLoad()
    {
        yield return new WaitForSeconds(0.5f);

        Debug.Log("=== FORCING MANAGER RESET FOR NEW GAME SESSION ===");

        if (NewCss.GameStateManager.Instance != null)
        {
            Debug.Log("Force resetting GameStateManager...");
            NewCss.GameStateManager.Instance.ResetGameState();
            Debug.Log("GameStateManager force reset completed");
        }
        else
        {
            Debug.LogWarning("GameStateManager.Instance not found in new scene");
        }

        if (NewCss.DayCycleManager.Instance != null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Debug.Log("Force resetting DayCycleManager...");
                NewCss.DayCycleManager.Instance.ResetDayCycle();
                Debug.Log("DayCycleManager force reset completed (server)");
            }
            else
            {
                Debug.Log("DayCycleManager reset skipped (not server)");
            }
        }
        else
        {
            Debug.LogWarning("DayCycleManager.Instance not found in new scene");
        }

        Debug.Log("All managers force reset completed - ready for new game");
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        // Sadece geçerli bir lobby'deyken üye katılımlarını işle
        if (!isLobbyJoinValid || CurrentLobby.Id == 0 || CurrentLobby.Id != lobby.Id)
        {
            Debug.LogWarning($"Ignoring member join event for invalid lobby state");
            return;
        }

        Debug.Log($"Player joined: {friend.Name}");
        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        // Sadece geçerli bir lobby'deyken üye ayrılmalarını işle
        if (!isLobbyJoinValid || CurrentLobby.Id == 0 || CurrentLobby.Id != lobby.Id)
        {
            Debug.LogWarning($"Ignoring member leave event for invalid lobby state");
            return;
        }

        Debug.Log($"Player left: {friend.Name}");
        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
    }

    private void UpdateStartButtonVisibility()
    {
        if (StartGameButton == null) return;

        // Geçerli bir lobby'de değilsek butonu gizle
        if (!isLobbyJoinValid || CurrentLobby.Id == 0)
        {
            StartGameButton.gameObject.SetActive(false);
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            StartGameButton.gameObject.SetActive(false);
            return;
        }

        bool isHost = NetworkManager.Singleton.IsHost;
        StartGameButton.gameObject.SetActive(isHost);

        Debug.Log($"Start button visibility: {isHost} (IsHost: {NetworkManager.Singleton.IsHost}, IsClient: {NetworkManager.Singleton.IsClient})");
    }

    private void UpdatePlayerSlots()
    {
        // Geçerli lobby yoksa tüm slotları boşalt ve çık
        if (!isLobbyJoinValid || CurrentLobby.Id == 0)
        {
            Debug.LogWarning("UpdatePlayerSlots called with invalid lobby state");
            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                SetSlotEmpty(i);
            }
            return;
        }

        // Önce tüm slotları temizle
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            SetSlotEmpty(i);
        }

        try
        {
            var members = CurrentLobby.Members.ToArray();

            // Eğer üye yoksa, bu geçersiz bir lobby'dir
            if (members.Length == 0)
            {
                Debug.LogWarning("Lobby has no members, marking as invalid");
                InvalidateLobby();
                return;
            }

            // Üyeleri slotlara yerleştir
            for (int i = 0; i < members.Length && i < MAX_PLAYERS; i++)
            {
                SetSlotOccupied(i, members[i].Name);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error updating player slots: {ex.Message}");
            InvalidateLobby();
        }
    }

    private void SetSlotOccupied(int slotIndex, string playerName)
    {
        if (slotIndex >= 0 && slotIndex < playerSlots.Length)
        {
            playerSlots[slotIndex].playerNameText.text = playerName;
            playerSlots[slotIndex].backgroundImage.sprite = occupiedSlotSprite;
            playerSlots[slotIndex].backgroundImage.color = UnityEngine.Color.white;
        }
    }

    private void SetSlotEmpty(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < playerSlots.Length)
        {
            playerSlots[slotIndex].playerNameText.text = "Empty";
            playerSlots[slotIndex].backgroundImage.sprite = emptySlotSprite;
            playerSlots[slotIndex].backgroundImage.color = UnityEngine.Color.white;
        }
    }

    private async void GameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        try
        {
            Debug.Log($"Steam overlay join requested for lobby: {lobby.Id}");
            var joinResult = await lobby.Join();

            if (joinResult != RoomEnter.Success)
            {
                Debug.LogError($"Failed to join requested lobby: {joinResult}");
                ShowErrorMessage("Lobiye katılınamadı!");
                isLobbyJoinValid = false;
            }
            else
            {
                isLobbyJoinValid = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error joining lobby: {ex.Message}");
            ShowErrorMessage("Bağlantı hatası!");
            isLobbyJoinValid = false;
        }
    }

    private void LobbyCreated(Result result, Lobby lobby)
    {
        if (result == Result.OK)
        {
            Debug.Log($"Lobby created successfully with ID: {lobby.Id}");

            CurrentLobby = lobby;
            isLobbyJoinValid = true; // Host her zaman geçerli lobby'ye sahiptir

            lobby.SetPublic();
            lobby.SetJoinable(true);
            lobby.SetData("game_version", Application.version);

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("NetworkManager.Singleton is null!");
                InvalidateLobby();
                return;
            }

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                Debug.Log("NetworkManager already running, shutting down first...");
                NetworkManager.Singleton.Shutdown();
                StartCoroutine(DelayedHostStart(lobby));
            }
            else
            {
                StartHost(lobby);
            }
        }
        else
        {
            Debug.LogError($"Lobby creation failed: {result}");
            ShowErrorMessage("Lobi oluşturulamadı!");
            isLobbyJoinValid = false;
        }
    }

    private void StartHost(Lobby lobby)
    {
        try
        {
            Debug.Log($"StartHost called for lobby {lobby.Id}");

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                Debug.LogError("NetworkManager is still active! Cannot start host.");
                InvalidateLobby();
                return;
            }

            var transport = NetworkManager.Singleton.gameObject.GetComponent<FacepunchTransport>();
            if (transport == null)
            {
                Debug.LogError("FacepunchTransport component not found!");
                ShowErrorMessage("Network hatası!");
                InvalidateLobby();
                return;
            }

            transport.targetSteamId = lobby.Id;
            Debug.Log($"FacepunchTransport configured with Steam ID: {lobby.Id}");

            bool hostStarted = NetworkManager.Singleton.StartHost();
            if (hostStarted)
            {
                Debug.Log("Host started successfully");
                isLobbyJoinValid = true;
                UpdateStartButtonVisibility();
            }
            else
            {
                Debug.LogError("Failed to start host!");
                ShowErrorMessage("Host başlatılamadı!");
                InvalidateLobby();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error starting host: {ex.Message}");
            ShowErrorMessage("Host hatası!");
            InvalidateLobby();
        }
    }

    private System.Collections.IEnumerator DelayedHostStart(Lobby lobby)
    {
        yield return new WaitForSeconds(1f);
        StartHost(lobby);
    }

    private void LobbyEntered(Lobby lobby)
    {
        // SADECE geçerli katılma işlemlerinde UI'ı güncelle
        if (!isLobbyJoinValid)
        {
            Debug.LogWarning("LobbyEntered called but join was not valid, ignoring...");
            return;
        }

        Debug.Log($"LobbyEntered: Valid join to lobby {lobby.Id}");

        CurrentLobby = lobby;
        LobbySaver.instance.CurrentLobby = lobby;
        LobbyID.text = lobby.Id.ToString();

        // Client ise network'e bağlan
        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
        {
            var transport = NetworkManager.Singleton.gameObject.GetComponent<FacepunchTransport>();
            transport.targetSteamId = lobby.Owner.Id;

            bool clientStarted = NetworkManager.Singleton.StartClient();
            Debug.Log($"Client start attempt: {clientStarted}");

            if (!clientStarted)
            {
                ShowErrorMessage("Bağlantı başarısız!");
                InvalidateLobby();
                return;
            }
        }

        // Üyeleri kontrol et - eğer üye yoksa geçersiz lobby
        try
        {
            var members = lobby.Members.ToArray();
            if (members.Length == 0)
            {
                Debug.LogError("Lobby has no members after join!");
                ShowErrorMessage("Geçersiz lobi!");
                InvalidateLobby();
                return;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error checking lobby members: {ex.Message}");
            ShowErrorMessage("Lobi hatası!");
            InvalidateLobby();
            return;
        }

        // Her şey başarılıysa UI'ı güncelle
        MainMenu.SetActive(false);
        InLobbyMenu.SetActive(true);
        UpdatePlayerSlots();
        UpdateStartButtonVisibility();

        Debug.Log("Successfully entered and validated lobby");
    }

    public async void HostLobby()
    {
        try
        {
            Debug.Log("HostLobby called - starting network cleanup...");
            await ForceNetworkCleanup();

            Debug.Log("Creating new lobby after cleanup...");
            var lobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
            if (lobby.HasValue)
            {
                Debug.Log("Lobby creation initiated");
                // isLobbyJoinValid, LobbyCreated callback'inde set edilecek
            }
            else
            {
                Debug.LogError("Failed to initiate lobby creation");
                ShowErrorMessage("Lobi oluşturulamadı!");
                isLobbyJoinValid = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error creating lobby: {ex.Message}");
            ShowErrorMessage("Lobi hatası!");
            isLobbyJoinValid = false;
        }
    }

    private async System.Threading.Tasks.Task ForceNetworkCleanup()
    {
        Debug.Log("=== STARTING FORCE NETWORK CLEANUP ===");

        // Önce geçersiz duruma getir
        isLobbyJoinValid = false;

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                Debug.Log("Shutting down existing NetworkManager connection...");
                NetworkManager.Singleton.Shutdown();

                int waitTime = 0;
                while ((NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient) && waitTime < 5000)
                {
                    await System.Threading.Tasks.Task.Delay(100);
                    waitTime += 100;
                }

                Debug.Log($"NetworkManager shutdown completed after {waitTime}ms");
            }

            try
            {
                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Single);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error during scene manager cleanup: {ex.Message}");
            }
        }

        var transport = FindObjectOfType<FacepunchTransport>();
        if (transport != null)
        {
            Debug.Log("Resetting FacepunchTransport...");
            transport.targetSteamId = 0;
            transport.enabled = false;
            await System.Threading.Tasks.Task.Delay(100);
            transport.enabled = true;
        }

        if (CurrentLobby.Id != 0)
        {
            Debug.Log($"Leaving previous lobby {CurrentLobby.Id}");
            try
            {
                CurrentLobby.Leave();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Error leaving lobby: {ex.Message}");
            }
            CurrentLobby = default(Lobby);
        }

        if (LobbySaver.instance != null)
        {
            LobbySaver.instance.ClearLobby();
        }

        Debug.Log("Final cleanup delay...");
        await System.Threading.Tasks.Task.Delay(500);

        Debug.Log("=== FORCE NETWORK CLEANUP COMPLETED ===");
    }

    public void CopyID()
    {
        if (!isLobbyJoinValid || CurrentLobby.Id == 0)
        {
            ShowErrorMessage("Geçerli bir lobi yok!");
            return;
        }

        TextEditor textEditor = new TextEditor();
        textEditor.text = LobbyID.text;
        textEditor.SelectAll();
        textEditor.Copy();

        if (ErrorMessageText != null)
        {
            ShowErrorMessage("Lobi ID kopyalandı!", 2f);
        }
    }

    public async void LeaveLobby()
    {
        Debug.Log("LeaveLobby called - starting comprehensive cleanup...");

        isLobbyJoinValid = false;
        await ForceNetworkCleanup();

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            SetSlotEmpty(i);
        }

        if (StartGameButton != null)
        {
            StartGameButton.gameObject.SetActive(false);
        }

        Debug.Log("Lobby cleanup completed");
        CheckUI();
    }

    private void CheckUI()
    {
        if (!isLobbyJoinValid || LobbySaver.instance.CurrentLobby == null || CurrentLobby.Id == 0)
        {
            MainMenu.SetActive(true);
            InLobbyMenu.SetActive(false);

            if (StartGameButton != null)
            {
                StartGameButton.gameObject.SetActive(false);
            }
        }
        else
        {
            MainMenu.SetActive(false);
            InLobbyMenu.SetActive(true);
            UpdateStartButtonVisibility();
        }
    }

    // Lobby'yi geçersiz kıl ve UI'ı düzelt
    private void InvalidateLobby()
    {
        Debug.Log("Invalidating current lobby...");
        isLobbyJoinValid = false;

        // Slotları temizle
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            SetSlotEmpty(i);
        }

        // UI'ı ana menüye döndür
        MainMenu.SetActive(true);
        InLobbyMenu.SetActive(false);

        if (StartGameButton != null)
        {
            StartGameButton.gameObject.SetActive(false);
        }

        // Lobby'yi temizle
        if (CurrentLobby.Id != 0)
        {
            try
            {
                CurrentLobby.Leave();
            }
            catch { }
            CurrentLobby = default(Lobby);
        }

        if (LobbySaver.instance != null)
        {
            LobbySaver.instance.ForceClearLobby();
        }
    }

    public async void JoinLobbyWithID()
    {
        if (isJoiningLobby)
        {
            Debug.Log("Already attempting to join a lobby, please wait...");
            return;
        }

        if (string.IsNullOrEmpty(LobbyIDInputField.text))
        {
            ShowErrorMessage("Lütfen bir Lobi ID girin!");
            return;
        }

        if (!ulong.TryParse(LobbyIDInputField.text, out ulong lobbyID))
        {
            ShowErrorMessage("Geçersiz Lobi ID formatı!");
            return;
        }

        isJoiningLobby = true;
        isLobbyJoinValid = false; // Henüz geçerli değil
        Debug.Log($"Attempting to join lobby with ID: {lobbyID}");

        try
        {
            // Önce mevcut bağlantıyı temizle
            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
            {
                Debug.Log("Cleaning up existing connection before joining...");
                await ForceNetworkCleanup();
            }

            // Lobby'yi oluştur
            Lobby targetLobby = new Lobby(lobbyID);

            Debug.Log($"Attempting to join lobby directly...");

            // Doğrudan katılmayı dene
            var joinResult = await targetLobby.Join();

            if (joinResult == RoomEnter.Success)
            {
                Debug.Log($"Join successful, validating lobby...");

                // Lobinin geçerli olup olmadığını kontrol et
                try
                {
                    var members = targetLobby.Members.ToArray();
                    if (members.Length > 0)
                    {
                        Debug.Log($"Lobby validated with {members.Length} members");
                        isLobbyJoinValid = true; // Geçerli katılım
                        // LobbyEntered event'i otomatik çağrılacak
                    }
                    else
                    {
                        Debug.LogError("Lobby has no members!");
                        ShowErrorMessage("Geçersiz lobi!");
                        isLobbyJoinValid = false;
                        targetLobby.Leave();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error validating lobby: {ex.Message}");
                    ShowErrorMessage("Lobi doğrulanamadı!");
                    isLobbyJoinValid = false;
                    targetLobby.Leave();
                }
            }
            else
            {
                // Katılma başarısız
                isLobbyJoinValid = false;
                string errorMessage = joinResult switch
                {
                    RoomEnter.DoesntExist => "Lobi mevcut değil!",
                    RoomEnter.NotAllowed => "Lobiye girme yetkiniz yok!",
                    RoomEnter.Full => "Lobi dolu!",
                    RoomEnter.Error => "Bağlantı hatası!",
                    RoomEnter.Banned => "Bu lobiden banlandınız!",
                    RoomEnter.Limited => "Hesabınız sınırlı!",
                    RoomEnter.ClanDisabled => "Klan devre dışı!",
                    RoomEnter.CommunityBan => "Topluluk yasağınız var!",
                    _ => $"Katılım başarısız: {joinResult}"
                };

                Debug.LogWarning($"Join failed with result: {joinResult}");
                ShowErrorMessage(errorMessage);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception while joining lobby: {ex.Message}\n{ex.StackTrace}");
            ShowErrorMessage("Beklenmeyen hata oluştu!");
            isLobbyJoinValid = false;
        }
        finally
        {
            isJoiningLobby = false;
        }
    }

    private void ShowErrorMessage(string message, float duration = -1)
    {
        if (ErrorMessageText == null)
        {
            Debug.LogWarning($"ErrorMessageText is not assigned! Message: {message}");
            return;
        }

        ErrorMessageText.text = message;
        ErrorMessageText.gameObject.SetActive(true);

        float displayDuration = duration > 0 ? duration : ErrorMessageDuration;

        StopAllCoroutines();
        StartCoroutine(HideErrorMessageAfterDelay(displayDuration));
    }

    private System.Collections.IEnumerator HideErrorMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (ErrorMessageText != null)
        {
            ErrorMessageText.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (!SteamClient.IsValid)
        {
            Debug.LogError("Steam client is not initialized!");
            ShowErrorMessage("Steam bağlantısı yok!");
        }

        isLobbyJoinValid = false;

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            SetSlotEmpty(i);
        }

        if (StartGameButton != null)
        {
            StartGameButton.gameObject.SetActive(false);
        }

        if (ErrorMessageText != null)
        {
            ErrorMessageText.gameObject.SetActive(false);
        }
    }

    public void StartGameServer()
    {
        if (!isLobbyJoinValid || CurrentLobby.Id == 0)
        {
            ShowErrorMessage("Geçerli bir lobi yok!");
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("Only the host can start the game!");
            ShowErrorMessage("Sadece host oyunu başlatabilir!");
            return;
        }

        if (NetworkManager.Singleton.SceneManager == null)
        {
            Debug.LogError("NetworkSceneManager is not ready!");
            ShowErrorMessage("Network hazır değil!");
            return;
        }

        Debug.Log("Host starting game - loading Map1 scene with manager reset");

        if (StartGameButton != null)
        {
            StartGameButton.interactable = false;
        }

        NotifyClientsGameStartingClientRpc();
        PrepareManagersForSceneLoad();
        StartCoroutine(DelayedSceneLoad());
    }

    private void PrepareManagersForSceneLoad()
    {
        Debug.Log("Preparing managers for scene load...");

        if (NetworkManager.Singleton.IsServer)
        {
            Time.timeScale = 1f;
            Debug.Log("Managers prepared for scene load");
        }
    }

    [ClientRpc]
    private void NotifyClientsGameStartingClientRpc()
    {
        Debug.Log("Game is starting, preparing for scene change...");
        Time.timeScale = 1f;
    }

    private System.Collections.IEnumerator DelayedSceneLoad()
    {
        yield return new WaitForSeconds(0.5f);

        try
        {
            Debug.Log("Loading Map1 scene for all clients...");

            var sceneLoadStatus = NetworkManager.Singleton.SceneManager.LoadScene("Map1", LoadSceneMode.Single);

            if (sceneLoadStatus != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"Failed to start scene loading: {sceneLoadStatus}");
                ShowErrorMessage("Sahne yüklenemedi!");

                if (StartGameButton != null)
                {
                    StartGameButton.interactable = true;
                }
            }
            else
            {
                Debug.Log("Scene loading started successfully - managers will be reset after load");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during scene loading: {ex.Message}");
            ShowErrorMessage("Yükleme hatası!");

            if (StartGameButton != null)
            {
                StartGameButton.interactable = true;
            }
        }
    }
}