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
            string boxColor = requireSpecificBoxType 
                ? LocalizationHelper.GetLocalizedString($"BoxType_{requiredBoxType}") 
                : null;
            
            return GetLocalizedDescription(questType, boxColor);
        }
        
        private string GetLocalizedDescription(QuestType questType, string boxColor)
        {
            return questType switch
            {
                QuestType.CompleteMinigame => 
                    LocalizationHelper.GetLocalizedStringFormat("Quest_CompleteMinigame", targetCount),
                    
                QuestType.PlaceBoxOnShelf => 
                    string.IsNullOrEmpty(boxColor)
                        ? LocalizationHelper.GetLocalizedStringFormat("Quest_PlaceBoxOnShelf", targetCount)
                        : LocalizationHelper.GetLocalizedStringFormat("Quest_PlaceBoxOnShelfSpecific", targetCount, boxColor),
                        
                QuestType.CompleteTruck => 
                    LocalizationHelper.GetLocalizedStringFormat("Quest_CompleteTruck", targetCount),
                    
                QuestType.ServeCustomer => 
                    LocalizationHelper.GetLocalizedStringFormat("Quest_ServeCustomer", targetCount),
                    
                QuestType.IgnoreCustomer => 
                    LocalizationHelper.GetLocalizedStringFormat("Quest_IgnoreCustomer", targetCount),
                    
                QuestType.PackToy => 
                    LocalizationHelper.GetLocalizedStringFormat("Quest_PackToy", targetCount),
                    
                _ => $"{targetCount}x"
            };
        }
    }
}
