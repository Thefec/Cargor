using UnityEngine;
using Unity.Netcode;
using NewCss;

public class NetworkCharacterCustomizationUI : MonoBehaviour
{
    public GameObject panel;
    [SerializeField] private NetworkCharacterMeshSwapper networkSwapper;

    [Header("Auto Reference Settings")]
    public string characterTag = "Player";
    public bool findOnStart = true;

    void Start()
    {
        if (findOnStart)
        {
            FindSwapperReference();
        }
    }

    public void FindSwapperReference()
    {
        // Try to find the local player's character first
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObject != null)
            {
                networkSwapper = localPlayerObject.GetComponent<NetworkCharacterMeshSwapper>();
                if (networkSwapper != null)
                {
                    Debug.Log("NetworkCharacterMeshSwapper found on local player");
                    return;
                }
            }
        }

        // Fallback: Find by tag
        if (!string.IsNullOrEmpty(characterTag))
        {
            GameObject character = GameObject.FindGameObjectWithTag(characterTag);
            if (character != null)
            {
                networkSwapper = character.GetComponent<NetworkCharacterMeshSwapper>();
                if (networkSwapper != null)
                {
                    Debug.Log("NetworkCharacterMeshSwapper found by tag: " + characterTag);
                    return;
                }
            }
        }

        // Last resort: Find any NetworkCharacterMeshSwapper that belongs to local player
        var allSwappers = FindObjectsOfType<NetworkCharacterMeshSwapper>();
        foreach (var swapper in allSwappers)
        {
            if (swapper.IsOwner)
            {
                networkSwapper = swapper;
                Debug.Log("NetworkCharacterMeshSwapper found by ownership");
                return;
            }
        }

        Debug.LogWarning("NetworkCharacterMeshSwapper could not be found!");
    }

    // Called when character is spawned
    public void OnCharacterSpawned(GameObject spawnedCharacter)
    {
        if (spawnedCharacter != null)
        {
            networkSwapper = spawnedCharacter.GetComponent<NetworkCharacterMeshSwapper>();
            if (networkSwapper != null)
            {
                Debug.Log("NetworkCharacterMeshSwapper assigned from spawned character");
            }
        }
    }

    private bool IsSwapperValid()
    {
        if (networkSwapper == null)
        {
            FindSwapperReference();
        }
        return networkSwapper != null && networkSwapper.IsOwner;
    }

    // UI Button Methods - These will be called from UI buttons
    public void ChangeAccessories(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeAccessories(dir);
    }

    public void ChangeFace(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeFaces(dir);
    }

    public void ChangeGlasses(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeGlasses(dir);
    }

    public void ChangeGloves(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeGloves(dir);
    }

    public void ChangeHair(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeHairstyle(dir);
    }

    public void ChangeHat(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeHat(dir);
    }

    public void ChangeOuterwear(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeOuterwear(dir);
    }

    public void ChangePant(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangePants(dir);
    }

    public void ChangeShoes(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeShoes(dir);
    }

    public void ChangeSkin(int dir)
    {
        if (!IsSwapperValid()) return;
        networkSwapper.ChangeSkinColor(dir);
    }

    // Method to get current customization state (for UI display)
    public void UpdateUIDisplay()
    {
        if (!IsSwapperValid()) return;
        
        // You can use these to update UI text or image displays
        Debug.Log($"Current Accessories: {networkSwapper.GetAccessoriesIndex()}");
        Debug.Log($"Current Face: {networkSwapper.GetFacesIndex()}");
        Debug.Log($"Current Hairstyle: {networkSwapper.GetHairstyleIndex()}");
        // Add more as needed...
    }

    // Helper method to check if we can customize (useful for enabling/disabling UI)
    public bool CanCustomize()
    {
        return IsSwapperValid();
    }
}