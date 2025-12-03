using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kameranın oyuncuyu takip etmesini sağlayan sistem.  
    /// Sabit açılı top-down görünüm ile smooth takip sunar.
    /// Network oyunlarında otomatik olarak local player'ı bulur.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CameraFollow]";
        private const float MIN_SMOOTH_SPEED = 0.1f;
        private const float TARGET_SEARCH_INTERVAL = 0.5f;

        #endregion

        #region Serialized Fields

        [Header("=== TARGET SETTINGS ===")]
        [SerializeField, Tooltip("Takip edilecek hedef Transform")]
        private Transform target;

        [SerializeField, Tooltip("Hedef bulunamazsa otomatik ara")]
        private bool autoFindTarget = true;

        [Header("=== POSITION SETTINGS ===")]
        [SerializeField, Tooltip("Kamera offset'i (hedeften uzaklık)")]
        private Vector3 offset = new Vector3(0f, 10f, -10f);

        [SerializeField, Tooltip("Kamera takip hızı")]
        [Range(0.1f, 20f)]
        private float smoothSpeed = 5f;

        [Header("=== ROTATION SETTINGS ===")]
        [SerializeField, Tooltip("Sabit X rotasyonu (eğim açısı)")]
        [Range(0f, 90f)]
        private float fixedRotationX = 45f;

        [SerializeField, Tooltip("Sabit Y rotasyonu")]
        private float fixedRotationY = 0f;

        [Header("=== ZOOM SETTINGS ===")]
        [SerializeField, Tooltip("Zoom tuşu")]
        private KeyCode zoomKey = KeyCode.Z;

        [SerializeField, Tooltip("Zoom yapıldığında hedef offset")]
        private Vector3 zoomedOffset = new Vector3(0f, 5f, -5f);

        [SerializeField, Tooltip("Zoom geçiş hızı")]
        [Range(0.1f, 20f)]
        private float zoomTransitionSpeed = 8f;

        [Header("=== BOUNDS (OPTIONAL) ===")]
        [SerializeField, Tooltip("Kamera sınırlarını kullan")]
        private bool useBounds;

        [SerializeField, Tooltip("Minimum sınır")]
        private Vector3 minBounds = new Vector3(-50f, 0f, -50f);

        [SerializeField, Tooltip("Maximum sınır")]
        private Vector3 maxBounds = new Vector3(50f, 20f, 50f);

        #endregion

        #region Private Fields

        private bool _isTargetSet;
        private float _lastTargetSearchTime;
        private Vector3 _currentVelocity;
        private Vector3 _defaultOffset;
        private Vector3 _currentOffset;
        private bool _isZooming;

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut hedef Transform (backward compatibility)
        /// </summary>
        public Transform Target
        {
            get => target;
            set => SetTarget(value);
        }

        /// <summary>
        /// Kamera offset'i
        /// </summary>
        public Vector3 Offset
        {
            get => offset;
            set
            {
                offset = value;
                _defaultOffset = value;
            }
        }

        /// <summary>
        /// Takip hızı
        /// </summary>
        public float SmoothSpeed
        {
            get => smoothSpeed;
            set => smoothSpeed = Mathf.Max(MIN_SMOOTH_SPEED, value);
        }

        /// <summary>
        /// Hedef atanmış mı?
        /// </summary>
        public bool HasTarget => target != null && _isTargetSet;

        /// <summary>
        /// X rotasyonu (eğim)
        /// </summary>
        public float FixedRotationX
        {
            get => fixedRotationX;
            set => fixedRotationX = Mathf.Clamp(value, 0f, 90f);
        }

        /// <summary>
        /// Zoom aktif mi?
        /// </summary>
        public bool IsZooming => _isZooming;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            HandleZoomInput();
        }

        private void LateUpdate()
        {
            UpdateCamera();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            // Varsayılan offset'i kaydet
            _defaultOffset = offset;
            _currentOffset = offset;

            if (target != null)
            {
                _isTargetSet = true;
                LogDebug($"Target pre-assigned: {target.name}");
                return;
            }

            if (autoFindTarget)
            {
                TryFindLocalPlayer();
            }
        }

        #endregion

        #region Zoom Handling

        private void HandleZoomInput()
        {
            // Z tuşuna basılı tutulduğunda zoom yap
            _isZooming = Input.GetKey(zoomKey);

            // Hedef offset'i belirle
            Vector3 targetOffset = _isZooming ? zoomedOffset : _defaultOffset;

            // Smooth geçiş
            _currentOffset = Vector3.Lerp(
                _currentOffset,
                targetOffset,
                zoomTransitionSpeed * Time.deltaTime
            );
        }

        #endregion

        #region Camera Update

        private void UpdateCamera()
        {
            // Hedef yoksa ara
            if (!HasValidTarget())
            {
                HandleMissingTarget();
                return;
            }

            // Kamera pozisyonunu güncelle
            UpdatePosition();

            // Kamera rotasyonunu güncelle
            UpdateRotation();
        }

        private bool HasValidTarget()
        {
            return target != null;
        }

        private void HandleMissingTarget()
        {
            if (!autoFindTarget || _isTargetSet)
            {
                return;
            }

            // Throttled arama (her frame aramaktan kaçın)
            if (Time.time - _lastTargetSearchTime < TARGET_SEARCH_INTERVAL)
            {
                return;
            }

            _lastTargetSearchTime = Time.time;
            TryFindLocalPlayer();
        }

        private void UpdatePosition()
        {
            Vector3 targetPosition = CalculateTargetPosition();

            // Sınırları uygula
            if (useBounds)
            {
                targetPosition = ClampToBounds(targetPosition);
            }

            // Smooth takip
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                smoothSpeed * Time.deltaTime
            );
        }

        private Vector3 CalculateTargetPosition()
        {
            // Zoom durumuna göre güncel offset kullan
            return target.position + _currentOffset;
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, minBounds.x, maxBounds.x),
                Mathf.Clamp(position.y, minBounds.y, maxBounds.y),
                Mathf.Clamp(position.z, minBounds.z, maxBounds.z)
            );
        }

        private void UpdateRotation()
        {
            transform.rotation = Quaternion.Euler(fixedRotationX, fixedRotationY, 0f);
        }

        #endregion

        #region Target Finding

        private void TryFindLocalPlayer()
        {
            // Yöntem 1: PlayerMovement ile ara
            if (TryFindByPlayerMovement())
            {
                return;
            }

            // Yöntem 2: NetworkManager ile ara
            if (TryFindByNetworkManager())
            {
                return;
            }

            // Yöntem 3: Tag ile ara
            TryFindByTag();
        }

        private bool TryFindByPlayerMovement()
        {
            var players = FindObjectsOfType<PlayerMovement>();

            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    SetTargetInternal(player.transform, "PlayerMovement.  IsOwner");
                    return true;
                }
            }

            return false;
        }

        private bool TryFindByNetworkManager()
        {
            if (NetworkManager.Singleton == null)
            {
                return false;
            }

            var localClient = NetworkManager.Singleton.LocalClient;
            if (localClient?.PlayerObject == null)
            {
                return false;
            }

            SetTargetInternal(localClient.PlayerObject.transform, "NetworkManager. LocalClient");
            return true;
        }

        private bool TryFindByTag()
        {
            var playerObject = GameObject.FindWithTag("Player");

            if (playerObject == null)
            {
                return false;
            }

            // Verify it's the local player if in network mode
            var networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.IsOwner)
            {
                return false;
            }

            SetTargetInternal(playerObject.transform, "Tag:Player");
            return true;
        }

        private void SetTargetInternal(Transform newTarget, string source)
        {
            target = newTarget;
            _isTargetSet = true;

            LogDebug($"Target found via {source}: {newTarget.name}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manuel olarak hedef atar
        /// </summary>
        /// <param name="newTarget">Yeni hedef Transform</param>
        public void SetTarget(Transform newTarget)
        {
            if (newTarget == null)
            {
                LogWarning("SetTarget called with null target");
                return;
            }

            target = newTarget;
            _isTargetSet = true;

            LogDebug($"Target manually set: {newTarget.name}");
        }

        /// <summary>
        /// Hedefi temizler
        /// </summary>
        public void ClearTarget()
        {
            target = null;
            _isTargetSet = false;

            LogDebug("Target cleared");
        }

        /// <summary>
        /// Kamerayı anında hedefe taşır (smooth yok)
        /// </summary>
        public void SnapToTarget()
        {
            if (!HasValidTarget())
            {
                LogWarning("SnapToTarget called but no valid target");
                return;
            }

            Vector3 targetPosition = CalculateTargetPosition();

            if (useBounds)
            {
                targetPosition = ClampToBounds(targetPosition);
            }

            transform.position = targetPosition;
            UpdateRotation();

            LogDebug("Camera snapped to target");
        }

        /// <summary>
        /// Offset'i değiştirir
        /// </summary>
        /// <param name="newOffset">Yeni offset değeri</param>
        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
            _defaultOffset = newOffset;
            _currentOffset = newOffset;
        }

        /// <summary>
        /// Zoom offset'ini değiştirir
        /// </summary>
        /// <param name="newZoomedOffset">Yeni zoom offset değeri</param>
        public void SetZoomedOffset(Vector3 newZoomedOffset)
        {
            zoomedOffset = newZoomedOffset;
        }

        /// <summary>
        /// Zoom yapar (Y offset'i değiştirir)
        /// </summary>
        /// <param name="zoomAmount">Zoom miktarı (pozitif = yakınlaş, negatif = uzaklaş)</param>
        public void Zoom(float zoomAmount)
        {
            offset.y = Mathf.Max(1f, offset.y - zoomAmount);
            _defaultOffset = offset;
        }

        /// <summary>
        /// Hedefi yeniden aramaya zorlar
        /// </summary>
        public void ForceRefindTarget()
        {
            _isTargetSet = false;
            _lastTargetSearchTime = 0f;
            target = null;

            TryFindLocalPlayer();
        }

        #endregion

        #region Logging

        private static void LogDebug(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"{LOG_PREFIX} {message}");
#endif
        }

        private static void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor & Debug

