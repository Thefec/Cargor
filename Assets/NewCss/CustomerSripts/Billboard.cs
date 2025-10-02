using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// This script makes the attached object always face the main camera.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        private Camera cam;

        void Start()
        {
            cam = Camera.main;
        }

        void LateUpdate()
        {
            if (cam == null) return;

            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            // Alternative METHOD 2:
            // transform.LookAt(transform.position + cam.transform.forward, cam.transform.up);
        }
    }
}