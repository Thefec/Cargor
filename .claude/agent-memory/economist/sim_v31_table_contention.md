---
name: sim-v31-table-contention
description: sim.js v3.1 — masa çekişmesi + 5 interactable düzeltmesi; yeni parametreler serviceStations/cargoValues/packingTables ve S=6sn duyarlılığı
metadata:
  type: project
---

`tools/economy-sim/sim.js` **v3.1** (FAZ 4, 2026-07-30). v3.0'ın iki modelleme hatası düzeltildi.

**Why:** v3.0 çok-oyunculu geliri %7-13 fazla tahmin ediyordu; FAZ 2 ve FAZ 3'ün tüm kalibrasyonu
bu şişik tabana yapılmıştı.

**How to apply:** yeni bir ekonomi turunda ÖNCE bu iki girdinin hâlâ doğru olduğunu doğrula
(sahne topolojisi değişmiş olabilir), sonra hesapla.

## Düzeltmeler
- **D1 `activeInteractablesAtLevel0 = 5`** (v3.0'da ASSUMED=3). Sahne: `ShelfState` guid
  `d02b1bd2…` = **13** örnek, 10'u "Geniş Ambar" `levelObjects` → seviye 0'da yalnız `[0]` aktif;
  + 3 bağımsız raf + `DisplayTable` (guid `c22e4241…`) = 1. `CountActiveInteractables`
  `FindObjectsOfType` kullanıyor → **inaktifleri saymaz**. ⇒ talep 1P 9→**13**, 4P 16→**24**.
- **D2 masa çekişmesi** `tableContentionEfficiency()` (M/M/c//P sonlu-kaynak kuyruğu).
  Sahnede `Table` guid `8656889b…` = **tam 2**, ikisi de "Paketleme İstasyonu" `levelObjects`'i →
  seviye 0'da **1 masa**; `Table` TEK item taşıyor (`Table.cs:57`), paketleme yalnız masada.

## Yeni parametreler
`runSim(P, { ..., packingTables, serviceStations, cargoValues })` ·
`SRC.serviceStations` (CANLI **1** — `CustomerAI.cs:582` seri) · `ASSUMED.tableBusySeconds` (**S=6**, VARSAYIM).
Canlı default'lar değişmedi → `node tools/economy-sim/sim.js` hâlâ *mevcut* ekonomiyi basar.

## S duyarlılığı (η, 1 masa) — `kutu/dk`'dan sonra 2. en duyarlı sayı
| S | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| 4 sn | 1.000 | 0.983 | 0.962 | 0.938 |
| **6 sn** | 1.000 | **0.962** | **0.916** | **0.862** |
| 8 sn | 1.000 | 0.934 | 0.856 | 0.771 |

S=8 senaryosu kira ölçeği seçimini belirledi: `{500,1050,1500,1850}` S=8'de 2P/3P/4P STRICT'i
gün 16'da iflas ettiriyordu → daha yumuşak `{500,1000,1450,1800}` seçildi.

İlgili: [[faz4-final-value-set-2026-07-30]] [[economy_rebuild_faz1_2026-07-30]]
