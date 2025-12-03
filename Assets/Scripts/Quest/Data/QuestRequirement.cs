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

        [Tooltip("Belirli kutu rengi gerekli mi?")]
        public bool requireSpecificBoxType;

        [Tooltip("Gerekli kutu türü (sadece requireSpecificBoxType true ise)")]
        public BoxInfo.BoxType requiredBoxType;

        /// <summary>
        /// Gereksinim açıklamasını döndürür
        /// </summary>
        public string GetDescription(QuestType questType)
        {
            string boxTypeStr = requireSpecificBoxType ? $"{requiredBoxType} " : "";

            return questType switch
            {
                QuestType.CompleteMinigame => $"{targetCount} kez mini oyunu tamamla",
                QuestType.PlaceBoxOnShelf => $"Rafa {targetCount} adet {boxTypeStr}kutu koy",
                QuestType.CompleteTruck => $"{targetCount} adet aracı tamamla",
                QuestType.ServeCustomer => $"{targetCount} müşteri ile ilgilen",
                QuestType.IgnoreCustomer => $"{targetCount} müşteriyi görmezden gel",
                QuestType.PackToy => $"{targetCount} oyuncak paketle",
                _ => $"{targetCount}x"
            };
        }
    }
}
