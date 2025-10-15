using Unity.Netcode;
using Steamworks;

public class SteamIdHolder : NetworkBehaviour
{
    public ulong SteamId { get; private set; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            SteamId = SteamClient.SteamId;
            SetSteamIdServerRpc(SteamId);
        }
    }

    [ServerRpc]
    private void SetSteamIdServerRpc(ulong steamId)
    {
        SteamId = steamId;
    }
}