using NewCss.Voice.Core;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Elle sahneye/prefaba yerleştirilen tek-sprite'lık push-talk göstergesi — RadioHudController'ın
/// aksine bu component KONUM/İKON KARARI vermez, sadece atanan Image'ın rengini telsiz durumuna
/// göre değiştirir. Sahnede/prefabda (örn. karakterin HUD'unda) bir Image objesi oluşturup kendi
/// ikonunu <see cref="icon"/> alanına sürükle — component konuşurken renkli, aksi halde gri
/// gösterir.
///
/// GRİLEŞTİRME NEDEN ÖNCEDEN ÜRETİLMİŞ İKİNCİ BİR SPRITE DEĞİL, SHADER: kullanıcı kendi ikonunu
/// elle atayacak — hangi texture import ayarlarıyla (Read/Write açık mı vb.) geldiğini kontrol
/// edemeyiz, runtime pixel-kopyalama bu yüzden kırılgan. Bunun yerine <see cref="SaturationShaderName"/>
/// materyali atanan sprite'ı OLDUĞU GİBİ kullanır, sadece _Saturation'ı 0/1 arası değiştirir —
/// hangi sprite atanırsa atansın çalışır.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class PushTalkIconIndicator : MonoBehaviour
{
    private const string SaturationShaderName = "Cargor/UI/Saturation";
    private static readonly int SaturationPropertyId = Shader.PropertyToID("_Saturation");

    [Header("Kendi ikonunu buraya sürükle (boş bırakılırsa aynı objedeki Image kullanılır)")]
    [SerializeField] private Image icon;

    [Header("Nabız animasyonu (konuşurken)")]
    [SerializeField] private float pulseFrequency = 5f; // Sin çarpanı — göze agresif gelmeyecek hız
    [SerializeField] private float pulseAmplitude = 0.12f; // base scale'in ±%12'si
    [SerializeField] private float scaleReturnSpeed = 8f; // konuşma bitince base'e dönüş Lerp hızı
    [SerializeField] private float saturationLerpSpeed = 10f; // _Saturation 0↔1 geçiş hızı

    private Material _material;
    private RectTransform _iconRect;
    private Vector3 _baseScale;
    private float _currentSaturation;
    private bool? _lastTransmitting;

    private void Awake()
    {
        if (icon == null) icon = GetComponent<Image>();
        _iconRect = icon.rectTransform;
        _baseScale = _iconRect.localScale;

        var shader = Shader.Find(SaturationShaderName);
        if (shader == null)
        {
            // Shader eksikse ikon sessizce her zaman renkli kalır — HUD'un geri kalanı bundan
            // etkilenmesin (RadioHudController'daki "null-guard'lı, birbirinden bağımsız alt sistem"
            // deseniyle aynı).
            Debug.LogWarning($"[PushTalkIconIndicator] '{SaturationShaderName}' shader'ı bulunamadı — ikon hep renkli kalacak.");
            return;
        }

        _material = new Material(shader);
        icon.material = _material;
        ApplyTransmitting(false);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    private void Update()
    {
        var runtime = RadioVoiceRuntime.Instance;
        bool transmitting = SteamClient.IsValid && runtime != null &&
            (runtime.Capture.State == RadioVoiceCapture.CaptureState.Transmitting ||
             runtime.Capture.State == RadioVoiceCapture.CaptureState.Tail);

        ApplyTransmitting(transmitting);

        if (transmitting)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency) * pulseAmplitude;
            _iconRect.localScale = _baseScale * pulse;
        }
        else if (_iconRect.localScale != _baseScale)
        {
            _iconRect.localScale = Vector3.Lerp(_iconRect.localScale, _baseScale, Time.deltaTime * scaleReturnSpeed);
        }

        if (_material != null)
        {
            float target = transmitting ? 1f : 0f;
            if (!Mathf.Approximately(_currentSaturation, target))
            {
                _currentSaturation = Mathf.MoveTowards(_currentSaturation, target, Time.deltaTime * saturationLerpSpeed);
                _material.SetFloat(SaturationPropertyId, _currentSaturation);
            }
        }
    }

    private void ApplyTransmitting(bool transmitting)
    {
        _lastTransmitting = transmitting; // durum takibi (gelecekte olay-bazlı tetikleyiciler için)
    }
}
