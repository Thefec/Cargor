using UnityEngine;

namespace NewCss
{
    public class BoxInfo : MonoBehaviour
    {
        public enum BoxType
        {
            Yellow,
            Blue,
            Red
        }

        public BoxType boxType;
        public bool isFull = false;

        /// <summary>
        /// Bu kutu raf görevlerinde bir kez sayıldı mı? "Rafa koy → geri al → tekrar koy"
        /// döngüsüyle aynı kutunun quest ilerlemesini defalarca artırmasını engeller
        /// (ShelfState.PlaceBoxOnShelf → QuestTracker.NotifyBoxPlacedOnShelf).
        /// Serialize EDİLMEZ: sahneye/prefab'a yazılmaz, kutu örneğiyle birlikte yok olur.
        /// </summary>
        [System.NonSerialized] public bool countedForShelfQuest = false;
    }
}