using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace NewCss.RoomsEditor
{
    /// <summary>
    /// RoomVolume için Scene view tutamaçları (authoring aracı — plan §9 / S2).
    ///
    /// NEDEN GEREKLİ: RoomVolume.worldBounds DÜNYA uzayındadır ve transform'a bağlı
    /// DEĞİLDİR (böylece §2.1'deki ölçülmüş duvar koordinatları doğrudan yazılabiliyor).
    /// Bunun bedeli, objeyi sahnede sürüklemenin kutuyu kıpırdatmamasıdır — tutamaç
    /// olmasa kullanıcı odaları yalnız Inspector'a 6 sayı yazarak konumlandırabilirdi.
    /// Bu editör o boşluğu kapatır: kutu doğrudan Scene view'da sürüklenip boyutlanır.
    /// </summary>
    [CustomEditor(typeof(NewCss.RoomVolume))]
    [CanEditMultipleObjects]
    public class RoomVolumeEditor : Editor
    {
        private BoxBoundsHandle _handle;

        private void OnEnable()
        {
            _handle = new BoxBoundsHandle();
        }

        private void OnSceneGUI()
        {
            var volume = (NewCss.RoomVolume)target;

            _handle.center = volume.worldBounds.center;
            _handle.size = volume.worldBounds.size;
            _handle.wireframeColor = new Color(1f, 0.7f, 0.1f, 1f);
            _handle.handleColor = new Color(1f, 0.7f, 0.1f, 1f);

            // Tutamaçlar dünya uzayında çalışmalı: objenin kendi dönüşü/ölçeği kutuyu
            // eğmesin diye matris kimliğe sabitlenir.
            using (new Handles.DrawingScope(Matrix4x4.identity))
            {
                EditorGUI.BeginChangeCheck();
                _handle.DrawHandle();

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(volume, "Oda kutusunu düzenle");
                    volume.worldBounds = new Bounds(_handle.center, _handle.size);
                    EditorUtility.SetDirty(volume);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Kutu DÜNYA uzayındadır — objeyi taşımak kutuyu taşımaz.\n" +
                "Scene view'daki sarı tutamaçlardan sürükle, ya da ölçülmüş duvar " +
                "koordinatlarını Bounds alanlarına doğrudan yaz.\n" +
                "Y'yi cömert ver (zemin ≈ 3.88 → yaklaşık y 2.9 .. 11.9): iç duvarlar " +
                "yalnız 1.84 m, dar bir Y kutusu tavanı ve zemini karartma dışında bırakır.",
                MessageType.Info);

            if (GUILayout.Button("Kutuyu objenin konumuna taşı"))
            {
                foreach (Object t in targets)
                {
                    var volume = (NewCss.RoomVolume)t;
                    Undo.RecordObject(volume, "Oda kutusunu objeye taşı");
                    volume.worldBounds = new Bounds(volume.transform.position, volume.worldBounds.size);
                    EditorUtility.SetDirty(volume);
                }
            }
        }
    }
}
