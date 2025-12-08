using NewCss.UIScripts;
using UnityEngine;

public class UITriggerZone : MonoBehaviour
{
    [SerializeField] private PagedUIPanel pagedUIPanel;
    [SerializeField] private string playerTag = "Player";

    [Header("Ayarlar")]
    [SerializeField] private bool openOnEnter = true;
    [SerializeField] private bool closeOnExit = true;

    private void OnTriggerEnter(Collider other)
    {
        if (openOnEnter && other.CompareTag(playerTag))
        {
            pagedUIPanel.OpenPanel();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (closeOnExit && other.CompareTag(playerTag))
        {
            pagedUIPanel.ClosePanel();
        }
    }
}