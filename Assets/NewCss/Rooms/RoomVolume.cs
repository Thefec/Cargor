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

        [Tooltip("Kutunun DÜNYA-uzayı sınırları — transform'dan bağımsızdır, objeyi taşımak kutuyu taşımaz. " +
                 "Scene view'daki sarı tutamaçlardan sürükle ya da §2.1'deki ölçülmüş duvar koordinatlarını doğrudan yaz.")]
        public Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(10f, 8f, 10f));

        /// <summary>
        /// Component ilk eklendiğinde (ve Inspector'dan Reset'lendiğinde) kutuyu objenin
        /// bulunduğu yere taşır. Aksi hâlde varsayılan kutu dünya orijininde doğar; depo
        /// x[-92..-24], y≈3.88'de olduğu için kullanıcı "kutu nerede" diye arar.
        /// </summary>
        private void Reset()
        {
            worldBounds = new Bounds(transform.position, new Vector3(10f, 8f, 10f));
        }

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

        /// <summary>
        /// Seçili DEĞİLKEN yalnız tel kafes. Dolu kutu çizilirse birkaç oda birden
        /// Scene view'ı renkli sise çevirir ve kutuyu duvara oturtmak için bakman
        /// gereken raf/masa/duvar geometrisi görünmez olur (authoring'i imkânsızlaştırır).
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawGizmo(GizmoColor, false);
        }

        /// <summary>Seçiliyken hacmi okunur kılmak için dolu kutu da çizilir.</summary>
        private void OnDrawGizmosSelected()
        {
            DrawGizmo(GizmoColorSelected, true);
        }

        private void DrawGizmo(Color color, bool solid)
        {
            if (solid)
            {
                Gizmos.color = color;
                Gizmos.DrawCube(worldBounds.center, worldBounds.size);
            }

            Gizmos.color = new Color(color.r, color.g, color.b, 1f);
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(worldBounds.center, $"{roomName} (id {roomId})");
#endif
        }
    }
}
