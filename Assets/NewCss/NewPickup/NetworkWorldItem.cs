using Unity;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

// World'deki pickup edilebilir item (NetworkObject olarak)
public class NetworkWorldItem : NetworkBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider itemCollider;
    
    // Network variables for synchronized state
    private NetworkVariable<int> networkItemID = new NetworkVariable<int>(-1);
    private NetworkVariable<bool> canBePickedUp = new NetworkVariable<bool>(true);

    public ItemData ItemData => itemData;
    public bool CanBePickedUp => canBePickedUp.Value;

    private void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (itemCollider == null) itemCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Initialize pickup state
        if (IsServer)
        {
            canBePickedUp.Value = true;
        }
        
        // Listen for item ID changes to update local ItemData
        networkItemID.OnValueChanged += OnItemIDChanged;
        
        // Update ItemData if we already have an ID
        if (networkItemID.Value != -1)
        {
            UpdateItemDataFromID(networkItemID.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkItemID.OnValueChanged -= OnItemIDChanged;
        base.OnNetworkDespawn();
    }

    private void OnItemIDChanged(int previousValue, int newValue)
    {
        if (newValue != -1)
        {
            UpdateItemDataFromID(newValue);
        }
    }

    private void UpdateItemDataFromID(int itemID)
    {
        // Load ItemData from Resources using the ID
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

    public void SetThrowForce(Vector3 force)
    {
        if (!IsServer) return;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Eğer kinematic ise, önce kinematic'i kapat
            if (rb.isKinematic)
            {
                rb.isKinematic = false;
            }
        
            // Gravity'yi aç
            rb.useGravity = true;
        
            // Collision detection'ı aç
            rb.detectCollisions = true;
        
            // Force'u uygula
            rb.AddForce(force, ForceMode.VelocityChange);
        
            Debug.Log($"Throw force applied: {force} to {gameObject.name}");
        }
        else
        {
            Debug.LogError($"No Rigidbody found on {gameObject.name} - cannot apply throw force");
        }
    }

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

    public void SetItemData(ItemData newItemData)
    {
        if (IsServer && newItemData != null)
        {
            // Set local reference
            itemData = newItemData;
            
            // Update network variable so all clients know the item ID
            networkItemID.Value = newItemData.itemID;
            
            Debug.Log($"Item data set: {newItemData.itemName} (ID: {newItemData.itemID})");
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
        // NetworkVariable'a değer atarken .Value kullan
        if (IsServer)
        {
            this.canBePickedUp.Value = canPickup;
        }

        // Eğer item alınamaz duruma geçerse, collider'ı da deaktive et
        if (!canPickup)
        {
            Collider itemCollider = GetComponent<Collider>();
            if (itemCollider != null)
            {
                itemCollider.enabled = false;
            }
        }
        else
        {
            // Eğer tekrar alınabilir duruma geçerse, collider'ı aktive et
            Collider itemCollider = GetComponent<Collider>();
            if (itemCollider != null)
            {
                itemCollider.enabled = true;
            }
        }
    }
    
    
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
}