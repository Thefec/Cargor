using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // UI işlemleri için gerekli
using System.Collections;

public class IntroManager : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Sahne ve Geçiş")]
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private float autoSkipTime = 15f;
    [SerializeField] private CanvasGroup fadePanel; // Editörden Siyah Paneli buraya sürükle
    [SerializeField] private float fadeDuration = 1f; // Kararma süresi

    [Header("İlk Açılış")]
    [SerializeField] private bool onlyFirstTime = true;

    private const string INTRO_KEY = "IntroWatched";
    private bool isFinished = false;
    private float timer = 0f;

    void Start()
    {
        // Başlangıçta fade panelini görünmez yap (Eğer panel açık unuttuysan diye)
        if (fadePanel != null) fadePanel.alpha = 0;

        bool hasWatched = PlayerPrefs.GetInt(INTRO_KEY, 0) == 1;
        Debug.Log($"Intro daha önce izlendi mi? {hasWatched}");

        // Eğer daha önce izlendiyse ve onlyFirstTime aktifse
        if (onlyFirstTime && hasWatched)
        {
            Debug.Log("Intro atlanıyor, menüye gidiliyor...");
            // Direkt yükle (Zaten oyun başı olduğu için glitch göze batmaz, ama istenirse buraya da fade konabilir)
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        Debug.Log("Intro oynatılıyor...");
        Cursor.visible = false;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += (vp) => SkipIntro();
            videoPlayer.Play();
        }
        else
        {
            Debug.LogError("VideoPlayer bulunamadı!");
            LoadMenu();
        }
    }

    void Update()
    {
        if (isFinished) return;

        timer += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SkipIntro();
        }

        if (timer >= autoSkipTime)
        {
            SkipIntro();
        }
    }

    private void SkipIntro()
    {
        if (isFinished) return;
        isFinished = true; // Tekrar çalışmasını engelle

        Debug.Log("Intro geçişi başladı...");

        // Kayıt işlemi
        if (onlyFirstTime)
        {
            PlayerPrefs.SetInt(INTRO_KEY, 1);
            PlayerPrefs.Save();
        }

        // Direkt LoadMenu çağırmak yerine Coroutine başlatıyoruz
        StartCoroutine(FadeAndLoadMenu());
    }

    // YENİ EKLENEN KISIM: Kararma ve Sahne Yükleme
    IEnumerator FadeAndLoadMenu()
    {
        // 1. Siyah ekranı yavaşça görünür yap (Fade Out)
        if (fadePanel != null)
        {
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                fadePanel.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
                yield return null;
            }
            fadePanel.alpha = 1f; // Tam siyah olduğundan emin ol
        }

        // 2. Ekran simsiyah oldu, şimdi videoyu durdurabiliriz (Glitch görünmez)
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();

        // 3. Fareyi aç
        Cursor.visible = true;

        // 4. Yeni sahneyi yükle
        Debug.Log($"Menü yükleniyor: {menuSceneName}");
        SceneManager.LoadScene(menuSceneName);
    }

    // Eğer VideoPlayer yoksa veya hata olursa acil durum için
    private void LoadMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }

    [ContextMenu("Reset Intro")]
    public void ResetIntro()
    {
        PlayerPrefs.DeleteKey(INTRO_KEY);
        PlayerPrefs.Save();
        Debug.Log("✅ Intro resetlendi!");
    }
}