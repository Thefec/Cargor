// Sahnedeki henuz Localization'a baglanmamis TMP metinlerini otomatik baglar:
// StringTable Shared Data'da yeni key acar, TR degerini sahnedeki mevcut metinden tasir
// (elle yeniden yazilmaz), LocalizeStringEvent component'i ekleyip TMP'nin "text"
// setter'ina baglar. EN degeri BOS birakilir - ayri bir turda doldurulur.
//
// Kullanim: sahneyi ac -> menu: Cargor / Localization / Aktif Sahneyi Otomatik Bagla
// Geri alma: component eklemeleri Undo'ya kayitli (Ctrl+Z); StringTable asset
// degisiklikleri icin git diff/checkout kullan.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;

public static class LocalizationAutoWire
{
    private const string TABLE_COLLECTION_NAME = "StringTable";
    private const string TR_LOCALE_CODE = "tr";

    [MenuItem("Cargor/Localization/Aktif Sahneyi Otomatik Bağla")]
    public static void WireActiveScene()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(TABLE_COLLECTION_NAME);
        if (collection == null)
        {
            Debug.LogError($"[LocalizationAutoWire] '{TABLE_COLLECTION_NAME}' adında bir String Table Collection bulunamadı.");
            return;
        }

        var stringTables = collection.StringTables;
        bool hasTr = stringTables.Any(t => t.LocaleIdentifier.Code == TR_LOCALE_CODE);
        if (!hasTr)
        {
            Debug.LogError($"[LocalizationAutoWire] '{TR_LOCALE_CODE}' locale tablosu bulunamadı.");
            return;
        }

        var existingKeys = new HashSet<string>(
            collection.SharedData.Entries.Select(e => e.Key), StringComparer.OrdinalIgnoreCase);

        // Ayni TR metni tasiyan bir key zaten varsa yeni key acmak yerine onu yeniden kullan
        // (Return/Return2/Return3 gibi anlamsiz coklu key birikimini onler).
        var trTableForReuse = stringTables.First(t => t.LocaleIdentifier.Code == TR_LOCALE_CODE);
        var textToEntryId = new Dictionary<string, long>();
        foreach (var sharedEntry in collection.SharedData.Entries)
        {
            var trEntry = trTableForReuse.GetEntry(sharedEntry.Id);
            if (trEntry == null || string.IsNullOrEmpty(trEntry.LocalizedValue)) continue;
            if (!textToEntryId.ContainsKey(trEntry.LocalizedValue))
                textToEntryId[trEntry.LocalizedValue] = sharedEntry.Id;
        }

