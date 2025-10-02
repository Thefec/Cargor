using UnityEngine;
using Unity.Netcode;

[ExecuteInEditMode]
public class NetworkCharacterMeshSwapper : NetworkBehaviour
{
    [Header("Accessories")]
    public Mesh[] accessoriesMeshes;
    
    [Header("Faces")]
    public Mesh[] facesMeshes;
    
    [Header("Glasses")]
    public Mesh[] glassesMeshes;
    
    [Header("Gloves")]
    public Mesh[] glovesMeshes;
    
    [Header("Hairstyle")]
    public Mesh[] hairstyleMeshes;
    
    [Header("Hat")]
    public Mesh[] hatMeshes;
    
    [Header("Outerwear")]
    public Mesh[] outerwearMeshes;
    
    [Header("Pants")]
    public Mesh[] pantsMeshes;
    
    [Header("Shoes")]
    public Mesh[] shoesMeshes;

    // Network Variables - Automatically synced between clients
    private NetworkVariable<int> accessoriesIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> facesIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> glassesIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> glovesIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> hairstyleIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> hatIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> outerwearIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> pantsIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<int> shoesIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // Skin color network variable
    private NetworkVariable<int> skinColorIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // YENI: Save key prefix for PlayerPrefs
    private string saveKeyPrefix;

    // Cached renderers
    [System.NonSerialized] public SkinnedMeshRenderer accessoriesRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer facesRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer glassesRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer glovesRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer hairstyleRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer hatRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer outerwearRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer pantsRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer shoesRenderer;
    [System.NonSerialized] public SkinnedMeshRenderer skinRenderer;

    [Header("Skin Colors")]
    public Color[] skinColors = new Color[] {
        new Color(1f, 0.8f, 0.7f), // Light
        new Color(0.9f, 0.7f, 0.5f), // Medium
        new Color(0.8f, 0.6f, 0.4f), // Tan
        new Color(0.6f, 0.4f, 0.3f), // Dark
        new Color(0.4f, 0.3f, 0.2f)  // Very Dark
    };

    public override void OnNetworkSpawn()
    {
        // YENI: Unique save key based on ClientId
        saveKeyPrefix = $"CharCustom_Client_{OwnerClientId}_";
        
        // Find and cache all renderers
        FindRenderers();
        
        // YENI: Load saved customization data BEFORE subscribing to changes
        if (IsOwner)
        {
            LoadCustomizationData();
        }
        
        // Subscribe to network variable changes
        accessoriesIndex.OnValueChanged += OnAccessoriesChanged;
        facesIndex.OnValueChanged += OnFacesChanged;
        glassesIndex.OnValueChanged += OnGlassesChanged;
        glovesIndex.OnValueChanged += OnGlovesChanged;
        hairstyleIndex.OnValueChanged += OnHairstyleChanged;
        hatIndex.OnValueChanged += OnHatChanged;
        outerwearIndex.OnValueChanged += OnOuterwearChanged;
        pantsIndex.OnValueChanged += OnPantsChanged;
        shoesIndex.OnValueChanged += OnShoesChanged;
        skinColorIndex.OnValueChanged += OnSkinColorChanged;
        
        // Apply current values
        SwapAllMeshes();
        ApplySkinColor();
    }

    public override void OnNetworkDespawn()
    {
        // YENI: Save data when player disconnects (if owner)
        if (IsOwner)
        {
            SaveCustomizationData();
        }
        
        // Unsubscribe from network variable changes
        accessoriesIndex.OnValueChanged -= OnAccessoriesChanged;
        facesIndex.OnValueChanged -= OnFacesChanged;
        glassesIndex.OnValueChanged -= OnGlassesChanged;
        glovesIndex.OnValueChanged -= OnGlovesChanged;
        hairstyleIndex.OnValueChanged -= OnHairstyleChanged;
        hatIndex.OnValueChanged -= OnHatChanged;
        outerwearIndex.OnValueChanged -= OnOuterwearChanged;
        pantsIndex.OnValueChanged -= OnPantsChanged;
        shoesIndex.OnValueChanged -= OnShoesChanged;
        skinColorIndex.OnValueChanged -= OnSkinColorChanged;
    }

