using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

namespace NewCss
{
    public class BoxingMinigameManager : NetworkBehaviour
    {
        #region Constants

        private const int REQUIRED_SEQUENCE_LENGTH = 3;
        private const float MINIGAME_START_DELAY = 0.1f;
        private const float NEXT_KEY_DELAY = 0.3f;
        private const float FAILURE_UI_HIDE_DELAY = 1.5f;

        #endregion

        #region Serialized Fields

        [Header("=== MINIGAME SETTINGS ===")]
        [SerializeField] private int sequenceLength = REQUIRED_SEQUENCE_LENGTH;

        [Header("=== FADE SETTINGS ===")]
        [SerializeField, Range(0.05f, 1f)] private float keyFadeInDuration = 0.15f;
        [SerializeField, Range(0.05f, 1f)] private float keyFadeOutDuration = 0.15f;
        [SerializeField, Range(0.1f, 1f)] private float nextKeyDelay = 0.3f;

        [Header("=== AUDIO ===")]
        [SerializeField] private AudioClip correctSound;
        [SerializeField] private AudioClip wrongSound;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip failSound;

        [Header("=== REFERENCES ===")]
        [SerializeField] private BoxingUIController uiController;
        [SerializeField] private Table parentTable;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private AudioSource _audioSource;

        // Minigame state
        private List<KeyCode> _currentSequence = new List<KeyCode>();
        private int _currentIndex = 0;
        private bool _isWaitingForInput = false;

        // ✅ Client-side: "Ben minigame'deyim mi?"
        private bool _isLocalPlayerInMinigame = false;
        private ulong _myClientId;

        private BoxInfo.BoxType _currentBoxType;
        private ItemData _currentItemData;

        private static readonly KeyCode[] PossibleKeys = new KeyCode[]
        {
            KeyCode. UpArrow,
            KeyCode.DownArrow,
            KeyCode. LeftArrow,
            KeyCode.RightArrow
        };

        #endregion

        #region Public Properties

        public bool IsMinigameActive => _isLocalPlayerInMinigame;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeAudioSource();
            ValidateSequenceLength();
        }

        private void Update()
        {
            ProcessInput();
        }

        #endregion

        #region Initialization

