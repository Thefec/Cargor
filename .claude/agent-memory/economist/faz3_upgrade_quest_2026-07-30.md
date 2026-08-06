---
name: faz3-upgrade-quest-2026-07-30
description: FAZ 3 upgrade/perk fiyat + quest EV turu — sadece uretim hizi ve TL/kutu deger uretir; 3 aktif zararli kart; upgradeCostMultiplier 1.15 yanlis; quest sistemi kosunun %3'u
metadata:
  type: project
---

# FAZ 3 (2026-07-30): upgrade/perk fiyatlandirma + quest EV yeniden capalama

Tam rapor: `plans/economy-rebuild-2026-07-30-faz3.md`. Taban: [[economy_rebuild_faz1_2026-07-30]].
Bu tur SALT HESAP — hicbir `Assets/` dosyasi ve `tools/economy-sim/sim.js` degistirilmedi.

**Why:** Kullanici tum ekonominin sifirdan hesaplanmasini istedi; FAZ 3 upgrade+quest ayagi.
**How to apply:** Upgrade/perk/quest fiyati sorulunca ONCE bu dosyayi, sonra faz3 raporunu oku.
Eski `upgrade_*.md` / `quest_*.md` hafizalari FAZ 1 oncesi gelir tabanina kalibre → BAYAT.

## Temel kanun (her upgrade tartismasinda basla)

