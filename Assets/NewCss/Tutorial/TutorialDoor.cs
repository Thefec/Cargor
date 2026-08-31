using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Tutorial kapılarını kontrol eder.
/// Animator ile çalışır, belirli step tamamlanınca açılır.
/// </summary>
public class TutorialDoor : NetworkBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private string doorName = "Door 1";
    [Tooltip("Hangi tutorial step tamamlanınca bu kapı açılsın? (0 = ilk step)")]
    [SerializeField] private int requiredStepToOpen = 1;

    [Header("Animator")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string boolParameterName = "IsOpen";

    [Header("Audio")]
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorCloseSound;
    [SerializeField] private AudioSource audioSource;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;

    [Header("Collision")]
    [SerializeField] private Collider doorCollider;
    [SerializeField] private bool disableCollisionWhenOpen = true;

    [Header("Visual Effects")]
    [SerializeField] private Light doorLight;
    [SerializeField] private Color lockedColor = Color.red;
    [SerializeField] private Color unlockedColor = Color.green;
    [SerializeField] private ParticleSystem openEffect;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Network senkronize değişken
    private NetworkVariable<bool> isOpen = new NetworkVariable<bool>(false);

    // Başlangıç
    private void Start()
    {
        SetupComponents();
        InitializeDoor();
    }

    /// <summary>
    /// Component'leri otomatik bul ve ayarla
    /// </summary>
    private void SetupComponents()
    {
        // Animator yoksa child'larda ara
        if (doorAnimator == null)
        {
            doorAnimator = GetComponentInChildren<Animator>();
        }

        // AudioSource yoksa ekle
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }
        }
        audioSource.volume = volume;

        // Collider yoksa bul
        if (doorCollider == null)
        {
            doorCollider = GetComponent<Collider>();
        }

        if (showDebugLogs)
        {
            Debug.Log($"🚪 {doorName} components setup complete");
            if (doorAnimator != null)
                Debug.Log($"  ✅ Animator found: {doorAnimator.gameObject.name}");
            else
                Debug.LogError($"  ❌ Animator NOT found!");
        }
    }

    /// <summary>
    /// Kapıyı başlangıç durumuna ayarla
    /// </summary>
    private void InitializeDoor()
    {
        // Animator başlangıç durumu
        if (doorAnimator != null)
        {
            // Parameter var mı kontrol et
            bool hasParameter = false;
            foreach (var param in doorAnimator.parameters)
            {
                if (param.name == boolParameterName && param.type == AnimatorControllerParameterType.Bool)
                {
                    hasParameter = true;
                    break;
                }
            }

            if (hasParameter)
            {
                doorAnimator.SetBool(boolParameterName, false);

                if (showDebugLogs)
                    Debug.Log($"  ✅ Parameter '{boolParameterName}' found and set to false");
            }
            else
            {
                Debug.LogError($"  ❌ Parameter '{boolParameterName}' NOT FOUND in Animator!");
                Debug.LogError($"  Available parameters:");
                foreach (var param in doorAnimator.parameters)
                {
                    Debug.LogError($"    - {param.name} ({param.type})");
                }
            }
        }

        // Işık rengini ayarla
        UpdateLightColor(false);

        // Collision açık olsun
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }
    }

    /// <summary>
    /// Network spawn olunca çağrılır
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Network değişikliklerini dinle
        isOpen.OnValueChanged += OnDoorOpenStateChanged;

        // TutorialManager'a kayıt ol
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.RegisterDoor(this);

            if (showDebugLogs)
                Debug.Log($"🚪 {doorName} registered to TutorialManager");
        }
        else
        {
            Debug.LogWarning($"⚠️ {doorName}: TutorialManager.Instance is NULL!");
        }
    }

    /// <summary>
    /// Network despawn olunca çağrılır
    /// </summary>
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        isOpen.OnValueChanged -= OnDoorOpenStateChanged;
    }

    /// <summary>
    /// Kapı durumu değiştiğinde çağrılır (network senkronize)
    /// </summary>
    private void OnDoorOpenStateChanged(bool oldValue, bool newValue)
    {
        if (showDebugLogs)
            Debug.Log($"🚪 {doorName}: {(oldValue ? "Open" : "Closed")} → {(newValue ? "Open" : "Closed")}");

        // Animator'ı güncelle
        UpdateAnimator(newValue);

        // Ses çal
        PlayDoorSound(newValue);

        // Collision ayarla
        UpdateCollision(newValue);

        // Işık rengini güncelle
        UpdateLightColor(newValue);

        // Partikül efekti (sadece açılırken)
        if (newValue && openEffect != null)
        {
            openEffect.Play();
        }
    }

    /// <summary>
    /// Animator'ı güncelle
    /// </summary>
    private void UpdateAnimator(bool open)
    {
        if (doorAnimator == null)
        {
            Debug.LogError($"❌ {doorName}: doorAnimator is NULL!");
            return;
        }

        doorAnimator.SetBool(boolParameterName, open);

        if (showDebugLogs)
        {
            Debug.Log($"🎬 {doorName}: Animator '{boolParameterName}' = {open}");

            // State bilgisini göster
            StartCoroutine(LogAnimatorStateAfterFrame());
        }
    }

    /// <summary>
    /// Animator state'ini bir frame sonra logla (transition için)
    /// </summary>
    private IEnumerator LogAnimatorStateAfterFrame()
    {
        yield return new WaitForEndOfFrame();

        if (doorAnimator != null)
        {
            AnimatorStateInfo stateInfo = doorAnimator.GetCurrentAnimatorStateInfo(0);
            string stateName = GetCurrentStateName(stateInfo);

            Debug.Log($"📊 {doorName}: State = {stateName}, NormalizedTime = {stateInfo.normalizedTime:F2}");
        }
    }

    /// <summary>
    /// Animator'ın şu anki state ismini al
    /// </summary>
    private string GetCurrentStateName(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsName("Open")) return "Open";
        if (stateInfo.IsName("Closed")) return "Closed";
        return $"Unknown (Hash: {stateInfo.shortNameHash})";
    }

    /// <summary>
    /// Kapı sesini çal
    /// </summary>
    private void PlayDoorSound(bool opening)
    {
        AudioClip clip = opening ? doorOpenSound : doorCloseSound;

        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);

            if (showDebugLogs)
                Debug.Log($"🔊 {doorName}: Playing {(opening ? "open" : "close")} sound");
        }
    }

    /// <summary>
    /// Collision durumunu güncelle
    /// </summary>
    private void UpdateCollision(bool open)
    {
        if (disableCollisionWhenOpen && doorCollider != null)
        {
            doorCollider.enabled = !open;

            if (showDebugLogs)
                Debug.Log($"🔲 {doorName}: Collision {(open ? "disabled" : "enabled")}");
        }
    }

    /// <summary>
    /// Işık rengini güncelle
    /// </summary>
    private void UpdateLightColor(bool open)
    {
        if (doorLight != null)
        {
            doorLight.color = open ? unlockedColor : lockedColor;
        }
    }

    /// <summary>
    /// Tutorial step tamamlandığında TutorialManager tarafından çağrılır
    /// </summary>
    public void OnTutorialStepCompleted(int completedStepIndex)
    {
        // Gerekli step tamamlandı mı?
        if (completedStepIndex >= requiredStepToOpen && !isOpen.Value)
        {
            if (showDebugLogs)
                Debug.Log($"🔓 {doorName} UNLOCKED! Step {completedStepIndex} completed (required: {requiredStepToOpen})");

            OpenDoor();
        }
    }

    /// <summary>
    /// Kapıyı aç
    /// </summary>
    public void OpenDoor()
    {
        if (IsServer)
        {
            // Server ise direkt değiştir
            isOpen.Value = true;
        }
        else
        {
            // Client ise server'a iste
            OpenDoorServerRpc();
        }
    }

    /// <summary>
    /// Kapıyı kapat
    /// </summary>
    public void CloseDoor()
    {
        if (IsServer)
        {
            isOpen.Value = false;
        }
        else
        {
            CloseDoorServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void OpenDoorServerRpc()
    {
        if (!isOpen.Value)
        {
            isOpen.Value = true;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CloseDoorServerRpc()
    {
        if (isOpen.Value)
        {
            isOpen.Value = false;
        }
    }

    // Inspector test metodları
    [ContextMenu("🚪 Test Open Door")]
    private void TestOpen()
    {
        OpenDoor();
    }

    [ContextMenu("🚪 Test Close Door")]
    private void TestClose()
    {
        CloseDoor();
    }

    [ContextMenu("📊 Debug Animator")]
    private void DebugAnimator()
    {
        if (doorAnimator == null)
        {
            Debug.LogError("❌ No Animator!");
            return;
        }

        Debug.Log($"🎬 Animator Debug for {doorName}:");
        Debug.Log($"  GameObject: {doorAnimator.gameObject.name}");
        Debug.Log($"  Controller: {(doorAnimator.runtimeAnimatorController != null ? doorAnimator.runtimeAnimatorController.name : "NULL")}");
        Debug.Log($"  Parameter Count: {doorAnimator.parameterCount}");

        foreach (var param in doorAnimator.parameters)
        {
            object value = param.type switch
            {
                AnimatorControllerParameterType.Bool => doorAnimator.GetBool(param.name),
                AnimatorControllerParameterType.Float => doorAnimator.GetFloat(param.name),
                AnimatorControllerParameterType.Int => doorAnimator.GetInteger(param.name),
                _ => "Trigger"
            };

            Debug.Log($"  Parameter: {param.name} ({param.type}) = {value}");
        }

        if (Application.isPlaying)
        {
            AnimatorStateInfo stateInfo = doorAnimator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"  Current State: {GetCurrentStateName(stateInfo)}");
            Debug.Log($"  Normalized Time: {stateInfo.normalizedTime}");
        }
    }

    // Public properties
    public bool IsOpen => isOpen.Value;
    public int RequiredStepToOpen => requiredStepToOpen;
    public string DoorName => doorName;
}