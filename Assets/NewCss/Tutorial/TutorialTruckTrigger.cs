using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// TutorialTruck i�in trigger collision handler. 
    /// Item'lar�n truck'a teslim edilmesini alg�lar.
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
            if (!CanProcessDelivery())
            {
                return;
            }

            // Haz�r de�ilse i�leme
            if (!tutorialTruck.IsReadyForDelivery)
            {
                Debug.Log($"{LOG_PREFIX} Item entered but truck not ready - ignoring");
                return;
            }

            // NetworkWorldItem kontrol�
            var worldItem = other.GetComponent<NetworkWorldItem>();
            if (worldItem == null)
            {
                worldItem = other.GetComponentInParent<NetworkWorldItem>();
            }

            if (worldItem == null) return;

            // BoxInfo kontrol�
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

            // Dolu olmayan kutular teslimat say�lmaz - ana TruckTrigger deseniyle hizal�
            if (!boxInfo.isFull)
            {
                Debug.Log($"{LOG_PREFIX} Box entered trigger but is not full - ignoring");
                return;
            }

            Debug.Log($"{LOG_PREFIX} Box entered trigger - Type: {boxInfo.boxType}, IsFull: {boxInfo.isFull}");

            // Teslimat� i�le
            bool delivered = tutorialTruck.HandleItemDelivery(boxInfo.boxType, boxInfo.isFull);

            // Teslimat kabul edilmediyse (haz�r de�il / tamamlanm�� / ��k��ta) kutuyu yok etme
            if (!delivered)
            {
                Debug.Log($"{LOG_PREFIX} Delivery not processed - box left intact");
                return;
            }

            // Item'� despawn et
            if (worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
            {
                worldItem.NetworkObject.Despawn();
            }
        }

        private bool CanProcessDelivery()
        {
            return tutorialTruck != null && tutorialTruck.IsServer;
        }

        #endregion
    }
}