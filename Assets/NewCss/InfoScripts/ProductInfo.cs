namespace NewCss
{
    using UnityEngine;

    public class ProductInfo : MonoBehaviour
    {
        public enum ProductType
        {
            Toy,
            Clothing,
            Glass
        }

        public ProductType productType;
    }
}