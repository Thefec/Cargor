# B1 — Netcode / Late-Join Bug Hunt (2026-09-18)

Scope: NetworkBehaviour subclasses under `Assets/NewCss/`, `SteamManager.cs` (grep +
targeted reads only), `LobbyManager.cs`, GDD.md §23. Read-only review, no code changed.

## 1. [HIGH] Player count for difficulty scaling is a one-time snapshot — never updates on late join/leave
**File:** `Assets/NewCss/GameState/DifficultyManager.cs:172-184, 248-289, 520-534`

`OnNetworkSpawn()` calls `InitializePlayerCount()` **only if `IsServer`**, which reads
`GetLobbyPlayerCount()` (Steam lobby member count, or `ConnectedClientsList.Count` as
fallback) **once**, at the moment the manager spawns, and writes it into
`_networkPlayerCount`. There is no `OnClientConnectedCallback` / `OnClientDisconnectCallback`
subscription anywhere in the file to refresh it. The only other write path is
`SetPlayerCount(int)`, which is called exclusively from `[ContextMenu]` debug methods
(`DebugSet1Player`...`DebugSet4Players`) — never from live connection events.

`DifficultyManager.PlayerCount` / `UpgradeCostMultiplier` are read by
`CustomerManager.cs:396,428` (customer spawn cadence), `TruckSpawner.cs:612,623`,
`Truck.cs:251`, `PhoneCallManager.cs:62`, `PerkEffect.cs:222,291,355`, and
`UpgradePanel.cs:1535,1650` (upgrade cost multiplier) — i.e. this stale value feeds
directly into economy/difficulty math for the rest of the run.

**Failure scenario:** Host starts solo (`PlayerCount` locks to 1). Three more players
late-join minutes later, mid-day. Difficulty stays scaled for 1 player (fewer customers,
lower patience requirements, cheaper upgrades) for the entire 16-day run even with 4
players now working the shop. Reverse case (a player disconnects mid-run) leaves
difficulty scaled too high for the remaining players — punishing them without the
lost player's throughput. This is directly exploitable as an economy cheese
(start alone, invite friends after spawn) and also a legitimate fairness bug on any
normal late-join.

## 2. [MEDIUM] ServerRpc trusts a client-supplied clientId instead of the RPC sender
**File:** `Assets/NewCss/PlayerSpawner.cs:176-184`

```csharp
[ServerRpc(RequireOwnership = false)]
private void NotifyHostClientReadyServerRpc(ulong clientId)
{
    clientsReady[clientId] = true;
    SpawnPlayerForClient(clientId);
}
```

`clientId` comes from the RPC argument, not `ServerRpcParams.Receive.SenderClientId`. Any
connected client can call this with an arbitrary id (including another player's or a stale
one). Impact is bounded today by guards inside `SpawnPlayerForClient` (already-spawned /
not-connected checks), so it is not an immediate crash or duplication bug, but it is an
identity-spoofing smell in exactly the kind of authority-sensitive path this audit targets.
Note: it's unclear whether `PlayerSpawner`'s "Main Office" flow is still the live spawn path
vs. superseded by `SteamManager`/scene-management spawn flow — worth confirming before
prioritizing a fix.

## 3. [LOW / theoretical] Customer setup is a one-shot ClientRpc a late joiner never receives
**File:** `Assets/NewCss/CustomerSripts/CustomerManager.cs:706-751, 818-838`

`SpawnCustomer()` runs `SetupCustomerAI()` directly on the server, then fires
`SetupCustomerClientRpc(...)` once to whoever is connected *at that moment* to replicate the
same setup (`ai.manager = this`, enable NavMeshAgent/Animator/Collider, wire
waitCanvas/waitBar). A client connecting after this customer already exists will never run
this locally — NGO doesn't replay past RPCs to late joiners.
Checked `Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Customer.prefab`: Animator,
NavMeshAgent and SphereCollider are all `m_Enabled: 1` by default, and
`CustomerAI.InitializeComponents()` (runs in every peer's own `Awake`/`OnNetworkSpawn`,
independent of the manager RPC) already wires `waitCanvas`/`waitBar`/interaction collider.
Every client-side read of `manager` is null-guarded except `TransitionToExit()`
(`CustomerAI.cs:1736-1738`), which only runs from `ServerUpdate()` (server-only). **So with
today's prefab defaults this does not currently manifest as a visible bug** — flagging as a
latent trap: if a future prefab edit disables any of those components pre-setup, or a new
client-side `manager.` read is added without a null guard, late joiners will silently break
with no error message. Not confirmed reachable today; downgraded from suspicion to
informational per the "don't inflate theoretical" instruction.

## 4. [INFORMATIONAL] Verified safe — pickup lock dictionary
`Assets/NewCss/NewPickup/PlayerInventory.cs:136-143, 406-437, 540-628` — the
`s_itemPickupLocks` static `Dictionary<ulong,float>` backing "no double pickup" is
correctly server-only gated (`IsItemLocked`/`TryLockItem`/`UnlockItem` are only invoked from
inside `[ServerRpc] RequestPickupServerRpc`), and is reset on `NetworkManager.OnServerStopped`
(comment cites a prior "G19" host-restart bug already fixed). No new issue found here.

## 5. [INFORMATIONAL] Dead code — never wired
`Assets/NewCss/Network/NetworkObjectPool.cs` is a complete, correctly server-gated
NetworkObject pool (`Get`/`Return`/`Clear` all check `IsServer`), but grep across
`Assets/` found **zero call sites** (`GetComponent<NetworkObjectPool>` or direct references)
anywhere in the project. Not a runtime bug — just flagging in case a prior task assumed it
was handling truck/box/customer spawn pooling and it silently isn't.

## 6. [INFORMATIONAL] Possibly-legacy lobby flow
`Assets/MENUUI/Lobby/LobbyManager.cs` loads a scene named `"GameScene"` (doesn't match
`"The Main Office"` used by `SteamManager`/`PlayerSpawner`) and contains a comment
admitting no relay service is wired up. No scene/prefab reference to this component was
found via guid grep. Likely superseded by `SteamManager`'s lobby flow; not analyzed further
under the assumption it's dead. If it turns out to still be reachable from
`MainMenuUI.cs`, its player-slot UI (`UpdatePlayerSlots`) is driven only by
`ClientRpc` broadcasts tied to connect/disconnect events with no NetworkList backing —
worth a second pass.

---
**Not confirmed / out of budget:** Steam P2P host-migration behavior on host disconnect
(topology is host-authoritative with no migration per GDD §23.1, so client-side disconnect
handling — not migration — is the relevant question; `SteamManager.cs` disconnect/rejection
logic around lines 705-770 is already heavily commented with prior fixes and re-entrancy
guards and reads as deliberately hardened, but a live 2-machine test was not performed here).
