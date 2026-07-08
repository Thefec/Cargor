# 📋 CARGOR — Canlı Plan (Dashboard)

> **Sahibi**: Müdür (planlama + delegasyon). Bu dosya = hızlı okunan gösterge paneli.
> Her oturum başında **bunu** oku. Detay gerektikçe `plans/` altındaki ilgili dosyayı aç.
> Bir iş bitince buradan çıkar, `plans/archive/` altına taşı.

---

## 🎯 Şu an aktif iş

**Roguelite Upgrade Draft Sistemi** — uygulama devam ediyor. **(2026-07-09 mola verildi)**
Branch: `feature/roguelite-upgrade-draft`. **Task 0-5 commit'li**, ekonomi kilitli (v3.2, 9945 TL).
→ Tam detay & task listesi: **[plans/roguelite-draft.md](plans/roguelite-draft.md)**

### ⏭️ Sıradaki adım (moladan dönünce buradan devam)
1. ✅ **Task 0-5 bitti** (Unity teyidi + RerollCurve + server-auth teklif + 3-kart panel). Task 4 & 5 Play-doğrulandı (local).
2. **Task 6 (aktif):** panelde reroll butonu (`RerollCurve` 50/90/160/290/525, günlük sıfırla) → 7 (16-perk effect registry) → 8 (Inspector veri) → 9 (qa+ölü kod) → kontrol whole-branch ONAY → Unity 1/2/4 kişi test.
3. 🧹 **Temizlik borcu:** Task 4'ün geçici `Draft teklifi üretildi` debug log'u (`UpgradePanel.cs`, commit `9b2b8c7`) hâlâ duruyor — Task 6/7'de kaldır.

> ⚠️ Task 6+ NetworkList/sahne/prefab içerir → batchmode EditMode sadece derlemeyi doğrular; senkron/UI için Play/gerçek Unity gerekir.
> ℹ️ **Yan görev bitti:** ses ayarı slider bug'ı çözüldü (`f7122c9`) — bkz. [plans/archive/2026-07-changelog.md](plans/archive/2026-07-changelog.md).

---

## 📌 Açık Kararlar (kullanıcı onayı gereken)
- [x] **Q1** Öncelik sırası onaylandı *(2026-07-06)*
- [x] **Q2** Kod organizasyon ikiliğine şimdilik dokunulmuyor *(2026-07-06)*
- [ ] **Q3** Otomatik test (Unity Test Framework) yatırımı — roguelite EditMode testleriyle fiilen başladı, kapsam kararı açık.

---

## 🗂️ Plan dosyaları
| Dosya | İçerik | Durum |
|---|---|---|
| **[plans/manuel-gorevler.md](plans/manuel-gorevler.md)** | 🙋 **Senin yapacakların** — Unity Play/UI/multiplayer teyitleri | 🔴 Task 4 testi bekliyor |
| **[plans/roguelite-draft.md](plans/roguelite-draft.md)** | Aktif: draft sistemi tasarım + uygulama + task listesi | 🚧 canlı |
| [plans/economy-balance.md](plans/economy-balance.md) | Ekonomi denge (Faz 1, Faz 2, bug'lar) | ✅ çoğu bitti (Unity teyidi) |
| [plans/roadmap.md](plans/roadmap.md) | Orijinal yol haritası + Sprint 0-3 + departman tablosu | 📖 referans |
| [plans/archive/2026-07-changelog.md](plans/archive/2026-07-changelog.md) | Değişiklik günlüğü, bitmiş kararlar | 🗄️ arşiv |

**Referans raporlar (kök):** `GDD.md` (tasarım), `UPGRADE_PRICING_REPORT.md` v3.2 (fiyat kaynağı), `ECONOMY_BALANCE_REPORT.md`.

---

## 🏢 Departman kısa hatırlatma
Ekonomik değer (fiyat/süre/ödül/çarpan) → **economist** (gameplay uydurmasın). Kod değişikliği sonrası → **qa**. Her departman çıktısı → **kontrol** (Fable 5) ONAY kapısı, en fazla 3 tur. Tam tablo: [plans/roadmap.md §4](plans/roadmap.md).
