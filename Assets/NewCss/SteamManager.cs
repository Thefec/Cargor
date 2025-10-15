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
    private bool isLobbyJoinValid = false;

    [System.Serializable]
    public class PlayerSlot
    {
        public TextMeshProUGUI playerNameText;
        public UnityEngine.UI.Image backgroundImage;
        public Button kickButton;
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
        if (scene.name == "Map1" || scene.name.Contains("Game") || scene.name.Contains("Map"))
        {
            if (wasInMainMenu || IsFirstGameLoad())
            {
                StartCoroutine(ResetManagersAfterSceneLoad());
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

        if (NewCss.GameStateManager.Instance != null)
        {
            NewCss.GameStateManager.Instance.ResetGameState();
        }

        if (NewCss.DayCycleManager.Instance != null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NewCss.DayCycleManager.Instance.ResetDayCycle();
            }
        }
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        if (!isLobbyJoinValid || CurrentLobby.Id == 0 || CurrentLobby.Id != lobby.Id)
        {
            return;
        }

        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        if (!isLobbyJoinValid || CurrentLobby.Id == 0 || CurrentLobby.Id != lobby.Id)
        {
            return;
        }

        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
    }

    private void UpdateStartButtonVisibility()
    {
        if (StartGameButton == null) return;

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
    }

    private void UpdatePlayerSlots()
    {
        if (!isLobbyJoinValid || CurrentLobby.Id == 0)
        {
            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                SetSlotEmpty(i);
            }
            return;
        }

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            SetSlotEmpty(i);
        }

        try
        {
            var members = CurrentLobby.Members.ToArray();

            if (members.Length == 0)
            {
                InvalidateLobby();
                return;
            }

            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

            for (int i = 0; i < members.Length && i < MAX_PLAYERS; i++)
            {
                var member = members[i];
                bool isOwner = member.Id == CurrentLobby.Owner.Id;

                SetSlotOccupied(i, member.Name, isHost && !isOwner, member.Id);
            }
        }
        catch (System.Exception ex)
        {
            InvalidateLobby();
        }
    }

    private void SetSlotOccupied(int slotIndex, string playerName, bool showKickButton, ulong steamId)
    {
        if (slotIndex >= 0 && slotIndex < playerSlots.Length)
        {
            playerSlots[slotIndex].playerNameText.text = playerName;
            playerSlots[slotIndex].backgroundImage.sprite = occupiedSlotSprite;
            playerSlots[slotIndex].backgroundImage.color = UnityEngine.Color.white;

            if (playerSlots[slotIndex].kickButton != null)
            {
                playerSlots[slotIndex].kickButton.gameObject.SetActive(showKickButton);
                playerSlots[slotIndex].kickButton.onClick.RemoveAllListeners();
                playerSlots[slotIndex].kickButton.onClick.AddListener(() => KickPlayer(steamId));
            }
        }
    }

    private void SetSlotEmpty(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < playerSlots.Length)
        {
            playerSlots[slotIndex].playerNameText.text = "Empty";
            playerSlots[slotIndex].backgroundImage.sprite = emptySlotSprite;
            playerSlots[slotIndex].backgroundImage.color = UnityEngine.Color.white;

            if (playerSlots[slotIndex].kickButton != null)
            {
                playerSlots[slotIndex].kickButton.gameObject.SetActive(false);
            }
        }
    }

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
        catch (System.Exception ex)
        {
            ShowErrorMessage("Kick işlemi başarısız!");
        }
    }

    private ulong? GetClientIdFromSteamId(ulong steamId)
    {
        if (NetworkManager.Singleton == null) return null;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                var steamComponent = client.PlayerObject.GetComponent<SteamIdHolder>();
                if (steamComponent != null && steamComponent.SteamId == steamId)
                {
                    return client.ClientId;
                }
            }
        }

        return null;
    }

    private async void GameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        try
        {
            var joinResult = await lobby.Join();

            if (joinResult != RoomEnter.Success)
            {
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
            ShowErrorMessage("Bağlantı hatası!");
            isLobbyJoinValid = false;
        }
    }

    private void LobbyCreated(Result result, Lobby lobby)
    {
        if (result == Result.OK)
        {
            CurrentLobby = lobby;
            isLobbyJoinValid = true;

            lobby.SetPublic();
            lobby.SetJoinable(true);
            lobby.SetData("game_version", Application.version);

            if (NetworkManager.Singleton == null)
            {
                InvalidateLobby();
                return;
            }

            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
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
            ShowErrorMessage("Lobi oluşturulamadı!");
            isLobbyJoinValid = false;
        }
    }

    private void StartHost(Lobby lobby)
    {
        try
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                InvalidateLobby();
                return;
            }

            var transport = NetworkManager.Singleton.gameObject.GetComponent<FacepunchTransport>();
            if (transport == null)
            {
                ShowErrorMessage("Network hatası!");
                InvalidateLobby();
                return;
            }

            transport.targetSteamId = lobby.Id;

            bool hostStarted = NetworkManager.Singleton.StartHost();
            if (hostStarted)
            {
                isLobbyJoinValid = true;
                UpdateStartButtonVisibility();
            }
            else
            {
                ShowErrorMessage("Host başlatılamadı!");
                InvalidateLobby();
            }
        }
        catch (System.Exception ex)
        {
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
        if (!isLobbyJoinValid)
        {
            return;
        }

        CurrentLobby = lobby;
        LobbySaver.instance.CurrentLobby = lobby;
        LobbyID.text = lobby.Id.ToString();

        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
        {
            var transport = NetworkManager.Singleton.gameObject.GetComponent<FacepunchTransport>();
            transport.targetSteamId = lobby.Owner.Id;

            bool clientStarted = NetworkManager.Singleton.StartClient();

            if (!clientStarted)
            {
                ShowErrorMessage("Bağlantı başarısız!");
                InvalidateLobby();
                return;
            }
        }

        try
        {
            var members = lobby.Members.ToArray();
            if (members.Length == 0)
            {
                ShowErrorMessage("Geçersiz lobi!");
                InvalidateLobby();
                return;
            }
        }
        catch (System.Exception ex)
        {
            ShowErrorMessage("Lobi hatası!");
            InvalidateLobby();
            return;
        }

        MainMenu.SetActive(false);
        InLobbyMenu.SetActive(true);
        UpdatePlayerSlots();
        UpdateStartButtonVisibility();
    }

    public async void HostLobby()
    {
        try
        {
            await ForceNetworkCleanup();

            var lobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
            if (!lobby.HasValue)
            {
                ShowErrorMessage("Lobi oluşturulamadı!");
                isLobbyJoinValid = false;
            }
        }
        catch (System.Exception ex)
        {
            ShowErrorMessage("Lobi hatası!");
            isLobbyJoinValid = false;
        }
    }

    private async System.Threading.Tasks.Task ForceNetworkCleanup()
    {
        isLobbyJoinValid = false;

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();

                int waitTime = 0;
                while ((NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient) && waitTime < 5000)
                {
                    await System.Threading.Tasks.Task.Delay(100);
                    waitTime += 100;
                }
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
            }
        }

        var transport = FindObjectOfType<FacepunchTransport>();
        if (transport != null)
        {
            transport.targetSteamId = 0;
            transport.enabled = false;
            await System.Threading.Tasks.Task.Delay(100);
            transport.enabled = true;
        }

        if (CurrentLobby.Id != 0)
        {
            try
            {
                CurrentLobby.Leave();
            }
            catch (System.Exception ex)
            {
            }
            CurrentLobby = default(Lobby);
        }

        if (LobbySaver.instance != null)
        {
            LobbySaver.instance.ClearLobby();
        }

        await System.Threading.Tasks.Task.Delay(500);
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

    private void InvalidateLobby()
    {
        isLobbyJoinValid = false;

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            SetSlotEmpty(i);
        }

        MainMenu.SetActive(true);
        InLobbyMenu.SetActive(false);

        if (StartGameButton != null)
        {
            StartGameButton.gameObject.SetActive(false);
        }

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
        isLobbyJoinValid = false;

        try
        {
            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
            {
                await ForceNetworkCleanup();
            }

            Lobby targetLobby = new Lobby(lobbyID);

            var joinResult = await targetLobby.Join();

            if (joinResult == RoomEnter.Success)
            {
                try
                {
                    var members = targetLobby.Members.ToArray();
                    if (members.Length > 0)
                    {
                        isLobbyJoinValid = true;
                    }
                    else
                    {
                        ShowErrorMessage("Geçersiz lobi!");
                        isLobbyJoinValid = false;
                        targetLobby.Leave();
                    }
                }
                catch (System.Exception ex)
                {
                    ShowErrorMessage("Lobi doğrulanamadı!");
                    isLobbyJoinValid = false;
                    targetLobby.Leave();
                }
            }
            else
            {
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

                ShowErrorMessage(errorMessage);
            }
        }
        catch (System.Exception ex)
        {
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
            ShowErrorMessage("Sadece host oyunu başlatabilir!");
            return;
        }

        if (NetworkManager.Singleton.SceneManager == null)
        {
            ShowErrorMessage("Network hazır değil!");
            return;
        }

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
        if (NetworkManager.Singleton.IsServer)
        {
            Time.timeScale = 1f;
        }
    }

    [ClientRpc]
    private void NotifyClientsGameStartingClientRpc()
    {
        Time.timeScale = 1f;
    }

    private System.Collections.IEnumerator DelayedSceneLoad()
    {
        yield return new WaitForSeconds(0.5f);

        try
        {
            var sceneLoadStatus = NetworkManager.Singleton.SceneManager.LoadScene("Map1", LoadSceneMode.Single);

            if (sceneLoadStatus != SceneEventProgressStatus.Started)
            {
                ShowErrorMessage("Sahne yüklenemedi!");

                if (StartGameButton != null)
                {
                    StartGameButton.interactable = true;
                }
            }
        }
        catch (System.Exception ex)
        {
            ShowErrorMessage("Yükleme hatası!");

            if (StartGameButton != null)
            {
                StartGameButton.interactable = true;
            }
        }
    }
}