using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections; // FixedString64Bytes için gerekli

namespace NewCss
{
    public class EventEffectManager : NetworkBehaviour
    {
        [Header("Manager References")] public CustomerManager customerManager;
        public UpgradePanel upgradePanel; // For Opportunity Day (future)
        public StaminaBar staminaBar;
        public TruckSpawner truckSpawner;

        [Header("Event Calendar Reference")] public EventCalendarUI eventCalendar;

        // Network Variables for synchronization
        private NetworkVariable<int> currentActiveEvent = new NetworkVariable<int>(-1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // List of active events (using index for network sync)
        private List<string> eventNames = new List<string>
        {
            "INTENSIVE DAY",
            "DELIVERY BONUS",
            "ANGRY CUSTOMERS",
            "RELAXED DAY",
            "SLOW LOGISTICS",
            "EXPRESS CARGO",
            "HEAVY BOXES",
            "GOLDEN BOX DAY",
            "OPPORTUNITY DAY",
            "FATIGUE PROBLEM",
            "VIP SERVICE"
        };

        // To save current values at event start
        private Dictionary<Truck, EventTruckValues> eventStartTruckValues = new Dictionary<Truck, EventTruckValues>();

        private Dictionary<CustomerAI, CustomerWaitTimeValues> eventStartCustomerWaitTimes =
            new Dictionary<CustomerAI, CustomerWaitTimeValues>();

        private float eventStartPlayerMoveSpeed;
        private float eventStartPlayerSprintSpeed;
        private float eventStartStaminaRegenRate;
        private float eventStartMaxSpawnInterval;
        private float eventStartMinSpawnInterval;

        // To store event multipliers
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
            public float spawnIntervalMultiplier;
            public bool isGoldenBoxDay;
            public bool isVIPServiceDay;
            public float upgradeCostMultiplier;    // <— yenisi
            
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

            // Initialize event multipliers
            InitializeEventMultipliers();

            // Subscribe to day change event
            DayCycleManager.OnNewDay += OnNewDayHandler;

            // Subscribe to network variable changes
            currentActiveEvent.OnValueChanged += OnActiveEventChanged;

            // Apply current event if joining mid-game
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
        }

