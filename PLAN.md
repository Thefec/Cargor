# 📋 CARGOR — Canlı Plan (Dashboard)

> **Sahibi**: Müdür (planlama + delegasyon). Bu dosya = hızlı okunan gösterge paneli.
> Her oturum başında **bunu** oku. Detay gerektikçe `plans/` altındaki ilgili dosyayı aç.
> Bir iş bitince buradan çıkar, `plans/archive/` altına taşı.

---

## 🎯 Şu an aktif iş

**Roguelite Upgrade Draft Sistemi** — uygulama devam ediyor.
Branch: `feature/roguelite-upgrade-draft`. **Task 0-6 commit'li**, ekonomi kilitli (v3.2, 9945 TL).
→ Tam detay & task listesi: **[plans/roguelite-draft.md](plans/roguelite-draft.md)**

### ⏭️ Sıradaki adım (buradan devam)
1. ✅ **Task 0-6 bitti.** Task 6 reroll butonu commit `b458a9d` (qa: 1 önemli+2 küçük → listener/panel-guard düzeltildi; kontrol: ONAY). Debug log temizlik borcu kapandı.
2. **Task 7 (aktif):** 16-perk effect registry (`PerkEffect.cs`, Assembly-CSharp; `ApplyUpgradeEffect` → effectId varsa registry'ye delege). Değerler UPGRADE_PRICING_REPORT.md v3.2 §3-4. Bazı risk perkleri (Volatilite per-delivery RNG, Acil Fren iflas bayrağı) gerçek kod dokunuşu → qa ile netleş. Sonra 8 (Inspector veri) → 9 (qa+ölü kod) → kontrol whole-branch ONAY → Unity 1/2/4 kişi test.
3. 🙋 **Manuel borç:** Task 4/5/6 Play/multiplayer teyidi (geç-join client'ta reroll fiyatı, host+client aynı 3 kart senkron) — [plans/manuel-gorevler.md](plans/manuel-gorevler.md).

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
Ekonomik değer (fiyat/süre/ödül/çarpan) → **economist** (gameplay uydurmasın). Kod değişikliği sonrası → **qa**. Her departman çıktısı → **kontrol** (Opus 4.8) ONAY kapısı, en fazla 3 tur. Tam tablo: [plans/roadmap.md §4](plans/roadmap.md).
