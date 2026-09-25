using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;

namespace NewCss
{
    public class EventEffectManager : NetworkBehaviour
    {
        // Her peer'de yerel olarak set edilir (server-only DEĞİL); geç-spawn Truck/CustomerAI
        // OnNetworkSpawn'undan erişmek için. Sahnede tek instance olduğu varsayılır.
        public static EventEffectManager Instance { get; private set; }

        // FESTIVAL DAY para bonusu (ve gelecekteki event ekonomi değerleri) için merkezi SO.
        private GameEconomySettings _economySettings;

        // FESTIVAL DAY bonusunun gün başına yalnız 1 kez verilmesini garanti eder.
        // Host'ta OnNewDay çift-tetiklenir (DayCycleManager doğrudan Invoke + ClientRpc yerel).
        private int _lastFestivalBonusDay = -1;

        [Header("Manager References")]
        public CustomerManager customerManager;
        public UpgradePanel upgradePanel;
        public StaminaBar staminaBar;
        public TruckSpawner truckSpawner;

        [Header("Event Calendar Reference")]
        public EventCalendarUI eventCalendar;

        // Network Variables for synchronization
        private NetworkVariable<int> currentActiveEvent = new NetworkVariable<int>(-1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // List of active events. Yeni event'ler SONA eklenir — currentActiveEvent NetworkVariable<int>
        // burada IndexOf(name) ile çözülen bir index taşır (bkz. OnNewDayHandler), mevcut sıra bozulmaz.
        private List<string> eventNames = new List<string>
        {
            "BUSY DAY",
            "DELIVERY BONUS",
            "ANGRY CUSTOMERS",
            "RELAXED DAY",
            "SLOW LOGISTICS",
            "EXPRESS CARGO",
            "HEAVY BOXES",
            "GOLDEN BOX DAY",
            "OPPORTUNITY DAY",
            "FATIGUE PROBLEM",
            "VIP SERVICE",
            "RAINY DAY",
            "MARKETING DAY",
            "SURPRISE AUDIT",
            "FESTIVAL DAY",
            "CUSTOMER SUPPORT",
            // ---- Yeni 7 (docs/economy/her-gun-event-ve-kart-tasarimi-2026-09-24.md §B + S5) ----
            "MONOCHROME DAY",
            "QUEST DAY",
            "RUSH BONUS",
            "IMPATIENT DRIVERS",
            "RETURN WAVE",
            "SUPPLY STRIKE",
            "MIXED SHIPMENT"
        };

        // Store original values
        private Dictionary<Truck, EventTruckValues> eventStartTruckValues = new Dictionary<Truck, EventTruckValues>();
        private Dictionary<CustomerAI, CustomerWaitTimeValues> eventStartCustomerWaitTimes = new Dictionary<CustomerAI, CustomerWaitTimeValues>();

        private float eventStartPlayerMoveSpeed;
        private float eventStartPlayerSprintSpeed;
        private float eventStartStaminaRegenRate;

        // NEW: Store event customer multiplier for restore
        private float eventStartEventCustomerMultiplier = 1f;

        private Dictionary<string, EventMultipliers> eventMultipliers = new Dictionary<string, EventMultipliers>();

        [System.Serializable]
        public struct EventMultipliers
        {
            public float rewardPerBoxMultiplier;
            public float exitDelayMultiplier;
            public float customerWaitTimeMultiplier;
            public float playerMoveSpeedMultiplier;
            public float playerSprintSpeedMultiplier;
            public float staminaRegenRateMultiplier;
            public float dailyCustomerMultiplier; // NEW: Replaces spawnIntervalMultiplier
            public bool isGoldenBoxDay;
            public bool isVIPServiceDay;
            public float upgradeCostMultiplier;

            // ---- §B "K" kancaları (2026-09-25, her-gün-event-ve-kart-yenileme) ----
            /// <summary>Tüm cezalar (yanlış teslim, kutu düşürme, müşteri kaçma) çarpanı.
            /// GetPenaltyMultiplier() artık isim yerine bunu okur. Varsayılan 1f.</summary>
            public float penaltyMultiplier;
            /// <summary>Truck.hangarStayDuration çarpanı (EXPRESS CARGO ×1.2, IMPATIENT DRIVERS ×0.6).
            /// Varsayılan 1f.</summary>
            public float hangarStayDurationMultiplier;
            /// <summary>Başarılı görev ödülünün (yalnızca Para + Prestij) çarpanı. QUEST DAY ×3. Varsayılan 1f.</summary>
            public float questRewardMultiplier;
            /// <summary>Tırın hangar süresinin ilk yarısında teslim edilen kutu ödülü çarpanı. RUSH BONUS ×1.4. Varsayılan 1f.</summary>
            public float rushBonusMultiplier;
            /// <summary>GOLDEN BOX DAY: yalnızca günün rengiyle eşleşen kutu teslimatında uygulanan ödül çarpanı. Varsayılan 1f.</summary>
            public float goldenBoxColorRewardMultiplier;
            /// <summary>İade (BoxRequest) moduna giriş oranı override'ı. RETURN WAVE=0.45f. -1f = override yok
            /// (PostRentFeatureUnlocks.RETURN_MODE_CHANCE taban değeri kullanılır).</summary>
            public float returnModeChanceOverride;
            /// <summary>MONOCHROME DAY: tüm tır/müşteri renkleri günün rengine zorlanır.</summary>
            public bool isMonochromeDay;
            /// <summary>MIXED SHIPMENT: o gün için karışık renkli tır modu (normalde gün 13+) erken açılır.</summary>
            public bool isMixedShipmentDay;
        }

        [System.Serializable]
        public struct EventTruckValues
        {
            public float rewardPerBox;
            public float exitDelay;
            public float hangarStayDuration;
        }

        [System.Serializable]
        public struct CustomerWaitTimeValues
        {
            public float minWaitTime;
            public float maxWaitTime;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Instance = this;

            if (_economySettings == null)
            {
                _economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }

            InitializeEventMultipliers();
            DayCycleManager.OnNewDay += OnNewDayHandler;
            currentActiveEvent.OnValueChanged += OnActiveEventChanged;

            // Gün 1 fix (2026-09-25): DayCycleManager.OnNewDay yalnızca NextDay()'de tetikleniyor
            // (DayCycleManager.cs:~1002) — koşu başında (gün 1) hiç çağrılmıyor, dolayısıyla gün 1'e
            // atanmış bir event asla currentActiveEvent'e yazılmıyordu. Server burada koşu başında
            // aktif etmeyi dener. currentActiveEvent NetworkVariable'ın kendi initial-sync'i late-join
            // client'ları zaten kapsıyor — bu blok yalnızca server-authoritative ilk yazım için gerekli.
            if (IsServer && currentActiveEvent.Value == -1)
            {
                StartCoroutine(ActivateDay1EventWhenCalendarReadyCoroutine());
            }

            if (currentActiveEvent.Value != -1)
            {
                ApplyEventEffectLocally(eventNames[currentActiveEvent.Value]);
            }
        }

        /// <summary>
        /// EventCalendarUI ile EventEffectManager ayrı NetworkObject'ler olduğundan OnNetworkSpawn
        /// sıraları garanti değildir — takvim henüz üretilmemişken GetEventForDay çağırmak gün 1'i
        /// hep "event yok" sanabilirdi. Takvim hazır olana kadar (server, kendi OnNetworkSpawn'ında
        /// senkron üretir) frame frame bekler, en fazla birkaç frame sürer.
        /// </summary>
        private System.Collections.IEnumerator ActivateDay1EventWhenCalendarReadyCoroutine()
        {
            while (eventCalendar == null || !eventCalendar.IsCalendarGenerated)
            {
                yield return null;
            }

            ActivateEventForCurrentDay();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            DayCycleManager.OnNewDay -= OnNewDayHandler;
            currentActiveEvent.OnValueChanged -= OnActiveEventChanged;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeEventMultipliers()
        {
            eventMultipliers["BUSY DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 0.85f, // NEW: 15% less patience
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1.35f, // 35% more customers per day
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["DELIVERY BONUS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1.2f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["ANGRY CUSTOMERS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 0.6f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1.1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["RELAXED DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1.5f, // §B.3: 1.3 -> 1.5 (+50%)
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f, // §B.10: gizli 0.7 cezası KALDIRILDI
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                penaltyMultiplier = 0.5f // §B.3: tüm cezalar yarıya iner
            };

            eventMultipliers["SLOW LOGISTICS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 0.9f, // §B.15: 0.92 -> 0.9
                exitDelayMultiplier = 2f, // §B.15: 1.5 -> 2 (kalkış 2 kat yavaş)
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["EXPRESS CARGO"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1.10f, // §B.2: 1.08 -> 1.10
                exitDelayMultiplier = 0.5f, // §B.2: 0.7 -> 0.5
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                hangarStayDurationMultiplier = 1.2f // §B.2: hangarda %20 daha uzun kal
            };

            eventMultipliers["HEAVY BOXES"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 0.85f,
                playerSprintSpeedMultiplier = 0.8f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            // §B.8 (yeniden tanımlandı): eski karışık çarpanlar (müşteri/kalkış/hareket/stamina) kalktı.
            // Tek etkisi günün rengiyle eşleşen kutu teslimatında ×1.6 ödül (bkz. goldenBoxColorRewardMultiplier,
            // Truck.CalculateRewardWithPrestige). isGoldenBoxDay artık gerçekten okunuyor (EventEffectManager.
            // TryGetForcedDayColor / GetGoldenBoxColorRewardMultiplier), eskiden hiç okuyucusu yoktu.
            eventMultipliers["GOLDEN BOX DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = true,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                goldenBoxColorRewardMultiplier = 1.6f
            };

            eventMultipliers["OPPORTUNITY DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 0.7f // §B.4: 0.8 -> 0.7
            };

            eventMultipliers["FATIGUE PROBLEM"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 0.9f,
                playerSprintSpeedMultiplier = 0.7f,
                staminaRegenRateMultiplier = 0.6f,
                dailyCustomerMultiplier = 1f, // §B.14 düzeltme: gizli 0.85 müşteri cezası KALDIRILDI (açıklamayla uyuşmuyordu)
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            // §B.9 (yeniden tanımlandı): eski +%12 ödül bonusu (Delivery Bonus'un zayıf kopyasıydı) kalktı.
            // Yeni etki: tüm müşteriler 2 kalem ister (gün 9 kilidi o gün için açılır — bkz.
            // CustomerManager.ShouldAssignDualItemMode, PostRentFeatureUnlocks.IsDualItemUnlocked ||
            // EventEffectManager.IsVIPServiceDay()). isVIPServiceDay zaten mevcuttu, artık gerçekten okunuyor.
            eventMultipliers["VIP SERVICE"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = true,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["RAINY DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 0.8f, // 20% fewer customers
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["MARKETING DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 0.7f, // 30% less earnings per box
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1.2f, // 20% more customers
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            // SURPRISE AUDIT: çarpanlar nötr; çift-ceza artık penaltyMultiplier struct alanından okunur
            // (GetPenaltyMultiplier eskiden isim-tabanlı sabit karşılaştırma yapıyordu, §B.3 Sakin Gün'ün
            // de bu alana ihtiyacı olduğu için genelleştirildi).
            eventMultipliers["SURPRISE AUDIT"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                penaltyMultiplier = 2f
            };

            // FESTIVAL DAY: çarpanlar nötr; gün başı tek seferlik para bonusu OnNewDayHandler'da verilir.
            eventMultipliers["FESTIVAL DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            // CUSTOMER SUPPORT: çarpanlar nötr; PhoneCallManager V4 (2026-08-29, dışarı arama
            // modeli) bu event aktifken IsEventActive("CUSTOMER SUPPORT") ile bir aramanın zaman
            // maliyetini (time-skip dakikası) yarıya indirir (bkz. PhoneCallManager.
            // GetEffectiveTimeSkipMinutes). Eski reaktif modeldeki "çalma sıklığı ×2" karşılığı budur.
            eventMultipliers["CUSTOMER SUPPORT"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            // ---- Yeni 7 (§B + S5 Karışık Sevkiyat) ----

            // MONOCHROME DAY: bugün tüm tır/müşteri renkleri günün rengine zorlanır (öğretici, kolay gün).
            // Çarpanlar nötr — tek etki isMonochromeDay üzerinden TruckSpawner.GenerateRandomTruckData ve
            // CustomerManager.PickCustomerColor'da okunuyor (bkz. TryGetForcedDayColor).
            eventMultipliers["MONOCHROME DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                isMonochromeDay = true
            };

            // QUEST DAY: başarılı görev ödülü (Para + Prestij) ×3. Diğer çarpanlar nötr.
            eventMultipliers["QUEST DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                questRewardMultiplier = 3f
            };

            // RUSH BONUS: tırın hangar süresinin ilk yarısında teslim edilen kutu ödülü ×1.4.
            eventMultipliers["RUSH BONUS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                rushBonusMultiplier = 1.4f
            };

            // IMPATIENT DRIVERS: hangar süresi ×0.6 (tırlar daha erken kalkar).
            eventMultipliers["IMPATIENT DRIVERS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                hangarStayDurationMultiplier = 0.6f
            };

            // RETURN WAVE: iade (BoxRequest) moduna giriş oranı %25 -> %45 (gün 5+, PostRentFeatureUnlocks
            // ile aynı gün eşiği).
            eventMultipliers["RETURN WAVE"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                returnModeChanceOverride = 0.45f
            };

            // SUPPLY STRIKE: müşteri ×0.7, ödül ×0.9. Sert negatif (final bant).
            eventMultipliers["SUPPLY STRIKE"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 0.9f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 0.7f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            // MIXED SHIPMENT (KULLANICI KARARI S5, gün 5-11): o gün için karışık renkli tır modu
            // (normalde gün 13+, PostRentFeatureUnlocks.MIXED_TRUCK_UNLOCK_DAY) erken açılır.
            // Çarpanlar nötr — tek etki isMixedShipmentDay üzerinden TruckSpawner.GenerateRandomTruckData'da okunuyor.
            eventMultipliers["MIXED SHIPMENT"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f,
                isMixedShipmentDay = true
            };
        }

        /// <summary>
        /// currentDay için takvimdeki event'i (varsa) çözüp currentActiveEvent'e yazar. Server-only
        /// çağrılmalı. OnNewDayHandler (gün 2+) ve ActivateDay1EventWhenCalendarReadyCoroutine (gün 1)
        /// tarafından paylaşılır — iki ayrı yol AYNI seçim mantığını kullanır.
        /// </summary>
        private void ActivateEventForCurrentDay()
        {
            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;
            var todaysEvent = eventCalendar.GetEventForDay(currentDay);

            if (todaysEvent != null)
            {
                int eventIndex = eventNames.IndexOf(todaysEvent.name);
                currentActiveEvent.Value = eventIndex != -1 ? eventIndex : -1;
            }
            else
            {
                currentActiveEvent.Value = -1;
            }
        }

        private void OnNewDayHandler()
        {
            if (!IsServer) return;

            ActivateEventForCurrentDay();

            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;

            // FESTIVAL DAY: gün başı tek seferlik rastgele para bonusu (server-only).
            // currentDay guard'ı host'taki OnNewDay çift-tetiklemesine karşı idempotency sağlar
            // (aksi halde bonus host'a iki kez uygulanırdı).
            if (currentActiveEvent.Value != -1 &&
                eventNames[currentActiveEvent.Value] == "FESTIVAL DAY" &&
                currentDay != _lastFestivalBonusDay)
            {
                _lastFestivalBonusDay = currentDay;
                ApplyFestivalBonus();
            }

            NotifyUpgradePanelRefreshClientRpc();
        }

        /// <summary>
        /// FESTIVAL DAY etkinliğinde gün başında bir kez rastgele para bonusu verir.
        /// Yalnızca server çağırır (MoneySystem server-authoritative).
        /// economist FAZ2 önerisi: bonus sabit değil, o anki kiranın %10-%20'si (kira dönemle
        /// büyüdükçe bonus da ölçeklensin). currentRent alınamazsa (DayCycleManager.Instance null)
        /// eski sabit min/max davranışına düşülür.
        /// </summary>
        private void ApplyFestivalBonus()
        {
            if (!IsServer || MoneySystem.Instance == null) return;

            int bonus;
            if (DayCycleManager.Instance != null)
            {
                int currentRent = DayCycleManager.Instance.GetCurrentRentAmount();
                bonus = Mathf.RoundToInt(Random.Range(currentRent * 0.10f, currentRent * 0.20f));
            }
            else
            {
                // Fallback: DayCycleManager erişilemiyorsa eski sabit değerler
                int min = _economySettings != null ? _economySettings.festivalBonusMin : 100;
                int max = _economySettings != null ? _economySettings.festivalBonusMax : 300;
                bonus = Random.Range(min, max + 1); // üst sınır dahil
            }

            MoneySystem.Instance.AddMoney(bonus);
            Debug.Log($"[EventEffectManager] FESTIVAL DAY bonusu: +{bonus} TL");
        }

        [ClientRpc]
        private void NotifyUpgradePanelRefreshClientRpc()
        {
            // For future use
        }

        private void OnActiveEventChanged(int previousValue, int newValue)
        {
            RemoveAllEventEffects();

            if (newValue != -1 && newValue < eventNames.Count)
            {
                ApplyEventEffectLocally(eventNames[newValue]);
            }
        }

        public float GetUpgradeCostMultiplier()
        {
            if (currentActiveEvent.Value == -1)
                return 1f;

            string name = eventNames[currentActiveEvent.Value];
            if (eventMultipliers.TryGetValue(name, out var mult))
                return mult.upgradeCostMultiplier;
            return 1f;
        }

        /// <summary>
        /// SURPRISE AUDIT etkinliğinde tüm ceza değerleri (yanlış teslim, kutu düşme,
        /// müşteri kaçma) bu çarpanla ölçeklenir (2×). Diğer günlerde 1× (etkisiz).
        /// Ceza uygulayan sistemler bu değeri okur.
        /// </summary>
        public float GetPenaltyMultiplier()
        {
            if (currentActiveEvent.Value == -1) return 1f;
            string name = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(name, out EventMultipliers m)) return 1f;
            // 0f = alan hiç set edilmemiş (yeni struct alanları için varsayılan) -> nötr 1f.
            float raw = m.penaltyMultiplier > 0f ? m.penaltyMultiplier : 1f;

            // SERİNLİK (cooler, §D): her çağrıda taze okunur — satın alma anından itibaren hemen etkili.
            float d = GetNegativeEventDampening(name);
            return d > 0f ? Dampen(raw, d) : raw;
        }

        private PlayerMovement GetOwnedPlayer()
        {
            PlayerMovement[] players = FindObjectsOfType<PlayerMovement>();
            foreach (var player in players)
            {
                if (player != null && player.IsOwner)
                    return player;
            }
            return null;
        }

        private void ApplyEventEffectLocally(string eventName)
        {
            if (!eventMultipliers.ContainsKey(eventName))
            {
                Debug.LogWarning($"Event {eventName} not found in event multipliers!");
                return;
            }

            EventMultipliers multipliers = GetDampenedMultipliers(eventMultipliers[eventName], eventName);
            SaveCurrentValuesAndApplyMultipliers(multipliers, eventName);
        }

        // ─────────────────────────────────────────────────────────────
        //  SERİNLİK (cooler, §D, 2026-09-25, gameplay-B kartı): negatif event'lerin sapmasını
        //  ekonomi SO'sundaki negativeEventDampening (0 = kartsız no-op, 0.75 = L1) kadar hafifletir.
        //  Formül GameEconomySettings.negativeEventDampening tooltip'iyle birebir aynı:
        //  dampened = 1 + (raw-1)*(1-d). Yalnızca EventType.Negative event'lerde uygulanır — Neutral
        //  (Busy Day, Mixed Shipment) ve Positive event'ler hiç hafiflemez (tasarım kararı).
        //
        //  Zamanlama kararı (kart gün ortasında alınırsa): PerkEffect.Apply("cooler", ...) bu dosyaya
        //  DOKUNMUYOR (gameplay-B UpgradePanel/PerkEffect tarafı, EventEffectManager'a bildirim
        //  vermiyor) — bu yüzden TAM canlı rebase (o an sahadaki tüm tır/müşteri/oyuncunun mevcut
        //  event çarpanını anında yeniden hesaplaması) bu dosyaların dışına (UpgradePanel'e dokunmak)
        //  taşardı. Bunun yerine mevcut "fresh-read" desenine uyuldu:
        //    - GetPenaltyMultiplier / GetReturnModeChanceOverride HER kullanımda taze okunuyor
        //      (ceza anında, müşteri spawn anında) → satın alma ANINDAN İTİBAREN hemen etkili.
        //    - Truck/customer/player'a BİR KEZ yazılan alanlar (rewardPerBox, exitDelay,
        //      hangarStayDuration, customerWaitTime, playerMoveSpeed/sprintSpeed/staminaRegenRate,
        //      customerManager.eventCustomerMultiplier) yalnız YENİ event aktivasyonunda (ertesi gün)
        //      veya event'ten SONRA spawn olan yeni tır/müşteride (ApplyEventEffectToNewObject)
        //      dampened değeri alır — o anda SAHADA olan tır/müşteri/oyuncu ertesi güne kadar eski
        //      (dampensiz) değerde kalır. Rebase için UpgradePanel'in bildirimi gerekir (out of scope).
        // ─────────────────────────────────────────────────────────────

        private static float Dampen(float raw, float d) => 1f + (raw - 1f) * (1f - d);

        /// <summary>0f = dampening yok (kart yok VEYA event Negative değil). Aksi halde economySettings.negativeEventDampening.</summary>
        private float GetNegativeEventDampening(string eventName)
        {
            if (_economySettings == null) return 0f;
            float d = _economySettings.negativeEventDampening;
            if (d <= 0f) return 0f;
            if (eventCalendar == null) return 0f;
            return eventCalendar.GetEventTypeByName(eventName) == EventCalendarUI.EventType.Negative ? d : 0f;
        }

        /// <summary>
        /// Sentinel alanları (penaltyMultiplier/hangarStayDurationMultiplier, 0f="set edilmemiş")
        /// önce nötre (1f) çözülür, SONRA (negatif event ise) hafifletilir — çağıran taraf artık
        /// 0f-sentinel ayrımını kendi yapmak zorunda değil.
        /// </summary>
        private EventMultipliers GetDampenedMultipliers(EventMultipliers raw, string eventName)
        {
            EventMultipliers m = raw;
            m.penaltyMultiplier = m.penaltyMultiplier > 0f ? m.penaltyMultiplier : 1f;
            m.hangarStayDurationMultiplier = m.hangarStayDurationMultiplier > 0f ? m.hangarStayDurationMultiplier : 1f;

            float d = GetNegativeEventDampening(eventName);
            if (d <= 0f) return m;

            m.rewardPerBoxMultiplier = Dampen(m.rewardPerBoxMultiplier, d);
            m.exitDelayMultiplier = Dampen(m.exitDelayMultiplier, d);
            m.customerWaitTimeMultiplier = Dampen(m.customerWaitTimeMultiplier, d);
            m.playerMoveSpeedMultiplier = Dampen(m.playerMoveSpeedMultiplier, d);
            m.playerSprintSpeedMultiplier = Dampen(m.playerSprintSpeedMultiplier, d);
            m.staminaRegenRateMultiplier = Dampen(m.staminaRegenRateMultiplier, d);
            m.dailyCustomerMultiplier = Dampen(m.dailyCustomerMultiplier, d);
            m.penaltyMultiplier = Dampen(m.penaltyMultiplier, d);
            m.hangarStayDurationMultiplier = Dampen(m.hangarStayDurationMultiplier, d);
            return m;
        }

        private void SaveCurrentValuesAndApplyMultipliers(EventMultipliers multipliers, string eventName)
        {
            // Apply daily customer count changes via multiplier system
            if (customerManager != null)
            {
                // Store original multiplier for restore
                eventStartEventCustomerMultiplier = customerManager.eventCustomerMultiplier;

                // Apply event multiplier (capacity-based system will use this)
                customerManager.eventCustomerMultiplier = multipliers.dailyCustomerMultiplier;

                Debug.Log($"Event '{eventName}': Customer multiplier set to {multipliers.dailyCustomerMultiplier}");
            }

            // Apply player movement changes (only to THIS peer's owned player)
            PlayerMovement playerController = GetOwnedPlayer();
            if (playerController != null)
            {
                eventStartPlayerMoveSpeed = playerController.moveSpeed;
                eventStartPlayerSprintSpeed = playerController.sprintSpeed;
                eventStartStaminaRegenRate = playerController.staminaRegenRate;

                playerController.moveSpeed = eventStartPlayerMoveSpeed * multipliers.playerMoveSpeedMultiplier;
                playerController.sprintSpeed = eventStartPlayerSprintSpeed * multipliers.playerSprintSpeedMultiplier;
                playerController.staminaRegenRate = eventStartStaminaRegenRate * multipliers.staminaRegenRateMultiplier;
            }

            // Apply truck changes
            float hangarMult = multipliers.hangarStayDurationMultiplier > 0f ? multipliers.hangarStayDurationMultiplier : 1f;
            Truck[] trucks = FindObjectsOfType<Truck>();
            foreach (var truck in trucks)
            {
                EventTruckValues currentValues = new EventTruckValues
                {
                    rewardPerBox = truck.rewardPerBox,
                    exitDelay = truck.exitDelay,
                    hangarStayDuration = truck.hangarStayDuration
                };
                eventStartTruckValues[truck] = currentValues;

                truck.rewardPerBox = (int)(currentValues.rewardPerBox * multipliers.rewardPerBoxMultiplier);
                truck.exitDelay = currentValues.exitDelay * multipliers.exitDelayMultiplier;
                truck.hangarStayDuration = currentValues.hangarStayDuration * hangarMult;
            }

            // Apply customer wait time changes
            CustomerAI[] customers = FindObjectsOfType<CustomerAI>();
            foreach (var customer in customers)
            {
                CustomerWaitTimeValues currentValues = new CustomerWaitTimeValues
                {
                    minWaitTime = customer.minWaitTime,
                    maxWaitTime = customer.maxWaitTime
                };
                eventStartCustomerWaitTimes[customer] = currentValues;

                customer.minWaitTime = currentValues.minWaitTime * multipliers.customerWaitTimeMultiplier;
                customer.maxWaitTime = currentValues.maxWaitTime * multipliers.customerWaitTimeMultiplier;
            }
        }

        private void RemoveAllEventEffects()
        {
            // Restore customer multiplier
            if (customerManager != null)
            {
                customerManager.eventCustomerMultiplier = eventStartEventCustomerMultiplier;
            }

            // Restore player movement (same owned-player selection as apply, avoids drift)
            PlayerMovement playerController = GetOwnedPlayer();
            if (playerController != null && eventStartPlayerMoveSpeed > 0)
            {
                playerController.moveSpeed = eventStartPlayerMoveSpeed;
                playerController.sprintSpeed = eventStartPlayerSprintSpeed;
                playerController.staminaRegenRate = eventStartStaminaRegenRate;
            }

            // Restore truck values
            foreach (var kvp in eventStartTruckValues)
            {
                Truck truck = kvp.Key;
                EventTruckValues savedValues = kvp.Value;

                if (truck != null)
                {
                    truck.rewardPerBox = (int)(savedValues.rewardPerBox);
                    truck.exitDelay = savedValues.exitDelay;
                    truck.hangarStayDuration = savedValues.hangarStayDuration;
                }
            }

            // Restore customer wait times
            foreach (var kvp in eventStartCustomerWaitTimes)
            {
                CustomerAI customer = kvp.Key;
                CustomerWaitTimeValues savedValues = kvp.Value;

                if (customer != null)
                {
                    customer.minWaitTime = savedValues.minWaitTime;
                    customer.maxWaitTime = savedValues.maxWaitTime;
                }
            }

            eventStartTruckValues.Clear();
            eventStartCustomerWaitTimes.Clear();
        }

        public void ApplyEventEffectToNewObject(GameObject newObject)
        {
            if (currentActiveEvent.Value == -1) return;

            string currentEventName = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.ContainsKey(currentEventName)) return;

            EventMultipliers multipliers = GetDampenedMultipliers(eventMultipliers[currentEventName], currentEventName);

            if (newObject.TryGetComponent<Truck>(out Truck truck))
            {
                EventTruckValues currentValues = new EventTruckValues
                {
                    rewardPerBox = truck.rewardPerBox,
                    exitDelay = truck.exitDelay,
                    hangarStayDuration = truck.hangarStayDuration
                };
                eventStartTruckValues[truck] = currentValues;

                float hangarMult = multipliers.hangarStayDurationMultiplier > 0f ? multipliers.hangarStayDurationMultiplier : 1f;
                truck.rewardPerBox = (int)(currentValues.rewardPerBox * multipliers.rewardPerBoxMultiplier);
                truck.exitDelay = currentValues.exitDelay * multipliers.exitDelayMultiplier;
                truck.hangarStayDuration = currentValues.hangarStayDuration * hangarMult;
            }

            if (newObject.TryGetComponent<CustomerAI>(out CustomerAI customer))
            {
                CustomerWaitTimeValues currentValues = new CustomerWaitTimeValues
                {
                    minWaitTime = customer.minWaitTime,
                    maxWaitTime = customer.maxWaitTime
                };
                eventStartCustomerWaitTimes[customer] = currentValues;

                customer.minWaitTime = currentValues.minWaitTime * multipliers.customerWaitTimeMultiplier;
                customer.maxWaitTime = currentValues.maxWaitTime * multipliers.customerWaitTimeMultiplier;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  perk-revival QA-fix (bkz. plans/perk-revival.md): AKTİF EVENT sırasında bir tır/oyuncu
        //  perki satın alınırsa, PerkEffect.ApplyToTruck/ApplyToPlayer o alana mutlak değeri
        //  doğrudan yazıyor — bu yazı eventStartTruckValues/eventStart* alanlarındaki
        //  (event-BAŞLANGICINDAKİ, perk-ÖNCESİ) baseline'dan habersiz. Sonuç: (a) o an ekranda
        //  event çarpanı kayboluyor (perkin ham değeri görünüyor, event'in katkısı yok) ve (b)
        //  event bitince RemoveAllEventEffects eski perk-öncesi baseline'ı geri yazıyor — perk
        //  kalıcı olarak siliniyor (yeniden tetiklenmez).
        //
        //  Çözüm: UpgradePanel.ApplyPerkToAllLiveTrucks / ApplyPerkToOwnedPlayer — SADECE satın
        //  alma anı, MEVCUT canlı tır/oyuncu yolu — PerkEffect'in mutlak perk değerini yazmasından
        //  HEMEN SONRA bu Rebase* metodlarını çağırır. Aktif event yoksa no-op (perkin yazdığı ham
        //  değer zaten doğru nihai değer). Aktif event VARSA: baseline o alan için perk değerine
        //  güncellenir VE çarpan o baseline üzerine YENİDEN uygulanır — ApplyEventEffectToNewObject'i
        //  tekrar çağırmak YANLIŞ olurdu (o metod exitDelay/sprintSpeed gibi perkin dokunmadığı
        //  alanları da zaten-çarpılmış haliyle yakalayıp İKİNCİ KEZ çarpardı); bu yüzden yalnızca
        //  perkin dokunduğu tek alan hedeflenir. İdempotent: aynı raw değerle iki kez çağrılırsa
        //  baseline aynı değere eziliyor ve çarpan sıfırdan yeniden hesaplanıyor — üst üste binmez.
        //
        //  BİLİNÇLİ OLARAK Truck.OnNetworkSpawn/PlayerMovement.OnNetworkSpawn (yeni doğan/late-join
        //  nesne) yolunda ÇAĞRILMAZ — o yol zaten ApplyEventEffectToNewObject (tır) veya
        //  EventEffectManager'ın kendi late-join yakalaması (oyuncu) ile doğru şekilde tek kez
        //  çarpılıyor; burada da rebase edilirse ÇİFT çarpım olurdu.
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Truck.rewardPerBox için perk-rebase. gambler_case ve all_in çağırır. penaltyPerBox/
        /// bonusPerTier/hangarStayDuration hiçbir EventMultipliers alanında YOK (grep ile
        /// doğrulandı) — event bu alanlara hiç dokunmuyor, dolayısıyla prestige_broker/fast_hangar
        /// ve gambler_case'in ceza yarısı bu rebase'e ihtiyaç duymaz.
        /// </summary>
        public void RebaseTruckRewardPerBox(Truck truck, int newBaseRewardPerBox)
        {
            if (truck == null || currentActiveEvent.Value == -1) return;

            string currentEventName = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(currentEventName, out EventMultipliers multipliers)) return;

            // Truck event'e hiç girmemişse (beklenmez — Truck.OnNetworkSpawn her zaman
            // ApplyEventEffectToNewObject'i çağırır — ama savunmacı) exitDelay baseline'ı mevcut
            // (muhtemelen zaten authored) değerden kurulur; yalnızca rewardPerBox dokunuluyor.
            EventTruckValues baseline = eventStartTruckValues.TryGetValue(truck, out EventTruckValues existing)
                ? existing
                : new EventTruckValues { exitDelay = truck.exitDelay, hangarStayDuration = truck.hangarStayDuration };

            baseline.rewardPerBox = newBaseRewardPerBox;
            eventStartTruckValues[truck] = baseline;

            truck.rewardPerBox = (int)(baseline.rewardPerBox * multipliers.rewardPerBoxMultiplier);
        }

        /// <summary>PlayerMovement.moveSpeed için perk-rebase. Sadece agile_crew çağırır.</summary>
        public void RebasePlayerMoveSpeed(PlayerMovement player, float newBaseMoveSpeed)
        {
            if (player == null || !player.IsOwner || currentActiveEvent.Value == -1) return;

            string currentEventName = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(currentEventName, out EventMultipliers multipliers)) return;

            eventStartPlayerMoveSpeed = newBaseMoveSpeed;
            player.moveSpeed = eventStartPlayerMoveSpeed * multipliers.playerMoveSpeedMultiplier;
        }

        /// <summary>PlayerMovement.staminaRegenRate için perk-rebase. energetic_crew VE stamina
        /// backbone yükseltmesi (UpgradePanel.ApplyStaminaUpgrade, satın alma anı — stamina-backbone-live
        /// fix, 2026-08-19) çağırır; spawn/late-join yolundan ASLA çağrılmaz (çift çarpım).</summary>
        public void RebasePlayerStaminaRegenRate(PlayerMovement player, float newBaseStaminaRegenRate)
        {
            if (player == null || !player.IsOwner || currentActiveEvent.Value == -1) return;

            string currentEventName = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(currentEventName, out EventMultipliers multipliers)) return;

            eventStartStaminaRegenRate = newBaseStaminaRegenRate;
            player.staminaRegenRate = eventStartStaminaRegenRate * multipliers.staminaRegenRateMultiplier;
        }

        public bool IsGoldenBoxDay()
        {
            if (currentActiveEvent.Value == -1) return false;
            string currentEventName = eventNames[currentActiveEvent.Value];
            return eventMultipliers.ContainsKey(currentEventName) && eventMultipliers[currentEventName].isGoldenBoxDay;
        }

        public bool IsVIPServiceDay()
        {
            if (currentActiveEvent.Value == -1) return false;
            string currentEventName = eventNames[currentActiveEvent.Value];
            return eventMultipliers.ContainsKey(currentEventName) && eventMultipliers[currentEventName].isVIPServiceDay;
        }

        public bool IsEventActive(string eventName)
        {
            if (currentActiveEvent.Value == -1) return false;
            return eventNames[currentActiveEvent.Value] == eventName;
        }

        public string GetCurrentActiveEvent()
        {
            if (currentActiveEvent.Value == -1) return "None";
            return eventNames[currentActiveEvent.Value];
        }

        // ─────────────────────────────────────────────────────────────
        //  §B "K" kancaları (2026-09-25) — yeni/yeniden tanımlı event'lerin okuyucuları.
        //  Hepsi 0f/false varsayılanını nötr sayar (eventMultipliers'ta yalnızca ilgili event alanı
        //  explicit set eder, diğer 22 event için alan hiç dokunulmamış (0f) olarak kalır).
        // ─────────────────────────────────────────────────────────────

        /// <summary>QUEST DAY: başarılı görev ödülü (Para+Prestij) çarpanı. Aktif değilse 1f.</summary>
        public float GetQuestRewardMultiplier()
        {
            if (currentActiveEvent.Value == -1) return 1f;
            string name = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(name, out EventMultipliers m)) return 1f;
            return m.questRewardMultiplier > 0f ? m.questRewardMultiplier : 1f;
        }

        /// <summary>RUSH BONUS: tırın hangar süresinin ilk yarısında teslim edilen kutu ödülü çarpanı. Aktif değilse 1f.</summary>
        public float GetRushBonusMultiplier()
        {
            if (currentActiveEvent.Value == -1) return 1f;
            string name = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(name, out EventMultipliers m)) return 1f;
            return m.rushBonusMultiplier > 0f ? m.rushBonusMultiplier : 1f;
        }

        /// <summary>
        /// GOLDEN BOX DAY: yalnızca teslim edilen kutu, takvimin o gün için ürettiği renkle
        /// eşleşiyorsa ödül çarpanı (1.6f); eşleşmiyorsa veya GOLDEN BOX DAY aktif değilse 1f.
        /// </summary>
        public float GetGoldenBoxColorRewardMultiplier(BoxInfo.BoxType deliveredBoxType)
        {
            if (!IsGoldenBoxDay()) return 1f;
            if (eventCalendar == null || DayCycleManager.Instance == null) return 1f;

            BoxInfo.BoxType? dayColor = eventCalendar.GetColorForDay(DayCycleManager.Instance.currentDay);
            if (!dayColor.HasValue || dayColor.Value != deliveredBoxType) return 1f;

            string name = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(name, out EventMultipliers m)) return 1f;
            return m.goldenBoxColorRewardMultiplier > 0f ? m.goldenBoxColorRewardMultiplier : 1f;
        }

        /// <summary>
        /// MONOCHROME DAY: aktifse takvimin o gün için ürettiği zorunlu rengi döndürür (true).
        /// TruckSpawner.GenerateRandomTruckData ve CustomerManager.PickCustomerColor bunu bag/favor
        /// mantığından ÖNCE kontrol eder.
        /// </summary>
        public bool TryGetForcedDayColor(out BoxInfo.BoxType color)
        {
            color = default;
            if (currentActiveEvent.Value == -1) return false;

            string name = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(name, out EventMultipliers m) || !m.isMonochromeDay) return false;
            if (eventCalendar == null || DayCycleManager.Instance == null) return false;

            BoxInfo.BoxType? dayColor = eventCalendar.GetColorForDay(DayCycleManager.Instance.currentDay);
            if (!dayColor.HasValue) return false;

            color = dayColor.Value;
            return true;
        }

        /// <summary>
        /// RETURN WAVE: iade (BoxRequest) moduna giriş oranı override'ı (0.45f). Aktif değilse -1f
        /// (== "override yok", çağıran taraf PostRentFeatureUnlocks.RETURN_MODE_CHANCE tabanını kullanmalı).
        /// </summary>
        public float GetReturnModeChanceOverride()
        {
            if (currentActiveEvent.Value == -1) return -1f;
            string name = eventNames[currentActiveEvent.Value];
            if (!eventMultipliers.TryGetValue(name, out EventMultipliers m)) return -1f;
            if (m.returnModeChanceOverride <= 0f) return -1f;

            float raw = m.returnModeChanceOverride;

            // SERİNLİK (cooler, §D): override alanları 1f-merkezli değil — taban PostRentFeatureUnlocks.
            // RETURN_MODE_CHANCE (0.25) merkezli lineer interpolasyon: 0.25+(raw-0.25)*(1-d).
            // Her çağrıda taze okunur (müşteri spawn anında) — satın alma anından itibaren hemen etkili.
            float d = GetNegativeEventDampening(name);
            if (d <= 0f) return raw;

            float baseline = PostRentFeatureUnlocks.RETURN_MODE_CHANCE;
            return baseline + (raw - baseline) * (1f - d);
        }

        /// <summary>MIXED SHIPMENT: gün 13 (PostRentFeatureUnlocks.MIXED_TRUCK_UNLOCK_DAY) öncesi de karışık tır modunu o gün için açar.</summary>
        public bool IsMixedShipmentDay()
        {
            if (currentActiveEvent.Value == -1) return false;
            string name = eventNames[currentActiveEvent.Value];
            return eventMultipliers.TryGetValue(name, out EventMultipliers m) && m.isMixedShipmentDay;
        }

    }
}