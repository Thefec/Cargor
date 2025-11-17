using UnityEngine;
using UnityEngine.UI;

namespace NewCss
{
    [RequireComponent(typeof(Collider))]
    public class QuestBoard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject interactButton;
        [SerializeField] private QuestUI questUI;

        [Header("Settings")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private bool playerInRange = false;
        private PlayerMovement nearbyPlayer;

        void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            if (interactButton != null)
                interactButton.SetActive(false);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Character"))
            {
                playerInRange = true;
                nearbyPlayer = other.GetComponent<PlayerMovement>();

                if (interactButton != null)
                    interactButton.SetActive(true);

                Debug.Log("Player entered quest board range");
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Character"))
            {
                playerInRange = false;
                nearbyPlayer = null;

                if (interactButton != null)
                    interactButton.SetActive(false);

                Debug.Log("Player exited quest board range");
            }
        }

        void Update()
        {
            if (playerInRange && Input.GetKeyDown(interactKey))
            {
                OpenQuestUI();
            }
        }

        public void OpenQuestUI()
        {
            if (questUI != null)
            {
                questUI.OpenUI();

                // Movement kilitle
                if (nearbyPlayer != null)
                {
                    nearbyPlayer.LockMovement(true);
                    nearbyPlayer.LockAllInteractions(true);
                }

                // Butonu gizle
                if (interactButton != null)
                    interactButton.SetActive(false);

                Debug.Log("Quest UI opened");
            }
        }

        public void OnUIClose()
        {
            // Movement kilidi aç
            if (nearbyPlayer != null)
            {
                nearbyPlayer.LockMovement(false);
                nearbyPlayer.LockAllInteractions(false);
            }

            // Butonu tekrar göster (eðer hala range içindeyse)
            if (playerInRange && interactButton != null)
                interactButton.SetActive(true);
        }
    }
}