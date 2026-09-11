using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NewCss;

/// <summary>
/// Genel pickup/drop sesini (Character.prefab, PlayerInventory) ve telefon çağrı
/// başarı sesini (The Main Office.unity, PhoneCallManager.successCallSound) günceller.
/// Geçici Editor aracı — tek kerelik ses değişimi için yazıldı.
/// </summary>
public static class GeneralSoundSetup
{
    private const string CharacterPrefabPath =
        "Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Character.prefab";
    private const string MainOfficeScenePath = "Assets/Scenes/The Main Office.unity";

    private const string PickClipPath = "Assets/Music/Pick.wav";
    private const string DropClipPath = "Assets/Music/drop.wav";
    private const string PhoneClipPath = "Assets/Music/telefon.wav";

    [MenuItem("Tools/Cargor/Audio/Setup Pickup-Drop-Phone Sounds")]
    public static void Run()
    {
        AssetDatabase.ImportAsset(PickClipPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(DropClipPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(PhoneClipPath, ImportAssetOptions.ForceUpdate);

        var pickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PickClipPath);
        var dropClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DropClipPath);
        var phoneClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PhoneClipPath);

        if (pickClip == null || dropClip == null || phoneClip == null)
        {
            Debug.LogError($"[GeneralSoundSetup] Clip yüklenemedi. Pick={pickClip}, Drop={dropClip}, Phone={phoneClip}");
            return;
        }

        UpdateCharacterPrefab(pickClip, dropClip);
        UpdateMainOfficePhone(phoneClip);
    }

    private static void UpdateCharacterPrefab(AudioClip pickClip, AudioClip dropClip)
    {
        var contents = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
        var inventory = contents.GetComponentInChildren<PlayerInventory>(true);
        if (inventory == null)
        {
            Debug.LogError("[GeneralSoundSetup] Character.prefab içinde PlayerInventory bulunamadı.");
            PrefabUtility.UnloadPrefabContents(contents);
            return;
        }

        var so = new SerializedObject(inventory);
        so.FindProperty("pickupSound").objectReferenceValue = pickClip;
        so.FindProperty("dropSound").objectReferenceValue = dropClip;
        so.FindProperty("placeOnTableSound").objectReferenceValue = dropClip;
        so.FindProperty("placeOnShelfSound").objectReferenceValue = dropClip;
        so.FindProperty("takeFromTableSound").objectReferenceValue = pickClip;
        so.FindProperty("takeFromShelfSound").objectReferenceValue = pickClip;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(contents, CharacterPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);

        Debug.Log("[GeneralSoundSetup] Character.prefab: pickup/drop aile sesleri güncellendi.");
    }

    private static void UpdateMainOfficePhone(AudioClip phoneClip)
    {
        var scene = EditorSceneManager.OpenScene(MainOfficeScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[GeneralSoundSetup] Sahne açılamadı: " + MainOfficeScenePath);
            return;
        }

        var phoneManager = Object.FindFirstObjectByType<PhoneCallManager>(FindObjectsInactive.Include);
        if (phoneManager == null)
        {
            Debug.LogError("[GeneralSoundSetup] Sahnede PhoneCallManager bulunamadı.");
            return;
        }

        var pmSO = new SerializedObject(phoneManager);
        var successCallSoundProp = pmSO.FindProperty("successCallSound");
        var audioSource = successCallSoundProp.objectReferenceValue as AudioSource;
        if (audioSource == null)
        {
            Debug.LogError("[GeneralSoundSetup] PhoneCallManager.successCallSound atanmamış, AudioSource bulunamadı.");
            return;
        }

        audioSource.resource = phoneClip;
        audioSource.clip = phoneClip;
        EditorUtility.SetDirty(audioSource);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GeneralSoundSetup] The Main Office: successCallSound -> telefon.wav bağlandı. Kaydedildi: {saved}");
    }
}
