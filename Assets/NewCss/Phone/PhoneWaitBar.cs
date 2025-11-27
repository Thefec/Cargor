using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Network destekli bekleme çubuðu - telefon çaðrýlarý ve diðer zamanlayýcýlar için kullanýlýr.  
/// Server-authoritative tasarým ile tüm client'larda senkronize çalýþýr.
/// </summary>
public class PhoneWaitBar : NetworkBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[PhoneWaitBar]";
    private const float MIN_WAIT_TIME = 0.1f;

    #endregion

    #region Serialized Fields

    [Header("=== WAIT BAR SETTINGS ===")]
    [SerializeField, Tooltip("Bekleme çubuðu Image component")]
    public Image waitBarImage;

    [SerializeField, Tooltip("Maksimum bekleme süresi")]
    public float maxWaitTime = 15.0f;

    [Header("=== VISUAL SETTINGS ===")]
    [SerializeField, Tooltip("Baþlangýçta gizli mi? ")]
    private bool startHidden = true;

    [SerializeField, Tooltip("Süre bittiðinde otomatik gizle")]
    private bool autoHideOnComplete = false;

    #endregion

    #region Network Variables

    private readonly NetworkVariable<float> _networkCurrentWaitTime = new(0f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> _networkMaxWaitTime = new(15f,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _networkIsActive = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _networkIsDecreasing = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    #endregion

    #region Events

    /// <summary>
    /// Süre bittiðinde tetiklenir
    /// </summary>
    public event Action OnTimeUpEvent;

    /// <summary>
    /// Çubuk baþlatýldýðýnda tetiklenir
    /// </summary>
    public event Action<float> OnBarStartedEvent;

    /// <summary>
    /// Çubuk gizlendiðinde tetiklenir
    /// </summary>
    public event Action OnBarHiddenEvent;

    #endregion

    #region Private Fields

    private float _localCurrentWaitTime;
    private bool _wasActive;

    #endregion

    #region Public Properties

    /// <summary>
    /// Kalan süre
    /// </summary>
    public float RemainingTime => _networkCurrentWaitTime.Value;

    /// <summary>
    /// Kalan süre yüzdesi (0-1)
    /// </summary>
    public float RemainingTimePercentage
    {
        get
        {
            if (_networkMaxWaitTime.Value <= 0) return 0f;
            return _networkCurrentWaitTime.Value / _networkMaxWaitTime.Value;
        }
    }

    /// <summary>
    /// Çubuk aktif mi?
    /// </summary>
    public bool IsActive => _networkIsActive.Value;

    /// <summary>
    /// Geri sayým devam ediyor mu?
    /// </summary>
    public bool IsCountingDown => _networkIsDecreasing.Value;

    /// <summary>
    /// Süre bitti mi?
    /// </summary>
    public bool IsTimeUp => _networkCurrentWaitTime.Value <= 0 && _networkIsActive.Value;

    /// <summary>
    /// Maksimum bekleme süresi
    /// </summary>
    public float MaxWaitTime => _networkMaxWaitTime.Value;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        InitializeBar();
    }

    private void Update()
    {
        if (IsServer)
        {
            ServerUpdate();
        }
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        SubscribeToNetworkEvents();

        if (IsServer)
        {
            InitializeNetworkVariables();
        }

        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeFromNetworkEvents();
        base.OnNetworkDespawn();
    }

    #endregion

    #region Initialization

    private void InitializeBar()
    {
        if (waitBarImage == null) return;

        waitBarImage.fillAmount = 1f;

        if (startHidden)
        {
            waitBarImage.gameObject.SetActive(false);
        }
    }

    private void InitializeNetworkVariables()
    {
        _networkCurrentWaitTime.Value = maxWaitTime;
        _networkMaxWaitTime.Value = maxWaitTime;
        _networkIsActive.Value = false;
        _networkIsDecreasing.Value = false;
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToNetworkEvents()
    {
        _networkCurrentWaitTime.OnValueChanged += HandleCurrentWaitTimeChanged;
        _networkMaxWaitTime.OnValueChanged += HandleMaxWaitTimeChanged;
        _networkIsActive.OnValueChanged += HandleIsActiveChanged;
        _networkIsDecreasing.OnValueChanged += HandleIsDecreasingChanged;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        _networkCurrentWaitTime.OnValueChanged -= HandleCurrentWaitTimeChanged;
        _networkMaxWaitTime.OnValueChanged -= HandleMaxWaitTimeChanged;
        _networkIsActive.OnValueChanged -= HandleIsActiveChanged;
        _networkIsDecreasing.OnValueChanged -= HandleIsDecreasingChanged;
    }

    #endregion

    #region Network Event Handlers

    private void HandleCurrentWaitTimeChanged(float previousValue, float newValue)
    {
        _localCurrentWaitTime = newValue;
        UpdateUI();
    }

    private void HandleMaxWaitTimeChanged(float previousValue, float newValue)
    {
        maxWaitTime = newValue;
        UpdateUI();
    }

    private void HandleIsActiveChanged(bool previousValue, bool newValue)
    {
        _wasActive = newValue;
        UpdateUI();
    }

    private void HandleIsDecreasingChanged(bool previousValue, bool newValue)
    {
        UpdateUI();
    }

    #endregion

    #region Server Update

    private void ServerUpdate()
    {
        if (!ShouldUpdateTimer()) return;

        UpdateTimer();
        CheckTimeUp();
    }

    private bool ShouldUpdateTimer()
    {
        return _networkIsActive.Value &&
               _networkIsDecreasing.Value &&
               _networkCurrentWaitTime.Value > 0;
    }

    private void UpdateTimer()
    {
        float newTime = _networkCurrentWaitTime.Value - Time.deltaTime;
        _networkCurrentWaitTime.Value = Mathf.Max(0f, newTime);
    }

    private void CheckTimeUp()
    {
        if (_networkCurrentWaitTime.Value <= 0)
        {
            HandleTimeUpServer();
        }
    }

    #endregion

    #region UI Update

    private void UpdateUI()
    {
        if (waitBarImage == null) return;

        UpdateFillAmount();
        UpdateVisibility();
    }

    private void UpdateFillAmount()
    {
        if (_networkMaxWaitTime.Value > 0)
        {
            waitBarImage.fillAmount = Mathf.Max(0f, _networkCurrentWaitTime.Value / _networkMaxWaitTime.Value);
        }
        else
        {
            waitBarImage.fillAmount = 0f;
        }
    }

    private void UpdateVisibility()
    {
        waitBarImage.gameObject.SetActive(_networkIsActive.Value);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Bekleme çubuðunu baþlatýr
    /// </summary>
    /// <param name="waitTime">Toplam bekleme süresi</param>
    public void StartWaitBar(float waitTime)
    {
        float clampedWaitTime = Mathf.Max(MIN_WAIT_TIME, waitTime);

        if (IsServer)
        {
            ExecuteStartWaitBar(clampedWaitTime);
        }
        else
        {
            StartWaitBarServerRpc(clampedWaitTime);
        }
    }

    /// <summary>
    /// Geri sayýmý durdurur (çubuk görünür kalýr)
    /// </summary>
    public void StopCountdown()
    {
        if (IsServer)
        {
            ExecuteStopCountdown();
        }
        else
        {
            StopCountdownServerRpc();
        }
    }

    /// <summary>
    /// Geri sayýmý devam ettirir
    /// </summary>
    public void ResumeCountdown()
    {
        if (IsServer)
        {
            ExecuteResumeCountdown();
        }
        else
        {
            ResumeCountdownServerRpc();
        }
    }

    /// <summary>
    /// Çubuðu gizler ve deaktif eder
    /// </summary>
    public void HideBar()
    {
        if (IsServer)
        {
            ExecuteHideBar();
        }
        else
        {
            HideBarServerRpc();
        }
    }

    /// <summary>
    /// Çubuðu yeni süre ile sýfýrlar
    /// </summary>
    /// <param name="newWaitTime">Yeni bekleme süresi</param>
    public void ResetBar(float newWaitTime)
    {
        float clampedWaitTime = Mathf.Max(MIN_WAIT_TIME, newWaitTime);

        if (IsServer)
        {
            ExecuteResetBar(clampedWaitTime);
        }
        else
        {
            ResetBarServerRpc(clampedWaitTime);
        }
    }

    /// <summary>
    /// Çubuðu zorla tamamlar
    /// </summary>
    public void ForceComplete()
    {
        if (IsServer)
        {
            ExecuteForceComplete();
        }
        else
        {
            ForceCompleteServerRpc();
        }
    }

    /// <summary>
    /// Süreyi deðiþtirir
    /// </summary>
    /// <param name="timeToAdd">Eklenecek süre (negatif = çýkarma)</param>
    public void ModifyTime(float timeToAdd)
    {
        if (IsServer)
        {
            ExecuteModifyTime(timeToAdd);
        }
        else
        {
            ModifyTimeServerRpc(timeToAdd);
        }
    }

    #endregion

    #region Public Getters (Backward Compatibility)

    /// <summary>
    /// Kalan süreyi döndürür
    /// </summary>
    public float GetRemainingTime() => RemainingTime;

    /// <summary>
    /// Kalan süre yüzdesini döndürür (0-1)
    /// </summary>
    public float GetRemainingTimePercentage() => RemainingTimePercentage;

    /// <summary>
    /// Aktif mi kontrolü
    /// </summary>
    public bool IsActiveCheck() => IsActive;

    /// <summary>
    /// Geri sayým devam ediyor mu kontrolü
    /// </summary>
    public bool IsCountingDownCheck() => IsCountingDown;

    /// <summary>
    /// Süre bitti mi kontrolü
    /// </summary>
    public bool IsTimeUpCheck() => IsTimeUp;

    #endregion

    #region Server Execution Methods

    private void ExecuteStartWaitBar(float waitTime)
    {
        _networkMaxWaitTime.Value = waitTime;
        _networkCurrentWaitTime.Value = waitTime;
        _networkIsActive.Value = true;
        _networkIsDecreasing.Value = true;

        NotifyBarStartedClientRpc(waitTime);
    }

    private void ExecuteStopCountdown()
    {
        _networkIsDecreasing.Value = false;
    }

    private void ExecuteResumeCountdown()
    {
        if (_networkIsActive.Value)
        {
            _networkIsDecreasing.Value = true;
        }
    }

    private void ExecuteHideBar()
    {
        _networkIsActive.Value = false;
        _networkIsDecreasing.Value = false;

        NotifyBarHiddenClientRpc();
    }

    private void ExecuteResetBar(float newWaitTime)
    {
        _networkMaxWaitTime.Value = newWaitTime;
        _networkCurrentWaitTime.Value = newWaitTime;
    }

    private void ExecuteForceComplete()
    {
        _networkCurrentWaitTime.Value = 0;
        HandleTimeUpServer();
    }

    private void ExecuteModifyTime(float timeToAdd)
    {
        float newTime = _networkCurrentWaitTime.Value + timeToAdd;
        _networkCurrentWaitTime.Value = Mathf.Max(0, newTime);
    }

    private void HandleTimeUpServer()
    {
        _networkIsDecreasing.Value = false;

        if (autoHideOnComplete)
        {
            ExecuteHideBar();
        }

        NotifyTimeUpClientRpc();
    }

    #endregion

    #region Server RPCs

    [ServerRpc(RequireOwnership = false)]
    private void StartWaitBarServerRpc(float waitTime)
    {
        ExecuteStartWaitBar(waitTime);
    }

    [ServerRpc(RequireOwnership = false)]
    private void StopCountdownServerRpc()
    {
        ExecuteStopCountdown();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResumeCountdownServerRpc()
    {
        ExecuteResumeCountdown();
    }

    [ServerRpc(RequireOwnership = false)]
    private void HideBarServerRpc()
    {
        ExecuteHideBar();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetBarServerRpc(float newWaitTime)
    {
        ExecuteResetBar(newWaitTime);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ForceCompleteServerRpc()
    {
        ExecuteForceComplete();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ModifyTimeServerRpc(float timeToAdd)
    {
        ExecuteModifyTime(timeToAdd);
    }

    #endregion

    #region Client RPCs

    [ClientRpc]
    private void NotifyTimeUpClientRpc()
    {
        OnTimeUp();
        OnTimeUpEvent?.Invoke();
    }

    [ClientRpc]
    private void NotifyBarStartedClientRpc(float waitTime)
    {
        OnBarStartedEvent?.Invoke(waitTime);
    }

    [ClientRpc]
    private void NotifyBarHiddenClientRpc()
    {
        OnBarHiddenEvent?.Invoke();
    }

    #endregion

    #region Virtual Methods

    /// <summary>
    /// Süre bittiðinde çaðrýlýr - override edilebilir
    /// </summary>
    protected virtual void OnTimeUp()
    {
        // PhoneCallManager will handle the timeout
        // Override this for custom behavior
    }

    #endregion

    #region Editor Debug

#if UNITY_EDITOR
    [ContextMenu("Test: Start Bar (10s)")]
    private void DebugStartBar10s()
    {
        StartWaitBar(10f);
    }

    [ContextMenu("Test: Start Bar (30s)")]
    private void DebugStartBar30s()
    {
        StartWaitBar(30f);
    }

    [ContextMenu("Test: Stop Countdown")]
    private void DebugStopCountdown()
    {
        StopCountdown();
    }

    [ContextMenu("Test: Resume Countdown")]
    private void DebugResumeCountdown()
    {
        ResumeCountdown();
    }

    [ContextMenu("Test: Hide Bar")]
    private void DebugHideBar()
    {
        HideBar();
    }

    [ContextMenu("Test: Force Complete")]
    private void DebugForceComplete()
    {
        ForceComplete();
    }

    [ContextMenu("Test: Add 5 Seconds")]
    private void DebugAdd5Seconds()
    {
        ModifyTime(5f);
    }

    [ContextMenu("Test: Remove 5 Seconds")]
    private void DebugRemove5Seconds()
    {
        ModifyTime(-5f);
    }

    [ContextMenu("Debug: Print State")]
    private void DebugPrintState()
    {
        Debug.Log($"{LOG_PREFIX} === WAIT BAR STATE ===");
        Debug.Log($"Is Active: {IsActive}");
        Debug.Log($"Is Counting Down: {IsCountingDown}");
        Debug.Log($"Is Time Up: {IsTimeUp}");
        Debug.Log($"Remaining Time: {RemainingTime:F2}s");
        Debug.Log($"Remaining Percentage: {RemainingTimePercentage:P0}");
        Debug.Log($"Max Wait Time: {MaxWaitTime:F2}s");
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Draw progress indicator
        DrawProgressGizmo();
    }

    private void DrawProgressGizmo()
    {
        Vector3 position = transform.position + Vector3.up * 2f;

        // Draw background
        Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawCube(position, new Vector3(2f, 0.2f, 0.1f));

        // Draw progress
        if (IsActive)
        {
            float progress = RemainingTimePercentage;
            Gizmos.color = progress > 0.3f ? Color.green : (progress > 0.1f ? Color.yellow : Color.red);
            Gizmos.DrawCube(position - Vector3.right * (1f - progress), new Vector3(2f * progress, 0.2f, 0.1f));
        }
    }
#endif

    #endregion
}