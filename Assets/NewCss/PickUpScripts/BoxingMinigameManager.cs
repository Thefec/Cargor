using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

namespace NewCss
{
    public class BoxingMinigameManager : NetworkBehaviour
    {
        [Header("Minigame Settings")]
        [SerializeField] private int sequenceLength = 3;

        [Header("Timing Settings")]
        [SerializeField] private float keyDisplayDelay = 1f;
        [SerializeField] private float keyFadeInDuration = 0.15f;
        [SerializeField] private float keyFadeOutDuration = 0.15f;
        [SerializeField] private float feedbackDisplayTime = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioClip correctSound;
        [SerializeField] private AudioClip wrongSound;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip failSound;

        [Header("References")]
        [SerializeField] private BoxingUIController uiController;
        [SerializeField] private Table parentTable;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private AudioSource audioSource;
        private PlayerInventory currentPlayer;
        private PlayerMovement currentPlayerMovement;
        private ulong currentPlayerClientId;

        // ✅ SERVER'DA TUTULUYOR
        private List<KeyCode> currentSequence = new List<KeyCode>();
        private int currentIndex = 0;
        private bool isActive = false;
        private bool isWaitingForInput = false;

        private BoxInfo.BoxType currentBoxType;
        private ItemData currentItemData;

        private readonly KeyCode[] possibleKeys = new KeyCode[]
        {
            KeyCode. UpArrow,
            KeyCode.DownArrow,
            KeyCode. LeftArrow,
            KeyCode.RightArrow
        };

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;

            if (sequenceLength != 3)
            {
                Debug.LogWarning("Sequence length is not 3!  Forcing to 3.");
                sequenceLength = 3;
            }
        }

