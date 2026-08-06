using System;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// Görev gereksinimi yapısı
    /// </summary>
    [Serializable]
    public class QuestRequirement
    {
        [Tooltip("Gerekli miktar")]
        public int targetCount = 1;

        [Header("=== BOX TYPE (Kutu Rengi) ===")]
        [Tooltip("Belirli kutu rengi gerekli mi?")]
        public bool requireSpecificBoxType;

        [Tooltip("Gerekli kutu türü (PlaceBoxOnShelf, PackToy için)")]
        public BoxInfo.BoxType requiredBoxType;

        [Header("=== TRUCK COLOR (Kamyon Rengi) ===")]
        [Tooltip("Belirli kamyon rengi gerekli mi?  (CompleteSpecificColorTruck için)")]
        public bool requireSpecificTruckColor;

        [Tooltip("Gerekli kamyon rengi")]
        public BoxInfo.BoxType requiredTruckColor;

        /// <summary>
        /// Gereksinim açıklamasını döndürür (ham targetCount ile - Editor/debug amaçlı).
        /// UI için <see cref="GetDescription(QuestType, int)"/> kullan; o metot oyuncu sayısına göre
        /// ölçeklenmiş hedefi yazar (bkz. QuestManager.CalculateEffectiveTargetCount).
        /// </summary>
        public string GetDescription(QuestType questType)
        {
            return GetDescription(questType, targetCount);
        }

        /// <summary>
        /// Gereksinim açıklamasını döndürür. `effectiveTargetCount`, kartta gösterilecek GERÇEK
        /// hedeftir (QuestManager.CalculateEffectiveTargetCount ile ölçeklenmiş) - ham targetCount
        /// değil. Tek doğruluk kaynağı QuestManager'daki hesaplama; burası sadece metne döker.
        /// </summary>
        public string GetDescription(QuestType questType, int effectiveTargetCount)
        {
            string boxColor = requireSpecificBoxType
                ? GetColorName(requiredBoxType)
                : null;

            string truckColor = requireSpecificTruckColor
                ? GetColorName(requiredTruckColor)
                : null;

            return GetLocalizedDescription(questType, boxColor, truckColor, effectiveTargetCount);
        }

        private string GetColorName(BoxInfo.BoxType boxType)
        {
            return boxType switch
            {
                BoxInfo.BoxType.Red => "Kırmızı",
                BoxInfo.BoxType.Blue => "Mavi",
                BoxInfo.BoxType.Yellow => "Sarı",
                _ => "Bilinmeyen"
            };
        }

        private string GetLocalizedDescription(QuestType questType, string boxColor, string truckColor, int targetCountToShow)
        {
            return questType switch
            {
                QuestType.CompleteMinigame =>
                    $"{targetCountToShow} kez mini oyunu tamamla",

                QuestType.PlaceBoxOnShelf =>
                    string.IsNullOrEmpty(boxColor)
                        ? $"Rafa {targetCountToShow} adet kutu koy"
                        : $"Rafa {targetCountToShow} adet {boxColor} kutu koy",

                QuestType.CompleteTruck =>
                    $"{targetCountToShow} adet kamyon tamamla",

                QuestType.CompleteSpecificColorTruck =>
                    string.IsNullOrEmpty(truckColor)
                        ? $"{targetCountToShow} adet kamyon tamamla"
                        : $"{targetCountToShow} adet {truckColor} kamyon tamamla",

                QuestType.PackToy =>
                    string.IsNullOrEmpty(boxColor)
                        ? $"{targetCountToShow} adet oyuncak paketle"
                        : $"{targetCountToShow} adet {boxColor} oyuncak paketle",

                QuestType.AnswerPhone =>
                    $"{targetCountToShow} kez telefona cevap ver",

                QuestType.MakePackagingMistake =>
                    $"{targetCountToShow} kez hatalı paketleme yap",

                _ => $"{targetCountToShow}x"
            };
        }
    }
}