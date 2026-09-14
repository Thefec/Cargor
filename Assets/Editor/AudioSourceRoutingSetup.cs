using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NewCss.Audio;

/// <summary>
/// Sahne + prefab'lardaki TÜM AudioSource'ları CargorMixer'ın Music/SFX gruplarına yönlendirir.
///
/// NEDEN: AudioMixer kurulana kadar (bkz. AudioMixerSetup.cs) sahne/prefab AudioSource'ları
/// hiçbir gruba bağlı değildi -> SFX slider'ı hiçbirini etkilemiyordu. Kaynak:
/// plans/ses-tasarimi.md §2 (eksik #1) ve §5 Faz A.
///
/// Kategori seçimi: projede TEK bir müzik amaçlı AudioSource var — MainMenu.unity'deki
/// "MusicPlayer" objesi (MusicPlayer bileşeni, UnifiedSettingsManager.musicAudioSource'a
/// bağlı). Onun dışındaki HER AudioSource SFX (adım, kutu, tır, garaj kapısı, telefon,
/// müşteri, UI butonları...). Bu proje genelinde grep ile doğrulandı.
///
/// İdempotent: zaten doğru gruba bağlı olan AudioSource'lara dokunmaz.
/// </summary>
public static class AudioSourceRoutingSetup
{
    private static readonly string[] SceneRelativePaths =
    {
        "Assets/Scenes/The Main Office.unity",
        "Assets/MainMenu/MainMenu.unity",
    };

    [MenuItem("Tools/Cargor/Audio/AudioSourcelari Mixere Yonlendir")]
    public static void Yonlendir() => Calistir(kuruGosterim: false);

    [MenuItem("Tools/Cargor/Audio/AudioSourcelari Mixere Yonlendir (kuru gosterim)")]
    public static void KuruGosterim() => Calistir(kuruGosterim: true);

    private static void Calistir(bool kuruGosterim)
    {
        var config = AssetDatabase.LoadAssetAtPath<AudioRoutingConfig>("Assets/Resources/Audio/AudioRoutingConfig.asset");
        if (config == null || config.MusicGroup == null || config.SfxGroup == null)
        {
            Debug.LogError("[SesYonlendir] AudioRoutingConfig veya grupları eksik. Önce " +
                            "Tools ▸ Cargor ▸ Audio ▸ Mixer Kur veya Dogrula çalıştırılmalı.");
            return;
        }

        int prefabDegisen = 0, prefabZatenDogru = 0, prefabMuzik = 0, prefabSfx = 0;
        RoutePrefabs(config, kuruGosterim, ref prefabDegisen, ref prefabZatenDogru, ref prefabMuzik, ref prefabSfx);

        int sahneDegisen = 0, sahneZatenDogru = 0, sahneMuzik = 0, sahneSfx = 0;
        RouteScenes(config, kuruGosterim, ref sahneDegisen, ref sahneZatenDogru, ref sahneMuzik, ref sahneSfx);

        string baslik = kuruGosterim ? "KURU GÖSTERİM" : "TAMAM";
        Debug.Log($"[SesYonlendir] {baslik}\n" +
                  $"  Prefab: değişen={prefabDegisen} (müzik={prefabMuzik}, sfx={prefabSfx}) · zaten doğru={prefabZatenDogru}\n" +
                  $"  Sahne : değişen={sahneDegisen} (müzik={sahneMuzik}, sfx={sahneSfx}) · zaten doğru={sahneZatenDogru}");
    }

    private static bool IsMusicSource(AudioSource source)
    {
        return source.GetComponent<MusicPlayer>() != null;
    }

    private static AudioMixerGroupPair ResolveGroup(AudioRoutingConfig config, AudioSource source)
    {
        bool isMusic = IsMusicSource(source);
        return new AudioMixerGroupPair
        {
            Group = isMusic ? config.MusicGroup : config.SfxGroup,
            Category = isMusic ? AudioCategory.Music : AudioCategory.SFX,
        };
    }

    private struct AudioMixerGroupPair
    {
        public UnityEngine.Audio.AudioMixerGroup Group;
        public AudioCategory Category;
    }

    private static void RoutePrefabs(AudioRoutingConfig config, bool kuruGosterim,
        ref int degisen, ref int zatenDogru, ref int muzik, ref int sfx)
    {
        // NOT: "t:AudioSource" arama filtresi bu projede prefab içindeki bileşenleri
        // BULAMIYOR (boş dönüyor, denendi) — bu yüzden TÜM prefab'lar taranıyor ve içinde
        // AudioSource olmayanlar hemen atlanıyor. Grep ile çapraz doğrulandı: 29 prefab
        // (plans/ses-tasarimi.md).
        string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab")
            .Distinct()
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".prefab"))
            .OrderBy(p => p)
            .ToArray();

        int prefabsWithAudio = 0;

        foreach (string prefabPath in prefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                if (sources.Length == 0) continue;

                prefabsWithAudio++;

                bool prefabDegisti = false;
                foreach (AudioSource source in sources)
                {
                    var pair = ResolveGroup(config, source);
                    if (source.outputAudioMixerGroup == pair.Group)
                    {
                        zatenDogru++;
                        continue;
                    }

                    if (pair.Category == AudioCategory.Music) muzik++; else sfx++;

                    if (kuruGosterim)
                    {
                        Debug.Log($"[SesYonlendir] KURU: {prefabPath} :: {GetPath(source.transform)} -> {pair.Category}");
                    }
                    else
                    {
                        source.outputAudioMixerGroup = pair.Group;
                        prefabDegisti = true;
                    }

                    degisen++;
                }

                if (!kuruGosterim && prefabDegisti)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[SesYonlendir] Taranan prefab: {prefabPaths.Length} · AudioSource içeren: {prefabsWithAudio}");
    }

    private static void RouteScenes(AudioRoutingConfig config, bool kuruGosterim,
        ref int degisen, ref int zatenDogru, ref int muzik, ref int sfx)
    {
        string originalScenePath = EditorSceneManager.GetActiveScene().path;
        bool originalSceneIsOneOfOurs = SceneRelativePaths.Contains(originalScenePath);

        foreach (string scenePath in SceneRelativePaths)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"[SesYonlendir] Sahne yok, atlandı: {scenePath}");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var sources = new List<AudioSource>();
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                sources.AddRange(rootGo.GetComponentsInChildren<AudioSource>(true));
            }

            bool sceneDegisti = false;
            foreach (AudioSource source in sources)
            {
                var pair = ResolveGroup(config, source);
                if (source.outputAudioMixerGroup == pair.Group)
                {
                    zatenDogru++;
                    continue;
                }

                if (pair.Category == AudioCategory.Music) muzik++; else sfx++;

                if (kuruGosterim)
                {
                    Debug.Log($"[SesYonlendir] KURU: {scenePath} :: {GetPath(source.transform)} -> {pair.Category}");
                }
                else
                {
                    source.outputAudioMixerGroup = pair.Group;
                    sceneDegisti = true;
                }

                degisen++;
            }

            if (!kuruGosterim && sceneDegisti)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        // Kullanıcının editördeki bağlamını bozmamak için, işlemden önce açık olan sahne
        // bizim listemizde değilse geri döndür.
        if (!string.IsNullOrEmpty(originalScenePath) && !originalSceneIsOneOfOurs)
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }
    }

    private static string GetPath(Transform t)
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