    // YENI: Save customization data to PlayerPrefs
    public void SaveCustomizationData()
    {
        if (!IsOwner) return;
        
        PlayerPrefs.SetInt(saveKeyPrefix + "accessories", accessoriesIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "faces", facesIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "glasses", glassesIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "gloves", glovesIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "hairstyle", hairstyleIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "hat", hatIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "outerwear", outerwearIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "pants", pantsIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "shoes", shoesIndex.Value);
        PlayerPrefs.SetInt(saveKeyPrefix + "skinColor", skinColorIndex.Value);
        
        PlayerPrefs.Save(); // Force save to disk
        
        Debug.Log("Character customization saved successfully!");
    }

    // YENI: Load customization data from PlayerPrefs
    public void LoadCustomizationData()
    {
        if (!IsOwner) return;
        
        // Load saved values, default to 0 if not found
        int savedAccessories = PlayerPrefs.GetInt(saveKeyPrefix + "accessories", 0);
        int savedFaces = PlayerPrefs.GetInt(saveKeyPrefix + "faces", 0);
        int savedGlasses = PlayerPrefs.GetInt(saveKeyPrefix + "glasses", 0);
        int savedGloves = PlayerPrefs.GetInt(saveKeyPrefix + "gloves", 0);
        int savedHairstyle = PlayerPrefs.GetInt(saveKeyPrefix + "hairstyle", 0);
        int savedHat = PlayerPrefs.GetInt(saveKeyPrefix + "hat", 0);
        int savedOuterwear = PlayerPrefs.GetInt(saveKeyPrefix + "outerwear", 0);
        int savedPants = PlayerPrefs.GetInt(saveKeyPrefix + "pants", 0);
        int savedShoes = PlayerPrefs.GetInt(saveKeyPrefix + "shoes", 0);
        int savedSkinColor = PlayerPrefs.GetInt(saveKeyPrefix + "skinColor", 0);
        
        // Apply loaded values to network variables
        accessoriesIndex.Value = ClampIndex(savedAccessories, accessoriesMeshes);
        facesIndex.Value = ClampIndex(savedFaces, facesMeshes);
        glassesIndex.Value = ClampIndex(savedGlasses, glassesMeshes);
        glovesIndex.Value = ClampIndex(savedGloves, glovesMeshes);
        hairstyleIndex.Value = ClampIndex(savedHairstyle, hairstyleMeshes);
        hatIndex.Value = ClampIndex(savedHat, hatMeshes);
        outerwearIndex.Value = ClampIndex(savedOuterwear, outerwearMeshes);
        pantsIndex.Value = ClampIndex(savedPants, pantsMeshes);
        shoesIndex.Value = ClampIndex(savedShoes, shoesMeshes);
        skinColorIndex.Value = ClampIndex(savedSkinColor, skinColors.Length);
        
        Debug.Log("Character customization loaded successfully!");
    }

    // YENI: Helper method to clamp index values
    private int ClampIndex(int value, Mesh[] array)
    {
        if (array == null || array.Length == 0) return 0;
        return Mathf.Clamp(value, 0, array.Length - 1);
    }

    private int ClampIndex(int value, int arrayLength)
    {
        if (arrayLength == 0) return 0;
        return Mathf.Clamp(value, 0, arrayLength - 1);
    }

    // YENI: Reset customization to defaults
    public void ResetToDefaults()
    {
        if (!IsOwner) return;
        
        accessoriesIndex.Value = 0;
        facesIndex.Value = 0;
        glassesIndex.Value = 0;
        glovesIndex.Value = 0;
        hairstyleIndex.Value = 0;
        hatIndex.Value = 0;
        outerwearIndex.Value = 0;
        pantsIndex.Value = 0;
        shoesIndex.Value = 0;
        skinColorIndex.Value = 0;
        
        SaveCustomizationData(); // Save the reset state
    }

