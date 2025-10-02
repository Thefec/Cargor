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
    }
}