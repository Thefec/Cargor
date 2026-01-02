using System;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Time-based customer density settings.
    /// Editable via Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSettings", menuName = "NewCss/Wave Settings")]
    public class WaveSettings : ScriptableObject
    {
        [Header("=== TIME PERIODS ===")]
        [Tooltip("Zaman dilimi ayarları")]
        public TimePeriod[] timePeriods = new TimePeriod[]
        {
            new TimePeriod { startHour = 8f, endHour = 12f, periodName = "Morning", maxCustomers = 4, spawnRateMultiplier = 1f, isRushHour = false },
            new TimePeriod { startHour = 12f, endHour = 14f, periodName = "Lunch Rush", maxCustomers = 6, spawnRateMultiplier = 1.5f, isRushHour = true },
            new TimePeriod { startHour = 14f, endHour = 15f, periodName = "Afternoon Lull", maxCustomers = 2, spawnRateMultiplier = 0.5f, isRushHour = false },
            new TimePeriod { startHour = 15f, endHour = 16f, periodName = "Afternoon", maxCustomers = 3, spawnRateMultiplier = 0.8f, isRushHour = false },
            new TimePeriod { startHour = 16f, endHour = 17f, periodName = "Evening Rush", maxCustomers = 4, spawnRateMultiplier = 1.3f, isRushHour = true },
            new TimePeriod { startHour = 17f, endHour = 18f, periodName = "Closing Time", maxCustomers = 2, spawnRateMultiplier = 0.6f, isRushHour = false }
        };

        /// <summary>
        /// Belirli bir saat için uygun zaman dilimini döndürür
        /// </summary>
        public TimePeriod GetPeriodForTime(float gameHour)
        {
            foreach (var period in timePeriods)
            {
                if (gameHour >= period.startHour && gameHour < period.endHour)
                {
                    return period;
                }
            }

            // Fallback - default period
            return new TimePeriod
            {
                startHour = 8f,
                endHour = 18f,
                periodName = "Default",
                maxCustomers = 3,
                spawnRateMultiplier = 1f,
                isRushHour = false
            };
        }

        /// <summary>
        /// Şu anki saatin rush hour olup olmadığını döndürür
        /// </summary>
        public bool IsRushHour(float gameHour)
        {
            var period = GetPeriodForTime(gameHour);
            return period.isRushHour;
        }

        /// <summary>
        /// Belirli bir saat için maksimum müşteri sayısını döndürür
        /// </summary>
        public int GetMaxCustomersForTime(float gameHour)
        {
            var period = GetPeriodForTime(gameHour);
            return period.maxCustomers;
        }

        /// <summary>
        /// Belirli bir saat için spawn hızı çarpanını döndürür
        /// </summary>
        public float GetSpawnRateMultiplier(float gameHour)
        {
            var period = GetPeriodForTime(gameHour);
            return period.spawnRateMultiplier;
        }
    }

    /// <summary>
    /// Tek bir zaman dilimi ayarları
    /// </summary>
    [Serializable]
    public struct TimePeriod
    {
        [Tooltip("Başlangıç saati (24 saat formatı)")]
        public float startHour;

        [Tooltip("Bitiş saati (24 saat formatı)")]
        public float endHour;

        [Tooltip("Dönem adı")]
        public string periodName;

        [Tooltip("Bu dönemde maksimum kuyruk boyutu")]
        public int maxCustomers;

        [Tooltip("Spawn hızı çarpanı (1 = normal)")]
        [Range(0.1f, 3f)]
        public float spawnRateMultiplier;

        [Tooltip("Rush hour mu?")]
        public bool isRushHour;

        public override string ToString()
        {
            return $"{periodName} ({startHour:F0}:00 - {endHour:F0}:00): Max {maxCustomers}, Rate x{spawnRateMultiplier:F1}";
        }
    }
}
