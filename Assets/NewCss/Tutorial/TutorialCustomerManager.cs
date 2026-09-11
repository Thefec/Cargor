using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using NewCss;

/// <summary>
/// Tutorial-only, minimal müşteri spawner. Tek amacı: belirlenen tutorial adımı
/// başladığında Customer prefabını belirlenen spawn noktasında, üretim CustomerAI'ının
/// çalışması için gerekli minimum kurulumla (manager, nav agent, UI, animator, collider,
/// servis istasyonu ataması) spawnlamak. Kuyruk/gün-döngüsü/ekonomi mantığına dokunmaz.
///
/// CustomerManager'dan türetilir çünkü CustomerAI.manager alanı bu tipi bekliyor —
/// InitializeServerState() içindeki perk/ürün ataması bu referans üzerinden okunuyor
/// (bkz. CustomerAI.cs:541-594). manager null bırakılırsa bu okumalar sessizce
/// default'a düşer (patienceMultiplier=1, dual-item/box-request kapalı), üretim
/// GetRandomProductIndexExcludingRecent çağrılmaz. Base sınıfın Awake/Start/Update'i
/// private olduğu için override EDİLEMEZ (Unity hem base'inkini hem burada tanımlanan
/// yenisini ayrı ayrı çağırır) — bu yüzden kendi mantığımızı OnNetworkSpawn'a
/// (NetworkBehaviour'un virtual metodu, CustomerManager bunu override etmiyor) bağladık.
/// Base'in Awake/Start/Update'i de çalışmaya devam eder ama Tutorial sahnesinde
/// DayCycleManager/başka CustomerManager olmadığından hepsi mevcut null-guard'lar
/// sayesinde no-op kalır (bkz. plans/tutorial-rewrite.md).
/// </summary>
public class TutorialCustomerManager : CustomerManager
{
    [Header("=== TUTORIAL SPAWN ===")]
    [SerializeField, Tooltip("Spawn edilecek müşteri prefabı (üretim Customer.prefab)")]
    private GameObject tutorialCustomerPrefab;

    [SerializeField, Tooltip("Müşterinin spawn olacağı konum")]
    private Transform tutorialSpawnPoint;

    [SerializeField, Tooltip("Müşterinin siparişini bırakacağı masa (ör. TutorialDisplayTable)")]
    private DisplayTable tutorialDisplayTable;

    [SerializeField, Tooltip("Bu TutorialStep.stepIndex'i (0-based) BAŞLADIĞINDA spawn tetiklenir. " +
        "Varsayım: adım 1 = TalkAndGetItem (\"2. adım\", 1-tabanlı sayımla) - yanlışsa değiştir.")]
    private int spawnOnStepIndex = 1;

    private bool _hasSpawned;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        StartCoroutine(SubscribeWhenTutorialManagerReady());
    }

    public override void OnNetworkDespawn()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnStepStarted -= HandleStepStarted;
        }

        base.OnNetworkDespawn();
    }

    private IEnumerator SubscribeWhenTutorialManagerReady()
    {
        while (TutorialManager.Instance == null)
        {
            yield return null;
        }

        TutorialManager.Instance.OnStepStarted += HandleStepStarted;
    }

    private void HandleStepStarted(int stepIndex, TutorialStep step)
    {
        if (_hasSpawned || stepIndex != spawnOnStepIndex) return;

        SpawnTutorialCustomer();
    }

    private void SpawnTutorialCustomer()
    {
        if (tutorialCustomerPrefab == null || tutorialSpawnPoint == null)
        {
            Debug.LogError("[TutorialCustomerManager] tutorialCustomerPrefab veya tutorialSpawnPoint atanmamış!");
            return;
        }

        var customerObject = Instantiate(tutorialCustomerPrefab, tutorialSpawnPoint.position, tutorialSpawnPoint.rotation);
        var networkObject = customerObject.GetComponent<NetworkObject>();
        var customerAI = customerObject.GetComponent<CustomerAI>();

        if (networkObject == null || customerAI == null)
        {
            Debug.LogError("[TutorialCustomerManager] Prefab'da NetworkObject/CustomerAI bulunamadı!");
            Destroy(customerObject);
            return;
        }

        // Üretim CustomerManager.SetupCustomerAI/SetupCustomerComponents ile aynı sıra:
        // manager + component kurulumu Spawn()'DAN ÖNCE yapılmalı (InitializeServerState,
        // OnNetworkSpawn içinden senkron çağrılır ve manager'ı o an okur).
        customerAI.isPrefabMode = false;
        customerAI.manager = this;

        var navAgent = customerAI.GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = true;
            // enabled=true tek başına ajanı NavMesh yüzeyine tam oturtmaz - spawn point
            // birkaç cm yukarıdaysa "havada duruyor" görüntüsü verir. Warp en yakın NavMesh
            // noktasına snap'ler.
            navAgent.Warp(customerObject.transform.position);
        }

        // Spawn point'in rotasyonundan bağımsız, müşteriyi masaya baksın diye döndür.
        if (tutorialDisplayTable != null)
        {
            Vector3 lookDirection = tutorialDisplayTable.transform.position - customerObject.transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                customerObject.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        var animator = customerAI.GetComponent<Animator>();
        if (animator != null) animator.enabled = true;

        var interactionCollider = customerAI.GetComponent<SphereCollider>();
        if (interactionCollider != null)
        {
            interactionCollider.enabled = true;
            interactionCollider.isTrigger = true;
        }

        var canvas = customerAI.GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.gameObject.SetActive(!customerAI.hideCanvasUntilTimer);
            customerAI.waitCanvas = canvas;
        }

        var waitBar = customerAI.GetComponentInChildren<WaitBar>();
        if (waitBar != null)
        {
            waitBar.HideBar();
            customerAI.waitBar = waitBar;
        }

        networkObject.Spawn();

        if (tutorialDisplayTable != null)
        {
            customerAI.AssignServiceStation(tutorialDisplayTable);
        }
        else
        {
            Debug.LogWarning("[TutorialCustomerManager] tutorialDisplayTable atanmamış - müşteri Service state'ine geçmeyecek.");
        }

        _hasSpawned = true;
        Debug.Log($"[TutorialCustomerManager] Customer step {spawnOnStepIndex}'de {tutorialSpawnPoint.position} konumunda spawnlandı.");
    }
}
