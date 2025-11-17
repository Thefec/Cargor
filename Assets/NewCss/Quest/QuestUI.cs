using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace NewCss
{
    public class QuestUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject questPanel;
        [SerializeField] private Button closeButton;

        [Header("Quest Slots")]
        [SerializeField] private QuestSlot[] questSlots; // 2 slot

        [Header("References")]
        [SerializeField] private QuestBoard questBoard;

        void Awake()
        {
            if (questPanel != null)
                questPanel.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseUI);

            // Quest slot'larý bul
            if (questSlots == null || questSlots.Length == 0)
            {
                questSlots = GetComponentsInChildren<QuestSlot>(true);
            }
        }

        void OnEnable()
        {
            QuestManager.OnQuestProgressUpdated += OnQuestProgressUpdated;
            QuestManager.OnDailyQuestsGenerated += OnDailyQuestsGenerated;
        }

        void OnDisable()
        {
            QuestManager.OnQuestProgressUpdated -= OnQuestProgressUpdated;
            QuestManager.OnDailyQuestsGenerated -= OnDailyQuestsGenerated;
        }

        public void OpenUI()
        {
            if (questPanel != null)
                questPanel.SetActive(true);

            RefreshQuests();

            // Cursor göster
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseUI()
        {
            if (questPanel != null)
                questPanel.SetActive(false);

            // QuestBoard'a bildir
            if (questBoard != null)
                questBoard.OnUIClose();

            // Cursor gizle
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log("Quest UI closed");
        }

        private void RefreshQuests()
        {
            if (QuestManager.Instance == null) return;

            List<QuestProgress> dailyQuests = QuestManager.Instance.GetDailyQuests();

            for (int i = 0; i < questSlots.Length; i++)
            {
                if (i < dailyQuests.Count)
                {
                    QuestData questData = QuestManager.Instance.GetQuestData(dailyQuests[i].questID);
                    questSlots[i].Setup(questData, dailyQuests[i]);
                    questSlots[i].gameObject.SetActive(true);
                }
                else
                {
                    questSlots[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnQuestProgressUpdated(QuestProgress progress)
        {
            RefreshQuests();
        }

        private void OnDailyQuestsGenerated()
        {
            RefreshQuests();
        }
    }
}