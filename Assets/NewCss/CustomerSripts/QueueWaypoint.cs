using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kuyruk waypoint sistemi - müþterilerin sýrayla duracaðý pozisyonlarý tutar ve görselleþtirir.
    /// </summary>
    public class QueueWaypoint : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QueueWaypoint]";

        #endregion

        #region Serialized Fields

        [Header("=== WAYPOINTS ===")]
        [SerializeField, Tooltip("Kuyruk sýrasýna göre waypoint'ler")]
        public Transform[] waypoints;

        [Header("=== GIZMO SETTINGS ===")]
        [SerializeField, Tooltip("Waypoint gizmo rengi")]
        public Color gizmoColor = Color.yellow;

        [SerializeField, Tooltip("Gizmo küre yarýçapý")]
        public float gizmoRadius = 0.2f;

        [SerializeField, Tooltip("Waypoint'ler arasý çizgi çiz")]
        private bool drawLinesBetweenWaypoints = true;

        [SerializeField, Tooltip("Index numaralarýný göster")]
        private bool showIndexLabels = true;

        #endregion

        #region Public Properties

        /// <summary>
        /// Toplam waypoint sayýsý
        /// </summary>
        public int WaypointCount => waypoints?.Length ?? 0;

        /// <summary>
        /// Waypoint'ler geçerli mi?
        /// </summary>
        public bool HasValidWaypoints => waypoints != null && waypoints.Length > 0;

        #endregion

        #region Public API

        /// <summary>
        /// Belirtilen index'teki waypoint pozisyonunu döndürür
        /// </summary>
        public Vector3 GetWaypointPosition(int index)
        {
            if (!IsValidIndex(index))
            {
                Debug.LogWarning($"{LOG_PREFIX} Invalid waypoint index: {index}");
                return Vector3.zero;
            }

            if (waypoints[index] == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} Waypoint at index {index} is null");
                return Vector3.zero;
            }

            return waypoints[index].position;
        }

        /// <summary>
        /// Belirtilen index'teki waypoint transform'unu döndürür
        /// </summary>
        public Transform GetWaypoint(int index)
        {
            if (!IsValidIndex(index))
            {
                return null;
            }

            return waypoints[index];
        }

        /// <summary>
        /// Ýlk waypoint pozisyonunu döndürür
        /// </summary>
        public Vector3 GetFirstWaypointPosition()
        {
            return GetWaypointPosition(0);
        }

        /// <summary>
        /// Son waypoint pozisyonunu döndürür
        /// </summary>
        public Vector3 GetLastWaypointPosition()
        {
            return GetWaypointPosition(WaypointCount - 1);
        }

        /// <summary>
        /// Tüm waypoint pozisyonlarýný döndürür
        /// </summary>
        public Vector3[] GetAllWaypointPositions()
        {
            if (!HasValidWaypoints)
            {
                return System.Array.Empty<Vector3>();
            }

            var positions = new Vector3[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                positions[i] = waypoints[i] != null ? waypoints[i].position : Vector3.zero;
            }

            return positions;
        }

        /// <summary>
        /// En yakýn waypoint index'ini döndürür
        /// </summary>
        public int GetNearestWaypointIndex(Vector3 position)
        {
            if (!HasValidWaypoints)
            {
                return -1;
            }

            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;

                float distance = Vector3.Distance(position, waypoints[i].position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        #endregion

        #region Validation

        private bool IsValidIndex(int index)
        {
            return waypoints != null && index >= 0 && index < waypoints.Length;
        }

        /// <summary>
        /// Null waypoint'leri kontrol eder
        /// </summary>
        public int GetNullWaypointCount()
        {
            if (!HasValidWaypoints)
            {
                return 0;
            }

            int nullCount = 0;
            foreach (var waypoint in waypoints)
            {
                if (waypoint == null)
                {
                    nullCount++;
                }
            }

            return nullCount;
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawWaypointGizmos();
        }

        private void DrawWaypointGizmos()
        {
            if (waypoints == null) return;

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;

                DrawWaypointSphere(i);
                DrawWaypointLabel(i);
                DrawLineToPreviousWaypoint(i);
            }
        }

        private void DrawWaypointSphere(int index)
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(waypoints[index].position, gizmoRadius);
        }

        private void DrawWaypointLabel(int index)
        {
            if (!showIndexLabels) return;

            Vector3 labelPosition = waypoints[index].position + Vector3.up * (gizmoRadius + 0.3f);
            UnityEditor.Handles.Label(labelPosition, $"Q{index}");
        }

        private void DrawLineToPreviousWaypoint(int index)
        {
            if (!drawLinesBetweenWaypoints) return;
            if (index <= 0) return;
            if (waypoints[index - 1] == null) return;

            Gizmos.color = gizmoColor * 0.7f;
            Gizmos.DrawLine(waypoints[index - 1].position, waypoints[index].position);
        }

        [ContextMenu("Validate Waypoints")]
        private void DebugValidateWaypoints()
        {
            Debug.Log($"{LOG_PREFIX} === WAYPOINT VALIDATION ===");
            Debug.Log($"Total Waypoints: {WaypointCount}");
            Debug.Log($"Null Waypoints: {GetNullWaypointCount()}");

            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Length; i++)
                {
                    string status = waypoints[i] != null ? "OK" : "NULL";
                    string name = waypoints[i] != null ? waypoints[i].name : "N/A";
                    Debug.Log($"  [{i}] {name} - {status}");
                }
            }
        }

        [ContextMenu("Auto-Find Child Waypoints")]
        private void DebugAutoFindWaypoints()
        {
            var childTransforms = GetComponentsInChildren<Transform>();
            var waypointList = new System.Collections.Generic.List<Transform>();

            foreach (var child in childTransforms)
            {
                if (child != transform && child.name.ToLower().Contains("waypoint"))
                {
                    waypointList.Add(child);
                }
            }

            if (waypointList.Count > 0)
            {
                waypoints = waypointList.ToArray();
                Debug.Log($"{LOG_PREFIX} Found and assigned {waypoints.Length} waypoints");
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} No waypoints found in children");
            }
        }
#endif

        #endregion
    }
}