using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace NewCss
{
    public class AutoLightController : NetworkBehaviour
    {
        [Header("Light Groups")]
        [Tooltip("Işıklar max 0.15 intensity'ye ulaşacak")]
        public List<Light> lightGroupA = new List<Light>();

        [Tooltip("Işıklar max 0.3 intensity'ye ulaşacak")]
        public List<Light> lightGroupB = new List<Light>();

        [Header("Time Settings")]
        [Tooltip("Bu saati geçince ışıklar yanacak (örn: 12 = öğlen 12:00'den sonra yanacak)")]
        public int lightTriggerHour = 12;

        [Header("Intensity Settings")]
        [Tooltip("Grup A için maksimum intensity")]
        public float maxIntensityGroupA = 0.15f;

        [Tooltip("Grup B için maksimum intensity")]
        public float maxIntensityGroupB = 0.3f;

        [Tooltip("Işıkların açılıp kapanma hızı (saniye)")]
        public float transitionSpeed = 2f;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // Network variable for light state
        private NetworkVariable<bool> networkLightsOn = new NetworkVariable<bool>(false);

        // Internal state
        private bool lightsOn = false;
        private float currentTransitionA = 0f;
        private float currentTransitionB = 0f;

        // Başlangıç intensity değerlerini sakla
        private Dictionary<Light, float> originalIntensitiesA = new Dictionary<Light, float>();
        private Dictionary<Light, float> originalIntensitiesB = new Dictionary<Light, float>();

        void Start()
        {
            // Kapalı durumdaki intensity'leri 0 olarak kaydet
            // Çünkü kapalı = tamamen sönük olmalı
            foreach (var light in lightGroupA)
            {
                if (light != null)
                {
                    originalIntensitiesA[light] = 0f;
                    light.intensity = 0f; // Hemen 0 yap
                }
            }

            foreach (var light in lightGroupB)
            {
                if (light != null)
                {
                    originalIntensitiesB[light] = 0f;
                    light.intensity = 0f; // Hemen 0 yap
                }
            }

            // Başlangıçta ışıkları kapat
            SetLightsImmediate(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Network variable değişikliklerini dinle
            networkLightsOn.OnValueChanged += OnLightsStateChanged;

            // Mevcut durumu uygula
            lightsOn = networkLightsOn.Value;
        }

        public override void OnNetworkDespawn()
        {
            networkLightsOn.OnValueChanged -= OnLightsStateChanged;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (DayCycleManager.Instance == null) return;

            // Sadece server saati kontrol eder ve karar verir
            if (IsServer)
            {
                CheckTimeAndUpdateLights();
            }

            // Tüm client'lar (server dahil) smooth geçişi uygular
            ApplySmoothTransition();
        }

        private void CheckTimeAndUpdateLights()
        {
            int currentHour = DayCycleManager.Instance.CurrentHour;
            bool shouldLightsBeOn = ShouldLightsBeOn(currentHour);

            // Durum değiştiyse network variable'ı güncelle
            if (shouldLightsBeOn != networkLightsOn.Value)
            {
                networkLightsOn.Value = shouldLightsBeOn;

                if (showDebugLogs)
                {
                    Debug.Log($"[AutoLightController] Hour: {currentHour}:00 - Lights turning {(shouldLightsBeOn ? "ON" : "OFF")} (Trigger: {lightTriggerHour}:00)");
                }
            }
        }

        private bool ShouldLightsBeOn(int hour)
        {
            // Belirlenen saati geçince ışıklar yanar
            // Örnek: lightTriggerHour=12
            // Saat 11:00 → Kapalı
            // Saat 12:00 ve sonrası → Açık
            return hour >= lightTriggerHour;
        }

        private void OnLightsStateChanged(bool previousValue, bool newValue)
        {
            lightsOn = newValue;

            if (showDebugLogs)
            {
                Debug.Log($"[AutoLightController] Lights state changed to: {(newValue ? "ON" : "OFF")}");
            }
        }

        private void ApplySmoothTransition()
        {
            float targetA = lightsOn ? 1f : 0f;
            float targetB = lightsOn ? 1f : 0f;

            // Smooth geçiş
            currentTransitionA = Mathf.MoveTowards(currentTransitionA, targetA, Time.deltaTime / transitionSpeed);
            currentTransitionB = Mathf.MoveTowards(currentTransitionB, targetB, Time.deltaTime / transitionSpeed);

            // Grup A'yı güncelle
            foreach (var light in lightGroupA)
            {
                if (light != null)
                {
                    float baseIntensity = originalIntensitiesA.ContainsKey(light) ? originalIntensitiesA[light] : 0f;
                    light.intensity = Mathf.Lerp(baseIntensity, maxIntensityGroupA, currentTransitionA);
                }
            }

            // Grup B'yi güncelle
            foreach (var light in lightGroupB)
            {
                if (light != null)
                {
                    float baseIntensity = originalIntensitiesB.ContainsKey(light) ? originalIntensitiesB[light] : 0f;
                    light.intensity = Mathf.Lerp(baseIntensity, maxIntensityGroupB, currentTransitionB);
                }
            }
        }

        private void SetLightsImmediate(bool on)
        {
            currentTransitionA = on ? 1f : 0f;
            currentTransitionB = on ? 1f : 0f;

            foreach (var light in lightGroupA)
            {
                if (light != null)
                {
                    float baseIntensity = originalIntensitiesA.ContainsKey(light) ? originalIntensitiesA[light] : 0f;
                    light.intensity = on ? maxIntensityGroupA : baseIntensity;
                }
            }

            foreach (var light in lightGroupB)
            {
                if (light != null)
                {
                    float baseIntensity = originalIntensitiesB.ContainsKey(light) ? originalIntensitiesB[light] : 0f;
                    light.intensity = on ? maxIntensityGroupB : baseIntensity;
                }
            }
        }

        // Debug metodları
        [ContextMenu("Test - Turn Lights ON")]
        public void TestLightsOn()
        {
            if (IsServer)
            {
                networkLightsOn.Value = true;
                Debug.Log("Test: Lights turned ON");
            }
        }

        [ContextMenu("Test - Turn Lights OFF")]
        public void TestLightsOff()
        {
            if (IsServer)
            {
                networkLightsOn.Value = false;
                Debug.Log("Test: Lights turned OFF");
            }
        }

        [ContextMenu("Print Current State")]
        public void PrintCurrentState()
        {
            if (DayCycleManager.Instance != null)
            {
                int hour = DayCycleManager.Instance.CurrentHour;
                bool shouldBeOn = ShouldLightsBeOn(hour);
                Debug.Log($"Current Hour: {hour}:00\n" +
                         $"Trigger Hour: {lightTriggerHour}:00\n" +
                         $"Lights Should Be: {(shouldBeOn ? "ON" : "OFF")} (hour >= {lightTriggerHour})\n" +
                         $"Lights Currently: {(lightsOn ? "ON" : "OFF")}\n" +
                         $"Transition A: {currentTransitionA:F2}\n" +
                         $"Transition B: {currentTransitionB:F2}");
            }
        }
    }
}