        void Update()
        {
            if (!isActive || !isWaitingForInput) return;

            if (NetworkManager.Singleton == null) return;
            if (currentPlayer == null || !currentPlayer.IsOwner) return;

            foreach (KeyCode key in possibleKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    Debug.Log($"🎮 CLIENT {NetworkManager.Singleton.LocalClientId}: Key pressed: {key}");
                    HandleInputServerRpc(key);
                    break;
                }
            }
        }

        /// <summary>
        /// ✅ CLIENT tarafından çağrılır, SERVER'a bildirir
        /// </summary>
        public void StartMinigame(PlayerInventory player, BoxInfo.BoxType boxType, ItemData itemData)
        {
            if (player == null)
            {
                Debug.LogError("❌ Player is null!");
                return;
            }

            Debug.Log($"🎮 CLIENT {NetworkManager.Singleton.LocalClientId}: Requesting minigame start");

            // ✅ SERVER'a başlatma isteği gönder
            RequestStartMinigameServerRpc(player.OwnerClientId, (int)boxType, itemData.itemID);
        }

        /// <summary>
        /// ✅ SERVER: Minigame'i başlat ve sequence oluştur
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestStartMinigameServerRpc(ulong playerClientId, int boxTypeInt, int itemDataID, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"📥 SERVER: Minigame start requested by client {senderClientId} for player {playerClientId}");

            if (isActive)
            {
                Debug.LogWarning("⚠️ SERVER: Minigame already active!");
                return;
            }

            // ✅ Player'ı bul
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(playerClientId, out var client) ||
                client.PlayerObject == null)
            {
                Debug.LogError($"❌ SERVER: Player {playerClientId} not found!");
                return;
            }

            currentPlayer = client.PlayerObject.GetComponent<PlayerInventory>();
            if (currentPlayer == null)
            {
                Debug.LogError($"❌ SERVER: PlayerInventory not found!");
                return;
            }

            currentPlayerMovement = currentPlayer.GetComponent<PlayerMovement>();
            currentPlayerClientId = playerClientId;
            currentBoxType = (BoxInfo.BoxType)boxTypeInt;

            // ✅ ItemData'yı bul
            currentItemData = GetItemDataFromID(itemDataID);
            if (currentItemData == null)
            {
                Debug.LogError($"❌ SERVER: ItemData {itemDataID} not found!");
                return;
            }

            // ✅ SERVER'DA SEQUENCE OLUŞTUR
            GenerateUniqueSequence();

            isActive = true;
            isWaitingForInput = false;
            currentIndex = 0;

            Debug.Log($"✅ SERVER: Minigame started - Sequence: {string.Join(", ", currentSequence)}");

            // ✅ CLIENT'LARA BAŞLATMA EMRİ GÖNDER
            StartMinigameClientRpc(playerClientId, (int)currentBoxType, currentSequence[0]);
        }

        /// <summary>
        /// ✅ CLIENT: UI'ı göster ve ilk tuşu göster
        /// </summary>
        [ClientRpc]
        private void StartMinigameClientRpc(ulong targetClientId, int boxTypeInt, KeyCode firstKey)
        {
            // ✅ Sadece hedef client çalıştırır
            if (NetworkManager.Singleton.LocalClientId != targetClientId)
            {
                Debug.Log($"⏩ CLIENT {NetworkManager.Singleton.LocalClientId}: Skipping minigame (not target)");
                return;
            }

            Debug.Log($"🎮 CLIENT {targetClientId}: Starting minigame UI");

            currentBoxType = (BoxInfo.BoxType)boxTypeInt;
            isActive = true;

            // ✅ Player movement'ı kilitle
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (netObj.IsOwner && netObj.OwnerClientId == targetClientId)
                {
                    currentPlayerMovement = netObj.GetComponent<PlayerMovement>();
                    if (currentPlayerMovement == null)
                        currentPlayerMovement = netObj.GetComponentInChildren<PlayerMovement>();

                    if (currentPlayerMovement != null)
                    {
                        currentPlayerMovement.LockMovement(true);
                        currentPlayerMovement.LockAllInteractions(true);
                        Debug.Log($"✅ CLIENT: Movement locked");
                    }
                    break;
                }
            }

            // ✅ UI'ı göster
            if (uiController != null)
            {
                uiController.SetFadeDurations(keyFadeInDuration, keyFadeOutDuration);
                uiController.ShowUI(currentBoxType);
            }

            StartCoroutine(StartMinigameSequenceCoroutine(firstKey));
        }

        /// <summary>
        /// ✅ CLIENT: İlk tuşu göster ve input beklemeye başla
        /// </summary>
        private IEnumerator StartMinigameSequenceCoroutine(KeyCode firstKey)
        {
            yield return new WaitForSeconds(0.1f);

            if (uiController != null)
            {
                uiController.ShowInputPrompt();
                uiController.ShowKey(firstKey);
            }

            yield return new WaitForSeconds(0.2f);

            isWaitingForInput = true;

            Debug.Log($"✅ CLIENT: Now waiting for input!");
        }

        /// <summary>
        /// ✅ SERVER'DA SEQUENCE OLUŞTUR
        /// </summary>
        private void GenerateUniqueSequence()
        {
            currentSequence.Clear();
            currentIndex = 0;

            List<KeyCode> availableKeys = new List<KeyCode>(possibleKeys);

            for (int i = 0; i < 3; i++)
            {
                int randomIndex = Random.Range(0, availableKeys.Count);
                currentSequence.Add(availableKeys[randomIndex]);
                availableKeys.RemoveAt(randomIndex);
            }

            Debug.Log($"🔑 SERVER: Generated sequence: {string.Join(", ", currentSequence)}");
        }

        /// <summary>
        /// ✅ SERVER: Input kontrolü
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void HandleInputServerRpc(KeyCode pressedKey, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"📥 SERVER: Input received - Key: {pressedKey}, Sender: {senderClientId}, Active: {isActive}, Waiting: {isWaitingForInput}");

            // ✅ Güvenlik kontrolleri
            if (senderClientId != currentPlayerClientId)
            {
                Debug.LogWarning($"❌ SERVER: Input from wrong client ({senderClientId} != {currentPlayerClientId})");
                return;
            }

            if (!isActive || !isWaitingForInput)
            {
                Debug.LogWarning($"❌ SERVER: Minigame not ready (active: {isActive}, waiting: {isWaitingForInput})");
                return;
            }

            if (currentSequence.Count == 0)
            {
                Debug.LogError($"❌ SERVER: Sequence is empty!");
                return;
            }

            // ✅ Tuş kontrolü
            KeyCode expectedKey = currentSequence[currentIndex];

            Debug.Log($"🎯 SERVER: Key {currentIndex + 1}/3 - Expected: {expectedKey}, Pressed: {pressedKey}");

            if (pressedKey == expectedKey)
            {
                // ✅ Doğru tuş
                OnCorrectKeyClientRpc(currentIndex);
                currentIndex++;

                if (currentIndex >= 3)
                {
                    Debug.Log("✅ SERVER: All keys correct! Success!");
                    OnMinigameSuccess();
                }
                else
                {
                    // ✅ Sonraki tuşu göster
                    KeyCode nextKey = currentSequence[currentIndex];
                    ShowNextKeyClientRpc(currentPlayerClientId, nextKey);
                }
            }
            else
            {
                // ❌ Yanlış tuş
                Debug.Log($"❌ SERVER: Wrong key!  Failed!");
                OnWrongKeyClientRpc(currentIndex);
                OnMinigameFailed();
            }
        }

        /// <summary>
        /// ✅ CLIENT: Sonraki tuşu göster
        /// </summary>
        [ClientRpc]
        private void ShowNextKeyClientRpc(ulong targetClientId, KeyCode nextKey)
        {
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            StartCoroutine(ShowNextKeyCoroutine(nextKey));
        }

        private IEnumerator ShowNextKeyCoroutine(KeyCode nextKey)
        {
            yield return new WaitForSeconds(feedbackDisplayTime);

            if (uiController != null)
            {
                uiController.HideKey();
            }

            yield return new WaitForSeconds(keyDisplayDelay);

            if (uiController != null)
            {
                uiController.ShowKey(nextKey);
            }
        }

        [ClientRpc]
        private void OnCorrectKeyClientRpc(int stepIndex)
        {
            PlaySound(correctSound);

            if (uiController != null)
            {
                uiController.ShowFeedback(true, stepIndex);
            }
        }

        [ClientRpc]
        private void OnWrongKeyClientRpc(int stepIndex)
        {
            PlaySound(wrongSound);

            if (uiController != null)
            {
                uiController.ShowFeedback(false, stepIndex);
            }
        }

        private void OnMinigameSuccess()
        {
            Debug.Log("✅ SERVER: Minigame SUCCESS!");

            OnSuccessClientRpc(currentPlayerClientId);

            isActive = false;
            isWaitingForInput = false;

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnMinigameCompleted();
            }

            if (parentTable != null)
            {
                parentTable.CompleteBoxingSuccess(currentPlayer, currentBoxType);
            }
        }

        [ClientRpc]
        private void OnSuccessClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                PlaySound(successSound);

                if (currentPlayerMovement != null)
                {
                    currentPlayerMovement.LockMovement(false);
                    currentPlayerMovement.LockAllInteractions(false);
                }

                if (uiController != null)
                {
                    uiController.HideUI();
                }
            }
        }

        private void OnMinigameFailed()
        {
            Debug.Log("❌ SERVER: Minigame FAILED!");

            OnFailureClientRpc(currentPlayerClientId);

            isActive = false;
            isWaitingForInput = false;

            if (parentTable != null)
            {
                parentTable.CompleteBoxingFailure(currentPlayer);
            }
        }

        [ClientRpc]
        private void OnFailureClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                PlaySound(failSound);

                if (currentPlayerMovement != null)
                {
                    currentPlayerMovement.LockMovement(false);
                    currentPlayerMovement.LockAllInteractions(false);
                }

                if (uiController != null)
                {
                    uiController.ShowFailure();
                    StartCoroutine(HideUIDelayed(1.5f));
                }
            }
        }

        private IEnumerator HideUIDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (uiController != null)
            {
                uiController.HideUI();
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
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

        public bool IsMinigameActive => isActive;
    }
}