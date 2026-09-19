using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Sahne geçişlerinin tümüne yayılan kalıcı (DontDestroyOnLoad) loading screen singleton'ı.
    /// Eski tasarım MainMenu.unity'nin Canvas'ına bağlıydı (Canvas/LoadScreen — artık kullanılmıyor,
    /// kaldırılması ayrı bir işte yapılacak) ve bu yüzden yalnızca menü sahnesi hâlâ yüklüyken
    /// çalışıyordu. Bu sınıf Resources/UI/LoadingScreen.prefab'ı ilk çağrıda instantiate eder ve
    /// DontDestroyOnLoad yapar; böylece dört sahne geçişinin (menü→harita, harita→menü,
    /// menü→tutorial, tutorial→menü) hepsinde hayatta kalır. Bkz. plans/loading-screen-gecisleri.md.
    ///
    /// İki kullanım katmanı var:
    /// - Ham birincil metotlar (Show/Hide/SetProgress/SetText): SteamManager kendi durum makinesini
    ///   (dots animasyonu dahil) koruyor, sadece görünürlük/ilerleme/metin YAZMA noktalarını buraya
    ///   yönlendiriyor. statusText.text'e yazan TEK yer SetText (RenderDotText de dahil ona sarar) —
    ///   SteamManager.RenderLoadingText ile aynı "tek yazar" ilkesi burada da geçerli, başka hiçbir
    ///   yerden statusText.text'e doğrudan atama yapılmamalı.
    /// - Yüksek seviye LoadSceneRoutine: yeni çağıranlar (GameStateManager, ExitHelper, Menu) kendi
    ///   nokta animasyonlarını yazmasın diye dots döngüsünü kendi içinde yönetir.
    /// Nokta animasyonu WaitForSecondsRealtime kullanır (Time.deltaTime/WaitForSeconds DEĞİL) çünkü
    /// geçişler sırasında Time.timeScale 0 olabiliyor (bkz. GameStateManager oyun sonu akışı) —
    /// scaled zamanla dots donardı.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const string PrefabResourcePath = "UI/LoadingScreen";
        private const float DotIntervalSeconds = 0.5f;

        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider progressSlider;

        private static LoadingScreen _instance;

        // LoadSceneRoutine'in kendi nokta animasyonu state'i. Ham metotlar (Show/Hide/SetProgress/
        // SetText) bunlara dokunmaz — o akışta dots'u çağıran taraf (ör. SteamManager) yönetir.
        private string _baseMessage = "";
        private int _dotCount;
        private Coroutine _dotCoroutine;

        private void Awake()
        {
            // Sahneye elle sürüklenmiş ikinci bir örnek varsa (olmamalı, savunma amaçlı).
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // İlk örnek sahipliği BURADA üstlenmeli. EnsureInstance zaten aynı atamayı yapıyor,
            // ama prefab ileride elle bir sahneye konursa oraya hiç uğranmaz; _instance boş kalır
            // ve ilk Show()/LoadScene() çağrısı Resources'tan İKİNCİ bir kopya daha yaratırdı.
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Ham API (SteamManager delegasyonu için)

        public static void Show(string message = null)
        {
            EnsureInstance();
            if (_instance == null) return;

            _instance.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(message))
            {
                _instance.StartMessageWithDots(message);
            }
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.gameObject.SetActive(false);
        }

        /// <summary>
        /// Taban mesajı kurar, nokta sayısını sıfırlar ve kendi nokta animasyon döngüsünü
        /// (yeniden) başlatır. Show(string) ve LoadSceneRoutine ortak kullanır.
        /// </summary>
        private void StartMessageWithDots(string message)
        {
            StopDotsInternal();
            _baseMessage = message;
            _dotCount = 0;
            RenderDotText();
            _dotCoroutine = StartCoroutine(AnimateDotsCoroutine());
        }


        public static void SetProgress(float value)
        {
            EnsureInstance();
            if (_instance == null) return;
            if (_instance.progressSlider != null)
            {
                _instance.progressSlider.value = Mathf.Clamp01(value);
            }
        }

        /// <summary>
        /// statusText'e yazan TEK yer. Taban mesaj + nokta birleşimini kuran taraf (SteamManager'ın
        /// RenderLoadingText'i veya bu sınıfın kendi LoadSceneRoutine'i) burayı çağırır; başka hiçbir
        /// yer statusText.text'e doğrudan atama yapmamalı.
        /// </summary>
        public static void SetText(string fullText)
        {
            EnsureInstance();
            if (_instance == null) return;
            if (_instance.statusText != null)
            {
                _instance.statusText.text = fullText;
            }
        }

        #endregion

        #region Yüksek seviye API (GameStateManager / ExitHelper / Menu için)

        /// <summary>
        /// Show → en az 1 kare bekle (senkron algılanabilecek yüklemelerin ekranı hiç göstermeden
        /// sahneyi değiştirmesini önlemek için — bkz. plan "Engel 2") → LoadSceneAsync → bar →
        /// tamamlanınca Hide. Kendi nokta animasyonunu yönetir, çağıran taraf mesaj dışında
        /// hiçbir şeyle uğraşmaz.
        ///
        /// KRİTİK: Coroutine ÇAĞIRANIN üzerinde değil, SINGLETON'ın (DontDestroyOnLoad) üzerinde
        /// başlatılır. Çağıranların bir kısmı yüklenen sahneyle birlikte yok oluyor (ör.
        /// MENUUI/Lobby/Menu.cs — DontDestroyOnLoad DEĞİL); coroutine onların üzerinde dönseydi
        /// sahne değişiminde ölür, sondaki gizleme adımı hiç çalışmaz ve ekran kalıcı takılırdı.
        /// Bu yüzden metot void: çağıran çağırır ve kendi akışını bitirir, gerisi singleton'da.
        /// </summary>
        public static void LoadScene(string sceneName, string message)
        {
            EnsureInstance();
            if (_instance == null)
            {
                // Ekran kurulamadıysa yükleme yine de gerçekleşsin — geçiş engellenmemeli.
                SceneManager.LoadScene(sceneName);
                return;
            }

            // StartCoroutine deaktif GameObject üzerinde çalışmaz; önce aktifleştir.
            _instance.gameObject.SetActive(true);
            _instance.StartCoroutine(_instance.LoadSceneRoutineInternal(sceneName, message));
        }

        private IEnumerator LoadSceneRoutineInternal(string sceneName, string message)
        {
            gameObject.SetActive(true);
            if (progressSlider != null) progressSlider.value = 0f;
            StartMessageWithDots(message);

            // Engel 2: araya en az bir kare girsin ki yukarıdaki SetActive(true) render'a yansısın.
            yield return null;

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[LoadingScreen] LoadSceneAsync('{sceneName}') null döndü.");
                StopDotsInternal();
                gameObject.SetActive(false);
                yield break;
            }

            while (!op.isDone)
            {
                if (progressSlider != null)
                {
                    progressSlider.value = Mathf.Clamp01(op.progress / 0.9f);
                }
                yield return null;
            }

            if (progressSlider != null) progressSlider.value = 1f;
            StopDotsInternal();
            gameObject.SetActive(false);
        }

        #endregion

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[LoadingScreen] Resources/{PrefabResourcePath}.prefab bulunamadı — " +
                                "yükleme ekranı gösterilemeyecek.");
                return;
            }

            var go = Instantiate(prefab);
            go.name = "LoadingScreen";
            DontDestroyOnLoad(go);

            _instance = go.GetComponent<LoadingScreen>();
            if (_instance == null)
            {
                Debug.LogError("[LoadingScreen] Prefab kökünde LoadingScreen component'i bulunamadı.");
            }
        }

        private void StopDotsInternal()
        {
            if (_dotCoroutine != null)
            {
                StopCoroutine(_dotCoroutine);
                _dotCoroutine = null;
            }
        }

        private IEnumerator AnimateDotsCoroutine()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(DotIntervalSeconds);
                _dotCount = (_dotCount + 1) % 4;
                RenderDotText();
            }
        }

        private void RenderDotText()
        {
            SetText(_dotCount > 0 ? _baseMessage + new string('.', _dotCount) : _baseMessage);
        }
    }
}
