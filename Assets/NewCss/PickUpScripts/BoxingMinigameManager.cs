using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kutu paketleme minigame yöneticisi. 
    /// Server-authoritative tuş sırası doğrulama ve network senkronizasyonu sağlar.
    /// </summary>
    public class BoxingMinigameManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[BoxingMinigame]";
        private const int REQUIRED_SEQUENCE_LENGTH = 3;
        private const float UI_SHOW_DELAY = 0.1f;
        private const float INPUT_WAIT_DELAY = 0.2f;
        private const float FAILURE_UI_HIDE_DELAY = 1.5f;
        private const float AUDIO_SPATIAL_BLEND = 0.5f;

        #endregion

        #region Serialized Fields - Settings

        [Header("=== MINIGAME SETTINGS ===")]
        [SerializeField, Tooltip("Sıra uzunluğu (zorunlu 3)")]
        private int sequenceLength = REQUIRED_SEQUENCE_LENGTH;

        [Header("=== TIMING SETTINGS ===")]
        [SerializeField, Tooltip("Tuş gösterim gecikmesi")]
        private float keyDisplayDelay = 1f;

        [SerializeField, Tooltip("Tuş fade in süresi")]
        private float keyFadeInDuration = 0.15f;

        [SerializeField, Tooltip("Tuş fade out süresi")]
        private float keyFadeOutDuration = 0.15f;

        [SerializeField, Tooltip("Geri bildirim gösterim süresi")]
        private float feedbackDisplayTime = 0.3f;

        #endregion

        #region Serialized Fields - Audio

        [Header("=== AUDIO ===")]
        [SerializeField, Tooltip("Doğru tuş sesi")]
        private AudioClip correctSound;

        [SerializeField, Tooltip("Yanlış tuş sesi")]
        private AudioClip wrongSound;

        [SerializeField, Tooltip("Başarı sesi")]
        private AudioClip successSound;

        [SerializeField, Tooltip("Başarısızlık sesi")]
        private AudioClip failSound;

        #endregion

        #region Serialized Fields - References

        [Header("=== REFERENCES ===")]
        [SerializeField, Tooltip("UI Controller")]
        private BoxingUIController uiController;

        [SerializeField, Tooltip("Parent Table referansı")]
        private Table parentTable;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields - Components

        private AudioSource _audioSource;

        #endregion

        #region Private Fields - Player State

        private PlayerInventory _currentPlayer;
        private PlayerMovement _currentPlayerMovement;
        private ulong _currentPlayerClientId;

        #endregion

        #region Private Fields - Minigame State

        private readonly List<KeyCode> _currentSequence = new();
        private int _currentIndex;
        private bool _isActive;
        private bool _isWaitingForInput;
        private BoxInfo.BoxType _currentBoxType;
        private ItemData _currentItemData;

        #endregion

        #region Private Fields - Cached

        private static readonly KeyCode[] PossibleKeys =
        {
            KeyCode. UpArrow,
            KeyCode.DownArrow,
            KeyCode.LeftArrow,
            KeyCode.RightArrow
        };

        // ItemData cache
        private ItemData[] _cachedItemData;
        private bool _isItemDataCached;

        #endregion

        #region Public Properties

        /// <summary>
        /// Minigame aktif mi?
        /// </summary>
        public bool IsMinigameActive => _isActive;

        /// <summary>
        /// Input bekleniyor mu?
        /// </summary>
        public bool IsWaitingForInput => _isWaitingForInput;

        /// <summary>
        /// Mevcut adım (0-2)
        /// </summary>
        public int CurrentStep => _currentIndex;

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

        private void OnDestroy()
        {
            ClearItemDataCache();
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
            _audioSource.spatialBlend = AUDIO_SPATIAL_BLEND;
        }

        private void ValidateSequenceLength()
        {
            if (sequenceLength != REQUIRED_SEQUENCE_LENGTH)
            {
                LogWarning($"Sequence length is {sequenceLength}!  Forcing to {REQUIRED_SEQUENCE_LENGTH}.");
                sequenceLength = REQUIRED_SEQUENCE_LENGTH;
            }
        }

        #endregion

        #region Input Processing

        private void ProcessInput()
        {
            if (!CanProcessInput()) return;

            foreach (KeyCode key in PossibleKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    LogDebug($"🎮 CLIENT {NetworkManager.Singleton.LocalClientId}: Key pressed: {key}");
                    HandleInputServerRpc(key);
                    break;
                }
            }
        }

        private bool CanProcessInput()
        {
            if (!_isActive || !_isWaitingForInput) return false;
            if (NetworkManager.Singleton == null) return false;
            if (_currentPlayer == null || !_currentPlayer.IsOwner) return false;

            return true;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Minigame'i başlatır (CLIENT tarafından çağrılır)
        /// </summary>
        public void StartMinigame(PlayerInventory player, BoxInfo.BoxType boxType, ItemData itemData)
        {
            if (player == null)
            {
                LogError("Player is null!");
                return;
            }

            if (itemData == null)
            {
                LogError("ItemData is null!");
                return;
            }

            LogDebug($"🎮 CLIENT {NetworkManager.Singleton.LocalClientId}: Requesting minigame start");

            RequestStartMinigameServerRpc(player.OwnerClientId, (int)boxType, itemData.itemID);
        }

        /// <summary>
        /// Minigame'i zorla durdurur
        /// </summary>
        public void ForceStop()
        {
            if (!IsServer) return;

            ResetMinigameState();
            ForceStopClientRpc(_currentPlayerClientId);
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void RequestStartMinigameServerRpc(ulong playerClientId, int boxTypeInt, int itemDataID, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            LogDebug($"📥 SERVER: Minigame start requested by client {senderClientId} for player {playerClientId}");

            if (!ValidateStartRequest(playerClientId, itemDataID, out PlayerInventory player, out ItemData itemData))
            {
                return;
            }

            InitializeMinigameState(player, playerClientId, (BoxInfo.BoxType)boxTypeInt, itemData);
            GenerateUniqueSequence();

            LogDebug($"✅ SERVER: Minigame started - Sequence: {string.Join(", ", _currentSequence)}");

            StartMinigameClientRpc(playerClientId, boxTypeInt, _currentSequence[0]);
        }

        [ServerRpc(RequireOwnership = false)]
        private void HandleInputServerRpc(KeyCode pressedKey, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            LogDebug($"📥 SERVER: Input received - Key: {pressedKey}, Sender: {senderClientId}");

            if (!ValidateInput(senderClientId))
            {
                return;
            }

            ProcessKeyInput(pressedKey);
        }

        #endregion

        #region Server - Validation

        private bool ValidateStartRequest(ulong playerClientId, int itemDataID, out PlayerInventory player, out ItemData itemData)
        {
            player = null;
            itemData = null;

            // Check if already active
            if (_isActive)
            {
                LogWarning("⚠️ SERVER: Minigame already active!");
                return false;
            }

            // Find player
            if (!TryGetPlayer(playerClientId, out player))
            {
                LogError($"❌ SERVER: Player {playerClientId} not found!");
                return false;
            }

            // Find ItemData
            itemData = GetItemDataFromID(itemDataID);
            if (itemData == null)
            {
                LogError($"❌ SERVER: ItemData {itemDataID} not found!");
                return false;
            }

            return true;
        }

        private bool ValidateInput(ulong senderClientId)
        {
            // Check sender
            if (senderClientId != _currentPlayerClientId)
            {
                LogWarning($"❌ SERVER: Input from wrong client ({senderClientId} != {_currentPlayerClientId})");
                return false;
            }

            // Check state
            if (!_isActive || !_isWaitingForInput)
            {
                LogWarning($"❌ SERVER: Minigame not ready (active: {_isActive}, waiting: {_isWaitingForInput})");
                return false;
            }

            // Check sequence
            if (_currentSequence.Count == 0)
            {
                LogError("❌ SERVER: Sequence is empty!");
                return false;
            }

            return true;
        }

        private bool TryGetPlayer(ulong playerClientId, out PlayerInventory player)
        {
            player = null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(playerClientId, out var client))
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

        #endregion

        #region Server - Game Logic

        private void InitializeMinigameState(PlayerInventory player, ulong clientId, BoxInfo.BoxType boxType, ItemData itemData)
        {
            _currentPlayer = player;
            _currentPlayerMovement = player.GetComponent<PlayerMovement>();
            _currentPlayerClientId = clientId;
            _currentBoxType = boxType;
            _currentItemData = itemData;

            _isActive = true;
            _isWaitingForInput = false;
            _currentIndex = 0;
        }

        private void GenerateUniqueSequence()
        {
            _currentSequence.Clear();
            _currentIndex = 0;

            var availableKeys = new List<KeyCode>(PossibleKeys);

            for (int i = 0; i < REQUIRED_SEQUENCE_LENGTH; i++)
            {
                int randomIndex = Random.Range(0, availableKeys.Count);
                _currentSequence.Add(availableKeys[randomIndex]);
                availableKeys.RemoveAt(randomIndex);
            }

            LogDebug($"🔑 SERVER: Generated sequence: {string.Join(", ", _currentSequence)}");
        }

        private void ProcessKeyInput(KeyCode pressedKey)
        {
            KeyCode expectedKey = _currentSequence[_currentIndex];

            LogDebug($"🎯 SERVER: Key {_currentIndex + 1}/{REQUIRED_SEQUENCE_LENGTH} - Expected: {expectedKey}, Pressed: {pressedKey}");

            if (pressedKey == expectedKey)
            {
                HandleCorrectKey();
            }
            else
            {
                HandleWrongKey();
            }
        }

        private void HandleCorrectKey()
        {
            OnCorrectKeyClientRpc(_currentIndex);
            _currentIndex++;

            if (_currentIndex >= REQUIRED_SEQUENCE_LENGTH)
            {
                LogDebug("✅ SERVER: All keys correct! Success!");
                OnMinigameSuccess();
            }
            else
            {
                KeyCode nextKey = _currentSequence[_currentIndex];
                ShowNextKeyClientRpc(_currentPlayerClientId, nextKey);
            }
        }

        private void HandleWrongKey()
        {
            LogDebug("❌ SERVER: Wrong key! Failed!");
            OnWrongKeyClientRpc(_currentIndex);
            OnMinigameFailed();
        }

        private void OnMinigameSuccess()
        {
            LogDebug("✅ SERVER: Minigame SUCCESS!");

            OnSuccessClientRpc(_currentPlayerClientId);
            ResetMinigameState();

            // Notify systems
            NotifyTutorialManager();
            NotifyParentTable(true);
        }

        private void OnMinigameFailed()
        {
            LogDebug("❌ SERVER: Minigame FAILED!");

            OnFailureClientRpc(_currentPlayerClientId);
            ResetMinigameState();

            // Notify systems
            NotifyParentTable(false);
        }

        private void ResetMinigameState()
        {
            _isActive = false;
            _isWaitingForInput = false;
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void StartMinigameClientRpc(ulong targetClientId, int boxTypeInt, KeyCode firstKey)
        {
            if (!IsTargetClient(targetClientId))
            {
                LogDebug($"⏩ CLIENT {NetworkManager.Singleton.LocalClientId}: Skipping minigame (not target)");
                return;
            }

            LogDebug($"🎮 CLIENT {targetClientId}: Starting minigame UI");

            InitializeClientState((BoxInfo.BoxType)boxTypeInt, targetClientId);
            ShowUIAndStartSequence(firstKey);
        }

        [ClientRpc]
        private void ShowNextKeyClientRpc(ulong targetClientId, KeyCode nextKey)
        {
            if (!IsTargetClient(targetClientId)) return;

            StartCoroutine(ShowNextKeyCoroutine(nextKey));
        }

        [ClientRpc]
        private void OnCorrectKeyClientRpc(int stepIndex)
        {
            PlaySound(correctSound);
            uiController?.ShowFeedback(true, stepIndex);
        }

        [ClientRpc]
        private void OnWrongKeyClientRpc(int stepIndex)
        {
            PlaySound(wrongSound);
            uiController?.ShowFeedback(false, stepIndex);
        }

        [ClientRpc]
        private void OnSuccessClientRpc(ulong targetClientId)
        {
            if (!IsTargetClient(targetClientId)) return;

            PlaySound(successSound);
            UnlockPlayerMovement();
            uiController?.HideUI();
        }

        [ClientRpc]
        private void OnFailureClientRpc(ulong targetClientId)
        {
            if (!IsTargetClient(targetClientId)) return;

            PlaySound(failSound);
            UnlockPlayerMovement();

            if (uiController != null)
            {
                uiController.ShowFailure();
                StartCoroutine(HideUIDelayedCoroutine(FAILURE_UI_HIDE_DELAY));
            }
        }

        [ClientRpc]
        private void ForceStopClientRpc(ulong targetClientId)
        {
            if (!IsTargetClient(targetClientId)) return;

            _isActive = false;
            _isWaitingForInput = false;

            UnlockPlayerMovement();
            uiController?.HideUI();
        }

        #endregion

        #region Client - Helpers

        private bool IsTargetClient(ulong targetClientId)
        {
            return NetworkManager.Singleton.LocalClientId == targetClientId;
        }

        private void InitializeClientState(BoxInfo.BoxType boxType, ulong targetClientId)
        {
            _currentBoxType = boxType;
            _isActive = true;

            LockPlayerMovement(targetClientId);
        }

        private void LockPlayerMovement(ulong targetClientId)
        {
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (!netObj.IsOwner || netObj.OwnerClientId != targetClientId) continue;

                _currentPlayerMovement = netObj.GetComponent<PlayerMovement>();
                if (_currentPlayerMovement == null)
                {
                    _currentPlayerMovement = netObj.GetComponentInChildren<PlayerMovement>();
                }

                if (_currentPlayerMovement != null)
                {
                    _currentPlayerMovement.LockMovement(true);
                    _currentPlayerMovement.LockAllInteractions(true);
                    LogDebug("✅ CLIENT: Movement locked");
                }

                break;
            }
        }

        private void UnlockPlayerMovement()
        {
            if (_currentPlayerMovement == null) return;

            _currentPlayerMovement.LockMovement(false);
            _currentPlayerMovement.LockAllInteractions(false);
        }

        private void ShowUIAndStartSequence(KeyCode firstKey)
        {
            if (uiController != null)
            {
                uiController.SetFadeDurations(keyFadeInDuration, keyFadeOutDuration);
                uiController.ShowUI(_currentBoxType);
            }

            StartCoroutine(StartSequenceCoroutine(firstKey));
        }

        #endregion

        #region Coroutines

        private IEnumerator StartSequenceCoroutine(KeyCode firstKey)
        {
            yield return new WaitForSeconds(UI_SHOW_DELAY);

            if (uiController != null)
            {
                uiController.ShowInputPrompt();
                uiController.ShowKey(firstKey);
            }

            yield return new WaitForSeconds(INPUT_WAIT_DELAY);

            _isWaitingForInput = true;

            LogDebug("✅ CLIENT: Now waiting for input!");
        }

        private IEnumerator ShowNextKeyCoroutine(KeyCode nextKey)
        {
            yield return new WaitForSeconds(feedbackDisplayTime);

            uiController?.HideKey();

            yield return new WaitForSeconds(keyDisplayDelay);

            uiController?.ShowKey(nextKey);
        }

        private IEnumerator HideUIDelayedCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            uiController?.HideUI();
        }

        #endregion

        #region Notifications

        private void NotifyTutorialManager()
        {
            TutorialManager.Instance?.OnMinigameCompleted();
        }

        private void NotifyParentTable(bool success)
        {
            if (parentTable == null) return;

            if (success)
            {
                parentTable.CompleteBoxingSuccess(_currentPlayer, _currentBoxType);
            }
            else
            {
                parentTable.CompleteBoxingFailure(_currentPlayer);
            }
        }

        #endregion

        #region Audio

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region ItemData Cache

        private ItemData GetItemDataFromID(int itemID)
        {
            EnsureItemDataCached();

            foreach (ItemData item in _cachedItemData)
            {
                if (item.itemID == itemID)
                {
                    return item;
                }
            }

            return null;
        }

        private void EnsureItemDataCached()
        {
            if (_isItemDataCached && _cachedItemData != null) return;

            _cachedItemData = Resources.LoadAll<ItemData>("Items");
            _isItemDataCached = true;
        }

        private void ClearItemDataCache()
        {
            _cachedItemData = null;
            _isItemDataCached = false;
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor & Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === MINIGAME STATE ===");
            Debug.Log($"Is Active: {_isActive}");
            Debug.Log($"Is Waiting For Input: {_isWaitingForInput}");
            Debug.Log($"Current Index: {_currentIndex}/{REQUIRED_SEQUENCE_LENGTH}");
            Debug.Log($"Current Box Type: {_currentBoxType}");
            Debug.Log($"Current Player Client ID: {_currentPlayerClientId}");
            Debug.Log($"Sequence: {(_currentSequence.Count > 0 ? string.Join(", ", _currentSequence) : "Empty")}");
        }

        [ContextMenu("Debug: Generate Test Sequence")]
        private void DebugGenerateSequence()
        {
            GenerateUniqueSequence();
            Debug.Log($"{LOG_PREFIX} Test sequence generated: {string.Join(", ", _currentSequence)}");
        }

        [ContextMenu("Debug: Force Stop")]
        private void DebugForceStop()
        {
            ForceStop();
        }
#endif

        #endregion
    }
}