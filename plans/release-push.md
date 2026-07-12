# 🚀 RELEASE PUSH — Çıkışa Doğru

> Kullanıcı hedefi: **buglar → denge/sistemler → Steam çıkışı.** Bu dosya fazların canlı kaydı.
> Oturum logu için `plans/devam.md`. Bu dosya = faz kapsamı + durum.

---

## FAZ 0 — Havadaki işi kapat  🟡 (test bekliyor)
Roguelite dalı kod olarak bitti; kapanış adımları çok-oyunculu teste bağlı.
- [ ] **Combined build testi** (late-join fix + `[NETDBG]` dahil) — Steam'e yükle, iki senaryo:
  - **A)** Late-join reddi: whitelist-dışı arkadaş maç sonrası katılamaz + net mesaj görür; **VE** normal çıkışta yanlış mesaj ÇIKMAMALI (1. tur regresyonu buydu).
  - **B)** Reconnect: düş→bağlan→satın alınmış perkler client-local geri gelir.
- [ ] Test ✅ → SteamManager+LateJoinGuard'ın **sadece late-join fix hunk'larını** seçici commit'le (`[NETDBG]` hariç).
- [ ] `[NETDBG]` enstrümantasyonunu kaldır (SteamManager ~706-731 + client-disconnect log ~795; LateJoinGuard ~128-131/146).
- [ ] Font/ProjectSettings artefaktlarını revert (bkz [[unity-batchmode-artifacts]]).
- [ ] Roguelite dalını `main`'e merge.

> ⏸️ **2026-07-12:** Kullanıcı testi şimdilik atladı → FAZ 1'e paralel geçildi. FAZ 0 kapanışı test yapılınca devam eder.

---

## FAZ 1 — Tüm-oyun bug envanteri  🚧 (aktif)
Salt-okunur qa taramaları → önceliklendirilmiş bulgu listesi → onaylı düzeltmeler.

### Dilim: Netcode / multiplayer  🚧 (2026-07-12 başladı)
İki paralel qa: **A** bağlantı/lobi/yaşam döngüsü (SteamManager, LateJoinGuard, LobbySaver, PlayerSpawner, NetworkCleanupHelper, NetworkObjectPool) · **B** state senkron/yetki (PlayerRosterEntry, GameStateManager, DayCycleManager roster, BreakRoomManager, NextDayUIManager, ClientNetworkTransform).
- ⚠️ In-flight istisna: SteamManager+LateJoinGuard'daki commit'siz late-join fix + `[NETDBG]` bulgu SAYILMAZ.

#### Bulgu envanteri (2026-07-12 qa A+B, müdür doğruladı)
**P0 — CRITICAL, gerçek olasılık yüksek, ucuz fix:**
- [ ] **N1** `PlayerSpawner.cs:304` — `spawnIndex = clientId % spawnPoints.Length` null/boş kontrolünden (305) ÖNCE → NRE/DivByZero + orphan playerInstance (soft-lock). ✅ doğrulandı. Fix: kontrolü öne al + fail'de Destroy.
- [ ] **N2** `LobbySaver.cs:82-96,196-212` — `OnApplicationFocus(false)`→`ClearLobby()`→Steam `Lobby.Leave()`. Standalone'da alt-tab / overlay / Discord'a geçiş bile lobiyi terk ettiriyor (DontDestroyOnLoad → oyun sahnesinde de). ✅ handler mevcut. Fix: focus-loss'ta leave etme (sadece Quit/mobil-pause).
**P0 — CRITICAL, saldırı yüzeyi / robustluk:**
- [ ] **N3** `DayCycleManager.cs:786-790`+`BreakRoomManager.cs:352-379` — "herkes break room'da" kararı client-authoritative pozisyona dayanıyor, `SetBreakRoomReadyServerRpc`'de server doğrulaması yok → lag desync + hileli client bypass. (Fix daha büyük: server-side presence doğrulama.)
- [ ] **N4** `DayCycleManager.cs:642-646` — `NextDayServerRpc(RequireOwnership=false)` sadece `IsServer` guard'lı; gün-bitti/kira kontrolü yok → herhangi client kirayı atlayıp gün 16'ya zıplayabilir. ✅ doğrulandı. Şu an canlı UI caller yok (editor ContextMenu). Fix: `_networkIsDayOver`/breakRoomReady guard.
**P1 — ÖNEMLİ, tutarlılık:**
- [ ] **N5** `SteamManager.cs:714-753` hook'ları (`HookNetworkDiagnostics`/`HookLateJoinRejectionHandler`) `NetworkManager.Singleton` callback'lerine abone; SteamManager DontDestroyOnLoad DEĞİL (grep: 0 çağrı) → `OnDisable`'da unsubscribe yok → sahne geçişinde dangling delegate, `MissingReferenceException`, tekrarlı host/join'de katlanan abonelik. → **late-join finalizasyonuna dahil et.**
- [ ] **N6** `SteamManager.cs:1849-1866` (`NotifyBreakRoomManager`, çağrı 500/516/1362/1718) — ham Steam lobi Members → `requiredPlayers` ikinci yazma yolu; roster tek-kaynak yorumunu (`DayCycleManager.cs:701-704`) çiğniyor.
- [x] **N7** `DayCycleManager.cs:601-613` `GetPlayerCount` → roster otorite (`GameStateManager.RosterPlayerCount`, BreakRoomManager ile aynı desen), fallback ConnectedClients→1. Kira formülü değişmedi. **kontrol ONAY, commit'li.** ✅
**P2 — KÜÇÜK / latent:**
- [ ] **N8** `LateJoinGuard.cs:139` kapasite TOCTOU (teorik, düşük).
- [ ] **N9** `GameStateManager.cs:102-119` `ApplyGameEndState` switch'te `None` case yok (latent).
- [ ] **N10** `DayCycleManager.cs:842-845` `HandleBreakRoomReadyChanged` boş gövde; server-auth state ↔ yerel UI tetikleyici ikiye ayrık (N3 ile ilişkili).

