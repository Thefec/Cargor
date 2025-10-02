using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace NewCss
{
    public class TruckLogManager : MonoBehaviour
    {
        public static TruckLogManager Instance;

        [Tooltip("UI TextMeshProUGUI element to display the daily truck log")]
        public TextMeshProUGUI logText;

        private List<string> dailyLogs = new List<string>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Log entry ekler. Her tır teslimatında çağrılır.
        /// </summary>
        /// <param name="truck">Teslimatı tamamlanmış Truck nesnesi</param>
        

        void UpdateLogUI()
        {
            if (logText != null)
            {
                logText.text = "";
                foreach (string log in dailyLogs)
                {
                    logText.text += log + "\n";
                }
            }
        }

        /// <summary>
        /// Gün sonu geldiğinde logları temizler.
        /// </summary>
        public void ClearDailyLog()
        {
            dailyLogs.Clear();
            UpdateLogUI();
        }
    }
}