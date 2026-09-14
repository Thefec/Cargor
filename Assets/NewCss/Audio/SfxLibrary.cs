using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewCss.Audio
{
    /// <summary>
    /// SfxBus'ın Resources üzerinden bir kez yükleyip cache'lediği SfxId -&gt; AudioClip tablosu.
    /// AudioRoutingConfig ile aynı desen (bkz. Assets/NewCss/Audio/AudioRoutingConfig.cs):
    /// "Assets/Resources/Audio/SfxLibrary.asset" olarak duruyor.
    ///
    /// Kurulum/güncelleme: Assets/Editor/SfxLibrarySetup.cs (menü: Tools ▸ Cargor ▸ Audio ▸
    /// SFX Kutuphanesini Kur veya Guncelle). Elle düzenlemeye gerek yok, idempotent — ama
    /// Inspector'dan da elle klip atanabilir (ör. MoneyEarned/CorrectItem henüz boş, plan §3.2).
    /// </summary>
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxId id;
            public AudioClip clip;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>AudioSlotValidator gibi salt-okunur tarayıcılar için — Inspector alanına doğrudan erişim.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        private Dictionary<SfxId, AudioClip> _map;
        private bool _mapBuilt;

        private void BuildMapIfNeeded()
        {
            if (_mapBuilt) return;

            _map = new Dictionary<SfxId, AudioClip>();
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                // Aynı SfxId iki kez tanımlanmışsa son kazanır — SfxLibrarySetup zaten tekilleştiriyor,
                // burada elle düzenleme sonucu oluşabilecek bir duplikasyona karşı savunma.
                _map[entry.id] = entry.clip;
            }
            _mapBuilt = true;
        }

        /// <summary>clip null ise (boş Inspector slotu veya hiç tanımlanmamış SfxId) false döner.</summary>
        public bool TryGetClip(SfxId id, out AudioClip clip)
        {
            BuildMapIfNeeded();
            return SfxLibraryLookup.TryResolve(_map, id, out clip);
        }

        /// <summary>Inspector'dan elle klip atandığında (OnValidate) cache'in bayatlamaması için.</summary>
        private void OnValidate()
        {
            _mapBuilt = false;
        }
    }
}
