using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    /// <summary>
    /// Boş kutu çarpışma sonrası kırılma sistemi.
    /// Server tarafında çarpışma algılanır, ClientRpc ile tüm client'larda efekt tetiklenir,
    /// ardından kutu Despawn edilir.
    /// 
    /// Gereksinimler:
    /// - BoxInfo component'i (kutu tipi ve doluluk bilgisi)
    /// - NetworkObject component'i (ağ senkronizasyonu)
    /// - Rigidbody component'i (hız hesaplaması)
    /// - Sahnede bir BoxDestructionEffectManager instance'ı
    /// </summary>
    [RequireComponent(typeof(BoxInfo))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public class BoxDestroyOnCollisionNetcode : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CrateDestruction]";

        #endregion

        #region Serialized Fields

        [Header("=== ÇARPIŞMA AYARLARI ===")]
        [SerializeField, Tooltip("Kırılma için gereken minimum çarpma hızı (m/s)")]
        private float impactSpeedThreshold = 3f;

        [SerializeField, Tooltip("Sadece boş kutular kırılabilir mi?")]
        private bool onlyIfEmpty = false;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = false;

        #endregion

        #region Private Fields

        /// <summary>
        /// Çoklu OnCollisionEnter çağrısını engellemek için bayrak.
        /// Kutu bir kez kırıldığında tekrar tetiklenmez.
        /// </summary>
        private bool _isDestroyed = false;

        /// <summary>
        /// Fırlatılma bayrağı. Sadece fırlatılan kutular kırılır.
        /// Bırakılan kutular yere güvenle konulur.
        /// </summary>
        private bool _wasThrown = false;

        private BoxInfo _boxInfo;
        private Rigidbody _rigidbody;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Component referanslarını cache'le
            _boxInfo = GetComponent<BoxInfo>();
            if (_boxInfo == null)
            {
                _boxInfo = GetComponentInParent<BoxInfo>();
            }

            _rigidbody = GetComponent<Rigidbody>();
        }

        #endregion

        #region Collision Detection

        private void OnCollisionEnter(Collision collision)
        {
            // ─── GÜVENLİK KONTROLLER ─────────────────────────────
            // 1. Zaten kırılmış mı?
            if (_isDestroyed) return;

            // 2. Sadece server çarpışma kararı verir (authoritative)
            if (!IsServer) return;

            // 3. Sadece fırlatılan kutular kırılır (bırakılan kutular güvenle yere konulur)
            if (!_wasThrown) return;

            // 4. Dolu kutu kontrolü (opsiyonel — varsayılan: her kutu kırılabilir)
            if (onlyIfEmpty && _boxInfo != null && _boxInfo.isFull) return;

            // ─── HIZ KONTROLÜ ─────────────────────────────────────
            // Çarpışma anındaki göreceli hız (relativeVelocity)
            // Bu değer, iki objenin birbirine göre hızını verir
            float impactSpeed = collision.relativeVelocity.magnitude;

            if (impactSpeed < impactSpeedThreshold)
            {
                return; // Yeterli hız yok, kırılma gerçekleşmez
            }

            // ─── KIRILMA TETİKLE ──────────────────────────────────
            LogDebug($"Çarpışma algılandı! Hız: {impactSpeed:F2} m/s (eşik: {impactSpeedThreshold:F2}). " +
                     $"Çarpılan: {collision.gameObject.name}");

            HandleDestruction();
        }

        /// <summary>
        /// Kutuyu fırlatılmış olarak işaretler.
        /// Sadece fırlatılan kutular çarpışma sonrası kırılır.
        /// PlayerInventory tarafından fırlatma sırasında çağrılır.
        /// </summary>
        public void MarkAsThrown()
        {
            _wasThrown = true;
            LogDebug("Kutu fırlatılmış olarak işaretlendi — çarpışma sonrası kırılacak.");
        }

        #endregion

        #region Destruction Logic

        /// <summary>
        /// Kırılma sürecini başlatır. Sadece Server tarafından çağrılır.
        /// 1. _isDestroyed bayrağını kaldır (çift tetiklenme koruması)
        /// 2. Pozisyon ve kutu tipini kaydet
        /// 3. ClientRpc ile tüm client'larda efekt tetikle
        /// 4. NetworkObject'i Despawn et
        /// </summary>
        private void HandleDestruction()
        {
            // Çift tetiklenme koruması
            _isDestroyed = true;

            // Pozisyon ve tip bilgisini Despawn öncesi kaydet
            Vector3 destroyPosition = transform.position;
            int boxTypeInt = _boxInfo != null ? (int)_boxInfo.boxType : 0;

            LogDebug($"Kutu kırılıyor: {gameObject.name} | Pozisyon: {destroyPosition} | Tip: {(BoxInfo.BoxType)boxTypeInt}");

            // ─── TÜM CLIENT'LARDA EFEKT OYNAT ────────────────────
            // NetworkObject hâlâ aktifken RPC gönder
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                PlayDestructionEffectClientRpc(destroyPosition, boxTypeInt);
            }

            // ─── KUTUYU YOK ET ────────────────────────────────────
            DespawnBox();
        }

        /// <summary>
        /// Kutuyu ağdan güvenli bir şekilde kaldırır.
        /// Despawn(true) = Despawn + Destroy
        /// </summary>
        private void DespawnBox()
        {
            if (NetworkObject == null)
            {
                LogDebug("NetworkObject null — sadece local Destroy yapılıyor.");
                Destroy(gameObject);
                return;
            }

            if (!NetworkObject.IsSpawned)
            {
                LogDebug("NetworkObject spawn edilmemiş — sadece local Destroy yapılıyor.");
                Destroy(gameObject);
                return;
            }

            try
            {
                // Despawn(true) = Despawn + GameObject'i yok et
                NetworkObject.Despawn(true);
                LogDebug("Kutu başarıyla Despawn edildi.");
            }
            catch (System.Exception ex)
            {
                // Despawn başarısız olursa en azından local'de temizle
                Debug.LogError($"{LOG_PREFIX} Despawn hatası: {ex.Message}");
                Destroy(gameObject);
            }
        }

        #endregion

        #region ClientRpc

        /// <summary>
        /// Tüm client'larda (Host dahil) kırılma efektini tetikler.
        /// Efekt local olarak oluşturulur — NetworkObject olarak spawn edilmez.
        /// Bu yaklaşım bant genişliğinden tasarruf sağlar.
        /// </summary>
        /// <param name="position">Efektin oynatılacağı dünya pozisyonu</param>
        /// <param name="boxType">Kutu tipi (renk belirlemek için)</param>
        [ClientRpc]
        private void PlayDestructionEffectClientRpc(Vector3 position, int boxType)
        {
            LogDebug($"ClientRpc alındı — Efekt oynatılıyor: Pozisyon={position}, Tip={boxType}");

            // BoxDestructionEffectManager sahnede var mı?
            var effectManager = BoxDestructionEffectManager.Instance;
            if (effectManager == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} BoxDestructionEffectManager bulunamadı! " +
                                 "Sahneye BoxDestructionEffectManager ekleyin.");
                return;
            }

            // Güvenli cast: int → BoxType enum
            BoxInfo.BoxType type = System.Enum.IsDefined(typeof(BoxInfo.BoxType), boxType)
                ? (BoxInfo.BoxType)boxType
                : BoxInfo.BoxType.Yellow; // Fallback renk

            // Local efekt oynat
            effectManager.PlayEffect(type, position);
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
#if UNITY_EDITOR
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
#endif
        }

        #endregion
    }
}