**Yalniz 2 sey deger uretir: (1) insan uretim hizi, (2) TL/kutu.**
Kapasite artiran her sey SIFIR degerde cunku baglayici kisitlar uretim hizi + seri musteri servisi:
- 3. hangar = 0 TL her bantta; 2. hangar OPT'ta 0, yalniz STRICT'te deger
- `hangarStayDuration` perki (fast_hangar) OPT'ta 0 — tir penceresi %10-42 dolu, tavan degil tampon
- `maxQueueSize` artirmak NEGATIF (kuyruk = koruma kalkani; dolu kuyruk spawn'i CEZASIZ atliyor)
- raf sayisi (Genis Ambar) = 0 veya negatif; talep zaten seri tavanin (11.4/gun) 1.05-2.1 kati
- musteri sabri = 0 (baglayici kisit sabir degil, seri servis dongusu)

## 3 AKTIF ZARARLI KART (P0)

1. `overtime` KOD BUG'I: `PerkEffect.cs:196-200` → `realDurationInSeconds = 160+20 = 180`, sahne **200**
   → gunu %10 KISALTIYOR. 300 TL karsiligi −612…−1937 TL. Duzeltme: `= taban × 1.125` (idempotent, `*=` YASAK).
   Duzeltildikten sonra 300 TL fiyat DOGRU.
2. `long_queue`: 2→5 kuyruk = −449 (1P) … −5392 (4P); STRICT 1P/2P'de IFLASA sebep. `unity:21531` disabledInDraft=1.
3. `leveraged_rent`: kira ×0.8 kazanci (+499…+1498) prestij bedeliyle (−754…−3685) siliniyor → her P'de negatif.
   Bedeli doymus musteri dongusunden cikar: kira ×0.75 + `gracePaymentPercent=0` (all_in ile ayni dislama grubu).

## upgradeCostMultiplierPerPlayer = 1.15 YANLIS

Icerik kapsama orani (tum icerik maliyeti / harcanabilir butce): 1P %209, 2P %132, 3P %112, 4P %98.
Solo yarisini alabiliyor, 4P tamamini. Cozum: **ECONOMY_SCALE_BY_PLAYERS = {1.00, 1.80, 2.45, 3.15}**
(gelir olcegi 1.00/1.81/2.44/3.15 ile birebir) → kapsama her P'de %103-105.
Tek skaler zorunluysa **1.47** (ort sapma %7.8). `DifficultyManager.cs:348-356`, `prefab:85`.
Reroll (`RerollCurve.cs`) P carpani HIC uygulanmiyor → 4P'de 3.15× ucuz.
**Ayni olcek vektoru kira + upgrade + reroll + quest odulu icin kullanilmali.**

## Paketleme Istasyonu = oyunun TEK gercek uretim upgrade'i, yarisi YOK

Sahnede `Table` bileseni tasiyan **tam 2 GameObject** var, ikisi de bu upgrade'in levelObjects'i.
`maxLevel: 3` ama 3./4. masa yok → seviye 2-3 icin 650 TL karsiligi HICBIR SEY.
`Table` tek item tasiyor → tum takim uretimi seri gecer. 2. masanin kazanci (machine-repairman, S=6s masa
mesgul suresi): 1P %0, 2P %4.0, 3P %8.8, 4P %14.7. **S = en kritik olculecek playtest sayisi** (4s↔8s arasi
degeri 4× degistiriyor). Fiyat 100 → 150, maxLevel 3 → 1.

## Quest: sistem kosunun %1.5-3.2'si, Gorev Kademesi HER P'de NEGATIF

`Gorev Kademesi` marjinal degeri (gun 6'da L1 / gun 10'da L2): 1P −39/−15, 2P +60/−4, 3P +94/+56, 4P +94/+100.
Fiyat 80/100 → ROI negatif veya <0.6. **Fiyat sorunu DEGIL, icerik sorunu.** Iki kok neden:
1. **Havuz seyrelmesi**: `tier <= maxTier` (`QuestManager.cs:471-485`) → T2'de 30 kartin 11'i Easy, 3 kartlik
   cekilisin "en iyisi" cogu zaman Easy oluyor.
2. **Erisilemez hedefler**: 1P gun-8 uretimi 5.0 kutu/gun. 1P'de hedef→tamamlanma:
   uretim 3→%87, 4→%71, 5→%55, 6→%40 | renk-kilitli (÷3) 1→%87, 2→%40 | tir 1→%83, 2→%30 | telefon 1→%87, 2→%73, 3→%41.
   Canli Hard (renksiz 12, renk 5, tir 3) → %11/%8/%15 → EV −21…−28 TL. **19/30 asset 1P'de negatif EV.**

**3 yapisal duzeltme (BIRLIKTE uygulanmali, ayri ayri ise yaramaz):**
- D1: 3 teklif = her tier'dan 1 kart (T2'de 1 Easy + 1 Medium + 1 Hard)
- D2: `targetCount` P-olcekli (`max(1, round(tgt × ECONOMY_SCALE[P]))`); **AnswerPhone ISTISNA** (telefon arzi P-flat 2.55/gun)
- D3: tier basina TEK odul/ceza cifti, ceza orani ust tier'da DUSER:
  **Easy 28/15 (ceza %55, p .87, EV 22.4) · Medium 60/27 (%45, p .71, EV 34.8) · Hard 150/53 (%35, p .55, EV 58.7)**
  Prestij = para/40. Sonuc: 16-gun quest geliri 662 TL = 1P kosusunun %10.2'si; Gorev Kademesi ROI 1.5/1.7 (fiyat 80/100 DOGRU olur).
- D4 (ops.): odulu de ayni vektorle olcekle → kosu payi her P'de %10.2 (olceksiz 1P %10.2 → 4P %3.2, gerileyen; solo can simidi olarak KASITLI birakilabilir)

**Ceza/odul asimetrisi %55 DOGRU, dokunma.** Kabul esigi p > %35.5. %100'e cikarmak karari derinlestirmiyor,
yalniz RNG/event tilt'ini artiriyor.

## PlaceBoxOnShelf EXPLOIT (P0, mekanik)

`ShelfState.cs:604-609` her yerlestirmede event basiyor, dedup YOK. Yalniz DOLU kutu rafa konabiliyor
(`PlayerInventory.Shelf.cs:330-371`) ama rafa koy → geri al → tekrar koy sonsuz sayiyor.
**Tek dolu kutu ile hedef 12 bile ~30 sn'de biter.** Etki: `questType:1` olan **13/30 asset**.
Exploit bilinirse 1P gunluk quest EV'si 11.7 → 28.5 TL. Quest ayari bunun uzerine yapilamaz — once dedup.

## Dokunma diyebilecegim kalemler (dogru kalibre)

