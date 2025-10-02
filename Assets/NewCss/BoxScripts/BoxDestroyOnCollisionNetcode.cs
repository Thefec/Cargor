using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    [RequireComponent(typeof(BoxInfo))]
    [RequireComponent(typeof(NetworkObject))]
    public class BoxDestroyOnCollisionNetcode : NetworkBehaviour
    {
        [Header("Destroy criteria")]
        public string[] targetTags = new string[] { "Ground" };
        public LayerMask targetLayers;
        public bool onlyIfEmpty = true;

        [Header("Server RPC settings")]
        [Tooltip("If true, non-owners may request destruction. If false, only the owner (or server) can request it.")]
        public bool allowNonOwnerRequests = false;

        BoxInfo boxInfo;

        void Awake()
        {
            boxInfo = GetComponent<BoxInfo>();
            if (boxInfo == null)
                boxInfo = GetComponentInParent<BoxInfo>();
        }

        bool ShouldDestroy(GameObject other)
        {
            if (onlyIfEmpty && boxInfo != null && boxInfo.isFull) return false;

            if (targetTags != null)
            {
                foreach (var t in targetTags)
                {
                    if (!string.IsNullOrEmpty(t) && other.CompareTag(t)) return true;
                }
            }

            if (((1 << other.layer) & targetLayers) != 0) return true;

            return false;
        }

        void OnCollisionEnter(Collision collision)
        {
            TryHandleCollision(collision.gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            TryHandleCollision(other.gameObject);
        }

        void TryHandleCollision(GameObject other)
        {
            if (!ShouldDestroy(other)) return;

            // If we're running on the server, destroy directly.
            if (IsServer)
            {
                DestroyOnServer();
            }
            else
            {
                // We're a client: request server to destroy (ServerRpc)
                // This ServerRpc allows the server to validate and then despawn.
                RequestDestroyServerRpc();
            }
        }

        // Allow non-owner requests only if explicitly enabled (security).
        [ServerRpc(RequireOwnership = false)]
        void RequestDestroyServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;

            // If server already (host) is executing, ignore (shouldn't happen often)
            if (!IsServer) return;

            // Validation: owner/security check
            if (!allowNonOwnerRequests)
            {
                // If the asking client is not the owner and not the server, reject
                if (NetworkObject != null && NetworkObject.OwnerClientId != senderId && senderId != NetworkManager.ServerClientId)
                {
                    Debug.LogWarning($"Destroy request rejected: sender {senderId} is not owner of {gameObject.name} (owner={NetworkObject.OwnerClientId}).");
                    return;
                }
            }

            // Re-check box state on server (authoritative)
            if (onlyIfEmpty && boxInfo != null && boxInfo.isFull)
            {
                // refuse: box became full meanwhile
                return;
            }

            DestroyOnServer();
        }

        void DestroyOnServer()
        {
            if (!IsServer) return;

            // If this is a spawned NetworkObject, despawn it so clients also remove it.
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
#if UNITY_NETCODE
                // Try to despawn via Netcode API. Some Netcode versions support a 'destroy' bool; we call Despawn() and then Destroy fallback.
                try
                {
                    NetworkObject.Despawn();
                }
                catch
                {
                    // If the Despawn(destroy) overload exists in your Netcode version, prefer that.
                    // If Despawn throws or doesn't exist, fall back to Destroy.
                }
#endif
                // Additionally, ensure the GameObject is destroyed locally on server.
                Destroy(gameObject);
            }
            else
            {
                // Not a network-spawned object: just destroy locally.
                Destroy(gameObject);
            }
        }
    }
}
