# Unity doğrulama kanıtı — 2026-09-19 (Ö1+Ö2+Ö3 uygulandıktan sonra)

Ekonomi denge değişikliklerinin Unity tarafından gerçekten okunduğunu ve test süitini
kırmadığını belgeler. Kontrol turu bu iddiaları repoda doğrulayamadığı için çıktı
buraya kalıcı olarak alındı.

**Unity:** 1.18.3+d7ffd15 · **Tarih:** 2026-09-19

## 1. Ekonomi invariant denetimi — GEÇTİ

```
Unity.exe -batchmode -nographics -projectPath . \n  -executeMethod EconomyInvariantCheck.RunFromCommandLine -logFile <log>
EXIT CODE: 0
```

```
=== EKONOMİ DEĞER DENETİMİ (FAZ4 §B) ===
223 kontrol çalıştı.
```

### Bu koşunun yakaladığı gerçek hata

Aynı komut **prefab düzeltmesinden önce** 1 sapma vermişti:

```
❌ DEĞER SAPMASI — 1 kontrol:
   · upgradeCostMultiplierByPlayerCount: beklenen [1, 1,6, 2,1, 2,5], bulunan [1, 2, 2,95, 3,7]
```

Sebep: alan `Assets/DifficultyManager.prefab`'ta serialize edilmemişti, ama Unity prefab'ın
cache'inden ESKİ değeri okuyordu — yani `DifficultyManager.cs`'teki field initializer'ı
değiştirmek **tek başına yetmedi** (kod değişir, oyun eski değeri kullanır). Değer prefab'a
açıkça (Unity liste formatı, hex DEĞİL) yazılınca denetim temizlendi.

## 2. EditMode test süiti — GEÇTİ

```
Unity.exe -batchmode -nographics -projectPath . \n  -runTests -testPlatform EditMode -testResults <xml> -logFile <log>
```

| total | passed | failed | skipped | result |
|---|---|---|---|---|
| 132 | 132 | 0 | 0 | **Passed** |

Başarısız test: **YOK**

Sınıf bazında (en kalabalık 12):

- `RoomResolverTests`: 10 test
- `VoiceRingBufferTests`: 10 test
- `AudioVolumeMathTests`: 9 test
- `MusicCrossfadeMathTests`: 9 test
- `MusicPlaylistShufflerTests`: 9 test
- `VoiceHudRowTimerTests`: 9 test
- `VoiceMicSilencePolicyTests`: 9 test
- `DraftPoolTests`: 8 test
- `MusicPhaseSelectorTests`: 8 test
- `VoiceDriftTrackerTests`: 8 test
- `VoiceBufferPolicyTests`: 7 test
- `VoiceSequenceTrackerTests`: 7 test

> Sonuç **XML'den** okundu. `-runTests` ile `-quit` BİRLİKTE kullanılmadı —
> birlikte kullanılırsa Unity test koşmadan exit 0 verip XML üretmez (bilinen tuzak).
> İlk denemede `-testResults /tmp/...` (POSIX yolu) Unity tarafından çözülemediği için
> XML hiç oluşmamıştı; Windows yolu ile tekrarlandı.

## 3. Sahne metin düzeltmesi sonrası YENİDEN doğrulama

`fast_hangar` kartının açıklama metni düzeltildikten ve sahne satır sonları working-copy
standardına (CRLF) döndürüldükten sonra her iki koşu tekrarlandı:

| Koşu | Sonuç |
|---|---|
| `EconomyInvariantCheck.RunFromCommandLine` | **223 kontrol, 0 sapma, exit 0** |
| EditMode süiti | **132/132 Passed, 0 failed** |

Yani sahnenin elle düzenlenmesi (fiyatlar + metin + satır sonları) Unity tarafında hiçbir
bozulma yaratmadı.
