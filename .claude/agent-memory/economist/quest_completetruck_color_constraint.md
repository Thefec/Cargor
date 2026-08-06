---
name: quest-completetruck-color-constraint
description: CompleteTruck quest tipi renk-kilitlenemez (kod bug/kisit) - requireSpecificBoxType=true verilirse quest SONSUZA KADAR tamamlanamaz (sessiz soft-lock). Ayrica Toy/Clothing/Glass <-> Red/Yellow/Blue SABIT urun-kutu eslemesi.
metadata:
  type: project
---

**Kod-doğrulandı (2026-07-25).** İki ayrı ama ilişkili bulgu, quest tasarımında renk/tip
kombinasyonu seçerken bağlayıcı:

## 1. CompleteTruck (QuestType=2) rengi ASLA kilitlenemez

`Assets/Scripts/Quest/Manager/QuestManager.cs:348-351`:
```csharp
private void HandleTruckCompleted()
{
    if (!IsServer) return;
    UpdateQuestProgress(QuestType.CompleteTruck, BoxInfo.BoxType.Red, 1); // HARDCODED Red!
}
```
`QuestTracker.OnTruckCompleted` parametresiz bir `Action` — gerçek tır kargo rengi hiç taşınmıyor.
`UpdateQuestProgress` (satır 488) `requireSpecificBoxType` kontrolünü QuestType'tan BAĞIMSIZ TÜM
tiplere uyguluyor: `if (questData.requirement.requireSpecificBoxType && questData.requirement.requiredBoxType != boxType) continue;`

**Sonuç**: Bir CompleteTruck quest asset'i `requireSpecificBoxType=true` + `requiredBoxType≠Red`
ile oluşturulursa, gelen event her zaman `boxType=Red` taşıdığı için koşul asla sağlanmaz →
**quest sonsuza kadar `Active` kalır, hiç `Completed` olmaz**. Oyuncu kabul eder, tüm günü çalışır,
gün sonu yine de `ApplyPenalties` cezasını yer — sessiz, kalıcı bir tuzak (soft-lock), exploit'in
tersi yönde bir "scam quest" riski. `requiredBoxType=Red` ile oluşturulsa bile YANILTICI olur
(her tır "Red" sayılır ama bu gerçek kargo rengini yansıtmaz, sadece placeholder değeri).

Renk-özel tır tamamlama için asıl tasarlanmış alan `requirement.requireSpecificTruckColor` /
`requiredTruckColor` (ayrı alan çifti) — ama bu SADECE `QuestType.CompleteSpecificColorTruck`
(tip=6) için anlamlı, ve o tip **DEAD** (hiçbir oyun kodu tetiklemiyor, bkz `plans/quest-redesign-2026-07-25.md`
§2). Bağlamak isterse (`Truck.cs`'te gerçek kargo rengini `NotifyTruckCompleted`'e parametre
olarak eklemek + `CompleteSpecificColorTruck` event'ini gerçekten fire etmek) küçük ama gameplay
kod değişikliği gerektirir, bu turun kapsamı dışı.

**KURAL: CompleteTruck quest asset'lerinde `requirement.requireSpecificBoxType` HER ZAMAN false
kalmalı.** Renk varyasyonu sadece PlaceBoxOnShelf ve PackToy'da güvenli.

## 2. Sabit ürün↔kutu-rengi eşlemesi (renk = ürün kategorisi, serbest değil)

`Assets/NewCss/PickUpScripts/Table.cs:828-832 IsValidBoxProductCombination()` +
`Assets/NewCss/CustomerSripts/CustomerManager.cs:1093-1095 ProductTypeToBoxType()`:

| Ürün (ProductType) | Kutu rengi (BoxType) |
|---|---|
| Toy | Red |
| Clothing | Yellow |
| Glass | Blue |

İSTİSNASIZ — `Table.cs:744 PerformInstantBoxing` bu eşlemeyi ihlal eden paketleme girişimini
REDDEDER (kutu kırılma efekti + `NotifyBoxingFailedClientRpc`, `NotifyToyPacked` hiç tetiklenmez).
Yani PackToy quest'inde "renk" seçimi aslında "hangi ürün kategorisini paketle" seçimidir —
`requiredBoxType=Yellow` demek "Clothing paketle" demektir, "sarı oyuncak" gibi bir ifade
ÇELİŞKİLİDİR (Toy hiçbir zaman Yellow olamaz). Eski `easy5.asset` bu hatayı yapmıştı (metin
"Sarı oyuncak paketle" ama mekanik olarak hâlâ çalışırdı çünkü `NotifyToyPacked(playerBox.boxType)`
paketlenen ürünün GERÇEK rengiyle ateşleniyor — sadece açıklama metni yanıltıcıydı, quest kırık
değildi). Yeni quest açıklamaları yazılırken bu eşlemeye uyulmalı.

PlaceBoxOnShelf tarafında (`ShelfState.cs:608`) analog bir "hangi raf slotu hangi rengi kabul eder"
kısıtı ARANMADI (zaman kısıtı, bu turun kapsamı dışı) — sadece `boxInfo.boxType` (kutunun zaten
taşıdığı renk, muhtemelen paketleme anında belirlenmiş) event'e taşınıyor. Playtest/QA'ya not:
eğer rafa koyma da bir kısıta tabiyse (örn. belirli raf sadece belirli renk kabul ediyorsa) bu
etki henüz doğrulanmadı.

**How to apply**: Yeni quest asset'i oluşturulurken (herhangi bir tier) CompleteTruck tipinde asla
renk kilidi açma; PlaceBoxOnShelf/PackToy renk kilidi açarken rengi doğru ürün kategorisiyle
eşleştirerek adlandır (Red=Toy/oyuncak, Yellow=Clothing/giysi, Blue=Glass/cam).

İlişkili: [[quest_tier_redesign_2026-07-25]] (bu bulgunun kullanıldığı asıl tasarım turu),
[[quest_answerphone_colortruck_2026-07-25]] (CompleteSpecificColorTruck canlandırıldı; renk-bag
mekaniği `TruckSpawner.cs` kod-doğrulandı, ~1/3 varsayımı artık KESİN gerçek)
