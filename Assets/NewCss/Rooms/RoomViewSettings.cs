using NewCss.Rooms.Core;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Oda görünürlük/karartma sisteminin TEK ayar yüzeyi. Sahnede `---ROOMS---` objesine
    /// takılır ve buradaki değerler diske kaydedilir.
    ///
    /// NEDEN AYRI BİR BİLEŞEN: RoomViewController kendini çalışma anında yaratıyor
    /// (RuntimeInitializeOnLoadMethod), yani onun Inspector'ında yapılan her ayar Play'den
    /// çıkınca KAYBOLUYORDU. Ayarlar sahnedeki bir bileşene taşınınca kalıcı oluyor: bir kez
    /// ayarla, biter. Bu yüzden tuning alanları RoomViewController'dan KALDIRILDI — iki ayrı
    /// yerde ayar olsaydı hangisinin kazandığı belirsiz kalırdı.
    ///
    /// Bu bileşen sahnede YOKSA sistem RoomViewController'daki sabit varsayılanlarla çalışır
    /// (Tutorial ve oda kurulmamış sahneler güvende).
    ///
    /// Değişiklikler ANINDA uygulanır — oyun çalışırken kaydırıcıları çevirip sonucu
    /// gerçek zamanlı görebilirsin.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomViewSettings : MonoBehaviour
    {
        /// <summary>Sahnedeki aktif ayar bileşeni; yoksa null (varsayılanlar kullanılır).</summary>
        public static RoomViewSettings Active { get; private set; }

        [Header("═══ GEÇİŞ SÜRELERİ ═══")]

        [Tooltip("ODA DEĞİŞİMİ geçiş süresi (saniye). Bir odadan diğerine geçince aydınlık bölgenin " +
                 "eski odadan yenisine yumuşakça kayma süresi. Küçült = daha çevik, büyüt = daha ağır. " +
                 "0.05'e çekersen pratikte eski ani sıçrama davranışına dönersin.")]
        [Range(0.05f, 5f)]
        public float roomBlendDuration = 0.6f;

        [Tooltip("HARİTA GÖRÜNÜMÜ (X) giriş/çıkış fade süresi (saniye). Kameranın kendi geçişiyle " +
                 "uyumlu tutulmalı (CameraFollow.mapViewTransitionSpeed = 5, yani ~0.2 sn) — çok " +
                 "büyütürsen kamera yerine oturur ama dünya hâlâ renklenmeye devam eder.")]
        [Range(0.05f, 3f)]
        public float mapFadeDuration = 0.2f;

        [Header("═══ KARARTMA GÖRÜNÜMÜ ═══")]

        [Tooltip("Başka odaların renksizleştirilme miktarı. 0 = renkli kalır, 1 = tam gri.")]
        [Range(0f, 1f)]
        public float desaturation = 1f;

        [Tooltip("Başka odaların karartılma çarpanı. 1 = hiç karartma, 0 = simsiyah.")]
        [Range(0f, 1f)]
        public float dim = 0.35f;

        [Header("═══ GÖRÜNÜRLÜK ═══")]

        [Tooltip("Kapalıysa başka odadaki oyuncular GİZLENMEZ, yalnız harita görünümündeki karartma " +
                 "çalışır. Karartmayı tek başına ayarlarken ya da görünürlüğün bir sorunun sebebi olup " +
                 "olmadığını test ederken kapat.")]
        public bool hideOtherRoomPlayers = true;

        [Header("═══ İLERİ DÜZEY ═══")]

        [Tooltip("KAPI TOLERANSI (metre). Oda kutuları sorgu sırasında bu kadar genişletilir ve " +
                 "kararsız bölgede mevcut oda korunur — kapı ağzında durunca görünürlüğün titremesini " +
                 "bu engelliyor. Küçültme: titreme riski. Büyütme: oda değişimi geç algılanır.")]
        [Range(0f, 2f)]
        public float doorwayMargin = RoomResolver.DefaultMargin;

        private void OnEnable()
        {
            // Son etkinleşen kazanır. [DisallowMultipleComponent] aynı objede ikinciyi engelliyor
            // ama farklı objelerde iki panel olabilir — o durumda sonuncusu geçerli olur.
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }
    }
}
