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
            if (requireSpecificBoxType)
            {
                string boxColorKey = $"BoxType_{requiredBoxType}";
                string boxColor = LocalizationHelper.GetLocalizedString(boxColorKey);
                
                return questType switch
                {
                    QuestType.CompleteMinigame => LocalizationHelper.GetLocalizedStringFormat("Quest_CompleteMinigame", targetCount),
                    QuestType.PlaceBoxOnShelf => LocalizationHelper.GetLocalizedStringFormat("Quest_PlaceBoxOnShelfSpecific", targetCount, boxColor + " "),
                    QuestType.CompleteTruck => LocalizationHelper.GetLocalizedStringFormat("Quest_CompleteTruck", targetCount),
                    QuestType.ServeCustomer => LocalizationHelper.GetLocalizedStringFormat("Quest_ServeCustomer", targetCount),
                    QuestType.IgnoreCustomer => LocalizationHelper.GetLocalizedStringFormat("Quest_IgnoreCustomer", targetCount),
                    QuestType.PackToy => LocalizationHelper.GetLocalizedStringFormat("Quest_PackToy", targetCount),
                    _ => $"{targetCount}x"
                };
            }
            else
            {
                return questType switch
                {
                    QuestType.CompleteMinigame => LocalizationHelper.GetLocalizedStringFormat("Quest_CompleteMinigame", targetCount),
                    QuestType.PlaceBoxOnShelf => LocalizationHelper.GetLocalizedStringFormat("Quest_PlaceBoxOnShelf", targetCount),
                    QuestType.CompleteTruck => LocalizationHelper.GetLocalizedStringFormat("Quest_CompleteTruck", targetCount),
                    QuestType.ServeCustomer => LocalizationHelper.GetLocalizedStringFormat("Quest_ServeCustomer", targetCount),
                    QuestType.IgnoreCustomer => LocalizationHelper.GetLocalizedStringFormat("Quest_IgnoreCustomer", targetCount),
                    QuestType.PackToy => LocalizationHelper.GetLocalizedStringFormat("Quest_PackToy", targetCount),
                    _ => $"{targetCount}x"
                };
            }
        }
    }
}
