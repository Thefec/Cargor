using System.Collections.Generic;
using NUnit.Framework;
using NewCss.Audio;

/// <summary>
/// SfxLibraryLookup (bkz. Assets/NewCss/Audio/Core/SfxLibraryLookup.cs) — SfxId -&gt; klip
/// çözümlemesinin motor-bağımsız çekirdeği. TClip=string kullanılarak (gerçek kullanımda
/// TClip=AudioClip, bkz. SfxLibrary.cs) UnityEngine hiç başlatılmadan doğrulanır — generic
/// olduğu için NewCss.Audio.Core.asmdef (noEngineReferences=true) içinde derlenebiliyor.
/// </summary>
public class SfxLibraryLookupTests
{
    [Test]
    public void TryResolve_KnownId_ReturnsClipAndTrue()
    {
        var entries = new Dictionary<SfxId, string> { { SfxId.DayEnd, "clip_day_end" } };

        bool found = SfxLibraryLookup.TryResolve(entries, SfxId.DayEnd, out string clip);

        Assert.IsTrue(found);
        Assert.AreEqual("clip_day_end", clip);
    }

    [Test]
    public void TryResolve_MissingId_ReturnsFalseAndNull()
    {
        var entries = new Dictionary<SfxId, string> { { SfxId.DayEnd, "clip_day_end" } };

        bool found = SfxLibraryLookup.TryResolve(entries, SfxId.Victory, out string clip);

        Assert.IsFalse(found);
        Assert.IsNull(clip);
    }

    [Test]
    public void TryResolve_EntryPresentButNullClip_ReturnsFalse()
    {
        // Sessiz ölüm senaryosu (bkz. GDD §27): SfxLibrary'de SfxId için bir Entry VAR ama
        // Inspector'da clip atanmamış (ör. MoneyEarned/CorrectItem, plan §3.2). Bu "yok" ile
        // AYNI muameleyi görmeli — sessizce "başarılı" sayılıp null klip çalınmaya çalışılmamalı.
        var entries = new Dictionary<SfxId, string> { { SfxId.MoneyEarned, null } };

        bool found = SfxLibraryLookup.TryResolve(entries, SfxId.MoneyEarned, out string clip);

        Assert.IsFalse(found);
        Assert.IsNull(clip);
    }

    [Test]
    public void TryResolve_NullDictionary_ReturnsFalse_NoThrow()
    {
        bool found = SfxLibraryLookup.TryResolve<string>(null, SfxId.DayEnd, out string clip);

        Assert.IsFalse(found);
        Assert.IsNull(clip);
    }

    [Test]
    public void TryResolve_EmptyDictionary_ReturnsFalse()
    {
        var entries = new Dictionary<SfxId, string>();

        bool found = SfxLibraryLookup.TryResolve(entries, SfxId.CounterTick, out string clip);

        Assert.IsFalse(found);
    }

    [Test]
    public void TryResolve_AllDefinedSfxIds_CanBeStoredAndResolved()
    {
        // Regresyon bekçisi: SfxId enum'una yeni bir üye eklenirse bu test onu otomatik kapsar —
        // Dictionary<SfxId,T> her zaman geçerli bir anahtar tipi olarak çalışmalı.
        var entries = new Dictionary<SfxId, string>();
        foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
        {
            entries[id] = id.ToString();
        }

        foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
        {
            bool found = SfxLibraryLookup.TryResolve(entries, id, out string clip);
            Assert.IsTrue(found, $"{id} çözümlenemedi");
            Assert.AreEqual(id.ToString(), clip);
        }
    }
}
