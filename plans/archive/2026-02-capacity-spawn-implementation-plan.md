# Kapasite & İtibar Bazlı Müşteri Spawn Sistemi

> 🗄️ **ARŞİV / OBSOLETE (2026-02-14 tasarım dokümanı).** Bu sistem SONRADAN uygulanıp canlıya alındı — `CustomerManager` bugün `CountActiveInteractables`/`storeLevel`/`playerCountMultiplier` (1P=1.0/2P=1.3/3P=1.6/4P=1.9) ile çalışıyor; "Opsiyon B" seçildi. Belgedeki "kararını bekliyorum" soruları ÇÖZÜLDÜ, `file:///d:/UnityFolders/...` yolları ÖLÜ (proje artık `C:/Users/cicek/Documents/GitHub/Cargor`). Yalnızca tarihsel referans.

Mevcut lineer formül (`baseCustomersPerDay + (day-1) * increase`) oyuncunun gerçek durumunu yansıtmıyor. Yeni sistem, sahnedeki etkileşime açık yüzeyleri (raf/masa) ve mağaza seviyesini baz alarak dinamik müşteri sayısı hesaplayacak.

## Mevcut Durum Analizi

| Dosya | Mevcut Rol |
|-------|-----------|
| [CustomerManager.cs](file:///d:/UnityFolders/Editor/Cargor/Assets/NewCss/CustomerSripts/CustomerManager.cs) | `CalculateTodaysCustomerCount(day)` → `base + (day-1)*increase` (satır 298-301) |
| [DifficultyManager.cs](file:///d:/UnityFolders/Editor/Cargor/Assets/NewCss/GameState/DifficultyManager.cs) | `ApplyCustomerSettings()` oyuncu sayısına göre `baseCustomersPerDay` değerini override ediyor (satır 422-430) |

> [!IMPORTANT]
> **DifficultyManager Entegrasyonu**: `DifficultyManager` şu an `baseCustomersPerDay` değerini doğrudan yazıyor. Yeni sistemde bu entegrasyon korunmalı — `DifficultyManager` oyuncu sayısı çarpanını sağlarken, `CustomerManager` kapasiteyi dinamik hesaplayacak.

## Matematiksel Model

```
DailyCustomers = (ActiveShelves × shelfMultiplier) + (StoreLevel × levelMultiplier) + Random(minVariance, maxVariance)
DailyCustomers = Clamp(DailyCustomers, minCustomersPerDay, maxCustomersPerDay)
```

**Kapasite Sayımı**: Sahnedeki tüm `ShelfState` ve `DisplayTable` objeleri runtime'da `FindObjectsOfType` ile sayılır.

**Katsayılar**: Tüm katsayılar `[SerializeField]` — Inspector'dan ayarlanabilir.

## Proposed Changes

### CustomerManager

#### [MODIFY] [CustomerManager.cs](file:///d:/UnityFolders/Editor/Cargor/Assets/NewCss/CustomerSripts/CustomerManager.cs)

**1. Yeni SerializeField'ler ekle** (region: `DAILY CUSTOMER SETTINGS`):

```csharp
[Header("=== CAPACITY-BASED SPAWN SETTINGS ===")]
[SerializeField, Tooltip("Her aktif raf/masa başına müşteri katkısı")]
private float shelfMultiplier = 3f;

[SerializeField, Tooltip("Mağaza seviyesi başına müşteri katkısı")]
private float levelMultiplier = 2f;

[SerializeField, Tooltip("Mağaza seviyesi (ileride XP sistemine bağlanacak)")]
private int storeLevel = 1;

[SerializeField, Tooltip("Rastgele sapma alt sınırı")]
private int minVariance = -2;

[SerializeField, Tooltip("Rastgele sapma üst sınırı")]
private int maxVariance = 3;

[SerializeField, Tooltip("Günlük minimum müşteri sayısı")]
private int minCustomersPerDay = 1;

[SerializeField, Tooltip("Günlük maksimum müşteri sayısı (Soft Cap)")]
private int maxCustomersPerDay = 50;
```

**2. Kapasite sayım metodu ekle**:

```csharp
private int CountActiveInteractables()
{
    int count = 0;
    // Tüm ShelfState (raf) objeleri
    var shelves = FindObjectsOfType<ShelfState>();
    count += shelves.Length;
    // Tüm DisplayTable (masa) objeleri
    var tables = FindObjectsOfType<DisplayTable>();
    count += tables.Length;
    return count;
}
```

**3. `CalculateTodaysCustomerCount` metodunu değiştir** (satır 298-301):

```diff
 private int CalculateTodaysCustomerCount(int currentDay)
 {
-    return baseCustomersPerDay + ((currentDay - 1) * customerIncreasePerDay);
+    // Eski lineer formül (devre dışı):
+    // return baseCustomersPerDay + ((currentDay - 1) * customerIncreasePerDay);
+
+    int activeShelves = CountActiveInteractables();
+    float raw = (activeShelves * shelfMultiplier) + (storeLevel * levelMultiplier) + Random.Range(minVariance, maxVariance + 1);
+    int result = Mathf.RoundToInt(raw);
+    result = Mathf.Clamp(result, minCustomersPerDay, maxCustomersPerDay);
+
+    LogDebug($"Capacity calc: shelves={activeShelves}, level={storeLevel}, raw={raw:F1}, clamped={result}");
+    return result;
 }
```

**4. Eski field'leri yorum satırına al**:

`baseCustomersPerDay` ve `customerIncreasePerDay` field'lerini `[Obsolete]` veya yorum satırına al. `DifficultyManager`'ın bunlara yazdığı değer artık formüle girmiyor; bunun yerine `DifficultyManager` entegrasyonu ayrıca ele alınacak (aşağıya bak).

---

### DifficultyManager Entegrasyonu

#### [MODIFY] [DifficultyManager.cs](file:///d:/UnityFolders/Editor/Cargor/Assets/NewCss/GameState/DifficultyManager.cs)

`ApplyCustomerSettings()` içinde (satır 422-430):

```diff
 private void ApplyCustomerSettings()
 {
     var customerManager = FindObjectOfType<CustomerManager>();
     if (customerManager != null)
     {
-        customerManager.baseCustomersPerDay = ScaledCustomerCount;
-        LogDebug($"Customer count set to: {ScaledCustomerCount}");
+        // Yeni capacity-based sistem kendi hesaplamasını yapıyor.
+        // DifficultyManager artık sadece storeLevel çarpanını ayarlayabilir.
+        // customerManager.baseCustomersPerDay = ScaledCustomerCount;
+        LogDebug($"Customer system is now capacity-based. Player count: {_cachedPlayerCount}");
     }
```

> [!WARNING]
> Bu değişiklik `DifficultyManager`'ın `baseCustomersPerDay` üzerindeki etkisini devre dışı bırakır. Eğer oyuncu sayısının müşteri sayısını etkilemesini istiyorsan, `CustomerManager`'a bir `playerCountMultiplier` field'i ekleyip `DifficultyManager`'dan bu değeri set edebiliriz. **Bu konuda kararını bekliyorum.**

## 2 Olası Yaklaşım — Seçim Senin

### Opsiyon A: Saf Kapasite (DifficultyManager devre dışı)
Müşteri sayısı **yalnızca** raf/masa + mağaza seviyesine bağlı. Oyuncu sayısı müşteri sayısını etkilemez.

### Opsiyon B: Kapasite + Oyuncu Çarpanı (Önerilen ✅)
Formül: `(shelves × mult + level × mult + random) × playerMultiplier`
`DifficultyManager` bir `playerMultiplier` (örn: 1 oyuncu = 1.0, 2 oyuncu = 1.3, 3 oyuncu = 1.6...) sağlar. Bu şekilde hem kapasite hem de multiplayer dengelenir.

## Verification Plan

### Manuel Test (Unity Editor)
1. **Sahneyi aç**, `CustomerManager` Inspector'ında yeni field'leri gör
2. `shelfMultiplier`, `levelMultiplier`, `storeLevel` değerlerini değiştir
3. Play moduna geç, günlük müşteri sayısını Console log'larından kontrol et: `[CustomerManager] Capacity calc: shelves=X, level=Y, raw=Z, clamped=W`
4. `maxCustomersPerDay = 10` yap, fazla raf ekle → sayının 10'da kilitlendiğini doğrula
5. Tüm rafları kaldır → `minCustomersPerDay` değerine düştüğünü doğrula
6. Birkaç gün geçir → her gün farklı random variance geldiğini doğrula
