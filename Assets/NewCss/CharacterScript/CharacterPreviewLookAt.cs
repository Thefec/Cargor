using UnityEngine;

namespace NewCss.CharacterScript
{
    /// <summary>
    /// Karakterin kafasını (Animator IK kullanarak) belirli bir hedefe/kameraya baktırır.
    /// Bu scriptin çalışması için karakterin Animator'ünde "Humanoid" seçili olması
    /// ve ilgili Animation Layer'da "IK Pass" ayarının açık olması gerekir.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterPreviewLookAt : MonoBehaviour
    {
        [Header("Ayarlar")]
        [Tooltip("Karakterin bakmasını istediğiniz hedef (Örn: PreviewCamera)")]
        public Transform targetCamera;
        
        [Range(0f, 1f)]
        public float lookWeight = 1.0f;
        
        [Range(0f, 1f)]
        public float bodyWeight = 0.3f;
        
        [Range(0f, 1f)]
        public float headWeight = 1.0f;

        private Animator _animator;

        private void Start()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null) return;

            if (targetCamera != null)
            {
                // IK ağırlıklarını ayarla
                _animator.SetLookAtWeight(lookWeight, bodyWeight, headWeight, 0f, 0.5f);
                
                // Bakılacak pozisyonu ayarla (Kameranın pozisyonu)
                _animator.SetLookAtPosition(targetCamera.position);
            }
            else
            {
                // Hedef yoksa bakmayı bırak
                _animator.SetLookAtWeight(0f);
            }
        }
    }
}
