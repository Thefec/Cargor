using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kamyon trigger b�lgesi - kutu teslimat�n� alg�lar ve i�ler. 
    /// </summary>
    public class TruckTrigger : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TruckTrigger]";

        #endregion

        #region Public Fields

        [HideInInspector]
        public Truck mainTruck;

        #endregion

        #region Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            if (!CanProcessDelivery())
            {
                return;
            }

            TryProcessDelivery(other);
        }

        private bool CanProcessDelivery()
        {
            return mainTruck != null && mainTruck.IsServer;
        }

        private void TryProcessDelivery(Collider other)
        {
            BoxInfo box = other.GetComponent<BoxInfo>();

            if (box == null || !box.isFull)
            {
                return;
            }

            mainTruck.ProcessDelivery(box.boxType, box.isFull);

            NetworkObject boxNetworkObject = other.GetComponent<NetworkObject>();
            if (boxNetworkObject != null && boxNetworkObject.IsSpawned)
            {
                boxNetworkObject.Despawn(true);
            }
            else
            {
                Destroy(other.gameObject);
            }
        }

        #endregion
    }
}