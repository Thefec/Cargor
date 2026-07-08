# 🙋 Senin Yapacakların (Manuel Görevler)

> Bu dosya = **benim (müdür/Claude) headless yapamadığım, sadece senin gerçek Unity'de yapabileceğin** işler.
> Batchmode ile derleme + saf-mantık testlerini ben doğrulayabiliyorum; **görsel UI, Play-mode davranışı, multiplayer senkron** senin gözünle teyit gerektiriyor.
> Bir maddeyi bitirince `[ ]` → `[x]` yap ve bana sonucu söyle.
> İlgili: [../PLAN.md](../PLAN.md) · [roguelite-draft.md](roguelite-draft.md)

---

## 🔴 ŞİMDİ — Task 4: Draft teklifi Play doğrulaması

**Durum:** Beklemede. Task 5'e geçmeden bunu doğrulamak istedin.
**Neden sen:** server-authoritative NetworkList davranışı batchmode'da görünmez.

### Adımlar
- [ ] **1.** Unity'yi aç (proje `feature/roguelite-upgrade-draft` branch'inde, güncel). Console'da derleme hatası olmamalı.
- [ ] **2.** Ana sahneyi **Play**'e al (tek editör host yeterli).
- [ ] **3.** Console'u aç, filtreye `Draft teklifi` yaz. Şu satırı ara:
  ```
  [UpgradePanel] Draft teklifi üretildi (gün 1, tier<= T1): [3, 7, 1] = Raf, Masa, Kuyruk
  ```
- [ ] **4.** Doğrula:

  | Beklenen | Anlamı |
  |---|---|
  | Log çıkıyor, köşeli parantezte **en fazla 3 index** | Teklif üretimi + `SelectOffer` çalışıyor |
  | `=` sonrası gerçek upgrade isimleri | Index → `upgrades` eşlemesi doğru |
  | Bir gün geçir → teklif değişiyor | `HandleNewDay` entegrasyonu çalışıyor |

### ⚠️ Bu aşamada NORMAL olan (bug sanma)
- **Tier kilidi henüz görünmez.** Upgrade'lerin `kind`/`tier` alanları hâlâ Inspector default'unda (`kind = LeveledBackbone`). Omurga tier'ı atladığı için şu an **tüm** upgrade'ler uygun → teklif "rastgele 3 uygun". Gerçek T1/T2/T3 kilidi **Task 8**'de veri girilince aktif olur. "Gün 3'te T3 çıktı" görürsen sorun yok, veri henüz yok demektir.
- **Log sadece host'ta çıkar** (server-only). Client'ta `_dailyOffer` replike olur ama UI olmadan gözle görünmez → tam client görünürlüğü Task 5'te gelir.

### Sonuç
- [ ] ✅ Sağlam → bana söyle, **Task 5**'e geçelim.
- [ ] ❌ Terslik var → Console çıktısını bana yapıştır, düzeltelim.

---

## 🟡 SONRA — birikmiş Play-teyidi borcu

Bu task'lar bittikçe senin gözünle bakman gerekecek (ben yazınca haber veririm):

- [ ] **Task 5 — 3-kart UI:** Panel gün sonu masada açılınca "tüm liste" yerine **3 kart** göstermeli. Kartlar doğru upgrade'leri/fiyatları gösteriyor mu?
- [ ] **Task 6 — Reroll butonu:** Reroll'a basınca yeni 3 kart + fiyat artıyor mu (50→90→160→290→525)? Günlük sıfırlanıyor mu?
- [ ] **Task 7 — 16 perk etkisi:** Her perk satın alınınca gerçekten iddia ettiği etkiyi yapıyor mu?
- [ ] **Task 8 — Veri girişi sonrası:** Tier kilidi artık gerçek — gün<5 sadece T1, gün≥5 T2, gün≥9 T3 çıkıyor mu? Fiyatlar v3.2 raporuyla eşleşiyor mu?

## 🟢 EN SON — tam multiplayer testi

- [ ] **1 / 2 / 4 kişi test:** Branch bitip kontrol ONAY verince, gerçek co-op oturumunda draft senkronu (herkes aynı teklifi görüyor mu, satın alma server-authoritative mi, reroll senkron mu) + 16 günlük döngü.

---

## ⚙️ Açık karar — ProjectSettings tuhaflığı

- [ ] **Karar bekliyor:** Her batchmode Unity çalıştırmasında `ProjectSettings.asset` içindeki `Standalone` scripting define'larından **`SENTIS_ANALYTICS_ENABLED`** siliniyor (Unity/paket kurulumun bunu deterministik kaldırıyor). Ben her seferinde geri alıp commit'e karıştırmadım. **Sen karar ver:** bu define kalmalı mı (Sentis analytics kullanıyorsan) yoksa gitmeli mi? Kullandığın bir şeyse Unity'de neden silindiğine bakmamız lazım.

---

## 📌 Not — benim yapabildiklerim (senden istemem gerekmeyen)
Referans olsun diye: derleme kontrolü, EditMode/saf-mantık testleri, kod yazımı, git commit, plan/doküman güncelleme → bunları ben headless hallediyorum, sana sormama gerek yok.
