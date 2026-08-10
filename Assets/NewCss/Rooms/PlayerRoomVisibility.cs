using NewCss.Rooms.Core;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Character.prefab kökünde yaşar. Bu karakterin (kendisi ya da uzak bir oyuncu farketmez)
    /// bulunduğu odayı kendi konumundan bağımsızca çözer ve <see cref="RoomViewController.LocalRoomId"/>
    /// (İZLEYEN istemcinin odası) ile karşılaştırır; farklıysa bu istemcide sadece bu karakteri gizler.
    ///
    /// Merkezi bir görünürlük yöneticisi YOK (plan §3.2) — her karakter prefabi kendi görünürlüğünü
    /// kendi hesaplar. Renderer.enabled tamamen istemci-yerel bir özelliktir (ağa senkron olmaz),
    /// bu yüzden her istemci aynı karakter objesi için farklı bir sonuca varabilir — bu TAM olarak
    /// istenen davranış: "sen görmüyorsun ama oyun sunucuda/diğerlerinde normal devam ediyor."
    ///
    /// Sabit renderer listesi TUTULMAZ: her Apply() çağrısında GetComponentsInChildren&lt;Renderer&gt;(true)
    /// taze çekilir (kapsam: 15 SkinnedMeshRenderer + 2 ParticleSystemRenderer "Dust"/"Dust (1)" +
    /// oyuncunun elindeki kutu — HoldPosition bu objenin child'ı olduğu için otomatik dahil olur).
    /// Canvas (stamina bar) hariçtir — Image/Graphic UnityEngine.Renderer'dan TÜREMEZ, GetComponentsInChildren
    /// &lt;Renderer&gt; zaten ona hiç dokunmaz; ayrıca bkz. H11/S6 (Canvas kendi IsOwner gate'iyle çözülüyor).
    ///
    /// Uygulama KENAR-TETİKLEMELİDİR (oda değişince), her karede DEĞİL — GetComponentsInChildren
    /// allocation yapar ve SetActive/Renderer.enabled UI/batching rebuild'i tetikleyebilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRoomVisibility : MonoBehaviour
    {
        // Bu karakterin kendi (histerezisli) oda id'si — izleyenin odasından BAĞIMSIZ, yalnız bu
        // objenin transform'undan hesaplanır.
        private int _ownRoomId = RoomResolver.NoRoomSentinel;
        private bool _visible = true;
        private bool _initialized;

        private void Update()
        {
            if (RoomRegistry.Count > 0)
            {
                Vector3 pos = transform.position;
                _ownRoomId = RoomRegistry.Resolve(_ownRoomId, pos.x, pos.y, pos.z);
            }
            else
            {
                // H8: sahnede oda sistemi hiç kurulmamış (Tutorial) -> herkes görünür.
                _ownRoomId = RoomResolver.NoRoomSentinel;
            }

            bool shouldBeVisible = ComputeVisible();
            if (_initialized && shouldBeVisible == _visible)
            {
                return;
            }

            _initialized = true;
            _visible = shouldBeVisible;
            Apply(_visible);
        }

        /// <summary>H8 varsayılanı: izleyenin ya da bu karakterin odası çözülememişse (-1) görünür kal.
        /// Yalnızca ikisi de çözülmüş VE farklıysa gizlenir.</summary>
        private bool ComputeVisible()
        {
            // K4: izleyenin KENDİ karakteri asla gizlenmez. Yerel oyuncunun odası iki ayrı
            // histerezis durum makinesiyle çözülüyor (RoomViewController._currentRoomId ve
            // buradaki _ownRoomId); ikisi ilk çözümlemelerini farklı karede/konumda yaparsa
            // çakışma bölgesinde ayrışabilir ve histerezis ayrışmayı KİLİTLER — oyuncu kendi
            // karakterini kaybeder. Plan §6.1 zaten "yerel oyuncu daima kendi odasında" diyor.
            if (RoomViewController.LocalPlayer == transform)
            {
                return true;
            }

            int viewerRoom = RoomViewController.LocalRoomId;

            if (viewerRoom == RoomResolver.NoRoomSentinel || _ownRoomId == RoomResolver.NoRoomSentinel)
            {
                return true;
            }

            return _ownRoomId == viewerRoom;
        }

        private void Apply(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        /// <summary>
        /// Renderer listesini mevcut görünürlük durumuna göre YENİDEN uygular. §H2 deliği:
        /// PlayerInventory.Visual.cs SpawnHeldItemVisual() her çağrıldığında YENİ bir GameObject +
        /// YENİ bir renderer üretir (holdPosition child'ı); bu sistem kenar-tetiklemeli olduğundan
        /// yeni renderer, bir sonraki oda değişimine kadar mevcut gizli/görünür durumdan habersiz
        /// (varsayılan enabled=true) kalır -> gizli bir oyuncunun elindeki kutu boş odada havada
        /// asılı görünür. Bu yüzden Refresh() spawn'dan hemen sonra çağrılır.
        /// </summary>
        public void Refresh()
        {
            Apply(_visible);
        }
    }
}
