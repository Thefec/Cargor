using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace NewCss
{
    /// <summary>
    /// PlateUp! tarzı iç mekan ışık kontrolcüsü.
    /// İç mekan ışıkları her zaman yanar (7/24).
    /// Akşam saatlerinde ek sıcak mood ışıkları devreye girer.
    /// NOT: Directional Light (güneş) ayrı DayLightController tarafından yönetilir.
    /// </summary>
    public class AutoLightController : NetworkBehaviour
    {
        [Header("Indoor Lights - Always On")]
        [Tooltip("Ana iç mekan ışıkları (spot/point) — her zaman yanar")]
        public List<Light> indoorLights = new List<Light>();

        [Tooltip("İç mekan ışıkları için intensity")]
        public float indoorIntensity = 60f;

        [Header("Ambient/Fill Lights")]
        [Tooltip("Dolgu ışıkları — her zaman yanar, daha düşük intensity")]
        public List<Light> fillLights = new List<Light>();

        [Tooltip("Dolgu ışıkları için intensity")]
        public float fillIntensity = 20f;

        [Header("Evening Mood Lights")]
        [Tooltip("Akşam olunca ek olarak yanan sıcak ışıklar (opsiyonel)")]
        public List<Light> eveningLights = new List<Light>();

        [Tooltip("Akşam ışıkları intensity")]
        public float eveningIntensity = 30f;

        [Tooltip("Akşam ışıkları bu saatten sonra yanar")]
        public int eveningTriggerHour = 14;

        [Header("Transition")]
        [Tooltip("Akşam ışık geçiş hızı (saniye)")]
        public float transitionSpeed = 3f;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // Network variable for evening state
        private NetworkVariable<bool> networkEveningOn = new NetworkVariable<bool>(false);

        // Internal state
        private bool eveningOn = false;
        private float currentEveningTransition = 0f;

        void Start()
        {
            // İç mekan ışıklarını hemen aç — PlateUp! tarzı, her zaman yanar
            foreach (var light in indoorLights)
            {
                if (light != null)
                {
                    light.intensity = indoorIntensity;
                    light.enabled = true;
                }
            }

            // Fill ışıklarını aç
            foreach (var light in fillLights)
            {
                if (light != null)
                {
                    light.intensity = fillIntensity;
                    light.enabled = true;
                }
            }

            // Akşam ışıklarını başlangıçta kapat (saat gelince açılır)
            foreach (var light in eveningLights)
            {
                if (light != null)
                {
                    light.intensity = 0f;
                    light.enabled = true;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            networkEveningOn.OnValueChanged += OnEveningStateChanged;
            eveningOn = networkEveningOn.Value;
        }

        public override void OnNetworkDespawn()
        {
            networkEveningOn.OnValueChanged -= OnEveningStateChanged;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (DayCycleManager.Instance == null) return;

            // Server saati kontrol eder
            if (IsServer)
            {
                CheckEveningState();
            }

            // Tüm client'lar akşam ışık geçişini uygular
            ApplyEveningTransition();
        }

        private void CheckEveningState()
        {
            int currentHour = DayCycleManager.Instance.CurrentHour;
            bool shouldEveningBeOn = currentHour >= eveningTriggerHour;

            if (shouldEveningBeOn != networkEveningOn.Value)
            {
                networkEveningOn.Value = shouldEveningBeOn;

                if (showDebugLogs)
                {
                    Debug.Log($"[AutoLightController] Hour: {currentHour}:00 - Evening lights {(shouldEveningBeOn ? "ON" : "OFF")}");
                }
            }
        }

        private void ApplyEveningTransition()
        {
            float eveningTarget = eveningOn ? 1f : 0f;
            currentEveningTransition = Mathf.MoveTowards(currentEveningTransition, eveningTarget,
                Time.deltaTime / transitionSpeed);

            foreach (var light in eveningLights)
            {
                if (light != null)
                {
                    light.intensity = Mathf.Lerp(0f, eveningIntensity, currentEveningTransition);
                }
            }
        }

        private void OnEveningStateChanged(bool previousValue, bool newValue)
        {
            eveningOn = newValue;

            if (showDebugLogs)
            {
                Debug.Log($"[AutoLightController] Evening lights: {(newValue ? "ON" : "OFF")}");
            }
        }

        // ─── Debug Metodları ───

        [ContextMenu("Test - Force Evening ON")]
        public void TestEveningOn()
        {
            if (IsServer)
            {
                networkEveningOn.Value = true;
                Debug.Log("Test: Evening lights forced ON");
            }
        }

        [ContextMenu("Test - Force Evening OFF")]
        public void TestEveningOff()
        {
            if (IsServer)
            {
                networkEveningOn.Value = false;
                Debug.Log("Test: Evening lights forced OFF");
            }
        }

        [ContextMenu("Print Current State")]
        public void PrintCurrentState()
        {
            if (DayCycleManager.Instance != null)
            {
                int hour = DayCycleManager.Instance.CurrentHour;
                Debug.Log($"Current Hour: {hour}:00\n" +
                         $"Evening Lights: {(eveningOn ? "ON" : "OFF")}\n" +
                         $"Evening Trigger: {eveningTriggerHour}:00\n" +
                         $"Evening Transition: {currentEveningTransition:F2}\n" +
                         $"Indoor Lights Count: {indoorLights.Count}\n" +
                         $"Fill Lights Count: {fillLights.Count}\n" +
                         $"Evening Lights Count: {eveningLights.Count}");
            }
        }
    }
}