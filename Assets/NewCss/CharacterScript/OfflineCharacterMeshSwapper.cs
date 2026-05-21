using System;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Ana menüde ağ (network) bağımlılığı olmadan çalışan karakter mesh değiştirici.
    /// NetworkCharacterMeshSwapper ile aynı PlayerPrefs anahtarlarını kullanarak
    /// ana menüde yapılan özelleştirmelerin oyun içinde de geçerli olmasını sağlar.
    /// </summary>
    [ExecuteInEditMode]
    public class OfflineCharacterMeshSwapper : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[OfflineMeshSwapper]";
        private const string SAVE_KEY_PREFIX = "CharCustom_LocalPlayer_";
        private const int DEFAULT_INDEX = 0;

        // PlayerPrefs Anahtarları (NetworkCharacterMeshSwapper ile aynı)
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

        // Renderer İsimleri
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

        [Header("=== MESH DİZİLERİ ===")]
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

        [Header("=== TEN RENKLERİ ===")]
        [SerializeField, Tooltip("Ten renkleri")]
        public Color[] skinColors = new Color[]
        {
            new Color(1f, 0.8f, 0.7f),      // Açık
            new Color(0.9f, 0.7f, 0.5f),    // Orta
            new Color(0.8f, 0.6f, 0.4f),    // Bronz
            new Color(0.6f, 0.4f, 0.3f),    // Koyu
            new Color(0.4f, 0.3f, 0.2f)     // Çok Koyu
        };

        #endregion

        #region Serialized Fields - Debug

        [Header("=== HATA AYIKLAMA (DEBUG) ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs;

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

        #region Private Fields - Indices

        private int _accessoriesIndex;
        private int _facesIndex;
        private int _glassesIndex;
        private int _glovesIndex;
        private int _hairstyleIndex;
        private int _hatIndex;
        private int _outerwearIndex;
        private int _pantsIndex;
        private int _shoesIndex;
        private int _skinColorIndex;

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

        public int AccessoriesIndex => _accessoriesIndex;
        public int FacesIndex => _facesIndex;
        public int GlassesIndex => _glassesIndex;
        public int GlovesIndex => _glovesIndex;
        public int HairstyleIndex => _hairstyleIndex;
        public int HatIndex => _hatIndex;
        public int OuterwearIndex => _outerwearIndex;
        public int PantsIndex => _pantsIndex;
        public int ShoesIndex => _shoesIndex;
        public int SkinColorIndex => _skinColorIndex;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            FindRenderers();
        }

        private void Start()
        {
            LoadCustomizationData();
            ApplyAllCustomizations();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                FindRenderers();
                ApplyAllCustomizations();
            }
        }

        #endregion

        #region Save/Load System

        /// <summary>
        /// Özelleştirme verilerini PlayerPrefs'e kaydeder
        /// </summary>
        public void SaveCustomizationData()
        {
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_ACCESSORIES, _accessoriesIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_FACES, _facesIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_GLASSES, _glassesIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_GLOVES, _glovesIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_HAIRSTYLE, _hairstyleIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_HAT, _hatIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_OUTERWEAR, _outerwearIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_PANTS, _pantsIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_SHOES, _shoesIndex);
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + KEY_SKIN_COLOR, _skinColorIndex);

            PlayerPrefs.Save();

            OnCustomizationSaved?.Invoke();
            LogDebug("Özelleştirme verileri başarıyla kaydedildi!");
        }

        /// <summary>
        /// Özelleştirme verilerini PlayerPrefs'ten yükler
        /// </summary>
        public void LoadCustomizationData()
        {
            _accessoriesIndex = LoadAndClampIndex(KEY_ACCESSORIES, accessoriesMeshes);
            _facesIndex = LoadAndClampIndex(KEY_FACES, facesMeshes);
            _glassesIndex = LoadAndClampIndex(KEY_GLASSES, glassesMeshes);
            _glovesIndex = LoadAndClampIndex(KEY_GLOVES, glovesMeshes);
            _hairstyleIndex = LoadAndClampIndex(KEY_HAIRSTYLE, hairstyleMeshes);
            _hatIndex = LoadAndClampIndex(KEY_HAT, hatMeshes);
            _outerwearIndex = LoadAndClampIndex(KEY_OUTERWEAR, outerwearMeshes);
            _pantsIndex = LoadAndClampIndex(KEY_PANTS, pantsMeshes);
            _shoesIndex = LoadAndClampIndex(KEY_SHOES, shoesMeshes);
            _skinColorIndex = LoadAndClampIndex(KEY_SKIN_COLOR, skinColors.Length);

            OnCustomizationLoaded?.Invoke();
            LogDebug("Özelleştirme verileri başarıyla yüklendi!");
        }

        private int LoadAndClampIndex(string key, Mesh[] meshArray)
        {
            int saved = PlayerPrefs.GetInt(SAVE_KEY_PREFIX + key, DEFAULT_INDEX);
            return ClampIndex(saved, meshArray);
        }

        private int LoadAndClampIndex(string key, int arrayLength)
        {
            int saved = PlayerPrefs.GetInt(SAVE_KEY_PREFIX + key, DEFAULT_INDEX);
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
            Transform child = transform.Find(objectName);
            if (child != null)
            {
                var renderer = child.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null) return renderer;
            }

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
            SwapMesh(accessoriesRenderer, accessoriesMeshes, _accessoriesIndex);
            SwapMesh(facesRenderer, facesMeshes, _facesIndex);
            SwapMesh(glassesRenderer, glassesMeshes, _glassesIndex);
            SwapMesh(glovesRenderer, glovesMeshes, _glovesIndex);
            SwapMesh(hairstyleRenderer, hairstyleMeshes, _hairstyleIndex);
            SwapMesh(hatRenderer, hatMeshes, _hatIndex);
            SwapMesh(outerwearRenderer, outerwearMeshes, _outerwearIndex);
            SwapMesh(pantsRenderer, pantsMeshes, _pantsIndex);
            SwapMesh(shoesRenderer, shoesMeshes, _shoesIndex);
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

            int clampedIndex = Mathf.Clamp(_skinColorIndex, 0, skinColors.Length - 1);
            Color color = skinColors[clampedIndex];

            // Edit Mode'da materyal kopyalanmasını ve sızıntıyı önlemek için MaterialPropertyBlock kullanıyoruz.
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            skinRenderer.GetPropertyBlock(propBlock);

            // URP (_BaseColor) veya Standart (_Color) shader uyumluluğu için renkleri set ediyoruz.
            propBlock.SetColor("_BaseColor", color);
            propBlock.SetColor("_Color", color);

            skinRenderer.SetPropertyBlock(propBlock);
        }

        #endregion

        #region Public Change Methods

        public void ChangeAccessories(int direction)
        {
            if (!CanModify(accessoriesMeshes)) return;
            _accessoriesIndex = WrapIndex(_accessoriesIndex, direction, accessoriesMeshes.Length);
            SwapMesh(accessoriesRenderer, accessoriesMeshes, _accessoriesIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Accessories, _accessoriesIndex);
        }

        public void ChangeFaces(int direction)
        {
            if (!CanModify(facesMeshes)) return;
            _facesIndex = WrapIndex(_facesIndex, direction, facesMeshes.Length);
            SwapMesh(facesRenderer, facesMeshes, _facesIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Faces, _facesIndex);
        }

        public void ChangeGlasses(int direction)
        {
            if (!CanModify(glassesMeshes)) return;
            _glassesIndex = WrapIndex(_glassesIndex, direction, glassesMeshes.Length);
            SwapMesh(glassesRenderer, glassesMeshes, _glassesIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Glasses, _glassesIndex);
        }

        public void ChangeGloves(int direction)
        {
            if (!CanModify(glovesMeshes)) return;
            _glovesIndex = WrapIndex(_glovesIndex, direction, glovesMeshes.Length);
            SwapMesh(glovesRenderer, glovesMeshes, _glovesIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Gloves, _glovesIndex);
        }

        public void ChangeHairstyle(int direction)
        {
            if (!CanModify(hairstyleMeshes)) return;
            _hairstyleIndex = WrapIndex(_hairstyleIndex, direction, hairstyleMeshes.Length);
            SwapMesh(hairstyleRenderer, hairstyleMeshes, _hairstyleIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Hairstyle, _hairstyleIndex);
        }

        public void ChangeHat(int direction)
        {
            if (!CanModify(hatMeshes)) return;
            _hatIndex = WrapIndex(_hatIndex, direction, hatMeshes.Length);
            SwapMesh(hatRenderer, hatMeshes, _hatIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Hat, _hatIndex);
        }

        public void ChangeOuterwear(int direction)
        {
            if (!CanModify(outerwearMeshes)) return;
            _outerwearIndex = WrapIndex(_outerwearIndex, direction, outerwearMeshes.Length);
            SwapMesh(outerwearRenderer, outerwearMeshes, _outerwearIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Outerwear, _outerwearIndex);
        }

        public void ChangePants(int direction)
        {
            if (!CanModify(pantsMeshes)) return;
            _pantsIndex = WrapIndex(_pantsIndex, direction, pantsMeshes.Length);
            SwapMesh(pantsRenderer, pantsMeshes, _pantsIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Pants, _pantsIndex);
        }

        public void ChangeShoes(int direction)
        {
            if (!CanModify(shoesMeshes)) return;
            _shoesIndex = WrapIndex(_shoesIndex, direction, shoesMeshes.Length);
            SwapMesh(shoesRenderer, shoesMeshes, _shoesIndex);
            OnCustomizationChanged?.Invoke(CustomizationPart.Shoes, _shoesIndex);
        }

        public void ChangeSkinColor(int direction)
        {
            if (skinColors == null || skinColors.Length == 0) return;
            _skinColorIndex = WrapIndex(_skinColorIndex, direction, skinColors.Length);
            ApplySkinColor();
            OnCustomizationChanged?.Invoke(CustomizationPart.SkinColor, _skinColorIndex);
        }

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
            _accessoriesIndex = DEFAULT_INDEX;
            _facesIndex = DEFAULT_INDEX;
            _glassesIndex = DEFAULT_INDEX;
            _glovesIndex = DEFAULT_INDEX;
            _hairstyleIndex = DEFAULT_INDEX;
            _hatIndex = DEFAULT_INDEX;
            _outerwearIndex = DEFAULT_INDEX;
            _pantsIndex = DEFAULT_INDEX;
            _shoesIndex = DEFAULT_INDEX;
            _skinColorIndex = DEFAULT_INDEX;

            ApplyAllCustomizations();
            SaveCustomizationData();
        }

        #endregion

        #region Getter Methods

        public int GetPartIndex(CustomizationPart part)
        {
            return part switch
            {
                CustomizationPart.Accessories => _accessoriesIndex,
                CustomizationPart.Faces => _facesIndex,
                CustomizationPart.Glasses => _glassesIndex,
                CustomizationPart.Gloves => _glovesIndex,
                CustomizationPart.Hairstyle => _hairstyleIndex,
                CustomizationPart.Hat => _hatIndex,
                CustomizationPart.Outerwear => _outerwearIndex,
                CustomizationPart.Pants => _pantsIndex,
                CustomizationPart.Shoes => _shoesIndex,
                CustomizationPart.SkinColor => _skinColorIndex,
                _ => DEFAULT_INDEX
            };
        }

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
            return meshArray != null && meshArray.Length > 0;
        }

        private int WrapIndex(int current, int direction, int length)
        {
            return (current + direction + length) % length;
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

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Kaydet (Save)")]
        private void DebugSave() => SaveCustomizationData();

        [ContextMenu("Yükle (Load)")]
        private void DebugLoad()
        {
            LoadCustomizationData();
            ApplyAllCustomizations();
        }

        [ContextMenu("Sıfırla (Reset)")]
        private void DebugReset() => ResetToDefaults();

        [ContextMenu("Renderer'ları Bul")]
        private void DebugFindRenderers()
        {
            FindRenderers();
            Debug.Log($"{LOG_PREFIX} Renderer'lar bulundu ve önbelleğe alındı");
        }

        [ContextMenu("Debug: Durumu Yazdır")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === OFFLINE MESH SWAPPER DURUMU ===");
            Debug.Log($"Accessories: {_accessoriesIndex}/{GetMaxIndex(accessoriesMeshes)}");
            Debug.Log($"Faces: {_facesIndex}/{GetMaxIndex(facesMeshes)}");
            Debug.Log($"Glasses: {_glassesIndex}/{GetMaxIndex(glassesMeshes)}");
            Debug.Log($"Gloves: {_glovesIndex}/{GetMaxIndex(glovesMeshes)}");
            Debug.Log($"Hairstyle: {_hairstyleIndex}/{GetMaxIndex(hairstyleMeshes)}");
            Debug.Log($"Hat: {_hatIndex}/{GetMaxIndex(hatMeshes)}");
            Debug.Log($"Outerwear: {_outerwearIndex}/{GetMaxIndex(outerwearMeshes)}");
            Debug.Log($"Pants: {_pantsIndex}/{GetMaxIndex(pantsMeshes)}");
            Debug.Log($"Shoes: {_shoesIndex}/{GetMaxIndex(shoesMeshes)}");
            Debug.Log($"Skin Color: {_skinColorIndex}/{skinColors?.Length - 1 ?? 0}");
        }
#endif

        #endregion
    }
}
