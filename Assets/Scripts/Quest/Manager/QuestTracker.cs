using System;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// Oyun içi olayları görev sistemine bildiren statik sınıf
    /// Mevcut sistemlere minimum müdahale ile event-driven yapı sağlar
    /// </summary>
    public static class QuestTracker
    {
        #region Events

        /// <summary>
        /// Mini oyun tamamlandığında tetiklenir
        /// </summary>
        public static event Action OnMinigameCompleted;

        /// <summary>
        /// Rafa kutu yerleştirildiğinde tetiklenir
        /// </summary>
        public static event Action<BoxInfo.BoxType> OnBoxPlacedOnShelf;

        /// <summary>
        /// Kamyon tamamlandığında tetiklenir
        /// </summary>
        public static event Action OnTruckCompleted;

        /// <summary>
        /// Müşteri ile ilgilenildiğinde tetiklenir
        /// </summary>
        public static event Action OnCustomerServed;

        /// <summary>
        /// Müşteri timeout olduğunda tetiklenir
        /// </summary>
        public static event Action OnCustomerTimeout;

        /// <summary>
        /// Oyuncak paketlendiğinde tetiklenir
        /// </summary>
        public static event Action<BoxInfo.BoxType> OnToyPacked;

        #endregion

        #region Public Methods

        /// <summary>
        /// Mini oyun tamamlandığını bildirir
        /// BoxingMinigameManager.CompleteBoxingSuccess tarafından çağrılır
        /// </summary>
        public static void NotifyMinigameCompleted()
        {
            Debug.Log("[QuestTracker] Minigame completed");
            OnMinigameCompleted?.Invoke();
        }

        /// <summary>
        /// Rafa kutu yerleştirildiğini bildirir
        /// ShelfState.PlaceItemInSlot tarafından çağrılır
        /// </summary>
        public static void NotifyBoxPlacedOnShelf(BoxInfo.BoxType boxType)
        {
            Debug.Log($"[QuestTracker] Box placed on shelf: {boxType}");
            OnBoxPlacedOnShelf?.Invoke(boxType);
        }

        /// <summary>
        /// Kamyon tamamlandığını bildirir
        /// Truck tarafından çağrılır
        /// </summary>
        public static void NotifyTruckCompleted()
        {
            Debug.Log("[QuestTracker] Truck completed");
            OnTruckCompleted?.Invoke();
        }

        /// <summary>
        /// Müşteri ile ilgilenildiğini bildirir
        /// CustomerAI.CompleteInteraction tarafından çağrılır
        /// </summary>
        public static void NotifyCustomerServed()
        {
            Debug.Log("[QuestTracker] Customer served");
            OnCustomerServed?.Invoke();
        }

        /// <summary>
        /// Müşteri timeout olduğunu bildirir
        /// CustomerAI.HandleTimeUp tarafından çağrılır
        /// </summary>
        public static void NotifyCustomerTimeout()
        {
            Debug.Log("[QuestTracker] Customer timeout");
            OnCustomerTimeout?.Invoke();
        }

        /// <summary>
        /// Oyuncak paketlendiğini bildirir
        /// Table.CompleteBoxingSuccess tarafından çağrılır
        /// </summary>
        public static void NotifyToyPacked(BoxInfo.BoxType boxType)
        {
            Debug.Log($"[QuestTracker] Toy packed: {boxType}");
            OnToyPacked?.Invoke(boxType);
        }

        #endregion
    }
}
