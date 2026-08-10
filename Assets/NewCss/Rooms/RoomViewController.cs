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

        /// <summary>Yerel oyuncunun histerezisli oda id'si. -1 (RoomResolver.NoRoomSentinel) = oda çözülemedi/yok.
        /// PlayerRoomVisibility (S4) bunu kendi oda id'siyle karşılaştırır.</summary>
        public static int LocalRoomId { get; private set; } = RoomResolver.NoRoomSentinel;

        private static readonly int MinId = Shader.PropertyToID("_CargoRoomMin");
        private static readonly int MaxId = Shader.PropertyToID("_CargoRoomMax");
        private static readonly int FadeId = Shader.PropertyToID("_CargoRoomFade");
        private static readonly int DesatId = Shader.PropertyToID("_CargoRoomDesat");
        private static readonly int DimId = Shader.PropertyToID("_CargoRoomDim");

        private Transform _localPlayer;
        private CameraFollow _cameraFollow;
        private float _lastSearchTime = -999f;

        private int _currentRoomId = RoomResolver.NoRoomSentinel;
        // İlk karede min/max'ın kesin gönderilmesini zorlamak için NoRoomSentinel(-1)'den de
        // farklı bir başlangıç değeri — "henüz hiç uygulanmadı" anlamına gelir.
        private int _lastAppliedRoomId = int.MinValue;
        private float _fade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // H6: startup hook'ta fade'i kesin sıfırla — önceki oturumdan (fast enter play mode /
            // domain reload kapalı) sızmış bayat bir global değer olabilir, ilk kare bile griye
            // düşmesin.
            Shader.SetGlobalFloat(FadeId, 0f);

            var go = new GameObject("[RoomViewController]");
            go.hideFlags = HideFlags.HideAndDontSave;
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

            if (roomChanged || !Mathf.Approximately(previousFade, _fade))
            {
                Shader.SetGlobalFloat(FadeId, _fade);
                Shader.SetGlobalFloat(DesatId, desaturation);
                Shader.SetGlobalFloat(DimId, dim);
            }
        }

        private static void ApplyRoomBounds(int roomId)
        {
            if (roomId != RoomResolver.NoRoomSentinel && RoomRegistry.TryGetBounds(roomId, out RoomBox box))
            {
                Shader.SetGlobalVector(MinId, new Vector4(box.MinX, box.MinY, box.MinZ, 0f));
                Shader.SetGlobalVector(MaxId, new Vector4(box.MaxX, box.MaxY, box.MaxZ, 0f));
            }
            else
            {
                // H8: oda yok / bulunamadı -> min=max=0. Fade zaten 0'a gidiyor olacağından
                // (want=false, haveRoom=false) bu kutunun içeriği görsel olarak etkisizdir; yine
                // de "her şey dışarıda" tanımsız-global-varsayılanıyla tutarlı kalsın diye sıfırlanır.
                Shader.SetGlobalVector(MinId, Vector4.zero);
                Shader.SetGlobalVector(MaxId, Vector4.zero);
            }
        }

        private void ResetFadeState()
        {
            _fade = 0f;
            Shader.SetGlobalFloat(FadeId, 0f);
        }
    }
}
