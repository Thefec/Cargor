# 📋 CARGOR — Canlı Plan (Dashboard)

> **Sahibi**: Müdür (planlama + delegasyon). Bu dosya = hızlı okunan gösterge paneli.
> Her oturum başında **bunu** oku. Detay gerektikçe `plans/` altındaki ilgili dosyayı aç.
> Bir iş bitince buradan çıkar, `plans/archive/` altına taşı.

---

## 🎯 Şu an aktif iş

**QUEST İÇERİK + KART UI TURU.** Branch: `feature/economy-balance-round` — `origin/main`'den **5 commit önde, PUSH YOK** (`origin/main` = `57b9be3`).
→ Oturum logu (**önce bunu oku**): **[plans/devam.md](plans/devam.md)**

- ✅ **Roguelite draft + RELEASE PUSH FAZ 0/1 kapandı** — main'e merge+push'lu (`5833070`, +73 commit). Arşiv niteliğinde: [plans/roguelite-draft.md](plans/roguelite-draft.md), [plans/release-push.md](plans/release-push.md).
- ✅ **Ekonomi turları merge'li:** prestij 0–240 → **0–100 rescale** (`1496949`) · hangar bekleme süresi oyuncu-bazlı 1P=90…4P=30 (`6ef9ad6`) · upgrade draft temizliği + Money zararlı-bug + perk fiyat (`57b9be3`).
- ✅ **Push bekleyen commit'ler:** `c729dbb` hangar kapısı kök-neden fix'i · `3442e62` ikon slotları + mavi tır · `4ce5fa7` quest kart UI · `4645e4e` QuestData elle-yazım modeli + 35 asset silindi · `d9ef6c5` sahne WIP.
- 🚧 **Quest içeriği sıfırlandı — havuz BOŞ.** Kullanıcı 30+5 asset'i sildirdi, görevleri elle yazacak. `QuestData` artık görev başına açık `moneyReward`/`prestigeReward`/`moneyPenalty`/`prestigePenalty` + opsiyonel buff taşıyor (havuzdan rastgele seçim yok). Havuz boşken günlük görev atanmaz ve Görev Kademesi draft'a girmez — beklenen.

### ⏭️ Sıradaki adım (buradan devam)
1. 🙋 **Kullanıcıda:** 3 quest slotunu Unity'de tam bağla (`descriptionText` + `actionButton` atlanan iki alandı; `ValidateWiring()` eksikleri Console'a yazar) → görevleri elle yaz (Create → Cargor → Quest Data → `Assets/Resources/Quests/`) → play-test.
2. 💰 **Yeni görev değerleri economist turu** (opsiyonel): sabit ödül/ceza sayılarını tier bandına oturtmak. Eski EV referansı Easy ~20 / Med ~36 / Hard ~60 TL; tarihsel tablolar [plans/quest-listesi.md](plans/quest-listesi.md) (GEÇERSİZ notlu) + [plans/quest-redesign-2026-07-25.md](plans/quest-redesign-2026-07-25.md).
3. 📊 **En büyük açık yapısal bulgu: tır penceresi cap** — tır 8:00–17:00 (9s) vs talep 7:00–18:00 (11s) → talebin **%18'i sıfır kapasite**. Para yalnız tırdan geliyor → doğrudan gelir tavanı. Kod-doğrulandı, değer seçimi economist+playtest'e bağlı.
4. 🔧 **Kalan ölü tetikleyiciler:** `CompleteMinigame` + `MakePackagingMistake` (çağrısız). `AnswerPhone` ve `CompleteSpecificColorTruck` 2026-07-25'te bağlandı.
5. 🧹 **Temizlik onayı bekliyor:** kök `herhangi` (0 bayt) + `Assets/_Recovery/0 (10..13).unity`.

> ⚠️ Batchmode EditMode **yalnız derlemeyi** doğrular; netcode senkron / UI / Play davranışı için gerçek Unity gerekir.
> ℹ️ Çalışma ağacında kalıcı gürültü: ~52 font/materyal churn + `UpgradeEntry.prefab` UI redesign kolu — commit'lerde dışarıda bırakılıyor ([[unity-batchmode-artifacts]]).

