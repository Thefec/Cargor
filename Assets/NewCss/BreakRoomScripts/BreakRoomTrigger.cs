using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class BreakRoomTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        NetworkObject networkObject = other.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsOwner)
        {
            // Oyuncu break room'a girdi
            if (BreakRoomManager.Instance != null)
            {
                BreakRoomManager.Instance.PlayerEnteredBreakRoomServerRpc();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkObject networkObject = other.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsOwner)
        {
            // Oyuncu break room'dan çýktý
            if (BreakRoomManager.Instance != null)
            {
                BreakRoomManager.Instance.PlayerExitedBreakRoomServerRpc();
            }
        }
    }
}