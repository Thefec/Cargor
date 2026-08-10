using NewCss.Rooms.Core;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Eşya (kutu/ürün) prefab'larının kökünde yaşar — <see cref="PlayerRoomVisibility"/>'nin
    /// eşya tarafındaki birebir örneği, tek fark görünürlük kuralı: "stok kontrolü" istisnası.
    ///
    /// Kural tablosu (plan: oda görünürlük genişletme görevi):
    ///   - Aynı oda                              -> görünür
    ///   - Başka oda, harita görünümü (X) KAPALI  -> gizli
    ///   - Başka oda, harita görünümü (X) AÇIK    -> görünür ("stok kontrolü")
    ///   - Oda sistemi yok / çözülemedi (H8)       -> görünür
    ///
    /// PlayerRoomVisibility'nin görünürlük kuralı bundan ETKİLENMEZ — oyuncular X'te de gizli
    /// kalmaya devam eder; bu bileşen yalnız <see cref="RoomViewController.MapViewActive"/>'i okur,
    /// oyuncu tarafına dokunmaz.
    ///
    /// Sabit renderer listesi TUTULMAZ (PlayerRoomVisibility ile aynı gerekçe): her Apply()
    /// çağrısında GetComponentsInChildren&lt;Renderer&gt;(true) taze çekilir.
    ///
    /// Uygulama KENAR-TETİKLEMELİDİR, her karede DEĞİL — GetComponentsInChildren allocation yapar.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomItemVisibility : MonoBehaviour
    {
        private int _ownRoomId = RoomResolver.NoRoomSentinel;
        private bool _visible = true;
        private bool _initialized;

        /// <summary>
        /// TUZAK 1 (pickup/drop): PlayerInventory.cs alındığında bu objeyi SetActive(false) yapar
        /// (Update tamamen durur), yere bırakılınca yeniden SetActive(true) olur. Kenar-tetiklemeli
        /// mantık "shouldBeVisible == _visible ise dokunma" dediğinden, yeniden aktifleşince ilk
        /// karede tesadüfen aynı sonuca varılırsa Apply() atlanabilir ve obje pickup ÖNCESİ
        /// oda/konumuna ait bayat bir görünürlükte kalabilir. Çözüm: OnEnable'da init bayrağını
        /// sıfırla — bu, bir sonraki Update'in (shouldBeVisible ne olursa olsun) Apply()'ı KESİN
        /// çalıştırmasını garantiler.
        /// </summary>
        private void OnEnable()
        {
            _initialized = false;
        }

        private void Update()
        {
            // TUZAK 2 (savunma amaçlı guard): obje bir karakterin (PlayerRoomVisibility taşıyan)
            // altındaysa bu bileşen karışmasın, karakterinki yönetsin. Pratikte PlayerInventory'nin
            // pickup'ta SetActive(false) yapması bu objeyi zaten Update'ten düşürüyor ve eşyalar
            // karaktere reparent edilmiyor (elde tutulan görsel ayrı bir obje) — ama ucuz olduğu
            // için guard yine de burada.
            if (GetComponentInParent<PlayerRoomVisibility>(true) != null)
            {
                return;
            }

            if (RoomRegistry.Count > 0)
            {
                Vector3 pos = transform.position;
                _ownRoomId = RoomRegistry.Resolve(_ownRoomId, pos.x, pos.y, pos.z);
            }
            else
            {
                // H8: sahnede oda sistemi hiç kurulmamış (Tutorial) -> herkes/her şey görünür.
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

        /// <summary>H8 varsayılanı: izleyenin ya da bu eşyanın odası çözülememişse (-1) görünür kal.
        /// İkisi de çözülmüş VE farklıysa, harita görünümü (X) AÇIK DEĞİLSE gizlenir.</summary>
        private bool ComputeVisible()
        {
            var settings = RoomViewSettings.Active;
            if (settings != null && !settings.hideOtherRoomItems)
            {
                return true;
            }

            int viewerRoom = RoomViewController.LocalRoomId;

            if (viewerRoom == RoomResolver.NoRoomSentinel || _ownRoomId == RoomResolver.NoRoomSentinel)
            {
                return true;
            }

            if (_ownRoomId == viewerRoom)
            {
                return true;
            }

            // Başka oda: normalde gizli, ama "stok kontrolü" istisnası — harita görünümü (X)
            // basılıyken eşyalar görünür kalır. Oyuncular bundan etkilenmez (PlayerRoomVisibility
            // bu alanı hiç okumuyor).
            return RoomViewController.MapViewActive;
        }

        private void Apply(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }
    }
}
