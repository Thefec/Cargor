using NewCss;
using NewCss.UIScripts;
using Unity.Netcode;
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
        if (!openOnEnter || !TryGetLocalPlayer(other, out PlayerMovement playerMovement)) return;

        pagedUIPanel.SetLocalPlayer(playerMovement);
        pagedUIPanel.OpenPanel();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!closeOnExit || !TryGetLocalPlayer(other, out _)) return;

        pagedUIPanel.ClosePanel();
    }

    private bool TryGetLocalPlayer(Collider other, out PlayerMovement playerMovement)
    {
        playerMovement = null;

        if (!other.CompareTag(playerTag)) return false;

        var networkObject = other.GetComponent<NetworkObject>() ?? other.GetComponentInParent<NetworkObject>();
        if (networkObject == null || !networkObject.IsOwner) return false;

        playerMovement = other.GetComponent<PlayerMovement>() ?? other.GetComponentInParent<PlayerMovement>();
        return true;
    }
}