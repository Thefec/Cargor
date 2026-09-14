using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using NewCss.Audio;
using Object = UnityEngine.Object;

/// <summary>
/// CargorMixer.mixer'ı (Master &gt; Music, SFX + MusicVol/SfxVol exposed parametreleri) ve
/// onu Resources'tan yükleyen AudioRoutingConfig asset'ini tek seferlik kurar.
///
/// NEDEN: Projede hiç AudioMixer yoktu — Müzik/SFX slider'ları sadece 2 elle-bağlı
/// AudioSource'u etkiliyordu, oyun içi SFX'in tamamı sessizce slider'ı atlıyordu.
/// Kaynak: plans/ses-tasarimi.md §2 (eksik #1) ve §5 Faz A.
///
/// NEDEN REFLECTION: Mixer'ı kod ile kurmak için gereken UnityEditor.Audio.AudioMixerController
/// / AudioMixerGroupController / ExposedAudioParameter bu Unity sürümünde derleme zamanında
/// erişilemez (CS0122 — internal, unity_reflect bunu göstermiyor çünkü sadece üye yapısını
/// listeliyor, erişim düzeyini değil). Editor tam güvenilir (full-trust) süreç olduğu için
/// System.Reflection ile çağırmak derleme zamanı erişim kontrolünü atlıyor ve çalışıyor —
/// aynı teknik topluluk mixer-otomasyon script'lerinde de kullanılıyor. Döndürülen nesneler
/// gerçekte UnityEngine.Audio.AudioMixer / AudioMixerGroup alt sınıfları OLDUĞU için, public
/// temel tiplere cast edilip mümkün olan her yerde normal (reflection'sız) API kullanılıyor;
/// reflection sadece Controller'a özel üyelerde (grup oluşturma, exposed parametreler) devreye
/// giriyor.
///
/// İdempotent: grup/parametre/asset zaten doğruysa dokunmaz, "Değişti: False" basar.
/// Grup/parametre adları AudioVolumeMath (Assets/NewCss/Audio/Core/AudioVolumeMath.cs)
/// ile birebir eşleşmek ZORUNDA — orada da aynı sözleşme test ediliyor.
/// </summary>
public static class AudioMixerSetup
{
    private const string MixerPath = "Assets/Audio/CargorMixer.mixer";
    private const string ConfigPath = "Assets/Resources/Audio/AudioRoutingConfig.asset";
    private const string MasterGroupName = "Master";

    private const BindingFlags InstancePublic = BindingFlags.Public | BindingFlags.Instance;
    private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;

    [MenuItem("Tools/Cargor/Audio/Mixer Kur veya Dogrula")]
    public static void KurVeyaDogrula() => Calistir(kuruGosterim: false);

    [MenuItem("Tools/Cargor/Audio/Mixer Kur veya Dogrula (kuru gosterim)")]
    public static void KuruGosterim() => Calistir(kuruGosterim: true);

    private static void Calistir(bool kuruGosterim)
    {
        string musicGroupName = AudioVolumeMath.GroupNameFor(AudioCategory.Music);
        string sfxGroupName = AudioVolumeMath.GroupNameFor(AudioCategory.SFX);
        string musicParam = AudioVolumeMath.ExposedParamFor(AudioCategory.Music);
        string sfxParam = AudioVolumeMath.ExposedParamFor(AudioCategory.SFX);

        Type controllerType = typeof(AssetDatabase).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
        if (controllerType == null)
        {
            Debug.LogError("[MixerKur] UnityEditor.Audio.AudioMixerController bulunamadı — Unity sürümü değişmiş olabilir.");
            return;
        }

        object controllerRaw = AssetDatabase.LoadAssetAtPath(MixerPath, controllerType);
        bool mixerVarMiydi = controllerRaw != null;

        if (controllerRaw == null)
        {
            if (kuruGosterim)
            {
                Debug.Log($"[MixerKur] KURU GÖSTERİM: {MixerPath} yeni oluşturulacaktı " +
                          $"(Master > {musicGroupName}, {sfxGroupName}; exposed {musicParam}, {sfxParam}).");
                return;
            }

            EnsureFolder("Assets/Audio");
            MethodInfo createAtPath = controllerType.GetMethod("CreateMixerControllerAtPath", StaticPublic);
            controllerRaw = createAtPath.Invoke(null, new object[] { MixerPath });
        }

        var controller = (AudioMixer)controllerRaw;

        object masterGroupRaw = FindGroupRaw(controller, MasterGroupName);
        object musicGroupRaw = FindGroupRaw(controller, musicGroupName);
        object sfxGroupRaw = FindGroupRaw(controller, sfxGroupName);

        bool degisti = false;

        if (musicGroupRaw == null)
        {
            if (kuruGosterim) Debug.Log($"[MixerKur] KURU: '{musicGroupName}' grubu Master altına eklenecekti.");
            else
            {
                musicGroupRaw = CreateChildGroup(controllerType, controllerRaw, masterGroupRaw, musicGroupName);
                degisti = true;
            }
        }

        if (sfxGroupRaw == null)
        {
            if (kuruGosterim) Debug.Log($"[MixerKur] KURU: '{sfxGroupName}' grubu Master altına eklenecekti.");
            else
            {
                sfxGroupRaw = CreateChildGroup(controllerType, controllerRaw, masterGroupRaw, sfxGroupName);
                degisti = true;
            }
        }

        if (!kuruGosterim)
        {
            var pairs = new[]
            {
                (groupRaw: musicGroupRaw, param: musicParam),
                (groupRaw: sfxGroupRaw, param: sfxParam),
            };

            foreach (var pair in pairs)
            {
                if (pair.groupRaw == null) continue;
                degisti |= EnsureExposedVolume(controllerType, controllerRaw, pair.groupRaw, pair.param);
            }

            if (degisti) EditorUtility.SetDirty((Object)controllerRaw);
        }

        bool configDegisti = false;
        if (!kuruGosterim && musicGroupRaw != null && sfxGroupRaw != null)
        {
            configDegisti = EnsureRoutingConfig((Object)controllerRaw, (Object)musicGroupRaw, (Object)sfxGroupRaw);
        }
        else if (kuruGosterim)
        {
            bool configExists = AssetDatabase.LoadAssetAtPath<AudioRoutingConfig>(ConfigPath) != null;
            Debug.Log(configExists
                ? "[MixerKur] KURU: AudioRoutingConfig zaten var, referanslar kuru modda kontrol edilmedi."
                : $"[MixerKur] KURU: {ConfigPath} yeni oluşturulacaktı.");
        }

        if (!kuruGosterim && (degisti || configDegisti))
        {
            AssetDatabase.SaveAssets();
        }

        string baslik = kuruGosterim ? "KURU GÖSTERİM" : (mixerVarMiydi ? "DOĞRULANDI" : "OLUŞTURULDU");
        Debug.Log($"[MixerKur] {baslik}: {MixerPath} — Master > {musicGroupName}, {sfxGroupName}; " +
                  $"exposed: {musicParam}, {sfxParam}. Mixer değişti: {degisti} · Config değişti: {configDegisti}");
    }