`agile_crew` 180 · `phone_line` 160 (ama zaman maliyeti modelde YOK) · `overtime` 300 (kod duzeltildikten sonra) ·
`Ek Hangar` baseCost 200 · `emergency_brake` 250 (ama `tier: 1 → 0` olmali: 1P STRICT gun 4'te iflas ediyor,
perk gun 5'te aciliyor) · `Gorev Kademesi` 80/20 (quest duzeltmeleri sonrasi) · 5 kapali omurga (`disabledInDraft: 1`) ·
`Saglam Kasa`'nin NO-OP govdesi (eski hali kutu odulunu 50 → 25'e DUSURUYORDU).

## ⚠️ FAZ2 ILE CAKISMA — 7 kosullu duzeltme (rapor §9)

FAZ2 paketi ([[faz2_prestige_rent_event_2026-07-30]]) gelir tabanini degistiriyor
(yeni: 1P 6154 / 2P 12251 / 3P 19173 / 4P 26352, olcek 1:1.99:3.12:4.28, upgrade butcesi 2838/5618/8891/12091).
**FAZ2 ONCE uygulanmali.** FAZ3'un su 7 kalemi kayiyor:
- C1 `ECONOMY_SCALE` {1,1.8,2.45,3.15} → **{1.00, 2.00, 3.10, 4.25}** (FAZ2'nin 1.62 skaleri 2P'de %19 ucuz)
- C2 icerik kapsamasi %105 → **%148**; **fiyatlari degistirme, %148'i kabul et** (secim baskisi daha iyi)
- C3 **`Genis Ambar` DEGER KAZANIYOR**: 2 istasyonla tavan 11.4→22.7, talep tavanin altina duser → L1 prestij/gun
  1P +0.80 / 2P +1.20 / 3P +0.93 / 4P −0.35. maxLevel 9→**3** (2 degil), baseCost 50→100, costStep 10→50
- C4 kira perkleri %26-82 daha degerli (kira toplami 1P 2497→3316, 4P 7490→**14261**) → `cheap_rent` **130'da KAL**
  ve `rentScaledMultiplier` etki degisikligine GEREK YOK; `leveraged_rent` 220→**300** (etki degisikligi hala gerekli)
- C5 `prestige_master` degeri YARIYA duser (taban 0.2→0.4, +0.06 goreli %30→%15) → etkiyi ×2 yap (+0.12/lvl) ve fiyat 175'te kalsin
- C6 P-bazli kargo ile 1P tamTir 1.43→3.33 → quest tir merdiveni **Easy 1 / Medium 2 / Hard 3** mumkun;
  **tir quest'lerine P-olcekleme UYGULANMAZ** (kargo boyutu zaten P ile buyuyor, cift sayma)
- C7 quest prestij alanlari ×2 → Easy +1.40/−0.80, Medium +3.00/−1.36, Hard +7.50/−2.66 (para odulleri 28/60/150 DEGISMEZ)

FAZ2'den BAGIMSIZ ve olduğu gibi gecerli: 3 zararli kart (§3), reroll P-olceklemesi, PlaceBoxOnShelf exploit,
maxLevel kismalari (Paketleme 3→1, Hangar 2→1), quest havuz politikasi D1, tier odul tablosu D3 (para kismi).

## FAZ 1 / sim'e geri bildirim (sim.js'e DOKUNULMADI)

- `ASSUMED.startingActiveInteractables = 3` **YANLIS → 5** (4 aktif `ShelfState` + 1 `DisplayTable`;
  13 ShelfState'in 10'u Genis Ambar levelObjects, seviye 0'da yalniz [0] aktif). Talep 1P 9→12, 4P 16→24.
  → HER oyuncu sayisi seri servis tavaninin ustunde, 1P bile gunde 0.64 musteri kaybediyor.
- Masa cekismesi modelde YOK → FAZ 1 §3 gelir tablosu 2P-4P icin %4-26 fazla iyimser.
- `_storeLevel` hic degismiyor (sabit 1, `CustomerManager.cs:74`) — olu knob.
