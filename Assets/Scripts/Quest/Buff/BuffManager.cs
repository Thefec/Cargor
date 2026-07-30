using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// Merkezi Buff yöneticisi - tüm oyunculara uygulanan buff'ları yönetir
    /// Server-authoritative tasarım ile network senkronizasyonu sağlar
    /// </summary>
    public class BuffManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[BuffManager]";

        #endregion

        #region Singleton

        public static BuffManager Instance { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Buff eklendiğinde tetiklenir
        /// </summary>
        public static event Action<BuffData> OnBuffAdded;

        /// <summary>
        /// Buff kaldırıldığında tetiklenir
        /// </summary>
        public static event Action<BuffData> OnBuffRemoved;

        /// <summary>
        /// Buff güncellendiğinde tetiklenir
        /// </summary>
        public static event Action<BuffData> OnBuffUpdated;

        #endregion

        #region Network Variables

        private NetworkList<BuffData> _activeBuffs;

        #endregion

        #region Private Fields

        private bool _isSubscribedToDayCycle;

        #endregion

        #region Public Properties

        /// <summary>
        /// Aktif buff sayısı
        /// </summary>
        public int ActiveBuffCount => _activeBuffs?.Count ?? 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            InitializeNetworkList();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            CleanupSingleton();
            UnsubscribeFromDayCycleEvents();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();
            SubscribeToDayCycleEvents();

            Debug.Log($"{LOG_PREFIX} Spawned - IsServer: {IsServer}");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            UnsubscribeFromDayCycleEvents();

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
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} Duplicate instance detected, destroying...");
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

        private void InitializeNetworkList()
        {
            _activeBuffs = new NetworkList<BuffData>();
        }

        private void SubscribeToNetworkEvents()
        {
            _activeBuffs.OnListChanged += HandleBuffListChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_activeBuffs != null)
            {
                _activeBuffs.OnListChanged -= HandleBuffListChanged;
            }
        }

        private void SubscribeToDayCycleEvents()
        {
            if (_isSubscribedToDayCycle) return;

            DayCycleManager.OnNewDay += HandleNewDay;
            _isSubscribedToDayCycle = true;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            if (!_isSubscribedToDayCycle) return;

            DayCycleManager.OnNewDay -= HandleNewDay;
            _isSubscribedToDayCycle = false;
        }

        #endregion

        #region Event Handlers

        private void HandleBuffListChanged(NetworkListEvent<BuffData> changeEvent)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<BuffData>.EventType.Add:
                    OnBuffAdded?.Invoke(changeEvent.Value);
                    ApplyBuffEffect(changeEvent.Value);
                    break;

                case NetworkListEvent<BuffData>.EventType.Remove:
                    OnBuffRemoved?.Invoke(changeEvent.Value);
                    RemoveBuffEffect(changeEvent.Value);
                    break;

                case NetworkListEvent<BuffData>.EventType.Value:
                    OnBuffUpdated?.Invoke(changeEvent.Value);
                    // Stacking: Add ilk effect'i uyguladi; burada yalniz artan delta kadar ek uygula.
                    var stackDelta = new BuffData(
                        changeEvent.Value.buffType,
                        changeEvent.Value.amount - changeEvent.PreviousValue.amount,
                        changeEvent.Value.remainingDays);
                    ApplyBuffEffect(stackDelta);
                    break;
            }
        }

        private void HandleNewDay()
        {
            if (!IsServer) return;

            Debug.Log($"{LOG_PREFIX} New day - updating temporary buffs");
            UpdateTemporaryBuffs();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Kalıcı buff ekler
        /// </summary>
        public void AddPermanentBuff(BuffType buffType, float amount)
        {
            if (!IsServer)
            {
                AddPermanentBuffServerRpc(buffType, amount);
                return;
            }

            var buff = new BuffData(buffType, amount, 0);
            AddBuffInternal(buff);
        }

        /// <summary>
        /// Geçici buff ekler
        /// </summary>
        public void AddTemporaryBuff(BuffType buffType, float amount, int durationDays)
        {
            if (!IsServer)
            {
                AddTemporaryBuffServerRpc(buffType, amount, durationDays);
                return;
            }

            var buff = new BuffData(buffType, amount, durationDays);
            AddBuffInternal(buff);
        }

        /// <summary>
        /// Belirli türdeki buff'ı kaldırır
        /// </summary>
        public void RemoveBuff(BuffType buffType)
        {
            if (!IsServer)
            {
                RemoveBuffServerRpc(buffType);
                return;
            }

            RemoveBuffInternal(buffType);
        }

        /// <summary>
        /// Tüm buff'ları temizler
        /// </summary>
        public void ClearAllBuffs()
        {
            if (!IsServer)
            {
                ClearAllBuffsServerRpc();
                return;
            }

            ClearAllBuffsInternal();
        }

        /// <summary>
        /// Belirli türdeki buff'ın toplam miktarını döndürür
        /// </summary>
        public float GetBuffAmount(BuffType buffType)
        {
            float total = 0f;

            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].buffType == buffType && _activeBuffs[i].isActive)
                {
                    total += _activeBuffs[i].amount;
                }
            }

            return total;
        }

        /// <summary>
        /// Belirli türde aktif buff var mı?
        /// </summary>
        public bool HasBuff(BuffType buffType)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].buffType == buffType && _activeBuffs[i].isActive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tüm aktif buff'ları döndürür
        /// </summary>
        public List<BuffData> GetAllActiveBuffs()
        {
            var result = new List<BuffData>();

            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].isActive)
                {
                    result.Add(_activeBuffs[i]);
                }
            }

            return result;
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void AddPermanentBuffServerRpc(BuffType buffType, float amount)
        {
            var buff = new BuffData(buffType, amount, 0);
            AddBuffInternal(buff);
        }

        [ServerRpc(RequireOwnership = false)]
        private void AddTemporaryBuffServerRpc(BuffType buffType, float amount, int durationDays)
        {
            var buff = new BuffData(buffType, amount, durationDays);
            AddBuffInternal(buff);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RemoveBuffServerRpc(BuffType buffType)
        {
            RemoveBuffInternal(buffType);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ClearAllBuffsServerRpc()
        {
            ClearAllBuffsInternal();
        }

        #endregion

        #region Internal Methods

        private void AddBuffInternal(BuffData buff)
        {
            // Check for existing buff of same type
            int existingIndex = FindBuffIndex(buff.buffType);

            if (existingIndex >= 0)
            {
                // Stack or replace
                var existing = _activeBuffs[existingIndex];
                existing.amount += buff.amount;

                if (!buff.IsPermanent)
                {
                    existing.remainingDays = Mathf.Max(existing.remainingDays, buff.remainingDays);
                }

                _activeBuffs[existingIndex] = existing;
                Debug.Log($"{LOG_PREFIX} Updated existing buff: {buff.buffType} = {existing.amount}");
            }
            else
            {
                _activeBuffs.Add(buff);
                Debug.Log($"{LOG_PREFIX} Added new buff: {buff.buffType} = {buff.amount}");
            }
        }

        private void RemoveBuffInternal(BuffType buffType)
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (_activeBuffs[i].buffType == buffType)
                {
                    var removed = _activeBuffs[i];
                    _activeBuffs.RemoveAt(i);
                    Debug.Log($"{LOG_PREFIX} Removed buff: {buffType}");
                    break;
                }
            }
        }

        private void ClearAllBuffsInternal()
        {
            _activeBuffs.Clear();

            Debug.Log($"{LOG_PREFIX} Cleared all buffs");
        }

        private int FindBuffIndex(BuffType buffType)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].buffType == buffType)
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateTemporaryBuffs()
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];

                if (buff.IsPermanent) continue;

                buff.remainingDays--;

                if (buff.remainingDays <= 0)
                {
                    Debug.Log($"{LOG_PREFIX} Temporary buff expired: {buff.buffType}");
                    _activeBuffs.RemoveAt(i);
                }
                else
                {
                    _activeBuffs[i] = buff;
                }
            }
        }

        #endregion

        #region Buff Effect Application

        private void ApplyBuffEffect(BuffData buff)
        {
            // Apply buff to relevant systems
            switch (buff.buffType)
            {
                case BuffType.MaxStamina:
                    ApplyMaxStaminaBuff(buff.amount);
                    break;

                case BuffType.MoveSpeed:
                case BuffType.WalkSpeed:
                case BuffType.TempSpeedBoost:
                    ApplySpeedBuff(buff.amount);
                    break;

                case BuffType.StaminaRegenRate:
                    ApplyStaminaRegenBuff(buff.amount);
                    break;

                case BuffType.DayDuration:
                    ApplyDayDurationBuff(buff.amount);
                    break;

                case BuffType.MaxQueueSize:
                    ApplyMaxQueueSizeBuff((int)buff.amount);
                    break;

                case BuffType.CustomerWaitTime:
                    ApplyCustomerWaitTimeBuff(buff.amount);
                    break;
            }
        }

        private void RemoveBuffEffect(BuffData buff)
        {
            // Remove buff from relevant systems (apply negative)
            switch (buff.buffType)
            {
                case BuffType.MaxStamina:
                    ApplyMaxStaminaBuff(-buff.amount);
                    break;

                case BuffType.MoveSpeed:
                case BuffType.WalkSpeed:
                case BuffType.TempSpeedBoost:
                    ApplySpeedBuff(-buff.amount);
                    break;

                case BuffType.StaminaRegenRate:
                    ApplyStaminaRegenBuff(-buff.amount);
                    break;

                case BuffType.DayDuration:
                    ApplyDayDurationBuff(-buff.amount);
                    break;

                case BuffType.MaxQueueSize:
                    ApplyMaxQueueSizeBuff(-(int)buff.amount);
                    break;

                case BuffType.CustomerWaitTime:
                    ApplyCustomerWaitTimeBuff(-buff.amount);
                    break;
            }
        }

        private void ApplyMaxStaminaBuff(float amount)
        {
            // Apply to all PlayerMovement instances
            var players = FindObjectsOfType<PlayerMovement>();
            foreach (var player in players)
            {
                ApplyMaxStaminaToPlayer(player, amount);
            }
        }

        private void ApplySpeedBuff(float amount)
        {
            var players = FindObjectsOfType<PlayerMovement>();
            foreach (var player in players)
            {
                ApplySpeedToPlayer(player, amount);
            }
        }

        private void ApplyStaminaRegenBuff(float amount)
        {
            var players = FindObjectsOfType<PlayerMovement>();
            foreach (var player in players)
            {
                ApplyStaminaRegenToPlayer(player, amount);
            }
        }

        // F10 fix: tek-objelik mutasyon burada toplanır - hem yukarıdaki FindObjectsOfType toplu uygulama
        // hem de aşağıdaki ApplyActiveBuffsTo catch-up API'si AYNI metodları çağırır (mutasyon mantığı
        // kopyalanmaz, tek kaynak).
        private static void ApplyMaxStaminaToPlayer(PlayerMovement player, float amount)
        {
            player.sprintDuration += amount;
        }

        private static void ApplySpeedToPlayer(PlayerMovement player, float amount)
        {
            player.moveSpeed += amount;
        }

        private static void ApplyStaminaRegenToPlayer(PlayerMovement player, float amount)
        {
            player.staminaRegenRate += amount;
        }

        private void ApplyDayDurationBuff(float amount)
        {
            // realDurationInSeconds artık doğrudan yazılmıyor (bkz. DayCycleManager.RecomputeDayDuration) —
            // overtime perki (çarpımsal) ile aynı alanı paylaştığı için tek yazıcı kuralı burada da geçerli.
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.AddBuffDurationBonus(amount);
            }
        }

        private void ApplyMaxQueueSizeBuff(int amount)
        {
            var customerManager = FindObjectOfType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.maxQueueSize += amount;
            }
        }

        private void ApplyCustomerWaitTimeBuff(float amount)
        {
            var customers = FindObjectsOfType<CustomerAI>();
            foreach (var customer in customers)
            {
                ApplyCustomerWaitTimeToCustomer(customer, amount);
            }
        }

        // F10 fix: bkz. yukarıdaki ApplyMaxStaminaToPlayer notu - tek kaynak, catch-up API tarafından da kullanılır.
        private static void ApplyCustomerWaitTimeToCustomer(CustomerAI customer, float amount)
        {
            customer.minWaitTime += amount;
            customer.maxWaitTime += amount;
        }

        #endregion

        #region Catch-up API (F10 fix: late-join / sonradan spawn tüketicileri)

        // GetEntityId() kullanılıyor, NetworkObjectId DEĞİL: CustomerManager.SpawnCustomer,
        // catch-up'ı networkObject.Spawn() ÇAĞRILMADAN ÖNCE tetikler (bkz. CustomerManager.cs) - o anda
        // NetworkObjectId henüz atanmamış olur (0 döner, tüm henüz-spawn-edilmemiş objelerde çakışır).
        // GetEntityId() (eski GetInstanceID) Instantiate anından itibaren her obje için garantili benzersizdir.
        private readonly HashSet<EntityId> _playerCatchupApplied = new HashSet<EntityId>();
        private readonly HashSet<EntityId> _customerCatchupApplied = new HashSet<EntityId>();

        /// <summary>
        /// F10 fix: buff verildiği anda sahnede olmayan (late-join oyuncusu ya da BuffManager senkronundan
        /// sonra spawn olan oyuncu) bir PlayerMovement'a, o ana kadar birikmiş TÜM aktif kalıcı buff'ları
        /// (MaxStamina/MoveSpeed/WalkSpeed/StaminaRegenRate/TempSpeedBoost) tek seferde uygular.
        /// PlayerMovement.OnNetworkSpawn içinden çağrılır.
        /// ÇİFT-UYGULAMA GUARD: aynı obje (GetEntityId) için ikinci çağrı no-op'tur (HashSet.Add false
        /// döner). Bu, olası bir NetworkList "geçmişi replay et" senaryosunda (bu NGO sürümünde olmuyor -
        /// bkz. F10 bug raporu: late-join'de OnListChanged pre-existing kayıtlar için hiç tetiklenmiyor,
        /// o yüzden gerçek çift-uygulama riski yok) veya bu metodun yanlışlıkla iki kez çağrılması
        /// durumunda korumayı garantiler.
        /// </summary>
        public void ApplyActiveBuffsTo(PlayerMovement player)
        {
            if (player == null || _activeBuffs == null) return;
            if (!_playerCatchupApplied.Add(player.GetEntityId())) return;

            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                var buff = _activeBuffs[i];
                if (!buff.isActive) continue;

                switch (buff.buffType)
                {
                    case BuffType.MaxStamina:
                        ApplyMaxStaminaToPlayer(player, buff.amount);
                        break;

                    case BuffType.MoveSpeed:
                    case BuffType.WalkSpeed:
                    case BuffType.TempSpeedBoost:
                        ApplySpeedToPlayer(player, buff.amount);
                        break;

                    case BuffType.StaminaRegenRate:
                        ApplyStaminaRegenToPlayer(player, buff.amount);
                        break;
                }
            }
        }

        /// <summary>
        /// F10 fix: buff verildiği anda sahnede olmayan, sonradan spawn edilen bir CustomerAI'a o ana kadar
        /// birikmiş TÜM aktif CustomerWaitTime buff'larını tek seferde uygular.
        /// ÇAĞRI YERİ ÖNEMLİ: CustomerManager.SpawnCustomer içinde networkObject.Spawn() ÇAĞRILMADAN ÖNCE
        /// çağrılmalıdır - CustomerAI.InitializeServerState, Spawn() sırasında senkron çalışan
        /// OnNetworkSpawn içinden minWaitTime/maxWaitTime'ı okuyup _actualWaitTime'ı hesaplıyor; buff bu
        /// okumadan önce uygulanmış olmalı, yoksa buff'lu süre geç kalan müşteriye yansımaz.
        /// ÇİFT-UYGULAMA GUARD: bkz. ApplyActiveBuffsTo(PlayerMovement) - aynı mantık, GetEntityId bazlı.
        /// </summary>
        public void ApplyActiveBuffsTo(CustomerAI customer)
        {
            if (customer == null || _activeBuffs == null) return;
            if (!_customerCatchupApplied.Add(customer.GetEntityId())) return;

            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                var buff = _activeBuffs[i];
                if (!buff.isActive) continue;

                if (buff.buffType == BuffType.CustomerWaitTime)
                {
                    ApplyCustomerWaitTimeToCustomer(customer, buff.amount);
                }
            }
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print All Buffs")]
        private void DebugPrintAllBuffs()
        {
            Debug.Log($"{LOG_PREFIX} === ACTIVE BUFFS ({_activeBuffs.Count}) ===");

            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                var buff = _activeBuffs[i];
                Debug.Log($"  [{i}] {buff.GetDescription()}");
            }
        }

        [ContextMenu("Debug: Add Test Speed Buff")]
        private void DebugAddTestSpeedBuff()
        {
            AddPermanentBuff(BuffType.MoveSpeed, 2f);
        }

        [ContextMenu("Debug: Add Test Temp Buff")]
        private void DebugAddTestTempBuff()
        {
            AddTemporaryBuff(BuffType.TempMoneyPerBox, 10f, 3);
        }

        [ContextMenu("Debug: Clear All Buffs")]
        private void DebugClearAllBuffs()
        {
            ClearAllBuffs();
        }
#endif

        #endregion
    }
}
