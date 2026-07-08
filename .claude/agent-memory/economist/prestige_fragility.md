---
name: prestige-fragility
description: startingPrestige=5.0 ile tek rush dalgasinda 3 kayipta oyun bitiyor - nicel esik degerleri
metadata:
  type: project
---

2026-07-07 analizi (güncel değerler için `PrestigeManager.cs` ve `EkonomiAyarlari.asset`'i tekrar kontrol et):

`startingPrestige=5.0`, `customerLostPrestigePenalty=-2.0`. Formül: `lossToZero = ceil(startingPrestige / |penalty|)`. Rush dalgasında max eşzamanlı müşteri 6 (GDD 9.2, Öğle Rush). Mevcut değerlerle 3 kayıp (6'nın yarısı) prestiji sıfırlıyor — beceriden çok spawn/RNG zamanlamasına bağlı bir ölüm riski.

Test edilen ve önerilen kombinasyon: `startingPrestige=15.0` + `customerLostPrestigePenalty=-1.5` → `lossToZero=10`, yani max eşzamanlı 6 müşterinin TAMAMI kaçsa bile (0 başarılı servisle) tek dalga oyunu bitiremiyor. Servis/kayıp oranı 4:1'den 3:1'e yumuşuyor.

Ayrıca not: `PrestigeManager.GetCustomerCapacity()` formülü (`1+floor(prestige/10)`, prestij=5'te kapasite=1) hiçbir wave-spawn koduna bağlı bulunamadı (muhtemelen dead code / sadece UI). Gameplay/QA'ya doğrulatılmalı.

**Why:** Prestij dengesi istenirse hızlıca bu eşik formülüyle (`ceil(startingPrestige/penalty)` vs. max eşzamanlı müşteri sayısı) yeniden hesaplanabilir.

**How to apply:** Prestij parametrelerinde değişiklik önerirken her zaman "tek en kötü rush dalgası tamamen kaybedilirse oyun biter mi?" testini yap — bu, ortalama senaryodan çok daha kritik bir eşik.
