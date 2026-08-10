using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Sahnede elle yerleştirilen bir oda kutusu (authoring). Kodda hiçbir oda
    /// sabiti yoktur — roomId/roomName tamamen sahneden gelir. Aynı roomId'yi
    /// birden çok RoomVolume paylaşabilir (L şekilli oda = 2+ kutu, tek id).
    ///
    /// OnEnable/OnDisable'da RoomRegistry'ye kayıt olur/çıkar — FindObjectsByType
    /// KULLANILMAZ (additive sahne yükleme/boşaltma ile uyumlu kalması için).
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomVolume : MonoBehaviour
    {
        [Tooltip("Bu kutunun ait olduğu oda kimliği. Aynı odanın diğer kutularıyla aynı olmalı.")]
        public int roomId;

        [Tooltip("Yalnız düzenleme/gizmo etiketi için — oyun mantığı bu ismi kullanmaz.")]
        public string roomName = "Oda";

        [Tooltip("Kutunun dünya-uzayı sınırları. Gizmo'ya bakarak elle oturt (plan §9).")]
        public Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(10f, 8f, 10f));

        private void OnEnable()
        {
            RoomRegistry.Register(this);
        }

        private void OnDisable()
        {
            RoomRegistry.Unregister(this);
        }

        private static readonly Color GizmoColor = new Color(0.2f, 0.9f, 1f, 0.35f);
        private static readonly Color GizmoColorSelected = new Color(1f, 0.7f, 0.1f, 0.55f);

        private void OnDrawGizmos()
        {
            DrawGizmo(GizmoColor);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo(GizmoColorSelected);
        }

        private void DrawGizmo(Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawCube(worldBounds.center, worldBounds.size);
            Gizmos.color = new Color(color.r, color.g, color.b, 1f);
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(worldBounds.center, $"{roomName} (id {roomId})");
#endif
        }
    }
}
