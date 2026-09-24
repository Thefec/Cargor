---
name: sifirdan-upgrade-ucuz-mu-2026-09-23
description: 2026-09-23 sıfırdan bağımsız ekonomi hesabı — "upgrade'ler ucuz mu" kararı, kira-fonu kilidi önerisi, all_in/leveraged_rent grace-bedava bug'ı, fiyat zammının neden reddedildiği
metadata:
  type: project
---

3P playtest'i (gün 1 kasa 500, 3 kart ≈200) tetikledi. Rapor: `docs/economy/ekonomi-sifirdan-2026-09-23.md`, koşular `tools/economy_sim/results_2026-09-23/`, betik `tools/economy_sim/sifirdan_2026_09_23.py`.

**Karar:** Fiyatlar nominal olarak ucuz. 3P'de gelire göre göreli fiyat 1P'nin 0.51 katı, iyi takım 25 seviyenin 24'ünü gün 12-13'te bitiriyor. Buna rağmen fiyat zammı REDDEDİLDİ. Test edilen her zam (eski P-dizisi, orta dizi, gün-eğimi, 5 karta hedefli zam) ayrım gözetmeden alan orta takımın kaybını artırdı (4P %17→%33-64). Seçici oyuncuda da "kart almak iyi" hedefini bozdu.

**Why:** "Ucuz" hissinin kaynağı, gün 1-3 kasasının aslında gün-4 kirasına ayrılmış başlangıç parası olması. Asıl sorunlar iki: (1) kasa harcanabilir parayı kiraya ayrılmış paradan ayırmıyor; (2) ~10/19 kart ölü veya zararlı (Hızlı Hangar, Mesai Saati, Prestij Ustası zararlı).

**Öneri Ö-A:** `upgradeRentReserveFraction=1.0`. Alım ve reroll kasayı sıradaki kiranın altına indiremez, Acil Fren muaf. Etki: zayıf takım kart alırsa kayıp %92-100 → %1-63, orta takımda kart kaynaklı iflas %0. Bedeli: orta takımın gün-16 kasası seçicide %9-23, açgözlüde %44-64 düşüyor. Yumuşak kilit (0.5/0.75) tuzağı ÇÖZMÜYOR.

**Yeni bulgular:**
- `all_in` / `leveraged_rent` `gracePaymentPercent=0` yazıyor → grace SİLİNMİYOR, BEDAVA oluyor (DayCycleManager cs:619). Sim bayrağı `graceZeroPctLive`.
- Acil Fren zayıf takım için en güçlü kart (−52/−62 pp).
- Sim bayat DEĞİL: canlı config ile `matrix_post_uygulama.csv` bit düzeyinde aynı.

**How to apply:** Sonraki fiyat tartışmasında önce kira-fonu kilidinin uygulanıp uygulanmadığını kontrol et (grep `RentReserve`). Kilit yokken fiyat zammı önerme. Kart dengesini fiyattan değil efektten düzelt. İlgili: [[zayif-tier-upgrade-trap-2026-09-19]], [[oneriler-o4-o10-status-2026-09-19]].