        var allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);

        int wired = 0, skippedNoise = 0, skippedAlready = 0, reused = 0;
        var manualReview = new List<string>();

        foreach (var tmp in allTexts)
        {
            var go = tmp.gameObject;
            var existingEvent = go.GetComponent<LocalizeStringEvent>();
            // Bos StringReference'li (entryId == 0) LocalizeStringEvent yarim kalmis wiring'dir
            // (orn. elle eklenip hic bagliskanamamis) - bunu atlarsak sonsuza kadar orphan kalir.
            bool hasRealBinding = existingEvent != null && existingEvent.StringReference.TableEntryReference.KeyId != SharedTableData.EmptyId;
            if (hasRealBinding)
            {
                skippedAlready++;
                continue;
            }

            string trimmed = (tmp.text ?? string.Empty).Trim();
            if (IsNoise(trimmed))
            {
                skippedNoise++;
                continue;
            }

            long entryId;
            if (textToEntryId.TryGetValue(trimmed, out var reusedId))
            {
                entryId = reusedId;
                reused++;
            }
            else
            {
                string key = MakeUniqueKey(trimmed, go.name, existingKeys);
                existingKeys.Add(key);

                var entry = collection.SharedData.AddKey(key);
                foreach (var table in stringTables)
                {
                    bool isTr = table.LocaleIdentifier.Code == TR_LOCALE_CODE;
                    table.AddEntry(entry.Id, isTr ? trimmed : string.Empty);
                }
                entryId = entry.Id;
                textToEntryId[trimmed] = entryId;
            }

            var localizeEvent = existingEvent != null
                ? existingEvent // yarim kalmis component'i yeniden kullan, ikinci bir tane ekleme
                : Undo.AddComponent<LocalizeStringEvent>(go);

            localizeEvent.StringReference.TableReference = collection.SharedData.TableCollectionNameGuid;
            localizeEvent.StringReference.TableEntryReference = entryId;

            // Yarim kalmis component'lerde OnUpdateString dinleyicisi de bos olabilir (gorulen orneklerde
            // oyleydi) - varligini kontrol etmeden atlarsak key bagli ama runtime'da metin hic guncellenmez.
            if (localizeEvent.OnUpdateString.GetPersistentEventCount() == 0)
            {
                var setMethod = typeof(TMP_Text).GetProperty("text")?.GetSetMethod();
                if (setMethod == null)
                {
                    Debug.LogWarning($"[LocalizationAutoWire] '{go.name}' için text setter bulunamadı, atlandı.");
                    continue;
                }
                var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), tmp, setMethod);
                UnityEventTools.AddPersistentListener(localizeEvent.OnUpdateString, setter);
                localizeEvent.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.RuntimeOnly);
            }

            EditorUtility.SetDirty(go);
            wired++;

            // Kisa/belirsiz metinler (olasi ikon/kod) - kullaniciya isaretle, otomatik bagla ama gozden gecirilsin
            if (trimmed.Length < 4 && !Regex.IsMatch(trimmed, @"^[A-ZÇĞİÖŞÜ ]+$"))
                manualReview.Add($"{go.name} -> \"{trimmed}\" (id: {entryId})");
        }

        EditorUtility.SetDirty(collection.SharedData);
        foreach (var table in stringTables) EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[LocalizationAutoWire] Sahne: {EditorSceneManager.GetActiveScene().name} | Bağlandı: {wired} (yeni key: {wired - reused}, mevcut key yeniden kullanıldı: {reused}) | Zaten bağlıydı: {skippedAlready} | Gürültü (atlandı): {skippedNoise}");
        if (manualReview.Count > 0)
            Debug.LogWarning("[LocalizationAutoWire] Kısa/belirsiz metin, gözden geçir:\n" + string.Join("\n", manualReview));
    }

    // Turkce ceviriler genelde Ingilizce kaynaktan daha uzun/kisa oluyor ve sabit boyutlu
    // kutular tasabiliyor. Bu, yerellestirilmis (LocalizeStringEvent'e bagli) her TMP'de
    // TMP'nin "Auto Size" ozelligini acar: metin kutuya sigmayinca font otomatik kuculur.
    [MenuItem("Cargor/Localization/Yerelleştirilmiş Metinlere Auto Size Uygula")]
    public static void ApplyAutoSizeToLocalizedTexts()
    {
        ApplyAutoSizeToActiveScene();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static (int applied, int alreadyOn, int skippedNoLocalize) ApplyAutoSizeToActiveScene()
    {
        var allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        int applied = 0, alreadyOn = 0, skippedNoLocalize = 0;

        foreach (var tmp in allTexts)
        {
            if (tmp.GetComponent<LocalizeStringEvent>() == null) { skippedNoLocalize++; continue; }
            if (tmp.enableAutoSizing) { alreadyOn++; continue; }

            Undo.RecordObject(tmp, "TMP Auto Size Aç");
            float max = tmp.fontSize > 0 ? tmp.fontSize : 36f;
            float min = Mathf.Max(max * 0.5f, 6f);
            tmp.fontSizeMax = max;
            tmp.fontSizeMin = min;
            tmp.enableAutoSizing = true;
            EditorUtility.SetDirty(tmp);
            applied++;
        }

        Debug.Log($"[LocalizationAutoWire] Auto Size uygulandı: {applied} | zaten açıktı: {alreadyOn} | yerelleştirilmemiş (atlandı): {skippedNoLocalize}");
        return (applied, alreadyOn, skippedNoLocalize);
    }

    // CLI/batchmode giris noktasi: Editor acik degilken -batchmode -executeMethod ile cagrilir.
    // Sahneyi kendisi acar + kaydeder, boylece kullanicinin Editor'de tek tek acmasi gerekmez.
    // Kullanim: Unity.exe -batchmode -nographics -projectPath <proje> -quit
    //   -executeMethod LocalizationAutoWire.BatchApplyAutoSizeToKnownScenes
    public static void BatchApplyAutoSizeToKnownScenes()
    {
        string[] scenePaths =
        {
            "Assets/MainMenu/MainMenu.unity",
        };

        foreach (var scenePath in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var (applied, alreadyOn, skipped) = ApplyAutoSizeToActiveScene();
            if (applied > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[LocalizationAutoWire] {scenePath} kaydedildi ({applied} TMP güncellendi).");
            }
            else
            {
                Debug.Log($"[LocalizationAutoWire] {scenePath}: değişiklik yok, kaydetme atlandı.");
            }
        }
    }

    // WireActiveScene GameObject adindan bagimsiz calisiyor; runtime'da dinamik guncellenen
    // metinler (sayfa sayaci, kalan sure vb.) yanlislikla statik key'e baglanabilir - o obje
    // her locale degisikliginde sabit metne donerdi. Bu, tek seferlik nokta duzeltmesi icin.
    public static void BatchRemoveLocalizeFromPageIndicator()
    {
        const string scenePath = "Assets/Scenes/The Main Office.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        int removed = 0;
        foreach (var tmp in allTexts)
        {
            if (tmp.gameObject.name != "PageIndicator") continue;
            var evt = tmp.gameObject.GetComponent<LocalizeStringEvent>();
            if (evt == null) continue;
            UnityEngine.Object.DestroyImmediate(evt, true);
            EditorUtility.SetDirty(tmp.gameObject);
            removed++;
        }

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"[LocalizationAutoWire] PageIndicator temizligi: {removed} component kaldirildi, sahne kaydedildi.");
    }

    // CLI/batchmode giris noktasi: Unity kapaliyken -batchmode -executeMethod ile WireActiveScene'in
    // aynisini calistirir (sahneyi acar, wiring yapar, kaydeder). Editor acikken KULLANMA - canli
    // Editor'un ayni sahneyi ayni anda diske yazmasi bozulmaya yol acabilir.
    public static void BatchWireMainOffice()
    {
        const string scenePath = "Assets/Scenes/The Main Office.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        WireActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[LocalizationAutoWire] {scenePath} kaydedildi (batch wiring tamam).");
    }

    // BatchWireMainOffice + PageIndicator temizligini AYNI Unity oturumunda, tek acilista yapar
    // (iki ayri batchmode koşumu yerine) - notebook sayfalari eklendiginde WireActiveScene
    // PageIndicator'i (dinamik sayfa sayaci) yanlislikla statik key'e baglar, o objeyi hemen
    // ayni koşumda geri temizler.
    public static void BatchWireAndCleanMainOffice()
    {
        const string scenePath = "Assets/Scenes/The Main Office.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        WireActiveScene();

        var allTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        int removed = 0;
        foreach (var tmp in allTexts)
        {
            if (tmp.gameObject.name != "PageIndicator") continue;
            var evt = tmp.gameObject.GetComponent<LocalizeStringEvent>();
            if (evt == null) continue;
            UnityEngine.Object.DestroyImmediate(evt, true);
            EditorUtility.SetDirty(tmp.gameObject);
            removed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[LocalizationAutoWire] {scenePath} kaydedildi (wiring + PageIndicator temizligi tamam, {removed} PageIndicator temizlendi).");
    }

    private static bool IsNoise(string t)
    {
        if (string.IsNullOrEmpty(t)) return true;
        if (t.Length <= 1) return true;
        if (Regex.IsMatch(t, @"^-?\d+([.,]\d+)?%?$")) return true; // saf sayi/yuzde (runtime deger)
        if (Regex.IsMatch(t, @"^[​\s]*$")) return true; // zero-width space / bosluk
        return false;
    }

    private static string MakeUniqueKey(string text, string fallbackName, HashSet<string> existingKeys)
    {
        string baseKey = Slugify(text);
        if (string.IsNullOrEmpty(baseKey)) baseKey = Slugify(fallbackName);
        if (string.IsNullOrEmpty(baseKey)) baseKey = "Text";
        if (baseKey.Length > 40) baseKey = baseKey.Substring(0, 40);

        string candidate = baseKey;
        int suffix = 2;
        while (existingKeys.Contains(candidate))
        {
            candidate = baseKey + suffix;
            suffix++;
        }
        return candidate;
    }

    private static string Slugify(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var words = Regex.Matches(s, @"[A-Za-zÇĞİÖŞÜçğıöşü0-9]+")
            .Cast<Match>()
            .Select(m => m.Value)
            .Take(5);

        var sb = new StringBuilder();
        foreach (var w in words)
        {
            sb.Append(char.ToUpperInvariant(w[0]));
            if (w.Length > 1) sb.Append(w.Substring(1).ToLowerInvariant());
        }
        return sb.ToString();
    }
}
