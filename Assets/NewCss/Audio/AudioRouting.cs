using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace NewCss.Audio
{
    /// <summary>
    /// Kategori (Music/SFX) -&gt; AudioMixerGroup çözen, mixer/config'i bir kez yükleyip
    /// cache'leyen statik yardımcı. Runtime'da AudioSource kuran HER yer bunu çağırmalı
    /// (bkz. plans/ses-tasarimi.md §5 Faz A). "Update'te GetComponent çağırma" kuralının
    /// ses tarafındaki eşdeğeri: Resources.Load burada sadece İLK çağrıda çalışır.
    ///
    /// TELSİZE DOKUNMAZ: RadioVoicePrefs / RadioVoiceSpeakerSlot / RadioVoiceRuntime kasten bu
    /// zincirin dışında kalır (bkz. UnifiedSettingsManager.cs:1049 civarı sınıf yorumu).
    /// </summary>
    public static class AudioRouting
    {
        private const string ConfigResourcePath = "Audio/AudioRoutingConfig";

        private static AudioRoutingConfig _config;
        private static bool _loadAttempted;
        private static readonly Dictionary<AudioCategory, AudioSource> _fallbackSources = new Dictionary<AudioCategory, AudioSource>();

        private static AudioRoutingConfig Config
        {
            get
            {
                if (_config == null && !_loadAttempted)
                {
                    _loadAttempted = true;
                    _config = Resources.Load<AudioRoutingConfig>(ConfigResourcePath);
                    if (_config == null)
                    {
                        Debug.LogError($"[AudioRouting] Resources/{ConfigResourcePath}.asset bulunamadı — " +
                                        "sesler mixer'a yönlenmeyecek. Tools ▸ Cargor ▸ Audio ▸ Mixer Kur veya Dogrula çalıştırılmalı.");
                    }
                }
                return _config;
            }
        }

        /// <summary>ApplyAudioSettings gibi mixer'a doğrudan SetFloat çağırması gereken
        /// yerler için (bkz. UnifiedSettingsManager.cs).</summary>
        public static AudioMixer GetMixer() => Config != null ? Config.Mixer : null;

        public static AudioMixerGroup GetGroup(AudioCategory category)
        {
            var cfg = Config;
            if (cfg == null) return null;

            switch (category)
            {
                case AudioCategory.Music: return cfg.MusicGroup;
                case AudioCategory.SFX: return cfg.SfxGroup;
                default: return null;
            }
        }

        /// <summary>Bir AudioSource'u ilgili mixer grubuna bağlar. Grup bulunamazsa
        /// (config eksik) source'a dokunmaz — sessizce eski (Master) çıkışta kalır.</summary>
        public static void Route(AudioSource source, AudioCategory category)
        {
            if (source == null) return;

            var group = GetGroup(category);
            if (group != null) source.outputAudioMixerGroup = group;
        }

        /// <summary>
        /// AudioSource.PlayClipAtPoint YERİNE kullan — o metot mixer'a hiç uğramaz, dolayısıyla
        /// SFX slider'ını atlar (bkz. plans/ses-tasarimi.md §5 mimari kararlar). Kategoriye göre
        /// havuzlanmış, kalıcı (DontDestroyOnLoad), mixer'a bağlı tek bir AudioSource üzerinden
        /// PlayOneShot çağırır — 2D (spatialBlend 0), konum argümanı gerekmiyor.
        /// </summary>
        public static void PlayOneShot(AudioCategory category, AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            var source = GetOrCreateFallbackSource(category);
            if (source != null) source.PlayOneShot(clip, volume);
        }

        private static AudioSource GetOrCreateFallbackSource(AudioCategory category)
        {
            if (_fallbackSources.TryGetValue(category, out var existing) && existing != null)
                return existing;

            var go = new GameObject($"AudioRouting_FallbackSource_{category}");
            Object.DontDestroyOnLoad(go);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            Route(source, category);

            _fallbackSources[category] = source;
            return source;
        }
    }
}
