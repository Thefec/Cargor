using UnityEngine;
using UnityEngine.UI;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Ana menüdeki karakter özelleştirme panelini kontrol eder.
    /// OfflineCharacterMeshSwapper ile iletişim kurarak switch butonlarını yönetir.
    /// </summary>
    public class MainMenuCustomizationUI : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[MainMenuCustomizationUI]";

        #endregion

        #region Serialized Fields - References

        [Header("=== TEMEL REFERANSLAR ===")]
        [SerializeField, Tooltip("Offline karakter mesh swapper")]
        private OfflineCharacterMeshSwapper meshSwapper;

        #endregion

        #region Serialized Fields - Category Buttons

        [Header("=== AKSESUAR BUTONLARI ===")]
        [SerializeField] private Button accessoriesNextBtn;
        [SerializeField] private Button accessoriesPrevBtn;

        [Header("=== YÜZ BUTONLARI ===")]
        [SerializeField] private Button faceNextBtn;
        [SerializeField] private Button facePrevBtn;

        [Header("=== GÖZLÜK BUTONLARI ===")]
        [SerializeField] private Button glassesNextBtn;
        [SerializeField] private Button glassesPrevBtn;

        [Header("=== ELDİVEN BUTONLARI ===")]
        [SerializeField] private Button glovesNextBtn;
        [SerializeField] private Button glovesPrevBtn;

        [Header("=== SAÇ BUTONLARI ===")]
        [SerializeField] private Button hairNextBtn;
        [SerializeField] private Button hairPrevBtn;

        [Header("=== ŞAPKA BUTONLARI ===")]
        [SerializeField] private Button hatNextBtn;
        [SerializeField] private Button hatPrevBtn;

        [Header("=== ÜST GİYSİ BUTONLARI ===")]
        [SerializeField] private Button outerwearNextBtn;
        [SerializeField] private Button outerwearPrevBtn;

        [Header("=== PANTOLON BUTONLARI ===")]
        [SerializeField] private Button pantsNextBtn;
        [SerializeField] private Button pantsPrevBtn;

        [Header("=== AYAKKABI BUTONLARI ===")]
        [SerializeField] private Button shoesNextBtn;
        [SerializeField] private Button shoesPrevBtn;

        [Header("=== TEN RENGİ BUTONLARI ===")]
        [SerializeField] private Button skinColorNextBtn;
        [SerializeField] private Button skinColorPrevBtn;

        [Header("=== KONTROL BUTONLARI ===")]
        [SerializeField, Tooltip("Sıfırla butonu")]
        private Button resetButton;

        #endregion

        #region Serialized Fields - Debug

        [Header("=== HATA AYIKLAMA ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs;

        #endregion

        #region Private Fields

        private bool _isOpen;

        #endregion

        #region Public Properties

        /// <summary>
        /// Panel açık mı?
        /// </summary>
        public bool IsOpen => _isOpen;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeButtons();
            _isOpen = false;
        }

        private void OnDestroy()
        {
            CleanupButtons();
        }

        #endregion

        #region Button Initialization

        private void InitializeButtons()
        {
            // Kategori butonları
            BindButton(accessoriesNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Accessories, 1));
            BindButton(accessoriesPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Accessories, -1));

            BindButton(faceNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Faces, 1));
            BindButton(facePrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Faces, -1));

            BindButton(glassesNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Glasses, 1));
            BindButton(glassesPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Glasses, -1));

            BindButton(glovesNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Gloves, 1));
            BindButton(glovesPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Gloves, -1));

            BindButton(hairNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Hairstyle, 1));
            BindButton(hairPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Hairstyle, -1));

            BindButton(hatNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Hat, 1));
            BindButton(hatPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Hat, -1));

            BindButton(outerwearNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Outerwear, 1));
            BindButton(outerwearPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Outerwear, -1));

            BindButton(pantsNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Pants, 1));
            BindButton(pantsPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Pants, -1));

            BindButton(shoesNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Shoes, 1));
            BindButton(shoesPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.Shoes, -1));

            BindButton(skinColorNextBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.SkinColor, 1));
            BindButton(skinColorPrevBtn, () => ChangePart(OfflineCharacterMeshSwapper.CustomizationPart.SkinColor, -1));

            // Kontrol butonları (Geri butonu artık Menu.cs tarafından yönetiliyor)
            BindButton(resetButton, HandleReset);
        }

        private void CleanupButtons()
        {
            RemoveButton(accessoriesNextBtn);
            RemoveButton(accessoriesPrevBtn);
            RemoveButton(faceNextBtn);
            RemoveButton(facePrevBtn);
            RemoveButton(glassesNextBtn);
            RemoveButton(glassesPrevBtn);
            RemoveButton(glovesNextBtn);
            RemoveButton(glovesPrevBtn);
            RemoveButton(hairNextBtn);
            RemoveButton(hairPrevBtn);
            RemoveButton(hatNextBtn);
            RemoveButton(hatPrevBtn);
            RemoveButton(outerwearNextBtn);
            RemoveButton(outerwearPrevBtn);
            RemoveButton(pantsNextBtn);
            RemoveButton(pantsPrevBtn);
            RemoveButton(shoesNextBtn);
            RemoveButton(shoesPrevBtn);
            RemoveButton(skinColorNextBtn);
            RemoveButton(skinColorPrevBtn);
            RemoveButton(resetButton);
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private void RemoveButton(Button button)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        #endregion

        #region Panel Control

        /// <summary>
        /// Özelleştirme panelini açar ve ana menüyü gizler
        /// </summary>
        public void OpenPanel()
        {
            if (_isOpen) return;
            _isOpen = true;

            // Mevcut verileri yükle
            if (meshSwapper != null)
            {
                meshSwapper.LoadCustomizationData();
            }

            LogDebug("Özelleştirme paneli açıldı");
        }

        /// <summary>
        /// Özelleştirme panelini kapatır, kaydeder ve ana menüyü geri getirir
        /// </summary>
        public void ClosePanel()
        {
            if (!_isOpen) return;

            // Kaydet
            if (meshSwapper != null)
            {
                meshSwapper.SaveCustomizationData();
            }

            _isOpen = false;
            LogDebug("Özelleştirme paneli kapatıldı, veriler kaydedildi");
        }

        #endregion

        #region Customization Actions

        private void ChangePart(OfflineCharacterMeshSwapper.CustomizationPart part, int direction)
        {
            if (meshSwapper == null)
            {
                LogWarning("MeshSwapper referansı atanmamış!");
                return;
            }

            meshSwapper.ChangePart(part, direction);
            LogDebug($"{part} değiştirildi (yön: {direction})");
        }

        private void HandleReset()
        {
            if (meshSwapper == null)
            {
                LogWarning("MeshSwapper referansı atanmamış!");
                return;
            }

            meshSwapper.ResetToDefaults();
            LogDebug("Özelleştirme sıfırlandı");
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

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        #endregion
    }
}
