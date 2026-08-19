# 🧟 PERK CANLANDIRMA — 6 ölü perkin canlı instance'a bağlanması

> Dal: `feature/perk-revival` (main'den, 2026-08-19). Karar: kullanıcı **seçenek A** (tam fix) dedi.
> Bu dosya tek başına yeterli — teşhis kanıtlarıyla birlikte burada. Keşif tekrarlanmayacak.

---

## 1. Teşhis (KANITLI — tekrar araştırma yapma)

### 1.1 `UpgradePanel`'in referansları PREFAB, sahne objesi değil
`Assets/Scenes/The Main Office.unity:23017-23019`:

```
CustomerManager: {fileID: 1958402830}                        ← sahne objesi ✅ perkler ÇALIŞIR
PlayerMovement:  {fileID: ..., guid: d376f9a9..., type: 3}   ← Character.prefab ❌
Truck:           {fileID: ..., guid: 7269fc87..., type: 3}   ← Truck_Anim (2).prefab ❌
```

Prefab guid'leri `TruckSpawner.truckPrefab` (`:39706`) ve `PlayerSpawner.PlayerPrefab` (`:24165`) ile
**aynı** → perk yazısı prefab'a ulaşıyor, sorun sonrasında.

### 1.2 Tır perkleri: `Truck.OnNetworkSpawn` SO'dan yeniden okuyup eziyor
`Assets/NewCss/TruckScripts/Truck.cs:243-250` (`Awake` DEĞİL — eski notlar yanlış):

```csharp
rewardPerBox       = economySettings.rewardPerBox;
penaltyPerBox      = economySettings.penaltyPerBox;
hangarStayDuration = economySettings.GetHangarStayDuration(pc);
prestigePerBonus   = economySettings.prestigePerBonus;
bonusPerTier       = economySettings.bonusPerTier;
```

Her tır `Instantiate(truckPrefab)` ile doğuyor (`TruckSpawner.cs:474`) → spawn'da bu 5 alan SO
tabanına dönüyor. Ayrıca perk satın alındığında **sahada zaten olan** tırlara prefab yazısı hiç
ulaşmıyor.

### 1.3 Oyuncu perkleri: oyuncu bir kez spawn oluyor
`PlayerSpawner.cs:293-301` — *"Client X already has a player object assigned, skipping..."*.
Prefab'a yazılan değer canlı oyuncuya ulaşmıyor. Alanlar instance'tan canlı okunuyor:
`PlayerMovement.cs:548` (`staminaRegenRate`), `:429` (`moveSpeed`).

### 1.4 Ölü perk listesi (6)

| effectId | Yazdığı alan | Durum |
|---|---|---|
| `prestige_broker` | `Truck.bonusPerTier` | ölü |
| `fast_hangar` | `Truck.hangarStayDuration` | ölü |
| `gambler_case` | `Truck.rewardPerBox` + `penaltyPerBox` | ölü (ödül+ceza birlikte → nötr) |
| `agile_crew` | `PlayerMovement.moveSpeed` | ölü |
| `energetic_crew` | `PlayerMovement.staminaRegenRate` | ölü *(eski notlarda eksikti)* |
| `all_in` | ödül `Truck` (ölü) + `Economy.gracePaymentPercent=0` (**çalışıyor**) | 🔴 **TUZAK: sadece bedel** |

`all_in` şu an oyuncuya para karşılığı **yalnız zarar** satıyor (grace period gidiyor, +%25 gelir
gelmiyor) ve draft'ta aktif (`disabledInDraft: 0`).

Çalışan perkler (SO veya sahne objesi hedefli, DOKUNMA): `cheap_rent`, `prestige_master`,
`patient_customers`, `long_queue`, `leveraged_rent`, `high_volatility`, `phone_line`,
`emergency_brake`, `overtime`, `bulk_buy`.

---

## 2. Çözüm deseni — kodda ZATEN var: `EventEffectManager`

Aynı problemi (kalıcı asset yerine canlı instance) çözmüş, deseni birebir kopyalanacak:

| İhtiyaç | Mevcut çalışan örnek |
|---|---|
| Canlı tırların hepsi | `EventEffectManager.cs:509` → `FindObjectsOfType<Truck>()` |
| **Gelecekte doğan tır** | `Truck.cs:258` → `EventEffectManager.Instance?.ApplyEventEffectToNewObject(gameObject)` (OnNetworkSpawn içinden, SO okumasından SONRA) |
| Bu peer'in kendi oyuncusu | `EventEffectManager.cs:498` → `GetOwnedPlayer()` |

### 2.1 ⚠️ SIRA KRİTİK
`Truck.OnNetworkSpawn` içinde doğru sıra: **SO okuması → perk uygulaması → event çarpanı.**
Ters sırada `EventEffectManager` snapshot'ı perk-öncesi değeri yakalar ve event bitince perki siler.
Aynı şekilde oyun ortasında perk alınırsa aktif event'in snapshot'ıyla etkileşim düşünülmeli.

### 2.2 İdempotenlik zorunlu (mevcut sözleşme)
`PerkEffect.cs` başlığındaki kural aynen geçerli: `HandleUpgradeLevelsChanged`
(NetworkList.OnListChanged) **tüm client'larda** tetiklenir. Her Apply* level'dan **mutlak** hedef
değer hesaplar, `+=`/`*=` yapmaz. Yeni "canlı instance'a uygula" yolu da idempotent olmalı —
aynı tıra iki kez uygulanınca değer sürüklenmemeli.

### 2.3 Prefab yazısı tamamen kalkmalı
Perkler artık prefab'a yazmayınca `UpgradePanel`'deki `PerkAssetSnapshot`'ın **prefab yarısı**
(`Truck`×4 + `PlayerMovement`×2 alan) işlevsiz kalır. SO yarısı (7 alan) AYNEN KALIR — onlar hâlâ
kalıcı asset'e yazıyor. Prefab yarısını silmek yerine bırakmak da güvenli; karar gameplay'in.

---

## 3. İş sırası

1. 🟡 **economist** — 6 perk canlanınca dengeye ne olur? Büyüklükler (`gambler_case` +%30/ceza +%55,
   `all_in` +%25, `fast_hangar` ×1.30, `agile_crew` +%15, `energetic_crew` 1→2.5,
   `prestige_broker` +0.5/seviye) UPGRADE_PRICING_REPORT'ta bu perkler **çalışıyor varsayılarak**
   fiyatlanmıştı; yine de fiili taban FAZ4 sonrası değişti. Fiyat/güç ayarı gerekiyorsa söyle.
2. 🟡 **gameplay** — §2'deki mekanizma. Ekonomik değer UYDURMA; sayılar economist'ten.
3. **qa** — inceleme.
4. **kontrol** — ONAY kapısı (en fazla 3 tur).
5. **Headless doğrulama müdürde** (Unity 6000.5.6f1, 0 CS + EditMode) — subagent'lar batchmode
   beklemede güvenilmez.
6. Playtest: kullanıcı. Perkler canlanınca `kutu/dk/oyuncu` tabanı değişir.

---

## 4. Doğrulama (bitmeden ONAY yok)
- [ ] 0 CS hatası (headless, 6000.5.6f1)
- [ ] EditMode testleri yeşil
- [ ] `Cargor / Ekonomi Değerlerini Doğrula` (`EconomyInvariantCheck`, 179 kontrol) temiz
- [ ] **Bağlanma kanıtı**: her ölü perk için "canlı instance'a yazan çağrı" grep'le gösterilecek
      (yazıldı ≠ çağrılıyor)
- [ ] Prefab'a artık yazılmadığının kanıtı (`ctx.Truck` / `ctx.PlayerMovement` kullanımı kalmadı)
