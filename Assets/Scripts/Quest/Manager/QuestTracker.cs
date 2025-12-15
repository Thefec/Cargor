using System;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// Statik event dispatcher - oyun eventlerini quest sistemine iletir
    /// </summary>
    public static class QuestTracker
    {
        #region Events

        // Mevcut eventler
        public static event Action OnMinigameCompleted;
        public static event Action<BoxInfo.BoxType> OnBoxPlacedOnShelf;
        public static event Action OnTruckCompleted;
        public static event Action<BoxInfo.BoxType> OnToyPacked;

        // YENİ EVENTLER
        public static event Action OnPhoneAnswered;
        public static event Action OnPackagingMistake;
        public static event Action<BoxInfo.BoxType> OnSpecificColorTruckCompleted;

        #endregion

        #region Event Triggers

        public static void NotifyMinigameCompleted()
        {
            OnMinigameCompleted?.Invoke();
            Debug.Log("[QuestTracker] Minigame completed");
        }

        public static void NotifyBoxPlacedOnShelf(BoxInfo.BoxType boxType)
        {
            OnBoxPlacedOnShelf?.Invoke(boxType);
            Debug.Log($"[QuestTracker] Box placed on shelf: {boxType}");
        }

        public static void NotifyTruckCompleted()
        {
            OnTruckCompleted?.Invoke();
            Debug.Log("[QuestTracker] Truck completed");
        }

        public static void NotifyToyPacked(BoxInfo.BoxType boxType)
        {
            OnToyPacked?.Invoke(boxType);
            Debug.Log($"[QuestTracker] Toy packed:  {boxType}");
        }

        // YENİ EVENT TRIGGER'LAR
        public static void NotifyPhoneAnswered()
        {
            OnPhoneAnswered?.Invoke();
            Debug.Log("[QuestTracker] Phone answered");
        }

        public static void NotifyPackagingMistake()
        {
            OnPackagingMistake?.Invoke();
            Debug.Log("[QuestTracker] Packaging mistake made");
        }

        public static void NotifySpecificColorTruckCompleted(BoxInfo.BoxType truckColor)
        {
            OnSpecificColorTruckCompleted?.Invoke(truckColor);
            Debug.Log($"[QuestTracker] {truckColor} truck completed");
        }

        #endregion
    }
}