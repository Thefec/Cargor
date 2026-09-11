using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

/// <summary>
/// Tutorial talimat metinlerini (10 adım + skip-hint + tamamlanma mesajı) oyunun
/// genel 17-dilli StringTable sistemine ekler. Geçici Editor aracı — tek kerelik
/// kurulum için yazıldı. İDEMPOTENT: StringTable.AddEntry var olan key'i günceller,
/// çoğaltmaz (SharedTableData.AddKey de var olan key'i yeniden kullanır).
///
/// Metinlerde tuş belirten yerler "[{0}] tuşuna bas" kalıbını kullanır — TutorialManager
/// bunu InputBindingManager.GetBindingDisplayName(Interact) ile dolduracak. Köşeli parantez
/// kalıbı bilinçli seçildi: dinamik tuş adına Türkçe/diğer dillerdeki hal eki (-'ye/-'ya vb.)
/// uygulanamayacağından (rebind sonrası "A'ye bas" gibi hatalı çekim riski), zaten
/// skipHintMessageTR/EN'nin kullandığı "[SPACE] tuşuna bas" kalıbıyla tutarlı hale getirildi.
/// </summary>
public static class TutorialLocalizationSetup
{
    private const string TablesFolder = "Assets/LocalSettings/Tables";
    private const string SharedDataPath = TablesFolder + "/StringTable Shared Data.asset";

    private static readonly string[] LocaleCodes =
    {
        "tr", "en", "de", "fr", "es", "pt", "it", "nl", "pl", "cs", "sk", "hr", "sl", "hu", "lv", "lt", "et"
    };

    private struct Entry
    {
        public string Key;
        public string[] Values; // LocaleCodes ile aynı sırada

        public Entry(string key, params string[] values)
        {
            Key = key;
            Values = values;
        }
    }

