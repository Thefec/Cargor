# 🌐 Çeviri (TR/EN) Bitirme

## Durum (2026-08-22)
Altyapı zaten kurulu, kapsam eksik:
- `Assets/LocalSettings/Tables/StringTable.asset` (`StringTable_tr`/`StringTable_en` + `StringTable Shared Data.asset`) — **161 key**, şu ana kadar sadece ayarlar menüsü (`Play`, `Language`, `Settings`, `Audio`, `Back`, `Credits`...).
- `Assets/NewCss/Localization/LocalizationHelper.cs` — kod tarafı yardımcı sınıf, hazır ve kullanımda.
- 3 ana sahnede (`The Main Office.unity`, `MainMenu.unity`, `Tutorial.unity`) toplam ~30 `LocalizeStringEvent` bağlantısı var ama bu sahnelerde ~320 `TextMeshProUGUI` var → **~290 metin hâlâ hardcoded**, sisteme bağlı değil.
- Bazı yerlerde metin içeriği henüz yazılmamış/taslak — kullanıcı hangi sahne/alanla başlanacağını kendisi söyleyecek.

## Yöntem (kullanıcıyla netleşti — Editor'ı canlı süren MCP yok)
- **Wiring (LocalizeStringEvent component + key oluşturma) → kullanıcı yapar**, Unity Editor'de elle. Ben hedef sahnedeki hardcoded metinleri tarayıp önerilen key adlarıyla (mevcut 161 key ile çakışmayan, kısa PascalCase İngilizce) liste hâlinde sunarım.
- **Translation (EN metin doldurma) → ben yaparım.** Var olan bir entry'nin `m_Localized` değerini değiştirmek düz string alanı düzenlemesi, GUID/component riski yok.

## Akış (her sahne turu)
1. Kullanıcı sahne/alan söyler.
2. Tara: o sahnedeki bağlı olmayan `TextMeshProUGUI` metinlerini bul.
3. Filtre: taslak/kesinleşmemiş metinleri çıkar.
4. Öneri listesi sun (GameObject yolu + TR metin + önerilen key).
5. Kullanıcı Editor'de wiring yapar.
6. Ben `StringTable_en.asset`'teki yeni key'lerin EN çevirisini yazarım.
7. Headless EditMode derleme kontrolü (0 CS).

## Ölçek notu
Kozmetik metin işi, kritik sistemlere dokunmuyor, ekonomik değer yok → CLAUDE.md eşiğine göre KÜÇÜK/orta, qa/kontrol kapısı zorunlu değil. Büyük birikim olursa müdür kendi diff kontrolünü yapar.

## Sıradaki adım
Kullanıcı ilk sahneyi/alanı söylediğinde 2. adımdan (tarama) başla.

## 15 yeni dil eklendi (2026-08-22)
Oyunun ana fontu SpaceGrotesk TR (Static atlas, tam kapsam **U+0000–017F**: ASCII + Latin-1 Supplement + Latin Extended-A) olacağı için, bu aralığa tamamen sığan 15 dil mevcut 209 key'in tamamıyla eklendi: **de/fr/es/pt/it/nl/pl/cs/sk/hr/sl/hu/lv/lt/et**. Detay: `plans/devam.md` 2026-08-22 girişi. Kapsam dışı (font işi gerektirir, henüz eklenmedi): Kiril (Rusça vb.), Yunanca, Vietnamca, Arapça/İbranice (RTL), Çince/Japonca/Korece.

**Mekanik not:** Yeni dil eklemek 5 dosya türü gerektiriyor (Locale asset, StringTable_XX asset, Addressables group asset, 2× schema asset) + 3 mevcut dosyaya (Localization-Locales.asset, StringTable.asset koleksiyonu, AddressableAssetSettings.asset) satır ekleme + `UnifiedSettingsManager.cs`'teki dil dropdown listesi (sıra `Locale.SortOrder`'a göre — tr=0/en=1/yeni diller=2..16). Üretim scripti (Python, tekrar kullanılabilir): `C:\Users\cicek\AppData\Local\Temp\claude\...\scratchpad\gen_assets.py` + `gen_locales.py` + `translations.py` (oturum sonunda scratchpad temizlenebilir, kalıcı değil — yeni dil eklenirken yeniden yazılması gerekir).
