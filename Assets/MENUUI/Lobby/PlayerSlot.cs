using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

[System.Serializable]
public class PlayerSlot : MonoBehaviour
{
    [Header("UI References")]
    public GameObject slotContainer;
    public TMP_Text playerNameText;
    public Image hostCrown;
    public Image slotBackground;
    
    [Header("Colors")]
    public Color occupiedColor = Color.green;
    public Color emptyColor = Color.gray;

    // Localization Keys
    private const string LOC_KEY_EMPTY_SLOT = "EmptySlot";
    
    private void Start()
    {
        ClearSlot();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
    }

    private void HandleLocaleChanged(Locale newLocale)
    {
        // Only update if slot is not occupied (showing "Empty Slot")
        if (slotContainer != null && !slotContainer.activeInHierarchy)
        {
            playerNameText.text = NewCss.LocalizationHelper.GetLocalizedString(LOC_KEY_EMPTY_SLOT);
        }
    }
    
    public void SetPlayer(string playerName, bool isHost = false)
    {
        slotContainer.SetActive(true);
        playerNameText.text = playerName;
        hostCrown.gameObject.SetActive(isHost);
        slotBackground.color = occupiedColor;
    }
    
    public void ClearSlot()
    {
        slotContainer.SetActive(false);
        playerNameText.text = NewCss.LocalizationHelper.GetLocalizedString(LOC_KEY_EMPTY_SLOT);
        hostCrown.gameObject.SetActive(false);
        slotBackground.color = emptyColor;
    }
}