using UnityEditor;
using UnityEngine;

namespace NewCss.RoomsEditor
{
    /// <summary>
    /// RoomViewSettings paneline canlı durum okuması ekler. Ayar yaparken "şu an hangi
    /// odadayım" bilgisi olmadan kapı toleransını ya da geçiş süresini değerlendirmek zor:
    /// oda id'si beklediğinden farklıysa sorun ayarda değil kutu yerleşimindedir.
    /// </summary>
    [CustomEditor(typeof(NewCss.RoomViewSettings))]
    public class RoomViewSettingsEditor : Editor
    {
        public override bool RequiresConstantRepaint()
        {
            // Oyun çalışırken oda id'si her an değişebilir; sürekli yeniden çiz.
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Bu panel oda görünürlük/karartma sisteminin tek ayar yüzeyidir ve değerler " +
                    "sahneye KAYDEDİLİR. Play'e basıp kaydırıcıları çevirdiğinde etkiyi anında " +
                    "görürsün — ama Play sırasında yapılan değişiklikler Unity'nin normal " +
                    "davranışı gereği çıkışta geri alınır; beğendiğin değeri Play'den sonra " +
                    "tekrar gir.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Canlı durum", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Sahnedeki oda kutusu sayısı", RoomRegistry.Count.ToString());

            int roomId = RoomViewController.LocalRoomId;
            EditorGUILayout.LabelField(
                "Bulunduğun oda",
                roomId < 0 ? "yok (-1) — herkes görünür, karartma kapalı" : roomId.ToString());

            if (RoomRegistry.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Sahnede hiç RoomVolume yok — sistem kendini kapattı. Bu, oda kurulmamış " +
                    "sahnelerde (Tutorial) beklenen davranıştır.",
                    MessageType.Warning);
            }
        }
    }
}
