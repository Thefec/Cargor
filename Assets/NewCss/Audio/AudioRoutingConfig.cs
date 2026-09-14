using UnityEngine;
using UnityEngine.Audio;

namespace NewCss.Audio
{
    /// <summary>
    /// AudioRouting'in Resources üzerinden bir kez yükleyip cache'lediği tek gerçek kaynak.
    /// CargorMixer.mixer'ın kendisi "Assets/Audio/" altında duruyor (bkz. plans/ses-tasarimi.md
    /// §5) — Resources'a taşınmasına gerek yok, bu küçük SO sadece referansları taşıyor ve
    /// "Assets/Resources/Audio/AudioRoutingConfig.asset" olarak duruyor.
    ///
    /// Kurulum: Assets/Editor/AudioMixerSetup.cs (menü: Tools ▸ Cargor ▸ Audio ▸ Mixer Kur
    /// veya Dogrula). Elle düzenlemeye gerek yok, idempotent.
    /// </summary>
    public class AudioRoutingConfig : ScriptableObject
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        public AudioMixer Mixer => mixer;
        public AudioMixerGroup MusicGroup => musicGroup;
        public AudioMixerGroup SfxGroup => sfxGroup;
    }
}
