#if UNITY_EDITOR
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;

namespace NewCss.DevTools
{
    /// <summary>
    /// SADECE EDİTÖR — yerel co-op test aracı (Steam'siz).
    /// Tüm sınıf `#if UNITY_EDITOR` içinde olduğundan gerçek oyuncu build'ine ASLA girmez.
    ///
    /// Amaç: Facepunch/Steam P2P (aynı SteamID ile tek makinede olmaz) yerine UnityTransport
    /// (127.0.0.1) kullanarak, tek makinede iki instance (Unity Multiplayer Play Mode sanal
    /// oyuncuları VEYA iki build) ile host+client oturumu açmak. Böylece client-tarafı görsel/
    /// netcode fix'leri (tır rengi, kutu, müşteri ürün animasyonu) yerelde test edilir.
    ///
    /// KULLANIM:
    ///  1. Menüden aç:  Tools ▸ Cargor ▸ Local Coop Test (127.0.0.1)  (tik işareti görünür)
    ///  2. "The Main Office" sahnesini aç (NetworkManager burada) ve Play'e bas.
    ///     (İstersen Window ▸ Multiplayer Play Mode ile 1 sanal oyuncu aç → 2. instance.)
    ///  3. Ana editörde ekrandaki "HOST başlat" butonuna, sanal oyuncuda "CLIENT başlat"a bas.
    ///  4. Test bitince menüden tik'i kaldır → normal Steam akışı geri gelir.
    ///
    /// Toggle KAPALIYKEN bu araç hiçbir şey yapmaz (GameObject bile oluşturulmaz) → normal
    /// Steam playtest'ine karışmaz.
    /// </summary>
    public class LocalCoopTestBootstrap : MonoBehaviour
    {
        private const string MENU_PATH = "Tools/Cargor/Local Coop Test (127.0.0.1)";
        private const string PREF_KEY = "Cargor_LocalCoopTest_Enabled";
        private const string ADDRESS = "127.0.0.1";
        private const ushort PORT = 7777;

        // ─── Menü toggle ────────────────────────────────────────────────
        [MenuItem(MENU_PATH)]
        private static void ToggleEnabled() => EditorPrefs.SetBool(PREF_KEY, !IsEnabled());

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(MENU_PATH, IsEnabled());
            return true;
        }

        private static bool IsEnabled() => EditorPrefs.GetBool(PREF_KEY, false);

        // ─── Play başlayınca kendini oluştur (sadece toggle açıksa) ──────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (!IsEnabled()) return;

            var go = new GameObject("[LocalCoopTestBootstrap]");
            go.AddComponent<LocalCoopTestBootstrap>();
            DontDestroyOnLoad(go);
        }

        // ─── Ekran UI ───────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!IsEnabled()) return;

            GUILayout.BeginArea(new Rect(10, 10, 280, 160), GUI.skin.box);
            GUILayout.Label("● YEREL CO-OP TEST (Steam baypas)");

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                GUILayout.Label("NetworkManager yok.\n'The Main Office' sahnesini aç.");
                GUILayout.EndArea();
                return;
            }

            if (nm.IsListening)
            {
                string role = nm.IsHost ? "HOST" : (nm.IsClient ? "CLIENT" : "?");
                GUILayout.Label($"Durum: {role}");
                GUILayout.Label($"Bağlı client: {nm.ConnectedClients.Count}");
                if (GUILayout.Button("■ Durdur (Shutdown)")) nm.Shutdown();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Transport → UnityTransport {ADDRESS}:{PORT}");
            if (GUILayout.Button("▶ HOST başlat (bu instance)")) StartLocal(asHost: true);
            if (GUILayout.Button("▶ CLIENT başlat (bu instance)")) StartLocal(asHost: false);
            GUILayout.EndArea();
        }

        // ─── Bağlantı başlatma ──────────────────────────────────────────
        private void StartLocal(bool asHost)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsListening) return;

            // 1) Facepunch yerine UnityTransport'a geç (127.0.0.1).
            var utp = nm.GetComponent<UnityTransport>();
            if (utp == null) utp = nm.gameObject.AddComponent<UnityTransport>();
            utp.SetConnectionData(ADDRESS, PORT);
            nm.NetworkConfig.NetworkTransport = utp;

            // 2) Steam whitelist/geç-join reddini baypas et — yerel client kimliksiz bağlanır.
            nm.NetworkConfig.ConnectionApproval = false;
            nm.ConnectionApprovalCallback = null;

            // 3) Başlat.
            bool ok = asHost ? nm.StartHost() : nm.StartClient();
            Debug.Log($"[LocalCoopTest] {(asHost ? "StartHost" : "StartClient")} → {ok} @ {ADDRESS}:{PORT}");
        }
    }
}
#endif
