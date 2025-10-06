using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;

namespace NewCss
{
    public class EventEffectManager : NetworkBehaviour
    {
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

        // Store original values
        private Dictionary<Truck, EventTruckValues> eventStartTruckValues = new Dictionary<Truck, EventTruckValues>();
        private Dictionary<CustomerAI, CustomerWaitTimeValues> eventStartCustomerWaitTimes = new Dictionary<CustomerAI, CustomerWaitTimeValues>();

        private float eventStartPlayerMoveSpeed;
        private float eventStartPlayerSprintSpeed;
        private float eventStartStaminaRegenRate;

        // NEW: Store customer manager values
        private int eventStartBaseCustomersPerDay;
        private int eventStartCustomerIncreasePerDay;

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
                dailyCustomerMultiplier = 1.5f, // 50% more customers per day
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

            NotifyUpgradePanelRefreshClientRpc();
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
            // Apply daily customer count changes (NEW)
            if (customerManager != null)
            {
                // Store original values if not already stored
                if (eventStartBaseCustomersPerDay == 0)
                {
                    eventStartBaseCustomersPerDay = customerManager.baseCustomersPerDay;
                    eventStartCustomerIncreasePerDay = customerManager.customerIncreasePerDay;
                }

                // Apply multiplier to base customers
                customerManager.baseCustomersPerDay = Mathf.RoundToInt(eventStartBaseCustomersPerDay * multipliers.dailyCustomerMultiplier);
                customerManager.customerIncreasePerDay = Mathf.RoundToInt(eventStartCustomerIncreasePerDay * multipliers.dailyCustomerMultiplier);

                Debug.Log($"Event '{eventName}': Daily customers adjusted to {customerManager.baseCustomersPerDay} (multiplier: {multipliers.dailyCustomerMultiplier})");
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

                truck.rewardPerBox = (int)(currentValues.rewardPerBox * multipliers.rewardPerBoxMultiplier);
                truck.exitDelay = currentValues.exitDelay * multipliers.exitDelayMultiplier;

                if (multipliers.isVIPServiceDay && Random.Range(0f, 1f) < 0.1f)
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
            // Restore customer manager values (NEW)
            if (customerManager != null && eventStartBaseCustomersPerDay > 0)
            {
                customerManager.baseCustomersPerDay = eventStartBaseCustomersPerDay;
                customerManager.customerIncreasePerDay = eventStartCustomerIncreasePerDay;
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