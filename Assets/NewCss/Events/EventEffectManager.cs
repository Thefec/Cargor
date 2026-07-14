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

        // List of active events
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
            "CUSTOMER SUPPORT"
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
        }

        [System.Serializable]
        public struct EventTruckValues
        {
            public float rewardPerBox;
            public float exitDelay;
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

            if (currentActiveEvent.Value != -1)
            {
                ApplyEventEffectLocally(eventNames[currentActiveEvent.Value]);
            }
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
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1.3f, // 30% more customers per day
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
                customerWaitTimeMultiplier = 0.7f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["RELAXED DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1.3f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 0.7f, // 30% fewer customers
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["SLOW LOGISTICS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1.5f,
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
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 0.7f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["HEAVY BOXES"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 0.8f,
                playerSprintSpeedMultiplier = 0.8f,
                staminaRegenRateMultiplier = 1f,
                dailyCustomerMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["GOLDEN BOX DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1.3f,
                exitDelayMultiplier = 0.8f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1.2f,
                playerSprintSpeedMultiplier = 1.2f,
                staminaRegenRateMultiplier = 0.8f,
                dailyCustomerMultiplier = 1.2f, // 20% more customers
                isGoldenBoxDay = true,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
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
                upgradeCostMultiplier = 0.8f
            };

            eventMultipliers["FATIGUE PROBLEM"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 0.7f,
                staminaRegenRateMultiplier = 0.6f,
                dailyCustomerMultiplier = 0.8f, // 20% fewer customers
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

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

            // SURPRISE AUDIT: çarpanlar nötr; çift-ceza GetPenaltyMultiplier() ile isim-tabanlı uygulanır.
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
                upgradeCostMultiplier = 1f
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

            // CUSTOMER SUPPORT: çarpanlar nötr; telefon çalma sıklığı çarpanı PhoneCallManager
            // tarafından IsEventActive("CUSTOMER SUPPORT") ile okunur (reaktif telefon sistemi).
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
        }

        private void OnNewDayHandler()
        {
            if (!IsServer) return;

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

            // FESTIVAL DAY: gün başı tek seferlik rastgele para bonusu (server-only).
            if (currentActiveEvent.Value != -1 &&
                eventNames[currentActiveEvent.Value] == "FESTIVAL DAY")
            {
                ApplyFestivalBonus();
            }

            NotifyUpgradePanelRefreshClientRpc();
        }

        /// <summary>
        /// FESTIVAL DAY etkinliğinde gün başında bir kez rastgele para bonusu verir.
        /// Yalnızca server çağırır (MoneySystem server-authoritative).
        /// </summary>
        private void ApplyFestivalBonus()
        {
            if (!IsServer || MoneySystem.Instance == null) return;

            int min = _economySettings != null ? _economySettings.festivalBonusMin : 100;
            int max = _economySettings != null ? _economySettings.festivalBonusMax : 300;
            int bonus = Random.Range(min, max + 1); // üst sınır dahil

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
            return eventNames[currentActiveEvent.Value] == "SURPRISE AUDIT" ? 2f : 1f;
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

            EventMultipliers multipliers = eventMultipliers[eventName];
            SaveCurrentValuesAndApplyMultipliers(multipliers, eventName);
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
            Truck[] trucks = FindObjectsOfType<Truck>();
            foreach (var truck in trucks)
            {
                EventTruckValues currentValues = new EventTruckValues
                {
                    rewardPerBox = truck.rewardPerBox,
                    exitDelay = truck.exitDelay
                };
                eventStartTruckValues[truck] = currentValues;

                truck.rewardPerBox = (int)(currentValues.rewardPerBox * multipliers.rewardPerBoxMultiplier);
                truck.exitDelay = currentValues.exitDelay * multipliers.exitDelayMultiplier;

                if (IsServer && multipliers.isVIPServiceDay && Random.Range(0f, 1f) < 0.1f)
                {
                    truck.rewardPerBox = (int)(truck.rewardPerBox * 1.1f);
                }
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

            EventMultipliers multipliers = eventMultipliers[currentEventName];

            if (newObject.TryGetComponent<Truck>(out Truck truck))
            {
                EventTruckValues currentValues = new EventTruckValues
                {
                    rewardPerBox = truck.rewardPerBox,
                    exitDelay = truck.exitDelay
                };
                eventStartTruckValues[truck] = currentValues;

                truck.rewardPerBox = (int)(currentValues.rewardPerBox * multipliers.rewardPerBoxMultiplier);
                truck.exitDelay = currentValues.exitDelay * multipliers.exitDelayMultiplier;

                if (IsServer && multipliers.isVIPServiceDay && Random.Range(0f, 1f) < 0.1f)
                {
                    truck.rewardPerBox = (int)(truck.rewardPerBox * 1.1f);
                }
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

    }
}