### Dilim: Çekirdek döngü + ekonomi  🚧 (2026-07-12, qa korrektlik taraması)
Bulgular (müdür 3'ünü kodda doğruladı):
- [x] **E1/C2** (CRITICAL) iflas dalı game-over döngüsünü durdurmuyordu → `TriggerLose()` spam. **Fix:** ayrı server-only `_gameOverStopProcessing` flag'i (Update guard'ında); `_networkIsDayOver` BİLEREK set edilmiyor (dayEndScreen açılmasın + NextDay ilerlemesin — kontrol tur-1 bunu yakaladı). ResetLocalState'te sıfırlanıyor. **kontrol ONAY (tur-2), commit'li.** ✅
- [x] **E2/C3** (ÖNEMLİ) `ResetLocalState` `_rentPaymentCount`/`_graceUsed`/`insuranceAvailable`'ı sıfırlamıyordu → replay'de kirli state. **Fix + kontrol ONAY, commit'li.** ✅
- [ ] **E3/C1** (CRITICAL, KARAR) `QuotaManager.CheckEndOfDayQuota`/`OnQuotaFailed` hiçbir yerden çağrılmıyor/dinlenmiyor → GDD'de tanımlı kota-ölümü ölü kod. ✅ doğrulandı. **Karar+economist:** kota-ölümünü aç (zorluk↑) mı, GDD davranışı ne (hard game-over? grace?).
- [x] **E4/C4** (ÖNEMLİ) prestij≤0 game-over sadece tek yolda. **Fix:** merkezi ≤0 kontrolü `PrestigeManager.ModifyPrestigeServerRpc`'de (tüm ceza yollarını kapsar), OnCustomerLost'taki redundant blok kaldırıldı, `using NewCss;` eklendi. **kontrol ONAY, commit'li.** ✅
- [ ] **E5/C5** (ÖNEMLİ, KARAR+economist) `UpgradeManager.Buy()` hiç çağrılmıyor → `IsPurchased` hep boş → rent `wealthTax = upgradeValue×%10` hep 0 (kira tasarımdan sistematik düşük). ✅ doğrulandı. **Karar:** Buy()'ı satın-alma akışına bağla (kira↑, roguelite sistemine dokunur) vs ölü terimi kaldır.

Temiz: MoneySystem clamp'li (negatif/overflow yok), UpgradePanel satın-alma server-auth (client exploit kapalı), NextDay guard doğru.

### Sonraki dilimler (sırasız)
- Roguelite upgrade (merge öncesi son regresyon taraması).
- Geniş tüm-oyun (Assets/NewCss geneli).

---

## FAZ 2 — Ekonomi + GDD sistem-boşluğu  ⬜ (bekliyor)
Ekonomi dengesi + GDD'de tanımlı ama kodda eksik/yarım sistemler.

## FAZ 3 — Steam çıkışı  ⬜ (bekliyor)
Build paritesi, depot/setlive ([[cargor-steam-deploy]]), store hazırlığı.
