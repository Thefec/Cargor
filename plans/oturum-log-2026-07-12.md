# 📋 Oturum Logu — 2026-07-12 (FAZ 1 otonom bug-avı)

> Bu dosya tek bir oturumun tam kaydı (save). Canlı durum için `plans/devam.md`, faz kapsamı için `plans/release-push.md`. Bu = arşiv/log.

## Bağlam
Kullanıcı ~1 saat dışarıdaydı, "onay istemeden kalan token'ı verimli işe harca" dedi. Dönünce FAZ 1'e devam + "sıralamayı sana bırakıyorum" + "önerini yap" ile otonom ilerleme onaylandı. **İş akışı:** her BÜYÜK iş gameplay/economist departmanı → müdür diff doğrulama → **kontrol kapısı (zorunlu)** → seçici commit. Hassas çalışma-ağacı dosyalarına (late-join fix, SteamManager, LateJoinGuard, fontlar, ProjectSettings) **hiç dokunulmadı** (Steam testine bağlı).

## Toplam çıktı
- **14 commit** (bu oturum), hepsi kontrol ONAY.
- **21 bulgu kapandı** (aşağıda).
- **28-item tüm-oyun bug envanteri** (G1–G28) çıkarıldı → `release-push.md`.
- **Roguelite dalı merge-onayı** (qa, blocker yok).
- **economist memoları:** C1/C5 karar-memosu + G9 event değerleri + kalıcı memory dosyaları.
- **Açık soru kapandı:** `ProjectSettings.asset` = IL2CPP/SENTIS DEĞİL, sadece LF→CRLF churn.

---

## Commit listesi (eski→yeni)

| Commit | İş | Kim | Kapı |
|---|---|---|---|
| `8a74238` | **G8** müşteri spawn init sırası (perk+loot geri geldi) · **G18** singleton guard · **G5** ölü/açık RPC sil | gameplay | ONAY 1 tur |
| `1374a62` | **G4** test-event RPC sil (event-bypass exploit) · **G17** kutu NetworkObject.Despawn · **G22** reward negatif-clamp | gameplay | ONAY 1 tur |
| `60e767b` | **G14** stamina writePerm ihlali · **G21** MoneyUI çift-subscribe · **G20** WaitBar despawn leak | gameplay | ONAY 1 tur |
| `9bc4141` | **G1-a** SetMoney/ResetMoney server-only → `SetMoney(999999999)` exploiti kapandı | müdür | ONAY 1 tur |
| `f9a3f1b` | **G3** telefon anti-spam server-authoritative (per-client `ServerCallState`) → RPC-spam bedava-para loop'u kapandı | gameplay | ONAY 1 tur |
| `9120670` | docs: 28-item envanter | müdür | — |
| `f4f07b6` | **G9 artış-1** BUSY/RAINY/MARKETING DAY event'leri bağlandı (kapsam 11→14/17) | gameplay | ONAY 1 tur |
| `8faf08a` | docs: G9 artış-1 + artış-2 hazır spec | müdür | — |
| `ad4d3f0` | **UpgradePanel null-guard** (roguelite merge-hardening) | müdür | — (KÜÇÜK) |
| `902aa54` | docs: roguelite merge-öncesi tarama sonucu | müdür | — |
| `1ea6f32` | **G26** ItemData cache · **G27** dosya-adı rename · **G13** VIP-RNG server-only · **G19** static lock-cleanup host-restart | gameplay+müdür | ONAY 1 tur |
| `5fc8161` | docs: temizlik batch kaydı | müdür | — |
| `110a1d7` | **G11** takvim server-seeded senkron · **G12** event oyuncu-efekti tüm oyunculara | gameplay | ONAY (G12 tur-1, G11 tur-2) |
| `f25b1e8` | docs: G11+G12 kaydı | müdür | — |

---

## Kapatılan 21 bulgu (kategorize)

**Çekirdek exploit'ler (para/state güvenliği):**
- **G1-a** `MoneySystem` SetMoney/ResetMoney server-only (arbitrary-set exploiti).
- **G3** telefon çağrı anti-spam server-authoritative (bedava-para/timeSkip spam).
- **G4** `EventEffectManager.TestEventServerRpc` silindi (herhangi client event-bypass).
- **G5** `CustomerManager.RequestCustomerSpawnServerRpc` silindi (sınırsız müşteri/prestij farm).

