---
name: gdd-section13-16-verified-current-2026-09-13
description: 2026-09-13 dogrulamasi -- GDD.md bolum 13 (Upgrade) ve 16 (Quest) hala koda birebir sadik, sifir sapma bulundu
metadata:
  type: project
---

# GDD §13/§16 doğrulama turu — 2026-09-13 (sapma YOK)

`git log --oneline -- Assets/NewCss/UpgradeScripts Assets/NewCss/UpgradeUiFolder
Assets/NewCss/Roguelite Assets/Scripts/Quest Assets/NewCss/Quest` en yeni commit hâlâ
`bb98ad1` (2026-08-30) — bu tarihten beri upgrade/quest sistemine dokunan hiçbir commit yok.
GDD.md ise `bb98ad1` → `93acda3` (GDD resync) → `328e7e7` (successCallSound + kira fallback
GDD notu temizliği, 2026-08-31) commit'leriyle senkronize. Yani **iki hafta boşlukta hiçbir
kod-GDD sapması birikmedi**.

Tek tek doğrulanan canlı değerler (hepsi GDD ile birebir eşleşti):
- `DraftPool.cs`: `OFFER_COUNT=3`, `T2_UNLOCK_DAY=5`, `T3_UNLOCK_DAY=9`, exclusion-group mantığı.
- `RerollCurve.cs`: `{50,90,160,290,525}` sabit tablo.
- `DifficultyManager.cs:73`: `upgradeCostMultiplierByPlayerCount = {1.00,2.00,2.95,3.70}`.
- `UpgradePanel.GetCostMultiplier` (cs:1622-1645): Görev Kademesi hâlâ P-çarpanından muaf.
- Sahne `The Main Office.unity:27114` — 25 upgrade entry (19 aktif draft'ta + 6
  `disabledInDraft=1`: Geniş Kuyruk, Sağlam Kasa, Dinç Ekip, Su Sebili, Güler Yüz, Uzun Kuyruk).
  NOT: eski hafıza kayıtları "26 upgrade" diyordu, gerçek sayı **25** — o rakam düzeltildi.
- `PerkEffect.cs`: 15 aktif effectId'nin hepsinin sayısal değerleri (cheap_rent 1.20-0.03L,
  phone_line 0.80/1f, leveraged_rent 0.75/gracePaymentPercent=0, gambler_case 1.30/1.55,
  all_in 1.25/grace=0, high_volatility ±0.35 ort 1.15, vb.) GDD'deki notlarla eşleşiyor.
- `Assets/Resources/Quests/*.asset` (30 dosya): tier dağılımı Easy 11/Medium 10/Hard 9,
  questType dağılımı 13/3/12/2 (PlaceBoxOnShelf/CompleteTruck/PackToy/AnswerPhone), ödül/ceza
  tablosu (28/15/0.6/0.32 · 60/20/1.2/0.4 · 150/30/3.0/0.6) — hepsi grep ile doğrulandı.
- `QuestManager.cs`: `SelectDailyQuestsStratified` (583), `CalculateCandidateCount` (662),
  `CalculateFeasibilityScore` (675), `CalculateEffectiveTargetCount` (727),
  `CompleteSpecificColorTruck` muafiyeti (735), `SetQuestTierInternal` (903) — satır numaraları
  GDD'nin verdiğiyle birebir.
- `Assets/Editor/EconomyInvariantCheck.cs`: quest-slot sayısı denetimi hâlâ orada (~193-195
  civarı, GDD'nin dediği gibi).

**Sonuç**: Bu turda GDD.md'ye HİÇBİR düzeltme uygulanmadı — mevcut hâli güncel kabul edildi.
Tek küçük düzeltme notu: "26 upgrade" ifadesi geçen eski hafıza kayıtları varsa gerçek sayı
**25**'tir (bkz. yukarıdaki liste).

İlgili: [[economy_full_balance_round12_gdd_resync_2026-08-30]],
[[economy_full_balance_round10_2026-08-30]], [[economy_full_balance_round11_2026-08-30]].
