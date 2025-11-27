using System.Collections.Generic;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Sergi masası üzerinde birden fazla item'ı yönetir.
    /// Önceden tanımlanmış slot Transform'larına göre item'ları hizalar.
    /// Inspector'dan slot waypoint'leri atanabilir ve item'lar bu slotlara yerleştirilebilir.
    /// </summary>
    public class DisplayTable : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[DisplayTable]";
        private const string ITEMS_RESOURCE_PATH = "Items";
        private const string CLONE_SUFFIX = "(Clone)";

        #endregion

        #region Serialized Fields

        [Header("=== ITEM PREFABS ===")]
        [SerializeField, Tooltip("Bu masaya yerleştirilebilecek item prefab'ları listesi")]
        private GameObject[] itemPrefabs;

        [Header("=== SLOT CONFIGURATION ===")]
        [SerializeField, Tooltip("Item'lar için slot pozisyonlarını temsil eden boş GameObject'ler")]
        private Transform[] slotPoints;

        [Header("=== DEBUG SETTINGS ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool enableDebugLogs = false;

        [SerializeField, Tooltip("Slot Gizmo'larını göster")]
        private bool showSlotGizmos = true;

        [SerializeField, Tooltip("Slot Gizmo boyutu")]
        private float slotGizmoSize = 0.15f;

        #endregion

        #region Private Fields

        private readonly List<GameObject> _placedItems = new List<GameObject>();
        private ItemData[] _cachedItemData;
        private bool _isItemDataCached;

        #endregion

        #region Public Properties

        /// <summary>
        /// Masadaki mevcut item sayısı
        /// </summary>
        public int ItemCount => _placedItems.Count;

        /// <summary>
        /// Toplam slot sayısı
        /// </summary>
        public int SlotCount => slotPoints?.Length ?? 0;

        /// <summary>
        /// Kullanılabilir (boş) slot sayısı
        /// </summary>
        public int AvailableSlotCount => Mathf.Max(0, SlotCount - ItemCount);

        /// <summary>
        /// Masa dolu mu?
        /// </summary>
        public bool IsFull => slotPoints != null && _placedItems.Count >= slotPoints.Length;

        /// <summary>
        /// Masada item var mı?
        /// </summary>
        public bool HasItems => _placedItems.Count > 0;

        /// <summary>
        /// Item prefab'larına erişim (backward compatibility)
        /// </summary>
        public GameObject[] ItemPrefabs => itemPrefabs;

        /// <summary>
        /// Slot noktalarına erişim (backward compatibility)
        /// </summary>
        public Transform[] SlotPoints => slotPoints;

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            ClearItemDataCache();
        }

        #endregion

        #region Public API - Place Items

        /// <summary>
        /// Prefab index'ine göre item yerleştirir
        /// </summary>
        /// <param name="prefabIndex">itemPrefabs dizisindeki index</param>
        public void PlacePrefab(int prefabIndex)
        {
            // Validation
            if (!ValidatePrefabIndex(prefabIndex))
            {
                return;
            }

            if (!ValidateSlotAvailability())
            {
                return;
            }

            // Instantiate ve yerleştir
            var prefab = itemPrefabs[prefabIndex];
            var slot = GetNextAvailableSlot();

            var itemInstance = Instantiate(prefab, slot.position, slot.rotation);
            PlaceItemInstance(itemInstance);

            LogDebug($"Prefab placed at slot {_placedItems.Count - 1}: {prefab.name}");
        }

        /// <summary>
        /// Visual prefab'ı (GameObject) slot'a yerleştirir
        /// </summary>
        /// <param name="visualPrefab">Yerleştirilecek visual prefab</param>
        public void PlaceVisualItem(GameObject visualPrefab)
        {
            if (!ValidateVisualPrefab(visualPrefab))
            {
                return;
            }

            if (!ValidateSlotAvailability())
            {
                return;
            }

            int slotIndex = _placedItems.Count;
            var slot = slotPoints[slotIndex];

            // Instantiate
            var itemInstance = Instantiate(visualPrefab, slot.position, slot.rotation);

            // Parent ayarla
            itemInstance.transform.SetParent(slot, true);

            // Physics devre dışı (sadece visual)
            DisablePhysicsForVisual(itemInstance);

            _placedItems.Add(itemInstance);

            LogDebug($"Visual item placed at slot {slotIndex}: {visualPrefab.name}");
        }

        /// <summary>
        /// ItemData'dan visual item yerleştirir
        /// </summary>
        /// <param name="itemData">Yerleştirilecek item'ın data'sı</param>
        public void PlaceVisualItem(ItemData itemData)
        {
            if (itemData == null)
            {
                LogWarning("Cannot place visual item: ItemData is null");
                return;
            }

            if (itemData.visualPrefab == null)
            {
                LogWarning($"Cannot place visual item: visualPrefab is null for {itemData.itemName}");
                return;
            }

            PlaceVisualItem(itemData.visualPrefab);
        }

        /// <summary>
        /// Mevcut bir GameObject instance'ını slot'a yerleştirir
        /// </summary>
        /// <param name="itemInstance">Yerleştirilecek instantiate edilmiş GameObject</param>
        public void PlaceItemInstance(GameObject itemInstance)
        {
            if (!ValidateItemInstance(itemInstance))
            {
                return;
            }

            if (!ValidateSlotAvailability())
            {
                return;
            }

            int slotIndex = _placedItems.Count;
            var slot = slotPoints[slotIndex];

            // Sadece pozisyon ve rotation ayarla (parent atama yok)
            itemInstance.transform.position = slot.position;
            itemInstance.transform.rotation = slot.rotation;

            _placedItems.Add(itemInstance);

            LogDebug($"Item instance placed at slot {slotIndex}: {itemInstance.name}");
        }

        #endregion

        #region Public API - Take Items

        /// <summary>
        /// İlk yerleştirilen item'ı alır ve döndürür (FIFO)
        /// </summary>
        /// <returns>Alınan GameObject veya boşsa null</returns>
        public GameObject TakeItem()
        {
            if (!HasItems)
            {
                LogWarning("No items to take");
                return null;
            }

            var firstItem = _placedItems[0];
            _placedItems.RemoveAt(0);

            // Kalan item'ları yeniden konumlandır
            RepositionItems();

            LogDebug($"Item taken: {firstItem.name}, remaining: {_placedItems.Count}");

            return firstItem;
        }

        /// <summary>
        /// İlk item'ın ItemData'sını alır, visual'ı yok eder
        /// </summary>
        /// <returns>Alınan item'ın ItemData'sı veya null</returns>
        public ItemData TakeItemData()
        {
            if (!HasItems)
            {
                LogWarning("No items to take");
                return null;
            }

            var firstItem = _placedItems[0];

            // Visual'dan ItemData'yı bul
            var itemData = GetItemDataFromVisual(firstItem);

            // Listeden çıkar ve yok et
            _placedItems.RemoveAt(0);
            Destroy(firstItem);

            // Kalan item'ları yeniden konumlandır
            RepositionItems();

            LogDebug($"ItemData taken: {itemData?.itemName ?? "null"}, remaining: {_placedItems.Count}");

            return itemData;
        }

        /// <summary>
        /// Belirli bir index'teki item'ı alır
        /// </summary>
        /// <param name="index">Alınacak item'ın index'i</param>
        /// <returns>Alınan GameObject veya null</returns>
        public GameObject TakeItemAt(int index)
        {
            if (!ValidateItemIndex(index))
            {
                return null;
            }

            var item = _placedItems[index];
            _placedItems.RemoveAt(index);

            RepositionItems();

            LogDebug($"Item taken at index {index}: {item.name}");

            return item;
        }

        #endregion

        #region Public API - Query

        /// <summary>
        /// Masada item var mı?  (backward compatibility)
        /// </summary>
        public bool HasItem()
        {
            return HasItems;
        }

        /// <summary>
        /// Belirli bir index'teki item'ı döndürür (almadan)
        /// </summary>
        /// <param name="index">İstenilen item'ın index'i</param>
        /// <returns>GameObject veya null</returns>
        public GameObject GetItemAt(int index)
        {
            if (!ValidateItemIndex(index))
            {
                return null;
            }

            return _placedItems[index];
        }

        /// <summary>
        /// Tüm yerleştirilmiş item'ların kopyasını döndürür
        /// </summary>
        public List<GameObject> GetAllItems()
        {
            return new List<GameObject>(_placedItems);
        }

        /// <summary>
        /// Belirli bir slot'un dolu olup olmadığını kontrol eder
        /// </summary>
        /// <param name="slotIndex">Kontrol edilecek slot index'i</param>
        public bool IsSlotOccupied(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _placedItems.Count;
        }

        #endregion

        #region Public API - Clear

        /// <summary>
        /// Tüm item'ları masadan temizler ve yok eder
        /// </summary>
        public void ClearItems()
        {
            int count = _placedItems.Count;

            foreach (var item in _placedItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            _placedItems.Clear();

            LogDebug($"Cleared {count} items from table");
        }

        /// <summary>
        /// Tüm item'ları yok etmeden masadan kaldırır
        /// </summary>
        public void RemoveAllItemsWithoutDestroy()
        {
            int count = _placedItems.Count;
            _placedItems.Clear();

            LogDebug($"Removed {count} items from table (not destroyed)");
        }

        #endregion

        #region Private Methods - Validation

        private bool ValidatePrefabIndex(int prefabIndex)
        {
            if (itemPrefabs == null || itemPrefabs.Length == 0)
            {
                LogWarning("Item prefabs array is null or empty");
                return false;
            }

            if (prefabIndex < 0 || prefabIndex >= itemPrefabs.Length)
            {
                LogWarning($"Invalid prefab index: {prefabIndex} (valid range: 0-{itemPrefabs.Length - 1})");
                return false;
            }

            if (itemPrefabs[prefabIndex] == null)
            {
                LogWarning($"Prefab at index {prefabIndex} is null");
                return false;
            }

            return true;
        }

        private bool ValidateSlotAvailability()
        {
            if (slotPoints == null || slotPoints.Length == 0)
            {
                LogWarning("Slot points array is null or empty");
                return false;
            }

            if (IsFull)
            {
                LogWarning($"No available slots (all {slotPoints.Length} slots occupied)");
                return false;
            }

            return true;
        }

        private bool ValidateVisualPrefab(GameObject visualPrefab)
        {
            if (visualPrefab == null)
            {
                LogWarning("Visual prefab is null");
                return false;
            }

            return true;
        }

        private bool ValidateItemInstance(GameObject itemInstance)
        {
            if (itemInstance == null)
            {
                LogWarning("Cannot place a null item instance");
                return false;
            }

            return true;
        }

        private bool ValidateItemIndex(int index)
        {
            if (index < 0 || index >= _placedItems.Count)
            {
                LogWarning($"Invalid item index: {index} (valid range: 0-{_placedItems.Count - 1})");
                return false;
            }

            return true;
        }

        #endregion

        #region Private Methods - Slot Management

        private Transform GetNextAvailableSlot()
        {
            int index = _placedItems.Count;
            return slotPoints[index];
        }

        /// <summary>
        /// Tüm item'ları slot pozisyonlarına göre yeniden konumlandırır
        /// </summary>
        private void RepositionItems()
        {
            for (int i = 0; i < _placedItems.Count; i++)
            {
                var item = _placedItems[i];
                var slot = slotPoints[i];

                if (item == null || slot == null) continue;

                // Parent'lı item'lar için parent'ı da güncelle
                if (item.transform.parent != null && IsSlotTransform(item.transform.parent))
                {
                    item.transform.SetParent(slot, true);
                }

                item.transform.position = slot.position;
                item.transform.rotation = slot.rotation;
            }

            LogDebug($"Repositioned {_placedItems.Count} items");
        }

        private bool IsSlotTransform(Transform parent)
        {
            if (slotPoints == null) return false;

            foreach (var slot in slotPoints)
            {
                if (slot == parent) return true;
            }

            return false;
        }

        #endregion

        #region Private Methods - Physics

        /// <summary>
        /// Visual item için physics'i devre dışı bırakır
        /// </summary>
        private static void DisablePhysicsForVisual(GameObject obj)
        {
            if (obj == null) return;

            // Rigidbody'leri kinematic yap
            var rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rigidbodies)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Collider'ları trigger yap
            var colliders = obj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.isTrigger = true;
            }
        }

        #endregion

        #region Private Methods - ItemData Lookup

        /// <summary>
        /// Visual object'ten ItemData'yı bulur
        /// </summary>
        private ItemData GetItemDataFromVisual(GameObject visualObject)
        {
            if (visualObject == null) return null;

            // Cache'i kontrol et veya oluştur
            EnsureItemDataCached();

            // İsim karşılaştırması için temizlenmiş isim
            string cleanName = GetCleanPrefabName(visualObject.name);

            // ItemData'yı ara
            foreach (var itemData in _cachedItemData)
            {
                if (itemData == null || itemData.visualPrefab == null) continue;

                if (itemData.visualPrefab.name == cleanName)
                {
                    return itemData;
                }
            }

            LogWarning($"ItemData not found for visual: {visualObject.name}");
            return null;
        }

        private void EnsureItemDataCached()
        {
            if (_isItemDataCached && _cachedItemData != null) return;

            _cachedItemData = Resources.LoadAll<ItemData>(ITEMS_RESOURCE_PATH);
            _isItemDataCached = true;

            LogDebug($"Cached {_cachedItemData.Length} ItemData assets");
        }

        private void ClearItemDataCache()
        {
            _cachedItemData = null;
            _isItemDataCached = false;
        }

        private static string GetCleanPrefabName(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName)) return string.Empty;

            // "(Clone)" suffix'ini kaldır
            if (instanceName.EndsWith(CLONE_SUFFIX))
            {
                return instanceName.Substring(0, instanceName.Length - CLONE_SUFFIX.Length);
            }

            return instanceName;
        }

        #endregion

        #region Private Methods - Logging

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Debug & Editor

        [ContextMenu("Debug: Print Table State")]
        private void DebugPrintTableState()
        {
            Debug.Log($"{LOG_PREFIX} === TABLE STATE ===");
            Debug.Log($"Total Slots: {SlotCount}");
            Debug.Log($"Placed Items: {ItemCount}");
            Debug.Log($"Available Slots: {AvailableSlotCount}");
            Debug.Log($"Is Full: {IsFull}");
            Debug.Log($"Has Items: {HasItems}");

            for (int i = 0; i < _placedItems.Count; i++)
            {
                var item = _placedItems[i];
                Debug.Log($"  Slot {i}: {(item != null ? item.name : "null")}");
            }
        }

        [ContextMenu("Clear All Items")]
        private void DebugClearAllItems()
        {
            ClearItems();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showSlotGizmos || slotPoints == null) return;

            DrawSlotGizmos();
        }

        private void DrawSlotGizmos()
        {
            for (int i = 0; i < slotPoints.Length; i++)
            {
                var slot = slotPoints[i];
                if (slot == null) continue;

                bool isOccupied = i < _placedItems.Count && _placedItems[i] != null;

                // Dolu slot: kırmızı, Boş slot: yeşil
                Gizmos.color = isOccupied ? Color.red : Color.green;
                Gizmos.DrawWireCube(slot.position, Vector3.one * slotGizmoSize);

                // Slot numarası için küçük küre
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(slot.position, slotGizmoSize * 0.3f);

                // Forward yönünü göster
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(slot.position, slot.position + slot.forward * slotGizmoSize * 2f);
            }

            // Slot'lar arası bağlantı çizgileri
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            for (int i = 0; i < slotPoints.Length - 1; i++)
            {
                if (slotPoints[i] != null && slotPoints[i + 1] != null)
                {
                    Gizmos.DrawLine(slotPoints[i].position, slotPoints[i + 1].position);
                }
            }
        }
#endif

        #endregion
    }
}