        private void InitializeEventMultipliers()
        {
            eventMultipliers["INTENSIVE DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 0.5f, // Spawn intervals reduced by 50%
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["DELIVERY BONUS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1.2f, // 20% bonus to reward per box
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["ANGRY CUSTOMERS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 0.7f, // 30% reduction in wait time
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["RELAXED DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1.3f, // 30% increase in wait time
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["SLOW LOGISTICS"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1.5f, // 50% increase in exit delay
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["EXPRESS CARGO"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 0.7f, // 30% reduction in exit delay
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["HEAVY BOXES"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 0.8f, // 20% reduction in move speed
                playerSprintSpeedMultiplier = 0.8f, // 20% reduction in sprint speed
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["GOLDEN BOX DAY"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1.3f, // 30% bonus to reward per box
                exitDelayMultiplier = 0.8f, // 20% reduction in exit delay
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1.2f, // 20% increase in move speed
                playerSprintSpeedMultiplier = 1.2f, // 20% increase in sprint speed
                staminaRegenRateMultiplier = 0.8f, // 20% reduction in stamina regen
                spawnIntervalMultiplier = 1f,
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
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 0.8f    // <— %20 indirim
            };

            eventMultipliers["FATIGUE PROBLEM"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f,
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 0.7f, // 30% reduction in sprint speed
                staminaRegenRateMultiplier = 0.6f, // 40% reduction in stamina regen
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = false,
                upgradeCostMultiplier = 1f
            };

            eventMultipliers["VIP SERVICE"] = new EventMultipliers
            {
                rewardPerBoxMultiplier = 1f, // Base multiplier, special logic applied per truck
                exitDelayMultiplier = 1f,
                customerWaitTimeMultiplier = 1f,
                playerMoveSpeedMultiplier = 1f,
                playerSprintSpeedMultiplier = 1f,
                staminaRegenRateMultiplier = 1f,
                spawnIntervalMultiplier = 1f,
                isGoldenBoxDay = false,
                isVIPServiceDay = true,
                upgradeCostMultiplier = 1f
            };
        }

        private void OnNewDayHandler()
        {
            if (!IsServer) return; // Only server handles day changes

            // Check today's event
            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;
            var todaysEvent = eventCalendar.GetEventForDay(currentDay);

            if (todaysEvent != null)
            {
                int eventIndex = eventNames.IndexOf(todaysEvent.name);
                if (eventIndex != -1)
                {
                    currentActiveEvent.Value = eventIndex;
                }
                else
                {
                    currentActiveEvent.Value = -1; // No event
                }
            }
            else
            {
                currentActiveEvent.Value = -1; // No event
            }

            // Notify upgrade panel to refresh prices (for all clients)
            NotifyUpgradePanelRefreshClientRpc();
        }

        [ClientRpc]
        private void NotifyUpgradePanelRefreshClientRpc()
        {
           
        }

        private void OnActiveEventChanged(int previousValue, int newValue)
        {
            // Remove previous effects
            RemoveAllEventEffects();

            // Apply new event if valid
            if (newValue != -1 && newValue < eventNames.Count)
            {
                ApplyEventEffectLocally(eventNames[newValue]);
            }

            // Refresh upgrade panel prices when event changes
            
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
            // Apply spawn interval changes
            if (customerManager != null)
            {
                eventStartMaxSpawnInterval = customerManager.maxSpawnInterval;
                eventStartMinSpawnInterval = customerManager.minSpawnInterval;

                customerManager.maxSpawnInterval = eventStartMaxSpawnInterval * multipliers.spawnIntervalMultiplier;
                customerManager.minSpawnInterval = eventStartMinSpawnInterval * multipliers.spawnIntervalMultiplier;
            }

            // Apply player movement changes
            PlayerMovement playerController = FindObjectOfType<PlayerMovement>();
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

                // Apply reward multiplier
                truck.rewardPerBox = (int)(currentValues.rewardPerBox * multipliers.rewardPerBoxMultiplier);

                // Apply exit delay multiplier
                truck.exitDelay = currentValues.exitDelay * multipliers.exitDelayMultiplier;

                // Special VIP Service logic
                if (multipliers.isVIPServiceDay && Random.Range(0f, 1f) < 0.1f)
                {
                    truck.rewardPerBox = (int)(truck.rewardPerBox * 1.1f); // Additional 10% bonus
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
            // Restore spawn intervals
            if (customerManager != null && eventStartMaxSpawnInterval > 0)
            {
                customerManager.maxSpawnInterval = eventStartMaxSpawnInterval;
                customerManager.minSpawnInterval = eventStartMinSpawnInterval;
            }

            // Restore player movement
            PlayerMovement playerController = FindObjectOfType<PlayerMovement>();
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

        // Apply event effects to newly spawned objects
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

                // Special VIP Service logic
                if (multipliers.isVIPServiceDay && Random.Range(0f, 1f) < 0.1f)
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

        // Public methods for other systems to check event status
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

        // Server RPC for manual event testing (admin only)
        [ServerRpc(RequireOwnership = false)]
        public void TestEventServerRpc(int eventIndex)
        {
            if (eventIndex >= 0 && eventIndex < eventNames.Count)
            {
                currentActiveEvent.Value = eventIndex;
            }
            else
            {
                currentActiveEvent.Value = -1;
            }
        }

        // For debug: manually test event
        [System.Obsolete("Used for debugging only")]
        public void TestEvent(string eventName)
        {
            if (!IsServer) return;

            int eventIndex = eventNames.IndexOf(eventName);
            if (eventIndex != -1)
            {
                TestEventServerRpc(eventIndex);
            }
        }
    }
}