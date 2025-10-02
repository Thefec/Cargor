using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Controls the visual elements of a position in the queue.
    /// </summary>
    public class QueuePositionVisual : MonoBehaviour
    {
        [Tooltip("Visual elements belonging to this position (ground marker, light, etc.)")]
        public GameObject[] visualElements;

        [Tooltip("Index of this position (starting from 0)")]
        public int positionIndex;

        private void Start()
        {
            // Initially can be off, controlled by CustomerManager
            SetVisibility(false);
        }

        /// <summary>
        /// Sets the visibility of all visual elements.
        /// </summary>
        public void SetVisibility(bool isVisible)
        {
            foreach (var element in visualElements)
            {
                if (element != null)
                {
                    element.SetActive(isVisible);
                }
            }
        }
    }
}