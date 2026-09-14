using System.Collections.Generic;

namespace NewCss.Audio
{
    /// <summary>
    /// SfxId -&gt; klip çözümlemesinin motor-bağımsız çekirdeği. Generic tutulduğu için UnityEngine'e
    /// bağımlı değil (NewCss.Audio.Core.asmdef, noEngineReferences=true): üretimde SfxLibrary bunu
    /// TClip=AudioClip ile çağırır, EditMode testleri TClip=string ile Unity'yi hiç başlatmadan
    /// aynı algoritmayı (eksik/null slot davranışı dahil) doğrular.
    /// </summary>
    public static class SfxLibraryLookup
    {
        /// <summary>
        /// entries'te id yoksa VEYA değeri null ise (boş Inspector slotu) false döner — "eksik klip"
        /// sessizce yutulmaz, çağıran (SfxBus) bunu loglamakla yükümlü.
        /// </summary>
        public static bool TryResolve<TClip>(IReadOnlyDictionary<SfxId, TClip> entries, SfxId id, out TClip clip)
            where TClip : class
        {
            if (entries != null && entries.TryGetValue(id, out var found) && found != null)
            {
                clip = found;
                return true;
            }

            clip = null;
            return false;
        }
    }
}
