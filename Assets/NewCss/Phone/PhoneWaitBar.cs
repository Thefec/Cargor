using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PhoneWaitBar : NetworkBehaviour
{
    [Header("Wait Bar Settings")]
    public Image waitBarImage; // UI Image component for the wait bar
    public float maxWaitTime = 15.0f; // Maximum wait time
    
    // Network variables for synchronization
    private NetworkVariable<float> networkCurrentWaitTime = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> networkMaxWaitTime = new NetworkVariable<float>(15f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> networkIsActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> networkIsDecreasing = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // Local variables
    private float localCurrentWaitTime;
    private bool wasActive = false;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to network variable changes
        networkCurrentWaitTime.OnValueChanged += OnCurrentWaitTimeChanged;
        networkMaxWaitTime.OnValueChanged += OnMaxWaitTimeChanged;
        networkIsActive.OnValueChanged += OnIsActiveChanged;
        networkIsDecreasing.OnValueChanged += OnIsDecreasingChanged;
        
        // Initialize UI based on current network values
        UpdateUI();
    }
    
    public override void OnNetworkDespawn()
    {
        // Unsubscribe from network variable changes
        networkCurrentWaitTime.OnValueChanged -= OnCurrentWaitTimeChanged;
        networkMaxWaitTime.OnValueChanged -= OnMaxWaitTimeChanged;
        networkIsActive.OnValueChanged -= OnIsActiveChanged;
        networkIsDecreasing.OnValueChanged -= OnIsDecreasingChanged;
        
        base.OnNetworkDespawn();
    }
    
    void Start()
    {
        // Initialize the bar as full but hidden
        if (waitBarImage != null)
        {
            waitBarImage.fillAmount = 1f;
            waitBarImage.gameObject.SetActive(false); // Start hidden
        }
        
        // If this is the server, initialize network variables
        if (IsServer)
        {
            networkCurrentWaitTime.Value = maxWaitTime;
            networkMaxWaitTime.Value = maxWaitTime;
            networkIsActive.Value = false;
            networkIsDecreasing.Value = false;
        }
    }
    
    void Update()
    {
        // Only server updates the timer
        if (IsServer && networkIsActive.Value && networkIsDecreasing.Value && networkCurrentWaitTime.Value > 0)
        {
            // Decrease the current wait time
            float newTime = networkCurrentWaitTime.Value - Time.deltaTime;
            networkCurrentWaitTime.Value = Mathf.Max(0f, newTime);
            
            // Check if time is up
            if (networkCurrentWaitTime.Value <= 0)
            {
                OnTimeUpServerRpc();
            }
        }
    }
    
    #region Network Variable Change Handlers
    
    private void OnCurrentWaitTimeChanged(float previousValue, float newValue)
    {
        localCurrentWaitTime = newValue;
        UpdateUI();
    }
    
    private void OnMaxWaitTimeChanged(float previousValue, float newValue)
    {
        maxWaitTime = newValue;
        UpdateUI();
    }
    
    private void OnIsActiveChanged(bool previousValue, bool newValue)
    {
        wasActive = newValue;
        UpdateUI();
    }
    
    private void OnIsDecreasingChanged(bool previousValue, bool newValue)
    {
        UpdateUI();
    }
    
    #endregion
    
    #region UI Update
    
    private void UpdateUI()
    {
        if (waitBarImage != null)
        {
            // Update fill amount
            if (networkMaxWaitTime.Value > 0)
            {
                waitBarImage.fillAmount = Mathf.Max(0f, networkCurrentWaitTime.Value / networkMaxWaitTime.Value);
            }
            
            // Update visibility
            waitBarImage.gameObject.SetActive(networkIsActive.Value);
        }
    }
    
    #endregion
    
    #region Public Methods (Client can call these)
    
    /// <summary>
    /// Starts the wait bar with specified wait time
    /// </summary>
    /// <param name="waitTime">Total wait time</param>
    public void StartWaitBar(float waitTime)
    {
        if (IsServer)
        {
            StartWaitBarServer(waitTime);
        }
        else
        {
            StartWaitBarServerRpc(waitTime);
        }
    }
    
    /// <summary>
    /// Stops the wait bar countdown (but keeps it visible)
    /// </summary>
    public void StopCountdown()
    {
        if (IsServer)
        {
            StopCountdownServer();
        }
        else
        {
            StopCountdownServerRpc();
        }
    }
    
    /// <summary>
    /// Resumes the wait bar countdown
    /// </summary>
    public void ResumeCountdown()
    {
        if (IsServer)
        {
            ResumeCountdownServer();
        }
        else
        {
            ResumeCountdownServerRpc();
        }
    }
    
    /// <summary>
    /// Hides and deactivates the wait bar
    /// </summary>
    public void HideBar()
    {
        if (IsServer)
        {
            HideBarServer();
        }
        else
        {
            HideBarServerRpc();
        }
    }
    
    /// <summary>
    /// Resets the wait bar to full
    /// </summary>
    public void ResetBar(float newWaitTime)
    {
        if (IsServer)
        {
            ResetBarServer(newWaitTime);
        }
        else
        {
            ResetBarServerRpc(newWaitTime);
        }
    }
    
    /// <summary>
    /// Force complete the wait bar
    /// </summary>
    public void ForceComplete()
    {
        if (IsServer)
        {
            ForceCompleteServer();
        }
        else
        {
            ForceCompleteServerRpc();
        }
    }
    
    /// <summary>
    /// Add or remove time from current wait time
    /// </summary>
    /// <param name="timeToAdd">Time to add (negative to subtract)</param>
    public void ModifyTime(float timeToAdd)
    {
        if (IsServer)
        {
            ModifyTimeServer(timeToAdd);
        }
        else
        {
            ModifyTimeServerRpc(timeToAdd);
        }
    }
    
    #endregion
    
    #region Server Methods
    
    private void StartWaitBarServer(float waitTime)
    {
        networkMaxWaitTime.Value = waitTime;
        networkCurrentWaitTime.Value = waitTime;
        networkIsActive.Value = true;
        networkIsDecreasing.Value = true;
    }
    
    private void StopCountdownServer()
    {
        networkIsDecreasing.Value = false;
    }
    
    private void ResumeCountdownServer()
    {
        if (networkIsActive.Value)
        {
            networkIsDecreasing.Value = true;
        }
    }
    
    private void HideBarServer()
    {
        networkIsActive.Value = false;
        networkIsDecreasing.Value = false;
    }
    
    private void ResetBarServer(float newWaitTime)
    {
        networkMaxWaitTime.Value = newWaitTime;
        networkCurrentWaitTime.Value = newWaitTime;
    }
    
    private void ForceCompleteServer()
    {
        networkCurrentWaitTime.Value = 0;
        OnTimeUpServer();
    }
    
    private void ModifyTimeServer(float timeToAdd)
    {
        networkCurrentWaitTime.Value = Mathf.Max(0, networkCurrentWaitTime.Value + timeToAdd);
    }
    
    private void OnTimeUpServer()
    {
        networkIsDecreasing.Value = false;
        
        // Notify all clients that time is up
        OnTimeUpClientRpc();
    }
    
    #endregion
    
    #region Server RPCs
    
    [ServerRpc(RequireOwnership = false)]
    private void StartWaitBarServerRpc(float waitTime)
    {
        StartWaitBarServer(waitTime);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void StopCountdownServerRpc()
    {
        StopCountdownServer();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ResumeCountdownServerRpc()
    {
        ResumeCountdownServer();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void HideBarServerRpc()
    {
        HideBarServer();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ResetBarServerRpc(float newWaitTime)
    {
        ResetBarServer(newWaitTime);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ForceCompleteServerRpc()
    {
        ForceCompleteServer();
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ModifyTimeServerRpc(float timeToAdd)
    {
        ModifyTimeServer(timeToAdd);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void OnTimeUpServerRpc()
    {
        OnTimeUpServer();
    }
    
    #endregion
    
    #region Client RPCs
    
    [ClientRpc]
    private void OnTimeUpClientRpc()
    {
        // Handle time up event on all clients
        // You can add custom logic here or use events
        OnTimeUp();
    }
    
    #endregion
    
    #region Public Getters
    
    /// <summary>
    /// Returns current remaining time
    /// </summary>
    public float GetRemainingTime()
    {
        return networkCurrentWaitTime.Value;
    }
    
    /// <summary>
    /// Returns remaining time as percentage (0-1)
    /// </summary>
    public float GetRemainingTimePercentage()
    {
        if (networkMaxWaitTime.Value <= 0) return 0f;
        return networkCurrentWaitTime.Value / networkMaxWaitTime.Value;
    }
    
    /// <summary>
    /// Returns whether the wait bar is currently active
    /// </summary>
    public bool IsActive()
    {
        return networkIsActive.Value;
    }
    
    /// <summary>
    /// Returns whether the countdown is currently running
    /// </summary>
    public bool IsCountingDown()
    {
        return networkIsDecreasing.Value;
    }
    
    /// <summary>
    /// Returns whether time has run out
    /// </summary>
    public bool IsTimeUp()
    {
        return networkCurrentWaitTime.Value <= 0 && networkIsActive.Value;
    }
    
    #endregion
    
    #region Events
    
    /// <summary>
    /// Called when time runs out - override this or subscribe to handle timeout
    /// </summary>
    protected virtual void OnTimeUp()
    {
        // PhoneCallManager will handle the timeout
        // You can also add UnityEvents here for Inspector assignment
    }
    
    #endregion
}