using System;
using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace NewCss
{
    /// <summary>
    /// Günlük Kargo Kotası Yöneticisi - Dinamik kota hesaplama, takip ve gün sonu kontrolü.
    /// Server-authoritative tasarım ile network senkronizasyonu içerir.
    /// Kota = CeilToInt(ToplamMüşteriSayısı × ZorlukOranı)
    /// </summary>
    public class QuotaManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QuotaManager]";

        #endregion

        #region Singleton

        public static QuotaManager Instance { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Kota güncellendiğinde tetiklenir (UI refresh için)
        /// </summary>
        public static event Action OnQuotaUpdated;

        /// <summary>
        /// Kota tamamlandığında tetiklenir (sesli/görsel feedback)
        /// </summary>
        public static event Action OnQuotaCompleted;

        /// <summary>
        /// Kota tutturulamadığında tetiklenir (Game Over)
        /// </summary>
        public static event Action OnQuotaFailed;

        #endregion

        #region Serialized Fields

        [Header("=== QUOTA SETTINGS ===")]
        [SerializeField, Range(0.1f, 1f), Tooltip("Zorluk oranı - Kota = CeilToInt(MüşteriSayısı × Bu Oran)")]
        private float _difficultyRatio = 0.8f;

        [SerializeField, Tooltip("Müşteri varken minimum kota değeri")]
        private int _minimumQuota = 1;

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Kota durumunu gösteren text (örn: 'Kargo: 3/5')")]
        private TextMeshProUGUI _quotaText;

        [Header("=== AUDIO SETTINGS ===")]
        [SerializeField, Tooltip("Kota tamamlama sesi")]
        private AudioSource _completionAudioSource;

        [SerializeField, Tooltip("Kota tamamlama ses klibi")]
        private AudioClip _completionClip;

        [SerializeField, Tooltip("Kota başarısızlık sesi")]
        private AudioClip _failureClip;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool _showDebugLogs = true;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<int> _networkDailyQuota = new(0);
        private readonly NetworkVariable<int> _networkShippedBoxes = new(0);
        private readonly NetworkVariable<bool> _networkIsQuotaMet = new(false);

        #endregion

        #region Private Fields

        private bool _endOfDayChecked;

        #endregion

        #region Public Properties

        /// <summary>
        /// Günlük kota hedefi
        /// </summary>
        public int DailyQuota => _networkDailyQuota.Value;

        /// <summary>
        /// Bugün kargoya atılan kutu sayısı
        /// </summary>
        public int ShippedBoxes => _networkShippedBoxes.Value;

        /// <summary>
        /// Kota tamamlandı mı?
        /// </summary>
        public bool IsQuotaMet => _networkIsQuotaMet.Value;

        /// <summary>
        /// Kota ilerleme yüzdesi (0-1 arası)
        /// </summary>
        public float QuotaProgress => _networkDailyQuota.Value > 0
            ? Mathf.Clamp01((float)_networkShippedBoxes.Value / _networkDailyQuota.Value)
            : 1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
        }

        private void OnDestroy()
        {
            CleanupSingleton();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();

            if (IsServer)
            {
                SubscribeToDayCycleEvents();
                // İlk gün kotasını hesapla (CustomerManager hazırsa)
                InitializeDailyQuota();
            }

            // İlk UI güncellemesi
            UpdateQuotaUI();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();

            if (IsServer)
            {
                UnsubscribeFromDayCycleEvents();
            }

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                LogDebug("Duplicate instance detected, destroying...");
                Destroy(gameObject);
            }
        }

        private void CleanupSingleton()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _networkDailyQuota.OnValueChanged += HandleDailyQuotaChanged;
            _networkShippedBoxes.OnValueChanged += HandleShippedBoxesChanged;
            _networkIsQuotaMet.OnValueChanged += HandleQuotaMetChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkDailyQuota.OnValueChanged -= HandleDailyQuotaChanged;
            _networkShippedBoxes.OnValueChanged -= HandleShippedBoxesChanged;
            _networkIsQuotaMet.OnValueChanged -= HandleQuotaMetChanged;
        }

        private void SubscribeToDayCycleEvents()
        {
            DayCycleManager.OnNewDay += HandleNewDay;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            DayCycleManager.OnNewDay -= HandleNewDay;
        }

        #endregion

        #region Network Event Handlers

        private void HandleDailyQuotaChanged(int previousValue, int newValue)
        {
            UpdateQuotaUI();
            OnQuotaUpdated?.Invoke();
        }

        private void HandleShippedBoxesChanged(int previousValue, int newValue)
        {
            UpdateQuotaUI();
            OnQuotaUpdated?.Invoke();
        }

        private void HandleQuotaMetChanged(bool previousValue, bool newValue)
        {
            if (newValue && !previousValue)
            {
                LogDebug("Kota tamamlandı!");
                PlayCompletionFeedbackClientRpc();
                OnQuotaCompleted?.Invoke();
            }
        }

        #endregion

        #region Day Cycle Handler

        private void HandleNewDay()
        {
            if (!IsServer) return;

            LogDebug("Yeni gün - Kota sıfırlanıyor");
            InitializeDailyQuota();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Kargoya kutu atıldığında çağrılır (Truck.ProcessSuccessfulDelivery tarafından).
        /// Server-only.
        /// </summary>
        public void RegisterShippedBox()
        {
            if (!IsServer) return;

            _networkShippedBoxes.Value++;

            LogDebug($"Kutu kargoya atıldı: {_networkShippedBoxes.Value}/{_networkDailyQuota.Value}");

            // Kota kontrolü
            if (!_networkIsQuotaMet.Value &&
                _networkShippedBoxes.Value >= _networkDailyQuota.Value)
            {
                _networkIsQuotaMet.Value = true;
            }
        }

        /// <summary>
        /// Gün sonu kota kontrolü - DayCycleManager.ProcessDayEnd tarafından çağrılır.
        /// Server-only. İlk çağrıda kontrol yapar, sonraki çağrılarda tekrar yapmaz.
        /// </summary>
        public void CheckEndOfDayQuota()
        {
            if (!IsServer) return;
            if (_endOfDayChecked) return;

            _endOfDayChecked = true;

            if (_networkShippedBoxes.Value >= _networkDailyQuota.Value)
            {
                // Başarı
                LogDebug($"Gün sonu: BAŞARI! {_networkShippedBoxes.Value}/{_networkDailyQuota.Value}");
                NotifyQuotaSuccessClientRpc(_networkShippedBoxes.Value, _networkDailyQuota.Value);
            }
            else
            {
                // Başarısızlık - Game Over
                LogDebug($"Gün sonu: BAŞARISIZ! {_networkShippedBoxes.Value}/{_networkDailyQuota.Value} - GAME OVER");
                NotifyQuotaFailedClientRpc(_networkShippedBoxes.Value, _networkDailyQuota.Value);
            }
        }

        #endregion

        #region Quota Calculation

        /// <summary>
        /// Günlük kotayı hesaplar ve NetworkVariable'ları sıfırlar.
        /// Server-only.
        /// </summary>
        public void InitializeDailyQuota()
        {
            if (!IsServer) return;

            int totalCustomers = GetTodaysTotalCustomers();
            int quota = CalculateQuota(totalCustomers);

            // Network değerlerini sıfırla
            _networkDailyQuota.Value = quota;
            _networkShippedBoxes.Value = 0;
            _networkIsQuotaMet.Value = quota <= 0; // Kota 0 ise otomatik tamamlanmış say
            _endOfDayChecked = false;

            LogDebug($"Günlük kota hesaplandı: {quota} " +
                     $"(Müşteri: {totalCustomers}, Zorluk: {_difficultyRatio:F2})");
        }

        private int GetTodaysTotalCustomers()
        {
            if (CustomerManager.Instance == null)
            {
                LogDebug("CustomerManager bulunamadı, kota 0 olarak ayarlanıyor");
                return 0;
            }

            return CustomerManager.Instance.TodaysTotalCustomers;
        }

        private int CalculateQuota(int totalCustomers)
        {
            if (totalCustomers <= 0) return 0;

            int quota = Mathf.CeilToInt(totalCustomers * _difficultyRatio);
            return Mathf.Max(quota, _minimumQuota);
        }

        #endregion

        #region UI Update

        private void UpdateQuotaUI()
        {
            if (_quotaText == null) return;

            _quotaText.text = $"Kargo: {_networkShippedBoxes.Value}/{_networkDailyQuota.Value}";
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void PlayCompletionFeedbackClientRpc()
        {
            LogDebug("Kota tamamlama feedback'i oynatılıyor");

            if (_completionAudioSource != null && _completionClip != null)
            {
                _completionAudioSource.PlayOneShot(_completionClip);
            }

            OnQuotaCompleted?.Invoke();
        }

        [ClientRpc]
        private void NotifyQuotaSuccessClientRpc(int shipped, int quota)
        {
            LogDebug($"Gün başarıyla tamamlandı: {shipped}/{quota}");
            // Gün sonu özet ekranında gösterilebilir
        }

        [ClientRpc]
        private void NotifyQuotaFailedClientRpc(int shipped, int quota)
        {
            LogDebug($"KOTA BAŞARISIZ: {shipped}/{quota} - Game Over!");

            if (_completionAudioSource != null && _failureClip != null)
            {
                _completionAudioSource.PlayOneShot(_failureClip);
            }

            OnQuotaFailed?.Invoke();
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (_showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        #endregion

        #region Editor & Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === QUOTA STATE ===\n" +
                      $"Daily Quota: {_networkDailyQuota.Value}\n" +
                      $"Shipped Boxes: {_networkShippedBoxes.Value}\n" +
                      $"Is Quota Met: {_networkIsQuotaMet.Value}\n" +
                      $"Difficulty Ratio: {_difficultyRatio:F2}\n" +
                      $"End of Day Checked: {_endOfDayChecked}\n" +
                      $"Progress: {QuotaProgress:P0}");
        }

        [ContextMenu("Debug: Force Complete Quota")]
        private void DebugForceCompleteQuota()
        {
            if (!IsServer)
            {
                Debug.LogWarning($"{LOG_PREFIX} Server-only operation!");
                return;
            }

            _networkShippedBoxes.Value = _networkDailyQuota.Value;
            _networkIsQuotaMet.Value = true;
            Debug.Log($"{LOG_PREFIX} Kota zorla tamamlandı!");
        }

        [ContextMenu("Debug: Add Shipped Box")]
        private void DebugAddShippedBox()
        {
            if (!IsServer)
            {
                Debug.LogWarning($"{LOG_PREFIX} Server-only operation!");
                return;
            }

            RegisterShippedBox();
        }

        [ContextMenu("Debug: Recalculate Quota")]
        private void DebugRecalculateQuota()
        {
            if (!IsServer)
            {
                Debug.LogWarning($"{LOG_PREFIX} Server-only operation!");
                return;
            }

            InitializeDailyQuota();
        }
#endif

        #endregion
    }
}
