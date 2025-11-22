using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    [Header("Loading Ekraný")]
    public GameObject loadScreen;

    [Header("Ayarlar")]
    [Range(0f, 1f)]
    public float minimumLoadTime = 1f; // Minimum yükleme süresi
    public bool smoothTransition = true; // Yumuþak geçiþ

    public void LoadScene(int levelIndex)
    {
        StartCoroutine(LoadSceneAsynchronously(levelIndex));
    }

    private IEnumerator LoadSceneAsynchronously(int levelIndex)
    {
        // Loading ekranýný aç
        if (loadScreen != null)
            loadScreen.SetActive(true);

        // Biraz bekle - kullanýcýya loading ekranýný göster
        yield return new WaitForSeconds(0.5f);

        AsyncOperation operation = SceneManager.LoadSceneAsync(levelIndex);

        // ÖNEMLÝ: Sahnenin otomatik aktive olmasýný engelle
        operation.allowSceneActivation = false;

        float timer = 0f;

        // Sahne %90 yüklenene kadar bekle
        while (operation.progress < 0.9f)
        {
            timer += Time.deltaTime;

            // Ýstersen progress gösterebilirsin
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            // Debug.Log($"Loading: {progress * 100}%");

            yield return null;
        }

        // Minimum yükleme süresini bekle (çok hýzlý yüklenirse loading ekraný yanýp söner)
        while (timer < minimumLoadTime)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // Yumuþak geçiþ için ekstra bekleme
        if (smoothTransition)
        {
            yield return new WaitForSeconds(0.3f);
        }

        // ÖNEMLÝ: Birkaç frame bekle - bu kasmayi önler
        yield return null;
        yield return null;

        // Þimdi sahneyi aktive et
        operation.allowSceneActivation = true;

        // Sahne tam olarak yüklenene kadar bekle
        while (!operation.isDone)
        {
            yield return null;
        }

        // Ek güvenlik: Sahne yüklendikten sonra 1 frame daha bekle
        yield return null;

        // Loading ekranýný kapat
        if (loadScreen != null)
            loadScreen.SetActive(false);
    }
}