# Upgrade İsim Listesi — 25 Upgrade (İngilizce ⇄ Türkçe)

> Tüm upgrade'ler için **perk tarzında, tutarlı** yeni isimler. Yeni perkler zaten Türkçeydi;
> eski 9 omurgaya da aynı üslupta yeni İngilizce + Türkçe isim türetildi.
> Kaynak: `The Main Office.unity` → `UpgradePanel.upgrades` + `PerkEffect.cs`.

---

## A) Yeni roguelite perkleri — 16 adet

| # | English | Türkçe | effectId | Etki (kısa) |
|---|---------|--------|----------|-------------|
| 1 | Cheap Rent | Ucuz Kira | `cheap_rent` | Kira artış çarpanı ↓ |
| 2 | Prestige Broker | Prestij Simsarı | `prestige_broker` | Tier başına bonus ↑ |
| 3 | Prestige Master | Prestij Ustası | `prestige_master` | Müşteri prestij bonusu ↑ |
| 4 | Fast Hangar | Hızlı Hangar | `fast_hangar` | Tır bekleme süresi ×1.30 |
| 5 | Energetic Crew | Enerjik Ekip | `energetic_crew` | Stamina regen ↑ |
| 6 | Agile Crew | Çevik Ekip | `agile_crew` | Hareket hızı +%15 |
| 7 | Patient Customers | Sabırlı Müşteriler | `patient_customers` | Sabır ×1.25 |
| 8 | Long Queue | Uzun Kuyruk | `long_queue` | maxQueueSize +2 |
| 9 | Gambler's Case | Kumarbaz Kasası | `gambler_case` | Ödül +%30 / ceza +%55 |
| 10 | Phone Line | Telefon Hattı | `phone_line` | Telefon çalma olasılığı +bonus |
| 11 | Overtime | Mesai Saati | `overtime` | Gün süresi +20 sn |
| 12 | Leveraged Rent | Kaldıraçlı Kira | `leveraged_rent` | Ölçekli kira ↓, prestij cezası ×2 |
| 13 | High Volatility | Yüksek Volatilite | `high_volatility` | Teslimat başı ±%35 RNG |
| 14 | Emergency Brake | Acil Fren | `emergency_brake` | İflası 1 kez önler |
| 15 | All In | Kelle Koltukta | `all_in` | Gelir +%25, grace iptal |
| 16 | Bulk Buy | Toplu Alım | `bulk_buy` | Sonraki draft'ta 1 karta -%50 |

---

## B) Eski omurgalar — 9 adet (yeni İngilizce + Türkçe isim önerisi)

| # | Şu anki (kod) | **Önerilen English** | **Önerilen Türkçe** | Durum / Not |
|---|---------------|----------------------|---------------------|-------------|
| 17 | Storage | Roomy Warehouse | Geniş Ambar | Fiziksel raf + prestij hızı — **tut** ✅ |
| 18 | Table | Packing Station | Paketleme İstasyonu | Fiziksel paketleme istasyonu — **tut** ✅ |
| 19 | Truck | Extra Hangar | Ek Hangar | +1 hangar (ROI model-bağımlı) — **tut** ✅ |
| 20 | Queue | Wide Queue | Geniş Kuyruk | `Long Queue` perki ile **duplike** ⚠️ |
| 21 | Stamina | Hardy Crew | Dinç Ekip | `Energetic Crew` perki ile **duplike** ⚠️ |
| 22 | Customer | Friendly Service | Güler Yüz | **Ölü** (kod bağlantısı yok) ⚠️ |
| 23 | Money | Solid Till | Sağlam Kasa | **Zararlı** (reward'ı 50 altına çeker) ⚠️ |
| 24 | Water | Water Cooler | Su Sebili | Kozmetik, ekonomik değer 0 ⚠️ |
| 25 | Quest Tier | Quest Tier | Görev Kademesi | Quest sistemi pasif, EV≈0 ⚠️ |

---

## C) Hepsi bir arada — hızlı referans (25)

| # | English | Türkçe |
|---|---------|--------|
| 1 | Cheap Rent | Ucuz Kira |
| 2 | Prestige Broker | Prestij Simsarı |
| 3 | Prestige Master | Prestij Ustası |
| 4 | Fast Hangar | Hızlı Hangar |
| 5 | Energetic Crew | Enerjik Ekip |
| 6 | Agile Crew | Çevik Ekip |
| 7 | Patient Customers | Sabırlı Müşteriler |
| 8 | Long Queue | Uzun Kuyruk |
| 9 | Gambler's Case | Kumarbaz Kasası |
| 10 | Phone Line | Telefon Hattı |
| 11 | Overtime | Mesai Saati |
| 12 | Leveraged Rent | Kaldıraçlı Kira |
| 13 | High Volatility | Yüksek Volatilite |
| 14 | Emergency Brake | Acil Fren |
| 15 | All In | Kelle Koltukta |
| 16 | Bulk Buy | Toplu Alım |
| 17 | Roomy Warehouse | Geniş Ambar |
| 18 | Packing Station | Paketleme İstasyonu |
| 19 | Extra Hangar | Ek Hangar |
| 20 | Wide Queue | Geniş Kuyruk |
| 21 | Hardy Crew | Dinç Ekip |
| 22 | Friendly Service | Güler Yüz |
| 23 | Solid Till | Sağlam Kasa |
| 24 | Water Cooler | Su Sebili |
| 25 | Quest Tier | Görev Kademesi |

---

> ⚠️ Not: 20–25 arası (Geniş Kuyruk, Dinç Ekip, Güler Yüz, Sağlam Kasa, Su Sebili, Görev Kademesi)
> yeni perklerle duplike / ölü / zararlı. İsim vermek bunları düzeltmez — ayrı bir "havuz temizliği"
> turu gerekir. Detay: `.claude/agent-memory/economist/upgrade_legacy_backbones.md`.