    private void Awake()
    {
        // Only find renderers in non-network mode or editor
        if (!Application.isPlaying || !NetworkManager.Singleton)
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

    private void FindRenderers()
    {
        accessoriesRenderer = FindRendererByName("Accessories");
        facesRenderer = FindRendererByName("Faces");
        glassesRenderer = FindRendererByName("Glasses");
        glovesRenderer = FindRendererByName("Gloves");
        hairstyleRenderer = FindRendererByName("Hairstyle");
        hatRenderer = FindRendererByName("Hat");
        outerwearRenderer = FindRendererByName("Outerwear");
        pantsRenderer = FindRendererByName("Pants");
        shoesRenderer = FindRendererByName("Shoes");
        
        // Find skin renderer (usually the main body)
        if (skinRenderer == null)
        {
            skinRenderer = FindRendererByName("Body");
            if (skinRenderer == null)
                skinRenderer = GetComponent<SkinnedMeshRenderer>();
        }
    }

    private SkinnedMeshRenderer FindRendererByName(string objectName)
    {
        Transform child = transform.Find(objectName);
        if (child != null)
        {
            SkinnedMeshRenderer renderer = child.GetComponent<SkinnedMeshRenderer>();
            if (renderer != null)
                return renderer;
        }
        return FindRendererRecursive(transform, objectName);
    }

    private SkinnedMeshRenderer FindRendererRecursive(Transform parent, string objectName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                SkinnedMeshRenderer renderer = child.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null)
                    return renderer;
            }