---

## 📌 Açık Kararlar (kullanıcı onayı gereken)
- [x] **Q1** Öncelik sırası onaylandı *(2026-07-06)*
- [x] **Q2** Kod organizasyon ikiliğine şimdilik dokunulmuyor *(2026-07-06)*
- [ ] **Q3** Otomatik test (Unity Test Framework) yatırımı — roguelite EditMode testleriyle fiilen başladı, kapsam kararı açık.

---

## 🗂️ Plan dosyaları
| Dosya | İçerik | Durum |
|---|---|---|
| **[plans/devam.md](plans/devam.md)** | Oturum logu (en son ne yapıldı + sırada ne) — oturum başında ÖNCE bunu oku | 🚧 **canlı — gerçek kaynak** |
| **[plans/quest-redesign-2026-07-25.md](plans/quest-redesign-2026-07-25.md)** | Quest derin analizi + tier/EV tabloları + Seçenek B | 📖 referans (asset'ler silindi, EV bantları geçerli) |
| [plans/quest-listesi.md](plans/quest-listesi.md) | Silinen 35 görevin dökümü | 🗄️ tarihsel (başında GEÇERSİZ notu) |
| [plans/economy-audit-2026-07-20.md](plans/economy-audit-2026-07-20.md) | Holistik ekonomi denetimi (7 sistem) — **tır penceresi cap** burada | 📖 açık bulgu kaynağı |
| [plans/economy-balance-round.md](plans/economy-balance-round.md) | Birleşik ekonomi turu (quest/upgrade/event ortak denge) | ✅ uygulandı |
| [plans/upgrade-round-2026-07-20.md](plans/upgrade-round-2026-07-20.md) | Upgrade fiyat/ROI turu (disabledInDraft, Money bug, perk fiyat) | ✅ uygulandı |
| [plans/upgrade-isim-listesi.md](plans/upgrade-isim-listesi.md) | 25 upgrade İngilizce⇄Türkçe isim tablosu + duplike/ölü tespiti | 📖 referans (sahnede uygulandı) |
| [plans/playtest-2026-07-19.md](plans/playtest-2026-07-19.md) | 🙋 **Senin yapacakların** — 27 maddelik play-test checklist'i | 🔴 bekliyor |
| [plans/manuel-gorevler.md](plans/manuel-gorevler.md) | 🙋 Unity Play/UI/multiplayer teyitleri (eski tur) | 🟡 kısmen bayat |
| [plans/release-push.md](plans/release-push.md) | RELEASE PUSH fazları + bug envanteri (G/N/E) | 🗄️ FAZ 0/1 kapandı |
| [plans/roguelite-draft.md](plans/roguelite-draft.md) | Draft sistemi tasarım + uygulama (Task 0-9) | ✅ bitti + merge'li |
| [plans/economy-audit-2026-07-13.md](plans/economy-audit-2026-07-13.md) · [economy-audit-2026-07-17.md](plans/economy-audit-2026-07-17.md) · [economy-balance.md](plans/economy-balance.md) | Eski ekonomi denetimleri | 📖 tarihsel referans |
| [plans/roadmap.md](plans/roadmap.md) | Orijinal yol haritası + Sprint 0-3 + departman tablosu | 📖 referans |
| [plans/archive/2026-07-changelog.md](plans/archive/2026-07-changelog.md) | Değişiklik günlüğü, bitmiş kararlar | 🗄️ arşiv |

**Referans raporlar (kök):** `GDD.md` (tasarım), `UPGRADE_PRICING_REPORT.md` v3.2 (fiyat kaynağı), `ECONOMY_BALANCE_REPORT.md` (Faz-1 ekonomi analizi, tarihli).

---

## 🏢 Departman kısa hatırlatma
Ekonomik değer (fiyat/süre/ödül/çarpan) → **economist** (gameplay uydurmasın). Kod değişikliği sonrası → **qa**. Her departman çıktısı → **kontrol** ONAY kapısı, en fazla 3 tur. Tam tablo: [plans/roadmap.md §4](plans/roadmap.md).
