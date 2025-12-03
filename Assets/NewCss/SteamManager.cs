using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityColor = UnityEngine.Color;
using UnityImage = UnityEngine.UI.Image;

/// <summary>
/// Steam lobi yönetimi, oyuncu slot sistemi, oyun başlatma ve network bağlantılarını yöneten ana sınıf.
/// Steamworks. NET ve Unity Netcode entegrasyonu sağlar.
/// </summary>
public class SteamManager : MonoBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[SteamManager]";
    private const int MAX_PLAYERS = 4;
    private const string GAME_SCENE_NAME = "The Main Office";
    private const string MAIN_MENU_SCENE_NAME = "MainMenu";
    private const string LOBBY_DATA_VERSION_KEY = "game_version";

    // Loading screen
    private const float LOADING_DOT_INTERVAL = 0.5f;
    private const float INITIAL_LOAD_PROGRESS = 0.2f;
    private const float SCENE_LOAD_START_PROGRESS = 0.3f;
    private const float MAX_TRACKING_PROGRESS = 0.9f;
    private const float LOADING_PROGRESS_SPEED = 0.3f;

    // Delays
    private const float HOST_START_DELAY = 1f;
    private const float MANAGERS_RESET_DELAY = 0.5f;
    private const float SCENE_LOAD_DELAY = 0.5f;
    private const float LOADING_COMPLETE_DELAY = 0.5f;
    private const int NETWORK_SHUTDOWN_TIMEOUT_MS = 5000;
    private const int NETWORK_SHUTDOWN_CHECK_INTERVAL_MS = 100;
    private const int TRANSPORT_RESET_DELAY_MS = 100;
    private const int CLEANUP_FINAL_DELAY_MS = 500;

    #endregion

    #region Serialized Fields

    [Header("=== UI REFERENCES ===")]
    [SerializeField, Tooltip("Lobi ID giriş alanı")]
    private TMP_InputField LobbyIDInputField;

    [SerializeField, Tooltip("Lobi ID gösterim text'i")]
    private TextMeshProUGUI LobbyID;

    [SerializeField, Tooltip("Ana menü paneli")]
    private GameObject MainMenu;

    [SerializeField, Tooltip("Lobi içi menü paneli")]
    private GameObject InLobbyMenu;

    [Header("=== ERROR MESSAGES ===")]
    [SerializeField, Tooltip("Hata mesajı text'i")]
    private TextMeshProUGUI ErrorMessageText;

    [SerializeField, Tooltip("Hata mesajı gösterim süresi")]
    private float ErrorMessageDuration = 3f;

    [Header("=== START GAME ===")]
    [SerializeField, Tooltip("Oyun başlatma butonu")]
    private Button StartGameButton;

    [Header("=== PLAYER SLOTS ===")]
    [SerializeField, Tooltip("Oyuncu slot'ları")]
    private PlayerSlot[] playerSlots = new PlayerSlot[MAX_PLAYERS];

    [Header("=== SLOT SPRITES ===")]
    [SerializeField, Tooltip("Dolu slot sprite'ı")]
    private Sprite occupiedSlotSprite;

    [SerializeField, Tooltip("Boş slot sprite'ı")]
    private Sprite emptySlotSprite;

    [Header("=== LOADING SCREEN ===")]
    [SerializeField, Tooltip("Yükleme ekranı")]
    private GameObject loadingScreen;

    [SerializeField, Tooltip("Yükleme text'i")]
    private TextMeshProUGUI loadingText;

    [SerializeField, Tooltip("Yükleme progress bar'ı")]
    private Slider loadingProgressBar;

    [SerializeField, Tooltip("Minimum yükleme süresi")]
    private float minimumLoadTime = 1f;

    #endregion

    #region Nested Types

    [Serializable]
    public class PlayerSlot
    {
        public TextMeshProUGUI playerNameText;
        public UnityImage backgroundImage;
        public Button kickButton;
    }

    #endregion

    #region Private Fields

    private Lobby _currentLobby;
    private bool _wasInMainMenu = true;
    private bool _isJoiningLobby;
    private bool _isLobbyJoinValid;
    private bool _isLoadingScene;
    private Coroutine _errorMessageCoroutine;
    private Coroutine _loadingDotsCoroutine;

    #endregion

    #region Public Properties

    /// <summary>
    /// Mevcut lobi (backward compatibility)
    /// </summary>
    public Lobby CurrentLobby => _currentLobby;

    /// <summary>
    /// Lobi geçerli mi?
    /// </summary>
    public bool IsLobbyValid => _isLobbyJoinValid && _currentLobby.Id != 0;

    /// <summary>
    /// Lobideki oyuncu sayısı
    /// </summary>
    public int PlayerCount => IsLobbyValid ? _currentLobby.Members.Count() : 0;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SubscribeToSteamEvents();
        SubscribeToSceneEvents();
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromSteamEvents();
        UnsubscribeFromSceneEvents();
        UnsubscribeFromNetworkEvents();
    }

    private void Start()
    {
        ValidateSteamConnection();
        InitializeState();
        InitializeUI();
    }

    #endregion

    #region Initialization

    private void ValidateSteamConnection()
    {
        if (!SteamClient.IsValid)
        {
            ShowErrorMessage("Steam bağlantısı yok!");
        }
    }

    private void InitializeState()
    {
        _isLobbyJoinValid = false;
        _isLoadingScene = false;
        _isJoiningLobby = false;
    }

    private void InitializeUI()
    {
        ClearAllPlayerSlots();
        SetStartButtonActive(false);
        SetErrorMessageActive(false);
        SetLoadingScreenActive(false);
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToSteamEvents()
    {
        SteamMatchmaking.OnLobbyCreated += HandleLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyMemberJoined += HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += HandleLobbyMemberLeave;
    }

    private void UnsubscribeFromSteamEvents()
    {
        SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= HandleLobbyMemberLeave;
    }

    private void SubscribeToSceneEvents()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void UnsubscribeFromSceneEvents()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void SubscribeToNetworkEvents()
    {
        if (NetworkManager.Singleton?.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleNetworkSceneEvent;
        }
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (NetworkManager.Singleton?.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleNetworkSceneEvent;
        }
    }

    #endregion

    #region Steam Event Handlers

    private void HandleLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            ShowErrorMessage("Lobi oluşturulamadı!");
            _isLobbyJoinValid = false;
            return;
        }

        _currentLobby = lobby;
        _isLobbyJoinValid = true;

        ConfigureLobby(lobby);
        StartHostForLobby(lobby);
    }

    private void ConfigureLobby(Lobby lobby)
    {
        lobby.SetPublic();
        lobby.SetJoinable(true);
        lobby.SetData(LOBBY_DATA_VERSION_KEY, Application.version);
    }

    private void StartHostForLobby(Lobby lobby)
    {
        if (NetworkManager.Singleton == null)
        {
            InvalidateLobby();
            return;
        }

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
            StartCoroutine(DelayedHostStartCoroutine(lobby));
        }
        else
        {
            ExecuteHostStart(lobby);
        }
    }

    private void HandleLobbyEntered(Lobby lobby)
    {
        if (!_isLobbyJoinValid)
        {
            return;
        }

        _currentLobby = lobby;
        UpdateLobbySaver(lobby);

        if (!ValidateLobbyMembers(lobby))
        {
            return;
        }

        if (!TryStartClient(lobby))
        {
            return;
        }

        TransitionToLobbyUI(lobby);
    }

    private async void HandleGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        try
        {
            var joinResult = await lobby.Join();

            if (joinResult != RoomEnter.Success)
            {
                ShowErrorMessage("Lobiye katılınamadı!");
                _isLobbyJoinValid = false;
            }
            else
            {
                _isLobbyJoinValid = true;
            }
        }
        catch (Exception ex)
        {
            LogError($"Lobby join error: {ex.Message}");
            ShowErrorMessage("Bağlantı hatası!");
            _isLobbyJoinValid = false;
        }
    }

    private void HandleLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        if (!IsValidLobbyEvent(lobby)) return;

        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
        NotifyBreakRoomManager();
    }

    private void HandleLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        if (!IsValidLobbyEvent(lobby)) return;

        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
        NotifyBreakRoomManager();
    }

    private bool IsValidLobbyEvent(Lobby lobby)
    {
        return _isLobbyJoinValid && _currentLobby.Id != 0 && _currentLobby.Id == lobby.Id;
    }

    #endregion

    #region Scene Event Handlers

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsGameScene(scene.name))
        {
            HandleGameSceneLoaded();
        }
        else if (scene.name == MAIN_MENU_SCENE_NAME)
        {
            HandleMainMenuLoaded();
        }
    }

    private void HandleGameSceneLoaded()
    {
        if (_wasInMainMenu || IsFirstGameLoad())
        {
            StartCoroutine(ResetManagersCoroutine());
        }
    }

    private void HandleMainMenuLoaded()
    {
        _wasInMainMenu = true;
        SetLoadingScreenActive(false);
        _isLoadingScene = false;
    }

    private void HandleNetworkSceneEvent(SceneEvent sceneEvent)
    {
        if (!_isLoadingScene) return;

        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.LoadEventCompleted:
            case SceneEventType.LoadComplete:
                StartCoroutine(HideLoadingScreenCoroutine());
                break;

            case SceneEventType.Load:
                UpdateLoadingProgress(INITIAL_LOAD_PROGRESS, "Connecting.. .");
                break;
        }
    }

    private static bool IsGameScene(string sceneName)
    {
        return sceneName == GAME_SCENE_NAME ||
               sceneName.Contains("Game") ||
               sceneName.Contains("Map");
    }

    private static bool IsFirstGameLoad()
    {
        return NewCss.GameStateManager.Instance == null ||
               !NewCss.GameStateManager.Instance.HasGameEverStarted;
    }

    #endregion

    #region Host/Client Management

    private void ExecuteHostStart(Lobby lobby)
    {
        try
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                InvalidateLobby();
                return;
            }

            var transport = GetFacepunchTransport();
            if (transport == null)
            {
                ShowErrorMessage("Network hatası!");
                InvalidateLobby();
                return;
            }

            transport.targetSteamId = lobby.Id;

            if (NetworkManager.Singleton.StartHost())
            {
                _isLobbyJoinValid = true;
                UpdateStartButtonVisibility();
            }
            else
            {
                ShowErrorMessage("Host başlatılamadı!");
                InvalidateLobby();
            }
        }
        catch (Exception ex)
        {
            LogError($"Host start error: {ex.Message}");
            ShowErrorMessage("Host hatası!");
            InvalidateLobby();
        }
    }

    private bool TryStartClient(Lobby lobby)
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
        {
            return true;
        }

        var transport = GetFacepunchTransport();
        if (transport == null)
        {
            ShowErrorMessage("Network hatası!");
            InvalidateLobby();
            return false;
        }

        transport.targetSteamId = lobby.Owner.Id;

        if (!NetworkManager.Singleton.StartClient())
        {
            ShowErrorMessage("Bağlantı başarısız!");
            InvalidateLobby();
            return false;
        }

        return true;
    }

    private FacepunchTransport GetFacepunchTransport()
    {
        return NetworkManager.Singleton?.gameObject.GetComponent<FacepunchTransport>();
    }

    private IEnumerator DelayedHostStartCoroutine(Lobby lobby)
    {
        yield return new WaitForSeconds(HOST_START_DELAY);
        ExecuteHostStart(lobby);
    }

    #endregion

    #region Lobby Validation

    private bool ValidateLobbyMembers(Lobby lobby)
    {
        try
        {
            var members = lobby.Members.ToArray();
            if (members.Length == 0)
            {
                ShowErrorMessage("Geçersiz lobi!");
                InvalidateLobby();
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Lobby validation error: {ex.Message}");
            ShowErrorMessage("Lobi hatası!");
            InvalidateLobby();
            return false;
        }
    }

    private void UpdateLobbySaver(Lobby lobby)
    {
        if (LobbySaver.instance != null)
        {
            LobbySaver.instance.CurrentLobby = lobby;
        }

        if (LobbyID != null)
        {
            LobbyID.text = lobby.Id.ToString();
        }
    }

    #endregion

    #region Public API - Lobby Operations

    /// <summary>
    /// Yeni lobi oluşturur
    /// </summary>
    public async void HostLobby()
    {
        try
        {
            // Loading ekranını göster
            SetLoadingScreenActive(true);
            UpdateLoadingProgress(0.2f, "Lobi oluşturuluyor...");

            await ForceNetworkCleanup();

            UpdateLoadingProgress(0.5f, "Steam'e bağlanılıyor...");

            var lobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);

            if (!lobby.HasValue)
            {
                ShowErrorMessage("Lobi oluşturulamadı!");
                _isLobbyJoinValid = false;
                SetLoadingScreenActive(false);
                return;
            }

            UpdateLoadingProgress(0.8f, "Lobi hazırlanıyor.. .");

            // Kısa bir bekleme - UI'ın güncelenmesi için
            await System.Threading.Tasks.Task.Delay(300);

            UpdateLoadingProgress(1f, "Hazır!");

            // Loading ekranını kapat
            await System.Threading.Tasks.Task.Delay(200);
            SetLoadingScreenActive(false);
        }
        catch (Exception ex)
        {
            LogError($"Host lobby error: {ex.Message}");
            ShowErrorMessage("Lobi hatası!");
            _isLobbyJoinValid = false;
            SetLoadingScreenActive(false);
        }
    }

    /// <summary>
    /// ID ile lobiye katılır
    /// </summary>
    public async void JoinLobbyWithID()
    {
        if (_isJoiningLobby) return;

        if (!ValidateLobbyIdInput(out ulong lobbyId))
        {
            return;
        }

        _isJoiningLobby = true;
        _isLobbyJoinValid = false;

        try
        {
            // Loading ekranını göster
            SetLoadingScreenActive(true);
            UpdateLoadingProgress(0.2f, "Lobiye bağlanılıyor...");

            await PrepareForJoin();

            UpdateLoadingProgress(0.5f, "Lobi aranıyor...");

            await ExecuteJoinLobby(lobbyId);

            UpdateLoadingProgress(1f, "Bağlandı!");

            // Loading ekranını kapat
            await System.Threading.Tasks.Task.Delay(300);
            SetLoadingScreenActive(false);
        }
        catch (Exception ex)
        {
            LogError($"Join lobby error: {ex.Message}");
            ShowErrorMessage("Beklenmeyen hata oluştu!");
            _isLobbyJoinValid = false;
            SetLoadingScreenActive(false);
        }
        finally
        {
            _isJoiningLobby = false;
        }
    }

    /// <summary>
    /// Mevcut lobiden ayrılır
    /// </summary>
    public async void LeaveLobby()
    {
        _isLobbyJoinValid = false;

        await ForceNetworkCleanup();

        ClearAllPlayerSlots();
        SetStartButtonActive(false);
        RefreshUI();
    }

    /// <summary>
    /// Lobi ID'sini panoya kopyalar
    /// </summary>
    public void CopyID()
    {
        if (!IsLobbyValid)
        {
            ShowErrorMessage("Geçerli bir lobi yok!");
            return;
        }

        CopyToClipboard(LobbyID.text);
        ShowErrorMessage("Lobi ID kopyalandı!", 2f);
    }

    /// <summary>
    /// Oyunu başlatır (sadece host)
    /// </summary>
    public void StartGameServer()
    {
        if (!ValidateGameStart()) return;

        DisableStartButton();
        ShowLoadingScreen();
        NotifyClientsGameStartingClientRpc();
        PrepareManagersForSceneLoad();
        StartCoroutine(LoadGameSceneCoroutine());
    }

    #endregion

    #region Join Lobby Helpers

    private bool ValidateLobbyIdInput(out ulong lobbyId)
    {
        lobbyId = 0;

        if (string.IsNullOrEmpty(LobbyIDInputField?.text))
        {
            ShowErrorMessage("Lütfen bir Lobi ID girin!");
            return false;
        }

        if (!ulong.TryParse(LobbyIDInputField.text, out lobbyId))
        {
            ShowErrorMessage("Geçersiz Lobi ID formatı!");
            return false;
        }

        return true;
    }

    private async Task PrepareForJoin()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
        {
            await ForceNetworkCleanup();
        }
    }

    private async Task ExecuteJoinLobby(ulong lobbyId)
    {
        var targetLobby = new Lobby(lobbyId);
        var joinResult = await targetLobby.Join();

        if (joinResult == RoomEnter.Success)
        {
            ValidateAndAcceptJoin(targetLobby);
        }
        else
        {
            HandleJoinFailure(joinResult);
        }
    }

    private void ValidateAndAcceptJoin(Lobby lobby)
    {
        try
        {
            var members = lobby.Members.ToArray();
            if (members.Length > 0)
            {
                _isLobbyJoinValid = true;
            }
            else
            {
                ShowErrorMessage("Geçersiz lobi!");
                _isLobbyJoinValid = false;
                lobby.Leave();
            }
        }
        catch (Exception ex)
        {
            LogError($"Join validation error: {ex.Message}");
            ShowErrorMessage("Lobi doğrulanamadı!");
            _isLobbyJoinValid = false;
            lobby.Leave();
        }
    }

    private void HandleJoinFailure(RoomEnter joinResult)
    {
        _isLobbyJoinValid = false;

        string errorMessage = joinResult switch
        {
            RoomEnter.DoesntExist => "Lobi mevcut değil! ",
            RoomEnter.NotAllowed => "Lobiye girme yetkiniz yok!",
            RoomEnter.Full => "Lobi dolu!",
            RoomEnter.Error => "Bağlantı hatası!",
            RoomEnter.Banned => "Bu lobiden banlandınız!",
            RoomEnter.Limited => "Hesabınız sınırlı!",
            RoomEnter.ClanDisabled => "Klan devre dışı!",
            RoomEnter.CommunityBan => "Topluluk yasağınız var!",
            _ => $"Katılım başarısız: {joinResult}"
        };

        ShowErrorMessage(errorMessage);
    }

    #endregion

    #region Network Cleanup

    private async Task ForceNetworkCleanup()
    {
        _isLobbyJoinValid = false;

        await ShutdownNetworkManager();
        ResetSceneManager();
        await ResetTransport();
        LeaveCurrentLobby();
        ClearLobbySaver();

        await Task.Delay(CLEANUP_FINAL_DELAY_MS);
    }

    private async Task ShutdownNetworkManager()
    {
        if (NetworkManager.Singleton == null) return;

        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient) return;

        NetworkManager.Singleton.Shutdown();

        int waitTime = 0;
        while ((NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient) &&
               waitTime < NETWORK_SHUTDOWN_TIMEOUT_MS)
        {
            await Task.Delay(NETWORK_SHUTDOWN_CHECK_INTERVAL_MS);
            waitTime += NETWORK_SHUTDOWN_CHECK_INTERVAL_MS;
        }
    }

    private void ResetSceneManager()
    {
        try
        {
            NetworkManager.Singleton?.SceneManager?.SetClientSynchronizationMode(LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            LogWarning($"Scene manager reset warning: {ex.Message}");
        }
    }

    private async Task ResetTransport()
    {
        var transport = FindObjectOfType<FacepunchTransport>();
        if (transport == null) return;

        transport.targetSteamId = 0;
        transport.enabled = false;

        await Task.Delay(TRANSPORT_RESET_DELAY_MS);

        transport.enabled = true;
    }

    private void LeaveCurrentLobby()
    {
        if (_currentLobby.Id == 0) return;

        try
        {
            _currentLobby.Leave();
        }
        catch (Exception ex)
        {
            LogWarning($"Leave lobby warning: {ex.Message}");
        }

        _currentLobby = default;
    }

    private void ClearLobbySaver()
    {
        LobbySaver.instance?.ClearLobby();
    }

    #endregion

    #region Player Slot Management

    private void UpdatePlayerSlots()
    {
        ClearAllPlayerSlots();

        if (!IsLobbyValid) return;

        try
        {
            var members = _currentLobby.Members.ToArray();

            if (members.Length == 0)
            {
                InvalidateLobby();
                return;
            }

            bool isHost = NetworkManager.Singleton?.IsHost ?? false;

            for (int i = 0; i < members.Length && i < MAX_PLAYERS; i++)
            {
                var member = members[i];
                bool isOwner = member.Id == _currentLobby.Owner.Id;
                bool showKick = isHost && !isOwner;

                SetSlotOccupied(i, member.Name, showKick, member.Id);
            }
        }
        catch (Exception ex)
        {
            LogError($"Update player slots error: {ex.Message}");
            InvalidateLobby();
        }

        NotifyBreakRoomManager();
    }

    private void SetSlotOccupied(int slotIndex, string playerName, bool showKickButton, ulong steamId)
    {
        if (!IsValidSlotIndex(slotIndex)) return;

        var slot = playerSlots[slotIndex];

        slot.playerNameText.text = playerName;
        slot.backgroundImage.sprite = occupiedSlotSprite;
        slot.backgroundImage.color = UnityColor.white;

        if (slot.kickButton != null)
        {
            slot.kickButton.gameObject.SetActive(showKickButton);
            slot.kickButton.onClick.RemoveAllListeners();
            slot.kickButton.onClick.AddListener(() => KickPlayer(steamId));
        }
    }

    private void SetSlotEmpty(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex)) return;

        var slot = playerSlots[slotIndex];

        slot.playerNameText.text = "Empty";
        slot.backgroundImage.sprite = emptySlotSprite;
        slot.backgroundImage.color = UnityColor.white;

        if (slot.kickButton != null)
        {
            slot.kickButton.gameObject.SetActive(false);
        }
    }

    private void ClearAllPlayerSlots()
    {
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            SetSlotEmpty(i);
        }
    }

    private bool IsValidSlotIndex(int index)
    {
        return index >= 0 && index < playerSlots.Length;
    }

    #endregion

    #region Kick Player

    private void KickPlayer(ulong steamId)
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            ShowErrorMessage("Sadece host oyuncuları kickleyebilir!");
            return;
        }

        try
        {
            var clientId = GetClientIdFromSteamId(steamId);
            if (clientId.HasValue)
            {
                NetworkManager.Singleton.DisconnectClient(clientId.Value);
                ShowErrorMessage("Oyuncu kicklendi!", 2f);
            }
        }
        catch (Exception ex)
        {
            LogError($"Kick player error: {ex.Message}");
            ShowErrorMessage("Kick işlemi başarısız!");
        }
    }

    private ulong? GetClientIdFromSteamId(ulong steamId)
    {
        if (NetworkManager.Singleton == null) return null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            var steamComponent = client.PlayerObject.GetComponent<SteamIdHolder>();
            if (steamComponent != null && steamComponent.SteamId == steamId)
            {
                return client.ClientId;
            }
        }

        return null;
    }

    #endregion

    #region Game Start

    private bool ValidateGameStart()
    {
        if (!IsLobbyValid)
        {
            ShowErrorMessage("Geçerli bir lobi yok!");
            return false;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            ShowErrorMessage("Sadece host oyunu başlatabilir!");
            return false;
        }

        if (NetworkManager.Singleton.SceneManager == null)
        {
            ShowErrorMessage("Network hazır değil!");
            return false;
        }

        return true;
    }

    private void DisableStartButton()
    {
        if (StartGameButton != null)
        {
            StartGameButton.interactable = false;
        }
    }

    private void PrepareManagersForSceneLoad()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Time.timeScale = 1f;
        }
    }

    [ClientRpc]
    private void NotifyClientsGameStartingClientRpc()
    {
        Time.timeScale = 1f;
        ShowLoadingScreen();
    }

    private IEnumerator LoadGameSceneCoroutine()
    {
        UpdateLoadingProgress(INITIAL_LOAD_PROGRESS, "Preparing");
        yield return new WaitForSeconds(SCENE_LOAD_DELAY);

        try
        {
            UpdateLoadingProgress(SCENE_LOAD_START_PROGRESS, "Loading Scene");

            var status = NetworkManager.Singleton.SceneManager.LoadScene(GAME_SCENE_NAME, LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                HandleSceneLoadFailure();
            }
            else
            {
                StartCoroutine(TrackLoadingProgressCoroutine());
            }
        }
        catch (Exception ex)
        {
            LogError($"Scene load error: {ex.Message}");
            HandleSceneLoadFailure();
        }
    }

    private void HandleSceneLoadFailure()
    {
        ShowErrorMessage("Sahne yüklenemedi!");
        SetLoadingScreenActive(false);
        _isLoadingScene = false;

        if (StartGameButton != null)
        {
            StartGameButton.interactable = true;
        }
    }

    #endregion

    #region Loading Screen

    private void ShowLoadingScreen()
    {
        _isLoadingScene = true;
        SetLoadingScreenActive(true);
        UpdateLoadingProgress(0f, "Starting Game");

        _loadingDotsCoroutine = StartCoroutine(AnimateLoadingDotsCoroutine());
    }

    private IEnumerator AnimateLoadingDotsCoroutine()
    {
        int dotCount = 0;

        while (_isLoadingScene)
        {
            dotCount = (dotCount + 1) % 4;
            string dots = new string('.', dotCount);

            if (loadingText != null)
            {
                loadingText.text = $"Loading{dots}";
            }

            yield return new WaitForSeconds(LOADING_DOT_INTERVAL);
        }
    }

    private IEnumerator HideLoadingScreenCoroutine()
    {
        yield return new WaitForSeconds(minimumLoadTime);

        UpdateLoadingProgress(1f, "Ready!");

        yield return new WaitForSeconds(LOADING_COMPLETE_DELAY);

        SetLoadingScreenActive(false);
        _isLoadingScene = false;
    }

    private IEnumerator TrackLoadingProgressCoroutine()
    {
        float progress = SCENE_LOAD_START_PROGRESS;
        float timer = 0f;

        while (_isLoadingScene && progress < MAX_TRACKING_PROGRESS)
        {
            progress += Time.deltaTime * LOADING_PROGRESS_SPEED;
            progress = Mathf.Clamp(progress, SCENE_LOAD_START_PROGRESS, MAX_TRACKING_PROGRESS);
            timer += Time.deltaTime;

            UpdateLoadingProgress(progress, "Loading");

            yield return null;
        }

        while (timer < minimumLoadTime && _isLoadingScene)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void UpdateLoadingProgress(float progress, string text = null)
    {
        if (loadingProgressBar != null)
        {
            loadingProgressBar.value = progress;
        }

        if (loadingText != null && !string.IsNullOrEmpty(text))
        {
            loadingText.text = text;
        }
    }

    #endregion

    #region UI Management

    private void TransitionToLobbyUI(Lobby lobby)
    {
        if (MainMenu != null) MainMenu.SetActive(false);
        if (InLobbyMenu != null) InLobbyMenu.SetActive(true);

        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
        NotifyBreakRoomManager();
    }

    private void UpdateStartButtonVisibility()
    {
        if (StartGameButton == null) return;

        if (!IsLobbyValid || NetworkManager.Singleton == null)
        {
            SetStartButtonActive(false);
            return;
        }

        SetStartButtonActive(NetworkManager.Singleton.IsHost);
    }

    private void RefreshUI()
    {
        if (!IsLobbyValid || LobbySaver.instance?.CurrentLobby == null)
        {
            if (MainMenu != null) MainMenu.SetActive(true);
            if (InLobbyMenu != null) InLobbyMenu.SetActive(false);
            SetStartButtonActive(false);
        }
        else
        {
            if (MainMenu != null) MainMenu.SetActive(false);
            if (InLobbyMenu != null) InLobbyMenu.SetActive(true);
            UpdateStartButtonVisibility();
        }
    }

    private void InvalidateLobby()
    {
        _isLobbyJoinValid = false;

        ClearAllPlayerSlots();

        if (MainMenu != null) MainMenu.SetActive(true);
        if (InLobbyMenu != null) InLobbyMenu.SetActive(false);

        SetStartButtonActive(false);
        LeaveCurrentLobby();

        LobbySaver.instance?.ForceClearLobby();
    }

    private void SetStartButtonActive(bool active)
    {
        if (StartGameButton != null)
        {
            StartGameButton.gameObject.SetActive(active);
        }
    }

    private void SetErrorMessageActive(bool active)
    {
        if (ErrorMessageText != null)
        {
            ErrorMessageText.gameObject.SetActive(active);
        }
    }

    private void SetLoadingScreenActive(bool active)
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(active);
        }
    }

    #endregion

    #region Error Messages

    private void ShowErrorMessage(string message, float duration = -1)
    {
        if (ErrorMessageText == null) return;

        ErrorMessageText.text = message;
        ErrorMessageText.gameObject.SetActive(true);

        float displayDuration = duration > 0 ? duration : ErrorMessageDuration;

        if (_errorMessageCoroutine != null)
        {
            StopCoroutine(_errorMessageCoroutine);
        }

        _errorMessageCoroutine = StartCoroutine(HideErrorMessageCoroutine(displayDuration));
    }

    private IEnumerator HideErrorMessageCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        SetErrorMessageActive(false);
        _errorMessageCoroutine = null;
    }

    #endregion

    #region Break Room Notification

    private void NotifyBreakRoomManager()
    {
        if (!IsLobbyValid) return;

        try
        {
            var members = _currentLobby.Members?.ToArray();
            if (members == null || members.Length == 0) return;

            var playerNames = members.Select(m => m.Name).ToList();

            NewCss.BreakRoomManager.Instance?.UpdateLobbyPlayers(playerNames);
        }
        catch (Exception ex)
        {
            LogError($"BreakRoom notification error: {ex.Message}");
        }
    }

    #endregion

    #region Manager Reset

    private IEnumerator ResetManagersCoroutine()
    {
        yield return new WaitForSeconds(MANAGERS_RESET_DELAY);

        NewCss.GameStateManager.Instance?.ResetGameState();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NewCss.DayCycleManager.Instance?.ResetDayCycle();
        }

        _wasInMainMenu = false;
    }

    #endregion

    #region Utility

    private static void CopyToClipboard(string text)
    {
        var textEditor = new TextEditor { text = text };
        textEditor.SelectAll();
        textEditor.Copy();
    }

    private static void LogError(string message)
    {
        Debug.LogError($"{LOG_PREFIX} {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"{LOG_PREFIX} {message}");
    }

    #endregion
}