using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Network karakter mesh değiştirici - karakter özelleştirme sistemini yönetir.    
    /// Network senkronizasyonu ve PlayerPrefs ile kayıt/yükleme desteği sağlar.
    /// </summary>
    [ExecuteInEditMode]
    public class NetworkCharacterMeshSwapper : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CharacterMeshSwapper]";
        private const string SAVE_KEY_PREFIX_FORMAT = "CharCustom_Client_{0}_";
        private const int DEFAULT_INDEX = 0;

        // PlayerPrefs Keys
        private const string KEY_ACCESSORIES = "accessories";
        private const string KEY_FACES = "faces";
        private const string KEY_GLASSES = "glasses";
        private const string KEY_GLOVES = "gloves";
        private const string KEY_HAIRSTYLE = "hairstyle";
        private const string KEY_HAT = "hat";
        private const string KEY_OUTERWEAR = "outerwear";
        private const string KEY_PANTS = "pants";
        private const string KEY_SHOES = "shoes";
        private const string KEY_SKIN_COLOR = "skinColor";

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
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _facesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _glassesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _glovesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _hairstyleIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _hatIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _outerwearIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _pantsIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _shoesIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<int> _skinColorIndex = new(DEFAULT_INDEX,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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

        #region Private Fields

        private string _saveKeyPrefix;

        #endregion

        #region Events

        /// <summary>
        /// Özelleştirme değiştiğinde tetiklenir
        /// </summary>
        public event Action<CustomizationPart, int> OnCustomizationChanged;

        /// <summary>
        /// Tüm özelleştirmeler yüklendiğinde tetiklenir
        /// </summary>
        public event Action OnCustomizationLoaded;

        /// <summary>
        /// Özelleştirmeler kaydedildiğinde tetiklenir
        /// </summary>
        public event Action OnCustomizationSaved;

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

            InitializeSaveKey();
            FindRenderers();

            if (IsOwner)
            {
                LoadCustomizationData();
            }

            SubscribeToNetworkEvents();
            ApplyAllCustomizations();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                SaveCustomizationData();
            }

            UnsubscribeFromNetworkEvents();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeSaveKey()
        {
            _saveKeyPrefix = string.Format(SAVE_KEY_PREFIX_FORMAT, OwnerClientId);
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

        #region Save/Load System

        /// <summary>
        /// Özelleştirme verilerini kaydeder
        /// </summary>
        public void SaveCustomizationData()
        {
            if (!IsOwner) return;

            SaveIndex(KEY_ACCESSORIES, _accessoriesIndex.Value);
            SaveIndex(KEY_FACES, _facesIndex.Value);
            SaveIndex(KEY_GLASSES, _glassesIndex.Value);
            SaveIndex(KEY_GLOVES, _glovesIndex.Value);
            SaveIndex(KEY_HAIRSTYLE, _hairstyleIndex.Value);
            SaveIndex(KEY_HAT, _hatIndex.Value);
            SaveIndex(KEY_OUTERWEAR, _outerwearIndex.Value);
            SaveIndex(KEY_PANTS, _pantsIndex.Value);
            SaveIndex(KEY_SHOES, _shoesIndex.Value);
            SaveIndex(KEY_SKIN_COLOR, _skinColorIndex.Value);

            PlayerPrefs.Save();

            OnCustomizationSaved?.Invoke();
            Debug.Log($"{LOG_PREFIX} Customization data saved successfully!");
        }

        /// <summary>
        /// Özelleştirme verilerini yükler
        /// </summary>
        public void LoadCustomizationData()
        {
            if (!IsOwner) return;

            _accessoriesIndex.Value = LoadAndClampIndex(KEY_ACCESSORIES, accessoriesMeshes);
            _facesIndex.Value = LoadAndClampIndex(KEY_FACES, facesMeshes);
            _glassesIndex.Value = LoadAndClampIndex(KEY_GLASSES, glassesMeshes);
            _glovesIndex.Value = LoadAndClampIndex(KEY_GLOVES, glovesMeshes);
            _hairstyleIndex.Value = LoadAndClampIndex(KEY_HAIRSTYLE, hairstyleMeshes);
            _hatIndex.Value = LoadAndClampIndex(KEY_HAT, hatMeshes);
            _outerwearIndex.Value = LoadAndClampIndex(KEY_OUTERWEAR, outerwearMeshes);
            _pantsIndex.Value = LoadAndClampIndex(KEY_PANTS, pantsMeshes);
            _shoesIndex.Value = LoadAndClampIndex(KEY_SHOES, shoesMeshes);
            _skinColorIndex.Value = LoadAndClampIndex(KEY_SKIN_COLOR, skinColors.Length);

            OnCustomizationLoaded?.Invoke();
            Debug.Log($"{LOG_PREFIX} Customization data loaded successfully!");
        }

        private void SaveIndex(string key, int value)
        {
            PlayerPrefs.SetInt(_saveKeyPrefix + key, value);
        }

        private int LoadAndClampIndex(string key, Mesh[] meshArray)
        {
            int saved = PlayerPrefs.GetInt(_saveKeyPrefix + key, DEFAULT_INDEX);
            return ClampIndex(saved, meshArray);
        }

        private int LoadAndClampIndex(string key, int arrayLength)
        {
            int saved = PlayerPrefs.GetInt(_saveKeyPrefix + key, DEFAULT_INDEX);
            return ClampIndex(saved, arrayLength);
        }

        private int ClampIndex(int value, Mesh[] array)
        {
            if (array == null || array.Length == 0) return DEFAULT_INDEX;
            return Mathf.Clamp(value, 0, array.Length - 1);
        }

        private int ClampIndex(int value, int arrayLength)
        {
            if (arrayLength == 0) return DEFAULT_INDEX;
            return Mathf.Clamp(value, 0, arrayLength - 1);
        }

        #endregion

        #region Public Change Methods

        /// <summary>
        /// Aksesuar değiştirir
        /// </summary>
        public void ChangeAccessories(int direction)
        {
            if (!CanModify(accessoriesMeshes)) return;
            _accessoriesIndex.Value = WrapIndex(_accessoriesIndex.Value, direction, accessoriesMeshes.Length);
        }

        /// <summary>
        /// Yüz değiştirir
        /// </summary>
        public void ChangeFaces(int direction)
        {
            if (!CanModify(facesMeshes)) return;
            _facesIndex.Value = WrapIndex(_facesIndex.Value, direction, facesMeshes.Length);
        }

        /// <summary>
        /// Gözlük değiştirir
        /// </summary>
        public void ChangeGlasses(int direction)
        {
            if (!CanModify(glassesMeshes)) return;
            _glassesIndex.Value = WrapIndex(_glassesIndex.Value, direction, glassesMeshes.Length);
        }

        /// <summary>
        /// Eldiven değiştirir
        /// </summary>
        public void ChangeGloves(int direction)
        {
            if (!CanModify(glovesMeshes)) return;
            _glovesIndex.Value = WrapIndex(_glovesIndex.Value, direction, glovesMeshes.Length);
        }

        /// <summary>
        /// Saç değiştirir
        /// </summary>
        public void ChangeHairstyle(int direction)
        {
            if (!CanModify(hairstyleMeshes)) return;
            _hairstyleIndex.Value = WrapIndex(_hairstyleIndex.Value, direction, hairstyleMeshes.Length);
        }

        /// <summary>
        /// Şapka değiştirir
        /// </summary>
        public void ChangeHat(int direction)
        {
            if (!CanModify(hatMeshes)) return;
            _hatIndex.Value = WrapIndex(_hatIndex.Value, direction, hatMeshes.Length);
        }

        /// <summary>
        /// Üst giysi değiştirir
        /// </summary>
        public void ChangeOuterwear(int direction)
        {
            if (!CanModify(outerwearMeshes)) return;
            _outerwearIndex.Value = WrapIndex(_outerwearIndex.Value, direction, outerwearMeshes.Length);
        }

        /// <summary>
        /// Pantolon değiştirir
        /// </summary>
        public void ChangePants(int direction)
        {
            if (!CanModify(pantsMeshes)) return;
            _pantsIndex.Value = WrapIndex(_pantsIndex.Value, direction, pantsMeshes.Length);
        }

        /// <summary>
        /// Ayakkabı değiştirir
        /// </summary>
        public void ChangeShoes(int direction)
        {
            if (!CanModify(shoesMeshes)) return;
            _shoesIndex.Value = WrapIndex(_shoesIndex.Value, direction, shoesMeshes.Length);
        }

        /// <summary>
        /// Ten rengi değiştirir
        /// </summary>
        public void ChangeSkinColor(int direction)
        {
            if (!IsOwner || skinColors == null || skinColors.Length == 0) return;
            _skinColorIndex.Value = WrapIndex(_skinColorIndex.Value, direction, skinColors.Length);
        }

        #endregion

        #region Generic Change Method

        /// <summary>
        /// Kategori bazlı değiştirme
        /// </summary>
        public void ChangePart(CustomizationPart part, int direction)
        {
            switch (part)
            {
                case CustomizationPart.Accessories: ChangeAccessories(direction); break;
                case CustomizationPart.Faces: ChangeFaces(direction); break;
                case CustomizationPart.Glasses: ChangeGlasses(direction); break;
                case CustomizationPart.Gloves: ChangeGloves(direction); break;
                case CustomizationPart.Hairstyle: ChangeHairstyle(direction); break;
                case CustomizationPart.Hat: ChangeHat(direction); break;
                case CustomizationPart.Outerwear: ChangeOuterwear(direction); break;
                case CustomizationPart.Pants: ChangePants(direction); break;
                case CustomizationPart.Shoes: ChangeShoes(direction); break;
                case CustomizationPart.SkinColor: ChangeSkinColor(direction); break;
            }
        }

        #endregion

        #region Reset

        /// <summary>
        /// Tüm özelleştirmeleri sıfırlar
        /// </summary>
        public void ResetToDefaults()
        {
            if (!IsOwner) return;

            _accessoriesIndex.Value = DEFAULT_INDEX;
            _facesIndex.Value = DEFAULT_INDEX;
            _glassesIndex.Value = DEFAULT_INDEX;
            _glovesIndex.Value = DEFAULT_INDEX;
            _hairstyleIndex.Value = DEFAULT_INDEX;
            _hatIndex.Value = DEFAULT_INDEX;
            _outerwearIndex.Value = DEFAULT_INDEX;
            _pantsIndex.Value = DEFAULT_INDEX;
            _shoesIndex.Value = DEFAULT_INDEX;
            _skinColorIndex.Value = DEFAULT_INDEX;

            SaveCustomizationData();
        }

        #endregion

        #region Getter Methods (Backward Compatibility)

        public int GetAccessoriesIndex() => _accessoriesIndex.Value;
        public int GetFacesIndex() => _facesIndex.Value;
        public int GetGlassesIndex() => _glassesIndex.Value;
        public int GetGlovesIndex() => _glovesIndex.Value;
        public int GetHairstyleIndex() => _hairstyleIndex.Value;
        public int GetHatIndex() => _hatIndex.Value;
        public int GetOuterwearIndex() => _outerwearIndex.Value;
        public int GetPantsIndex() => _pantsIndex.Value;
        public int GetShoesIndex() => _shoesIndex.Value;
        public int GetSkinColorIndex() => _skinColorIndex.Value;

        /// <summary>
        /// Belirli bir kategorinin index'ini döndürür
        /// </summary>
        public int GetPartIndex(CustomizationPart part)
        {
            return part switch
            {
                CustomizationPart.Accessories => _accessoriesIndex.Value,
                CustomizationPart.Faces => _facesIndex.Value,
                CustomizationPart.Glasses => _glassesIndex.Value,
                CustomizationPart.Gloves => _glovesIndex.Value,
                CustomizationPart.Hairstyle => _hairstyleIndex.Value,
                CustomizationPart.Hat => _hatIndex.Value,
                CustomizationPart.Outerwear => _outerwearIndex.Value,
                CustomizationPart.Pants => _pantsIndex.Value,
                CustomizationPart.Shoes => _shoesIndex.Value,
                CustomizationPart.SkinColor => _skinColorIndex.Value,
                _ => DEFAULT_INDEX
            };
        }

        /// <summary>
        /// Belirli bir kategorinin maksimum index'ini döndürür
        /// </summary>
        public int GetPartMaxIndex(CustomizationPart part)
        {
            return part switch
            {
                CustomizationPart.Accessories => GetMaxIndex(accessoriesMeshes),
                CustomizationPart.Faces => GetMaxIndex(facesMeshes),
                CustomizationPart.Glasses => GetMaxIndex(glassesMeshes),
                CustomizationPart.Gloves => GetMaxIndex(glovesMeshes),
                CustomizationPart.Hairstyle => GetMaxIndex(hairstyleMeshes),
                CustomizationPart.Hat => GetMaxIndex(hatMeshes),
                CustomizationPart.Outerwear => GetMaxIndex(outerwearMeshes),
                CustomizationPart.Pants => GetMaxIndex(pantsMeshes),
                CustomizationPart.Shoes => GetMaxIndex(shoesMeshes),
                CustomizationPart.SkinColor => skinColors?.Length - 1 ?? 0,
                _ => 0
            };
        }

        private int GetMaxIndex(Mesh[] array)
        {
            return array != null && array.Length > 0 ? array.Length - 1 : 0;
        }

        #endregion

        #region Utility Methods

        private bool CanModify(Mesh[] meshArray)
        {
            return IsOwner && meshArray != null && meshArray.Length > 0;
        }

        private int WrapIndex(int current, int direction, int length)
        {
            return (current + direction + length) % length;
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Save Customization")]
        private void DebugSaveCustomization()
        {
            SaveCustomizationData();
        }

        [ContextMenu("Load Customization")]
        private void DebugLoadCustomization()
        {
            LoadCustomizationData();
        }

        [ContextMenu("Reset to Defaults")]
        private void DebugResetToDefaults()
        {
            ResetToDefaults();
        }

        [ContextMenu("Find All Renderers")]
        private void DebugFindRenderers()
        {
            FindRenderers();
            Debug.Log($"{LOG_PREFIX} Renderers found and cached");
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === CHARACTER MESH SWAPPER STATE ===");
            Debug.Log($"Is Owner: {IsOwner}");
            Debug.Log($"Save Key Prefix: {_saveKeyPrefix}");
            Debug.Log($"--- Current Indices ---");
            Debug.Log($"Accessories: {_accessoriesIndex.Value}/{GetMaxIndex(accessoriesMeshes)}");
            Debug.Log($"Faces: {_facesIndex.Value}/{GetMaxIndex(facesMeshes)}");
            Debug.Log($"Glasses: {_glassesIndex.Value}/{GetMaxIndex(glassesMeshes)}");
            Debug.Log($"Gloves: {_glovesIndex.Value}/{GetMaxIndex(glovesMeshes)}");
            Debug.Log($"Hairstyle: {_hairstyleIndex.Value}/{GetMaxIndex(hairstyleMeshes)}");
            Debug.Log($"Hat: {_hatIndex.Value}/{GetMaxIndex(hatMeshes)}");
            Debug.Log($"Outerwear: {_outerwearIndex.Value}/{GetMaxIndex(outerwearMeshes)}");
            Debug.Log($"Pants: {_pantsIndex.Value}/{GetMaxIndex(pantsMeshes)}");
            Debug.Log($"Shoes: {_shoesIndex.Value}/{GetMaxIndex(shoesMeshes)}");
            Debug.Log($"Skin Color: {_skinColorIndex.Value}/{skinColors?.Length - 1 ?? 0}");
            Debug.Log($"--- Renderers ---");
            Debug.Log($"Accessories Renderer: {(accessoriesRenderer != null ? "Found" : "NULL")}");
            Debug.Log($"Faces Renderer: {(facesRenderer != null ? "Found" : "NULL")}");
            Debug.Log($"Skin Renderer: {(skinRenderer != null ? "Found" : "NULL")}");
        }

        [ContextMenu("Debug: Print All Parts")]
        private void DebugPrintAllParts()
        {
            Debug.Log($"{LOG_PREFIX} === ALL CUSTOMIZATION PARTS ===");
            foreach (CustomizationPart part in Enum.GetValues(typeof(CustomizationPart)))
            {
                Debug.Log($"  {part}: {GetPartIndex(part)}/{GetPartMaxIndex(part)}");
            }
        }
#endif

        #endregion
    }
}