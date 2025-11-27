using System;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Network karakter özelleþtirme UI kontrolcüsü - karakter customization butonlarýný ve 
    /// NetworkCharacterMeshSwapper ile iletiþimi yönetir. 
    /// </summary>
    public class NetworkCharacterCustomizationUI : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CharacterCustomizationUI]";
        private const string DEFAULT_CHARACTER_TAG = "Player";

        #endregion

        #region Enums

        /// <summary>
        /// Özelleþtirme kategorileri
        /// </summary>
        public enum CustomizationCategory
        {
            Accessories,
            Face,
            Glasses,
            Gloves,
            Hair,
            Hat,
            Outerwear,
            Pants,
            Shoes,
            Skin
        }

        #endregion

        #region Serialized Fields

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Özelleþtirme paneli")]
        public GameObject panel;

        [Header("=== SWAPPER REFERENCE ===")]
        [SerializeField, Tooltip("Network karakter mesh swapper")]
        private NetworkCharacterMeshSwapper networkSwapper;

        [Header("=== AUTO REFERENCE SETTINGS ===")]
        [SerializeField, Tooltip("Karakter tag'i")]
        public string characterTag = DEFAULT_CHARACTER_TAG;

        [SerializeField, Tooltip("Start'ta referans bul")]
        public bool findOnStart = true;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarýný göster")]
        private bool showDebugLogs;

        #endregion

        #region Events

        /// <summary>
        /// Swapper bulunduðunda tetiklenir
        /// </summary>
        public event Action<NetworkCharacterMeshSwapper> OnSwapperFound;

        /// <summary>
        /// Özelleþtirme deðiþtiðinde tetiklenir
        /// </summary>
        public event Action<CustomizationCategory, int> OnCustomizationChanged;

        #endregion

        #region Public Properties

        /// <summary>
        /// Swapper geçerli mi?
        /// </summary>
        public bool HasValidSwapper => networkSwapper != null && networkSwapper.IsOwner;

        /// <summary>
        /// Özelleþtirme yapýlabilir mi?
        /// </summary>
        public bool CanCustomize => HasValidSwapper;

        /// <summary>
        /// Mevcut swapper referansý
        /// </summary>
        public NetworkCharacterMeshSwapper CurrentSwapper => networkSwapper;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (findOnStart)
            {
                FindSwapperReference();
            }
        }

        #endregion

        #region Swapper Reference Finding

        /// <summary>
        /// NetworkCharacterMeshSwapper referansýný bulur
        /// </summary>
        public void FindSwapperReference()
        {
            // 1. Local player'dan bul
            if (TryFindFromLocalPlayer())
            {
                return;
            }

            // 2. Tag ile bul
            if (TryFindByTag())
            {
                return;
            }

            // 3.  Ownership ile bul
            if (TryFindByOwnership())
            {
                return;
            }

            LogWarning("NetworkCharacterMeshSwapper could not be found!");
        }

        private bool TryFindFromLocalPlayer()
        {
            if (NetworkManager.Singleton == null) return false;
            if (NetworkManager.Singleton.LocalClient == null) return false;

            var localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObject == null) return false;

            networkSwapper = localPlayerObject.GetComponent<NetworkCharacterMeshSwapper>();

            if (networkSwapper != null)
            {
                LogDebug("NetworkCharacterMeshSwapper found on local player");
                NotifySwapperFound();
                return true;
            }

            return false;
        }

        private bool TryFindByTag()
        {
            if (string.IsNullOrEmpty(characterTag)) return false;

            GameObject character = GameObject.FindGameObjectWithTag(characterTag);
            if (character == null) return false;

            networkSwapper = character.GetComponent<NetworkCharacterMeshSwapper>();

            if (networkSwapper != null)
            {
                LogDebug($"NetworkCharacterMeshSwapper found by tag: {characterTag}");
                NotifySwapperFound();
                return true;
            }

            return false;
        }

        private bool TryFindByOwnership()
        {
            var allSwappers = FindObjectsOfType<NetworkCharacterMeshSwapper>();

            foreach (var swapper in allSwappers)
            {
                if (swapper.IsOwner)
                {
                    networkSwapper = swapper;
                    LogDebug("NetworkCharacterMeshSwapper found by ownership");
                    NotifySwapperFound();
                    return true;
                }
            }

            return false;
        }

        private void NotifySwapperFound()
        {
            OnSwapperFound?.Invoke(networkSwapper);
        }

        /// <summary>
        /// Karakter spawn edildiðinde çaðrýlýr
        /// </summary>
        public void OnCharacterSpawned(GameObject spawnedCharacter)
        {
            if (spawnedCharacter == null)
            {
                LogWarning("Spawned character is null");
                return;
            }

            networkSwapper = spawnedCharacter.GetComponent<NetworkCharacterMeshSwapper>();

            if (networkSwapper != null)
            {
                LogDebug("NetworkCharacterMeshSwapper assigned from spawned character");
                NotifySwapperFound();
            }
            else
            {
                LogWarning("Spawned character has no NetworkCharacterMeshSwapper component");
            }
        }

        /// <summary>
        /// Swapper'ý manuel olarak ayarlar
        /// </summary>
        public void SetSwapper(NetworkCharacterMeshSwapper swapper)
        {
            networkSwapper = swapper;

            if (networkSwapper != null)
            {
                LogDebug("NetworkCharacterMeshSwapper manually assigned");
                NotifySwapperFound();
            }
        }

        #endregion

        #region Validation

        private bool ValidateSwapper()
        {
            if (networkSwapper == null)
            {
                FindSwapperReference();
            }

            return networkSwapper != null && networkSwapper.IsOwner;
        }

        #endregion

        #region UI Button Methods - Individual

        /// <summary>
        /// Aksesuar deðiþtirir
        /// </summary>
        public void ChangeAccessories(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Accessories, direction,
                () => networkSwapper.ChangeAccessories(direction));
        }

        /// <summary>
        /// Yüz deðiþtirir
        /// </summary>
        public void ChangeFace(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Face, direction,
                () => networkSwapper.ChangeFaces(direction));
        }

        /// <summary>
        /// Gözlük deðiþtirir
        /// </summary>
        public void ChangeGlasses(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Glasses, direction,
                () => networkSwapper.ChangeGlasses(direction));
        }

        /// <summary>
        /// Eldiven deðiþtirir
        /// </summary>
        public void ChangeGloves(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Gloves, direction,
                () => networkSwapper.ChangeGloves(direction));
        }

        /// <summary>
        /// Saç deðiþtirir
        /// </summary>
        public void ChangeHair(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Hair, direction,
                () => networkSwapper.ChangeHairstyle(direction));
        }

        /// <summary>
        /// Þapka deðiþtirir
        /// </summary>
        public void ChangeHat(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Hat, direction,
                () => networkSwapper.ChangeHat(direction));
        }

        /// <summary>
        /// Üst giysi deðiþtirir
        /// </summary>
        public void ChangeOuterwear(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Outerwear, direction,
                () => networkSwapper.ChangeOuterwear(direction));
        }

        /// <summary>
        /// Pantolon deðiþtirir
        /// </summary>
        public void ChangePant(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Pants, direction,
                () => networkSwapper.ChangePants(direction));
        }

        /// <summary>
        /// Ayakkabý deðiþtirir
        /// </summary>
        public void ChangeShoes(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Shoes, direction,
                () => networkSwapper.ChangeShoes(direction));
        }

        /// <summary>
        /// Ten rengi deðiþtirir
        /// </summary>
        public void ChangeSkin(int direction)
        {
            ExecuteCustomization(CustomizationCategory.Skin, direction,
                () => networkSwapper.ChangeSkinColor(direction));
        }

        #endregion

        #region UI Button Methods - Generic

        /// <summary>
        /// Kategori bazlý özelleþtirme deðiþtirir
        /// </summary>
        public void ChangeCustomization(CustomizationCategory category, int direction)
        {
            switch (category)
            {
                case CustomizationCategory.Accessories:
                    ChangeAccessories(direction);
                    break;
                case CustomizationCategory.Face:
                    ChangeFace(direction);
                    break;
                case CustomizationCategory.Glasses:
                    ChangeGlasses(direction);
                    break;
                case CustomizationCategory.Gloves:
                    ChangeGloves(direction);
                    break;
                case CustomizationCategory.Hair:
                    ChangeHair(direction);
                    break;
                case CustomizationCategory.Hat:
                    ChangeHat(direction);
                    break;
                case CustomizationCategory.Outerwear:
                    ChangeOuterwear(direction);
                    break;
                case CustomizationCategory.Pants:
                    ChangePant(direction);
                    break;
                case CustomizationCategory.Shoes:
                    ChangeShoes(direction);
                    break;
                case CustomizationCategory.Skin:
                    ChangeSkin(direction);
                    break;
            }
        }

        /// <summary>
        /// Sonraki seçeneðe geçer
        /// </summary>
        public void NextOption(CustomizationCategory category)
        {
            ChangeCustomization(category, 1);
        }

        /// <summary>
        /// Önceki seçeneðe geçer
        /// </summary>
        public void PreviousOption(CustomizationCategory category)
        {
            ChangeCustomization(category, -1);
        }

        #endregion

        #region Customization Execution

        private void ExecuteCustomization(CustomizationCategory category, int direction, Action customizationAction)
        {
            if (!ValidateSwapper())
            {
                LogWarning($"Cannot change {category} - swapper not valid");
                return;
            }

            customizationAction?.Invoke();
            OnCustomizationChanged?.Invoke(category, direction);

            LogDebug($"{category} changed by {direction}");
        }

        #endregion

        #region UI Display Methods

        /// <summary>
        /// UI görüntüsünü günceller
        /// </summary>
        public void UpdateUIDisplay()
        {
            if (!ValidateSwapper())
            {
                LogWarning("Cannot update UI display - swapper not valid");
                return;
            }

            var state = GetCurrentCustomizationState();
            LogDebug($"Current State: {state}");
        }

        /// <summary>
        /// Mevcut özelleþtirme durumunu döndürür
        /// </summary>
        public CustomizationState GetCurrentCustomizationState()
        {
            if (!ValidateSwapper())
            {
                return new CustomizationState();
            }

            return new CustomizationState
            {
                Accessories = networkSwapper.GetAccessoriesIndex(),
                Face = networkSwapper.GetFacesIndex(),
                Hairstyle = networkSwapper.GetHairstyleIndex(),
                // Diðer index'ler swapper'da varsa eklenebilir
            };
        }

        /// <summary>
        /// Belirli bir kategorinin mevcut index'ini döndürür
        /// </summary>
        public int GetCurrentIndex(CustomizationCategory category)
        {
            if (!ValidateSwapper())
            {
                return -1;
            }

            return category switch
            {
                CustomizationCategory.Accessories => networkSwapper.GetAccessoriesIndex(),
                CustomizationCategory.Face => networkSwapper.GetFacesIndex(),
                CustomizationCategory.Hair => networkSwapper.GetHairstyleIndex(),
                // Diðer kategoriler eklenebilir
                _ => -1
            };
        }

        #endregion

        #region Panel Control

        /// <summary>
        /// Paneli gösterir
        /// </summary>
        public void ShowPanel()
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        /// <summary>
        /// Paneli gizler
        /// </summary>
        public void HidePanel()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        /// <summary>
        /// Panel görünürlüðünü toggle eder
        /// </summary>
        public void TogglePanel()
        {
            if (panel != null)
            {
                panel.SetActive(!panel.activeSelf);
            }
        }

        /// <summary>
        /// Panel açýk mý? 
        /// </summary>
        public bool IsPanelOpen => panel != null && panel.activeSelf;

        #endregion

        #region Data Structures

        /// <summary>
        /// Mevcut özelleþtirme durumu
        /// </summary>
        [Serializable]
        public struct CustomizationState
        {
            public int Accessories;
            public int Face;
            public int Glasses;
            public int Gloves;
            public int Hairstyle;
            public int Hat;
            public int Outerwear;
            public int Pants;
            public int Shoes;
            public int Skin;

            public override string ToString()
            {
                return $"Acc:{Accessories}, Face:{Face}, Hair:{Hairstyle}, Hat:{Hat}, " +
                       $"Outer:{Outerwear}, Pants:{Pants}, Shoes:{Shoes}, Skin:{Skin}";
            }
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

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Find Swapper Reference")]
        private void DebugFindSwapperReference()
        {
            FindSwapperReference();
        }

        [ContextMenu("Update UI Display")]
        private void DebugUpdateUIDisplay()
        {
            UpdateUIDisplay();
        }

        [ContextMenu("Show Panel")]
        private void DebugShowPanel()
        {
            ShowPanel();
        }

        [ContextMenu("Hide Panel")]
        private void DebugHidePanel()
        {
            HidePanel();
        }

        [ContextMenu("Test: Change Hair +1")]
        private void DebugChangeHairNext()
        {
            ChangeHair(1);
        }

        [ContextMenu("Test: Change Hair -1")]
        private void DebugChangeHairPrev()
        {
            ChangeHair(-1);
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === CUSTOMIZATION UI STATE ===");
            Debug.Log($"Has Valid Swapper: {HasValidSwapper}");
            Debug.Log($"Can Customize: {CanCustomize}");
            Debug.Log($"Is Panel Open: {IsPanelOpen}");
            Debug.Log($"Character Tag: {characterTag}");
            Debug.Log($"Find On Start: {findOnStart}");

            if (networkSwapper != null)
            {
                Debug.Log($"Swapper Object: {networkSwapper.name}");
                Debug.Log($"Swapper IsOwner: {networkSwapper.IsOwner}");
                Debug.Log($"Current State: {GetCurrentCustomizationState()}");
            }
            else
            {
                Debug.Log("Swapper: NULL");
            }
        }

        [ContextMenu("Debug: Print All Categories")]
        private void DebugPrintAllCategories()
        {
            Debug.Log($"{LOG_PREFIX} === CUSTOMIZATION CATEGORIES ===");

            foreach (CustomizationCategory category in Enum.GetValues(typeof(CustomizationCategory)))
            {
                int index = GetCurrentIndex(category);
                Debug.Log($"  {category}: {index}");
            }
        }
#endif

        #endregion
    }
}