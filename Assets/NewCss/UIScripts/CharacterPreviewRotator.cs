using UnityEngine;
using UnityEngine.EventSystems;

namespace NewCss.UIScripts
{
    /// <summary>
    /// 3D karakter önizlemesini fare sürüklemesiyle Y ekseninde 360° döndürür.
    /// Bu scripti, 3D önizlemenin üzerindeki görünmez bir UI Image'a ekleyin.
    /// Image'ın Raycast Target özelliği aktif olmalıdır.
    /// </summary>
    public class CharacterPreviewRotator : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Constants

        private const string LOG_PREFIX = "[PreviewRotator]";

        #endregion

        #region Serialized Fields

        [Header("=== DÖNDÜRME AYARLARI ===")]
        [SerializeField, Tooltip("Döndürülecek 3D karakter Transform'u")]
        private Transform targetCharacter;

        [SerializeField, Tooltip("Döndürme hızı çarpanı")]
        private float rotationSpeed = 0.5f;

        [SerializeField, Tooltip("Döndürme yönünü ters çevir")]
        private bool invertDirection;

        [Header("=== OTOMATİK DÖNDÜRME ===")]
        [SerializeField, Tooltip("Fare bırakıldığında yavaşça durma etkisi (inertia)")]
        private bool enableInertia = true;

        [SerializeField, Tooltip("Yavaşlama hızı (daha yüksek = daha hızlı durur)")]
        private float inertiaDamping = 5f;

        [Header("=== HATA AYIKLAMA ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs;

        #endregion

        #region Private Fields

        private float _currentVelocity;
        private bool _isDragging;

        #endregion

        #region Public Properties

        /// <summary>
        /// Döndürülecek hedef karakteri ayarlar
        /// </summary>
        public Transform TargetCharacter
        {
            get => targetCharacter;
            set => targetCharacter = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (_isDragging || targetCharacter == null) return;

            // Atalet (inertia) etkisi - fare bırakıldıktan sonra yavaşça durma
            if (enableInertia && Mathf.Abs(_currentVelocity) > 0.01f)
            {
                targetCharacter.Rotate(Vector3.up, _currentVelocity * Time.deltaTime, Space.World);
                _currentVelocity = Mathf.Lerp(_currentVelocity, 0f, inertiaDamping * Time.deltaTime);
            }
            else
            {
                _currentVelocity = 0f;
            }
        }

        #endregion

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _currentVelocity = 0f;
            LogDebug("Sürükleme başladı");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (targetCharacter == null) return;

            float delta = eventData.delta.x * rotationSpeed;

            if (invertDirection)
            {
                delta = -delta;
            }

            targetCharacter.Rotate(Vector3.up, -delta, Space.World);

            // Atalet için hız kaydet
            _currentVelocity = -delta / Time.deltaTime;

            LogDebug($"Döndürme delta: {delta:F2}");
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            LogDebug($"Sürükleme bitti, hız: {_currentVelocity:F2}");
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        #endregion
    }
}