**Korrektlik / netcode senkron:**
- **G8** CustomerAI spawn init sırası → Sabırlı-Müşteriler perki + deterministic-loot geri geldi (gerçek regresyon).
- **G11** takvim server-seeded deterministik (her client aynı takvim; late-join base-day fix).
- **G12** event oyuncu-efektleri her peer kendi owned oyuncusuna (tüm oyuncular etkilenir).
- **G13** VIP %10 bonus RNG server-only (peer-divergence).
- **G14** stamina NetworkVariable writePerm=Owner (her-frame write ihlali).
- **G17** TruckTrigger kutu `NetworkObject.Despawn` (hayalet-kutu/stale-ref).

**Robustluk / lifecycle / temizlik:**
- **G18** CustomerManager Start singleton guard (destroyed-instance event leak).
- **G19** lock-cleanup static `OnServerStopped`'a bağlandı (host-restart).
- **G20** WaitBar OnNetworkDespawn unsubscribe.
- **G21** MoneyUI çift-subscribe giderildi.
- **G22** Truck reward negatif-clamp (her yolda).
- **G26** ItemData `Resources.LoadAll` → build-once static cache (3 site).
- **G27** `TimeBasedLightController.cs`→`AutoLightController.cs` (dosya=sınıf, GUID korundu).

**Event kapsamı (G9 artış-1):**
- **BUSY DAY** (1.3) · **RAINY DAY** (0.8) · **MARKETING DAY** (1.2+reward0.7) bağlandı. Kapsam 11→14/17.

**Roguelite merge-hardening:**
- **UpgradePanel** PurchaseUpgradeServerRpc null-guard.

---

## Kalan FAZ 1 (bir sonraki oturum)

**Güvenli, hemen yapılabilir:**
- **G10** `ApplyEventEffectToNewObject` hiç çağrılmıyor → event sonrası spawn olan truck/customer multiplier almıyor. Wiring (mevcut metod), ekonomik risk yok. *Önerilen sıradaki iş.*
- **G25** StaminaBarUI ölü kod mu → teyit + sil.
- **G6/G7** RPC `sender==owner` (önce meşru table/shelf caller-analizi).

**Play-test'e bağlı (kullanıcı testi gerekiyor):**
- **G1-b + G16** BoxFallPenalty server-auth — kutu-ownership fizik modeli Play-test'te doğrulanmalı (server mi client mi collision simüle ediyor). `ModifyMoneyServerRpc` ham-delta bu yüzden açık bırakıldı (MoneySystem'de açıklayıcı yorum var).
- **G2** truck teslim-kanıtı (fiziksel kutu doğrulaması).

**economist + mekanik (değerler hazır, `release-push.md` G9):**
- **G9 artış-2:** QUOTA DAY (TruckSpawner tek-renk), SURPRISE AUDIT (`penaltyPerBoxMultiplier=1.5`, ceza plumbing), CUSTOMER SUPPORT (`phoneCooldownMultiplier=0.7`, boş stub doldur), FESTIVAL DAY (gün-başı kira%-bazlı bonus).

**Design teyidi (kullanıcı kararı):**
- **G15** movement client-auth kabul mü (yoksa server-auth refactor)?
- **G23** masa-dolu haksız prestij cezası isim/mantık.
- **G24** ESC global-pause kastı mı?
- **G28** dedicated-server drop state kaybı (host-client'ta önemsiz).

**KARAR bekleyen ekonomi (economist memo hazır, `release-push.md` FAZ 2):**
- **C1 (E3)** kota-ölümü ölü kod → economist: 2-kademeli tampon öner.
- **C5 (E5)** wealthTax hep 0 → economist: ölü terimi kaldır öner.

---

## FAZ 0 (havadaki iş — hâlâ kullanıcıya bağlı)
Roguelite dalı **kod olarak merge'e hazır** (qa merge-öncesi tarama: blocker yok). Kalan: kullanıcı çok-oyunculu Play doğrulaması (late-join reddi + reconnect) → late-join fix seçici commit + [NETDBG] kaldır + font/ProjectSettings revert → **roguelite→main merge**.

## Çalışma ağacı notu (commit'siz, DOKUNULMADI)
`SteamManager.cs`+`LateJoinGuard.cs` (late-join fix + [NETDBG], test bekliyor), 8 Figma font + LiberationSans (Unity-açık artefaktı), `ProjectSettings.asset` (sadece LF/CRLF churn → commit öncesi `git checkout`), `Assets/_Recovery/` (crash junk). Bu oturumda bunların HİÇBİRİNE dokunulmadı.
