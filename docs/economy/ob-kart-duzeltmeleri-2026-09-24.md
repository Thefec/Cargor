# Ö-B: Hızlı Hangar / Mesai Saati / Prestij Ustası efekt düzeltmeleri (2026-09-24)

Kaynak rapor: `docs/economy/ekonomi-sifirdan-2026-09-23.md` §2, §4, §5. Plan: `plans/ekonomi-oa-ob-oc.md`.
Betik: `tools/economy_sim/ob_2026_09_24.py`. `sim.py` ve `sifirdan_2026_09_23.py` değişmedi. Çıktılar `tools/economy_sim/results_2026-09-24/` altında.
Tüm ölçümlerde **Ö-A** (kira fonu kilidi 1.0, Acil Fren muaf; kilit zorla yapılan alımlara da uygulandı) ve **Ö-C** (all_in/leveraged_rent grace'i iptal ediyor) AÇIK.
Oyuncu sayısı: sim oyuncu sayısını her koşuda baştan sona sabit tutuyor. `5b55926` sonrası oyun da aynı şekilde çalışıyor, yani modelde değişiklik gerekmedi.

## 1. Kök nedenler (sim ↔ oyun doğrulandı)

Yöntem: her karta "null ikiz" kontrolü eklendi. Null ikiz aynı fiyatta, aynı tier'da, efekti olmayan bir kart. Kartın Δkasası ile null ikizin Δkasası arasındaki fark, fiyattan bağımsız saf efekti veriyor.

| Kart | Oyun kodu | Sim | Kök neden |
|---|---|---|---|
| Hızlı Hangar | `PerkEffect.ApplyFastHangarToTruck` hangarStay ×0.75. `Truck.HangarTimerCoroutine`: süre bitince yarı dolu da olsa kalkar, cezası yok | aynı | Gelir **tırın hangarda durduğu saniyeyle** sınırlı: tırların sadece %21-45'i dolu kalkıyor, Ek Hangar +44 tır ve +17 teslim getiriyor. Tırlar arasındaki ölü süre sabit, yaklaşık 12 sn (giriş 3 + exitDelay 2 + çıkış 3 + respawn 3-5). Kalışı kısaltınca yükleme penceresinin oranı düşüyor (3P: 40/52 → 30/42) ve tır, yükleme sürerken kalkıyor. **Saf efekt 3P orta −255, 4P orta −671 TL.** 1P/2P'de efekt yok, zarar sadece fiyattan geliyor. |
| Mesai Saati | `DayCycleManager` gün ×1.125. `CustomerManager.CheckEarlyDayCompletion` kota bitince 30 sn sonra günü sona sarıyor | aynı | Orta ve iyi takım günlerin %77-100'ünde kotayı erken bitiriyor, gün hemen sona sarılıyor. Uzayan saate hiç ulaşılmıyor. **Saf efekt ≈ 0**: null ikizle fark ±120 TL, gürültü seviyesinde. Zayıf takımın kaybındaki +21 pp (1P) tamamen fiyattan: null ikiz de +28 pp veriyor. Kart zararlı değil, **ölü**. |
| Prestij Ustası | `CustomerAI.HandleSuccessfulInteraction` → customerServedPrestigeBonus 0.4+0.12·lvl | aynı | Prestijin tek ekonomik karşılığı ödül kademesi: ⌊prestij/8⌋ × 5 TL/kutu. +0.12/müşteri, 2 seviyede gün 16'ya kadar ancak +4..+20 prestij, yani 0.5-2.5 kademe getiriyor. Bunun büyük kısmı oyunun son günlerine denk geliyor. 4P iyi zaten tavana (100) yakın. Efekt pozitif (null ikize göre +66..+650 TL), ama 2 seviyenin fiyatı 370-937 TL ve bunu karşılamıyor. **Zayıf.** |

Sim ile oyun arasında kartın hükmünü değiştirecek bir sapma bulunmadı. Canlı tır prefabı `Truck_Anim (2).prefab`: exitDelay 2, sim ile aynı.

## 2. Yeni spec (gameplay uygular, fiyatlar DEĞİŞMİYOR)

| Kart | Dosya / alan | Eski → Yeni | Tür |
|---|---|---|---|
| Hızlı Hangar | `PerkEffect.ApplyFastHangarToTruck` çarpanı | `0.75f` → `0.9f` | sayı |
| Hızlı Hangar | `TruckSpawner` respawn gecikmesi (`respawnDelayRange` {3,5}) | kart sahipken **0** (tır kalkınca yenisi hemen gelir). Yeni alan `respawnDelayMultiplier` (varsayılan 1). Perk level>0 iken 0, level 0'da 1 (mutlak/idempotent atama) | mekanik |
| Hızlı Hangar | `Truck.ExitSequenceCoroutine` `WaitForSeconds(exitDelay)` | kart sahipken bekleme **0**. Yeni alan `perkExitDelayMultiplier` (varsayılan 1), `ApplyToTruck` yolundan yazılır. **`Truck.exitDelay`'e YAZMA:** EventEffectManager bu alanı snapshot/restore ediyor (cs:518-608), perk sessizce silinir | mekanik |
| Mesai Saati | `SetOvertimeMultiplier(1.125f)` | **aynı kalır** | — |
| Mesai Saati | `CustomerManager.CheckEarlyDayCompletion` kapanış payı (`dayEndGraceSeconds` 30) | kart sahipken **30 → 45 sn** (+15). Yeni alan, örn. `DayCycleManager/CustomerManager.overtimeGraceBonusSeconds = 15f`, level 0'da 0. SO'daki `dayEndGraceSeconds` değerine YAZMA (kalıcı asset, bkz. hafıza perk-mutates-persistent-assets) | mekanik (küçük) |
| Prestij Ustası | `PerkEffect.ApplyPrestigeMaster` | `0.4f + 0.12f * level` → `0.4f + 0.35f * level` (L1 0.75, L2 1.10) | sayı |

- Perk snapshot'ına yeni iki alanı ekleyin (`UpgradePanel` snapshot/restore, ~cs:677-741): respawn çarpanı, exit çarpanı ve overtime grace.
- Kart açıklama metinleri için öneriler. Hızlı Hangar: "Tır kalkar kalkmaz yenisi yanaşır; hangarda bekleme −%10." Mesai Saati: "Gün %12.5 uzar; son müşteriden sonra +15 sn ek mesai." Prestij Ustası: "Müşteri başı prestij +0.35/seviye." Loc'u müdür yapacak.
- Uygulamadan sonra sim'in `config.json`'unu da güncelleyin: `fast_hangar_mult 0.9`, `prestige_master_served_step 0.35`. Respawn, exit ve grace için `ob_2026_09_24.py`'deki parametreler (`fast_hangar_respawn_mult`, `fast_hangar_exit_mult`, `overtime_grace_add`) sim.py'ye taşınmalı.

## 3. Kart tablosu: net Δkasa (z/o/i) ve zayıf Δkayıp (pp)

Ölçüm düzeni: kart tek başına, kilidin açıldığı günden itibaren Ö-A'ya uyarak alınıyor. Strateji "hic", event'ler kapalı, hücre başına 400 koşu (`card_table_final.csv`). Gürültü yaklaşık ±100 TL. "Null" satırı yalnızca fiyatın etkisini gösteriyor.

| Kart | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| Hızlı Hangar CANLI | 3/−90/−70 (+8.0) | −72/−86/−76 (+4.3) | −13/**−562**/−164 (+0.8) | −17/**−988**/−240 (0) |
| Hızlı Hangar ÖNERİ | 2/−90/−46 (+9.0) | −45/**21/178** (+1.0) | −7/**38/369** (+0.3) | −16/**78/367** (0) |
| null (fiyat 120-300) | 0/−122/−128 (+8.8) | −59/−202/−188 (+2.0) | −14/−307/−294 (0) | −21/−317/−341 (0) |
| Mesai Saati CANLI | 28/−58/−200 (+21.2) | 28/−116/−204 (+8.0) | 45/−412/−367 (+5.5) | 1/−430/−320 (+1.7) |
| Mesai Saati ÖNERİ | 26/**42**/−132 (+4.5) | 25/**115/95** (+2.0) | 16/**159/356** (−1.5) | −32/**502/819** (−2.0) |
| null (fiyat 120-300) | 31/−59/−131 (+28.2) | 2/−164/−202 (+14.0) | 33/−294/−329 (+5.8) | 6/−293/−231 (+2.7) |
| Prestij Ustası CANLI | −10/−310/−244 (+11.5) | −80/−262/61 (+0.8) | −15/−515/−143 (0) | −37/−677/−423 (0) |
| Prestij Ustası ÖNERİ | −7/−194/**35** (+10.0) | −70/**357/790** (+0.5) | −15/**46/358** (0) | −36/−274/−5 (0) |
| null (2 sv. fiyat 370-937) | −11/−376/−388 (+13.0) | −79/−589/−595 (+1.2) | −14/−894/−737 (0) | −30/−961/−924 (+0.2) |
| *ref* Ek Hangar | 28/186/328 | 6/658/1214 | 15/935/1778 | −17/1219/1850 |
| *ref* Kumarbaz Kasası | −10/−22/229 | −15/281/832 | −3/343/997 | −20/225/1234 |

- **Pozitiflik hedefi:** üç kart da en az iki oyuncu sayısında orta/iyi takım için net pozitif. İstisnalar: Hızlı Hangar 1P'de negatif (1P'de 120 sn kalışa göre 12 sn ölü süre önemsiz). Prestij Ustası 4P'de ≈0/negatif (4P fiyatı 937 TL, iyi takım prestij tavanına yakın).
- **Zayıf Δkayıp:** önerilen her hücrede, aynı fiyattaki null ikizin kaybına eşit ya da altında. Kalan +4..+10 pp yalnız 1P'de görülüyor ve fiyattan geliyor, efektten değil. Mesai Saati 3P/4P'de kaybı düşürüyor (−1.5/−2.0 pp).
- **Dominantlık:** hiçbir hücrede Ek Hangar'ı geçen yok. Kumarbaz'ı geçen iki hücre var: Mesai 4P orta (502 > 225) ve Prestij Ustası 2P orta (357 > 281). İkisinde de iyi takım değeri Kumarbaz'ın altında ya da ona eşit (790 < 832, 819 < 1234).
- **Event'ler açıkken** (`card_table_events_on.csv`, 300 koşu) aynı tablo: Hızlı Hangar 3P 8/416, 4P 87/487. Mesai 3P 157/529, 4P 419/825. Prestij Ustası 2P 289/747, 3P 264/462. Hüküm değişmiyor.

Denenip elenen varyantlar (`sweep_r1.csv`, `sweep_r2.csv`, `sweep_fh_check.csv`):
- Hızlı Hangar kalış ×1.0 + yalnız respawn 0: zayıf (3P orta −99). exitDelay'e dokunmadan ×0.9 + respawn 0: 2P/3P orta negatif. Yani exitDelay 0 şart.
- Mesai grace +30 veya daha fazlası 4P orta/iyi'de 1038-1626 TL, Ek Hangar düzeyi. +20 sınırda (4P orta 695). +10 3P orta'da negatif. Kota +1 müşteri de denendi, o da Ek Hangar düzeyinde.
- Prestij Ustası adım 0.4-0.6: 2P iyi 875-1077, yani Kumarbaz'ı ve Ek Hangar'ı zorluyor. Anında +8 prestij veren mekanik ise ≈0.35 ile aynı sonucu veriyor ama tek seferlik bayrak gerektiriyor, bu yüzden reddedildi.

## 4. Ö-A kazanımları Ö-B'den sonra da korunuyor

Değerler kayıp % : gün-16 kasa, strateji sırası hiç / açgözlü / mantıklı. Event'ler açık, 300 koşu (`matrix_oa_ob.csv`). Ö-A+Ö-C sütunu 2026-09-23 §4 "ÖNERİ" sütununu birebir yeniden üretiyor.

| Hücre | CANLI (kilit yok, grace bug'lı) | Ö-A+Ö-C | Ö-A+Ö-C+Ö-B |
|---|---|---|---|
| 1P zayıf | 20:218 / 100:220 / 92:314 | 20:218 / 11:190 / 10:183 | 20:218 / 11:197 / 11:184 |
| 1P orta | 0:1131 / 54:272 / 14:289 | 0 / 0:306 / 0:290 | 0 / 0:334 / 0:325 |
| 2P zayıf | 21:465 / 100:537 / 92:629 | 21 / 1:342 / 3:350 | 21 / 1:345 / 2:348 |
| 2P orta | 0:3392 / 16:2781 / 6:2247 | 0 / 0:1555 / 0:1817 | 0 / 0:1717 / 0:2105 |
| 3P zayıf | 45:901 / 100:886 / 95:1087 | 45 / 2:649 / 56:937 | 45 / 1:654 / 56:942 |
| 3P orta | 0:4475 / 13:5677 / 7:3425 | 0 / 0:2581 / 0.3:3104 | 0 / 0:2941 / 0.3:3517 |
| 3P iyi | 0:7747 / 0:12304 / 0:10717 | 0 / 0:8658 / 0:9448 | 0 / 0:10129 / 0:10927 |
| 4P zayıf | 61:1420 / 99:1341 / 96:1493 | 61 / 3:961 / 63:1383 | 61 / 3:988 / 63:1381 |
| 4P orta | 0:4575 / 17:7150 / 9:4097 | 0 / 0:2539 / 0:2992 | 0 / 0:3007 / 0:3415 |
| 4P iyi | 0:8558 / 0:14506 / 0:11682 | 0 / 0:8163 / 0:9283 | 0 / 0:8985 / 0:10068 |

- Zayıf takımda tuzak kapalı kalıyor: kayıplar ±1 pp içinde değişmedi. Orta/iyi takımda kart kaynaklı iflas hâlâ %0.
- Ö-B, Ö-A'nın bedelini kısmen geri ödüyor: kart alan (açgözlü/mantıklı) orta takımın gün-16 kasası %9-18 arttı, iyi takımınki %8-19 (1P-4P; 1P iyi / 2P iyi satırları CSV'de).
- Sahiplik oranları neredeyse aynı kaldı (mantıklı stratejide ±0.1). Kartlar daha sık alınmıyor, sadece artık zarar ettirmiyor.

## 5. Riskler
- **Mesai Saati 4P'de güçlü** (orta +502, iyi +819; fiyat 300, T1, gün 1'den alınabiliyor). Playtest'te baskın hissettirirse ilk ayar grace'i +15 → +10 sn'ye çekmek (4P orta +169). Kart P-dizisine dokunmayın.
- **Hızlı Hangar'ın değeri modeldeki animasyon sürelerine bağlı.** Giriş/çıkış animasyonu sim'de 3+3 sn varsayılıyor, gerçek Animator süresi ölçülmedi. Kazancın kaynağı respawn (3-5 sn) ve exitDelay (2 sn); bunlar mutlak saniye olduğu için hüküm aynı kalır, ama animasyonlar uzunsa kazanç oransal olarak küçülür.
- exitDelay 0 olunca kalkış uyarı sesi (`exitDelayClip`) çalmadan tır hemen çıkar. UX kontrol edilmeli; gerekirse 0.3-0.5 sn bırakılabilir (etkisi ihmal edilebilir).
- Prestij Ustası 4P'de hâlâ zayıf (fiyat 937, tavan). Kabul edildi, çünkü hedef "en az bir P'de pozitif" idi.
- Mekanik değişikliklerin üçü de perk-event çakışma sınıfında. Yeni alanlar snapshot'a eklenmezse perk sessizce ölür (hafıza: perk-mutates-persistent-assets, perk_card_absolute_assignment_conflict).

## 6. Sim senkronu + 0.4 sn kalkış alt sınırı (2026-09-24, uygulama sonrası)
- `tools/economy_sim/config.json` + `sim.py` artık varsayılan olarak yeni canlıyı modelliyor. Ö-A: `upgradeRentReserveFraction 1.0`, `rentReserveExempt [emergency_brake]` (zorlanmış alım ve reroll dahil). Ö-B: `fast_hangar_mult 0.9`, `fast_hangar_respawn_mult 0`, `fast_hangar_exit_mult 0`, `fast_hangar_exit_min 0.4`, `overtime_grace_add 15`, `prestige_master_served_step 0.35`. Ö-C: grace iptali zaten sim varsayılanıydı.
- Regresyon: Ö-A/Ö-B kapatıldığında yeni sim eski sim ile 540/540 koşuda bit düzeyinde aynı. Yeni varsayılan matris (`verify_matrix_native.csv`), §4'teki Ö-A+Ö-C+Ö-B sütunuyla gürültü seviyesinde uyuşuyor.
- **0.4 sn alt sınırı ihmal edilebilir DEĞİL (orta takım için).** Sim dt=1 sn çözünürlükte 0.4'ü 1 sn'ye yuvarlıyor, bu yüzden dt=0.2 ile ölçüldü (`verify_fh_exitmin_dt02.csv`, 600 koşu). Hızlı Hangar net Δkasa, z/o/i sırasıyla:

| | 2P | 3P | 4P |
|---|---|---|---|
| exit 0 sn | −41/−18/116 | −8/2/273 | −31/78/399 |
| exit 0.4 sn (canlı) | −41/**−71**/105 | −5/**−55**/201 | −26/**24**/379 |

  Orta takım 2P/3P'de ≈0'dan yaklaşık −55..−70'e iniyor. İyi takım 2P-4P'de pozitif kalıyor (105/201/379). Zayıf takım Δkayıp değişmiyor. Canlı karta göre hâlâ çok daha iyi (3P orta −562 → −55). "En az bir profilde pozitif" hedefi tutuyor, ama "orta takım için kazançlı" iddiası 0.4 ile düşüyor.
- Sim notu: alt-saniye mekanikleri (≤1 sn) varsayılan dt=1 ile ölçülmemeli. `config.json` sim.dt=0.2 ile tekrar koşun.
