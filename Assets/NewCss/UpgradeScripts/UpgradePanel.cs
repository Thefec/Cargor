using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace NewCss
{
    #region Data Classes

    /// <summary>
    /// Upgrade tanımı - her upgrade'in özelliklerini içerir
    /// </summary>
    [Serializable]
    public class UpgradeDefinition
    {
        [Header("=== BASIC INFO ===")]
        [Tooltip("Görünen isim")]
        public string displayName;

        [Tooltip("Localization key for display name")]
        public string displayNameLocKey;

        [Tooltip("Açıklama metni")]
        public string contentText;

        [Tooltip("Localization key for content text")]
        public string contentTextLocKey;

        [Header("=== VALUES ===")]
        [Tooltip("Başlangıç değeri")]
        public int starterValue;

        [Tooltip("Stamina değeri")]
        public int StaminaValue = 1;

        [Tooltip("Kamyon değeri")]
        public int TruckValue = 20;

        [Tooltip("Bekleme süresi")]
        public int WaitTime;

        [Header("=== COST & LEVELS ===")]
        [Tooltip("Maksimum seviye")]
        public int maxLevel;

        [Tooltip("Temel maliyet")]
        public int baseCost;

        [Tooltip("Seviye başına maliyet artışı")]
        public int costStep;

        [Header("=== ROGUELITE ===")]
        [Tooltip("Omurga mı perk mi? Omurga tier'sız, her zaman havuzda.")]
        public PerkKind kind = PerkKind.LeveledBackbone;

        [Tooltip("Perk güç tier'ı (T1 hep açık, T2 gün>=5, T3 gün>=9). Omurga için yok sayılır.")]
        public PerkTier tier = PerkTier.T1;

        [Tooltip("Effect registry anahtarı (PerkEffect.cs). Boşsa mevcut displayName switch'i kullanılır.")]
        public string effectId;

        [Tooltip("True ise görev sistemi aktif olana kadar havuza girmez (Görev Tier).")]
        public bool requiresQuestSystem;

        [Tooltip("True ise bu upgrade draft havuzuna (günlük teklif/reroll) hiç girmez. Index-güvenli " +
            "hariç tutma — upgrades listesinden ELEMAN SİLİNMEZ (NetworkList index'leri kayar, netcode " +
            "bozulur). Eski/ölü/duplike omurgaları (Money, Customer, Water, Stamina, Queue) kapatmak için.")]
        public bool disabledInDraft;

        [Header("=== LEVEL OBJECTS ===")]
        [Tooltip("Seviye objeleri (Level0, Level1, Level2...)")]
        public GameObject[] levelObjects;

        [Header("=== GARAGE DOORS (Truck Only) ===")]
        [Tooltip("Her seviye için garaj kapı kontrolcüleri")]
        public GarageDoorController[] garageDoorControllers;
    }

    /// <summary>
    /// Network senkronizasyonu için bekleyen upgrade struct'ı
    /// </summary>
    [Serializable]
    public struct NetworkPendingUpgrade : INetworkSerializable, IEquatable<NetworkPendingUpgrade>
    {
        public FixedString64Bytes upgradeName;
        public int levelToBecomeActive;
        public int dayPurchased;

        public NetworkPendingUpgrade(string name, int level, int day)
        {
            upgradeName = name;
            levelToBecomeActive = level;
            dayPurchased = day;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref upgradeName);
            serializer.SerializeValue(ref levelToBecomeActive);
            serializer.SerializeValue(ref dayPurchased);
        }

        public bool Equals(NetworkPendingUpgrade other)
        {
            return upgradeName.Equals(other.upgradeName) &&
                   levelToBecomeActive == other.levelToBecomeActive &&
                   dayPurchased == other.dayPurchased;
        }

        public override bool Equals(object obj) => obj is NetworkPendingUpgrade other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(upgradeName.GetHashCode(), levelToBecomeActive, dayPurchased);
        public static bool operator==(NetworkPendingUpgrade left, NetworkPendingUpgrade right) => left.Equals(right);
        public static bool operator !=(NetworkPendingUpgrade left, NetworkPendingUpgrade right) => !left.Equals(right);
    }

    #endregion

    /// <summary>
    /// Upgrade panel yöneticisi - upgrade satın alma, network senkronizasyonu ve UI yönetimini sağlar. 
    /// </summary>
    public class UpgradePanel : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[UpgradePanel]";
        private const int PANEL_OPEN_HOUR = 10;

        // Upgrade Names
        private const string UPGRADE_QUEUE = "Queue";
        private const string UPGRADE_STAMINA = "Stamina";
        private const string UPGRADE_MONEY = "Money";
        private const string UPGRADE_TRUCK = "Truck";
        private const string UPGRADE_QUEST_TIER = "Quest Tier";

        // UI Element Names
        private const string UI_NAME_TEXT = "NameText";
        private const string UI_LEVEL_TEXT = "LevelText";
        private const string UI_COST_TEXT = "CostText";
        private const string UI_CONTENT_TEXT = "ContentText";
        private const string UI_BUY_BUTTON = "BuyButton";
        private const string UI_BUTTON_TEXT = "Text";
        private const string UI_BUTTON_TEXT_TMP = "Text (TMP)";

        // Localization Keys
        private const string LOC_KEY_LEVEL = "UpgradeLevel";
        private const string LOC_KEY_COST = "UpgradeCost";
        private const string LOC_KEY_MAX = "UpgradeMax";
        private const string LOC_KEY_BUY = "UpgradeBuy";

        // Roguelite draft dışlama grupları — aynı gruptan aynı 3-kart teklifte en fazla 1 kart çıkar.
        // Kullanıcı kararı: gambler_case ve all_in birlikte asla teklif edilmez (günlük teklif + reroll).
        private static readonly string[][] EXCLUSIVE_EFFECT_GROUPS =
        {
            new[] { "gambler_case", "all_in" },
        };

        #endregion

        #region Nested Classes

        private class EntryUI
        {
            public UpgradeDefinition Definition;
            public int UpgradeIndex;
            public TMP_Text NameText;
            public TMP_Text LevelText;
            public TMP_Text CostText;
            public TMP_Text ContentText;
            public Button BuyButton;
        }

        #endregion

        #region Serialized Fields - UI

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Ana panel")]
        private GameObject panel;

        [SerializeField, Tooltip("Content parent transform")]
        private Transform contentParent;

        [SerializeField, Tooltip("Entry prefab'ı")]
        private GameObject entryPrefab;

        [SerializeField, Tooltip("Reroll butonu")]
        private Button rerollButton;

        [SerializeField, Tooltip("Reroll maliyet metni")]
        private TMP_Text rerollCostText;

        #endregion

        #region Serialized Fields - Upgrades

        [Header("=== UPGRADE DEFINITIONS ===")]
        [SerializeField, Tooltip("Tüm upgrade tanımları")]
        private List<UpgradeDefinition> upgrades = new();

        #endregion

        #region Serialized Fields - Manager References

        [Header("=== MANAGER REFERENCES ===")]
        [SerializeField] private CustomerManager CustomerManager;
        [SerializeField] private PlayerMovement PlayerMovement;
        [SerializeField] private Truck Truck;
        [SerializeField] private CustomerAI CustomerAI;
        [SerializeField] private EventEffectManager eventEffectManager;

        [Tooltip("Perk registry'sinin (PerkEffect.cs) dokunduğu tekil ekonomi SO'su. Boşsa Resources/EkonomiAyarlari yüklenir (Truck/DayCycleManager ile aynı kaynak).")]
        [SerializeField] private GameEconomySettings economySettings;

        #endregion

        #region Network Variables

        private NetworkList<int> _upgradeLevels;
        private NetworkList<int> _visualUpgradeLevels;
        private NetworkList<NetworkPendingUpgrade> _pendingUpgrades;
        private NetworkList<int> _dailyOffer;
        private readonly NetworkVariable<bool> _isPanelOpen = new(false);
        private readonly NetworkVariable<int> _rerollCountToday = new(0);
        private readonly NetworkVariable<bool> _questSystemActive = new(false); // Görev Tier feature-flag
        private readonly NetworkVariable<int> _discountedUpgradeIndex = new(-1); // Toplu Alım (bulk_buy) — sonraki tekliftedki 1 kart -%50

        #endregion

        #region Private Fields - Perk State

        private bool _pendingBulkBuyDiscount; // server-only: bir sonraki GenerateDailyOfferServer'da tüketilir

        #endregion

        #region Private Fields

        private readonly List<EntryUI> _entries = new();

        #endregion

        #region Public Properties

        /// <summary>
        /// Panel açık mı? 
        /// </summary>
        public bool IsPanelOpen => _isPanelOpen.Value;

        /// <summary>
        /// Upgrade sayısı
        /// </summary>
        public int UpgradeCount => upgrades.Count;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeNetworkLists();
            LocalizationHelper.OnLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            LocalizationHelper.OnLocaleChanged -= OnLocaleChanged;
        }
        
        private void OnLocaleChanged()
        {
            // Refresh all upgrade entries when locale changes
            RefreshAllUpgradeUI();
        }

        /// <summary>
        /// Reconnect/geç-join fix: NGO'nun NetworkList.OnListChanged'i geç-join eden client'a
        /// mevcut elemanları replay ETMEZ (yalnızca yeni değişiklikleri bildirir). Bu yüzden
        /// spawn anında zaten level>0 olan her perk'in efektini burada elle uyguluyoruz.
        /// InitializeBaseValues'tan SONRA çağrılmalı — aksi halde base init, burada uygulanan
        /// mutlak değerleri ezer. Tüm Apply yolları (PerkEffect.Apply + ApplyXUpgrade) level'dan
        /// mutlak değer hesaplar (+= yok), bu yüzden fresh spawn'da (tüm level=0) no-op ve
        /// tekrar tekrar çağrılsa da güvenlidir (idempotent).
        /// </summary>
        private void ReplayPurchasedUpgradeLevels()
        {
            for (int i = 0; i < _upgradeLevels.Count; i++)
            {
                int level = _upgradeLevels[i];
                if (level <= 0) continue;
                ReplayUpgradeLevel(i, level);
            }
        }

        /// <summary>
        /// Bir upgrade index'i için efekt+görsel+özel-durum uygulamasını yapan ortak adım.
        /// Hem canlı <see cref="HandleUpgradeLevelsChanged"/> event'i hem de geç-join replay'i
        /// (<see cref="ReplayPurchasedUpgradeLevels"/>) bunu çağırır — davranış tek yerde.
        ///
        /// NOT (kapsam-içi düzeltme): eski kod burada <c>_entries[upgradeIndex]</c> pozisyonel
        /// erişimi kullanıyordu, ama <see cref="_entries"/> yalnızca O ANKİ ≤3 kartlık draft
        /// teklifini tutar (bkz. RebuildDraftEntries) ve sırası gerçek `upgrades` index'iyle
        /// EŞLEŞMEZ (DraftPool.SelectOffer rastgele karıştırır) — bu da hem canlı satın almada
        /// hem de yeni replay'de yanlış/eksik efekt uygulanmasına yol açardı. ApplyUpgradeEffect/
        /// UpdateLevelObjects/HandleSpecialUpgrades yalnızca EntryUI.Definition'a (sahne/veri
        /// referansı, UI widget'larından bağımsız) ihtiyaç duyduğundan, burada doğrudan
        /// upgrades[upgradeIndex]'ten kurulan hafif bir EntryUI kullanılıyor — _entries'in o an
        /// neyi teklif ettiğinden tamamen bağımsız, dolayısıyla her zaman doğru.
        /// </summary>
        private void ReplayUpgradeLevel(int upgradeIndex, int newLevel)
        {
            if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) return;

            var entry = new EntryUI { Definition = upgrades[upgradeIndex], UpgradeIndex = upgradeIndex };
            ApplyUpgradeEffect(entry, newLevel);
            UpdateLevelObjects(entry, newLevel);
            HandleSpecialUpgrades(entry, newLevel);
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (economySettings == null)
            {
                // Truck/DayCycleManager ile aynı tekil kaynak (Resources/EkonomiAyarlari.asset).
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }

            if (IsServer)
            {
                InitializeUpgradeLevels();
                GenerateDailyOfferServer();
            }

            SubscribeToNetworkEvents();
            InitializePanel();
            RebuildDraftEntries();
            InitializeLevelObjects();
            InitializeBaseValues();
            ReplayPurchasedUpgradeLevels();
            SubscribeToDayCycleEvents();
            SubscribeToMoneySystem();

            if (rerollButton != null) rerollButton.onClick.AddListener(OnReroll);
            RefreshRerollUI();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            UnsubscribeFromDayCycleEvents();
            UnsubscribeFromMoneySystem();

            if (rerollButton != null) rerollButton.onClick.RemoveListener(OnReroll);

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeNetworkLists()
        {
            _upgradeLevels = new NetworkList<int>();
            _visualUpgradeLevels = new NetworkList<int>();
            _pendingUpgrades = new NetworkList<NetworkPendingUpgrade>();
            _dailyOffer = new NetworkList<int>();
        }

        private void InitializeUpgradeLevels()
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                _upgradeLevels.Add(0);
                _visualUpgradeLevels.Add(0);
            }
        }

        private void InitializePanel()
        {
            panel.SetActive(false);
        }

        /// <summary>
        /// Tüm upgrade'lerin görsel durumunu seviye-0 tabanına kurar. `_entries` (o günün ≤3
        /// kartlık draft teklifi) yerine TAM `upgrades` listesi üzerinde döner — eski hâli
        /// teklifte olmayan upgrade'lerin levelObjects[0]'ını hiç aktive etmiyordu
        /// (bkz. FindDefinitionByName notu, aynı bug sınıfı). Satın alınmış seviyeler bunun
        /// ardından ReplayPurchasedUpgradeLevels ile uygulanır.
        /// </summary>
        private void InitializeLevelObjects()
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpdateLevelObjects(new EntryUI { Definition = upgrades[i], UpgradeIndex = i }, 0);
            }
        }

        private void InitializeBaseValues()
        {
            InitializeStaminaBaseValue();
            InitializeMoneyBaseValue();
            InitializeQueueBaseValue();
            InitializeTruckUpgrade();
        }

        private void InitializeStaminaBaseValue()
        {
            if (PlayerMovement == null) return;

            var staminaUpgrade = FindDefinitionByName(UPGRADE_STAMINA);
            if (staminaUpgrade != null)
            {
                PlayerMovement.staminaRegenRate = staminaUpgrade.Definition.StaminaValue;
            }
        }

        private void InitializeMoneyBaseValue()
        {
            // NO-OP (upgrade turu 2026-07-20, economist onaylı): eskiden burada
            // Truck.rewardPerBox = moneyUpgrade.Definition.TruckValue (=15) yazılıyordu — Truck
            // zaten OnNetworkSpawn'da rewardPerBox'ı economySettings.rewardPerBox'tan (50, doğru
            // taban) kuruyor (Truck.cs:212). Bu satır rewardPerBox'ı 15'e çekip tabanın ALTINA
            // düşürüyordu (aktif zararlı — Money kartı teklifteyken bile spawn'da tetiklenirdi).
            // Money artık draft havuzundan kalıcı olarak çıkarıldı (disabledInDraft, bkz.
            // UpgradeDefinition.disabledInDraft / DraftPool.IsEligible) — reward'a hiç dokunma.
        }

        private void InitializeQueueBaseValue()
        {
            if (CustomerManager == null) return;

            var queueUpgrade = FindDefinitionByName(UPGRADE_QUEUE);
            if (queueUpgrade != null)
            {
                CustomerManager.maxQueueSize = queueUpgrade.Definition.starterValue;
            }
        }

        private void InitializeTruckUpgrade()
        {
            var truckEntry = FindDefinitionByName(UPGRADE_TRUCK);
            if (truckEntry == null) return;

            // Seviye 0: yalnız ilk hangar aktif, diğerleri kapalı.
            UpdateLevelObjects(truckEntry, 0);
            UpdateGarageDoorControllers(truckEntry, 0);
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _upgradeLevels.OnListChanged += HandleUpgradeLevelsChanged;
            _visualUpgradeLevels.OnListChanged += HandleVisualUpgradeLevelsChanged;
            _pendingUpgrades.OnListChanged += HandlePendingUpgradesChanged;
            _dailyOffer.OnListChanged += HandleDailyOfferChanged;
            _isPanelOpen.OnValueChanged += HandlePanelStateChanged;
            _rerollCountToday.OnValueChanged += HandleRerollCountChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _upgradeLevels.OnListChanged -= HandleUpgradeLevelsChanged;
            _visualUpgradeLevels.OnListChanged -= HandleVisualUpgradeLevelsChanged;
            _pendingUpgrades.OnListChanged -= HandlePendingUpgradesChanged;
            _dailyOffer.OnListChanged -= HandleDailyOfferChanged;
            _isPanelOpen.OnValueChanged -= HandlePanelStateChanged;
            _rerollCountToday.OnValueChanged -= HandleRerollCountChanged;
        }

        private void SubscribeToDayCycleEvents()
        {
            DayCycleManager.OnNewDay += HandleNewDay;
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            DayCycleManager.OnNewDay -= HandleNewDay;
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private void SubscribeToMoneySystem()
        {
            if (MoneySystem.Instance != null)
            {
                MoneySystem.Instance.OnMoneyChanged += HandleMoneyChanged;
            }
            else
            {
                // MoneySystem henüz spawn olmamış, gecikmeli olarak subscribe ol
                StartCoroutine(WaitForMoneySystemCoroutine());
            }
        }

        private void UnsubscribeFromMoneySystem()
        {
            if (MoneySystem.Instance != null)
            {
                MoneySystem.Instance.OnMoneyChanged -= HandleMoneyChanged;
            }
        }

        private System.Collections.IEnumerator WaitForMoneySystemCoroutine()
        {
            float timeout = 10f;
            while (MoneySystem.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (MoneySystem.Instance != null)
            {
                MoneySystem.Instance.OnMoneyChanged += HandleMoneyChanged;
                LogDebug("MoneySystem found — refreshing all upgrade buttons");
                RefreshAllUpgradeUI();
            }
            else
            {
                LogError("MoneySystem.Instance could not be found within timeout!");
            }
        }

        private void HandleMoneyChanged(int newMoney)
        {
            RefreshAllUpgradeUI();
            RefreshRerollUI();
        }

        private void HandleLocaleChanged(Locale newLocale)
        {
            LogDebug($"Locale changed to: {newLocale?.Identifier.Code ?? "null"}");
            RefreshAllUpgradeUI();
        }

        #endregion

        #region Network Event Handlers

        private void HandleUpgradeLevelsChanged(NetworkListEvent<int> changeEvent)
        {
            if (changeEvent.Type != NetworkListEvent<int>.EventType.Value) return;

            int upgradeIndex = changeEvent.Index;
            int newLevel = changeEvent.Value;

            ReplayUpgradeLevel(upgradeIndex, newLevel);

            RefreshAllUpgradeUI();
        }

        private void HandleVisualUpgradeLevelsChanged(NetworkListEvent<int> changeEvent)
        {
            RefreshAllUpgradeUI();
        }

        private void HandlePendingUpgradesChanged(NetworkListEvent<NetworkPendingUpgrade> changeEvent)
        {
            RefreshAllUpgradeUI();
        }

        private void HandlePanelStateChanged(bool previousValue, bool newValue)
        {
            panel.SetActive(newValue);

            if (newValue)
            {
                RebuildDraftEntries();
            }
        }

        private void HandleNewDay()
        {
            if (IsServer)
            {
                ActivatePendingUpgradesServerRpc();
                GenerateDailyOfferServer();
            }
        }

        private void HandleDailyOfferChanged(NetworkListEvent<int> _)
        {
            RebuildDraftEntries();
        }

        private void HandleRerollCountChanged(int previousValue, int newValue)
        {
            RefreshRerollUI();
        }

        #endregion

        #region Special Upgrade Handling

        private void HandleSpecialUpgrades(EntryUI entry, int newLevel)
        {
            if (entry.Definition.displayName != UPGRADE_TRUCK) return;

            UpdateGarageDoorControllers(entry, newLevel);

            if (TruckSpawner.Instance != null && IsServer)
            {
                TruckSpawner.Instance.SetTruckUpgradeLevel(newLevel);
            }
        }

        #endregion

        #region Level Objects Management

        private void UpdateLevelObjects(EntryUI entry, int currentLevel)
        {
            var levelObjects = entry.Definition.levelObjects;
            if (levelObjects == null) return;

            // null guard: artık TÜM upgrade'ler için çağrılıyor (bkz. InitializeLevelObjects),
            // atanmamış slotu olan bir tanım tüm kurulumu NRE ile düşürmesin.
            for (int i = 0; i < levelObjects.Length; i++)
            {
                if (levelObjects[i] == null) continue;
                levelObjects[i].SetActive(i <= currentLevel);
            }
        }

        private void UpdateGarageDoorControllers(EntryUI truckEntry, int currentLevel)
        {
            var controllers = truckEntry.Definition.garageDoorControllers;
            if (controllers == null || controllers.Length == 0) return;

            // Kilit artık `enabled` bayrağıyla değil GarageDoorController.isUnlocked ile yönetiliyor.
            // Gerekçe: `enabled=false` kapıyı yalnızca Update'i durdurarak "dondururdu" — sahne
            // default'u enabled=true olduğu için kilitleme yolu atlandığında TÜM kapılar açılıyordu
            // (bkz. InitializeTruckUpgrade / FindDefinitionByName notu). isUnlocked default'u false,
            // yani hata yönü fail-closed. Kontrolcüyü enabled bırakıyoruz ki kilitlenen bir kapının
            // kapanma animasyonu tamamlanabilsin.
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == null) continue;

                controllers[i].enabled = true;
                controllers[i].SetUnlocked(i <= currentLevel);
            }
        }

        #endregion

        #region Upgrade Effects

        private void ApplyUpgradeEffect(EntryUI entry, int level)
        {
            string effectId = entry.Definition.effectId;
            if (!string.IsNullOrEmpty(effectId))
            {
                PerkEffect.Apply(effectId, level, BuildPerkContext());
                return;
            }

            string upgradeName = entry.Definition.displayName;

            switch (upgradeName)
            {
                case UPGRADE_QUEUE:
                    ApplyQueueUpgrade(entry, level);
                    break;

                case UPGRADE_STAMINA:
                    ApplyStaminaUpgrade(entry, level);
                    break;

                case UPGRADE_MONEY:
                    ApplyMoneyUpgrade(entry, level);
                    break;

                case UPGRADE_QUEST_TIER:
                    ApplyQuestTierUpgrade(level);
                    break;
            }
        }

        private void ApplyQueueUpgrade(EntryUI entry, int level)
        {
            if (CustomerManager != null)
            {
                CustomerManager.maxQueueSize = entry.Definition.starterValue + level;
            }
        }

        private void ApplyStaminaUpgrade(EntryUI entry, int level)
        {
            if (PlayerMovement != null)
            {
                PlayerMovement.staminaRegenRate = entry.Definition.StaminaValue + (level * 0.5f);
            }
        }

        private void ApplyMoneyUpgrade(EntryUI entry, int level)
        {
            // NO-OP (upgrade turu 2026-07-20, economist onaylı): eskiden
            // Truck.rewardPerBox = TruckValue(15) + level*10 = 25/35/45 yazıyordu — taban 50'nin
            // ALTINDA, yani bu upgrade'i satın alan oyuncunun kutu başı gelirini DÜŞÜRÜYORDU.
            // Money artık draft havuzundan çıkarıldı (disabledInDraft); eski save'lerde level>0
            // kalmış olabileceğinden (ReplayPurchasedUpgradeLevels bu case'i hâlâ çağırabilir)
            // zararı kalıcı olarak kapatmak için gövde boşaltıldı — rewardPerBox'a asla dokunma.
        }

        private void ApplyQuestTierUpgrade(int level)
        {
            if (Quest.QuestManager.Instance != null)
            {
                Quest.QuestManager.Instance.SetQuestTier(level);
                LogDebug($"Quest tier set to {level}");
            }
        }

        /// <summary>
        /// PerkEffect.Apply çağrısı için mevcut manager referanslarından bir PerkContext kurar.
        /// Not: HandleUpgradeLevelsChanged tüm client'larda tetiklenir (mevcut mimari); bu yüzden
        /// PerkEffect metodları idempotent olmalı (level'dan mutlak değer hesaplar, += yapmaz).
        /// </summary>
        private PerkContext BuildPerkContext()
        {
            return new PerkContext
            {
                Truck = Truck,
                CustomerManager = CustomerManager,
                PlayerMovement = PlayerMovement,
                Economy = economySettings,
                DayCycle = DayCycleManager.Instance,
                Panel = this,
            };
        }

        #endregion

        #region Pending Upgrades

        [ServerRpc(RequireOwnership = false)]
        private void ActivatePendingUpgradesServerRpc()
        {
            if (DayCycleManager.Instance == null) return;

            int currentDay = DayCycleManager.Instance.currentDay;

            var upgradesToActivate = new List<NetworkPendingUpgrade>();
            var upgradesToKeep = new List<NetworkPendingUpgrade>();

            // Categorize pending upgrades
            for (int i = 0; i < _pendingUpgrades.Count; i++)
            {
                var pending = _pendingUpgrades[i];
                if (pending.dayPurchased < currentDay)
                {
                    upgradesToActivate.Add(pending);
                }
                else
                {
                    upgradesToKeep.Add(pending);
                }
            }

            // Apply upgrades
            foreach (var pendingUpgrade in upgradesToActivate)
            {
                int upgradeIndex = FindUpgradeIndex(pendingUpgrade.upgradeName.ToString());

                if (upgradeIndex >= 0 && upgradeIndex < _upgradeLevels.Count)
                {
                    _upgradeLevels[upgradeIndex] = pendingUpgrade.levelToBecomeActive;
                }
            }

            // Update pending list
            _pendingUpgrades.Clear();
            foreach (var upgrade in upgradesToKeep)
            {
                _pendingUpgrades.Add(upgrade);
            }

            RefreshUpgradePricesClientRpc();
        }

        private int FindUpgradeIndex(string upgradeName)
        {
            return upgrades.FindIndex(u => u.displayName == upgradeName);
        }

        #endregion

        #region Panel Toggle

        /// <summary>
        /// Paneli toggle eder
        /// </summary>
        public void TogglePanel()
        {
            if (GetCurrentHour() < PANEL_OPEN_HOUR) return;

            TogglePanelServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void TogglePanelServerRpc()
        {
            _isPanelOpen.Value = !_isPanelOpen.Value;
        }

        #endregion

        #region Draft Offer

        /// <summary>
        /// Server-only: o günkü draft teklifini üretir. Uygun (eligible) upgrade'lerden
        /// tier-kilidine ve max-seviyeye göre en fazla OFFER_COUNT tanesini seçip
        /// <see cref="_dailyOffer"/>'a yazar. RNG seed'i güne bağlı → deterministik/senkron.
        /// </summary>
        private void GenerateDailyOfferServer()
        {
            if (!IsServer) return;
            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;
            var eligibility = BuildEligibility(currentDay, out _);

            var rng = new System.Random(unchecked(currentDay * 73856093));
            var offer = DraftPool.SelectOffer(eligibility, DraftPool.OFFER_COUNT, rng, BuildExclusionGroups());

            _dailyOffer.Clear();
            foreach (var idx in offer) _dailyOffer.Add(idx);
            _rerollCountToday.Value = 0;

            ApplyPendingBulkBuyDiscount();
        }

        /// <summary>
        /// Toplu Alım (bulk_buy) perki: bir önceki gün işaretlendiyse, o günkü teklifteki
        /// rastgele 1 karta -%50 indirim uygular (server-only, tek kullanımlık).
        /// </summary>
        private void ApplyPendingBulkBuyDiscount()
        {
            if (_pendingBulkBuyDiscount && _dailyOffer.Count > 0)
            {
                int discountIndex = _dailyOffer[UnityEngine.Random.Range(0, _dailyOffer.Count)];
                _discountedUpgradeIndex.Value = discountIndex;
                _pendingBulkBuyDiscount = false;
            }
            else
            {
                _discountedUpgradeIndex.Value = -1;
            }
        }

        /// <summary>Toplu Alım (bulk_buy) etkisi: PerkEffect.cs tarafından çağrılır (server-only).</summary>
        public void MarkNextDraftDiscount()
        {
            if (!IsServer) return;
            _pendingBulkBuyDiscount = true;
        }

        /// <summary>
        /// Server-only: verilen elverişlilik listesine göre eligibility hesaplar.
        /// <see cref="GenerateDailyOfferServer"/> ve reroll akışı arasında paylaşılır.
        /// </summary>
        private List<bool> BuildEligibility(int currentDay, out PerkTier maxUnlocked)
        {
            maxUnlocked = DraftPool.MaxUnlockedTier(currentDay);
            bool questActive = _questSystemActive.Value;
            var exclusionGroups = BuildExclusionGroups();

            var eligibility = new List<bool>(upgrades.Count);
            for (int i = 0; i < upgrades.Count; i++)
            {
                var def = upgrades[i];
                int visLevel = GetVisualLevel(i);
                bool eligible = DraftPool.IsEligible(
                    i, def.tier, def.kind, def.requiresQuestSystem,
                    visLevel, def.maxLevel, maxUnlocked, questActive, def.disabledInDraft);

                // Kalıcı dışlama (a): grup arkadaşlarından biri zaten satın alındıysa
                // (level>0), bu perk bir daha hiçbir teklifte/reroll'da çıkmaz.
                if (eligible && IsBlockedByOwnedGroupMate(i, exclusionGroups))
                    eligible = false;

                eligibility.Add(eligible);
            }
            return eligibility;
        }

        /// <summary>
        /// index, kendi dışlama grubundaki başka bir perk zaten satın alınmış (level>0) olduğu
        /// için eleniyor mu? Simetrik: hangi taraf önce alınırsa alınsın diğeri hemen kilitlenir.
        /// </summary>
        private bool IsBlockedByOwnedGroupMate(int index, List<IReadOnlyList<int>> exclusionGroups)
        {
            foreach (var group in exclusionGroups)
            {
                if (!group.Contains(index)) continue;
                foreach (var other in group)
                {
                    if (other != index && GetVisualLevel(other) > 0) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// EXCLUSIVE_EFFECT_GROUPS'taki effectId'leri <see cref="upgrades"/> listesindeki index'lere
        /// çevirir (case/whitespace toleranslı eşleşme). DraftPool saf mantığına effectId string'i
        /// sızmaz — yalnızca index grupları geçilir. Bir grupta 2'den az index bulunursa
        /// (perk eksik/henüz eklenmemiş) DraftPool tarafında otomatik etkisiz kalır.
        /// </summary>
        private List<IReadOnlyList<int>> BuildExclusionGroups()
        {
            var groups = new List<IReadOnlyList<int>>();
            foreach (var effectIds in EXCLUSIVE_EFFECT_GROUPS)
            {
                var indices = new List<int>();
                foreach (var effectId in effectIds)
                {
                    for (int i = 0; i < upgrades.Count; i++)
                    {
                        if (string.Equals(upgrades[i].effectId?.Trim(), effectId, StringComparison.OrdinalIgnoreCase))
                        {
                            indices.Add(i);
                            break;
                        }
                    }
                }
                if (indices.Count >= 2) groups.Add(indices);
            }
            return groups;
        }

        /// <summary>
        /// Draft teklifi değişince panel içeriğini yeniden kurar: tüm liste yerine
        /// sadece <see cref="_dailyOffer"/>'daki (≤3) upgrade için kart kurar.
        /// `EntryUI.UpgradeIndex` gerçek `upgrades` index'i olduğundan satın alma/pending/
        /// effect zinciri değişmeden çalışır.
        /// </summary>
        private void RebuildDraftEntries()
        {
            ClearEntries();
            for (int k = 0; k < _dailyOffer.Count; k++)
            {
                int upgradeIndex = _dailyOffer[k];
                if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) continue;
                BuildSingleEntry(upgradeIndex);
            }
            RefreshAllUpgradeUI();
            RefreshRerollUI();
        }

        #endregion

        #region Reroll

        /// <summary>
        /// Client-side giriş noktası: reroll maliyetini kontrol edip sunucuya isteği yollar.
        /// UI tarafında iyimser doğrulama yapılır; asıl doğrulama sunucuda tekrarlanır.
        /// </summary>
        public void OnReroll()
        {
            int cost = RerollCurve.CostForReroll(_rerollCountToday.Value);
            if (MoneySystem.Instance == null || MoneySystem.Instance.CurrentMoney < cost) return;
            RerollServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RerollServerRpc()
        {
            // NOT: Reroll gate'i bilerek PurchaseUpgradeServerRpc ile simetrik tutuluyor — ikisi de
            // sadece para/geçerlilik kontrolü yapar, saat gate'i YOK. Panel OfficeTerminal.cs üzerinden
            // (client-local SetActive) açılıyor; _isPanelOpen NetworkVariable'ı hiç set edilmediği için
            // ona bağlanmak reroll'u daima öldürüyordu. Saat gate'i de yanlıştı: panel PANEL_OPEN_HOUR'dan
            // önce (ör. saat 7) açılabiliyor ama satın alma o saatte çalışırken reroll ölüyordu.
            // Gerçek server-authoritative panel-open state'i ayrı bir iş (OfficeTerminal↔UpgradePanel birleştirme).
            int cost = RerollCurve.CostForReroll(_rerollCountToday.Value);
            if (MoneySystem.Instance == null || MoneySystem.Instance.CurrentMoney < cost) return;

            MoneySystem.Instance.SpendMoney(cost);

            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;
            var eligibility = BuildEligibility(currentDay, out _);

            // reroll sayacını seed'e kat → farklı sonuç
            var rng = new System.Random(unchecked((currentDay * 73856093) ^ ((_rerollCountToday.Value + 1) * 19349663)));
            var offer = DraftPool.SelectOffer(eligibility, DraftPool.OFFER_COUNT, rng, BuildExclusionGroups());

            _dailyOffer.Clear();
            foreach (var idx in offer) _dailyOffer.Add(idx);
            _rerollCountToday.Value += 1;

            // bulk_buy indirimi rerolled tekliflere taşınmaz — günlük tek kullanımlıktır ve
            // yeni teklif eski indeksle tesadüfen çakışırsa indirim "sızmasın" diye burada iptal edilir.
            _discountedUpgradeIndex.Value = -1;
        }

        /// <summary>
        /// Reroll butonunun metin/interactable durumunu günceller. Prefab bağlantısı
        /// Task 8'e kadar eksik olabileceğinden tüm alanlar null-guard'lıdır.
        /// </summary>
        private void RefreshRerollUI()
        {
            if (rerollButton == null && rerollCostText == null) return;

            int cost = RerollCurve.CostForReroll(_rerollCountToday.Value);
            bool canAfford = MoneySystem.Instance != null && MoneySystem.Instance.CurrentMoney >= cost;

            if (rerollCostText != null)
            {
                rerollCostText.text = cost.ToString();
            }

            if (rerollButton != null)
            {
                rerollButton.interactable = canAfford;
            }
        }

        #endregion

        #region Purchase System

        private void OnBuy(int upgradeIndex)
        {
            if (!ValidatePurchase(upgradeIndex, out int finalCost)) return;

            PurchaseUpgradeServerRpc(upgradeIndex, finalCost);
        }

        private bool ValidatePurchase(int upgradeIndex, out int finalCost)
        {
            finalCost = 0;

            if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) return false;

            var upgrade = upgrades[upgradeIndex];
            int currentVisualLevel = GetVisualLevel(upgradeIndex);

            if (currentVisualLevel >= upgrade.maxLevel) return false;
            if (IsBlockedByOwnedGroupMate(upgradeIndex, BuildExclusionGroups())) return false;

            finalCost = CalculateFinalCost(upgradeIndex, upgrade, currentVisualLevel);

            return MoneySystem.Instance != null && MoneySystem.Instance.CurrentMoney >= finalCost;
        }

        [ServerRpc(RequireOwnership = false)]
        private void PurchaseUpgradeServerRpc(int upgradeIndex, int cost)
        {
            if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) return;

            var upgrade = upgrades[upgradeIndex];
            int currentVisualLevel = GetVisualLevel(upgradeIndex);

            if (currentVisualLevel >= upgrade.maxLevel) return;

            // Edge-case guard: iki dışlanan perk aynı teklifte (satın alınmadan önce) çıkabilir.
            // Biri az önce alındıysa, aynı anda ekranda kalan diğerinin satın alınması reddedilir
            // (server-authoritative) — kararı ("(a) Dışlama") ihlal etmesin.
            if (IsBlockedByOwnedGroupMate(upgradeIndex, BuildExclusionGroups())) return;

            // Server kendi maliyetini yeniden hesaplar (client'tan gelen cost sadece UI amaçlı geçilir).
            int serverCost = CalculateFinalCost(upgradeIndex, upgrade, currentVisualLevel);
            if (MoneySystem.Instance == null || MoneySystem.Instance.CurrentMoney < serverCost) return;

            // Process purchase
            MoneySystem.Instance.SpendMoney(serverCost);

            if (upgradeIndex < _visualUpgradeLevels.Count)
            {
                _visualUpgradeLevels[upgradeIndex] = currentVisualLevel + 1;
            }

            // Toplu Alım (bulk_buy) indirimi bir kez kullanılır — tüketildi.
            if (_discountedUpgradeIndex.Value == upgradeIndex)
            {
                _discountedUpgradeIndex.Value = -1;
            }

            // Add pending upgrade
            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;
            _pendingUpgrades.Add(new NetworkPendingUpgrade(upgrade.displayName, currentVisualLevel + 1, currentDay));

            RefreshUpgradePricesClientRpc();
        }

        #endregion

        #region Cost Calculation

        private int CalculateFinalCost(int upgradeIndex, UpgradeDefinition upgrade, int currentLevel)
        {
            float costMultiplier = GetCostMultiplier();
            int baseCost = upgrade.baseCost + currentLevel * upgrade.costStep;

            // Toplu Alım (bulk_buy) perki: sonraki taslakta işaretlenen 1 kart -%50.
            if (_discountedUpgradeIndex.Value == upgradeIndex)
            {
                baseCost = Mathf.RoundToInt(baseCost * 0.5f);
            }

            return Mathf.RoundToInt(baseCost * costMultiplier);
        }

        private float GetCostMultiplier()
        {
            float multiplier = 1f;

            // Apply event effect multiplier
            if (eventEffectManager != null)
            {
                multiplier *= eventEffectManager.GetUpgradeCostMultiplier();
            }

            // Apply difficulty-based cost multiplier
            if (DifficultyManager.Instance != null)
            {
                multiplier *= DifficultyManager.Instance.UpgradeCostMultiplier;
            }

            return multiplier;
        }

        #endregion

        #region UI Building

        private void ClearEntries()
        {
            foreach (Transform c in contentParent)
            {
                Destroy(c.gameObject);
            }
            _entries.Clear();
        }

        private void BuildSingleEntry(int index)
        {
            var def = upgrades[index];

            if (entryPrefab == null)
            {
                LogError("entryPrefab is null!");
                return;
            }

            var go = Instantiate(entryPrefab, contentParent);
            if (go == null)
            {
                LogError($"Failed to instantiate prefab for {def.displayName}");
                return;
            }

            var entry = new EntryUI
            {
                Definition = def,
                UpgradeIndex = index
            };

            if (!TrySetupEntryUI(go, entry, def))
            {
                Destroy(go);
                return;
            }

            // Button click event
            int upgradeIndex = index;
            entry.BuyButton.onClick.AddListener(() => OnBuy(upgradeIndex));

            _entries.Add(entry);
            UpdateEntryUI(entry);
        }

        private bool TrySetupEntryUI(GameObject go, EntryUI entry, UpgradeDefinition def)
        {
            // Name Text - now capture reference for dynamic localization
            entry.NameText = FindTextComponent(go, UI_NAME_TEXT);
            if (entry.NameText == null)
            {
                LogError($"NameText not found for {def.displayName}");
                return false;
            }
            entry.NameText.raycastTarget = false;
            // Set initial localized name
            entry.NameText.text = GetLocalizedUpgradeName(def);

            // Level Text
            entry.LevelText = FindTextComponent(go, UI_LEVEL_TEXT);
            if (entry.LevelText == null)
            {
                LogError($"LevelText not found for {def.displayName}");
                return false;
            }
            entry.LevelText.raycastTarget = false;

            // Cost Text
            entry.CostText = FindTextComponent(go, UI_COST_TEXT);
            if (entry.CostText == null)
            {
                LogError($"CostText not found for {def.displayName}");
                return false;
            }
            entry.CostText.raycastTarget = false;

            // Content Text
            entry.ContentText = FindTextComponent(go, UI_CONTENT_TEXT);
            if (entry.ContentText == null)
            {
                LogError($"ContentText not found for {def.displayName}");
                return false;
            }
            entry.ContentText.raycastTarget = false;

            // Buy Button
            var buyButtonTransform = FindChildRecursive(go.transform, UI_BUY_BUTTON);
            if (buyButtonTransform == null)
            {
                LogError($"BuyButton not found for {def.displayName}");
                return false;
            }

            entry.BuyButton = buyButtonTransform.GetComponent<Button>();
            if (entry.BuyButton == null)
            {
                LogError($"Button component not found for {def.displayName}");
                return false;
            }

            return true;
        }

        private bool TrySetTextComponent(GameObject go, string elementName, string text)
        {
            var transform = FindChildRecursive(go.transform, elementName);
            if (transform == null) return false;

            var tmpText = transform.GetComponent<TMP_Text>();
            if (tmpText == null) return false;

            tmpText.text = text;
            return true;
        }

        private TMP_Text FindTextComponent(GameObject go, string elementName)
        {
            var transform = FindChildRecursive(go.transform, elementName);
            return transform?.GetComponent<TMP_Text>();
        }

        #endregion

        #region UI Update

        [ClientRpc]
        private void RefreshUpgradePricesClientRpc()
        {
            RefreshAllUpgradeUI();
        }

        private void RefreshAllUpgradeUI()
        {
            foreach (var entry in _entries)
            {
                UpdateEntryUI(entry);
            }
        }

        private void UpdateEntryUI(EntryUI entry)
        {
            int currentVisualLevel = GetVisualLevel(entry.UpgradeIndex);

            // Update name text with localized value
            if (entry.NameText != null)
            {
                string displayName = GetLocalizedUpgradeName(entry.Definition);
                entry.NameText.text = displayName;
            }

            // Update level text with localized format
            string levelTemplate = LocalizationHelper.GetLocalizedString(LOC_KEY_LEVEL);
            try
            {
                entry.LevelText.text = string.Format(levelTemplate, currentVisualLevel);
            }
            catch
            {
                entry.LevelText.text = $"Level: {currentVisualLevel}";
            }

            // Update content text with localized value
            entry.ContentText.text = GetLocalizedUpgradeContent(entry.Definition);

            if (currentVisualLevel < entry.Definition.maxLevel)
            {
                UpdateEntryUIForPurchasable(entry, currentVisualLevel);
            }
            else
            {
                UpdateEntryUIForMaxLevel(entry);
            }
        }

        private string GetLocalizedUpgradeName(UpgradeDefinition def)
        {
            if (!string.IsNullOrEmpty(def.displayNameLocKey))
            {
                return LocalizationHelper.GetLocalizedString(def.displayNameLocKey);
            }
            return def.displayName;
        }

        private string GetLocalizedUpgradeContent(UpgradeDefinition def)
        {
            if (!string.IsNullOrEmpty(def.contentTextLocKey))
            {
                return LocalizationHelper.GetLocalizedString(def.contentTextLocKey);
            }
            return def.contentText;
        }

        private void UpdateEntryUIForPurchasable(EntryUI entry, int currentVisualLevel)
        {
            int baseCost = entry.Definition.baseCost + currentVisualLevel * entry.Definition.costStep;
            int finalCost = CalculateFinalCost(entry.UpgradeIndex, entry.Definition, currentVisualLevel);
            bool hasDiscount = finalCost < baseCost;

            string costTemplate = LocalizationHelper.GetLocalizedString(LOC_KEY_COST);
            string costText;
            try
            {
                costText = string.Format(costTemplate, finalCost);
            }
            catch
            {
                costText = $"Cost: {finalCost}";
            }

            // Show cost with discount if applicable
            if (hasDiscount)
            {
                entry.CostText.text = $"<color=green>{costText}</color> <color=red><s>{baseCost}</s></color>";
            }
            else
            {
                entry.CostText.text = costText;
            }

            // MoneySystem henüz hazır değilse butonu interactable bırak (iyimser yaklaşım)
            // Gerçek doğrulama OnBuy() → ValidatePurchase() → PurchaseUpgradeServerRpc() zincirinde yapılır
            if (MoneySystem.Instance != null)
            {
                entry.BuyButton.interactable = MoneySystem.Instance.CurrentMoney >= finalCost;
            }
            else
            {
                entry.BuyButton.interactable = true;
            }

            SetButtonText(entry.BuyButton, LocalizationHelper.GetLocalizedString(LOC_KEY_BUY));
        }

        private void UpdateEntryUIForMaxLevel(EntryUI entry)
        {
            entry.CostText.text = LocalizationHelper.GetLocalizedString(LOC_KEY_MAX);
            entry.BuyButton.interactable = false;
            SetButtonText(entry.BuyButton, LocalizationHelper.GetLocalizedString(LOC_KEY_MAX));
        }

        private void SetButtonText(Button button, string text)
        {
            var buttonText = FindButtonText(button);
            if (buttonText != null)
            {
                buttonText.text = text;
            }
        }

        private TMP_Text FindButtonText(Button button)
        {
            var textTransform = button.transform.Find(UI_BUTTON_TEXT);
            if (textTransform != null)
            {
                return textTransform.GetComponent<TMP_Text>();
            }

            return button.GetComponentInChildren<TMP_Text>();
        }

        #endregion

        #region Utility Methods

        private int GetVisualLevel(int upgradeIndex)
        {
            return upgradeIndex < _visualUpgradeLevels.Count ? _visualUpgradeLevels[upgradeIndex] : 0;
        }

        private int GetCurrentHour()
        {
            return DayCycleManager.Instance?.CurrentHour ?? 0;
        }

        private EntryUI FindEntryByName(string displayName)
        {
            return _entries.FirstOrDefault(e => e.Definition.displayName == displayName);
        }

        /// <summary>
        /// İsimle upgrade arar — <see cref="_entries"/> (o günün ≤3 kartlık draft teklifi) yerine
        /// TAM <c>upgrades</c> listesinde. Başlangıç kurulumu (Initialize*) draft'ın o an ne
        /// teklif ettiğinden bağımsız olmalı: <see cref="FindEntryByName"/> kullanıldığında kart
        /// teklifte yoksa null dönüp kurulum sessizce atlanıyordu. Garaj kapılarında bu, hiçbir
        /// kontrolcünün devre dışı bırakılmaması (sahne default'u enabled=true) ve satın
        /// alınmamış TÜM hangar kapılarının openTime'da açılması demekti.
        /// Aynı gerekçe <see cref="ReplayUpgradeLevel"/> için de geçerli (bkz. oradaki not).
        /// </summary>
        private EntryUI FindDefinitionByName(string displayName)
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i].displayName == displayName)
                {
                    return new EntryUI { Definition = upgrades[i], UpgradeIndex = i };
                }
            }
            return null;
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Test Truck Level 0")]
        private void DebugTestTruckLevel0()
        {
            var truckEntry = FindEntryByName(UPGRADE_TRUCK);
            if (truckEntry != null)
            {
                UpdateGarageDoorControllers(truckEntry, 0);
            }
        }

        [ContextMenu("Test Truck Level 1")]
        private void DebugTestTruckLevel1()
        {
            var truckEntry = FindEntryByName(UPGRADE_TRUCK);
            if (truckEntry != null)
            {
                UpdateGarageDoorControllers(truckEntry, 1);
            }
        }

        [ContextMenu("Test Truck Level 2")]
        private void DebugTestTruckLevel2()
        {
            var truckEntry = FindEntryByName(UPGRADE_TRUCK);
            if (truckEntry != null)
            {
                UpdateGarageDoorControllers(truckEntry, 2);
            }
        }

        [ContextMenu("Toggle Panel")]
        private void DebugTogglePanel()
        {
            TogglePanel();
        }

        [ContextMenu("Refresh UI")]
        private void DebugRefreshUI()
        {
            RefreshAllUpgradeUI();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === UPGRADE PANEL STATE ===");
            Debug.Log($"Is Panel Open: {_isPanelOpen.Value}");
            Debug.Log($"Total Upgrades: {upgrades.Count}");
            Debug.Log($"Built Entries: {_entries.Count}");
            Debug.Log($"Pending Upgrades: {_pendingUpgrades.Count}");
            Debug.Log($"Cost Multiplier: {GetCostMultiplier()}");

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                int actualLevel = i < _upgradeLevels.Count ? _upgradeLevels[i] : 0;
                int visualLevel = GetVisualLevel(i);

                Debug.Log($"  [{i}] {entry.Definition.displayName}: " +
                         $"Actual={actualLevel}, Visual={visualLevel}, Max={entry.Definition.maxLevel}");
            }
        }

        [ContextMenu("Debug: Print Pending Upgrades")]
        private void DebugPrintPendingUpgrades()
        {
            Debug.Log($"{LOG_PREFIX} === PENDING UPGRADES ===");

            for (int i = 0; i < _pendingUpgrades.Count; i++)
            {
                var pending = _pendingUpgrades[i];
                Debug.Log($"  [{i}] {pending.upgradeName}: Level {pending.levelToBecomeActive}, Day {pending.dayPurchased}");
            }
        }
#endif

        #endregion
    }
}