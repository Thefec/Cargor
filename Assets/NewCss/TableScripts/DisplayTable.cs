using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    /// <summary>
    /// Manages multiple items placed on a display table, aligning them according to predefined slot Transforms.
    /// Allows assigning slot waypoints in inspector, and placing items (by prefab or instance) into these slots.
    /// </summary>
    public class DisplayTable : MonoBehaviour
    {
        [Header("Item Prefabs")] [Tooltip("List of item prefabs that can be placed on this table.")]
        public GameObject[] itemPrefabs;

        [Header("Slot Transforms")] [Tooltip("Ordered list of empty GameObjects representing slots for items.")]
        public Transform[] slotPoints;

        private readonly List<GameObject> placedItems = new List<GameObject>();

        /// <summary>
        /// Returns how many items are currently on the table.
        /// </summary>
        public int ItemCount => placedItems.Count;

        /// <summary>
        /// Instantiates and places a prefab by index into the next available slot.
        /// </summary>
        /// <param name="prefabIndex">Index into itemPrefabs array.</param>
        public void PlacePrefab(int prefabIndex)
        {
            if (itemPrefabs == null || prefabIndex < 0 || prefabIndex >= itemPrefabs.Length)
            {
                Debug.LogWarning($"DisplayTable: Invalid prefab index {prefabIndex}.");
                return;
            }

            if (slotPoints == null || placedItems.Count >= slotPoints.Length)
            {
                Debug.LogWarning("DisplayTable: No available slots to place item.");
                return;
            }

            GameObject prefab = itemPrefabs[prefabIndex];
            GameObject itemInstance = Instantiate(prefab, slotPoints[placedItems.Count].position,
                slotPoints[placedItems.Count].rotation);
            PlaceItemInstance(itemInstance);
        }

        public void PlaceVisualItem(GameObject visualPrefab)
        {
            if (visualPrefab == null || slotPoints == null || placedItems.Count >= slotPoints.Length)
                return;

            int index = placedItems.Count;
            GameObject itemInstance = Instantiate(visualPrefab, slotPoints[index].position, slotPoints[index].rotation);

            // Visual item'ı parent yap
            itemInstance.transform.SetParent(slotPoints[index], true);

            // Physics'i kapat (visual sadece)
            DisablePhysicsForVisual(itemInstance);

            placedItems.Add(itemInstance);
        }

        private void DisablePhysicsForVisual(GameObject obj)
        {
            // Rigidbody'leri kinematic yap
            Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in rigidbodies)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Collider'ları trigger yap veya kapat
            Collider[] colliders = obj.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                col.isTrigger = true; // veya col.enabled = false;
            }
        }

        private ItemData GetItemDataFromVisual(GameObject visualObject)
        {
            ItemData[] allItems = Resources.LoadAll<ItemData>("Items");
            foreach (ItemData item in allItems)
            {
                if (item.visualPrefab != null &&
                    item.visualPrefab.name == visualObject.name.Replace("(Clone)", ""))
                {
                    return item;
                }
            }

            return null;
        }

        public ItemData TakeItemData()
        {
            if (placedItems.Count == 0)
                return null;

            GameObject first = placedItems[0];
        
            // Visual'dan ItemData'yı bul
            ItemData itemData = GetItemDataFromVisual(first);
        
            // Visual'ı yok et
            placedItems.RemoveAt(0);
            Destroy(first);
        
            // Kalan itemları yeniden konumlandır
            RepositionItems();
        
            return itemData;
        }
        public void PlaceVisualItem(ItemData itemData)
        {
            if (itemData?.visualPrefab != null)
            {
                PlaceVisualItem(itemData.visualPrefab);
            }
        }

        /// <summary>
        /// Places an existing GameObject instance into the next slot.
        /// </summary>
        /// <param name="itemInstance">The instantiated GameObject.</param>
        public void PlaceItemInstance(GameObject itemInstance)
        {
            int index = placedItems.Count;
            if (itemInstance == null)
            {
                Debug.LogWarning("DisplayTable: Cannot place a null item instance.");
                return;
            }

            if (slotPoints == null || index >= slotPoints.Length)
            {
                Debug.LogWarning("DisplayTable: No available slots for the item instance.");
                return;
            }

            // Sadece pozisyon ve rotation ayarla, parent atama
            itemInstance.transform.position = slotPoints[index].position;
            itemInstance.transform.rotation = slotPoints[index].rotation;

            placedItems.Add(itemInstance);
        }


        /// <summary>
        /// Returns true if there are any items on the table.
        /// </summary>
        public bool HasItem()
        {
            return placedItems.Count > 0;
        }

        /// <summary>
        /// Removes and returns the first-placed item (FIFO).
        /// </summary>
        /// <returns>The removed GameObject, or null if empty.</returns>
        public GameObject TakeItem()
        {
            if (placedItems.Count == 0)
            {
                Debug.LogWarning("DisplayTable: No items to take.");
                return null;
            }

            GameObject first = placedItems[0];
            placedItems.RemoveAt(0);

            // Shift remaining items: reassign them to earlier slots
            RepositionItems();
            return first;
        }

        /// <summary>
        /// Clears all items from the table.
        /// </summary>
        public void ClearItems()
        {
            foreach (var item in placedItems)
                Destroy(item);
            placedItems.Clear();
        }

        /// <summary>
        /// Repositions all placed items according to slotPoints.
        /// </summary>
        private void RepositionItems()
        {
            for (int i = 0; i < placedItems.Count; i++)
            {
                GameObject item = placedItems[i];
                var slot = slotPoints[i];

                // Sadece pozisyon ve rotation ayarla
                item.transform.position = slot.position;
                item.transform.rotation = slot.rotation;
            }
        }
    }
}