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

        #endregion

        #region Network Variables

        private NetworkList<int> _upgradeLevels;
        private NetworkList<int> _visualUpgradeLevels;
        private NetworkList<NetworkPendingUpgrade> _pendingUpgrades;
        private readonly NetworkVariable<bool> _isPanelOpen = new(false);

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
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                InitializeUpgradeLevels();
            }

            SubscribeToNetworkEvents();
            InitializePanel();
            BuildEntries();
            InitializeLevelObjects();
            InitializeBaseValues();
            SubscribeToDayCycleEvents();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            UnsubscribeFromDayCycleEvents();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeNetworkLists()
        {
            _upgradeLevels = new NetworkList<int>();
            _visualUpgradeLevels = new NetworkList<int>();
            _pendingUpgrades = new NetworkList<NetworkPendingUpgrade>();
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

        private void InitializeLevelObjects()
        {
            foreach (var entry in _entries)
            {
                if (entry.Definition.levelObjects.Length > 0)
                {
                    entry.Definition.levelObjects[0].SetActive(true);
                }
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

            var staminaUpgrade = FindEntryByName(UPGRADE_STAMINA);
            if (staminaUpgrade != null)
            {
                PlayerMovement.staminaRegenRate = staminaUpgrade.Definition.StaminaValue;
            }
        }

        private void InitializeMoneyBaseValue()
        {
            if (Truck == null) return;

            var moneyUpgrade = FindEntryByName(UPGRADE_MONEY);
            if (moneyUpgrade != null)
            {
                Truck.rewardPerBox = moneyUpgrade.Definition.TruckValue;
            }
        }

        private void InitializeQueueBaseValue()
        {
            if (CustomerManager == null) return;

            var queueUpgrade = FindEntryByName(UPGRADE_QUEUE);
            if (queueUpgrade != null)
            {
                CustomerManager.maxQueueSize = queueUpgrade.Definition.starterValue;
            }
        }

        private void InitializeTruckUpgrade()
        {
            var truckEntry = FindEntryByName(UPGRADE_TRUCK);
            if (truckEntry == null) return;

            // İlk hangar aktif
            if (truckEntry.Definition.levelObjects.Length > 0)
            {
                truckEntry.Definition.levelObjects[0].SetActive(true);
            }

            // Diğer hangarlar kapalı
            for (int i = 1; i < truckEntry.Definition.levelObjects.Length; i++)
            {
                truckEntry.Definition.levelObjects[i].SetActive(false);
            }

            UpdateGarageDoorControllers(truckEntry, 0);
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _upgradeLevels.OnListChanged += HandleUpgradeLevelsChanged;
            _visualUpgradeLevels.OnListChanged += HandleVisualUpgradeLevelsChanged;
            _pendingUpgrades.OnListChanged += HandlePendingUpgradesChanged;
            _isPanelOpen.OnValueChanged += HandlePanelStateChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _upgradeLevels.OnListChanged -= HandleUpgradeLevelsChanged;
            _visualUpgradeLevels.OnListChanged -= HandleVisualUpgradeLevelsChanged;
            _pendingUpgrades.OnListChanged -= HandlePendingUpgradesChanged;
            _isPanelOpen.OnValueChanged -= HandlePanelStateChanged;
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

            if (upgradeIndex >= _entries.Count) return;

            var entry = _entries[upgradeIndex];
            ApplyUpgradeEffect(entry, newLevel);
            UpdateLevelObjects(entry, newLevel);
            HandleSpecialUpgrades(entry, newLevel);

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
                RefreshAllUpgradeUI();
            }
        }

        private void HandleNewDay()
        {
            if (IsServer)
            {
                ActivatePendingUpgradesServerRpc();
            }
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
            for (int i = 0; i < entry.Definition.levelObjects.Length; i++)
            {
                entry.Definition.levelObjects[i].SetActive(i <= currentLevel);
            }
        }

        private void UpdateGarageDoorControllers(EntryUI truckEntry, int currentLevel)
        {
            var controllers = truckEntry.Definition.garageDoorControllers;
            if (controllers == null || controllers.Length == 0) return;

            // Tüm kapıları deaktif et
            foreach (var controller in controllers)
            {
                if (controller != null)
                {
                    controller.enabled = false;
                }
            }

            // Mevcut seviye ve altındakileri aktif et
            for (int i = 0; i <= currentLevel && i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                {
                    controllers[i].enabled = true;
                }
            }
        }

        #endregion

        #region Upgrade Effects

        private void ApplyUpgradeEffect(EntryUI entry, int level)
        {
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
            if (Truck != null)
            {
                Truck.rewardPerBox = entry.Definition.TruckValue + (level * 10);
            }
        }

        private void ApplyQuestTierUpgrade(int level)
        {
            if (Quest.QuestManager.Instance != null)
            {
                Quest.QuestManager.Instance.SetQuestTier(level);
                LogDebug($"Quest tier set to {level}");
            }
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

            finalCost = CalculateFinalCost(upgrade, currentVisualLevel);

            return MoneySystem.Instance.CurrentMoney >= finalCost;
        }

        [ServerRpc(RequireOwnership = false)]
        private void PurchaseUpgradeServerRpc(int upgradeIndex, int cost)
        {
            if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) return;

            var upgrade = upgrades[upgradeIndex];
            int currentVisualLevel = GetVisualLevel(upgradeIndex);

            if (currentVisualLevel >= upgrade.maxLevel) return;
            if (MoneySystem.Instance.CurrentMoney < cost) return;

            // Process purchase
            MoneySystem.Instance.SpendMoney(cost);

            if (upgradeIndex < _visualUpgradeLevels.Count)
            {
                _visualUpgradeLevels[upgradeIndex] = currentVisualLevel + 1;
            }

            // Add pending upgrade
            int currentDay = DayCycleManager.Instance.currentDay;
            _pendingUpgrades.Add(new NetworkPendingUpgrade(upgrade.displayName, currentVisualLevel + 1, currentDay));

            RefreshUpgradePricesClientRpc();
        }

        #endregion

        #region Cost Calculation

        private int CalculateFinalCost(UpgradeDefinition upgrade, int currentLevel)
        {
            float costMultiplier = GetCostMultiplier();
            int baseCost = upgrade.baseCost + currentLevel * upgrade.costStep;
            return Mathf.RoundToInt(baseCost * costMultiplier);
        }

        private float GetCostMultiplier()
        {
            return eventEffectManager != null ? eventEffectManager.GetUpgradeCostMultiplier() : 1f;
        }

        #endregion

        #region UI Building

        private void BuildEntries()
        {
            ClearEntries();

            LogDebug($"Building {upgrades.Count} upgrade entries");

            for (int i = 0; i < upgrades.Count; i++)
            {
                try
                {
                    BuildSingleEntry(i);
                }
                catch (Exception ex)
                {
                    LogError($"Error building entry for {upgrades[i].displayName}: {ex.Message}");
                }
            }

            LogDebug($"Successfully built {_entries.Count} entries");
        }

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
            // Set initial localized name
            entry.NameText.text = GetLocalizedUpgradeName(def);

            // Level Text
            entry.LevelText = FindTextComponent(go, UI_LEVEL_TEXT);
            if (entry.LevelText == null)
            {
                LogError($"LevelText not found for {def.displayName}");
                return false;
            }

            // Cost Text
            entry.CostText = FindTextComponent(go, UI_COST_TEXT);
            if (entry.CostText == null)
            {
                LogError($"CostText not found for {def.displayName}");
                return false;
            }

            // Content Text
            entry.ContentText = FindTextComponent(go, UI_CONTENT_TEXT);
            if (entry.ContentText == null)
            {
                LogError($"ContentText not found for {def.displayName}");
                return false;
            }

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
            float costMultiplier = GetCostMultiplier();
            int baseCost = entry.Definition.baseCost + currentVisualLevel * entry.Definition.costStep;
            int finalCost = Mathf.RoundToInt(baseCost * costMultiplier);

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
            if (costMultiplier < 1f)
            {
                entry.CostText.text = $"<color=green>{costText}</color> <color=red><s>{baseCost}</s></color>";
            }
            else
            {
                entry.CostText.text = costText;
            }

            entry.BuyButton.interactable = MoneySystem.Instance.CurrentMoney >= finalCost;
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