using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
    
    private void Start()
    {
        ClearSlot();
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
        playerNameText.text = "Empty Slot";
        hostCrown.gameObject.SetActive(false);
        slotBackground.color = emptyColor;
    }
}