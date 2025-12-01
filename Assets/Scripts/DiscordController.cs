using UnityEngine;
using UnityEngine.SceneManagement;
using Discord;

public class DiscordController : MonoBehaviour
{
    [Header("Discord Ayarları")]
    [Tooltip("Discord Developer Portal'dan aldığın Application ID")]
    public long applicationId = 1234567890123456789; // Kendi ID'ni gir! 

    [Header("Görsel Ayarları")]
    public string largeImageKey = "game_logo"; // Discord Developer Portal'a yüklediğin görselin adı

    private Discord.Discord discord;
    private static bool instanceExists;
    private static DiscordController instance;
    private long startTime;

    private float updateInterval = 4f;
    private float nextUpdate = 0f;
    private bool isDiscordRunning = false;

    // Discord'dan gelen dil kodu (örn: "tr", "en", "de")
    private string currentDiscordLang = "en";

    void Awake()
    {
        if (!instanceExists)
        {
            instanceExists = true;
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        startTime = System.DateTimeOffset.Now.ToUnixTimeSeconds();
        InitializeDiscord();
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    void InitializeDiscord()
    {
        try
        {
            discord = new Discord.Discord(applicationId, (ulong)Discord.CreateFlags.NoRequireDiscord);
            isDiscordRunning = true;

            // İlk açılışta dili çekmeye çalış
            FetchDiscordLanguage();

            Debug.Log("<color=green>[Discord]</color> SDK başarıyla başlatıldı!");
            UpdateActivity();
        }
        catch (System.Exception e)
        {
            isDiscordRunning = false;
            Debug.LogWarning($"<color=yellow>[Discord]</color> Başlatılamadı: {e.Message}");
        }
    }

    void Update()
    {
        if (!isDiscordRunning || discord == null) return;

        try
        {
            discord.RunCallbacks();
        }
        catch (System.Exception)
        {
            isDiscordRunning = false;
        }

        if (Time.time >= nextUpdate)
        {
            // Periyodik olarak dili kontrol et (Kullanıcı Discord dilini değiştirirse anında yansısın)
            FetchDiscordLanguage();
            UpdateActivity();
            nextUpdate = Time.time + updateInterval;
        }
    }

    void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        UpdateActivity();
    }

    // Discord'un o anki dilini öğrenen fonksiyon
    void FetchDiscordLanguage()
    {
        if (discord == null) return;
        try
        {
            var appManager = discord.GetApplicationManager();
            string locale = appManager.GetCurrentLocale(); // Örn: "tr", "en-US", "de" döner

            // Gelen veri boşsa varsayılan İngilizce yap
            if (string.IsNullOrEmpty(locale))
            {
                currentDiscordLang = "en";
            }
            else
            {
                // Sadece ilk 2 harfi al (en-US yerine en, tr yerine tr)
                currentDiscordLang = locale.Length >= 2 ? locale.Substring(0, 2).ToLower() : "en";
            }
        }
        catch
        {
            currentDiscordLang = "en";
        }
    }

    void UpdateActivity()
    {
        if (!isDiscordRunning || discord == null) return;

        try
        {
            var activityManager = discord.GetActivityManager();

            var activity = new Discord.Activity
            {
                // Details: Oyunun genel durumu (Örn: Kutuları Tekmeliyor)
                Details = GetLocalizedDetails(),

                // State: Hangi sahnede olduğu (Örn: Ana Menü, Level 1)
                State = GetFormattedMapName(),

                Assets =
                {
                    LargeImage = largeImageKey,
                    // Mouse ile resmin üzerine gelince çıkan yazı (Oyunun adı veya sloganı)
                    LargeText = "Cargor"
                },

                Timestamps =
                {
                    Start = startTime
                }
            };

            activityManager.UpdateActivity(activity, (result) =>
            {
                if (result != Discord.Result.Ok)
                {
                    // Debug.LogWarning("Update Hatası: " + result);
                }
            });
        }
        catch (System.Exception e)
        {
            isDiscordRunning = false;
        }
    }

    #region Çoklu Dil Desteği (Discord Diline Göre)

    // "Details" kısmı: Oyuncunun ne yaptığını açıklar
    string GetLocalizedDetails()
    {
        // currentDiscordLang değişkenine göre switch yapıyoruz
        return currentDiscordLang switch
        {
            "tr" => "Kutuları Tekmeliyor",
            "en" => "Kicking The Boxes",
            "de" => "Kisten Treten",       // Almanca
            "fr" => "Frapper Les Boîtes",  // Fransızca
            "es" => "Pateando Cajas",      // İspanyolca
            "ru" => "Пинаю Коробки",       // Rusça
            "ja" => "箱を蹴る",             // Japonca
            "pt" => "Chutando Caixas",     // Portekizce
            "it" => "Calciando Scatole",   // İtalyanca
            _ => "Kicking The Boxes"       // Tanınmayan diller için İngilizce
        };
    }

    // Sahne isimlerini formatlayan kısım
    string GetFormattedMapName()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        // Önce sahne isminin çevirisini alalım
        string localizedMapName = sceneName switch
        {
            "MainMenu" => GetLocalizedText("main_menu"),
            "Menu" => GetLocalizedText("main_menu"),
            "Level_01" => GetLocalizedText("level") + " 1",
            "Level_02" => GetLocalizedText("level") + " 2",
            "Level_03" => GetLocalizedText("level") + " 3",
            "Level1" => GetLocalizedText("level") + " 1",
            "Level2" => GetLocalizedText("level") + " 2",
            _ => sceneName
        };

        // Sonra o dile özgü "Harita" veya "Konum" ekini getirelim (Opsiyonel, istersen kaldırabilirsin)
        // Eğer sadece "Ana Menü" yazsın istiyorsan prefix kullanmayabilirsin.
        // Aşağıdaki format: "Harita: Ana Menü" veya "Map: Main Menu" şeklindedir.

        string prefix = GetMapPrefix();
        return $"{prefix}: {localizedMapName}";
    }

    // Sahne adları için kelime sözlüğü
    string GetLocalizedText(string key)
    {
        return key switch
        {
            "main_menu" => currentDiscordLang switch
            {
                "tr" => "Ana Menü",
                "de" => "Hauptmenü",
                "fr" => "Menu Principal",
                "es" => "Menú Principal",
                "ru" => "Главное Меню",
                _ => "Main Menu"
            },
            "level" => currentDiscordLang switch
            {
                "tr" => "Seviye",
                "de" => "Level",
                "fr" => "Niveau",
                "es" => "Nivel",
                "ru" => "Уровень",
                _ => "Level"
            },
            _ => key
        };
    }

    // "Harita" kelimesinin çevirisi
    string GetMapPrefix()
    {
        return currentDiscordLang switch
        {
            "tr" => "Harita",
            "de" => "Karte",
            "fr" => "Carte",
            "es" => "Mapa",
            "ru" => "Карта",
            "ja" => "マップ",
            _ => "Map"
        };
    }

    #endregion

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        if (discord != null)
        {
            discord.Dispose();
            discord = null;
        }
    }

    void OnApplicationQuit()
    {
        if (discord != null)
        {
            discord.Dispose();
            discord = null;
        }
    }
}