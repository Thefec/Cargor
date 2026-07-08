---
name: money-config-conflict
description: Cargor'da baslangic parasi icin uc farkli, birbiriyle celisen kaynak var
metadata:
  type: project
---

2026-07-07'de tespit edildi (kod değişmiş olabilir, önce güncel dosyaları oku ve doğrula):

- `Assets/NewCss/UIScripts/MoneySystem.cs:12` → `startingMoney = 100000` ("Test için" yorumuyla, açık placeholder)
- `Assets/NewCss/GameState/DifficultyManager.cs:36` → `baseStartingMoney = 100`, ardından `ScaledStartingMoney` ile oyuncu sayısına göre ×0.85^(P-1) küçültülüyor (1P=100, 2P=85, 3P=72, 4P=61) ve `ApplyDifficultySettings()` içinde `moneySystem.startingMoney`'nin üzerine yazılıyor
- `GDD.md` bölüm 31.1 → tüm simülasyonlar 500 TL varsayıyor

**Why:** Üç kaynak da farklı sayı söylüyor; hangisinin gerçekte çalışma zamanında etkili olduğu (DifficultyManager'ın MoneySystem'i her zaman override edip etmediği) net değilse startingMoney önerisi yanlış temellendirilebilir.

**How to apply:** Bu alanda öneri verirken önce DifficultyManager'ın gerçekten sahneye/GameManager'a bağlı olup olmadığını, `ApplyDifficultySettings()`'in her oyun başında çağrılıp çağrılmadığını doğrula. Önerilen düzeltme: her iki kaynağı da 500 TL'ye senkronlamak, `moneyMultiplierPerPlayer`'ı 1.0 yapmak (bkz. [[rent_death_spiral]]).
