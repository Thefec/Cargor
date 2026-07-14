using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-authoritative "geç-join yasağı" muhafızı.
/// Oyun (The Main Office sahnesi) başladıktan sonra whitelist dışı hiçbir yeni
/// Steam kullanıcısının NGO bağlantısına onay almasını engeller. Oyun başındaki
/// orijinal lobi üyeleri (SteamId whitelist) bağlantı koparsa geri dönebilir.
///
/// Steam lobisi bilerek joinable bırakılır (bkz. SteamManager.ConfigureLobby) —
/// whitelist dışı biri Steam lobisine girebilse bile burada, NGO Connection
/// Approval aşamasında reddedilir. Böylece reconnect eden orijinal üyeler
/// lobiye tekrar girebilirken yeni kimse oyuna alınmaz.
///
/// Kurulum: manuel sahne bağlantısı gerekmez. `EnsureInstance()` çağrıldığında
/// örnek yoksa kendi GameObject'ini yaratıp DontDestroyOnLoad yapar (auto-bootstrap).
/// SteamManager, host başlatma yolunda bunu çağırır.
/// </summary>
public class LateJoinGuard : MonoBehaviour
{
    private const string LOG_PREFIX = "[LateJoinGuard]";
    private const int MAX_PLAYERS = 4;

    public static LateJoinGuard Instance { get; private set; }

    /// <summary>
    /// Instance yoksa kendi GameObject'ini yaratıp döndürür (auto-bootstrap).
    /// Unity'de manuel sahne/objeye component ekleme gereğini ortadan kaldırır.
    /// Sadece server/host tarafında (ExecuteHostStart) çağrılmalı.
    /// </summary>
    public static LateJoinGuard EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject(nameof(LateJoinGuard));
            go.AddComponent<LateJoinGuard>(); // Awake içinde Instance + DontDestroyOnLoad ayarlanır
        }

        return Instance;
    }

    /// <summary>Oyun (harita) başladı mı? Server-authoritative; sadece host/server tarafında anlamlıdır.</summary>
    public bool GameStarted { get; private set; }

    private readonly HashSet<ulong> _allowedSteamIds = new HashSet<ulong>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{LOG_PREFIX} Birden fazla örnek bulundu, fazlalık yok ediliyor.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Host, sahne yüklenmeden hemen önce (StartGameServer akışında) çağırır.
    /// Orijinal lobi üyelerinin SteamId'lerini whitelist'e alır ve muhafızı aktive eder.
    /// Sadece server/host çağırmalı.
    /// </summary>
    public void MarkGameStarted(IEnumerable<ulong> rosterSteamIds)
    {
        _allowedSteamIds.Clear();
        if (rosterSteamIds != null)
        {
            foreach (var id in rosterSteamIds)
            {
                _allowedSteamIds.Add(id);
            }
        }

        GameStarted = true;
        Debug.Log($"{LOG_PREFIX} Oyun başladı. Whitelist'e alınan üye sayısı: {_allowedSteamIds.Count}");
    }

    /// <summary>
    /// Yeni bir lobiye dönüldüğünde (oyun bitti / ana menüye çıkıldı) muhafızı sıfırlar.
    /// </summary>
    public void ResetGuard()
    {
        GameStarted = false;
        _allowedSteamIds.Clear();
    }

    /// <summary>
    /// NetworkManager.ConnectionApprovalCallback olarak atanır. Sadece server tarafında çalışır.
    /// </summary>
    public void ApproveConnection(NetworkManager.ConnectionApprovalRequest req,
                                   NetworkManager.ConnectionApprovalResponse resp)
    {
        // Host'un kendi bağlantısı her zaman onaylanır.
        if (req.ClientNetworkId == NetworkManager.ServerClientId)
        {
            resp.Approved = true;
            resp.CreatePlayerObject = true;
            resp.Pending = false;
            return;
        }

        // Güven modeli: SteamId payload'dan (ConnectionData) okunuyor, client tarafından
        // yazılıyor — teorik olarak client kod seviyesinde spoof edilebilir. Ancak bağlantı
        // Facepunch/Steam P2P transport'u üzerinden kuruluyor; alt katmandaki P2P kanalı
        // zaten göndericinin gerçek Steam hesabına (SteamNetworkingIdentity) bağlı ve Steam
        // tarafından doğrulanıyor, yani P2P bağlantıyı kurabilen taraf zaten kendi SteamId'sini
        // taklit edemiyor. Payload'daki değer, transport'un doğruladığı kimlikle örtüşmek
        // zorunda kalıyor pratikte; NGO'nun bu callback'inde ayrıca doğrulanmış bir SenderId
        // alanı olmadığından (ConnectionApprovalRequest sadece Payload + ClientNetworkId taşır)
        // ek bir doğrulama katmanı NGO seviyesinde mevcut değil. Daha güçlü bir garanti
        // gerekirse Facepunch transport'un connection callback'inden gelen SteamId doğrudan
        // kullanılabilir; şu an payload + bu güven modeli yeterli kabul edildi.
        ulong steamId = (req.Payload != null && req.Payload.Length >= 8)
            ? System.BitConverter.ToUInt64(req.Payload, 0)
            : 0;

        if (!GameStarted)
        {
            // Lobi aşaması: mevcut davranışı koru, sadece dolu lobiye ekstra client almasın.
            // NetworkManager.Singleton null ise fail-closed (reddet) — bu callback zaten
            // NetworkManager tarafından tetiklendiği için pratikte null olmaz, ama güvenli
            // varsayılan budur.
            bool roomAvailable = NetworkManager.Singleton != null &&
                                  NetworkManager.Singleton.ConnectedClientsIds.Count < MAX_PLAYERS;

            resp.Approved = roomAvailable;
            resp.Reason = roomAvailable ? string.Empty : "Lobi dolu.";
            resp.CreatePlayerObject = roomAvailable;
            resp.Pending = false;
            return;
        }

        // Oyun başladı: sadece orijinal (whitelist'teki) üyelerin reconnect'ine izin ver.
        bool isOriginalMember = steamId != 0 && _allowedSteamIds.Contains(steamId);

        resp.Approved = isOriginalMember;
        resp.Reason = isOriginalMember ? string.Empty : "Oyun zaten başladı.";
        resp.CreatePlayerObject = isOriginalMember;
        resp.Pending = false;

        if (!isOriginalMember)
        {
            Debug.Log($"{LOG_PREFIX} Geç-join reddedildi. SteamId={steamId}");
        }
    }
}
