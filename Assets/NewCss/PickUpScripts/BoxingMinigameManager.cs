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

        [Header("Audio")]
        [SerializeField] private AudioClip correctSound;
        [SerializeField] private AudioClip wrongSound;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip failSound;

        [Header("References")]
        [SerializeField] private BoxingUIController uiController;
        [SerializeField] private Table parentTable;

        private AudioSource audioSource;
        private PlayerInventory currentPlayer;
        private PlayerMovement currentPlayerMovement;

        private List<KeyCode> currentSequence = new List<KeyCode>();
        private int currentIndex = 0;
        private bool isActive = false;
        private bool isWaitingForInput = false;

        private BoxInfo.BoxType currentBoxType;
        private ItemData currentItemData;

        private readonly KeyCode[] possibleKeys = new KeyCode[]
        {
            KeyCode.UpArrow,
            KeyCode.DownArrow,
            KeyCode.LeftArrow,
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
                Debug.LogWarning("Sequence length is not 3! Forcing to 3.");
                sequenceLength = 3;
            }
        }

        void Update()
        {
            if (!IsOwner || !isActive || !isWaitingForInput) return;

            // ✅ SADECE YÖN TUŞLARINI DİNLE
            foreach (KeyCode key in possibleKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    HandleInput(key);
                    break;
                }
            }
        }

        public void StartMinigame(PlayerInventory player, BoxInfo.BoxType boxType, ItemData itemData)
        {
            if (isActive || player == null) return;

            currentPlayer = player;
            currentPlayerMovement = player.GetComponent<PlayerMovement>();
            currentBoxType = boxType;
            currentItemData = itemData;

            Debug.Log($"🎮 Boxing minigame started for {boxType} box - 3 keys required");

            // ✅ TAM KİLİT: Movement + Interactions
            if (currentPlayerMovement != null)
            {
                currentPlayerMovement.LockMovement(true);
                currentPlayerMovement.LockAllInteractions(true);
            }

            GenerateSequence();

            if (uiController != null)
            {
                uiController.ShowUI(currentBoxType);
            }

            isActive = true;

            // ✅ YENİ: Direkt başlat (gösterme yok)
            StartCoroutine(StartDirectlyCoroutine());
        }

        private void GenerateSequence()
        {
            currentSequence.Clear();
            currentIndex = 0;

            for (int i = 0; i < 3; i++)
            {
                KeyCode randomKey = possibleKeys[Random.Range(0, possibleKeys.Length)];
                currentSequence.Add(randomKey);
            }

            Debug.Log($"🔑 Generated 3-key sequence: {string.Join(", ", currentSequence)}");
        }

        // ✅ YENİ: Direkt başlat (sekans gösterme YOK)
        private IEnumerator StartDirectlyCoroutine()
        {
            yield return new WaitForSeconds(0.1f); // Kısa gecikme

            Debug.Log("⏳ Minigame started - waiting for player input... (3 keys required)");
            currentIndex = 0;
            isWaitingForInput = true;

            if (uiController != null)
            {
                uiController.ShowInputPrompt();
                // İLK tuşu göster ve BEKLE
                uiController.ShowKey(currentSequence[currentIndex]);
            }
        }

        private void HandleInput(KeyCode pressedKey)
        {
            KeyCode expectedKey = currentSequence[currentIndex];

            Debug.Log($"🎯 Key {currentIndex + 1}/3 - Expected: {expectedKey}, Pressed: {pressedKey}");

            if (pressedKey == expectedKey)
            {
                // ✅ DOĞRU TUŞ
                PlaySound(correctSound);

                if (uiController != null)
                {
                    uiController.ShowFeedback(true, currentIndex);
                }

                currentIndex++;

                // 3 tuş tamamlandı mı?
                if (currentIndex >= 3)
                {
                    Debug.Log("✅ All 3 keys correct! Success!");
                    OnMinigameSuccess();
                }
                else
                {
                    // Sıradaki tuşu göster
                    StartCoroutine(ShowNextKeyAfterDelay());
                }
            }
            else
            {
                // ❌ YANLIŞ TUŞ - ANINDA FAIL
                PlaySound(wrongSound);

                if (uiController != null)
                {
                    uiController.ShowFeedback(false, currentIndex);
                }

                Debug.Log($"❌ Wrong key at step {currentIndex + 1}/3! Failed!");
                OnMinigameFailed();
            }
        }

        private IEnumerator ShowNextKeyAfterDelay()
        {
            // Kısa gecikme
            yield return new WaitForSeconds(0.3f);

            if (uiController != null && currentIndex < currentSequence.Count)
            {
                Debug.Log($"➡️ Showing key {currentIndex + 1}/3");
                uiController.ShowKey(currentSequence[currentIndex]);
            }
        }

        private void OnMinigameSuccess()
        {
            Debug.Log("✅ Minigame SUCCESS! All 3 keys pressed correctly!");
            PlaySound(successSound);

            isActive = false;
            isWaitingForInput = false;

            if (uiController != null)
            {
                uiController.HideUI();
            }

            // ✅ KİLİTLERİ AÇ
            if (currentPlayerMovement != null)
            {
                currentPlayerMovement.LockMovement(false);
                currentPlayerMovement.LockAllInteractions(false);
            }

            if (parentTable != null && IsServer)
            {
                parentTable.CompleteBoxingSuccess(currentPlayer, currentBoxType);
            }
            else if (IsOwner)
            {
                RequestBoxingSuccessServerRpc(currentBoxType);
            }
        }

        private void OnMinigameFailed()
        {
            Debug.Log("❌ Minigame FAILED! Wrong key pressed!");
            PlaySound(failSound);

            isActive = false;
            isWaitingForInput = false;

            if (uiController != null)
            {
                uiController.ShowFailure();
                StartCoroutine(HideUIDelayed(1.5f));
            }

            // ✅ KİLİTLERİ AÇ
            if (currentPlayerMovement != null)
            {
                currentPlayerMovement.LockMovement(false);
                currentPlayerMovement.LockAllInteractions(false);
            }

            if (parentTable != null && IsServer)
            {
                parentTable.CompleteBoxingFailure(currentPlayer);
            }
            else if (IsOwner)
            {
                RequestBoxingFailureServerRpc();
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

        [ServerRpc(RequireOwnership = false)]
        private void RequestBoxingSuccessServerRpc(BoxInfo.BoxType boxType)
        {
            if (parentTable != null)
            {
                parentTable.CompleteBoxingSuccess(currentPlayer, boxType);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestBoxingFailureServerRpc()
        {
            if (parentTable != null)
            {
                parentTable.CompleteBoxingFailure(currentPlayer);
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        public bool IsMinigameActive => isActive;
    }
}