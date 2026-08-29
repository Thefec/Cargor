using UnityEditor;
using UnityEngine;

namespace NewCss.RoomsEditor
{
    /// <summary>
    /// Eşya (kutu/ürün) prefab'larına <see cref="RoomItemVisibility"/> bileşenini kalıcı olarak
    /// bağlayan tek seferlik/idempotent editör aracı.
    ///
    /// "Eşya prefab'ı" NetworkWorldItem taşıyan prefab olarak TANIMLIDIR — isim listesi koda
    /// GÖMÜLMEZ; proje sahnedeki her prefab'ı tip araması ile tarar, bu sayede ileride eklenen
    /// yeni bir eşya prefab'ı da tek tıkla otomatik kapsama girer.
    ///
    /// Idempotent: RoomItemVisibility zaten varsa o prefab atlanır, PrefabUtility.EditPrefabContentsScope
    /// açılmaz — tekrar tekrar çalıştırmak güvenlidir.
    /// </summary>
    public static class RoomItemVisibilityPrefabLinker
    {
        [MenuItem("Tools/Cargor/Rooms/Eşya Prefab'larına Oda Görünürlüğü Ekle")]
        public static void AddToItemPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            int scanned = 0;
            int added = 0;
            int skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                // Yalnız NetworkWorldItem taşıyan (= eşya) prefab'lar hedeflenir.
                if (prefab.GetComponent<NetworkWorldItem>() == null)
                {
                    continue;
                }

                scanned++;

                if (prefab.GetComponent<RoomItemVisibility>() != null)
                {
                    skipped++;
                    continue;
                }

                using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
                {
                    GameObject root = editScope.prefabContentsRoot;
                    if (root.GetComponent<RoomItemVisibility>() == null)
                    {
                        root.AddComponent<RoomItemVisibility>();
                    }
                }

                added++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[RoomItemVisibilityPrefabLinker] Taranan eşya prefab'ı: {scanned}, " +
                $"eklenen: {added}, atlanan (zaten bağlıydı): {skipped}.");
        }
    }
}
