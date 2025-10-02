using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace NewCss
{
    [System.Serializable]
    public class LightGroup
    {
        public string groupName = "Light Group";
        [Tooltip("Bu gruptaki ���klar")]
        public List<Light> spotlights = new List<Light>();
        [Tooltip("Bu grubun maksimum intensity de�eri")]
        public float maxIntensity = 2f;
    }

    public class TimeBasedLightController : NetworkBehaviour
    {
        [Header("Light Settings")]
        [Tooltip("I��klar bu saatten sonra a��lmaya ba�lar")]
        public int activationHour = 12;

        [Tooltip("I��klar�n tam a��lma s�resi (saniye)")]
        public float transitionDuration = 5f;

        [Header("Light Groups")]
        [Tooltip("Farkl� intensity de�erlerine sahip ���k gruplar�")]
        public List<LightGroup> lightGroups = new List<LightGroup>();

        [Header("Intensity Settings")]
        [Tooltip("Ba�lang�� intensity (kapal� durum)")]
        public float minIntensity = 0f;

        private bool isTransitioning = false;
        private float transitionProgress = 0f;
        private bool lightsActivated = false;

        void Start()
        {
            // Ba�lang��ta t�m spotlightlar� kapat
            SetAllLightsIntensity(minIntensity);
        }

        void Update()
        {
            if (DayCycleManager.Instance == null) return;

            float currentTime = DayCycleManager.Instance.CurrentTime;
            int currentHour = Mathf.FloorToInt(currentTime);

            // I��klar hen�z a��lmad�ysa ve belirlenen saati ge�tiyse
            if (!lightsActivated && currentHour >= activationHour)
            {
                isTransitioning = true;
                lightsActivated = true;
            }

            // Smooth transition
            if (isTransitioning)
            {
                transitionProgress += Time.deltaTime / transitionDuration;

                if (transitionProgress >= 1f)
                {
                    transitionProgress = 1f;
                    isTransitioning = false;
                }

                // Smooth interpolation - her grup i�in ayr� max intensity
                float easedProgress = Mathf.SmoothStep(0f, 1f, transitionProgress);

                foreach (var group in lightGroups)
                {
                    float currentIntensity = Mathf.Lerp(minIntensity, group.maxIntensity, easedProgress);
                    SetGroupLightsIntensity(group, currentIntensity);
                }
            }
        }

        void SetAllLightsIntensity(float intensity)
        {
            foreach (var group in lightGroups)
            {
                SetGroupLightsIntensity(group, intensity);
            }
        }

        void SetGroupLightsIntensity(LightGroup group, float intensity)
        {
            foreach (Light spotlight in group.spotlights)
            {
                if (spotlight != null)
                {
                    spotlight.intensity = intensity;
                }
            }
        }

        // G�n s�f�rland���nda �a�r�lacak
        void OnEnable()
        {
            DayCycleManager.OnNewDay += ResetLights;
        }

        void OnDisable()
        {
            DayCycleManager.OnNewDay -= ResetLights;
        }

        void ResetLights()
        {
            lightsActivated = false;
            isTransitioning = false;
            transitionProgress = 0f;
            SetAllLightsIntensity(minIntensity);
        }

        // Inspector'da test etmek i�in
        [ContextMenu("Test Light Activation")]
        void TestActivation()
        {
            isTransitioning = true;
            lightsActivated = true;
            transitionProgress = 0f;
        }

        [ContextMenu("Reset Lights")]
        void TestReset()
        {
            ResetLights();
        }

        [ContextMenu("Show Group Info")]
        void ShowGroupInfo()
        {
            foreach (var group in lightGroups)
            {
                Debug.Log($"Group: {group.groupName} - Lights: {group.spotlights.Count} - Max Intensity: {group.maxIntensity}");
            }
        }
    }
}