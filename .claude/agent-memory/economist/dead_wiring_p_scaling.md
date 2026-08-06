---
name: dead-wiring-p-scaling
description: DifficultyManager'in P-bazli sandigimiz 3 yolu OLU (musteri sabri, telefon sansi, ScaledCustomerCount) + sahnede startingMoney=50000 debug degeri; ekonomi analizinde bunlari P-bazli VARSAYMA
metadata:
  type: project
---

**2026-07-30 kod denetimi (FAZ1 sifirdan tur).** `DifficultyManager` "oyuncu sayisina gore
olcekle" izlenimi veriyor ve LOG BASIYOR ama uc yolu fiilen hicbir seye baglanmiyor:

1. **Musteri sabri OLU.** `ApplyCustomerSettings` (`DifficultyManager.cs:436-443`)
   `FindObjectsOfType<CustomerAI>()` ile sahnedeki ornekleri yamiyor. Sahnede CustomerAI YOK
   (`grep minWaitTime "The Main Office.unity"` = 0 sonuc); musteriler `CustomerManager.cs:680`'de
   prefab'tan Instantiate ediliyor. => `baseMinPatience 8 / baseMaxPatience 14 /
   patienceReductionPerPlayer 2` (prefab:77,78,83) hic uygulanmiyor.
   **CANLI SABIR = 15-20 sn sabit (`Customer.prefab:2305-2306`), P'den BAGIMSIZ.**

2. **Telefon sansi OLU.** `PhoneCallManager.cs:425`: `public void SetCallChance(float newChance) { }`
   -- GOVDE BOS. `ApplyPhoneSettings` (`cs:474-481`) cagiriyor + log basiyor (sahte yesil).
   **CANLI SANS = `phoneRingChancePerHour` 0.30 sabit, P'den BAGIMSIZ.**
   (Turev: 10 saatlik zar/gun -> 3.0 calma/gun, 25s ringDuration -> 75s/gun ekranda caliyor.)

3. **`ScaledCustomerCount` OLU.** `CalculateScaledCustomerCount` (`cs:299-303`, 10 + 2/oyuncu)
   hicbir sisteme yazilmiyor, yalniz `GetDifficultyInfo` log/UI (`cs:558`).
   **Gercek talep** = `CustomerManager.CalculateTodaysCustomerCount` (interactables x2 +
   storeLevel x2 + rand(-2..3)) x `playerCountMultiplier`.

**GERCEKTEN P-bazli olan 4 sey:** `baseRentByPlayerCount` {500,900,1200,1500},
`hangarStayDurationByPlayerCount` {90,60,40,30}, `playerCountMultiplier` = 1+(P-1)*0.3
(1.0/1.3/1.6/1.9 -- `cs:429` YORUMU 4P=2.0 diyor, YANLIS), `upgradeCostMultiplierPerPlayer` 1.15
(1.00/1.15/1.32/1.52). Baslangic parasi P'den BAGIMSIZ (moneyMultiplierPerPlayer=1.0).

## KRITIK YAN BULGU: sahnede `startingMoney: 50000`
`The Main Office.unity:4734`. `MoneySystem.OnNetworkSpawn` (`MoneySystem.cs:45-47`) bunu
`_currentMoney`'e yaziyor; `DifficultyManager.ApplyMoneySettings` (`cs:448-471`) sonra
`SetMoney(500)` ile duzeltmeye calisiyor ama YALNIZ `HasGameEverStarted == false` ise.
Sira garantisi yok. Debug kalintisi -- urune bu degerle cikarsa tum ekonomi anlamsizlasir.

## Latent risk: `Truck.prefab` gizli dusuk odul
`Truck.prefab:197-198` `rewardPerBox: 10`, `penaltyPerBox: 2`. `Truck.cs:206-218` OnNetworkSpawn'da
`Resources.Load<GameEconomySettings>("EkonomiAyarlari")` basariliysa 50/40 ile eziyor. Yukleme
basarisiz olursa kutu basi odul 10 TL'ye duser = sessiz %80 gelir kaybi.

**Why:** FAZ1'de "hangi deger P-bazli?" kolonu doldurulurken bu uc yolun log bastigi halde hicbir
sey yapmadigi ortaya cikti. Onceki turlarda P-bazli sanilip modele boyle girmis olabilir.

**How to apply:** Ekonomi analizinde musteri sabrini, telefon sansini ve ScaledCustomerCount'u
ASLA P-bazli varsayma. Bu uc degeri "degistirelim" onerisi yaparken once KABLOLAMA duzeltilmeli
diye not dus, yoksa degisiklik hicbir etki yapmaz. Sahne `startingMoney` degeri her ekonomi
turunun basinda yeniden kontrol edilmeli.

Iliskili: [[economy_rebuild_faz1_2026-07-30]], [[serial_customer_service_ceiling]],
[[hangar_stay_duration_per_player]]
