#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NewCss.DevTools
{
    /// <summary>
    /// SADECE EDİTÖR — telsiz paket kaydet/oynat aracı (plan Adım 4b).
    ///
    /// AMAÇ: Aynı makinede/hesapta muhtemelen iki Steam-voice sürecinin (iki Unity instance'ı)
    /// aynı mikrofonu paylaşamaması sorununu aşmak (plan riski #3, doğrulanmadı). "Konuşan" taraf
    /// canlı mikrofon yerine burada kaydedilmiş bir akışı OYNATIR — kayıt doğrudan
    /// RadioVoiceRuntime.Playback.HandleIncomingPacket'e RadioVoiceRuntime.DevReplayClientId ile
    /// beslenir (canlı mikrofondan gelen bir paketle AYNI koddan geçer). Dalga 3'ün 2-instance ağ
    /// doğrulaması buna dayanacak.
    ///
    /// Bu dosya SADECE paket kaydet/oynat yapar; ağ gecikme/jitter/kayıp SİMÜLATÖRÜ Dalga 4'te
    /// (Adım 9) ayrı bir araca eklenecek — burada karıştırılmıyor.
    ///
    /// KULLANIM:
    ///  1. Menüden aç: Tools ▸ Cargor ▸ Voice ▸ Kayıt-Oynatma Aracı (tik görünür)
    ///  2. Play'e bas, PTT'ye bas-konuş, ekrandaki "● Kaydı Başlat"a bas, konuş, "■ Kaydı Durdur"a bas.
    ///  3. Diskteki dosyayı (yol ekranda yazılı, Application.persistentDataPath altında) diğer
    ///     instance'a/makineye kopyala.
    ///  4. Diğer instance'ta "▶ Oynatmayı Başlat" — kayıt RadioVoicePlayback'e canlı mikrofon gibi beslenir.
    ///
    /// Toggle KAPALIYKEN bu araç hiçbir şey yapmaz (GameObject oluşturulmaz) → normal playtest'e karışmaz.
    /// </summary>
    public class RadioVoiceDevTools : MonoBehaviour
    {
        private const string MENU_PATH = "Tools/Cargor/Voice/Kayıt-Oynatma Aracı";
        private const string PREF_KEY = "Cargor_VoiceDevTools_Enabled";

        private static readonly string RecordingPath =
            Path.Combine(Application.persistentDataPath, "CargorVoiceRecordings", "capture.bin");

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (!IsEnabled()) return;

            var go = new GameObject("[RadioVoiceDevTools]");
            go.AddComponent<RadioVoiceDevTools>();
            DontDestroyOnLoad(go);
        }

        // ─── Kayıt durumu ───────────────────────────────────────────────
        private bool _isRecording;
        private double _recordStartTime;
        private readonly List<(double t, byte[] data)> _recordedPackets = new();

        // ─── Oynatma durumu ─────────────────────────────────────────────
        private bool _isReplaying;
        private double _replayStartTime;
        private int _replayIndex;
        private List<(double t, byte[] data)> _replayPackets;

        // RadioVoiceRuntime.Instance, bu bileşen [RuntimeInitializeOnLoadMethod] ile aynı
        // AfterSceneLoad aşamasında oluştuğu için henüz null olabilir (script'ler arası çalıştırma
        // sırası garanti değil) — bu yüzden OnEnable'da değil, Update'te "geç bağlanma" ile abone
        // olunuyor: Instance her değiştiğinde (null->var ya da obje yeniden kurulunca) yeniden bağlan.
        private RadioVoiceRuntime _subscribedRuntime;

        private void OnDisable()
        {
            if (_subscribedRuntime != null)
            {
                _subscribedRuntime.Capture.PacketProduced -= OnPacketForRecording;
                _subscribedRuntime = null;
            }
        }

        private void Update()
        {
            if (RadioVoiceRuntime.Instance != _subscribedRuntime)
            {
                if (_subscribedRuntime != null) _subscribedRuntime.Capture.PacketProduced -= OnPacketForRecording;
                _subscribedRuntime = RadioVoiceRuntime.Instance;
                if (_subscribedRuntime != null) _subscribedRuntime.Capture.PacketProduced += OnPacketForRecording;
            }

            if (!_isReplaying || _replayPackets == null || _subscribedRuntime == null) return;

            double elapsed = Time.unscaledTimeAsDouble - _replayStartTime;

            while (_replayIndex < _replayPackets.Count && _replayPackets[_replayIndex].t <= elapsed)
            {
                var (_, data) = _replayPackets[_replayIndex];
                _subscribedRuntime.Playback.HandleIncomingPacket(RadioVoiceRuntime.DevReplayClientId, new ArraySegment<byte>(data));
                _replayIndex++;
            }

            if (_replayIndex >= _replayPackets.Count)
            {
                _isReplaying = false; // akış bitti, otomatik durdu
            }
        }

        private void OnPacketForRecording(ArraySegment<byte> packet)
        {
            if (!_isRecording) return;

            // RadioVoiceCapture'ın paylaşılan tek buffer'ı bir sonraki paketle üzerine yazılacak —
            // burada TUTMAK güvenli değil, bu yüzden KOPYALANIYOR. Bu allocation sadece kayıt AÇIKKEN
            // ve 30Hz'de olur; bu dosya ana thread'de çalışan editor-only bir araç (audio thread
            // DEĞİL) — RadioVoiceCapture'ın kendi "0 B GC alloc" kuralı bu dosyaya uygulanmaz.
            byte[] copy = new byte[packet.Count];
            Buffer.BlockCopy(packet.Array, packet.Offset, copy, 0, packet.Count);
            _recordedPackets.Add((Time.unscaledTimeAsDouble - _recordStartTime, copy));
        }

        private void OnGUI()
        {
            if (!IsEnabled()) return;

            GUILayout.BeginArea(new Rect(300, 10, 340, 220), GUI.skin.box);
            GUILayout.Label("● TELSİZ PAKET KAYIT/OYNATMA (Adım 4b)");

            if (_subscribedRuntime == null)
            {
                GUILayout.Label("RadioVoiceRuntime henüz kurulmadı (sahne yükleniyor olabilir).");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Dosya: {RecordingPath}");

            if (!_isRecording)
            {
                using (new EditorGUI.DisabledScope(_isReplaying))
                {
                    if (GUILayout.Button("● Kaydı Başlat")) StartRecording();
                }
            }
            else
            {
                GUILayout.Label($"KAYITTA... {_recordedPackets.Count} paket");
                if (GUILayout.Button("■ Kaydı Durdur ve Kaydet")) StopRecordingAndSave();
            }

            GUILayout.Space(8);

            if (!_isReplaying)
            {
                bool hasFile = File.Exists(RecordingPath);
                using (new EditorGUI.DisabledScope(!hasFile || _isRecording))
                {
                    if (GUILayout.Button("▶ Oynatmayı Başlat")) StartReplay();
                }
                if (!hasFile) GUILayout.Label("(disk üzerinde kayıt yok)");
            }
            else
            {
                GUILayout.Label($"OYNATILIYOR... {_replayIndex}/{_replayPackets?.Count ?? 0}");
                if (GUILayout.Button("■ Oynatmayı Durdur")) _isReplaying = false;
            }

            GUILayout.EndArea();
        }

        private void StartRecording()
        {
            _recordedPackets.Clear();
            _recordStartTime = Time.unscaledTimeAsDouble;
            _isRecording = true;
        }

        private void StopRecordingAndSave()
        {
            _isRecording = false;

            var dir = Path.GetDirectoryName(RecordingPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var stream = new FileStream(RecordingPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(_recordedPackets.Count);
                foreach (var (t, data) in _recordedPackets)
                {
                    writer.Write(t);
                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }

            Debug.Log($"[RadioVoiceDevTools] {_recordedPackets.Count} paket kaydedildi -> {RecordingPath}");
        }

        private void StartReplay()
        {
            var loaded = new List<(double t, byte[] data)>();

            using (var stream = new FileStream(RecordingPath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    double t = reader.ReadDouble();
                    int len = reader.ReadInt32();
                    byte[] data = reader.ReadBytes(len);
                    loaded.Add((t, data));
                }
            }

            _replayPackets = loaded;
            _replayIndex = 0;
            _replayStartTime = Time.unscaledTimeAsDouble;
            _isReplaying = true;

            Debug.Log($"[RadioVoiceDevTools] {loaded.Count} paket yüklendi, oynatma başladı <- {RecordingPath}");
        }
    }
}
#endif
