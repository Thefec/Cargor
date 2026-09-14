using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NewCss.Audio;

/// <summary>
/// Assets/Resources/Audio/SfxLibrary.asset'i (SfxId -&gt; AudioClip tablosu) tek seferlik kurar/
/// günceller. AudioMixerSetup.cs (Faz A) ile aynı desen: idempotent, menu item, kuru gösterim
/// varyantı. Kaynak eşleme tablosu plans/ses-tasarimi.md §3.1'deki ONAYLANAN seçimlerdir.
///
/// MoneyEarned ve CorrectItem BİLİNÇLİ OLARAK klipsiz bırakılır (plan §3.2 — kullanıcı henüz
/// seçmedi); AudioSlotValidator bu iki slotu boş olarak raporlayacak, bu BEKLENEN bir durumdur,
/// hata değildir — klip seçilince Inspector'dan elle atanır veya bu tabloya eklenip yeniden çalıştırılır.
/// </summary>
public static class SfxLibrarySetup
{
    private const string LibraryPath = "Assets/Resources/Audio/SfxLibrary.asset";

    private static readonly (SfxId id, string clipPath)[] KnownClips =
    {
        (SfxId.CounterTick,    "Assets/Audio/SFX/sfx_counter_tick.ogg"),
        (SfxId.DayEnd,         "Assets/Audio/SFX/sfx_day_end.ogg"),
        (SfxId.QuestComplete,  "Assets/Audio/SFX/sfx_quest_complete.ogg"),
        (SfxId.WrongItem,      "Assets/Audio/SFX/sfx_wrong_item.ogg"),
        (SfxId.RentWarning,    "Assets/Audio/SFX/sfx_rent_warning.ogg"),
        (SfxId.UpgradeBought,  "Assets/Audio/SFX/sfx_upgrade_bought.ogg"),
        (SfxId.Bankrupt,       "Assets/Music/Stinger/Bankrupt - Take 2 (10s) F.ogg"),
        (SfxId.Victory,        "Assets/Music/Stinger/Sixteen Day Finale (Take 1) G.ogg"),
        // MoneyEarned, CorrectItem: BİLİNÇLİ OLARAK burada YOK — klip henüz seçilmedi (plan §3.2).
    };

    /// <summary>Tabloda olmayan (henüz klipsiz) SfxId'ler — asset'te boş slot olarak oluşturulur,
    /// AudioSlotValidator'ın taraması için (bağlantı noktası hazır, klip bekleniyor).</summary>
    private static readonly SfxId[] PendingIds = { SfxId.MoneyEarned, SfxId.CorrectItem };

    [MenuItem("Tools/Cargor/Audio/SFX Kutuphanesini Kur veya Guncelle")]
    public static void KurVeyaGuncelle() => Calistir(kuruGosterim: false);

    [MenuItem("Tools/Cargor/Audio/SFX Kutuphanesini Kur veya Guncelle (kuru gosterim)")]
    public static void KuruGosterim() => Calistir(kuruGosterim: true);

    private static void Calistir(bool kuruGosterim)
    {
        int eklenen = 0, guncellenen = 0, zatenDogru = 0, bulunamayan = 0;

        var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
        bool yeniAsset = library == null;

        if (kuruGosterim)
        {
            var existingIds = new HashSet<SfxId>();
            if (!yeniAsset)
            {
                var so = new SerializedObject(library);
                var entriesProp = so.FindProperty("entries");
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    var idProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                    existingIds.Add((SfxId)idProp.enumValueIndex);
                }
            }

            foreach (var (id, clipPath) in KnownClips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                {
                    Debug.LogError($"[SfxKutuphaneKur] KURU GÖSTERİM: '{id}' için klip BULUNAMADI: {clipPath}");
                    bulunamayan++;
                    continue;
                }
                Debug.Log($"[SfxKutuphaneKur] KURU GÖSTERİM: '{id}' -> {clipPath} {(existingIds.Contains(id) ? "(güncellenecek)" : "(eklenecek)")}");
            }
            foreach (var id in PendingIds)
            {
                Debug.Log($"[SfxKutuphaneKur] KURU GÖSTERİM: '{id}' boş slot olarak {(existingIds.Contains(id) ? "zaten var" : "eklenecek")} (klip henüz seçilmedi).");
            }
            Debug.Log($"[SfxKutuphaneKur] KURU GÖSTERİM tamam. Asset {(yeniAsset ? "YENİ oluşturulacak" : "mevcut, güncellenecek")}: {LibraryPath}");
            return;
        }

        if (yeniAsset)
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Audio");

            library = ScriptableObject.CreateInstance<SfxLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        var serialized = new SerializedObject(library);
        var entries = serialized.FindProperty("entries");

        // id -> array index (mevcut girişleri koru, üstüne yaz — sırası önemsiz).
        var indexById = new Dictionary<SfxId, int>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            var idProp = entries.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            indexById[(SfxId)idProp.enumValueIndex] = i;
        }

        foreach (var (id, clipPath) in KnownClips)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogError($"[SfxKutuphaneKur] '{id}' için klip BULUNAMADI: {clipPath} — atlandı.");
                bulunamayan++;
                continue;
            }

            if (indexById.TryGetValue(id, out int existingIndex))
            {
                var clipProp = entries.GetArrayElementAtIndex(existingIndex).FindPropertyRelative("clip");
                if (clipProp.objectReferenceValue == clip)
                {
                    zatenDogru++;
                }
                else
                {
                    clipProp.objectReferenceValue = clip;
                    guncellenen++;
                }
            }
            else
            {
                int newIndex = entries.arraySize;
                entries.InsertArrayElementAtIndex(newIndex);
                var newEntry = entries.GetArrayElementAtIndex(newIndex);
                newEntry.FindPropertyRelative("id").enumValueIndex = (int)id;
                newEntry.FindPropertyRelative("clip").objectReferenceValue = clip;
                indexById[id] = newIndex;
                eklenen++;
            }
        }

        // Klipsiz (pending) SfxId'ler için de bir slot bulunsun — validator bunu boş diye raporlasın,
        // "hiç girişi bile yok" diye sessizce atlamasın.
        foreach (var id in PendingIds)
        {
            if (indexById.ContainsKey(id))
            {
                zatenDogru++;
                continue;
            }

            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            var newEntry = entries.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("id").enumValueIndex = (int)id;
            newEntry.FindPropertyRelative("clip").objectReferenceValue = null;
            indexById[id] = newIndex;
            eklenen++;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SfxKutuphaneKur] Tamam. Eklenen: {eklenen} · Güncellenen: {guncellenen} · " +
                   $"Zaten doğru: {zatenDogru} · Bulunamayan klip: {bulunamayan}. Asset: {LibraryPath}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string folderName = path.Substring(lastSlash + 1);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
