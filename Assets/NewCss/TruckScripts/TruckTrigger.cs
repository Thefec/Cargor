using UnityEngine;



namespace NewCss
{
    public class TruckTrigger : MonoBehaviour
    {
        [HideInInspector]
        public Truck mainTruck;

        private void OnTriggerEnter(Collider other)
        {
            if (mainTruck != null && mainTruck.IsServer)
            {
                BoxInfo box = other.GetComponent<BoxInfo>();
                if (box != null && box.isFull)
                {
                    mainTruck.HandleDeliveryServerRpc(box.boxType, box.isFull);
                    Destroy(other.gameObject);
                }
            }
        }
    }
}