    [MenuItem("Tools/Cargor/Localization/Add Tutorial Keys")]
    public static void Run()
    {
        var sharedData = AssetDatabase.LoadAssetAtPath<SharedTableData>(SharedDataPath);
        if (sharedData == null)
        {
            Debug.LogError("[TutorialLocalizationSetup] SharedTableData bulunamadı: " + SharedDataPath);
            return;
        }

        var tables = new Dictionary<string, StringTable>();
        foreach (var code in LocaleCodes)
        {
            string path = $"{TablesFolder}/StringTable_{code}.asset";
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
            if (table == null)
            {
                Debug.LogError($"[TutorialLocalizationSetup] StringTable bulunamadı: {path}");
                return;
            }
            tables[code] = table;
        }

        var entries = BuildEntries();
        int addedOrUpdated = 0;

        foreach (var entry in entries)
        {
            for (int i = 0; i < LocaleCodes.Length; i++)
            {
                string code = LocaleCodes[i];
                string value = entry.Values[i];
                tables[code].AddEntry(entry.Key, value);
                EditorUtility.SetDirty(tables[code]);
            }
            addedOrUpdated++;
        }

        EditorUtility.SetDirty(sharedData);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialLocalizationSetup] {addedOrUpdated} key x {LocaleCodes.Length} dil işlendi ve kaydedildi.");
    }

    private static List<Entry> BuildEntries()
    {
        return new List<Entry>
        {
            new Entry("TutorialWelcome",
                "Cargor'a hoş geldin! Eşyaları müşterilerden alıp araçlara teslim edeceksin. Devam etmek için [SPACE] tuşuna bas.",
                "Welcome to Cargor! You'll pick up items from customers and deliver them to trucks. Press [SPACE] to continue.",
                "Willkommen bei Cargor! Du nimmst Bestellungen von Kunden entgegen und lieferst sie an die Lkws. Drücke [SPACE], um fortzufahren.",
                "Bienvenue chez Cargor ! Tu récupéreras des articles auprès des clients et les livreras aux camions. Appuie sur [SPACE] pour continuer.",
                "¡Bienvenido a Cargor! Recogerás pedidos de los clientes y los entregarás en los camiones. Pulsa [SPACE] para continuar.",
                "Bem-vindo ao Cargor! Vais recolher pedidos dos clientes e entregá-los nos camiões. Pressiona [SPACE] para continuar.",
                "Benvenuto a Cargor! Ritirerai gli ordini dai clienti e li consegnerai ai camion. Premi [SPACE] per continuare.",
                "Welkom bij Cargor! Je haalt bestellingen op bij klanten en levert ze af bij de vrachtwagens. Druk op [SPACE] om door te gaan.",
                "Witaj w Cargor! Będziesz odbierać zamówienia od klientów i dostarczać je do ciężarówek. Naciśnij [SPACE], aby kontynuować.",
                "Vítej v Cargor! Budeš vyzvedávat objednávky od zákazníků a doručovat je do náklaďáků. Stiskni [SPACE] pro pokračování.",
                "Vitaj v Cargor! Budeš vyzdvihovať objednávky od zákazníkov a doručovať ich do nákladiakov. Stlač [SPACE] pre pokračovanie.",
                "Dobrodošao u Cargor! Preuzimat ćeš narudžbe od kupaca i dostavljati ih kamionima. Pritisni [SPACE] za nastavak.",
                "Dobrodošel v Cargor! Naročila boš prevzemal od strank in jih dostavljal do tovornjakov. Pritisni [SPACE] za nadaljevanje.",
                "Üdvözlünk a Cargorban! Rendeléseket veszel át a vásárlóktól, és kiszállítod őket a teherautókhoz. Nyomd meg a [SPACE] gombot a folytatáshoz.",
                "Laipni lūdzam Cargor! Tu ņemsi pasūtījumus no klientiem un piegādāsi tos kravas automašīnām. Nospied [SPACE], lai turpinātu.",
                "Sveikas atvykęs į Cargor! Priimsi užsakymus iš klientų ir pristatysi juos į sunkvežimius. Spausk [SPACE], kad tęstum.",
                "Tere tulemast Cargorisse! Võtad klientidelt tellimusi vastu ja toimetad need veokitesse. Vajuta jätkamiseks [SPACE]."
            ),
            new Entry("TutorialTalkAndGetItem",
                "Müşteriye gidip [{0}] tuşuna basarak siparişini al, sonra masanın önünde tekrar [{0}] tuşuna basarak ürünü al.",
                "Walk up to the customer and press [{0}], then press [{0}] again at the table to pick up the item.",
                "Geh zum Kunden und drücke [{0}], dann drücke [{0}] erneut am Tisch, um den Artikel aufzunehmen.",
                "Va voir le client et appuie sur [{0}], puis appuie à nouveau sur [{0}] devant la table pour récupérer l'article.",
                "Acércate al cliente y pulsa [{0}], luego pulsa [{0}] otra vez frente a la mesa para recoger el artículo.",
                "Vai até o cliente e pressiona [{0}], depois pressiona [{0}] novamente junto à mesa para pegar o item.",
                "Avvicinati al cliente e premi [{0}], poi premi di nuovo [{0}] davanti al tavolo per ritirare l'oggetto.",
                "Loop naar de klant en druk op [{0}], druk daarna opnieuw op [{0}] bij de tafel om het product op te pakken.",
                "Podejdź do klienta i naciśnij [{0}], a następnie ponownie naciśnij [{0}] przy stole, aby odebrać przedmiot.",
                "Jdi za zákazníkem a stiskni [{0}], poté znovu stiskni [{0}] u stolu, abys vyzvedl předmět.",
                "Choď k zákazníkovi a stlač [{0}], potom znova stlač [{0}] pri stole, aby si vyzdvihol predmet.",
                "Priđi kupcu i pritisni [{0}], zatim ponovno pritisni [{0}] kod stola da preuzmeš predmet.",
                "Pojdi do stranke in pritisni [{0}], nato znova pritisni [{0}] pri mizi, da prevzameš izdelek.",
                "Menj oda a vásárlóhoz, és nyomd meg a [{0}] gombot, majd nyomd meg újra a [{0}] gombot az asztalnál, hogy felvedd a terméket.",
                "Ej pie klienta un nospied [{0}], tad vēlreiz nospied [{0}] pie galda, lai paņemtu preci.",
                "Prieik prie kliento ir spausk [{0}], tada dar kartą spausk [{0}] prie stalo, kad paimtum prekę.",
                "Mine kliendi juurde ja vajuta [{0}], seejärel vajuta laua juures uuesti [{0}], et toode kätte saada."
            ),
            new Entry("TutorialPlaceOnPackingTable",
                "Ürünü paketleme masasına götür ve önünde [{0}] tuşuna basarak bırak.",
                "Carry the item to the packing table and press [{0}] to place it down.",
                "Bring den Artikel zum Packtisch und drücke [{0}], um ihn abzulegen.",
                "Apporte l'article à la table d'emballage et appuie sur [{0}] pour le déposer.",
                "Lleva el artículo a la mesa de embalaje y pulsa [{0}] para dejarlo.",
                "Leva o item até a mesa de embalagem e pressiona [{0}] para o colocar.",
                "Porta l'oggetto al tavolo d'imballaggio e premi [{0}] per posarlo.",
                "Breng het product naar de inpaktafel en druk op [{0}] om het neer te zetten.",
                "Zanieś przedmiot do stołu pakowania i naciśnij [{0}], aby go położyć.",
                "Odnes předmět na balicí stůl a stiskni [{0}], abys ho položil.",
                "Odnes predmet na baliaci stôl a stlač [{0}], aby si ho položil.",
                "Odnesi predmet na stol za pakiranje i pritisni [{0}] da ga odložiš.",
                "Odnesi izdelek na mizo za pakiranje in pritisni [{0}], da ga odložiš.",
                "Vidd a terméket a csomagoló asztalhoz, és nyomd meg a [{0}] gombot a lerakáshoz.",
                "Aiznes preci uz iepakošanas galdu un nospied [{0}], lai to noliktu.",
                "Nunešk prekę prie pakavimo stalo ir spausk [{0}], kad ją padėtum.",
                "Vii toode pakkimislauale ja vajuta [{0}], et see maha panna."
            ),
            new Entry("TutorialTakeBox",
                "Raftan uygun kutuyu almak için önünde [{0}] tuşuna bas.",
                "Press [{0}] at the shelf to take the right box.",
                "Drücke [{0}] am Regal, um die richtige Kiste zu nehmen.",
                "Appuie sur [{0}] devant l'étagère pour prendre la bonne boîte.",
                "Pulsa [{0}] frente a la estantería para coger la caja correcta.",
                "Pressiona [{0}] junto à prateleira para pegar a caixa certa.",
                "Premi [{0}] davanti allo scaffale per prendere la scatola giusta.",
                "Druk op [{0}] bij het schap om de juiste doos te pakken.",
                "Naciśnij [{0}] przy regale, aby wziąć właściwe pudełko.",
                "Stiskni [{0}] u regálu, abys vzal správnou krabici.",
                "Stlač [{0}] pri regáli, aby si vzal správnu krabicu.",
                "Pritisni [{0}] kod police da uzmeš pravu kutiju.",
                "Pritisni [{0}] pri polici, da vzameš pravo škatlo.",
                "Nyomd meg a [{0}] gombot a polcnál, hogy elvedd a megfelelő dobozt.",
                "Nospied [{0}] pie plaukta, lai paņemtu pareizo kasti.",
                "Spausk [{0}] prie lentynos, kad paimtum tinkamą dėžę.",
                "Vajuta riiuli juures [{0}], et võtta õige kast."
            ),
            new Entry("TutorialPackItem",
                "Kutuyu paketleme masasına götür, [{0}] tuşuna bas — ürün otomatik paketlenecek.",
                "Bring the box to the packing table and press [{0}] — the item will be packed automatically.",
                "Bring die Kiste zum Packtisch und drücke [{0}] — der Artikel wird automatisch verpackt.",
                "Apporte la boîte à la table d'emballage, appuie sur [{0}] — l'article sera emballé automatiquement.",
                "Lleva la caja a la mesa de embalaje, pulsa [{0}] — el artículo se empaquetará automáticamente.",
                "Leva a caixa até a mesa de embalagem, pressiona [{0}] — o item será embalado automaticamente.",
                "Porta la scatola al tavolo d'imballaggio, premi [{0}] — l'oggetto verrà imballato automaticamente.",
                "Breng de doos naar de inpaktafel, druk op [{0}] — het product wordt automatisch ingepakt.",
                "Zanieś pudełko do stołu pakowania, naciśnij [{0}] — przedmiot zostanie automatycznie zapakowany.",
                "Odnes krabici na balicí stůl, stiskni [{0}] — předmět se automaticky zabalí.",
                "Odnes krabicu na baliaci stôl, stlač [{0}] — predmet sa automaticky zabalí.",
                "Odnesi kutiju na stol za pakiranje, pritisni [{0}] — predmet će se automatski zapakirati.",
                "Odnesi škatlo na mizo za pakiranje, pritisni [{0}] — izdelek bo samodejno zapakiran.",
                "Vidd a dobozt a csomagoló asztalhoz, nyomd meg a [{0}] gombot — a termék automatikusan becsomagolódik.",
                "Aiznes kasti uz iepakošanas galdu, nospied [{0}] — prece tiks iepakota automātiski.",
                "Nunešk dėžę prie pakavimo stalo, spausk [{0}] — prekė bus supakuota automatiškai.",
                "Vii kast pakkimislauale, vajuta [{0}] — toode pakitakse automaatselt."
            ),
            new Entry("TutorialTakePackedBox",
                "Paketlenmiş kutuyu almak için masanın önünde tekrar [{0}] tuşuna bas.",
                "Press [{0}] again at the table to pick up the packed box.",
                "Drücke [{0}] erneut am Tisch, um die verpackte Kiste aufzunehmen.",
                "Appuie à nouveau sur [{0}] devant la table pour récupérer la boîte emballée.",
                "Pulsa [{0}] otra vez frente a la mesa para recoger la caja empaquetada.",
                "Pressiona [{0}] novamente junto à mesa para pegar a caixa embalada.",
                "Premi di nuovo [{0}] davanti al tavolo per ritirare la scatola imballata.",
                "Druk opnieuw op [{0}] bij de tafel om de ingepakte doos op te pakken.",
                "Naciśnij ponownie [{0}] przy stole, aby odebrać zapakowane pudełko.",
                "Znovu stiskni [{0}] u stolu, abys vyzvedl zabalenou krabici.",
                "Znova stlač [{0}] pri stole, aby si vyzdvihol zabalenú krabicu.",
                "Ponovno pritisni [{0}] kod stola da preuzmeš zapakiranu kutiju.",
                "Znova pritisni [{0}] pri mizi, da prevzameš zapakirano škatlo.",
                "Nyomd meg újra a [{0}] gombot az asztalnál, hogy felvedd a becsomagolt dobozt.",
                "Vēlreiz nospied [{0}] pie galda, lai paņemtu iepakoto kasti.",
                "Dar kartą spausk [{0}] prie stalo, kad paimtum supakuotą dėžę.",
                "Vajuta laua juures uuesti [{0}], et võtta pakitud kast."
            ),
            new Entry("TutorialPlaceOnShelf",
                "Kutuyu rafa yerleştirmek için [{0}] tuşuna bas.",
                "Press [{0}] to place the box on the shelf.",
                "Drücke [{0}], um die Kiste ins Regal zu stellen.",
                "Appuie sur [{0}] pour poser la boîte sur l'étagère.",
                "Pulsa [{0}] para colocar la caja en la estantería.",
                "Pressiona [{0}] para colocar a caixa na prateleira.",
                "Premi [{0}] per posizionare la scatola sullo scaffale.",
                "Druk op [{0}] om de doos op het schap te zetten.",
                "Naciśnij [{0}], aby umieścić pudełko na regale.",
                "Stiskni [{0}], abys umístil krabici na regál.",
                "Stlač [{0}], aby si umiestnil krabicu na regál.",
                "Pritisni [{0}] da staviš kutiju na policu.",
                "Pritisni [{0}], da postaviš škatlo na polico.",
                "Nyomd meg a [{0}] gombot, hogy a polcra tedd a dobozt.",
                "Nospied [{0}], lai noliktu kasti uz plaukta.",
                "Spausk [{0}], kad padėtum dėžę ant lentynos.",
                "Vajuta [{0}], et panna kast riiulile."
            ),
            new Entry("TutorialWaitForTruck",
                "Araç geliyor, biraz bekle...",
                "The truck is arriving, hold on...",
                "Der Lkw kommt, warte kurz...",
                "Le camion arrive, patiente un instant...",
                "El camión está llegando, espera un momento...",
                "O camião está a chegar, aguarda um pouco...",
                "Il camion sta arrivando, attendi un momento...",
                "De vrachtwagen komt eraan, wacht even...",
                "Ciężarówka nadjeżdża, poczekaj chwilę...",
                "Náklaďák přijíždí, chvíli počkej...",
                "Nákladiak prichádza, chvíľu počkaj...",
                "Kamion dolazi, pričekaj malo...",
                "Tovornjak prihaja, počakaj trenutek...",
                "Jön a teherautó, várj egy kicsit...",
                "Kravas automašīna tuvojas, uzgaidi mazliet...",
                "Sunkvežimis atvyksta, palauk truputį...",
                "Veok saabub, oota veidi..."
            ),
            new Entry("TutorialTakePackageAgain",
                "Paketi tekrar almak için rafın önünde [{0}] tuşuna bas.",
                "Press [{0}] again at the shelf to take the package.",
                "Drücke [{0}] erneut am Regal, um das Paket zu nehmen.",
                "Appuie à nouveau sur [{0}] devant l'étagère pour prendre le colis.",
                "Pulsa [{0}] otra vez frente a la estantería para coger el paquete.",
                "Pressiona [{0}] novamente junto à prateleira para pegar o pacote.",
                "Premi di nuovo [{0}] davanti allo scaffale per prendere il pacco.",
                "Druk opnieuw op [{0}] bij het schap om het pakket te pakken.",
                "Naciśnij ponownie [{0}] przy regale, aby wziąć paczkę.",
                "Znovu stiskni [{0}] u regálu, abys vzal balík.",
                "Znova stlač [{0}] pri regáli, aby si vzal balík.",
                "Ponovno pritisni [{0}] kod police da uzmeš paket.",
                "Znova pritisni [{0}] pri polici, da vzameš paket.",
                "Nyomd meg újra a [{0}] gombot a polcnál, hogy elvedd a csomagot.",
                "Vēlreiz nospied [{0}] pie plaukta, lai paņemtu paku.",
                "Dar kartą spausk [{0}] prie lentynos, kad paimtum siuntinį.",
                "Vajuta riiuli juures uuesti [{0}], et võtta pakk."
            ),
            new Entry("TutorialDeliver",
                "Paketi tırın arkasına götür ve fırlat.",
                "Carry the package to the back of the truck and throw it in.",
                "Bring das Paket zur Rückseite des Lkws und wirf es hinein.",
                "Apporte le colis à l'arrière du camion et jette-le dedans.",
                "Lleva el paquete a la parte trasera del camión y lánzalo dentro.",
                "Leva o pacote até à traseira do camião e atira-o para dentro.",
                "Porta il pacco sul retro del camion e lancialo dentro.",
                "Breng het pakket naar de achterkant van de vrachtwagen en gooi het erin.",
                "Zanieś paczkę na tył ciężarówki i wrzuć ją do środka.",
                "Odnes balík k zadní části náklaďáku a hoď ho dovnitř.",
                "Odnes balík k zadnej časti nákladiaku a hoď ho dovnútra.",
                "Odnesi paket do stražnjeg dijela kamiona i baci ga unutra.",
                "Odnesi paket do zadnjega dela tovornjaka in ga vrzi noter.",
                "Vidd a csomagot a teherautó hátuljához, és dobd be.",
                "Aiznes paku uz kravas automašīnas aizmuguri un iemet to iekšā.",
                "Nunešk siuntinį į sunkvežimio galą ir įmesk jį vidun.",
                "Vii pakk veoki taha ja viska see sisse."
            ),
            new Entry("TutorialSkipHint",
                "Geçmek için [SPACE] tuşuna basın",
                "Press [SPACE] to skip",
                "Drücke [SPACE], um zu überspringen",
                "Appuie sur [SPACE] pour passer",
                "Pulsa [SPACE] para saltar",
                "Pressiona [SPACE] para saltar",
                "Premi [SPACE] per saltare",
                "Druk op [SPACE] om over te slaan",
                "Naciśnij [SPACE], aby pominąć",
                "Stiskni [SPACE] pro přeskočení",
                "Stlač [SPACE] pre preskočenie",
                "Pritisni [SPACE] za preskakanje",
                "Pritisni [SPACE], da preskočiš",
                "Nyomd meg a [SPACE] gombot a kihagyáshoz",
                "Nospied [SPACE], lai izlaistu",
                "Spausk [SPACE], kad praleistum",
                "Vahelejätmiseks vajuta [SPACE]"
            ),
            new Entry("TutorialCompleted",
                "Tutorial tamamlandı!",
                "Tutorial completed!",
                "Tutorial abgeschlossen!",
                "Tutoriel terminé !",
                "¡Tutorial completado!",
                "Tutorial concluído!",
                "Tutorial completato!",
                "Tutorial voltooid!",
                "Samouczek ukończony!",
                "Tutoriál dokončen!",
                "Tutoriál dokončený!",
                "Tutorial dovršen!",
                "Tutorial zaključen!",
                "Tutorial befejezve!",
                "Tutoriāls pabeigts!",
                "Tutorialas baigtas!",
                "Õpetus lõpetatud!"
            ),
        };
    }
}
