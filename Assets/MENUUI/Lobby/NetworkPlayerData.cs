using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
public class NetworkPlayerData : NetworkBehaviour
{
    [Header("Player Info")]
    private NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>("");
    private NetworkVariable<bool> isReady = new NetworkVariable<bool>(false);
    
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            string savedName = PlayerPrefs.GetString("PlayerName", $"Player{Random.Range(1000, 9999)}");
            SetPlayerNameServerRpc(savedName);
        }
    }
    
    [ServerRpc]
    private void SetPlayerNameServerRpc(string name)
    {
        playerName.Value = name;
    }
    
    [ServerRpc]
    public void SetReadyStatusServerRpc(bool ready)
    {
        isReady.Value = ready;
    }
    
    public string GetPlayerName() => playerName.Value.ToString();
    public bool IsPlayerReady() => isReady.Value;
}