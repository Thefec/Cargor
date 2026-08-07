using UnityEngine;

/// <summary>
/// Telsiz (Bas-Konuş) sesli iletişim ayarlarını PlayerPrefs üzerinde tutan static sınıf.
/// Desen <see cref="InputBindingManager"/>'daki persistence yaklaşımıyla aynı: tembel
/// kurulum (EnsureInitialized) + PlayerPrefs.Set* + PlayerPrefs.Save().
///
/// NEDEN UnifiedSettingsManager ÜZERİNDEN DEĞİL:
/// UnifiedSettingsManager bir singleton DEĞİL — FindObjectOfType ile bulunuyor
/// (Assets/NewCss/NewPickup/PlayerInventory.cs:337) ve her sahnede sahnede yok
/// (örn. Tutorial). Ses sistemi (RadioVoiceRuntime ve alt sistemleri) bootstrap
/// singleton olduğu için sahne bağımsız çalışmak zorunda; bu yüzden değerler
/// doğrudan PlayerPrefs'ten okunur/yazılır, UnifiedSettingsManager'ın SettingsData
/// katmanına bağımlı olunmaz.
///
/// NEDEN Voice_Volume MASTER İLE ÇARPILMIYOR:
/// UnifiedSettingsManager.ApplyAudioSettings() (Assets/MENUUI/UnifiedSettingsManager.cs:1071)
/// zaten AudioListener.volume = master yapıyor — yani master tüm AudioListener çıkışına
/// dolaylı olarak uygulanmış oluyor. Assets/NewCss/NewPickup/PlayerInventory.Audio.cs:60
/// bunun ÜSTÜNE bir kez daha master ile çarpıyor; bu satır master'ı iki kez uyguluyor
/// (mevcut bir hata). Bu sınıf o hatayı KOPYALAMIYOR: Voice_Volume yalnızca telsiz
/// ses seviyesini ölçekler, master çarpımı playback tarafında (AudioListener.volume
/// üzerinden) zaten dolaylı olarak devrede.
/// </summary>
public static class RadioVoicePrefs
{
    #region Keys & Defaults

    private const string KEY_ENABLED = "Voice_Enabled";
    private const string KEY_VOLUME = "Voice_Volume";
    private const string KEY_SELF_MONITOR = "Voice_SelfMonitor";

    private const bool DEFAULT_ENABLED = true;
    private const float DEFAULT_VOLUME = 1.0f;
    private const bool DEFAULT_SELF_MONITOR = false;

    #endregion

    #region State (tembel kurulum — InputBindingManager.EnsureInitialized deseni)

    private static bool _initialized;
    private static bool _enabled;
    private static float _volume;
    private static bool _selfMonitor;

    #endregion

    #region Public API

    /// <summary>Telsiz tamamen açık mı (kapalıysa yakalama/çalma hiç başlamaz).</summary>
    public static bool IsEnabled()
    {
        EnsureInitialized();
        return _enabled;
    }

    public static void SetEnabled(bool enabled)
    {
        EnsureInitialized();
        if (_enabled == enabled) return;

        _enabled = enabled;
        PlayerPrefs.SetInt(KEY_ENABLED, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>0..1 arası telsiz ses seviyesi. Master ile ÇARPILMAZ (bkz. sınıf yorumu).</summary>
    public static float GetVolume()
    {
        EnsureInitialized();
        return _volume;
    }

    public static void SetVolume(float volume)
    {
        EnsureInitialized();
        volume = Mathf.Clamp01(volume);
        if (Mathf.Approximately(_volume, volume)) return;

        _volume = volume;
        PlayerPrefs.SetFloat(KEY_VOLUME, _volume);
        PlayerPrefs.Save();
    }

    /// <summary>"Kendini Dinle" (loopback) — dev/erişilebilirlik amaçlı, varsayılan kapalı.</summary>
    public static bool IsSelfMonitorEnabled()
    {
        EnsureInitialized();
        return _selfMonitor;
    }

    public static void SetSelfMonitorEnabled(bool enabled)
    {
        EnsureInitialized();
        if (_selfMonitor == enabled) return;

        _selfMonitor = enabled;
        PlayerPrefs.SetInt(KEY_SELF_MONITOR, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Kullanıcı bu üç ayardan en az birini daha önce elle değiştirip kaydetmiş mi.
    /// Üçü de hiç yazılmamışsa (temiz kurulum) false döner — UI ilk açılışta
    /// "varsayılan" rozetini göstermek gibi kozmetik kararlar için kullanılabilir.
    /// </summary>
    public static bool HasAny()
    {
        return PlayerPrefs.HasKey(KEY_ENABLED)
            || PlayerPrefs.HasKey(KEY_VOLUME)
            || PlayerPrefs.HasKey(KEY_SELF_MONITOR);
    }

    #endregion

    #region Initialization

    private static void EnsureInitialized()
    {
        if (_initialized) return;

        _enabled = PlayerPrefs.HasKey(KEY_ENABLED)
            ? PlayerPrefs.GetInt(KEY_ENABLED) == 1
            : DEFAULT_ENABLED;

        _volume = PlayerPrefs.HasKey(KEY_VOLUME)
            ? Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_VOLUME))
            : DEFAULT_VOLUME;

        _selfMonitor = PlayerPrefs.HasKey(KEY_SELF_MONITOR)
            ? PlayerPrefs.GetInt(KEY_SELF_MONITOR) == 1
            : DEFAULT_SELF_MONITOR;

        _initialized = true;
    }

    #endregion
}
