using NewCss.Rooms.Core;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Oda görünürlük/karartma sisteminin bootstrap + orkestrasyon noktası (plan §5.1).
    ///
    /// Her karede yerel oyuncunun konumunu RoomRegistry üzerinden çözer, sonucu
    /// <see cref="LocalRoomId"/> ile yayınlar (S4/PlayerRoomVisibility bunu okur) ve harita
    /// görünümü (X, CameraFollow.IsMapViewActive) açıkken karartma için global shader
    /// değişkenlerini besler.
    ///
    /// Bootstrap deseni WorldSpaceCanvasCameraBinder.cs:26-32 ile BİREBİR aynı: sahne authoring'i
    /// GEREKTİRMEZ. Oda hiç kurulmamış sahnelerde (Tutorial, RoomRegistry.Count == 0) H8
    /// varsayılanı devreye girer: herkes görünür, karartma kapalı — script boş sahnede güvenle
    /// çalışır.
    /// </summary>
    public class RoomViewController : MonoBehaviour
    {
        // CameraFollow.cs:17 TARGET_SEARCH_INTERVAL ile aynı desen — her karede FindFirstObjectByType
        // çağırmamak için referanslar bulununca cache'lenir, yalnız kayıpsa (null) periyodik yeniden aranır.
        private const float TargetSearchInterval = 0.5f;

        [Header("=== KARARTMA AYARLARI ===")]
        [SerializeField]
        [Tooltip("Fade geçiş hızı (birim/sn). Kamera harita-görünümü lerp'iyle (CameraFollow.mapViewTransitionSpeed, " +
                 "varsayılan 5) UYUMLU tutulmalı — aksi halde kamera oturmadan dünya griye döner ya da tersi. " +
                 "DOĞRULAMA: EscapeMenuManager.cs:404 Time.timeScale=0 yapıyor, ANCAK CameraFollow.cs:264-275 " +
                 "kendi map-view lerp'inde ÖLÇEKLİ Time.deltaTime kullanıyor (InputBindingManager.GetAction ise " +
                 "ham Input.GetKey okuduğu için IsMapViewActive duraklatmada da değişebilir). Burada da ölçekli " +
                 "Time.deltaTime kullanılır ki duraklatmada kamera ile karartma AYNI ANDA dursun; unscaledDeltaTime " +
                 "kullansaydık kamera donarken karartma bağımsız ilerler/tersine döner ve ikisi senkronsuz görünürdü.")]
        [Range(0.1f, 20f)]
        private float fadeSpeed = 5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Karartılmış alanda desatürasyon miktarı (0 = renkli kalır, 1 = tam gri) — shader'a iletilir.")]
        private float desaturation = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Karartılmış alanda karanlık çarpanı (1 = değişmez, 0 = simsiyah) — shader'a iletilir.")]
        private float dim = 0.35f;

        [SerializeField]
        [Tooltip("Oda değişince eski oda kutusundan yeni oda kutusuna ÇAPRAZ GEÇİŞ süresi (saniye) — " +
                 "kullanıcı şikâyeti: harita görünümü basılıyken oda değişince griden renge ANİ sıçrama " +
                 "yerine ~1-2 saniyelik yumuşak aydınlanma istendi. 'Hız' değil 'süre': RoomBlend.Advance " +
                 "bu saniyede 0'dan 1'e lineer ilerler, shader'a giden değer ayrıca smoothstep'lenir.")]
        [Range(0.05f, 5f)]
        private float roomBlendDuration = 1.2f;

        /// <summary>Yerel oyuncunun histerezisli oda id'si. -1 (RoomResolver.NoRoomSentinel) = oda çözülemedi/yok.
        /// PlayerRoomVisibility (S4) bunu kendi oda id'siyle karşılaştırır.</summary>
        public static int LocalRoomId { get; private set; } = RoomResolver.NoRoomSentinel;

        /// <summary>
        /// İzleyen istemcinin kendi karakteri. PlayerRoomVisibility (S4) bunu "ben yerel oyuncu
        /// muyum" testi için okur — K4: aksi halde yerel oyuncunun odası İKİ ayrı histerezis durum
        /// makinesiyle (buradaki _currentRoomId ve o objenin kendi _ownRoomId'si) çözülür; ikisi
        /// ilk çözümlemelerini farklı karede/konumda yaparsa çakışma bölgesinde ayrışabilir ve
        /// histerezis ayrışmayı KİLİTLER → oyuncu kendi karakterini göremez. Plan §6.1 zaten
        /// "yerel oyuncu daima kendi odasındadır" varsayıyor; bu alan o varsayımı koda yazar.
        /// </summary>
        public static Transform LocalPlayer { get; private set; }

        private static readonly int MinId = Shader.PropertyToID("_CargoRoomMin");
        private static readonly int MaxId = Shader.PropertyToID("_CargoRoomMax");
        private static readonly int FadeId = Shader.PropertyToID("_CargoRoomFade");
        private static readonly int DesatId = Shader.PropertyToID("_CargoRoomDesat");
        private static readonly int DimId = Shader.PropertyToID("_CargoRoomDim");
        private static readonly int PrevMinId = Shader.PropertyToID("_CargoRoomPrevMin");
        private static readonly int PrevMaxId = Shader.PropertyToID("_CargoRoomPrevMax");
        private static readonly int BlendId = Shader.PropertyToID("_CargoRoomBlend");

        private Transform _localPlayer;
        private CameraFollow _cameraFollow;
        private float _lastSearchTime = -999f;

        private int _currentRoomId = RoomResolver.NoRoomSentinel;
        // İlk karede min/max'ın kesin gönderilmesini zorlamak için NoRoomSentinel(-1)'den de
        // farklı bir başlangıç değeri — "henüz hiç uygulanmadı" anlamına gelir.
        private int _lastAppliedRoomId = int.MinValue;
        private float _fade;
        // NaN ile başlar ki ilk karşılaştırma kesin "değişti" desin ve globaller bir kez yazılsın.
        private float _lastDesat = float.NaN;
        private float _lastDim = float.NaN;

        // İki kutulu çapraz geçiş durumu (kullanıcı şikâyeti: oda değişince ani sıçrama).
        // _newMin/_newMax "hedef" (blend=1) kutuyu, _prevMin/_prevMax "eski" (blend=0) kutuyu
        // tutar. İkisi de field-default Vector4(0,0,0,0) ile başlar — ilk oda çözümlemesinde
        // bu "sıfır kutu" doğal olarak prev'e kayar (madde 4: "oda yoktan gelme" özel durum
        // GEREKMEZ, genel geçiş mantığı zaten yumuşak aydınlanma üretir).
        private Vector4 _newMin;
        private Vector4 _newMax;
        private Vector4 _prevMin;
        private Vector4 _prevMax;
        // Ham lineer ilerleme (0..1) — RoomBlend.Advance ile roomBlendDuration saniyede 1'e ulaşır.
        // 0f ile başlar: ilk ApplyRoomBounds çağrısı zaten sıfırlayacak, ama o çağrıdan önce
        // (henüz oda yokken) fade=0 olduğundan roomK=0 ve değeri önemsizdir — 0f en identity-
        // uyumlu varsayılan.
        private float _blendProgress;
        // NaN: ilk karede kesin push (aynı desat/dim deseni).
        private float _lastAppliedBlend = float.NaN;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // H6: startup hook'ta fade'i kesin sıfırla — önceki oturumdan (fast enter play mode /
            // domain reload kapalı) sızmış bayat bir global değer olabilir, ilk kare bile griye
            // düşmesin. Blend de aynı sızıntıya açık (statik shader global) — 0 = "tamamen eski
            // kutu" ama fade zaten 0 olduğundan görsel etkisi yok, yine de tutarlılık için sıfırlanır.
            Shader.SetGlobalFloat(FadeId, 0f);
            Shader.SetGlobalFloat(BlendId, 0f);
            // Domain reload kapalıyken statikler önceki oturumdan sağ çıkar — yok olmuş bir
            // transform'a takılı kalmasın.
            LocalPlayer = null;
            LocalRoomId = RoomResolver.NoRoomSentinel;

            var go = new GameObject("[RoomViewController]");
            // HideAndDontSave DEĞİL: o bayrak objeyi hem Hierarchy'de gizler hem NotEditable
            // yapar, kullanıcı Inspector'dan dim/desat/roomBlendDuration'ı elde ayarlayamazdı
            // (plan §9.3 tam olarak bunu istiyor). DontSaveInEditor|DontSaveInBuild aynı "sahneye
            // kaydedilmez" garantisini verir ama Play sırasında Hierarchy'de görünür + düzenlenebilir
            // bırakır.
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            DontDestroyOnLoad(go);
            go.AddComponent<RoomViewController>();
        }

        private void OnEnable()
        {
            ResetFadeState();
        }

        private void OnDisable()
        {
            // H6: harita görünümü açıkken bu obje devre dışı kalırsa (sahne değişimi vb.) dünya
            // kalıcı gri kalmasın — sıfırlayacak başka kimse yok.
            ResetFadeState();
        }

        private void OnDestroy()
        {
            ResetFadeState();
            LocalPlayer = null;
            LocalRoomId = RoomResolver.NoRoomSentinel;
        }

        private void Update()
        {
            RefreshReferencesIfNeeded();
            ResolveLocalRoom();
            UpdateFade();
        }

        private void RefreshReferencesIfNeeded()
        {
            if (_localPlayer != null && _cameraFollow != null)
            {
                return;
            }

            if (Time.unscaledTime - _lastSearchTime < TargetSearchInterval)
            {
                return;
            }

            _lastSearchTime = Time.unscaledTime;

            if (_cameraFollow == null)
            {
                _cameraFollow = FindFirstObjectByType<CameraFollow>();
            }

            if (_localPlayer == null)
            {
                _localPlayer = FindLocalPlayerTransform();
            }

            LocalPlayer = _localPlayer;
        }

        /// <summary>PlayerMovement.cs:271 SetupCamera / CameraFollow.cs TryFindByNetworkManager+TryFindByTag
        /// ile aynı yöntemler: NetworkManager üzerinden yerel oyuncu, olmazsa Player tag'i + IsOwner doğrulaması.</summary>
        private static Transform FindLocalPlayerTransform()
        {
            var nm = NetworkManager.Singleton;
            var playerObject = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (playerObject != null)
            {
                return playerObject.transform;
            }

            var tagged = GameObject.FindWithTag("Player");
            if (tagged == null)
            {
                return null;
            }

            var netObj = tagged.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsOwner)
            {
                return null;
            }

            return tagged.transform;
        }

        private void ResolveLocalRoom()
        {
            if (_localPlayer == null || RoomRegistry.Count == 0)
            {
                // H8: oyuncu henüz bulunamadı / sahnede hiç RoomVolume yok (Tutorial) -> oda yok,
                // herkes görünür kalsın.
                _currentRoomId = RoomResolver.NoRoomSentinel;
                LocalRoomId = RoomResolver.NoRoomSentinel;
                return;
            }

            Vector3 pos = _localPlayer.position;
            _currentRoomId = RoomRegistry.Resolve(_currentRoomId, pos.x, pos.y, pos.z);
            LocalRoomId = _currentRoomId;
        }

        private void UpdateFade()
        {
            bool haveRoom = _currentRoomId != RoomResolver.NoRoomSentinel;
            bool want = haveRoom && _cameraFollow != null && _cameraFollow.IsMapViewActive;

            float previousFade = _fade;
            // Ölçekli Time.deltaTime — gerekçe için fadeSpeed tooltip'ine bak.
            _fade = RoomFade.Step(_fade, want, Time.deltaTime, fadeSpeed);

            bool roomChanged = _currentRoomId != _lastAppliedRoomId;
            if (roomChanged)
            {
                ApplyRoomBounds(_currentRoomId);
                _lastAppliedRoomId = _currentRoomId;
            }

            // K5: desat/dim'i de değişim testine dahil et. Sadece fade/oda değişimine baksaydık,
            // X basılı tutulup fade 1'e oturduğunda Inspector'dan dim/desat çevirmek HİÇBİR ŞEY
            // yapmazdı — oysa plan §9.3 kullanıcıdan bu iki değeri tam olarak öyle, oyun
            // çalışırken elde ayarlamasını istiyor.
            bool tuningChanged = !Mathf.Approximately(_lastDesat, desaturation)
                                 || !Mathf.Approximately(_lastDim, dim);

            if (roomChanged || tuningChanged || !Mathf.Approximately(previousFade, _fade))
            {
                Shader.SetGlobalFloat(FadeId, _fade);
                Shader.SetGlobalFloat(DesatId, desaturation);
                Shader.SetGlobalFloat(DimId, dim);
                _lastDesat = desaturation;
                _lastDim = dim;
            }

            // Oda değişiminden bağımsız — çapraz geçiş her karede ilerler (roomChanged'in
            // kendisiyle sınırlı değil, ApplyRoomBounds içinde sıfırlanan _blendProgress'i
            // buradan sonra da ilerletmemiz gerekir ki geçiş süre boyunca devam etsin).
            UpdateBlend();
        }

        /// <summary>
        /// Eski "hedef" kutuyu "önceki" kutuya kaydırır, yeni hedefi uygular, çapraz geçişi
        /// (blend) sıfırlar. Instance metodu (eskiden static'ti) — artık _prevMin/_newMin gibi
        /// örnek durumuna yazıyor.
        ///
        /// SINIRLAMA (kullanıcı isteği madde 5): geçiş ORTASINDA oda yeniden değişirse, "prev"
        /// o anki (belki hâlâ 0..1 arası karışmakta olan) hedef kutu olur — ara karışım durumu
        /// tek bir kutuyla temsil edilemediği için görsel küçük bir sıçrama olabilir. Bilerek
        /// kabul edildi: histerezis (RoomResolver, 0.35 m margin + "mevcut oda korunur") hızlı
        /// art arda oda değişimini zaten nadir kılıyor, bu köşe durumu playtest'te göze
        /// çarpmayacak kadar seyrek olmalı.
        /// </summary>
        private void ApplyRoomBounds(int roomId)
        {
            _prevMin = _newMin;
            _prevMax = _newMax;

            if (roomId != RoomResolver.NoRoomSentinel && RoomRegistry.TryGetBounds(roomId, out RoomBox box))
            {
                _newMin = new Vector4(box.MinX, box.MinY, box.MinZ, 0f);
                _newMax = new Vector4(box.MaxX, box.MaxY, box.MaxZ, 0f);
            }
            else
            {
                // H8: oda yok / bulunamadı -> min=max=0. Fade zaten 0'a gidiyor olacağından
                // (want=false, haveRoom=false) bu kutunun içeriği görsel olarak etkisizdir; yine
                // de "her şey dışarıda" tanımsız-global-varsayılanıyla tutarlı kalsın diye sıfırlanır.
                _newMin = Vector4.zero;
                _newMax = Vector4.zero;
            }

            Shader.SetGlobalVector(MinId, _newMin);
            Shader.SetGlobalVector(MaxId, _newMax);
            Shader.SetGlobalVector(PrevMinId, _prevMin);
            Shader.SetGlobalVector(PrevMaxId, _prevMax);

            // Madde 1+4: her oda değişiminde (ilk çözümleme dahil, çünkü _newMin/_newMax ilk
            // seferde sıfır kutudan geliyordu) çapraz geçiş baştan başlar.
            _blendProgress = 0f;
        }

        /// <summary>
        /// Çapraz geçişi (madde 1-3) roomBlendDuration saniyede lineer ilerletir, shader'a
        /// smoothstep'lenmiş hâlini gönderir. Oda değişip değişmediğinden bağımsız her karede
        /// çağrılır — ApplyRoomBounds yalnız _blendProgress'i sıfırlar, ilerletme burada olur.
        /// </summary>
        private void UpdateBlend()
        {
            _blendProgress = RoomBlend.Advance(_blendProgress, Time.deltaTime, roomBlendDuration);
            float smoothed = RoomBlend.Smoothstep01(_blendProgress);

            if (!Mathf.Approximately(_lastAppliedBlend, smoothed))
            {
                Shader.SetGlobalFloat(BlendId, smoothed);
                _lastAppliedBlend = smoothed;
            }
        }

        private void ResetFadeState()
        {
            _fade = 0f;
            Shader.SetGlobalFloat(FadeId, 0f);
        }
    }
}
