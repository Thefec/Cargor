using UnityEngine;

namespace NewCss
{
    public class BoxScaler : MonoBehaviour
    {
        public Vector3 originalScale;

        void Awake()
        {
            // Prefabdan gelen scale’i ilk başta kaydet
            originalScale = transform.localScale;
        }

        public void ResetToOriginalScale()
        {
            transform.localScale = originalScale;
        }
    }
}