            SkinnedMeshRenderer foundRenderer = FindRendererRecursive(child, objectName);
            if (foundRenderer != null)
                return foundRenderer;
        }
        return null;
    }

    // Network Variable Change Callbacks
    private void OnAccessoriesChanged(int previousValue, int newValue)
    {
        SwapMesh(accessoriesRenderer, accessoriesMeshes, newValue);
    }

    private void OnFacesChanged(int previousValue, int newValue)
    {
        SwapMesh(facesRenderer, facesMeshes, newValue);
    }

    private void OnGlassesChanged(int previousValue, int newValue)
    {
        SwapMesh(glassesRenderer, glassesMeshes, newValue);
    }

    private void OnGlovesChanged(int previousValue, int newValue)
    {
        SwapMesh(glovesRenderer, glovesMeshes, newValue);
    }

    private void OnHairstyleChanged(int previousValue, int newValue)
    {
        SwapMesh(hairstyleRenderer, hairstyleMeshes, newValue);
    }

    private void OnHatChanged(int previousValue, int newValue)
    {
        SwapMesh(hatRenderer, hatMeshes, newValue);
    }

    private void OnOuterwearChanged(int previousValue, int newValue)
    {
        SwapMesh(outerwearRenderer, outerwearMeshes, newValue);
    }

    private void OnPantsChanged(int previousValue, int newValue)
    {
        SwapMesh(pantsRenderer, pantsMeshes, newValue);
    }

    private void OnShoesChanged(int previousValue, int newValue)
    {
        SwapMesh(shoesRenderer, shoesMeshes, newValue);
    }

    private void OnSkinColorChanged(int previousValue, int newValue)
    {
        ApplySkinColor();
    }

    private void SwapAllMeshes()
    {
        SwapMesh(accessoriesRenderer, accessoriesMeshes, accessoriesIndex.Value);
        SwapMesh(facesRenderer, facesMeshes, facesIndex.Value);
        SwapMesh(glassesRenderer, glassesMeshes, glassesIndex.Value);
        SwapMesh(glovesRenderer, glovesMeshes, glovesIndex.Value);
        SwapMesh(hairstyleRenderer, hairstyleMeshes, hairstyleIndex.Value);
        SwapMesh(hatRenderer, hatMeshes, hatIndex.Value);
        SwapMesh(outerwearRenderer, outerwearMeshes, outerwearIndex.Value);
        SwapMesh(pantsRenderer, pantsMeshes, pantsIndex.Value);
        SwapMesh(shoesRenderer, shoesMeshes, shoesIndex.Value);
    }

    private void SwapMesh(SkinnedMeshRenderer renderer, Mesh[] meshes, int index)
    {
        if (renderer == null || meshes == null || meshes.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, meshes.Length - 1);
        if (renderer.sharedMesh != meshes[index])
        {
            renderer.sharedMesh = meshes[index];
        }
    }

    private void ApplySkinColor()
    {
        if (skinRenderer != null && skinColors != null && skinColors.Length > 0)
        {
            int colorIndex = Mathf.Clamp(skinColorIndex.Value, 0, skinColors.Length - 1);
            skinRenderer.material.color = skinColors[colorIndex];
        }
    }

    // Public methods to change customization (only owner can call these)
    // GÜNCELLEME: Her değişiklik sonrası otomatik kaydetme eklenebilir (opsiyonel)
    public void ChangeAccessories(int direction)
    {
        if (!IsOwner) return;
        if (accessoriesMeshes == null || accessoriesMeshes.Length == 0) return;
        
        int newIndex = (accessoriesIndex.Value + direction + accessoriesMeshes.Length) % accessoriesMeshes.Length;
        accessoriesIndex.Value = newIndex;
        // SaveCustomizationData(); // Uncomment for instant saving
    }

    public void ChangeFaces(int direction)
    {
        if (!IsOwner) return;
        if (facesMeshes == null || facesMeshes.Length == 0) return;
        
        int newIndex = (facesIndex.Value + direction + facesMeshes.Length) % facesMeshes.Length;
        facesIndex.Value = newIndex;
    }

    public void ChangeGlasses(int direction)
    {
        if (!IsOwner) return;
        if (glassesMeshes == null || glassesMeshes.Length == 0) return;
        
        int newIndex = (glassesIndex.Value + direction + glassesMeshes.Length) % glassesMeshes.Length;
        glassesIndex.Value = newIndex;
    }

    public void ChangeGloves(int direction)
    {
        if (!IsOwner) return;
        if (glovesMeshes == null || glovesMeshes.Length == 0) return;
        
        int newIndex = (glovesIndex.Value + direction + glovesMeshes.Length) % glovesMeshes.Length;
        glovesIndex.Value = newIndex;
    }

    public void ChangeHairstyle(int direction)
    {
        if (!IsOwner) return;
        if (hairstyleMeshes == null || hairstyleMeshes.Length == 0) return;
        
        int newIndex = (hairstyleIndex.Value + direction + hairstyleMeshes.Length) % hairstyleMeshes.Length;
        hairstyleIndex.Value = newIndex;
    }

    public void ChangeHat(int direction)
    {
        if (!IsOwner) return;
        if (hatMeshes == null || hatMeshes.Length == 0) return;
        
        int newIndex = (hatIndex.Value + direction + hatMeshes.Length) % hatMeshes.Length;
        hatIndex.Value = newIndex;
    }

    public void ChangeOuterwear(int direction)
    {
        if (!IsOwner) return;
        if (outerwearMeshes == null || outerwearMeshes.Length == 0) return;
        
        int newIndex = (outerwearIndex.Value + direction + outerwearMeshes.Length) % outerwearMeshes.Length;
        outerwearIndex.Value = newIndex;
    }

    public void ChangePants(int direction)
    {
        if (!IsOwner) return;
        if (pantsMeshes == null || pantsMeshes.Length == 0) return;
        
        int newIndex = (pantsIndex.Value + direction + pantsMeshes.Length) % pantsMeshes.Length;
        pantsIndex.Value = newIndex;
    }

    public void ChangeShoes(int direction)
    {
        if (!IsOwner) return;
        if (shoesMeshes == null || shoesMeshes.Length == 0) return;
        
        int newIndex = (shoesIndex.Value + direction + shoesMeshes.Length) % shoesMeshes.Length;
        shoesIndex.Value = newIndex;
    }

    public void ChangeSkinColor(int direction)
    {
        if (!IsOwner) return;
        if (skinColors == null || skinColors.Length == 0) return;
        
        int newIndex = (skinColorIndex.Value + direction + skinColors.Length) % skinColors.Length;
        skinColorIndex.Value = newIndex;
    }

    // Get current values for UI
    public int GetAccessoriesIndex() => accessoriesIndex.Value;
    public int GetFacesIndex() => facesIndex.Value;
    public int GetGlassesIndex() => glassesIndex.Value;
    public int GetGlovesIndex() => glovesIndex.Value;
    public int GetHairstyleIndex() => hairstyleIndex.Value;
    public int GetHatIndex() => hatIndex.Value;
    public int GetOuterwearIndex() => outerwearIndex.Value;
    public int GetPantsIndex() => pantsIndex.Value;
    public int GetShoesIndex() => shoesIndex.Value;
    public int GetSkinColorIndex() => skinColorIndex.Value;
}