using Unity.Netcode;
using UnityEngine;
using System.Collections;

/// <summary>
/// World'deki pickup edilebilir item (NetworkObject olarak)
/// </summary>
public class NetworkWorldItem : NetworkBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[NetworkWorldItem]";

    #endregion

    #region Serialized Fields

    [SerializeField] private ItemData itemData;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider itemCollider;

    #endregion

    #region Network Variables

    private NetworkVariable<int> networkItemID = new NetworkVariable<int>(-1);
    private NetworkVariable<bool> canBePickedUp = new NetworkVariable<bool>(true);
    private NetworkVariable<bool> isOnTable = new NetworkVariable<bool>(false);

    #endregion

    #region Public Properties

    public ItemData ItemData => itemData;
    public bool CanBePickedUp => canBePickedUp.Value;
    public bool IsOnTable => isOnTable.Value;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (itemCollider == null) itemCollider = GetComponent<Collider>();
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            canBePickedUp.Value = true;
            isOnTable.Value = false;
        }

        networkItemID.OnValueChanged += OnItemIDChanged;
        isOnTable.OnValueChanged += OnTableStateChanged;

        if (networkItemID.Value != -1)
        {
            UpdateItemDataFromID(networkItemID.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkItemID.OnValueChanged -= OnItemIDChanged;
        isOnTable.OnValueChanged -= OnTableStateChanged;
        base.OnNetworkDespawn();
    }

    #endregion

    #region Network Event Handlers

    private void OnItemIDChanged(int previousValue, int newValue)
    {
        if (newValue != -1)
        {
            UpdateItemDataFromID(newValue);
        }
    }

    private void OnTableStateChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            // Masaya konuldu - fizik dondur
            FreezePhysics();
        }
        else
        {
            // Masadan alındı - fizik aç (pickup sistemi yönetecek)
        }
    }

    #endregion

    #region ItemData Management

    private void UpdateItemDataFromID(int itemID)
    {
        ItemData[] allItems = Resources.LoadAll<ItemData>("Items");
        foreach (ItemData item in allItems)
        {
            if (item.itemID == itemID)
            {
                itemData = item;
                break;
            }
        }
    }

    public void SetItemData(ItemData newItemData)
    {
        if (IsServer && newItemData != null)
        {
            itemData = newItemData;
            networkItemID.Value = newItemData.itemID;
            Debug.Log($"{LOG_PREFIX} Item data set: {newItemData.itemName} (ID: {newItemData.itemID})");
        }
    }

    #endregion

    #region Physics Control

    /// <summary>
    /// Fizik ayarlarını dondurur (masada durması için)
    /// </summary>
    public void FreezePhysics()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        // ItemFreezeSystem varsa devre dışı bırak
        var freezeSystem = GetComponent<ItemFreezeSystem>();
        if (freezeSystem != null)
        {
            freezeSystem.enabled = false;
        }

        if (IsServer)
        {
            isOnTable.Value = true;
        }

        Debug.Log($"{LOG_PREFIX} Physics frozen: {gameObject.name}");
    }

    /// <summary>
    /// Fizik ayarlarını geri açar (pickup sonrası)
    /// </summary>
    public void UnfreezePhysics()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
            // isKinematic durumunu pickup sistemi yönetsin
        }

        // ItemFreezeSystem'ı tekrar aktif et
        var freezeSystem = GetComponent<ItemFreezeSystem>();
        if (freezeSystem != null)
        {
            freezeSystem.enabled = true;
        }

        if (IsServer)
        {
            isOnTable.Value = false;
        }

        Debug.Log($"{LOG_PREFIX} Physics unfrozen:  {gameObject.name}");
    }

    public void SetThrowForce(Vector3 force)
    {
        if (!IsServer) return;

        if (rb == null) rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Önce fizik ayarlarını aç
            rb.constraints = RigidbodyConstraints.None;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;

            // Masadan alındı
            isOnTable.Value = false;

            // Force'u uygula
            rb.AddForce(force, ForceMode.VelocityChange);

            Debug.Log($"{LOG_PREFIX} Throw force applied: {force} to {gameObject.name}");
        }
        else
        {
            Debug.LogError($"{LOG_PREFIX} No Rigidbody found on {gameObject.name}");
        }
    }

    #endregion

    #region Pickup State

    public void DisablePickup()
    {
        if (IsServer)
        {
            canBePickedUp.Value = false;
        }
    }

    public void EnablePickup()
    {
        if (IsServer)
        {
            canBePickedUp.Value = true;
        }
    }

    public void SetCanBePickedUp(bool canPickup)
    {
        if (IsServer)
        {
            canBePickedUp.Value = canPickup;
        }
    }

    public void SetPickupState(bool canPickup)
    {
        if (IsServer)
        {
            canBePickedUp.Value = canPickup;
        }

        if (itemCollider == null) itemCollider = GetComponent<Collider>();

        if (itemCollider != null)
        {
            itemCollider.enabled = canPickup;
        }
    }

    #endregion

    #region Public API

    public void ApplyThrowForce(Vector3 force)
    {
        if (IsServer)
        {
            SetThrowForce(force);
        }
    }

    public void Initialize(ItemData newItemData, bool canPickup = true)
    {
        if (IsServer)
        {
            SetItemData(newItemData);
            SetCanBePickedUp(canPickup);
        }
    }

    /// <summary>
    /// Masaya yerleştirildiğinde çağrılır
    /// </summary>
    public void PlaceOnTable()
    {
        if (IsServer)
        {
            FreezePhysics();
            EnablePickup();
        }
    }

    /// <summary>
    /// Masadan alındığında çağrılır
    /// </summary>
    public void RemoveFromTable()
    {
        if (IsServer)
        {
            UnfreezePhysics();
        }
    }

    #endregion
}