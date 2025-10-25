using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicPlayer : MonoBehaviour
{
    private AudioSource audioSource;

    [Header("Müzik Ayarları")]
    [Tooltip("Müziğin çalacağı sahne isimleri")]
    public string[] allowedScenes = { "MainMenu" };

    [Header("Diğer AudioSource Kontrolü")] // ✨ YENİ
    [Tooltip("Diğer sahnelerdeki AudioSource'ları otomatik durdur")]
    public bool stopOtherAudioSources = true;

    void Awake()
    {
        if (FindObjectsOfType<MusicPlayer>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        audioSource = GetComponent<AudioSource>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckScene(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckScene(scene.name);
    }

    void CheckScene(string sceneName)
    {
        bool shouldPlay = System.Array.Exists(allowedScenes, s => s == sceneName);

        if (shouldPlay && !audioSource.isPlaying)
        {
            audioSource.Play();
            Debug.Log($"🎵 Ana menü müziği başlatıldı: {sceneName}");
        }
        else if (!shouldPlay)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
                audioSource.time = 0f;
                Debug.Log($"🔇 Ana menü müziği durduruldu: {sceneName}");
            }

            // ✨ YENİ: Diğer sahnelerdeki AudioSource'ları yönet
            if (stopOtherAudioSources)
            {
                HandleOtherAudioSources(sceneName);
            }
        }
    }

    // ✨ YENİ: Diğer AudioSource'ları bul ve yönet
    void HandleOtherAudioSources(string sceneName)
    {
        // Biraz bekle ki sahne tam yüklensin
        Invoke(nameof(CheckOtherAudioSources), 0.5f);
    }

    void CheckOtherAudioSources()
    {
        AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>();

        foreach (AudioSource source in allAudioSources)
        {
            // Kendi AudioSource'umuz değilse
            if (source != audioSource)
            {
                // Eğer "Play On Awake" açıksa ve müzik dosyası varsa
                if (source.playOnAwake && source.clip != null)
                {
                    Debug.Log($"🎵 Map sahnesinde AudioSource bulundu: {source.gameObject.name} - Clip: {source.clip.name}");

                    // Bu AudioSource'un çalmasına izin ver (Map müziği için)
                    // Sadece ana menü müziğini durdurduk, Map müziği çalabilir
                }
            }
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}