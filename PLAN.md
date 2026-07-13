# 📋 CARGOR — Canlı Plan (Dashboard)

> **Sahibi**: Müdür (planlama + delegasyon). Bu dosya = hızlı okunan gösterge paneli.
> Her oturum başında **bunu** oku. Detay gerektikçe `plans/` altındaki ilgili dosyayı aç.
> Bir iş bitince buradan çıkar, `plans/archive/` altına taşı.

---

## 🎯 Şu an aktif iş

**RELEASE PUSH** — buglar → denge → Steam çıkışı. Branch: `feature/roguelite-upgrade-draft`.
→ Faz kapsamı & bug envanteri: **[plans/release-push.md](plans/release-push.md)** · oturum logu: **[plans/devam.md](plans/devam.md)**

- ✅ **Roguelite Upgrade Draft BİTTİ** (Task 0-9 commit'li, 16 perk sahnede authored, ekonomi v3.2/9945 TL, qa merge-öncesi tarama: **blocker yok**). Kod olarak `main`'e merge'e hazır — tek kalan: FAZ 0 late-join çok-oyunculu Play testi (kullanıcıda). Detay: [plans/roguelite-draft.md](plans/roguelite-draft.md).
- ✅ **FAZ 1 bug-avı** büyük ölçüde bitti: 21+ bulgu kapandı (G1-G28 exploit/korrektlik + N7/E1/E2/E4 netcode/çekirdek). Kalan açıklar release-push.md'de (N1-N6/N8-N10, G9-artış2, G15/G23/G24/G25/G28, C1).

### ⏭️ Sıradaki adım (buradan devam)
1. 🔧 **Commit'siz bug fix'leri kapat (bu oturum):** dolu-kutu-fırlatınca-parçalanma (`NetworkWorldItem` doluluk kontrolü + `RedFull.prefab` veri) + client NaN-frustum spam'i (`WorldSpaceCanvasCameraBinder`). Kullanıcı play-test'i ✅. Kalan: kontrol kapısı + seçici commit (hassas font/SteamManager/ProjectSettings dosyalarına dokunmadan).
2. 🙋 **Manuel test borcu:** [plans/manuel-gorevler.md](plans/manuel-gorevler.md) — A0 (bu oturum fix'leri) + A/B/C/D (güvenlik regresyonu, FAZ 0 merge blocker, roguelite draft senkron, ekonomi ölçümü).
3. 📊 **FAZ 2 açık kararlar:** C1 kota-ölümü (playtest-blocked, [plans/economy-audit-2026-07-13.md](plans/economy-audit-2026-07-13.md)); C5 wealthTax ✅ çözüldü (`9d2c3b0`).

> ⚠️ Netcode/sahne/prefab işleri → batchmode EditMode sadece derlemeyi doğrular; senkron/UI için Play/gerçek Unity gerekir.
> ℹ️ Çalışma ağacındaki hassas dosyalar (late-join fix, SteamManager, fontlar, ProjectSettings) Steam testine bağlı — commit'siz bırakıldı.

---

## 📌 Açık Kararlar (kullanıcı onayı gereken)
- [x] **Q1** Öncelik sırası onaylandı *(2026-07-06)*
- [x] **Q2** Kod organizasyon ikiliğine şimdilik dokunulmuyor *(2026-07-06)*
- [ ] **Q3** Otomatik test (Unity Test Framework) yatırımı — roguelite EditMode testleriyle fiilen başladı, kapsam kararı açık.

---

## 🗂️ Plan dosyaları
| Dosya | İçerik | Durum |
|---|---|---|
| **[plans/release-push.md](plans/release-push.md)** | RELEASE PUSH fazları (FAZ 0 test → FAZ 1 bug envanteri → ekonomi → Steam) + gerçek bug envanteri (G/N/E) | 🚧 **aktif — gerçek kaynak** |
| **[plans/devam.md](plans/devam.md)** | Oturum logu (en son ne yapıldı + sırada ne) — oturum başında ÖNCE bunu oku | 🚧 canlı |
| **[plans/manuel-gorevler.md](plans/manuel-gorevler.md)** | 🙋 **Senin yapacakların** — Unity Play/UI/multiplayer teyitleri | 🔴 A0+A/B/C/D testi bekliyor |
| [plans/roguelite-draft.md](plans/roguelite-draft.md) | Draft sistemi tasarım + uygulama (Task 0-9) | ✅ bitti (merge-pending) |
| [plans/economy-audit-2026-07-13.md](plans/economy-audit-2026-07-13.md) | FAZ 2 ekonomi denetimi (16-gün sim, C1/C5 kararları) | 📖 karar-referans |
| [plans/economy-balance.md](plans/economy-balance.md) | Ekonomi denge (Faz 1 değerleri) | ✅ bitti (playtest teyidi borcu) |
| [plans/roadmap.md](plans/roadmap.md) | Orijinal yol haritası + Sprint 0-3 + departman tablosu | 📖 referans |
| [plans/archive/2026-07-changelog.md](plans/archive/2026-07-changelog.md) | Değişiklik günlüğü, bitmiş kararlar | 🗄️ arşiv |

**Referans raporlar (kök):** `GDD.md` (tasarım), `UPGRADE_PRICING_REPORT.md` v3.2 (fiyat kaynağı), `ECONOMY_BALANCE_REPORT.md` (Faz-1 ekonomi analizi, tarihli).

---

## 🏢 Departman kısa hatırlatma
Ekonomik değer (fiyat/süre/ödül/çarpan) → **economist** (gameplay uydurmasın). Kod değişikliği sonrası → **qa**. Her departman çıktısı → **kontrol** ONAY kapısı, en fazla 3 tur. Tam tablo: [plans/roadmap.md §4](plans/roadmap.md).
