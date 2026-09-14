using System;

namespace NewCss.Audio
{
    /// <summary>
    /// Lineer (0..1) slider değerini AudioMixer'ın dB skalasına çeviren ve kategori ->
    /// grup adı / exposed parametre adı eşlemesini taşıyan saf mantık. UnityEngine
    /// bağımsız (bkz. NewCss.Audio.Core.asmdef, noEngineReferences=true — NewCss.UI.Core /
    /// NumberRoller ile aynı desen) — EditMode testinden Unity başlatmadan çağrılabilir.
    ///
    /// TUZAK: Log10(0) = -Infinity. AudioMixer.SetFloat bu değeri kabul etmiyor / click-pop
    /// üretebiliyor. 0 (veya çok küçük) girişte MinDecibel'e klipleniyoruz.
    /// </summary>
    public static class AudioVolumeMath
    {
        public const float MinDecibel = -80f;
        public const float MaxDecibel = 0f;

        /// <summary>Slider'ın altında "duyulabilir sıfır" kabul edilen eşik — bunun altı
        /// doğrudan MinDecibel'e klipleniyor, Log10 hiç çağrılmıyor.</summary>
        private const float SilenceThreshold = 0.0001f;

        public static float LinearToDecibel(float linear)
        {
            if (linear <= SilenceThreshold) return MinDecibel;

            float db = 20f * (float)Math.Log10(linear);
            return Clamp(db, MinDecibel, MaxDecibel);
        }

        /// <summary>CargorMixer.mixer içindeki grup adı (bkz. Assets/Editor/AudioMixerSetup.cs).</summary>
        public static string GroupNameFor(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Music: return "Music";
                case AudioCategory.SFX: return "SFX";
                default: throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        /// <summary>CargorMixer.mixer'da dışarı açılan (exposed) float parametre adı.</summary>
        public static string ExposedParamFor(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Music: return "MusicVol";
                case AudioCategory.SFX: return "SfxVol";
                default: throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
