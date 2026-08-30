# Economist Agent Memory Index

## ⭐ EN GÜNCEL TABAN (buradan başla — Round 10+11 KODA İŞLENDİ, commit `bb98ad1`)
- [⭐⭐Round 12: GDD uygulama-sonrası senkron 2026-08-30](economy_full_balance_round12_gdd_resync_2026-08-30.md) — GDD 14 bölümde ~30 düzeltme + §16.1 best-of-K EKLENDİ; canlı değer↔dosya:satır tablosu; 2 yeni açık
- [⭐⭐Round 11: quest best-of-K yeniden tasarımı 2026-08-30](economy_full_balance_round11_2026-08-30.md) — U6 oyunda NO-OP çıkıp geri alındı; K-aday+adaptif fizibilite → T2<T0 4/16→0/16; Hard cezası 53→30
- [⭐⭐Round 10 FİNAL: nihai uygulama listesi 2026-08-30](economy_full_balance_round10_2026-08-30.md) — sim v5.0 (5 model hatası); **12 UYGULA / 11 ÖLÇÜMLE-RED**; her reddin gerekçesi §4'te
- [⭐Round 9: GDD ↔ kod senkronu 2026-08-30](economy_full_balance_round9_2026-08-30.md) — 26 düzeltme; gün 200s (160 değil), sabır prefabda 15-20s, asset yeni alanları içermiyor → `.cs` canlı
- [⭐Round 8: quest sistemi 2026-08-30](economy_full_balance_round8_2026-08-30.md) — "Görev Kademesi ödenmiş kötüleştirme" ilk tespiti (çerçevesi R11'de düzeltildi); quest prestiji görünmez değer
- [⭐Round 7: telefon ekonomisi 2026-08-30](economy_full_balance_round7_2026-08-30.md) — "telefon TRAP'i" büyük ölçüde model artifaktı; `SkipTime` TABAN-200s dönüşümü KORUNMALI
- [⭐Round 6: prestij ekonomisi 2026-08-30](economy_full_balance_round6_2026-08-30.md) — prestij fail-state değil GİZLİ GELİR ÇARPANI; kazanma eşiği 16/16 hücrede ölü
- [⭐Round 5: event sistemi dengesi 2026-08-30](economy_full_balance_round5_2026-08-30.md) — FESTIVAL outlier, CUSTOMER SUPPORT NO-OP, kota çarpanı yukarı yönde ölü, pozitifler %43 daha sık
- [⭐Round 4: perk/upgrade fiyat-güç 2026-08-30](economy_full_balance_round4_2026-08-30.md) — Ek Hangar bant-bağımlı aşırılık; `cheap_rent` stale-baseline; 6 perk `disabledInDraft=1`
- [⭐Round 3: kira eğrisi SEVİYE fix'i 2026-08-30](economy_full_balance_round3_2026-08-30.md) — asimetrik `{290,650,1140,1630}` türetimi (şişme tahmini R10'da %7-18 değil %16-115 çıktı)
- [⭐Round 2: zayıf bant haritası 2026-08-30](economy_full_balance_round2_2026-08-30.md) — P1 marjı SAHTE (grace asıl tampon); Slow/strict duvarı EĞRİ değil SEVİYE; grace "fakir kal" exploiti
- [⭐Round 1: sim.js `runFullSim` kanonik model 2026-08-30](economy_full_balance_round1_2026-08-30.md) — eski `runSim` canlı kodu YANSITMIYOR (sapma −76%…+540%); ⚠️§3 hatalıydı, R2'de düzeltildi

## Sistem notları
- [Para YALNIZ tırdan gelir](money_comes_only_from_trucks.md) — ⚠️ÇÜRÜDÜ: telefon `AddMoney(20)` koşulsuz ikinci musluk; müşteri hâlâ para vermez ama ürün ARZI onda
- [Seri müşteri servisi tavanı](serial_customer_service_ceiling.md) — ~11.4 müşteri/gün P-BAĞIMSIZ tavan; ⚠️"2. istasyon çözer" iddiası R10'da çürüdü; kuyruk büyütme GERİ TEPER
- [Prestijin işlev yüzeyi](prestige_function_surface.md) — tek ekonomik işlev ödül tier'ı; müşteri kapasitesi zinciri ÖLÜ, kazanma koşulu prestije bakmıyor
- [P-ölçekleme ölü kablolar](dead_wiring_p_scaling.md) — müşteri sabri/telefon şansı/ScaledCustomerCount ÖLÜ + sahnede startingMoney=50000 DEBUG
- [Perk/kart mutlak-atama çakışma riski](perk_card_absolute_assignment_conflict.md) — TÜM perkler idempotent MUTLAK atama; aynı alana yazan kart perki SESSİZCE SİLER (5 çakışma noktası)
- [Python/Node ortamı](env_no_python.md) — python3 PATH'te ama sim.js için Node tercih ediliyor

## Tarihsel karar kayıtları (bayatlık uyarılarıyla)
- [⭐FAZ4 NİHAİ DEĞER SETİ](faz4_final_value_set_2026-07-30.md) — ⚠️kira satırı Round 10'da geçersiz; FAZ 1/2/3'ün üstüne yazar
- [FAZ3 upgrade+perk+quest](faz3_upgrade_quest_2026-07-30.md) — fiyatlar geçerli; costMult dizisi {1,2,2.95,3.7}
- [FAZ2 kararlar: kira+prestij+event+süreler](faz2_prestige_rent_event_2026-07-30.md) — ⚠️kısmen bayat; event tablosu geçerli
- [FAZ1 ekonomi yeniden kurulum](economy_rebuild_faz1_2026-07-30.md) — ⚠️§3 gelir tablosu bayat; §1 envanter geçerli
- [Kira büyüme 1.35 açığı + 1.20 önerisi 2026-08-20](rent_growth_1_35_deficit_2026-08-20.md) — g=1.20 kararının gerekçesi (hâlâ canlı)
- [Kira ölüm sarmalı kök nedeni](rent_death_spiral.md) — 🔁tarihsel; wealthTax inert'ti
- [FAZ2 wealthTax kırık kablolama](wealthtax_broken_wiring.md) — ✅çözüldü, terim formülden kaldırıldı
- [Başlangıç parası çelişkisi](money_config_conflict.md) — ✅çözüldü
- [sim.js v3.2 resync 2026-08-19](sim_resync_2026-08-19.md) · [sim.js v3.1 masa çekişmesi](sim_v31_table_contention.md) — model evrimi
- [Event interval retune 1/3→1/2 2026-08-25](event_interval_retune_2026-08-25.md) — ⚠️sim event sıklığını para akışına BAĞLAMIYOR; MC ile ölçüldü
- [PlateUp müşteri/telefon kota tasarımı v2 2026-08-29](plateup_customer_quota_2026-08-29.md) — kota=GERÇEK gelir tavanı; `rewardPerBoxByPlayerCount=[50,55,70,88]` türetimi
- [Telefon V4 cooldown perk/event onayı 2026-08-29](phone_cooldown_perk_event_stacking_2026-08-29.md) — ⚠️perk 10f/event cooldown yolu R10'da terk edildi; stacking risk-yok analizi geçerli
- [Telefon pasif/reaktif yeniden tasarım](phone_passive_redesign.md) — ⚠️V3 dönemi, V4'te çalma tamamen silindi
- [Eksik event'ler G9 kalibrasyonu](missing_events_g9.md) — ✅kapandı; 16 event tam eşleşiyor
- [Prestij tavanı bug + düzeltme](prestige_cap_bug_and_fix.md) · [Prestij tavanı yeniden ayar](prestige_cap_retune_2026-07-18.md) · [Prestij 0-100 rescale (k=0.4)](prestige_100_rescale_2026-07-20.md) — tavan tarihçesi (canlı: 100)
- [Prestij kırılganlığı eşikleri](prestige_fragility.md) — startingPrestige tarihçesi
- [Kutu düşme cezası merkezileştirme](box_drop_penalty_centralization.md) — boxDropMoneyPenalty=5, penaltyPerBox 60→40
- [Tır/hangar penceresi tavanı](truck_hangar_window_cap.md) — ✅tavan HİÇ bağlayıcı değil → Truck upgrade 2 seviyede bitmeli
- [hangarStayDuration P-bazlı](hangar_stay_duration_per_player.md) — ✅uygulandı (canlı: 120/60/40/30)
- [fast_hangar perk kodu bug](fast_hangar_perk_bug.md) — ✅çözüldü
- [Upgrade fiyatlandırma çerçevesi](upgrade_pricing_framework.md) — 7 upgrade karakterine göre payback hedefi (1.4-3.2 gün)
- [Upgrade eski omurgalar (P0/P1)](upgrade_legacy_backbones.md) — 9 kind:0 omurga draft havuzunda; Money AKTİF ZARARLI, Customer ÖLÜ
- [Upgrade ROI turu 2026-07-20](upgrade_roi_2026-07-20.md) — gelir ÜRETİM-bound; reward-çarpan perkleri underpriced no-brainer
- [Roguelite perk fiyatlandırma v3.2 ONAY](roguelite_perk_pricing.md) — ⚠️prestij-tabanlı perk büyüklükleri bayat; para-perkleri TL geçerli
- [Perk canlandırma ekonomik inceleme 2026-08-19](perk_revival_economic_review_2026-08-19.md) — `prestige_broker` doubling YAPMA (18x riski)
- [Upgrade çift sistem çakışması](upgrade_dual_system.md) — ✅çözüldü, tek sistem UpgradePanel
- [Kalıcı Kart Sistemi değer onayı 2026-08-12](permanent_cards_value_review_2026-08-12.md) — 14 karttan 3'ü revize
- [Kira-sonrası 3 özellik fiyatlandırma](post_rent_features_pricing_2026-08-15.md) — iade %25+parasız, 2-item ×1.3, karışık tır düz toplam
- [Quest 3-tier yeniden tasarım](quest_tier_redesign_2026-07-25.md) — 15→30 asset öncesi taban; EV formülü hâlâ kullanılıyor
- [Quest SABİT ödül/ceza tablosu (30 quest)](quest_fixed_reward_table_2026-07-28.md) — ⚠️ödül kolonu BAYAT (canlı: R10/R11 tablosu); dağılım 11/10/9 + hasBuff=0 geçerli
- [Hard tier targetCount retune](quest_hard_targetcount_retune_2026-07-29.md) — ⚠️D2'nin tabanı; FLAT/P-kalibreli, D2 ile çarpılırsa çöker
- [⭐D2 çifte ölçekleme bug'ı KARAR](quest_d2_double_scaling_bug_2026-08-06.md) — muafiyet listesi mevcut katalogda D2'yi NO-OP yapmalı (tip 6 dahil, R10 U10)
- [CompleteTruck renk kısıtı](quest_completetruck_color_constraint.md) — CompleteTruck ASLA renk-kilitlenemez; Toy=Red/Clothing=Yellow/Glass=Blue
- [AnswerPhone+ColorTruck bağlandı](quest_answerphone_colortruck_2026-07-25.md) — ⚠️ColorTruck asset'i YOK; AnswerPhone 2 asset
- [Quest ödül dengesi](quest_reward_balance.md) · [Quest easy4/5 çifti](quest_easy4_5_duplicate.md) — ⚠️tarihsel (easy1-5 silindi)
- [Q3: TempMoneyPerBox ölü kod](q3_tempmoneyperbox_dead.md) — KARAR=bırak; wiring için yedek sayı hazır
- [Q8: buff stack politikası](q8_buff_stacking_policy.md) — şu an sıfır etki; temp buff'a ileride MAX_STACK=2
- [FAZ2 kota-verim kalibrasyonu](quota_throughput_calibration.md) — ⚠️QuotaManager SİLİNDİ, konu kapandı