    /// <summary>Master dahil herhangi bir grubu adına göre bulur — public
    /// AudioMixer.FindMatchingGroups üzerinden, reflection'sız.</summary>
    private static object FindGroupRaw(AudioMixer controller, string name)
    {
        AudioMixerGroup[] matches = controller.FindMatchingGroups(name);
        return matches.FirstOrDefault(g => g.name == name);
    }

    private static object CreateChildGroup(Type controllerType, object controllerRaw, object parentGroupRaw, string name)
    {
        MethodInfo createNewGroup = controllerType.GetMethod("CreateNewGroup", InstancePublic);
        object newGroup = createNewGroup.Invoke(controllerRaw, new object[] { name, false });

        MethodInfo addChildToParent = controllerType.GetMethod("AddChildToParent", InstancePublic);
        addChildToParent.Invoke(controllerRaw, new[] { newGroup, parentGroupRaw });

        return newGroup;
    }

    private static bool EnsureExposedVolume(Type controllerType, object controllerRaw, object groupRaw, string paramName)
    {
        Type groupType = groupRaw.GetType();
        MethodInfo getGuidForVolume = groupType.GetMethod("GetGUIDForVolume", InstancePublic);
        object guid = getGuidForVolume.Invoke(groupRaw, null);

        PropertyInfo exposedParamsProp = controllerType.GetProperty("exposedParameters", InstancePublic);
        Array existing = (Array)exposedParamsProp.GetValue(controllerRaw) ?? Array.CreateInstance(GetExposedParamType(), 0);
        Type exposedParamType = GetExposedParamType();
        FieldInfo guidField = exposedParamType.GetField("guid");
        FieldInfo nameField = exposedParamType.GetField("name");

        foreach (object entry in existing)
        {
            bool sameName = (string)nameField.GetValue(entry) == paramName;
            bool sameGuid = guid.Equals(guidField.GetValue(entry));
            if (sameName && sameGuid) return false; // zaten doğru
        }

        // Aynı isimde ama farklı guid'e bağlı eski bir kayıt varsa (yeniden kurulum senaryosu) at.
        var kept = existing.Cast<object>().Where(entry => (string)nameField.GetValue(entry) != paramName).ToList();

        object newEntry = Activator.CreateInstance(exposedParamType);
        guidField.SetValue(newEntry, guid);
        nameField.SetValue(newEntry, paramName);
        kept.Add(newEntry);

        Array updated = Array.CreateInstance(exposedParamType, kept.Count);
        for (int i = 0; i < kept.Count; i++) updated.SetValue(kept[i], i);

        exposedParamsProp.SetValue(controllerRaw, updated);
        return true;
    }

    private static Type _exposedParamType;
    private static Type GetExposedParamType() =>
        _exposedParamType ??= typeof(AssetDatabase).Assembly.GetType("UnityEditor.Audio.ExposedAudioParameter");

    private static bool EnsureRoutingConfig(Object controller, Object musicGroup, Object sfxGroup)
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Audio");

        var config = AssetDatabase.LoadAssetAtPath<AudioRoutingConfig>(ConfigPath);
        bool yeni = config == null;

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<AudioRoutingConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        var so = new SerializedObject(config);
        bool degisti = yeni;
        degisti |= SetObjectRefIfDifferent(so, "mixer", controller);
        degisti |= SetObjectRefIfDifferent(so, "musicGroup", musicGroup);
        degisti |= SetObjectRefIfDifferent(so, "sfxGroup", sfxGroup);

        if (degisti)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        return degisti;
    }

    private static bool SetObjectRefIfDifferent(SerializedObject so, string propertyName, Object value)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError($"[MixerKur] AudioRoutingConfig üzerinde SerializedProperty bulunamadı: {propertyName}");
            return false;
        }

        if (prop.objectReferenceValue == value) return false;

        prop.objectReferenceValue = value;
        return true;
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
