using System;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Müşteri mesh değiştirici - customer'ların spawn'da rastgele kıyafet/görünüm almasını sağlar.
    /// NetworkCharacterMeshSwapper'ın sadeleştirilmiş kardeşi: PlayerPrefs kayıt/yükleme YOK,
    /// yazma yetkisi Owner değil Server (customer'ların oyuncu owner'ı yok, server spawn ediyor).
    /// </summary>
    [ExecuteInEditMode]
    public class CustomerMeshSwapper : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CustomerMeshSwapper]";
        private const int DEFAULT_INDEX = 0;

        // Renderer Names
        private const string RENDERER_ACCESSORIES = "Accessories";
        private const string RENDERER_FACES = "Faces";
        private const string RENDERER_GLASSES = "Glasses";
        private const string RENDERER_GLOVES = "Gloves";
        private const string RENDERER_HAIRSTYLE = "Hairstyle";
        private const string RENDERER_HAT = "Hat";
        private const string RENDERER_OUTERWEAR = "Outerwear";
        private const string RENDERER_PANTS = "Pants";
        private const string RENDERER_SHOES = "Shoes";
        private const string RENDERER_BODY = "Body";

        #endregion

        #region Enums

        /// <summary>
        /// Özelleştirme kategorileri
        /// </summary>
        public enum CustomizationPart
        {
            Accessories,
            Faces,
            Glasses,
            Gloves,
            Hairstyle,
            Hat,
            Outerwear,
            Pants,
            Shoes,
            SkinColor
        }

        #endregion

        #region Serialized Fields - Meshes

        [Header("=== MESH ARRAYS ===")]
        [SerializeField, Tooltip("Aksesuar mesh'leri")]
        public Mesh[] accessoriesMeshes;

        [SerializeField, Tooltip("Yüz mesh'leri")]
        public Mesh[] facesMeshes;

        [SerializeField, Tooltip("Gözlük mesh'leri")]
        public Mesh[] glassesMeshes;

        [SerializeField, Tooltip("Eldiven mesh'leri")]
        public Mesh[] glovesMeshes;

        [SerializeField, Tooltip("Saç mesh'leri")]
        public Mesh[] hairstyleMeshes;

        [SerializeField, Tooltip("Şapka mesh'leri")]
        public Mesh[] hatMeshes;

        [SerializeField, Tooltip("Üst giysi mesh'leri")]
        public Mesh[] outerwearMeshes;

        [SerializeField, Tooltip("Pantolon mesh'leri")]
        public Mesh[] pantsMeshes;

        [SerializeField, Tooltip("Ayakkabı mesh'leri")]
        public Mesh[] shoesMeshes;

        #endregion

        #region Serialized Fields - Skin Colors

        [Header("=== SKIN COLORS ===")]
        [SerializeField, Tooltip("Ten renkleri")]
        public Color[] skinColors = new Color[]
        {
            new Color(1f, 0.8f, 0.7f),      // Light
            new Color(0.9f, 0.7f, 0.5f),    // Medium
            new Color(0.8f, 0.6f, 0.4f),    // Tan
            new Color(0.6f, 0.4f, 0.3f),    // Dark
            new Color(0.4f, 0.3f, 0.2f)     // Very Dark
        };

        #endregion

        #region Network Variables

        private readonly NetworkVariable<int> _accessoriesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _facesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _glassesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _glovesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _hairstyleIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _hatIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _outerwearIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _pantsIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _shoesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _skinColorIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Cached Renderers

        [NonSerialized] public SkinnedMeshRenderer accessoriesRenderer;
        [NonSerialized] public SkinnedMeshRenderer facesRenderer;
        [NonSerialized] public SkinnedMeshRenderer glassesRenderer;
        [NonSerialized] public SkinnedMeshRenderer glovesRenderer;
        [NonSerialized] public SkinnedMeshRenderer hairstyleRenderer;
        [NonSerialized] public SkinnedMeshRenderer hatRenderer;
        [NonSerialized] public SkinnedMeshRenderer outerwearRenderer;
        [NonSerialized] public SkinnedMeshRenderer pantsRenderer;
        [NonSerialized] public SkinnedMeshRenderer shoesRenderer;
        [NonSerialized] public SkinnedMeshRenderer skinRenderer;

        #endregion

        #region Events

        /// <summary>
        /// Özelleştirme değiştiğinde tetiklenir
        /// </summary>
        public event Action<CustomizationPart, int> OnCustomizationChanged;

        #endregion

        #region Public Properties

        /// <summary>
        /// Aksesuar index'i
        /// </summary>
        public int AccessoriesIndex => _accessoriesIndex.Value;

        /// <summary>
        /// Yüz index'i
        /// </summary>
        public int FacesIndex => _facesIndex.Value;

        /// <summary>
        /// Gözlük index'i
        /// </summary>
        public int GlassesIndex => _glassesIndex.Value;

        /// <summary>
        /// Eldiven index'i
        /// </summary>
        public int GlovesIndex => _glovesIndex.Value;

        /// <summary>
        /// Saç index'i
        /// </summary>
        public int HairstyleIndex => _hairstyleIndex.Value;

        /// <summary>
        /// Şapka index'i
        /// </summary>
        public int HatIndex => _hatIndex.Value;

        /// <summary>
        /// Üst giysi index'i
        /// </summary>
        public int OuterwearIndex => _outerwearIndex.Value;

        /// <summary>
        /// Pantolon index'i
        /// </summary>
        public int PantsIndex => _pantsIndex.Value;

        /// <summary>
        /// Ayakkabı index'i
        /// </summary>
        public int ShoesIndex => _shoesIndex.Value;

        /// <summary>
        /// Ten rengi index'i
        /// </summary>
        public int SkinColorIndex => _skinColorIndex.Value;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (!Application.isPlaying || NetworkManager.Singleton == null)
            {
                FindRenderers();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                FindRenderers();
                SwapAllMeshes();
            }
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            FindRenderers();
            SubscribeToNetworkEvents();
            ApplyAllCustomizations();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _accessoriesIndex.OnValueChanged += HandleAccessoriesChanged;
            _facesIndex.OnValueChanged += HandleFacesChanged;
            _glassesIndex.OnValueChanged += HandleGlassesChanged;
            _glovesIndex.OnValueChanged += HandleGlovesChanged;
            _hairstyleIndex.OnValueChanged += HandleHairstyleChanged;
            _hatIndex.OnValueChanged += HandleHatChanged;
            _outerwearIndex.OnValueChanged += HandleOuterwearChanged;
            _pantsIndex.OnValueChanged += HandlePantsChanged;
            _shoesIndex.OnValueChanged += HandleShoesChanged;
            _skinColorIndex.OnValueChanged += HandleSkinColorChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _accessoriesIndex.OnValueChanged -= HandleAccessoriesChanged;
            _facesIndex.OnValueChanged -= HandleFacesChanged;
            _glassesIndex.OnValueChanged -= HandleGlassesChanged;
            _glovesIndex.OnValueChanged -= HandleGlovesChanged;
            _hairstyleIndex.OnValueChanged -= HandleHairstyleChanged;
            _hatIndex.OnValueChanged -= HandleHatChanged;
            _outerwearIndex.OnValueChanged -= HandleOuterwearChanged;
            _pantsIndex.OnValueChanged -= HandlePantsChanged;
            _shoesIndex.OnValueChanged -= HandleShoesChanged;
            _skinColorIndex.OnValueChanged -= HandleSkinColorChanged;
        }

        #endregion

        #region Network Change Handlers

        private void HandleAccessoriesChanged(int prev, int curr)
        {
            SwapMesh(accessoriesRenderer, accessoriesMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Accessories, curr);
        }

        private void HandleFacesChanged(int prev, int curr)
        {
            SwapMesh(facesRenderer, facesMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Faces, curr);
        }

        private void HandleGlassesChanged(int prev, int curr)
        {
            SwapMesh(glassesRenderer, glassesMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Glasses, curr);
        }

        private void HandleGlovesChanged(int prev, int curr)
        {
            SwapMesh(glovesRenderer, glovesMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Gloves, curr);
        }

        private void HandleHairstyleChanged(int prev, int curr)
        {
            SwapMesh(hairstyleRenderer, hairstyleMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Hairstyle, curr);
        }

        private void HandleHatChanged(int prev, int curr)
        {
            SwapMesh(hatRenderer, hatMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Hat, curr);
        }

        private void HandleOuterwearChanged(int prev, int curr)
        {
            SwapMesh(outerwearRenderer, outerwearMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Outerwear, curr);
        }

        private void HandlePantsChanged(int prev, int curr)
        {
            SwapMesh(pantsRenderer, pantsMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Pants, curr);
        }

        private void HandleShoesChanged(int prev, int curr)
        {
            SwapMesh(shoesRenderer, shoesMeshes, curr);
            OnCustomizationChanged?.Invoke(CustomizationPart.Shoes, curr);
        }

        private void HandleSkinColorChanged(int prev, int curr)
        {
            ApplySkinColor();
            OnCustomizationChanged?.Invoke(CustomizationPart.SkinColor, curr);
        }

        #endregion

        #region Renderer Finding

        private void FindRenderers()
        {
            accessoriesRenderer = FindRendererByName(RENDERER_ACCESSORIES);
            facesRenderer = FindRendererByName(RENDERER_FACES);
            glassesRenderer = FindRendererByName(RENDERER_GLASSES);
            glovesRenderer = FindRendererByName(RENDERER_GLOVES);
            hairstyleRenderer = FindRendererByName(RENDERER_HAIRSTYLE);
            hatRenderer = FindRendererByName(RENDERER_HAT);
            outerwearRenderer = FindRendererByName(RENDERER_OUTERWEAR);
            pantsRenderer = FindRendererByName(RENDERER_PANTS);
            shoesRenderer = FindRendererByName(RENDERER_SHOES);

            FindSkinRenderer();
        }

        private void FindSkinRenderer()
        {
            if (skinRenderer != null) return;

            skinRenderer = FindRendererByName(RENDERER_BODY);

            if (skinRenderer == null)
            {
                skinRenderer = GetComponent<SkinnedMeshRenderer>();
            }
        }

        private SkinnedMeshRenderer FindRendererByName(string objectName)
        {
            // Direct child check
            Transform child = transform.Find(objectName);
            if (child != null)
            {
                var renderer = child.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null) return renderer;
            }

            // Recursive search
            return FindRendererRecursive(transform, objectName);
        }

        private SkinnedMeshRenderer FindRendererRecursive(Transform parent, string objectName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    var renderer = child.GetComponent<SkinnedMeshRenderer>();
                    if (renderer != null) return renderer;
                }

                var found = FindRendererRecursive(child, objectName);
                if (found != null) return found;
            }

            return null;
        }

        #endregion

        #region Mesh Swapping

        private void SwapAllMeshes()
        {
            SwapMesh(accessoriesRenderer, accessoriesMeshes, _accessoriesIndex.Value);
            SwapMesh(facesRenderer, facesMeshes, _facesIndex.Value);
            SwapMesh(glassesRenderer, glassesMeshes, _glassesIndex.Value);
            SwapMesh(glovesRenderer, glovesMeshes, _glovesIndex.Value);
            SwapMesh(hairstyleRenderer, hairstyleMeshes, _hairstyleIndex.Value);
            SwapMesh(hatRenderer, hatMeshes, _hatIndex.Value);
            SwapMesh(outerwearRenderer, outerwearMeshes, _outerwearIndex.Value);
            SwapMesh(pantsRenderer, pantsMeshes, _pantsIndex.Value);
            SwapMesh(shoesRenderer, shoesMeshes, _shoesIndex.Value);
        }

        private void SwapMesh(SkinnedMeshRenderer renderer, Mesh[] meshes, int index)
        {
            if (renderer == null || meshes == null || meshes.Length == 0) return;

            int clampedIndex = Mathf.Clamp(index, 0, meshes.Length - 1);

            if (renderer.sharedMesh != meshes[clampedIndex])
            {
                renderer.sharedMesh = meshes[clampedIndex];
            }
        }

        private void ApplyAllCustomizations()
        {
            SwapAllMeshes();
            ApplySkinColor();
        }

        private void ApplySkinColor()
        {
            if (skinRenderer == null || skinColors == null || skinColors.Length == 0) return;

            int clampedIndex = Mathf.Clamp(_skinColorIndex.Value, 0, skinColors.Length - 1);
            skinRenderer.material.color = skinColors[clampedIndex];
        }

        #endregion

        #region Random Outfit

        /// <summary>
        /// Her kategori için rastgele bir mesh/renk index'i atar. Sadece server tarafından,
        /// NetworkObject.Spawn() çağrıldıktan SONRA çağrılmalıdır (bkz. CustomerManager.SpawnCustomer).
        /// </summary>
        public void RandomizeOutfit()
        {
            if (!IsServer) return;

            // Not: Bu metod NetworkObject.Spawn()'dan SONRA çağrılır (bkz. CustomerManager.SpawnCustomer).
            // Spawn() öncesi .Value set etmek NGO'nun "NetworkVariable is written to, but doesn't know
            // its NetworkBehaviour yet" uyarısını basıyordu; Spawn() NetworkSpawnManager.HandleNetworkObjectShow
            // ile PostLateUpdate'te flush edildiğinden, aynı frame içinde Spawn()'dan hemen sonra yapılan bu
            // atama hem mevcut hem geç katılan (late-join) client'lara doğru ilk network state olarak gider.
            _accessoriesIndex.Value = RandomIndex(accessoriesMeshes);
            _facesIndex.Value = RandomIndex(facesMeshes);
            _glassesIndex.Value = RandomIndex(glassesMeshes);
            _glovesIndex.Value = RandomIndex(glovesMeshes);
            _hairstyleIndex.Value = RandomIndex(hairstyleMeshes);
            _hatIndex.Value = RandomIndex(hatMeshes);
            _outerwearIndex.Value = RandomIndex(outerwearMeshes);
            _pantsIndex.Value = RandomIndex(pantsMeshes);
            _shoesIndex.Value = RandomIndex(shoesMeshes);
            _skinColorIndex.Value = RandomIndex(skinColors?.Length ?? 0);
        }

        private int RandomIndex(Mesh[] meshArray)
        {
            if (meshArray == null || meshArray.Length == 0) return DEFAULT_INDEX;
            return UnityEngine.Random.Range(0, meshArray.Length);
        }

        private int RandomIndex(int arrayLength)
        {
            if (arrayLength <= 0) return DEFAULT_INDEX;
            return UnityEngine.Random.Range(0, arrayLength);
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Find All Renderers")]
        private void DebugFindRenderers()
        {
            FindRenderers();
            Debug.Log($"{LOG_PREFIX} Renderers found and cached");
        }

        [ContextMenu("Randomize Outfit")]
        private void DebugRandomizeOutfit()
        {
            RandomizeOutfit();
            ApplyAllCustomizations();
        }
#endif

        #endregion
    }
}
