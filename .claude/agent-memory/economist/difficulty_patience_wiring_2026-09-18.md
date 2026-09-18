---
name: difficulty-patience-wiring-2026-09-18
description: DifficultyManager musteri sabri/stamina P-olceklemesi CANLIYA alindi (2026-09-18); gercek deger PREFAB'ta yasiyordu, cs script default'u degil; 15/20/2 karari
metadata:
  type: project
---

**2026-09-18.** Gameplay `CustomerManager.cs:735-736` (spawn aninda `customerAI.minWaitTime/
maxWaitTime = DifficultyManager.Instance.ScaledMinPatience/ScaledMaxPatience`) ve
`PlayerSpawner.cs:312` (`staminaRegenRate = ScaledStaminaRegenRate`) ile [[dead_wiring_p_scaling]]
notunda "OLU KABLO" diye isaretlenen 3 yoldan ikisini (sabir + stamina regen) CANLIYA aldi. Onceki
canli davranis: sabit 15/20s (P-bagimsiz, `Customer.prefab`/ithappy).

**KRITIK bulgu — deger cs script'te degil PREFAB'ta yasiyor.** `DifficultyManager.cs`daki
`[SerializeField] baseMinPatience = 35f` gibi alan initializer'lari yalniz "hic serialize
edilmemis yeni instance" icin gecerli. `Assets/DifficultyManager.prefab` (guid
`7149ff1fd9bacc54b83e01e765c94d06`) kendi serialize edilmis degerlerini tasiyor ve gercek gameplay
sahnesi (`Assets/Scenes/The Main Office.unity`, PrefabInstance @525258220) bu alanlar icin HICBIR
override tasimiyor -> **prefab'in degeri kazaniyor, cs default'u degil.** Task brief'i "cs
default 35/55/5 canliya cikacak" varsayimiyla geldi ama gercek canli deger prefabda 8/14/2 idi
(2026-07-30'dan kalma eski FAZ1 sabir denemesi). Floor'larla (5s/10s, `cs:356,363`) birlikte bu
3P/4P'de min/max ikisini de floor'a cakiyordu (3P=4P=5-10s), 4P'de 2-8s'e dusuyordu —
`customerInteractionTime~2s` + yuruyus suresiyle fiilen imkansiza yakin, "tek istasyon
doygunlugu" (GDD §7.1 P3≈P4 notu) yuzunden servis hizi P'yle proportional artmadigindan riski
katliyordu.

**Karar: `baseMinPatience=15, baseMaxPatience=20, patienceReductionPerPlayer=2`** (hem
`DifficultyManager.cs` field initializer'lari hem `Assets/DifficultyManager.prefab` serialized
degerleri ayni degere cekildi, ikisi arasi sapma kapatildi). Sonuc egri: 1P 15-20s (canli
15/20s'le TAM AYNI, sifir regresyon), 2P 13-18s, 3P 11-16s, 4P 9-14s — floor'lari (5/10)
hic tetiklemiyor, GDD §19.3 felsefesiyle (P arttikca zorluk artsin ama "ham guc" degil
koordinasyon zorlansin) uyumlu kucuk-monoton bir egim.

**Cift-olceklenme kontrolu: YOK.** `GameEconomySettings.cs` grep'inde sabir/stamina alani yok
(sadece rent/reward/hangar/truckCargo/timeSkip P-dizileri var); `PerkEffect`'teki "Sabirli
Musteriler" perki (`cs:63` min/maxWaitTime CARPANI) DifficultyManager'in ayarladigi degerin
UZERINE carpiyor, P-bazli degil oyuncu secimi — cakisma degil, ek katman.

**sim.js degisikligi gerekmiyor.** `customerMinWaitTime/maxWaitTime=15/20` sim.js'de
`reference` blogunda ("SIM BU ALANLARI OKUMAZ" yorumu) — sabir/walkaway hic simule edilmiyor,
sim ciktisini etkilemez.

**MainMenu.unity'deki `baseMaxPatience: 16` override'i** ayni prefab'a ait ama lobby sahnesinde;
`DifficultyManager` DontDestroyOnLoad DEGIL (Awake/InitializeSingleton'da yok), yani sahne
degisince yeni instance kuruluyor — lobby override'i gameplay'e tasinmiyor, dokunulmadi.

**Why:** Sessiz kod-canli sapmasi ikinci kez [[dead_wiring_p_scaling]] alaninda cikti (ilkinde
"3 yol hic baglanmiyor", bu kez "baglandi ama deger yanlis yerden okunuyor"). Prefab/sahne
override'inin cs default'u EZDIGI genel kural [[unity-scene-override-vs-code-default]] burada
tam isliyor.

**How to apply:** DifficultyManager alanlarindan biri tekrar degisecekse ONCE
`Assets/DifficultyManager.prefab`daki serialized degeri grep'le, cs script default'una guvenme.
Sabir/stamina disindaki 3. olu kablo (`ScaledCustomerCount`) hala baglanmadi — ayri is.

Iliskili: [[dead_wiring_p_scaling]], [[unity-scene-override-vs-code-default]],
[[serial_customer_service_ceiling]]
