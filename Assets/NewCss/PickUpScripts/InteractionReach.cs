using System.Collections.Generic;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Masa/raf etkileşim hedeflemesi için TEK ortak kural: hedefin fiziksel gövde
    /// collider'ları üzerinde en yakın yüzey noktasına yatay mesafe.
    ///
    /// Kök neden (bkz. plans/imdi-ald-m-bir-tak-m-indexed-lamport.md bölüm 3): eskiden
    /// client hedeflemesi masanın PİVOT noktasına dar bir koniyle bakıyordu, server ise
    /// oriented-box VEYA pivota düz mesafeyle ayrı bir kural kullanıyordu, outline ise
    /// üçüncü bir trigger-collider kuralıyla yanıyordu. Üç ayrı kural aynı anda "doğru"
    /// olmadığından outline yanıp tıklama reddediliyordu. Bu yardımcı, client (hedef seçimi),
    /// server (yetki kontrolü) ve outline (görsel geri bildirim) için TEK hesaplamayı sağlar.
    /// </summary>
    public static class InteractionReach
    {
        // Tek thread'de (Unity main thread), reentrant OLMAYAN çağrılar için paylaşılan
        // scratch liste - her çağrı başında Clear'lanır, GetComponentsInChildren<T>(List<T>)
        // overload'u ile per-call array alloc'undan kaçınılır (bkz. QA bulgusu #1).
        private static readonly List<Collider> s_colliderScratch = new List<Collider>(16);

        /// <summary>
        /// Hedefin (masa/raf) kendi ve alt objelerindeki fiziksel gövde collider'ları arasında
        /// fromPosition'a en yakın yüzey noktasını bulur. Hariç tutulanlar:
        /// - Trigger collider'lar (etkileşim alanı/outline tetikleyicileri gibi fiziksel olmayan
        ///   collider'lar) - aksi halde büyük bir trigger collider gerçek gövdeden çok daha
        ///   "yakın" görünebilir.
        /// - NetworkWorldItem'a ait collider'lar - ShelfState rafa yerleştirilen kutuları slot
        ///   transform'una parent ediyor (bkz. ShelfState.cs:444,629), bu yüzden hedef rafın
        ///   GetComponentsInChildren taraması rafın ÜSTÜNDEKİ kutunun collider'ını da yakalar;
        ///   bu, "raf yüzeyi" değil "üstündeki eşya" olduğundan hariç tutulur (bkz. QA bulgusu #2).
        /// Collider bulunamazsa false döner; çağıran taraf transform.position'a düşebilir.
        /// </summary>
        public static bool TryGetClosestSurfacePoint(GameObject target, Vector3 fromPosition, out Vector3 closestPoint)
        {
            closestPoint = target != null ? target.transform.position : fromPosition;
            if (target == null) return false;

            s_colliderScratch.Clear();
            target.GetComponentsInChildren<Collider>(false, s_colliderScratch);

            if (s_colliderScratch.Count == 0) return false;

            bool found = false;
            float bestSqrDistance = float.MaxValue;

            foreach (var collider in s_colliderScratch)
            {
                if (collider == null || collider.isTrigger || !collider.enabled) continue;

                // Rafın üstüne parent edilmiş kutu/eşya collider'ı - hedefin kendi gövdesi değil.
                if (collider.GetComponentInParent<NetworkWorldItem>() != null) continue;

                Vector3 point = GetClosestPointSafe(collider, fromPosition);
                float sqrDistance = (point - fromPosition).sqrMagnitude;

                if (!found || sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    closestPoint = point;
                    found = true;
                }
            }

            s_colliderScratch.Clear();
            return found;
        }

        /// <summary>
        /// Collider.ClosestPoint, CONVEX OLMAYAN MeshCollider için desteklenmez (Unity bunu
        /// uyarı/hata olarak loglar). Bu durumda kaba ama güvenli bir fallback olarak
        /// world-space bounding box'ın en yakın noktasına düşülür (bkz. QA bulgusu #3).
        /// </summary>
        private static Vector3 GetClosestPointSafe(Collider collider, Vector3 fromPosition)
        {
            if (collider is MeshCollider meshCollider && !meshCollider.convex)
            {
                return collider.bounds.ClosestPoint(fromPosition);
            }

            return collider.ClosestPoint(fromPosition);
        }

        /// <summary>
        /// fromPosition'dan hedefin en yakın fiziksel yüzeyine YATAY (Y ekseni yoksayılan)
        /// mesafe. Fiziksel (trigger olmayan, hedefin kendi gövdesine ait) collider yoksa
        /// transform.position'a düşer.
        /// </summary>
        public static float GetHorizontalDistanceToSurface(GameObject target, Vector3 fromPosition)
        {
            if (target == null) return float.MaxValue;

            if (!TryGetClosestSurfacePoint(target, fromPosition, out Vector3 point))
            {
                point = target.transform.position;
            }

            Vector3 delta = point - fromPosition;
            delta.y = 0f;
            return delta.magnitude;
        }

        /// <summary>
        /// fromPosition, hedefin en yakın fiziksel yüzeyine yatay olarak reach mesafesi
        /// içinde mi?
        /// </summary>
        public static bool IsWithinReach(GameObject target, Vector3 fromPosition, float reach)
        {
            if (target == null) return false;
            return GetHorizontalDistanceToSurface(target, fromPosition) <= reach;
        }
    }
}
