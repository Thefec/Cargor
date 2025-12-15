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
        /// Gereksinim açıklamasını döndürür
        /// </summary>
        public string GetDescription(QuestType questType)
        {
            string boxColor = requireSpecificBoxType
                ? GetColorName(requiredBoxType)
                : null;

            string truckColor = requireSpecificTruckColor
                ? GetColorName(requiredTruckColor)
                : null;

            return GetLocalizedDescription(questType, boxColor, truckColor);
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

        private string GetLocalizedDescription(QuestType questType, string boxColor, string truckColor)
        {
            return questType switch
            {
                QuestType.CompleteMinigame =>
                    $"{targetCount} kez mini oyunu tamamla",

                QuestType.PlaceBoxOnShelf =>
                    string.IsNullOrEmpty(boxColor)
                        ? $"Rafa {targetCount} adet kutu koy"
                        : $"Rafa {targetCount} adet {boxColor} kutu koy",

                QuestType.CompleteTruck =>
                    $"{targetCount} adet kamyon tamamla",

                QuestType.CompleteSpecificColorTruck =>
                    string.IsNullOrEmpty(truckColor)
                        ? $"{targetCount} adet kamyon tamamla"
                        : $"{targetCount} adet {truckColor} kamyon tamamla",

                QuestType.PackToy =>
                    string.IsNullOrEmpty(boxColor)
                        ? $"{targetCount} adet oyuncak paketle"
                        : $"{targetCount} adet {boxColor} oyuncak paketle",

                QuestType.AnswerPhone =>
                    $"{targetCount} kez telefona cevap ver",

                QuestType.MakePackagingMistake =>
                    $"{targetCount} kez hatalı paketleme yap",

                _ => $"{targetCount}x"
            };
        }
    }
}