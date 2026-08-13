using System;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// UI panelini YUKARIDAN AŞAĞI kaydırarak açar, AŞAĞIDAN YUKARI kaydırarak kapatır.
    ///
    /// <see cref="UIPopInAnimation"/>'dan farkı: o OnEnable ile kendiliğinden tetiklenir ve
    /// yalnız GİRİŞ animasyonu yapabilir — çıkış animasyonu OnDisable'da imkânsızdır, çünkü
    /// obje o an zaten kapanmıştır. Panelin kapanışını da animasyonlu istiyorsak kapatan kodun
    /// beklemesi gerekir; bu yüzden burada tetikleme OnEnable'a değil, açık API'ye bağlıdır:
    /// <see cref="PlayIn"/> ve <see cref="PlayOut"/> (çağıran, geri çağırma ile SetActive(false)
    /// anını erteler).
    ///
    /// Animasyon coroutine ile DEĞİL Update ile yürütülür: coroutine'ler objenin kendisi
    /// kapandığında durur, bu da tam olarak çıkış animasyonunun bittiği andır.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UISlideAnimation : MonoBehaviour
    {
        /// <summary>Canvas bulunamazsa kullanılan kaydırma mesafesi (1080p referansı).</summary>
        private const float FallbackDistance = 1080f;

        [Header("Kaydırma Ayarları")]
        [Tooltip("Açılış (yukarıdan aşağı) süresi (saniye). 0 = animasyon yok, anında yerine oturur.")]
        [SerializeField, Min(0f)] private float inDuration = 0.35f;

        [Tooltip("Kapanış (aşağıdan yukarı) süresi (saniye). 0 = animasyon yok, anında kapanır.")]
        [SerializeField, Min(0f)] private float outDuration = 0.25f;

        [Tooltip("Panelin ne kadar yukarıdan geleceği (piksel). 0 = OTOMATİK: canvas yüksekliği " +
                 "kadar, yani panel her çözünürlükte ekranın tam üstünden girer.")]
        [SerializeField, Min(0f)] private float slideDistance = 0f;

        [Tooltip("Açılışta hedefi aşma miktarı. 0 = taşma yok (ease-out cubic), 1.7 ≈ belirgin yaylanma.")]
        [SerializeField, Range(0f, 4f)] private float overshoot = 0f;

        [Tooltip("Time.timeScale = 0 iken de oynasın (duraklatma/menü).")]
        [SerializeField] private bool useUnscaledTime = true;

        private RectTransform _rect;
        private Vector2 _restPosition;
        private bool _restCached;

        private bool _playing;
        private bool _closing;
        private float _elapsed;
        private float _duration;
        private Vector2 _from;
        private Vector2 _to;
        private Action _onComplete;

        /// <summary>Şu an bir giriş/çıkış animasyonu oynuyor mu?</summary>
        public bool IsPlaying => _playing;

        private void Awake()
        {
            CacheRest();
        }

        /// <summary>
        /// Panelin GERÇEK (yerine oturmuş) konumunu bir kez saklar. Tek seferlik olması şart:
        /// animasyon ortasında yeniden önbelleklenirse ekran dışındaki ara konum "gerçek konum"
        /// sanılır ve panel bir daha asla yerine oturmaz.
        ///
        /// Awake'e ek olarak PlayIn/PlayOut içinden de çağrılır: QuestUIController panelini
        /// Awake sırasında kapatıyor, bu da bu bileşenin Awake'inin ertelenmesine yol açabilir.
        /// </summary>
        private void CacheRest()
        {
            if (_restCached)
            {
                return;
            }

            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }

            _restPosition = _rect.anchoredPosition;
            _restCached = true;
        }

        /// <summary>Paneli ekranın üstünden aşağı, yerine kaydırır. Obje AKTİF olmalıdır.</summary>
        public void PlayIn()
        {
            CacheRest();

            if (inDuration <= 0f)
            {
                Stop();
                _rect.anchoredPosition = _restPosition;
                return;
            }

            // Kapanış animasyonu sürerken açılırsa panel yukarıya sıçramasın: bulunduğu yerden
            // geri iner. Begin ayrıca bekleyen kapanış geri çağırmasını düşürür — yani PlayIn
            // devam eden bir PlayOut'u temiz biçimde iptal eder.
            bool interruptingClose = _playing && _closing;

            _from = interruptingClose
                ? _rect.anchoredPosition
                : _restPosition + new Vector2(0f, ResolveDistance());
            _to = _restPosition;
            _rect.anchoredPosition = _from;

            Begin(inDuration, false, null);
        }

        /// <summary>
        /// Paneli bulunduğu yerden yukarı, ekran dışına kaydırır ve bitince
        /// <paramref name="onComplete"/> çağırır — çağıran SetActive(false)'u orada yapmalıdır.
        /// Süre 0 ise geri çağırma AYNI karede tetiklenir (davranış hep aynı, sadece anlık).
        /// </summary>
        public void PlayOut(Action onComplete)
        {
            CacheRest();

            if (outDuration <= 0f)
            {
                Stop();
                _rect.anchoredPosition = _restPosition;
                onComplete?.Invoke();
                return;
            }

            // Giriş animasyonu yarıda kesilmiş olabilir; nereden başlayacağımız o anki konumdur.
            _from = _rect.anchoredPosition;
            _to = _restPosition + new Vector2(0f, ResolveDistance());

            Begin(outDuration, true, onComplete);
        }

        private void Begin(float duration, bool closing, Action onComplete)
        {
            _duration = duration;
            _closing = closing;
            _onComplete = onComplete;
            _elapsed = 0f;
            _playing = true;
        }

        private void Stop()
        {
            _playing = false;
            _onComplete = null;
        }

        private void OnDisable()
        {
            // Panel animasyon ortasında kapatılırsa ekran dışı konumda donup kalmasın — bir
            // sonraki açılış konumu PlayIn zaten baştan kuruyor, ama önbelleklenmiş "gerçek
            // konum"a dönmek bu bileşeni kullanmayan kodlara karşı da güvenli.
            if (_restCached && _rect != null)
            {
                _rect.anchoredPosition = _restPosition;
            }

            // Bekleyen geri çağırma varsa MUTLAKA tetiklenir: çağıran (QuestUIController) onu
            // "animasyon bitti" sinyali olarak kullanıp _isAnimating'i indiriyor. Yutulsaydı
            // panel sonsuza dek "animasyon sürüyor" durumunda kalır, bir daha açılmazdı.
            Action pending = _onComplete;
            Stop();
            pending?.Invoke();
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

            if (t >= 1f)
            {
                _rect.anchoredPosition = _to;

                // Geri çağırma SetActive(false) yapabilir (ve o da OnDisable'ı tetikler) —
                // bu yüzden önce durumu temizle, sonra çağır: aynı geri çağırma iki kez
                // tetiklenmesin.
                Action done = _onComplete;
                Stop();
                done?.Invoke();
                return;
            }

            float eased = _closing ? EaseInCubic(t) : EaseOutBack(t);
            _rect.anchoredPosition = Vector2.LerpUnclamped(_from, _to, eased);
        }

        /// <summary>0 = otomatik: kök canvas'ın yüksekliği kadar (her çözünürlükte ekran dışı).</summary>
        private float ResolveDistance()
        {
            if (slideDistance > 0f)
            {
                return slideDistance;
            }

            var canvas = GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            float height = canvasRect != null ? canvasRect.rect.height : 0f;

            return height > 0f ? height : FallbackDistance;
        }

        /// <summary>Girişte: hızlı başlar, yavaşlayarak yerine oturur (overshoot &gt; 0 ise hafif yaylanır).</summary>
        private float EaseOutBack(float t)
        {
            float c1 = overshoot;
            float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Çıkışta: yavaş kopar, hızlanarak ekrandan çıkar — "gidiyor" hissi verir.</summary>
        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }
    }
}