#if UNITY_EDITOR
        [ContextMenu("Snap To Target")]
        private void DebugSnapToTarget()
        {
            SnapToTarget();
        }

        [ContextMenu("Force Refind Target")]
        private void DebugForceRefindTarget()
        {
            ForceRefindTarget();
        }

        [ContextMenu("Clear Target")]
        private void DebugClearTarget()
        {
            ClearTarget();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === CAMERA STATE ===");
            Debug.Log($"Target: {(target != null ? target.name : "null")}");
            Debug.Log($"Is Target Set: {_isTargetSet}");
            Debug.Log($"Has Valid Target: {HasValidTarget()}");
            Debug.Log($"Offset: {offset}");
            Debug.Log($"Current Offset: {_currentOffset}");
            Debug.Log($"Is Zooming: {_isZooming}");
            Debug.Log($"Smooth Speed: {smoothSpeed}");
            Debug.Log($"Fixed Rotation X: {fixedRotationX}");
            Debug.Log($"Use Bounds: {useBounds}");
        }

        private void OnDrawGizmosSelected()
        {
            DrawTargetGizmo();
            DrawBoundsGizmo();
            DrawOffsetGizmo();
        }

        private void DrawTargetGizmo()
        {
            if (target == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.5f);
            Gizmos.DrawLine(transform.position, target.position);
        }

        private void DrawBoundsGizmo()
        {
            if (!useBounds) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 center = (minBounds + maxBounds) / 2f;
            Vector3 size = maxBounds - minBounds;
            Gizmos.DrawWireCube(center, size);
        }

        private void DrawOffsetGizmo()
        {
            if (target == null) return;

            Gizmos.color = Color.cyan;
            Vector3 targetPos = target.position + _currentOffset;
            Gizmos.DrawWireCube(targetPos, Vector3.one * 0.3f);
            Gizmos.DrawLine(target.position, targetPos);

            // Zoomed offset preview
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Vector3 zoomedPos = target.position + zoomedOffset;
            Gizmos.DrawWireCube(zoomedPos, Vector3.one * 0.2f);
        }
#endif

        #endregion
    }
}