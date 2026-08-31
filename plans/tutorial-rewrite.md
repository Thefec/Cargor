# 🎓 Tutorial Sistemi — Sıfırdan Yeniden Yazım

> Dal: `feature/tutorial-rewrite` · Başlangıç: 2026-08-31 · Onay: kullanıcı ("devam et başla")

## Neden

Ana oyun haritasında (`The Main Office`) kodlanmış bekleyen iş kalmadı, tek açık kapı playtest. Tutorial ise 2 aydır dokunulmamış: sahne içeriği neredeyse boş (tek statik FBX harita + 9 küçük prefab, 430 GameObject'in çoğu ayarlar-menü UI kopyası), script'ler iki dağınık klasöre bölünmüş.

## Onaylanan Kapsam
1. Script mimarisi + adım akışı + level tümü kapsamda (sadece level değil).
2. İçerik: **yalnız çekirdek döngü** (hareket → kutu al → rafa koy/al → masaya koy/al → tıra teslim). Telefon/quest/oda-görünürlük tutorial'a girmez.
3. İki script klasörü (`Assets/Tutorialassets/` + `Assets/NewCss/Tutorial/`) → tek klasörde (`Assets/NewCss/Tutorial/`) birleşir.

Detaylı gerekçe/riskler: plan onayı sırasında yazılan tam plan → `C:\Users\cicek\.claude\plans\imdi-senden-unu-istiyorum-robust-piglet.md` (yerel plan dosyası, repo dışı — referans için burada özetleniyor).

## Adımlar

### Adım 1 — gameplay: Klasör birleştirme (izole commit)
`git mv` ile guid korunarak taşı:
- `Assets/Tutorialassets/TutorialManager.cs(.meta)`
- `Assets/Tutorialassets/TutorialStep.cs(.meta)`
- `Assets/Tutorialassets/TutorialDoor.cs(.meta)`
- `Assets/Tutorialassets/TutorialGarageDoorController.cs(.meta)`
- `Assets/Tutorialassets/TutorialShelfState.cs(.meta)`
- `Assets/Tutorialassets/TutorialTruckSpawner.cs(.meta)`

→ hedef: `Assets/NewCss/Tutorial/`. Dokunulmayacak: `TutorialPlayerSpawner.cs`, `TutorialTruck.cs`, `TutorialTruckTrigger.cs` (zaten orada), `Truck_Anim (2) 1.prefab`. Bu commit SADECE dosya taşıma içerir, başka kod değişikliği yok.

### Adım 2 — gameplay: Sadeleştirme (ayrı commit)
- `TutorialStep.cs`: `requiredBoxType` alanına netleştirici tooltip (NetworkedShelf.BoxType vs BoxInfo.BoxType karışıklığı uyarısı).
- `TutorialManager.cs`: kapsam dışı condition tiplerinin (CompleteMinigame, Custom) switch-case'inde "desteklenmiyor" notu.
- Çekirdek döngü adım iskeleti (~8 adım, plan dosyasındaki sıra) için varsayılan öneri — nihai doldurma kullanıcıda (Inspector).

### Adım 3 — graphics-ui (Adım 1-2 ile paralel, bağımsız)
Highlight/outline materyali, skip-hint UI cilası — varsa görsel kırıklık giderilir.

### Adım 4 — qa
Guid kırılmadı mı (grep), BoxType kıyaslamaları hâlâ enum-to-enum mı, RPC server-only guard'ları korundu mu.

### Adım 5 — kontrol
Dal-sonu tek toplu ONAY kapısı, en fazla 3 tur.

### Adım 6 — kullanıcı (manuel, Editor)
- Sahneye yeni obje yerleştirme yok (kapsam dışı, FBX korunuyor)
- `TutorialManager` Inspector'ında adım listesini doldurma/sıralama
- Highlight/trigger referanslarını sahne objelerine bağlama
- `Tutorial.unity` açıp Console'da Missing Script kontrolü

### Adım 7 — playtest
`plans/playtest-checklist.md`'ye T-serisi (T1-T9) eklenecek.

## Kapsam Dışı (bilinçli)
- `TutorialShelfState.TakeItemFromShelfServerRpc` client-spoof borcu (`plans/release-push.md` G7) — ayrı iş.
- WASD client bug'ı — gözlemlenir, çözülmeye çalışılmaz.
- Telefon/quest/oda-görünürlük öğretimi — organik öğrenmeye bırakılır.

## Durum
- [x] Adım 1 — klasör birleştirme (`af65bf1`, 6 script + meta, guid doğrulandı, 0 CS)
- [x] Adım 2 — sadeleştirme (`d234880`, BoxType tooltip + kapsam dışı condition notu)
- [x] Adım 3 — graphics-ui cila (`5dbae9e`, stale `_currentStep` sonrası tutorial-bitti bug'ı düzeltildi — CompleteTutorial'da null atanmıyordu, skip/dil-değiştirme ile tekrar tetiklenebiliyordu)
- [x] Adım 4 — qa (temiz PASS, 1 bloklayıcı-olmayan not: `DebugCompleteTutorial()` context-menu debug aracı, dal-öncesi zaten var olan davranış)
- [x] Adım 5 — kontrol ONAY (bağımsız doğrulandı: guid, content-preserving taşıma, `_currentStep=null` fix, kapsam taşması yok, BoxType tooltip doğruluğu, headless derleme, departman ayrımı — bulgu yok)
- [ ] Adım 6 — kullanıcı Editor işleri (aşağıya bak)
- [ ] Adım 7 — playtest
