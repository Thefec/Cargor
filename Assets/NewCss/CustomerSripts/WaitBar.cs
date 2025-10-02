using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class WaitBar : NetworkBehaviour
{
    [Header("Wait Bar Settings")]
    public Image waitBarImage; // UI Image component for the wait bar
    public float maxWaitTime = 15.0f; // Maximum wait time

    private float currentWaitTime;
    private bool isActive = false;
    private bool isDecreasing = false;

    // Network variables for synchronization
    private NetworkVariable<float> networkCurrentWaitTime = new NetworkVariable<float>(0f);
    private NetworkVariable<float> networkMaxWaitTime = new NetworkVariable<float>(15f);
    private NetworkVariable<bool> networkIsActive = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> networkIsDecreasing = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes on clients
        if (!IsServer)
        {
            networkCurrentWaitTime.OnValueChanged += OnCurrentWaitTimeChanged;
            networkMaxWaitTime.OnValueChanged += OnMaxWaitTimeChanged;
            networkIsActive.OnValueChanged += OnIsActiveChanged;
            networkIsDecreasing.OnValueChanged += OnIsDecreasingChanged;
        }
    }

    void Start()
    {
        // Initialize the bar as full but hidden
        currentWaitTime = maxWaitTime;
        if (waitBarImage != null)
        {
            waitBarImage.fillAmount = 1f;
            waitBarImage.gameObject.SetActive(false); // Start hidden
        }
    }

    void Update()
    {
        if (IsServer)
        {
            // Server handles the logic
            if (isActive && isDecreasing && currentWaitTime > 0)
            {
                currentWaitTime -= Time.deltaTime;
                
                // Update network variable
                networkCurrentWaitTime.Value = currentWaitTime;

                // Check if time is up
                if (currentWaitTime <= 0)
                {
                    currentWaitTime = 0;
                    networkCurrentWaitTime.Value = 0;
                    OnTimeUp();
                }
            }
        }
        
        // Update UI on all clients
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (waitBarImage != null && networkMaxWaitTime.Value > 0)
        {
            float fillAmount = Mathf.Max(0f, networkCurrentWaitTime.Value / networkMaxWaitTime.Value);
            waitBarImage.fillAmount = fillAmount;
        }
    }

    /// <summary>
    /// Starts the wait bar with specified wait time
    /// </summary>
    /// <param name="waitTime">Total wait time</param>
    public void StartWaitBar(float waitTime)
    {
        if (IsServer)
        {
            maxWaitTime = waitTime;
            currentWaitTime = waitTime;
            isActive = true;
            isDecreasing = true;

            // Update network variables
            networkMaxWaitTime.Value = waitTime;
            networkCurrentWaitTime.Value = waitTime;
            networkIsActive.Value = true;
            networkIsDecreasing.Value = true;
        }

        // Update UI immediately
        if (waitBarImage != null)
        {
            waitBarImage.fillAmount = 1f;
            waitBarImage.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Stops the wait bar countdown (but keeps it visible)
    /// </summary>
    public void StopCountdown()
    {
        if (IsServer)
        {
            isDecreasing = false;
            networkIsDecreasing.Value = false;
        }
    }

    /// <summary>
    /// Resumes the wait bar countdown
    /// </summary>
    public void ResumeCountdown()
    {
        if (IsServer)
        {
            isDecreasing = true;
            networkIsDecreasing.Value = true;
        }
    }

    /// <summary>
    /// Hides and deactivates the wait bar
    /// </summary>
    public void HideBar()
    {
        if (IsServer)
        {
            isActive = false;
            isDecreasing = false;
            networkIsActive.Value = false;
            networkIsDecreasing.Value = false;
        }

        if (waitBarImage != null)
        {
            waitBarImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Resets the wait bar to full
    /// </summary>
    public void ResetBar(float newWaitTime)
    {
        if (IsServer)
        {
            maxWaitTime = newWaitTime;
            currentWaitTime = newWaitTime;
            networkMaxWaitTime.Value = newWaitTime;
            networkCurrentWaitTime.Value = newWaitTime;
        }

        if (waitBarImage != null)
        {
            waitBarImage.fillAmount = 1f;
        }
    }

    /// <summary>
    /// Returns current remaining time
    /// </summary>
    public float GetRemainingTime()
    {
        return IsServer ? currentWaitTime : networkCurrentWaitTime.Value;
    }

    /// <summary>
    /// Returns whether the wait bar is currently active
    /// </summary>
    public bool IsActive()
    {
        return IsServer ? isActive : networkIsActive.Value;
    }

    /// <summary>
    /// Returns whether the countdown is currently running
    /// </summary>
    public bool IsCountingDown()
    {
        return IsServer ? isDecreasing : networkIsDecreasing.Value;
    }

    /// <summary>
    /// Called when time runs out
    /// </summary>
    private void OnTimeUp()
    {
        if (IsServer)
        {
            isDecreasing = false;
            networkIsDecreasing.Value = false;
        }
        // This will be handled by CustomerAI
    }

    // Network variable change handlers (for clients)
    private void OnCurrentWaitTimeChanged(float previousValue, float newValue)
    {
        if (!IsServer)
        {
            currentWaitTime = newValue;
        }
    }

    private void OnMaxWaitTimeChanged(float previousValue, float newValue)
    {
        if (!IsServer)
        {
            maxWaitTime = newValue;
        }
    }

    private void OnIsActiveChanged(bool previousValue, bool newValue)
    {
        if (!IsServer)
        {
            isActive = newValue;
            if (waitBarImage != null)
            {
                waitBarImage.gameObject.SetActive(newValue);
            }
        }
    }

    private void OnIsDecreasingChanged(bool previousValue, bool newValue)
    {
        if (!IsServer)
        {
            isDecreasing = newValue;
        }
    }
}