        private void InitializeAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.5f;
        }

        private void ValidateSequenceLength()
        {
            if (sequenceLength != REQUIRED_SEQUENCE_LENGTH)
            {
                LogWarning($"Sequence length forced to {REQUIRED_SEQUENCE_LENGTH}");
                sequenceLength = REQUIRED_SEQUENCE_LENGTH;
            }
        }

        #endregion

        #region Input Processing

        private void ProcessInput()
        {
            // ✅ Phone gibi: Sadece minigame'deki local player
            if (!_isLocalPlayerInMinigame || !_isWaitingForInput) return;

            foreach (KeyCode key in PossibleKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    LogDebug($"🎮 Key pressed: {key}");
                    _isWaitingForInput = false; // Double-press önle
                    HandleInput(key);
                    break;
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Minigame'i başlatır - Table tarafından çağrılır
        /// </summary>
        public void StartMinigame(PlayerInventory player, BoxInfo.BoxType boxType, ItemData itemData)
        {
            if (player == null || itemData == null)
            {
                LogError("Player or ItemData is null!");
                return;
            }

            LogDebug($"🎮 StartMinigame - Player: {player.OwnerClientId}, BoxType: {boxType}");

            // Server'a istek gönder
            RequestStartMinigameServerRpc(player.OwnerClientId, (int)boxType, itemData.itemID);
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void RequestStartMinigameServerRpc(ulong playerClientId, int boxTypeInt, int itemDataID, ServerRpcParams rpcParams = default)
        {
            // ✅ Phone gibi: Kim gönderdi? 
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            LogDebug($"📥 SERVER: Minigame requested by client {senderClientId} for player {playerClientId}");

            // Unique sequence oluştur
            GenerateUniqueSequence();

            KeyCode[] sequenceArray = _currentSequence.ToArray();
            LogDebug($"✅ SERVER: Sequence: {string.Join(", ", _currentSequence)}");

            // ✅ HERKESE gönder, ama sadece doğru client işleyecek
            OnMinigameStartedClientRpc(playerClientId, boxTypeInt, itemDataID, sequenceArray);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportSuccessServerRpc(int boxTypeInt, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            LogDebug($"📥 SERVER: Success from client {senderClientId}");

            // Player'ı bul
            if (TryGetPlayer(senderClientId, out PlayerInventory player))
            {
                if (parentTable != null)
                {
                    parentTable.CompleteBoxingSuccess(player, (BoxInfo.BoxType)boxTypeInt);
                }
            }

            // Herkese bittiğini bildir
            OnMinigameEndedClientRpc(senderClientId, true);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportFailureServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            LogDebug($"📥 SERVER: Failure from client {senderClientId}");

            if (TryGetPlayer(senderClientId, out PlayerInventory player))
            {
                if (parentTable != null)
                {
                    parentTable.CompleteBoxingFailure(player);
                }
            }

            OnMinigameEndedClientRpc(senderClientId, false);
        }

        #endregion

        #region Client RPCs

        /// <summary>
        /// ✅ Phone gibi: Herkese gidiyor, ama sadece doğru client işliyor
        /// </summary>
        [ClientRpc]
        private void OnMinigameStartedClientRpc(ulong targetClientId, int boxTypeInt, int itemDataID, KeyCode[] sequence)
        {
            // ✅ Phone gibi: Ben miyim?
            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            LogDebug($"📥 CLIENT {localClientId}: Received minigame start - Target: {targetClientId}");

            if (targetClientId != localClientId)
            {
                LogDebug($"⏩ CLIENT {localClientId}: Not my minigame, skipping");
                return;
            }

            // ✅ EVET, BENİM! 
            LogDebug($"🎮 CLIENT {localClientId}: Starting MY minigame!");

            _myClientId = localClientId;
            _isLocalPlayerInMinigame = true;
            _currentSequence = new List<KeyCode>(sequence);
            _currentIndex = 0;
            _currentBoxType = (BoxInfo.BoxType)boxTypeInt;
            _currentItemData = GetItemDataFromID(itemDataID);

            // ✅ Phone gibi: Kendi movement'ımı kilitle
            LockLocalPlayerMovement(true);

            // UI başlat
            if (uiController != null)
            {
                uiController.SetFadeDurations(keyFadeInDuration, keyFadeOutDuration);
                uiController.ShowUI(_currentBoxType);
            }

            StartCoroutine(StartMinigameCoroutine());
        }

        /// <summary>
        /// ✅ Phone gibi: Herkese gidiyor, doğru client unlock yapıyor
        /// </summary>
        [ClientRpc]
        private void OnMinigameEndedClientRpc(ulong targetClientId, bool success)
        {
            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            LogDebug($"📥 CLIENT {localClientId}: Minigame ended - Target: {targetClientId}, Success: {success}");

            if (targetClientId != localClientId)
            {
                return;
            }

            // ✅ Benim minigame'im bitti
            LogDebug($"🏁 CLIENT {localClientId}: MY minigame ended!");

            PlaySound(success ? successSound : failSound);

            if (!success && uiController != null)
            {
                uiController.ShowFailure();
                StartCoroutine(HideUIDelayedCoroutine(FAILURE_UI_HIDE_DELAY));
            }
            else if (uiController != null)
            {
                uiController.HideUI();
            }

            // ✅ Phone gibi: Kendi movement'ımı aç
            LockLocalPlayerMovement(false);

            // State temizle
            CleanupMinigame();

            if (success)
            {
                NotifyTutorialMinigameComplete();
            }
        }

        #endregion

        #region Sequence Generation

        private void GenerateUniqueSequence()
        {
            _currentSequence.Clear();
            _currentIndex = 0;

            List<KeyCode> availableKeys = new List<KeyCode>(PossibleKeys);

            for (int i = 0; i < REQUIRED_SEQUENCE_LENGTH; i++)
            {
                int randomIndex = Random.Range(0, availableKeys.Count);
                KeyCode selectedKey = availableKeys[randomIndex];
                _currentSequence.Add(selectedKey);
                availableKeys.RemoveAt(randomIndex);
            }

            LogDebug($"🔑 Generated UNIQUE sequence: {string.Join(", ", _currentSequence)}");
        }

        #endregion

        #region Minigame Flow

        private IEnumerator StartMinigameCoroutine()
        {
            yield return new WaitForSeconds(MINIGAME_START_DELAY);

            LogDebug("⏳ Showing first key...");

            if (uiController != null)
            {
                uiController.ShowInputPrompt();
                uiController.ShowKey(_currentSequence[_currentIndex]);
            }

            _isWaitingForInput = true;
            LogDebug($"✅ Input ENABLED - Waiting for key {_currentIndex + 1}/{REQUIRED_SEQUENCE_LENGTH}");
        }

        private void HandleInput(KeyCode pressedKey)
        {
            KeyCode expectedKey = _currentSequence[_currentIndex];

            LogDebug($"🎯 Key {_currentIndex + 1}/{REQUIRED_SEQUENCE_LENGTH} - Expected: {expectedKey}, Pressed: {pressedKey}");

            if (pressedKey == expectedKey)
            {
                OnCorrectKey();
            }
            else
            {
                OnWrongKey();
            }
        }

        private void OnCorrectKey()
        {
            PlaySound(correctSound);

            if (uiController != null)
            {
                uiController.ShowFeedback(true, _currentIndex);
            }

            _currentIndex++;

            if (_currentIndex >= REQUIRED_SEQUENCE_LENGTH)
            {
                LogDebug("✅ All keys correct! Reporting to server...");
                ReportSuccessServerRpc((int)_currentBoxType);
            }
            else
            {
                StartCoroutine(ShowNextKeyCoroutine());
            }
        }

        private void OnWrongKey()
        {
            PlaySound(wrongSound);

            if (uiController != null)
            {
                uiController.ShowFeedback(false, _currentIndex);
            }

            LogDebug($"❌ Wrong key! Reporting to server...");
            ReportFailureServerRpc();
        }

        private IEnumerator ShowNextKeyCoroutine()
        {
            if (uiController != null)
            {
                uiController.HideKey();
            }

            yield return new WaitForSeconds(nextKeyDelay);

            if (uiController != null && _currentIndex < _currentSequence.Count)
            {
                LogDebug($"➡️ Showing key {_currentIndex + 1}/{REQUIRED_SEQUENCE_LENGTH}");
                uiController.ShowKey(_currentSequence[_currentIndex]);
            }

            _isWaitingForInput = true;
        }

        private IEnumerator HideUIDelayedCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (uiController != null)
            {
                uiController.HideUI();
            }
        }

        #endregion

        #region Player Movement Lock

        /// <summary>
        /// ✅ Phone'dan kopyalandı: Local player'ın movement'ını kilitle/aç
        /// </summary>
        private void LockLocalPlayerMovement(bool locked)
        {
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            LogDebug($"LockLocalPlayerMovement - Client {localClientId}, Locked: {locked}");

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(localClientId, out var client))
            {
                LogWarning($"Client {localClientId} not found!");
                return;
            }

            if (client.PlayerObject == null)
            {
                LogWarning($"PlayerObject is null for client {localClientId}!");
                return;
            }

            var playerMovement = client.PlayerObject.GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
                playerMovement = client.PlayerObject.GetComponentInChildren<PlayerMovement>();
            }

            if (playerMovement != null)
            {
                playerMovement.LockMovement(locked);
                playerMovement.LockAllInteractions(locked);
                LogDebug($"✅ Client {localClientId} - Movement locked: {locked}");
            }
            else
            {
                LogError($"❌ Client {localClientId} - PlayerMovement not found!");
            }
        }

        #endregion

        #region Cleanup

        private void CleanupMinigame()
        {
            _isLocalPlayerInMinigame = false;
            _isWaitingForInput = false;
            _currentSequence.Clear();
            _currentIndex = 0;
        }

        #endregion

        #region Helpers

        private bool TryGetPlayer(ulong clientId, out PlayerInventory player)
        {
            player = null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                return false;
            }

            if (client.PlayerObject == null)
            {
                return false;
            }

            player = client.PlayerObject.GetComponent<PlayerInventory>();
            return player != null;
        }

        private ItemData GetItemDataFromID(int itemID)
        {
            ItemData[] allItems = Resources.LoadAll<ItemData>("Items");
            foreach (ItemData item in allItems)
            {
                if (item.itemID == itemID)
                {
                    return item;
                }
            }
            return null;
        }

        private void NotifyTutorialMinigameComplete()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnMinigameCompleted();
                LogDebug("📚 Tutorial notified!");
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[BoxingMinigame] {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[BoxingMinigame] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[BoxingMinigame] {message}");
        }

        #endregion
    }
}