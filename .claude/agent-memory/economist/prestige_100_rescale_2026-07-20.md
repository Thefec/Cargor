---
name: prestige-100-rescale-2026-07-20
description: 0-240 prestij iç skalasını 0-100 görünür tavana çeken formül (k=0.4, threshold=4); tam spec ve kanıt (sim trajectory eşleşmesi)
metadata:
  type: project
---

**✅ UYGULANDI (2026-07-20 doğrulandı, commit 1496949).** Kod+asset+quest'ler canlıda:
`EkonomiAyarlari.asset` prestigePerBonus=4, customerLostPrestigePenalty=-0.6, customerServedPrestigeBonus=0.2,
boxDropPrestigePenalty=-0.02, wrongDeliveryPrestigePenalty=-0.08, wrongProductPrestigePenalty=-0.04,
callPrestigeReward=0.2; `PrestigeManager.cs` startingPrestige=6, maxPrestige=100, prestigePerCustomer=4,
gösterim `:F0`; quest asset'leri (easy1-5) prestij ×0.4. Sim (`tools/economy-sim/sim.js`) bu değerlere
senkronlandı. Sim doğrulaması: 2P/3P/4P Normal tavan günü 16/14/13 (rescale öncesi pacing korundu),
1P hiç tavana ulaşmıyor (final 75.9). Aşağısı orijinal spec (tarihsel).

---
SPEC (2026-07-20'de yazıldı, sonra uygulandı).

**Formül**: TÜM prestij miktarları (starting/ödül/ceza/quest/perk) ×k=0.4 ile ölçeklenir.
TÜM prestij eşikleri (prestigePerCustomer, prestigePerBonus) 10→4 (temiz sabit, k ile
TUTARLI: 4/10=0.4=k). maxPrestige görünür tavan olarak sabit **100** (k*240=96 değil —
bilinçli, aşağıda gerekçe). Para-cinsi alanlar (bonusPerTier, rewardPerBox TL) DOKUNULMADI.

**Neden X=4 (X=5 değil)**: node ile 4 senaryonun (1-4P, Normal+Quest) günlük ham prestij
yörüngesi (0-240) sim.js `runSim` ile üretildi, sonra X=4 (k=0.4) ve X=5 (k=0.5) ile
haritalanan kapasite+tier her gün eski değerle karşılaştırıldı. **X=4 tüm 4 senaryoda
16 gün boyunca kapasite VE bonus-tier'i eski (240 skala) ile HARFİYEN eşleşti (sıfır
sapma)** — çünkü k=X/10=0.4 ≤ gerçek oran 100/240=0.41667, yani yeni clamp (100) hiçbir
zaman eski clamp'ten (240) önce devreye girmiyor (240*0.4=96<100, marj var). X=5 (k=0.5)
ise raw prestij ~200'ü geçince (yaklaşık gün 11-13) erken tavana çarpıp tier'i 20'de
dondurdu, oysa eski sistemde 24'e kadar çıkıyordu (4 tier / 20 TL kutu-başı kayıp,
2P/3P/4P senaryolarında gün 11-16 arası gözlendi). Kanıt scripti:
`C:\Users\cicek\AppData\Local\Temp\claude\...\scratchpad\prestige_rescale.js` (session-local,
gerekirse yeniden üretilebilir — mantık: sim.js `runSim` + capacity/tier simülasyonu).

**maxPrestige=100 ama pratik tavan ~96**: k=0.4 ile 240 → 96. 100'ü tam "temiz görünür
tavan" olarak koymak 4 puanlık güvenlik payı bırakıyor (asla dolmaz ama asla taşmaz da).
Alternatif olarak maxPrestige=96 de "kanıtlanmış tam eşleşme" verirdi ama çirkin; 100
görsel/UX için daha iyi ve fonksiyonel fark yok (clamp zaten pratikte hiç tetiklenmiyor).

