using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "Boş slot bekçisi" — sahne + prefab'ları tarayıp KENDİ script'lerimizdeki (Assets/NewCss,
/// Assets/MENUUI) boş AudioClip/AudioSource referans alanlarını hata olarak raporlar.
///
/// NEDEN: GDD.md §27 CAUTION — ses bu projede İKİ KEZ sessizce öldü: (1) 2026-08-13
/// bağlanmamış AudioSource, (2) 2026-08-31 Unity'nin otomatik yeniden-serileştirme commit'i
/// successCallSound referansını sildi. Kod doğruydu, hata yoktu, ses yoktu — ikisinde de.
/// Bu araç o sınıf hatayı YAKALAMAK için var: boş slot varsa artık sessiz kalmıyor.
///
/// Kapsam BİLEREK üçüncü parti asset'leri (ithappy vb.) DIŞARIDA bırakıyor — onların
/// tasarım gereği boş bıraktığı alanlar bizim bug'ımız değil, gürültü üretir. Sadece
/// Assets/NewCss/ ve Assets/MENUUI/ altındaki script'lerin AudioClip/AudioSource alanları
/// kontrol ediliyor.
///
/// Salt okunur — hiçbir asset'i değiştirmez, sadece Console'a hata basar.
/// </summary>
public static class AudioSlotValidator
{
    private static readonly string[] OwnedScriptPrefixes = { "Assets/NewCss/", "Assets/MENUUI/" };

    [MenuItem("Tools/Cargor/Audio/Bos Ses Slotlarini Kontrol Et")]
    public static void Kontrol()
    {
        int bosSlot = 0;
        int taranan = 0;

        string originalScenePath = EditorSceneManager.GetActiveScene().path;

        // Prefablar
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".prefab"))
            .OrderBy(p => p)
            .ToArray();

        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                taranan++;
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    bosSlot += ReportEmptyAudioSlots(mb, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Sahneler (Build Settings'teki tüm sahneler)
        var scenePaths = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .Where(System.IO.File.Exists)
            .ToArray();

        foreach (string scenePath in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            taranan++;

            var behaviours = new List<MonoBehaviour>();
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                behaviours.AddRange(rootGo.GetComponentsInChildren<MonoBehaviour>(true));
            }

            foreach (MonoBehaviour mb in behaviours)
            {
                bosSlot += ReportEmptyAudioSlots(mb, scenePath);
            }
        }

        if (!string.IsNullOrEmpty(originalScenePath))
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        string sonuc = bosSlot > 0
            ? $"[SesSlotKontrol] {bosSlot} BOŞ SLOT bulundu — yukarıdaki hatalara bak. Taranan: {taranan} (prefab+sahne)."
            : $"[SesSlotKontrol] Boş slot yok. Taranan: {taranan} (prefab+sahne).";

        if (bosSlot > 0) Debug.LogError(sonuc);
        else Debug.Log(sonuc);
    }

    private static int ReportEmptyAudioSlots(MonoBehaviour mb, string assetPath)
    {
        if (mb == null) return 0;

        MonoScript script = MonoScript.FromMonoBehaviour(mb);
        if (script == null) return 0;

        string scriptPath = AssetDatabase.GetAssetPath(script);
        if (!OwnedScriptPrefixes.Any(prefix => scriptPath.StartsWith(prefix))) return 0;

        var so = new SerializedObject(mb);
        var prop = so.GetIterator();
        int found = 0;
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (prop.type != "PPtr<$AudioClip>" && prop.type != "PPtr<$AudioSource>") continue;
            if (prop.objectReferenceValue != null) continue;

            string path = GetHierarchyPath(mb.transform);
            Debug.LogError($"[SesSlotKontrol] BOŞ SLOT: {assetPath} :: {path} :: {mb.GetType().Name}.{prop.name} ({prop.type})");
            found++;
        }

        return found;
    }

    private static string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
