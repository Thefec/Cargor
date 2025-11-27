using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// TutorialTruck için trigger collision handler. 
    /// Item'larýn truck'a teslim edilmesini algýlar.
    /// </summary>
    public class TutorialTruckTrigger : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TutorialTruckTrigger]";

        #endregion

        #region Public Fields

        [HideInInspector]
        public TutorialTruck tutorialTruck;

        #endregion

        #region Trigger Events

        private void OnTriggerEnter(Collider other)
        {
            if (tutorialTruck == null) return;

            // Sadece server'da iþle
            if (!tutorialTruck.IsServer) return;

            // Kapýlar kapalýysa iþleme
            if (!tutorialTruck.AreDoorsOpen)
            {
                Debug.Log($"{LOG_PREFIX} Item entered but doors are CLOSED - ignoring");
                return;
            }

            // NetworkWorldItem kontrolü
            var worldItem = other.GetComponent<NetworkWorldItem>();
            if (worldItem == null)
            {
                // Parent'ta ara
                worldItem = other.GetComponentInParent<NetworkWorldItem>();
            }

            if (worldItem == null) return;

            // BoxInfo kontrolü
            var boxInfo = other.GetComponent<BoxInfo>();
            if (boxInfo == null)
            {
                boxInfo = other.GetComponentInParent<BoxInfo>();
            }

            if (boxInfo == null)
            {
                Debug.Log($"{LOG_PREFIX} Item has no BoxInfo component");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Box entered trigger - Type: {boxInfo.boxType}, IsFull: {boxInfo.isFull}");

            // Teslimatý iþle
            tutorialTruck.HandleItemDelivery(boxInfo.boxType, boxInfo.isFull);

            // Item'ý despawn et
            if (worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
            {
                worldItem.NetworkObject.Despawn();
            }
        }

        #endregion
    }
}