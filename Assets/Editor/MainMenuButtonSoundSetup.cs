using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// MainMenu.unity sahnesine (halihazırda mevcut ama hiçbir sahneye bağlanmamış)
/// UIButtonSoundManager'ı ekler ve tıklama sesini click3.ogg'a bağlar.
/// Geçici Editor aracı — tek kerelik kurulum için yazıldı. İDEMPOTENT: sahnede
/// zaten bir UIButtonSoundManager varsa yeniden kullanılır, çoğaltılmaz.
/// </summary>
public static class MainMenuButtonSoundSetup
{
    private const string ScenePath = "Assets/MainMenu/MainMenu.unity";
    private const string ClickSoundPath = "Assets/MENUUI/click3.ogg";

    [MenuItem("Tools/Cargor/UI/Setup MainMenu Button Sounds")]
    public static void Run()
    {
        AssetDatabase.ImportAsset(ClickSoundPath, ImportAssetOptions.ForceUpdate);
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickSoundPath);
        if (clip == null)
        {
            Debug.LogError("[MainMenuButtonSoundSetup] click3.ogg AudioClip olarak yüklenemedi: " + ClickSoundPath);
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[MainMenuButtonSoundSetup] Sahne açılamadı: " + ScenePath);
            return;
        }

        var managerGO = GameObject.Find("UIButtonSoundManager");
        if (managerGO == null)
        {
            managerGO = new GameObject("UIButtonSoundManager");
            Debug.Log("[MainMenuButtonSoundSetup] UIButtonSoundManager objesi oluşturuldu.");
        }
        else
        {
            Debug.Log("[MainMenuButtonSoundSetup] UIButtonSoundManager zaten var, yeniden kullanılıyor.");
        }

        var manager = managerGO.GetComponent<UIButtonSoundManager>();
        if (manager == null)
        {
            manager = managerGO.AddComponent<UIButtonSoundManager>();
        }

        var so = new SerializedObject(manager);
        so.FindProperty("buttonClickSound").objectReferenceValue = clip;
        so.FindProperty("clickVolume").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();

        int buttonCount = Object.FindObjectsOfType<UnityEngine.UI.Button>(true).Length;

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MainMenuButtonSoundSetup] Sahne kaydedildi: {saved}. " +
                  $"UIButtonSoundManager.buttonClickSound=click3.ogg bağlandı. Sahnedeki buton sayısı: {buttonCount}.");
    }
}
