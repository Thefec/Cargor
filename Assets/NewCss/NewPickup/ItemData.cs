using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Items/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public GameObject visualPrefab; // Oyuncunun elinde görünecek prefab (NetworkObject OLMAYAN)
    public GameObject worldPrefab; // Yerde duran prefab (NetworkObject OLAN)
    public float throwForce = 10f;
    public int itemID; // Unique ID
    public ItemCategory itemCategory;
}