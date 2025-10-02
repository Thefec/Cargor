using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using System;
using Unity.Collections;

namespace NewCss
{
    [System.Serializable]
    public class UpgradeDefinition
    {
        public string displayName; // "Storage Capacity"
        public int starterValue = 0;
        public int StaminaValue = 1;
        public int TruckValue = 20;
        public int maxLevel;
        public int WaitTime;
        public int baseCost; // e.g. 100
        public int costStep; // e.g. 100
        public string contentText;
        public GameObject[] levelObjects; // Scene objects like Level0..Level3

        [Header("Garage Door Controllers (for Truck upgrade only)")]
        [Tooltip("Her level için hangi garaj kapılarının açılacağını belirler")]
        public GarageDoorController[] garageDoorControllers; // Her level için garaj kapıları
    }

    [System.Serializable]
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

        public override bool Equals(object obj)
        {
            return obj is NetworkPendingUpgrade other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(upgradeName.GetHashCode(), levelToBecomeActive, dayPurchased);
        }

        public static bool operator ==(NetworkPendingUpgrade left, NetworkPendingUpgrade right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NetworkPendingUpgrade left, NetworkPendingUpgrade right)
        {
            return !left.Equals(right);
        }
    }

    public class UpgradePanel : NetworkBehaviour
    {
        [Header("UI References")] [SerializeField]
        private GameObject panel;

        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject entryPrefab;

        [Header("Upgrade Definitions")] [SerializeField]
        private List<UpgradeDefinition> upgrades = new();

        [Header("Manager References")] [SerializeField]
        private CustomerManager CustomerManager;

        [SerializeField] private PlayerMovement PlayerMovement;
        [SerializeField] private Truck Truck;
        [SerializeField] private CustomerAI CustomerAI;
        [SerializeField] private EventEffectManager eventEffectManager; // For Opportunity Day

        // Network Variables for synchronization
        private NetworkList<int> upgradeLevels;
        private NetworkList<int> visualUpgradeLevels; // Current visual levels (purchased but not yet active)
        private NetworkList<NetworkPendingUpgrade> pendingUpgrades;
        private NetworkVariable<bool> isPanelOpen = new NetworkVariable<bool>(false);

        private class EntryUI
        {
            public UpgradeDefinition def;
            public int upgradeIndex; // Index in the upgrades list
            public TMP_Text levelText, costText, contentText;
            public Button buyButton;
        }

        private List<EntryUI> _entries = new();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Initialize NetworkLists
            if (IsServer)
            {
                // Initialize upgrade levels to 0
                for (int i = 0; i < upgrades.Count; i++)
                {
                    upgradeLevels.Add(0);
                    visualUpgradeLevels.Add(0);
                }
            }

            // Subscribe to network variable changes
            upgradeLevels.OnListChanged += OnUpgradeLevelsChanged;
            visualUpgradeLevels.OnListChanged += OnVisualUpgradeLevelsChanged;
            pendingUpgrades.OnListChanged += OnPendingUpgradesChanged;
            isPanelOpen.OnValueChanged += OnPanelStateChanged;

            panel.SetActive(false);
            BuildEntries();

            // Show Level 0 objects initially if hidden
            foreach (var e in _entries)
            {
                if (e.def.levelObjects.Length > 0)
                {
                    e.def.levelObjects[0].SetActive(true);
                }
            }

            // Initialize base values
            InitializeBaseValues();

            // Subscribe to new day event
            DayCycleManager.OnNewDay += OnNewDay;
        }

        void Awake()
        {
            // Initialize NetworkLists in Awake
            upgradeLevels = new NetworkList<int>();
            visualUpgradeLevels = new NetworkList<int>();
            pendingUpgrades = new NetworkList<NetworkPendingUpgrade>();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // Unsubscribe from network events
            if (upgradeLevels != null)
                upgradeLevels.OnListChanged -= OnUpgradeLevelsChanged;
            if (visualUpgradeLevels != null)
                visualUpgradeLevels.OnListChanged -= OnVisualUpgradeLevelsChanged;
            if (pendingUpgrades != null)
                pendingUpgrades.OnListChanged -= OnPendingUpgradesChanged;

            isPanelOpen.OnValueChanged -= OnPanelStateChanged;

            // Unsubscribe to prevent memory leaks
            DayCycleManager.OnNewDay -= OnNewDay;
        }

        private void OnUpgradeLevelsChanged(NetworkListEvent<int> changeEvent)
        {
            // Only apply upgrade effects when actual levels change (not visual levels)
            if (changeEvent.Type == NetworkListEvent<int>.EventType.Value)
            {
                int upgradeIndex = changeEvent.Index;
                int newLevel = changeEvent.Value;

                if (upgradeIndex < _entries.Count)
                {
                    var entry = _entries[upgradeIndex];
                    ApplyUpgradeEffect(entry, newLevel);

                    // Update level objects - show all levels up to current level
                    UpdateLevelObjects(entry, newLevel);

                    // Special handling for Truck upgrade - update garage doors
                    if (entry.def.displayName == "Truck")
                    {
                        UpdateGarageDoorControllers(entry, newLevel);
                    }
                }
            }

            RefreshAllUpgradeUI();
        }

        private void OnVisualUpgradeLevelsChanged(NetworkListEvent<int> changeEvent)
        {
            // Update UI when visual levels change (for purchased but not yet active upgrades)
            RefreshAllUpgradeUI();
        }

        private void OnPendingUpgradesChanged(NetworkListEvent<NetworkPendingUpgrade> changeEvent)
        {
            // Handle pending upgrades changes if needed
            RefreshAllUpgradeUI();
        }

        private void OnPanelStateChanged(bool previousValue, bool newValue)
        {
            panel.SetActive(newValue);

            if (newValue)
            {
                RefreshAllUpgradeUI();
            }
        }

        private void UpdateLevelObjects(EntryUI entry, int currentLevel)
        {
            // Show all level objects up to and including current level
            for (int i = 0; i < entry.def.levelObjects.Length; i++)
            {
                entry.def.levelObjects[i].SetActive(i <= currentLevel);
            }
        }

        private void InitializeBaseValues()
        {
            if (PlayerMovement != null)
            {
                var staminaUpgrade = _entries.FirstOrDefault(e => e.def.displayName == "Stamina");
                if (staminaUpgrade != null)
                    PlayerMovement.staminaRegenRate = staminaUpgrade.def.StaminaValue;
            }

            if (Truck != null)
            {
                var moneyUpgrade = _entries.FirstOrDefault(e => e.def.displayName == "Money");
                if (moneyUpgrade != null)
                    Truck.rewardPerBox = moneyUpgrade.def.TruckValue;
            }

            if (CustomerManager != null)
            {
                var queueUpgrade = _entries.FirstOrDefault(e => e.def.displayName == "Queue");
                if (queueUpgrade != null)
                    CustomerManager.maxQueueSize = queueUpgrade.def.starterValue;
            }

            var TruckUpgrade = _entries.FirstOrDefault(e => e.def.displayName == "Truck");
            if (TruckUpgrade != null)
            {
                InitializeTruckUpgrade(TruckUpgrade);
            }
        }

        private void InitializeTruckUpgrade(EntryUI truckEntry)
        {
            // İlk hangar aktif olsun (Level 0 objesi)
            if (truckEntry.def.levelObjects.Length > 0)
            {
                truckEntry.def.levelObjects[0].SetActive(true);
            }

            // Diğer hangarlar kapalı olsun
            for (int i = 1; i < truckEntry.def.levelObjects.Length; i++)
            {
                truckEntry.def.levelObjects[i].SetActive(false);
            }

            // Garaj kapılarını kontrol et - sadece Level 0 için aktif
            UpdateGarageDoorControllers(truckEntry, 0);
        }

        private void UpdateGarageDoorControllers(EntryUI truckEntry, int currentLevel)
        {
            if (truckEntry.def.garageDoorControllers == null || truckEntry.def.garageDoorControllers.Length == 0)
            {
                return;
            }

            // Tüm garaj kapılarını önce deaktif et
            for (int i = 0; i < truckEntry.def.garageDoorControllers.Length; i++)
            {
                if (truckEntry.def.garageDoorControllers[i] != null)
                {
                    // Garaj kapısını deaktif et (çalışmayı durdur)
                    truckEntry.def.garageDoorControllers[i].enabled = false;
                }
            }

            // Sadece mevcut seviye ve altındaki garaj kapılarını aktif et
            for (int i = 0; i <= currentLevel && i < truckEntry.def.garageDoorControllers.Length; i++)
            {
                if (truckEntry.def.garageDoorControllers[i] != null)
                {
                    // Garaj kapısını aktif et
                    truckEntry.def.garageDoorControllers[i].enabled = true;
                }
            }
        }

        private void OnNewDay()
        {
            if (IsServer)
            {
                ActivatePendingUpgradesServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ActivatePendingUpgradesServerRpc()
        {
            if (DayCycleManager.Instance == null) return;

            int currentDay = DayCycleManager.Instance.currentDay;

            // Get upgrades that should be activated today (purchased previous day)
            var upgradesToActivate = new List<NetworkPendingUpgrade>();
            var upgradesToKeep = new List<NetworkPendingUpgrade>();

            for (int i = 0; i < pendingUpgrades.Count; i++)
            {
                if (pendingUpgrades[i].dayPurchased < currentDay)
                {
                    upgradesToActivate.Add(pendingUpgrades[i]);
                }
                else
                {
                    upgradesToKeep.Add(pendingUpgrades[i]);
                }
            }

            // Apply upgrades - update actual levels to match visual levels
            foreach (var pendingUpgrade in upgradesToActivate)
            {
                int upgradeIndex = upgrades.FindIndex(u => u.displayName == pendingUpgrade.upgradeName.ToString());
                if (upgradeIndex >= 0 && upgradeIndex < upgradeLevels.Count)
                {
                    upgradeLevels[upgradeIndex] = pendingUpgrade.levelToBecomeActive;
                }
            }

            // Update pending upgrades list
            pendingUpgrades.Clear();
            foreach (var upgrade in upgradesToKeep)
            {
                pendingUpgrades.Add(upgrade);
            }

            RefreshUpgradePricesClientRpc();
        }

        [ClientRpc]
        private void RefreshUpgradePricesClientRpc()
        {
            RefreshAllUpgradeUI();
        }

        private void ApplyUpgradeEffect(EntryUI entry, int level)
        {
            if (entry.def.displayName == "Queue" && CustomerManager != null)
            {
                CustomerManager.maxQueueSize = entry.def.starterValue + level;
            }
            else if (entry.def.displayName == "Stamina" && PlayerMovement != null)
            {
                PlayerMovement.staminaRegenRate = entry.def.StaminaValue + (level * 0.5f);
            }
            else if (entry.def.displayName == "Money" && Truck != null)
            {
                Truck.rewardPerBox = entry.def.TruckValue + (level * 10);
            }
        }

        public void TogglePanel()
        {
            if (DayCycleManager.Instance.CurrentHour >= 10)
            {
                TogglePanelServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void TogglePanelServerRpc()
        {
            isPanelOpen.Value = !isPanelOpen.Value;
        }

        private void BuildEntries()
        {
            foreach (Transform c in contentParent) Destroy(c.gameObject);
            _entries.Clear();

            Debug.Log($"Total upgrades to build: {upgrades.Count}");

            for (int i = 0; i < upgrades.Count; i++)
            {
                var def = upgrades[i];
                Debug.Log($"Building entry for: {def.displayName}");

                try
                {
                    // Prefab'ı kontrol et
                    if (entryPrefab == null)
                    {
                        Debug.LogError("entryPrefab is null!");
                        continue;
                    }

                    var go = Instantiate(entryPrefab, contentParent);
                    if (go == null)
                    {
                        Debug.LogError($"Failed to instantiate prefab for {def.displayName}");
                        continue;
                    }

                    Debug.Log($"Prefab instantiated for: {def.displayName}");

                    var e = new EntryUI { def = def, upgradeIndex = i };

                    // Prefab'ın tüm çocuklarını listele (debug için)
                    Debug.Log($"Prefab children for {def.displayName}:");
                    foreach (Transform child in go.transform)
                    {
                        Debug.Log($"  Child: {child.name}");
                    }

                    // Name Text - daha güvenli bulma
                    var nameTextTransform = FindChildRecursive(go.transform, "NameText");
                    if (nameTextTransform == null)
                    {
                        Debug.LogError($"NameText not found in prefab for {def.displayName}");
                        LogAllChildren(go.transform, "Available children:");
                        Destroy(go);
                        continue;
                    }

                    var nameTextComponent = nameTextTransform.GetComponent<TMP_Text>();
                    if (nameTextComponent == null)
                    {
                        Debug.LogError($"TMP_Text component not found on NameText for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    nameTextComponent.text = def.displayName;

                    // Level Text
                    var levelTextTransform = FindChildRecursive(go.transform, "LevelText");
                    if (levelTextTransform == null)
                    {
                        Debug.LogError($"LevelText not found in prefab for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    e.levelText = levelTextTransform.GetComponent<TMP_Text>();
                    if (e.levelText == null)
                    {
                        Debug.LogError($"TMP_Text component not found on LevelText for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    // Cost Text
                    var costTextTransform = FindChildRecursive(go.transform, "CostText");
                    if (costTextTransform == null)
                    {
                        Debug.LogError($"CostText not found in prefab for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    e.costText = costTextTransform.GetComponent<TMP_Text>();
                    if (e.costText == null)
                    {
                        Debug.LogError($"TMP_Text component not found on CostText for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    // Content Text
                    var contentTextTransform = FindChildRecursive(go.transform, "ContentText");
                    if (contentTextTransform == null)
                    {
                        Debug.LogError($"ContentText not found in prefab for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    e.contentText = contentTextTransform.GetComponent<TMP_Text>();
                    if (e.contentText == null)
                    {
                        Debug.LogError($"TMP_Text component not found on ContentText for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    // Buy Button
                    var buyButtonTransform = FindChildRecursive(go.transform, "BuyButton");
                    if (buyButtonTransform == null)
                    {
                        Debug.LogError($"BuyButton not found in prefab for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    e.buyButton = buyButtonTransform.GetComponent<Button>();
                    if (e.buyButton == null)
                    {
                        Debug.LogError($"Button component not found on BuyButton for {def.displayName}");
                        Destroy(go);
                        continue;
                    }

                    // Button Text - alternatif yollar dene
                    Transform buttonTextTransform = null;

                    // Önce "Text" isimli çocuk ara
                    buttonTextTransform = FindChildRecursive(buyButtonTransform, "Text");

                    // Bulamazsa "Text (TMP)" ara
                    if (buttonTextTransform == null)
                        buttonTextTransform = FindChildRecursive(buyButtonTransform, "Text (TMP)");

                    // Hala bulamazsa button'ın ilk TMP_Text component'ını ara
                    if (buttonTextTransform == null)
                    {
                        var tmpText = buyButtonTransform.GetComponentInChildren<TMP_Text>();
                        if (tmpText != null)
                            buttonTextTransform = tmpText.transform;
                    }

                    if (buttonTextTransform == null)
                    {
                        Debug.LogError($"Button Text not found in BuyButton for {def.displayName}");
                        LogAllChildren(buyButtonTransform, "BuyButton children:");
                        Destroy(go);
                        continue;
                    }

                    // Button click event - capture the index for the lambda
                    int upgradeIndex = i;
                    e.buyButton.onClick.AddListener(() => OnBuy(upgradeIndex));

                    _entries.Add(e);
                    Debug.Log($"Successfully added entry for: {def.displayName}");

                    UpdateEntryUI(e);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Error building entry for {def.displayName}: {ex.Message}");
                    Debug.LogError($"Stack trace: {ex.StackTrace}");
                }
            }

            Debug.Log($"Total entries built: {_entries.Count}");
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            // Önce direkt çocukları kontrol et
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;
            }

            // Sonra recursive olarak alt çocukları kontrol et
            foreach (Transform child in parent)
            {
                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void LogAllChildren(Transform parent, string prefix = "")
        {
            Debug.Log($"{prefix} {parent.name}");
            foreach (Transform child in parent)
            {
                LogAllChildren(child, prefix + "  ");
            }
        }

        private void OnBuy(int upgradeIndex)
        {
            if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) return;

            var upgrade = upgrades[upgradeIndex];
            int currentVisualLevel = visualUpgradeLevels.Count > upgradeIndex ? visualUpgradeLevels[upgradeIndex] : 0;

            if (currentVisualLevel >= upgrade.maxLevel) return;

            // Apply Opportunity Day multiplier
            float costMultiplier = eventEffectManager != null ? eventEffectManager.GetUpgradeCostMultiplier() : 1f;
            int baseCost = upgrade.baseCost + currentVisualLevel * upgrade.costStep;
            int finalCost = Mathf.RoundToInt(baseCost * costMultiplier);

            if (MoneySystem.Instance.CurrentMoney < finalCost) return;

            // Send purchase request to server
            PurchaseUpgradeServerRpc(upgradeIndex, finalCost);
        }

        [ServerRpc(RequireOwnership = false)]
        private void PurchaseUpgradeServerRpc(int upgradeIndex, int cost)
        {
            if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) return;

            var upgrade = upgrades[upgradeIndex];
            int currentVisualLevel = visualUpgradeLevels.Count > upgradeIndex ? visualUpgradeLevels[upgradeIndex] : 0;

            if (currentVisualLevel >= upgrade.maxLevel) return;
            if (MoneySystem.Instance.CurrentMoney < cost) return;

            // Spend money
            MoneySystem.Instance.SpendMoney(cost);

            // Increase visual level immediately (this shows in UI)
            if (upgradeIndex < visualUpgradeLevels.Count)
            {
                visualUpgradeLevels[upgradeIndex] = currentVisualLevel + 1;
            }

            // Add to pending upgrades (will be activated next day)
            int currentDay = DayCycleManager.Instance.currentDay;
            pendingUpgrades.Add(new NetworkPendingUpgrade(upgrade.displayName, currentVisualLevel + 1, currentDay));

            // Refresh UI for all clients
            RefreshUpgradePricesClientRpc();
        }

        private void RefreshAllUpgradeUI()
        {
            foreach (var entry in _entries)
            {
                UpdateEntryUI(entry);
            }
        }

        private void UpdateEntryUI(EntryUI e)
        {
            // Get current visual level (purchased level) from network variable
            int currentVisualLevel = e.upgradeIndex < visualUpgradeLevels.Count ? visualUpgradeLevels[e.upgradeIndex] : 0;

            // Show current visual level
            e.levelText.text = $"Level: {currentVisualLevel}";
            e.contentText.text = e.def.contentText;

            if (currentVisualLevel < e.def.maxLevel)
            {
                // Apply Opportunity Day multiplier - always get fresh multiplier
                float costMultiplier = eventEffectManager != null ? eventEffectManager.GetUpgradeCostMultiplier() : 1f;
                int baseCost = e.def.baseCost + currentVisualLevel * e.def.costStep;
                int finalCost = Mathf.RoundToInt(baseCost * costMultiplier);

                // Show discounted cost with colors if multiplier < 1
                if (costMultiplier < 1f)
                {
                    e.costText.text = $"<color=green>Cost: {finalCost}</color> <color=red><s>{baseCost}</s></color>";
                }
                else
                {
                    e.costText.text = $"Cost: {finalCost}";
                }

                e.buyButton.interactable = MoneySystem.Instance.CurrentMoney >= finalCost;

                var buttonText = e.buyButton.transform.Find("Text")?.GetComponent<TMP_Text>();
                if (buttonText == null)
                    buttonText = e.buyButton.GetComponentInChildren<TMP_Text>();

                if (buttonText != null)
                    buttonText.text = "Buy";
            }
            else
            {
                e.costText.text = "Max";
                e.buyButton.interactable = false;

                var buttonText = e.buyButton.transform.Find("Text")?.GetComponent<TMP_Text>();
                if (buttonText == null)
                    buttonText = e.buyButton.GetComponentInChildren<TMP_Text>();

                if (buttonText != null)
                    buttonText.text = "Max";
            }
        }

        // Test metodları
        [ContextMenu("Test Truck Level 0")]
        public void TestTruckLevel0()
        {
            var truckEntry = _entries.FirstOrDefault(e => e.def.displayName == "Truck");
            if (truckEntry != null)
            {
                UpdateGarageDoorControllers(truckEntry, 0);
            }
        }

        [ContextMenu("Test Truck Level 1")]
        public void TestTruckLevel1()
        {
            var truckEntry = _entries.FirstOrDefault(e => e.def.displayName == "Truck");
            if (truckEntry != null)
            {
                UpdateGarageDoorControllers(truckEntry, 1);
            }
        }

        [ContextMenu("Test Truck Level 2")]
        public void TestTruckLevel2()
        {
            var truckEntry = _entries.FirstOrDefault(e => e.def.displayName == "Truck");
            if (truckEntry != null)
            {
                UpdateGarageDoorControllers(truckEntry, 2);
            }
        }
    }
}