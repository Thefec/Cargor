using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target; // Character to follow
        public Vector3 offset = new Vector3(0f, 10f, -10f); // Slightly tilted top-down view
        public float smoothSpeed = 5f; // Camera follow speed
        public float fixedRotationX = 45f; // Camera angle (tilt)
        
        private bool isTargetSet = false;
        
        void Start()
        {
            // Eğer target önceden atanmamışsa, local player'ı bul
            if (target == null)
            {
                FindLocalPlayer();
            }
        }
        
        void LateUpdate()
        {
            // Eğer target hala null ise, tekrar aramaya çalış
            if (target == null && !isTargetSet)
            {
                FindLocalPlayer();
                return;
            }
            
            if (target == null) return;

            // Calculate the desired camera position based on target and offset
            Vector3 targetPosition = target.position + offset;
            
            // Smoothly move the camera toward the target position
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
            
            // Keep the camera rotation fixed
            transform.rotation = Quaternion.Euler(fixedRotationX, 0f, 0f);
        }
        
        void FindLocalPlayer()
        {
            // Yöntem 1: Unity Netcode ile NetworkBehaviour kullanarak
            PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();
            foreach (PlayerMovement player in players)
            {
                if (player.IsOwner) // Unity Netcode'da IsOwner kullanıyoruz
                {
                    target = player.transform;
                    isTargetSet = true;
                    break;
                }
            }
           
            
            // Yöntem 3: NetworkManager ile (alternatif)
            if (target == null && NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.LocalClient != null)
                {
                    var localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
                    if (localPlayerObject != null)
                    {
                        target = localPlayerObject.transform;
                        isTargetSet = true;
                    }
                }
            }
        }
        
        // Manuel olarak target atamak için public method
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            isTargetSet = true;
        }
    }
}