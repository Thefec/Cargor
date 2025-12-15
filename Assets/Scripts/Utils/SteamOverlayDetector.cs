
using UnityEngine;
using Steamworks;

public class SteamOverlayDetector : MonoBehaviour
{
    public static bool IsOverlayActive { get; private set; }
    private float checkInterval = 1.0f; // Check every 1 second
    private float timer;

    void Update()
    {
        timer += Time.unscaledDeltaTime;

        if (timer >= checkInterval)
        {
            timer = 0f;
            if (SteamManager.Initialized)
            {
                IsOverlayActive = SteamAPI.IsGameOverlayActive();
            }
        }
    }
}
