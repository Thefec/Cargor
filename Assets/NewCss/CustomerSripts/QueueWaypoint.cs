using System;

namespace NewCss
{
    using UnityEngine;

    /// <summary>
    /// Holds and visualizes queue waypoint positions for customers to stand in order.
    /// </summary>
    public class QueueWaypoint : MonoBehaviour
    {
        [Tooltip("Waypoints in queue order (assign transforms in sequence).")]
        public Transform[] waypoints;

        [Header("Gizmo Settings")]
        [Tooltip("Color of the waypoint gizmos.")]
        public Color gizmoColor = Color.yellow;
        [Tooltip("Radius of the gizmo spheres.")]
        public float gizmoRadius = 0.2f;

        private void OnDrawGizmos()
        {
            if (waypoints == null) return;

            Gizmos.color = gizmoColor;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(waypoints[i].position, gizmoRadius);
            }
        }

        
    }
}