**Tam değer tablosu** (eski→yeni, k=0.4):
- PrestigeManager.cs:16 startingPrestige 15→6
- PrestigeManager.cs:19 maxPrestige 240→100
- PrestigeManager.cs:26 prestigePerCustomer 10→4
- PrestigeManager.cs:177 gösterim `:F2` → öneri `:F0` (bkz [[prestige_display_recommendation]] altbaşlık aşağıda)
- Scene override "The Main Office.unity" ~satır 25676 (`m_Script guid 344aba9d...`, PrestigeManager component): startingPrestige 15→6, maxPrestige 240→100, prestigePerCustomer 10→4 (baseCustomerCapacity=1, maxCustomerCapacity=20 DEĞİŞMEZ — müşteri sayısı birimi, prestij değil). d72efc7'de yaşanan "sadece kod değeri güncellenip sahne override unutuldu" tuzağına dikkat.
- GameEconomySettings.cs:51 + EkonomiAyarlari.asset:24 prestigePerBonus 10→4
- GameEconomySettings.cs:90 + asset:33 callPrestigeReward 0.5→0.2
- GameEconomySettings.cs:99 + asset:34 customerLostPrestigePenalty -1.5→-0.6
- GameEconomySettings.cs:102 + asset:35 customerServedPrestigeBonus 0.5→0.2
- GameEconomySettings.cs:105 + asset:36 wrongProductPrestigePenalty -0.1→-0.04
- GameEconomySettings.cs:108 + asset:37 boxDropPrestigePenalty -0.05→-0.02
- GameEconomySettings.cs:111 + asset:38 wrongDeliveryPrestigePenalty -0.2→-0.08
- GameEconomySettings.cs:54 bonusPerTier 5 → DEĞİŞMEZ (TL, prestij değil)
- Truck.cs:104 prestigePerBonus default 10f→4f; :105 bonusPerTier default 5f DEĞİŞMEZ
- PerkEffect.cs:85 "Prestij Simsarı" bonusPerTier=5f+0.5f*level → DEĞİŞMEZ (TL formülü)
- PerkEffect.cs:92 "Prestij Ustası" customerServedPrestigeBonus=0.5f+0.15f*level → 0.2f+0.06f*level (0.5→0.2 taban, 0.15→0.06 eğim, ikisi de ×0.4)
- CustomerAI.cs:878 fallback wrongProductPrestigePenalty -0.1f→-0.04f
- PhoneCallManager.cs:51 fallback callPrestigeReward 0.5f→0.2f
- GameStateManager.cs:622 fallback (economySettings null ise) -2f→-0.8f (NOT: bu fallback zaten asset değerinden farklıydı [-2 vs -1.5], tutarsızlık önceden vardı, ×0.4 ile korunuyor -0.8 vs -0.6)
- DayCycleManager.cs:81 EMERGENCY_BRAKE_PRESTIGE_PENALTY -5f→-2f
- BoxFallPenalty.cs:16 fallback boxDropPrestigePenalty -0.05f→-0.02f
- Quest assets (5x, rewardType:1 satırları, amount alanı):
  - easy1.asset: ödül +1→+0.4, +2→+0.8; ceza -2→-0.8, -1→-0.4
  - easy2.asset: ödül +1→+0.4, +2→+0.8; ceza -1→-0.4, -2→-0.8
  - easy3.asset: ödül +1→+0.4, +2→+0.8; ceza -1→-0.4, -2→-0.8
  - easy4.asset: ödül +1→+0.4, +1.5→+0.6; ceza -1→-0.4, -0.5→-0.2
  - easy5.asset: ödül +1→+0.4, +2→+0.8; ceza -1→-0.4, -1.5→-0.6

**Gösterim önerisi**: `:F2`→`:F0` (tam sayı, "43" gibi — 0-100 aralığında ondalık
gerekmiyor, F2 240-skalada da anlamsız hassasiyet gösteriyordu). Kapasite ilerleme barı
(GetProgressToNextCustomer/GetPrestigeForNextCustomer) hardcoded sabit KULLANMIYOR,
sadece prestigePerCustomer field'ından okuyor — değer güncellenince otomatik doğru
çalışır, ek kod değişikliği gerekmez.

**Yuvarlama riski**: prestigePerCustomer/prestigePerBonus=4 (int-benzeri temiz) ve
tüm flow'lar ×0.4 (ondalık ama sonlu/temiz: .2, .4, .6, .8, .04, .02, .08, .06) —
kayan nokta birikim hatası riski düşük (üslü/periyodik ondalık yok). Tek dikkat noktası:
GameStateManager.cs:622 gibi kod-içi sabit fallback'ler asset değerinden BAĞIMSIZ
tanımlı — asset güncellenip fallback unutulursa sessiz tutarsızlık oluşur (önceden de
vardı: -2 vs -1.5). Uygulama turunda ikisi birlikte değiştirilmeli.

**Doğrulama kanıtı**: `tools/economy-sim/sim.js` prestij trajectory'sini üretiyor ama
maxPrestige alanı sim içinde STALE (150, canlısı 240 — ayrı bilinen sorun, ilgisiz).
Bu analiz için sim.ECONOMY.maxPrestige=240 olarak monkeypatch edilip 1-4P
Normal+Quest senaryoları koşuldu; X=4 haritalaması her gün her senaryoda birebir
eşleşti (yukarıda gerekçe). Ayrıntılı 16 günlük tablo bu oturumun asistan çıktısında.

İlgili: [[prestige_cap_retune_2026-07-18]] (240'a çıkarma kararının orijinal
gerekçesi — bu rescale o kararın PACING'ini korumak üzere tasarlandı, değiştirmiyor).
