using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NewCss;

/// <summary>
/// Tutorial.unity sahnesine 5 fonksiyonel gameplay objesi ekler (Customer, DisplayTable,
/// paketleme Table, basit-cube Shelf) ve script referanslarını bağlar. Geçici Editor
/// aracı — plans/tutorial-rewrite.md Adım 6 kapsamında bir kerelik kurulum için yazıldı.
/// İDEMPOTENT: isimle bulunan mevcut objeler yeniden kullanılır (pozisyonları korunur),
/// tekrar çalıştırmak obje çoğaltmaz — sadece eksik component/alan/wiring'i tamamlar.
/// Tekrar çalıştırmak isterse kullanıcı Tools/Cargor/Tutorial/Setup Gameplay Objects
/// menüsünden veya -executeMethod ile çağırabilir.
/// </summary>
public static class TutorialSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Tutorial.unity";

    private const string CustomerPrefabPath =
        "Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Customer.prefab";
    private const string DisplayTablePrefabPath =
        "Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Tables/Cube.006 (8).prefab";
    private const string PackingTablePrefabPath =
        "Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Tables/Cube.012 (3).prefab";

    private const string PackingTableID = "301";
    private const string InitialStockItemDataPath = "Assets/Resources/Items/RedBox.asset";

    [MenuItem("Tools/Cargor/Tutorial/Setup Gameplay Objects")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[TutorialSceneSetup] Sahne açılamadı: " + ScenePath);
            return;
        }

        var spawnerGO = GameObject.Find("Spawner");
        var truckGO = GameObject.Find("TutorialTruck");
        if (spawnerGO == null || truckGO == null)
        {
            Debug.LogError("[TutorialSceneSetup] Spawner veya TutorialTruck sahnede bulunamadı, iptal.");
            return;
        }

        Vector3 start = spawnerGO.transform.position;
        Vector3 end = truckGO.transform.position;
        Debug.Log($"[TutorialSceneSetup] Spawner world pos: {start}, TutorialTruck world pos: {end}");

        Vector3 Along(float t) => Vector3.Lerp(start, end, t);

        // --- 1. Customer (~%20) -----------------------------------------------------
        var customerGO = GameObject.Find("TutorialCustomer");
        if (customerGO == null)
        {
            var customerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            if (customerPrefab == null)
            {
                Debug.LogError("[TutorialSceneSetup] Customer prefab bulunamadı: " + CustomerPrefabPath);
                return;
            }
            customerGO = (GameObject)PrefabUtility.InstantiatePrefab(customerPrefab, scene);
            customerGO.name = "TutorialCustomer";
            customerGO.transform.position = Along(0.20f);
        }
        else
        {
            Debug.Log("[TutorialSceneSetup] TutorialCustomer zaten var, yeniden kullanılıyor (pozisyon korunuyor).");
        }

        // --- 2. DisplayTable / sipariş masası (~%25) --------------------------------
        var displayTableGO = GameObject.Find("TutorialDisplayTable");
        if (displayTableGO == null)
        {
            var displayTablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DisplayTablePrefabPath);
            if (displayTablePrefab == null)
            {
                Debug.LogError("[TutorialSceneSetup] DisplayTable prefab bulunamadı: " + DisplayTablePrefabPath);
                return;
            }
            displayTableGO = (GameObject)PrefabUtility.InstantiatePrefab(displayTablePrefab, scene);
            displayTableGO.name = "TutorialDisplayTable";
            displayTableGO.transform.position = Along(0.25f);
        }
        else
        {
            Debug.Log("[TutorialSceneSetup] TutorialDisplayTable zaten var, yeniden kullanılıyor (pozisyon korunuyor).");
        }

        // --- 3. Table / paketleme masası (~%45) -------------------------------------
        var packingTableGO = GameObject.Find("TutorialPackingTable");
        if (packingTableGO == null)
        {
            var packingTablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackingTablePrefabPath);
            if (packingTablePrefab == null)
            {
                Debug.LogError("[TutorialSceneSetup] Packing table prefab bulunamadı: " + PackingTablePrefabPath);
                return;
            }
            packingTableGO = (GameObject)PrefabUtility.InstantiatePrefab(packingTablePrefab, scene);
            packingTableGO.name = "TutorialPackingTable";
            packingTableGO.transform.position = Along(0.45f);
        }
        else
        {
            Debug.Log("[TutorialSceneSetup] TutorialPackingTable zaten var, yeniden kullanılıyor (pozisyon korunuyor).");
        }

        // --- 4. Shelf (~%65) - basit fonksiyonel cube -------------------------------
        var shelfGO = GameObject.Find("TutorialShelf");
        if (shelfGO == null)
        {
            shelfGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelfGO.name = "TutorialShelf";
            shelfGO.transform.position = Along(0.65f);
            shelfGO.transform.localScale = new Vector3(1.5f, 1.5f, 0.6f);
        }
        else
        {
            Debug.Log("[TutorialSceneSetup] TutorialShelf zaten var, yeniden kullanılıyor (pozisyon korunuyor).");
        }

        if (shelfGO.GetComponent<NetworkObject>() == null)
        {
            shelfGO.AddComponent<NetworkObject>();
        }

        var shelfState = shelfGO.GetComponent<TutorialShelfState>();
        if (shelfState == null)
        {
            shelfState = shelfGO.AddComponent<TutorialShelfState>();
        }

        var shelfSO = new SerializedObject(shelfState);
        var shelfSlotsProp = shelfSO.FindProperty("shelfSlots");

        bool shelfNeedsSlotFill = shelfSlotsProp.arraySize == 0;
        for (int i = 0; i < shelfSlotsProp.arraySize; i++)
        {
            if (shelfSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                shelfNeedsSlotFill = true;
            }
        }

        if (shelfNeedsSlotFill)
        {
            const int shelfSlotCount = 3;
            shelfSlotsProp.arraySize = shelfSlotCount;
            for (int i = 0; i < shelfSlotCount; i++)
            {
                var existing = shelfSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (existing != null) continue;

                var slotGO = new GameObject($"ShelfSlot_{i}");
                slotGO.transform.SetParent(shelfGO.transform, false);
                slotGO.transform.localPosition = new Vector3((i - 1) * 0.45f, 0.6f, 0f);
                shelfSlotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotGO.transform;
            }
            Debug.Log("[TutorialSceneSetup] TutorialShelf.shelfSlots dolduruldu.");
        }
        else
        {
            Debug.Log("[TutorialSceneSetup] TutorialShelf.shelfSlots zaten doluydu, dokunulmadı.");
        }

        shelfSO.FindProperty("acceptedBoxType").enumValueIndex = (int)BoxInfo.BoxType.Red;
        shelfSO.FindProperty("requireSpecificBoxType").boolValue = true;
        shelfSO.FindProperty("maxItemCount").intValue = 1;

        // Bulgu 2 düzeltmesi (kontrol round 1): raf boş doğuyordu, TakeFromShelf (adım 4) için
        // alacak kutu yoktu. RedBox ItemData → worldPrefab RedNGO.prefab (BoxInfo.boxType=Red,
        // isFull=false, NetworkObject zaten kurulu) - TutorialShelfState.SpawnInitialStock server'da
        // OnNetworkSpawn'da bunu kullanarak 1 ham kutu spawn edip slot 0'a kaydeder.
        var initialStockItemData = AssetDatabase.LoadAssetAtPath<ItemData>(InitialStockItemDataPath);
        if (initialStockItemData == null)
        {
            Debug.LogError("[TutorialSceneSetup] initialStockItemData bulunamadı: " + InitialStockItemDataPath +
                            " - TutorialShelf boş doğacak (TakeFromShelf adımı çalışmaz)!");
        }
        else
        {
            shelfSO.FindProperty("initialStockItemData").objectReferenceValue = initialStockItemData;
            shelfSO.FindProperty("initialStockCount").intValue = 1;
        }
        shelfSO.ApplyModifiedPropertiesWithoutUndo();

        // --- Referans bağlama: Customer -> DisplayTable -----------------------------
        var customerAI = customerGO.GetComponent<CustomerAI>();
        if (customerAI == null)
        {
            Debug.LogError("[TutorialSceneSetup] CustomerAI component'i Customer prefab'ında bulunamadı.");
        }
        else
        {
            var displayTable = displayTableGO.GetComponent<DisplayTable>();
            var customerSO = new SerializedObject(customerAI);
            customerSO.FindProperty("dropOffTable").objectReferenceValue = displayTable;
            customerSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- DisplayTable.slotPoints kontrol/doldurma -------------------------------
        var displayTableComp = displayTableGO.GetComponent<DisplayTable>();
        var displayTableSO = new SerializedObject(displayTableComp);
        var slotPointsProp = displayTableSO.FindProperty("slotPoints");

        bool needsSlotFill = slotPointsProp.arraySize == 0;
        for (int i = 0; i < slotPointsProp.arraySize; i++)
        {
            if (slotPointsProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                needsSlotFill = true;
            }
        }

        if (needsSlotFill)
        {
            int slotCount = Mathf.Max(slotPointsProp.arraySize, 2);
            slotPointsProp.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                var existing = slotPointsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (existing != null) continue;

                var slotGO = new GameObject($"DisplaySlot_{i}");
                slotGO.transform.SetParent(displayTableGO.transform, false);
                slotGO.transform.localPosition = new Vector3((i - (slotCount - 1) * 0.5f) * 0.4f, 0.9f, 0f);
                slotPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotGO.transform;
            }
            displayTableSO.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[TutorialSceneSetup] DisplayTable.slotPoints {slotCount} boş child ile dolduruldu.");
        }
        else
        {
            Debug.Log("[TutorialSceneSetup] DisplayTable.slotPoints prefab'da zaten doluydu, dokunulmadı.");
        }

        // --- Table.tableID benzersizlik ata -----------------------------------------
        var packingTable = packingTableGO.GetComponent<Table>();
        if (packingTable == null)
        {
            Debug.LogError("[TutorialSceneSetup] Table component'i packing table prefab'ında bulunamadı.");
        }
        else
        {
            var tableSO = new SerializedObject(packingTable);
            tableSO.FindProperty("tableID").stringValue = PackingTableID;
            tableSO.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Bulgu 1 düzeltmesi (kontrol round 1): TutorialManager.tutorialCustomer / --------
        // --- tutorialDropOffTable wiring - CustomerManager sahnede yok, CustomerAI hiçbir ----
        // --- zaman Service state'ine geçmiyordu. TutorialManager.AssignTutorialCustomerService-
        // --- StationWhenReady() bu iki alanı runtime'da server'da kullanıp AssignServiceStation'ı
        // --- (CustomerAI.cs, zaten public/production API) doğrudan çağırıyor. ------------------
        var tutorialManagerGO = GameObject.Find("TutorialManager");
        if (tutorialManagerGO == null)
        {
            Debug.LogError("[TutorialSceneSetup] TutorialManager sahnede bulunamadı - tutorialCustomer/" +
                            "tutorialDropOffTable bağlanamadı (müşteri Service state'ine hiç geçmeyecek)!");
        }
        else
        {
            var tutorialManager = tutorialManagerGO.GetComponent<TutorialManager>();
            if (tutorialManager == null)
            {
                Debug.LogError("[TutorialSceneSetup] TutorialManager component'i bulunamadı.");
            }
            else if (customerAI == null)
            {
                Debug.LogError("[TutorialSceneSetup] customerAI null - tutorialCustomer bağlanamadı.");
            }
            else
            {
                var tmSO = new SerializedObject(tutorialManager);
                tmSO.FindProperty("tutorialCustomer").objectReferenceValue = customerAI;
                tmSO.FindProperty("tutorialDropOffTable").objectReferenceValue = displayTableGO.GetComponent<DisplayTable>();
                tmSO.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[TutorialSceneSetup] TutorialManager.tutorialCustomer/tutorialDropOffTable bağlandı.");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TutorialSceneSetup] Sahne kaydedildi: {saved}. " +
                  $"Customer={customerGO.name}@{customerGO.transform.position}, " +
                  $"DisplayTable={displayTableGO.name}@{displayTableGO.transform.position}, " +
                  $"PackingTable={packingTableGO.name}@{packingTableGO.transform.position} (tableID={PackingTableID}), " +
                  $"Shelf={shelfGO.name}@{shelfGO.transform.position}, " +
                  $"Truck(mevcut)={truckGO.name}@{truckGO.transform.position}");
    }

    /// <summary>
    /// plans/tutorial-rewrite.md'deki güncel 10 adımlık içerik taslağını (2026-09-09)
    /// TutorialManager.tutorialSteps'e yazar + her adımın objectToHighlight'ını ilgili
    /// sahne objesine bağlar. Sahneyi diskten yeniden açar (OpenScene, Single) — bu yüzden
    /// menüye tıklamadan önce Inspector'da kaydetmediğin elle-girilmiş adım varsa kaybolur
    /// (zaten tamamının üstüne yazılacaktı, sorun değil). Tekrar çalıştırmak güvenlidir:
    /// tutorialSteps her seferinde bu 10 adımla TAMAMEN değiştirilir (idempotent overwrite,
    /// birikmeli ekleme değil).
    /// </summary>
    [MenuItem("Tools/Cargor/Tutorial/Setup Tutorial Steps")]
    public static void SetupSteps()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[TutorialSceneSetup] Sahne açılamadı: " + ScenePath);
            return;
        }

        var tutorialManagerGO = GameObject.Find("TutorialManager");
        if (tutorialManagerGO == null)
        {
            Debug.LogError("[TutorialSceneSetup] TutorialManager sahnede bulunamadı.");
            return;
        }

        var tutorialManager = tutorialManagerGO.GetComponent<TutorialManager>();
        if (tutorialManager == null)
        {
            Debug.LogError("[TutorialSceneSetup] TutorialManager component'i bulunamadı.");
            return;
        }

        var customerGO = GameObject.Find("TutorialCustomer");
        var displayTableGO = GameObject.Find("TutorialDisplayTable");
        var packingTableGO = GameObject.Find("TutorialPackingTable");
        var shelfGO = GameObject.Find("TutorialShelf");
        var truckGO = GameObject.Find("TutorialTruck");

        var steps = new[]
        {
            new StepDef("Welcome", TutorialConditionType.PressKey,
                "Cargor'a hoş geldin! Eşyaları müşterilerden alıp araçlara teslim edeceksin. Devam etmek için [SPACE]'e bas.",
                "Welcome to Cargor! You'll pick up items from customers and deliver them to trucks. Press [SPACE] to continue.",
                requiredKey: KeyCode.Space),
            new StepDef("TalkAndGetItem", TutorialConditionType.TakeFromTable,
                "Müşteriye gidip E'ye basarak siparişini al, sonra masanın önünde tekrar E'ye basarak ürünü al.",
                "Walk up to the customer and press E, then press E again at the table to pick up the item.",
                highlight: customerGO),
            new StepDef("PlaceOnPackingTable", TutorialConditionType.PlaceOnTable,
                "Ürünü paketleme masasına götür ve önünde E'ye basarak bırak.",
                "Carry the item to the packing table and press E to place it down.",
                highlight: packingTableGO),
            new StepDef("TakeBox", TutorialConditionType.TakeFromShelf,
                "Raftan uygun kutuyu almak için önünde E'ye bas.",
                "Press E at the shelf to take the right box.",
                highlight: shelfGO, requiresBoxType: true, boxType: NetworkedShelf.BoxType.Red),
            new StepDef("PackItem", TutorialConditionType.PlaceOnTable,
                "Kutuyu paketleme masasına götür, E'ye bas — ürün otomatik paketlenecek.",
                "Bring the box to the packing table and press E — the item will be packed automatically.",
                highlight: packingTableGO),
            new StepDef("TakePackedBox", TutorialConditionType.TakeFromTable,
                "Paketlenmiş kutuyu almak için masanın önünde tekrar E'ye bas.",
                "Press E again at the table to pick up the packed box.",
                highlight: packingTableGO),
            new StepDef("PlaceOnShelf", TutorialConditionType.PlaceOnShelf,
                "Kutuyu rafa yerleştirmek için E'ye bas.",
                "Press E to place the box on the shelf.",
                highlight: shelfGO),
            new StepDef("WaitForTruck", TutorialConditionType.WaitForTime,
                "Araç geliyor, biraz bekle...",
                "The truck is arriving, hold on...",
                highlight: truckGO, waitDuration: 2.5f),
            new StepDef("TakePackageAgain", TutorialConditionType.TakeFromShelf,
                "Paketi tekrar almak için rafın önünde E'ye bas.",
                "Press E at the shelf again to take the package.",
                highlight: shelfGO, requiresBoxType: true, boxType: NetworkedShelf.BoxType.Red),
            new StepDef("Deliver", TutorialConditionType.DeliverToTruck,
                "Paketi tırın arkasına götür ve fırlat.",
                "Carry the package to the back of the truck and throw it in.",
                highlight: truckGO, requiredDeliveryCount: 1, requiresTruckBoxType: true, truckBoxType: BoxInfo.BoxType.Red),
        };

        var tmSO = new SerializedObject(tutorialManager);
        var stepsProp = tmSO.FindProperty("tutorialSteps");
        stepsProp.arraySize = steps.Length;

        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            var el = stepsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("stepName").stringValue = s.Name;
            el.FindPropertyRelative("stepIndex").intValue = i;
            el.FindPropertyRelative("instructionText").stringValue = s.TR;
            el.FindPropertyRelative("instructionTextEnglish").stringValue = s.EN;
            el.FindPropertyRelative("conditionType").enumValueIndex = (int)s.Condition;
            el.FindPropertyRelative("waitDuration").floatValue = s.WaitDuration;
            // Unity array büyürken yeni elemanları son elemandan kopyalar (fresh default değil) —
            // kullanılmayan alanları burada açıkça sıfırlamazsak önceki içerikten kalıntı kalır.
            el.FindPropertyRelative("requiresItemPickup").boolValue = false;
            el.FindPropertyRelative("requiredItemName").stringValue = "";
            el.FindPropertyRelative("triggerTag").stringValue = "";
            // KeyCode duz sirali bir enum degil (None=0, Tab=9, Space=32, A=97...) -
            // enumValueIndex tanim sirasindaki index'i ister, gercek int degerini degil.
            // Onceki calistirmada Space (32) enumValueIndex olarak yazilinca Alpha9'a (57)
            // denk gelmisti - requiredKey icin dogru yontem intValue (ham int) atamak.
            el.FindPropertyRelative("requiredKey").intValue = (int)s.RequiredKey;
            el.FindPropertyRelative("requiresSpecificBoxType").boolValue = s.RequiresBoxType;
            el.FindPropertyRelative("requiredBoxType").enumValueIndex = (int)s.BoxType;
            el.FindPropertyRelative("requiredDeliveryCount").intValue = s.RequiredDeliveryCount;
            el.FindPropertyRelative("requiresSpecificBoxTypeForTruck").boolValue = s.RequiresTruckBoxType;
            el.FindPropertyRelative("requiredTruckBoxType").enumValueIndex = (int)s.TruckBoxType;
            el.FindPropertyRelative("objectToHighlight").objectReferenceValue = s.Highlight;
        }

        tmSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TutorialSceneSetup] {steps.Length} tutorial adımı TutorialManager'a yazıldı. Kaydedildi: {saved}");
    }

    private struct StepDef
    {
        public string Name;
        public TutorialConditionType Condition;
        public string TR;
        public string EN;
        public GameObject Highlight;
        public KeyCode RequiredKey;
        public float WaitDuration;
        public bool RequiresBoxType;
        public NetworkedShelf.BoxType BoxType;
        public int RequiredDeliveryCount;
        public bool RequiresTruckBoxType;
        public BoxInfo.BoxType TruckBoxType;

        public StepDef(string name, TutorialConditionType condition, string tr, string en, GameObject highlight = null,
            KeyCode requiredKey = KeyCode.None, float waitDuration = 3f, bool requiresBoxType = false,
            NetworkedShelf.BoxType boxType = default, int requiredDeliveryCount = 1, bool requiresTruckBoxType = false,
            BoxInfo.BoxType truckBoxType = default)
        {
            Name = name;
            Condition = condition;
            TR = tr;
            EN = en;
            Highlight = highlight;
            RequiredKey = requiredKey;
            WaitDuration = waitDuration;
            RequiresBoxType = requiresBoxType;
            BoxType = boxType;
            RequiredDeliveryCount = requiredDeliveryCount;
            RequiresTruckBoxType = requiresTruckBoxType;
            TruckBoxType = truckBoxType;
        }
    }
}
