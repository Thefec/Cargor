using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// Ana görev yöneticisi - görev ataması, ilerleme takibi ve ödül/ceza dağıtımını yönetir
    /// Server-authoritative tasarım ile network senkronizasyonu sağlar
    /// </summary>
    public class QuestManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QuestManager]";
        private const int DAILY_QUEST_COUNT = 3;

        /// <summary>Quest asset'lerinin kanonik klasörü (Assets/Resources/&lt;bu&gt;).</summary>
        private const string QUEST_RESOURCE_FOLDER = "Quests";

        #endregion

        #region Singleton

        public static QuestManager Instance { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Yeni görevler atandığında tetiklenir
        /// </summary>
        public static event Action OnQuestsAssigned;

        /// <summary>
        /// Görev durumu değiştiğinde tetiklenir
        /// </summary>
        public static event Action<string, QuestStatus> OnQuestStatusChanged;

        /// <summary>
        /// Görev ilerlemesi güncellendiğinde tetiklenir
        /// </summary>
        public static event Action<string, int, int> OnQuestProgressUpdated;

        /// <summary>
        /// Görev kabul isteği server tarafından reddedildiğinde (günlük limit doldu) sadece
        /// isteği yapan client'ta tetiklenir. UI bu event ile "zaten kabul ettin" geri bildirimi verebilir.
        /// </summary>
        public static event Action OnAcceptRejectedLocal;

        #endregion

        #region Serialized Fields

        [Header("=== QUEST DATABASE ===")]
        [SerializeField, Tooltip("Tüm mevcut görevler")]
        private List<QuestData> allQuests = new List<QuestData>();

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Network Variables

        private NetworkList<QuestProgress> _dailyQuests;
        private readonly NetworkVariable<int> _currentQuestTier = new(0);

        /// <summary>
        /// Bugün bir görev kabul edilip edilmediğini client'lara görünür kılar (writePerm: Server, default).
        /// HasAcceptedQuestToday() ile tutarlı tutulur: yalnızca AcceptQuestInternal'in başarı yolunda true olur,
        /// yeni gün atamasında (AssignDailyQuests) false'a döner.
        /// </summary>
        private readonly NetworkVariable<bool> _hasAcceptedToday = new(false);

        /// <summary>
        /// Q6 fix: her günlük görev atamasında (AssignDailyQuests) artan sunucu-yetkili sayaç.
        /// Client, Accept isteğine RPC gönderirken o an bildiği generation değerini de
        /// ekler; server kendi değeriyle eşleşmiyorsa isteği reddeder. Böylece gün geçişi ile
        /// uçuştaki bir Accept RPC'si arasındaki mikro pencerede, eski günün slotIndex'i
        /// yanlışlıkla yeni günün quest'ine uygulanmaz.
        /// </summary>
        private readonly NetworkVariable<int> _questGeneration = new(0);

        #endregion

        #region Private Fields

        private Dictionary<string, QuestData> _questDatabase;
        private bool _isSubscribedToDayCycle;

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut görev tier'ı (UpgradePanel'den)
        /// </summary>
        public int CurrentQuestTier => _currentQuestTier.Value;

        /// <summary>
        /// Günlük görev sayısı
        /// </summary>
        public int DailyQuestCount => _dailyQuests?.Count ?? 0;

        /// <summary>
        /// Havuzda oynanabilir en az bir quest var mı? <see cref="UpgradePanel"/> bunu
        /// "Görev Kademesi" upgrade'ini draft'a sokup sokmayacağına karar verirken okur —
        /// quest'i olmayan bir sistemin kartı teklif havuzunu kirletmesin.
        /// </summary>
        public bool HasQuests => _questDatabase != null && _questDatabase.Count > 0;

        /// <summary>
        /// Bugün bir görev kabul edilip edilmediği (client-görünür, server-authoritative NV'den okunur).
        /// R2b UI bunu kullanarak "kabul et" butonunu devre dışı bırakabilir / durum gösterebilir.
        /// </summary>
        public bool HasAcceptedQuestTodayClient => _hasAcceptedToday.Value;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            InitializeNetworkList();
            BuildQuestDatabase();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            CleanupSingleton();
            UnsubscribeFromDayCycleEvents();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();
            SubscribeToDayCycleEvents();
            SubscribeToGameEvents();

            if (IsServer)
            {
                AssignDailyQuests();
            }

            Debug.Log($"{LOG_PREFIX} Spawned - IsServer: {IsServer}");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            UnsubscribeFromDayCycleEvents();
            UnsubscribeFromGameEvents();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} Duplicate instance detected, destroying.. .");
                Destroy(gameObject);
            }
        }

        private void CleanupSingleton()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeNetworkList()
        {
            _dailyQuests = new NetworkList<QuestProgress>();
        }

        /// <summary>
        /// <c>allQuests</c>'i normalize eder: null girişleri atar, ardından
        /// <c>Resources/Quests</c> altındaki tüm QuestData asset'lerinden listede olmayanları ekler.
        ///
        /// Gerekçe: 15 asset'i tek tek inspector'a sürüklemek gereksiz ve unutulmaya açık bir adım —
        /// klasör zaten quest'lerin tek kaynağı. Inspector'a elle eklenmiş asset'ler korunur ve
        /// sırada önce gelir; silinmiş asset'lerin geride bıraktığı null referanslar temizlenir
        /// (bu olmadan liste "boş değil ama hepsi null" durumuna düşebiliyordu).
        /// </summary>
        private void CollectQuestAssets()
        {
            var merged = new List<QuestData>();
            var seen = new HashSet<QuestData>();

            foreach (var quest in allQuests)
            {
                if (quest != null && seen.Add(quest)) merged.Add(quest);
            }

            int fromInspector = merged.Count;

            foreach (var quest in Resources.LoadAll<QuestData>(QUEST_RESOURCE_FOLDER))
            {
                if (quest != null && seen.Add(quest)) merged.Add(quest);
            }

            allQuests = merged;

            LogDebug($"Quest asset'leri toplandı: {fromInspector} inspector + " +
                     $"{merged.Count - fromInspector} Resources/{QUEST_RESOURCE_FOLDER} = {merged.Count}");
        }

        private void BuildQuestDatabase()
        {
            CollectQuestAssets();

            _questDatabase = new Dictionary<string, QuestData>();

            foreach (var quest in allQuests)
            {
                if (quest != null && !string.IsNullOrEmpty(quest.questId))
                {
                    // F12 fix: duplicate questId sessizce üzerine yazılıyordu - iki farklı asset aynı
                    // questId'yi taşırsa hangisinin kaybolduğu hiçbir yerde görünmüyordu.
                    if (_questDatabase.TryGetValue(quest.questId, out QuestData existingQuest))
                    {
                        Debug.LogWarning($"{LOG_PREFIX} Duplicate questId '{quest.questId}' detected! " +
                                          $"'{existingQuest.name}' üzerine '{quest.name}' yazılıyor.");
                    }

                    _questDatabase[quest.questId] = quest;
                }
            }

            LogDebug($"Quest database built: {_questDatabase.Count} quests");
        }

        private void SubscribeToNetworkEvents()
        {
            _dailyQuests.OnListChanged += HandleDailyQuestsChanged;
            _currentQuestTier.OnValueChanged += HandleQuestTierChanged;
            _hasAcceptedToday.OnValueChanged += HandleHasAcceptedTodayChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_dailyQuests != null)
            {
                _dailyQuests.OnListChanged -= HandleDailyQuestsChanged;
            }

            _currentQuestTier.OnValueChanged -= HandleQuestTierChanged;
            _hasAcceptedToday.OnValueChanged -= HandleHasAcceptedTodayChanged;
        }

        private void SubscribeToDayCycleEvents()
        {
            if (_isSubscribedToDayCycle) return;

            DayCycleManager.OnNewDay += HandleNewDay;
            _isSubscribedToDayCycle = true;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            if (!_isSubscribedToDayCycle) return;

            DayCycleManager.OnNewDay -= HandleNewDay;
            _isSubscribedToDayCycle = false;
        }

        private void SubscribeToGameEvents()
        {
            // Mevcut eventler
            QuestTracker.OnMinigameCompleted += HandleMinigameCompleted;
            QuestTracker.OnBoxPlacedOnShelf += HandleBoxPlacedOnShelf;
            QuestTracker.OnTruckCompleted += HandleTruckCompleted;
            QuestTracker.OnToyPacked += HandleToyPacked;

            // YENİ EVENTLER
            QuestTracker.OnPhoneAnswered += HandlePhoneAnswered;
            QuestTracker.OnPackagingMistake += HandlePackagingMistake;
            QuestTracker.OnSpecificColorTruckCompleted += HandleSpecificColorTruckCompleted;
        }

        private void UnsubscribeFromGameEvents()
        {
            QuestTracker.OnMinigameCompleted -= HandleMinigameCompleted;
            QuestTracker.OnBoxPlacedOnShelf -= HandleBoxPlacedOnShelf;
            QuestTracker.OnTruckCompleted -= HandleTruckCompleted;
            QuestTracker.OnToyPacked -= HandleToyPacked;

            // YENİ EVENTLER
            QuestTracker.OnPhoneAnswered -= HandlePhoneAnswered;
            QuestTracker.OnPackagingMistake -= HandlePackagingMistake;
            QuestTracker.OnSpecificColorTruckCompleted -= HandleSpecificColorTruckCompleted;
        }


        #endregion

        #region Event Handlers

        private void HandleDailyQuestsChanged(NetworkListEvent<QuestProgress> changeEvent)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<QuestProgress>.EventType.Add:
                case NetworkListEvent<QuestProgress>.EventType.Clear:
                    // Q5 fix: Add/Clear, AssignDailyQuests() içindeki toplu atamanın (1 Clear + N Add)
                    // parçası olarak burada ayrı ayrı tetiklenirdi (~5x). Tekil "görevler atandı"
                    // bildirimi artık sadece NotifyQuestsAssignedClientRpc() tarafından batch
                    // tamamlandıktan sonra TEK sefer yapılıyor; burada tekrar Invoke etmiyoruz.
                    break;

                case NetworkListEvent<QuestProgress>.EventType.Value:
                    var quest = changeEvent.Value;
                    OnQuestStatusChanged?.Invoke(quest.questId.ToString(), quest.status);
                    OnQuestProgressUpdated?.Invoke(quest.questId.ToString(), quest.currentProgress, quest.targetProgress);
                    break;
            }
        }

        private void HandleQuestTierChanged(int previousValue, int newValue)
        {
            LogDebug($"Quest tier changed: {previousValue} -> {newValue}");
        }

        /// <summary>
        /// Günlük kabul limiti (_hasAcceptedToday) değiştiğinde mevcut UI-refresh event'ini tetikler.
        /// Böylece bir slot kabul edildiğinde diğer slotlardaki accept butonları anında grileşir.
        /// </summary>
        private void HandleHasAcceptedTodayChanged(bool previousValue, bool newValue)
        {
            OnQuestsAssigned?.Invoke();
        }

        private void HandleNewDay()
        {
            if (!IsServer) return;

            LogDebug("New day - settling accepted quests and assigning new ones");

            try
            {
                // Kabul edilmiş görevleri kapat: tamamlananlar ödülünü, tamamlanamayanlar cezasını burada alır
                SettleAcceptedQuestsForDayEnd();

                // Assign new daily quests
                AssignDailyQuests();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} Error processing new day: {e.Message}");
            }
        }

        #endregion

        #region Quest Tracking Event Handlers

        private void HandleMinigameCompleted()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.CompleteMinigame, BoxInfo.BoxType.Red, 1);
        }

        private void HandleBoxPlacedOnShelf(BoxInfo.BoxType boxType)
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.PlaceBoxOnShelf, boxType, 1);
        }

        private void HandleTruckCompleted()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.CompleteTruck, BoxInfo.BoxType.Red, 1);
        }

        private void HandleToyPacked(BoxInfo.BoxType boxType)
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.PackToy, boxType, 1);
        }

        // YENİ EVENT HANDLERS
        private void HandlePhoneAnswered()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.AnswerPhone, BoxInfo.BoxType.Red, 1);
        }

        private void HandlePackagingMistake()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.MakePackagingMistake, BoxInfo.BoxType.Red, 1);
        }

        private void HandleSpecificColorTruckCompleted(BoxInfo.BoxType truckColor)
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.CompleteSpecificColorTruck, truckColor, 1);
        }

        #endregion

        #region Quest Assignment

        private void AssignDailyQuests()
        {
            if (!IsServer) return;

            // Q6 fix: yeni atama = yeni generation. Bu satırdan sonra gelen (eski generation'lı)
            // Accept istekleri AcceptQuestInternal tarafından reddedilir.
            _questGeneration.Value++;

            // Clear existing quests
            _dailyQuests.Clear();

            // Yeni gün -> günlük kabul limiti sıfırlanır
            _hasAcceptedToday.Value = false;

            // Get available quests based on tier
            var availableQuests = GetAvailableQuestsForTier();

            if (availableQuests.Count == 0)
            {
                LogDebug("No available quests for current tier!");
                return;
            }

            // D1 (Faz4 §B.9): her teklif FARKLI bir tier'dan gelsin - üst tier açılınca alt
            // tier'ların havuzu seyrelip Hard ödülü hiç masaya gelmesin diye tier başına en az
            // bir teklif garantiye alınır.
            var selectedQuests = SelectDailyQuestsStratified(_currentQuestTier.Value);

            foreach (var quest in selectedQuests)
            {
                // F9 fix: bu quest ataması için deterministik bir rewardSeed üret ve networked
                // QuestProgress'e ekle. Client GetQuestData() çağrısında aynı seed'le RerollSelection
                // yaparak server ile birebir aynı ödül/ceza seçimini görür (late-join dahil, NetworkList
                // replikasyonu ile otomatik kapsanır).
                int rewardSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                quest.RerollSelection(rewardSeed);

                int effectiveTarget = CalculateEffectiveTargetCount(quest);

                var progress = new QuestProgress(quest.questId, effectiveTarget, rewardSeed);
                _dailyQuests.Add(progress);

                LogDebug($"Assigned quest: {quest.questTitle} (Tier: {quest.tier}) - Target: {effectiveTarget} (base {quest.requirement.targetCount}) - Rewards: {quest.GetRewardsSummary()}, Penalties: {quest.GetPenaltiesSummary()}");
            }

            NotifyQuestsAssignedClientRpc();
        }

        private List<QuestData> GetAvailableQuestsForTier()
        {
            var available = new List<QuestData>();
            int maxTier = _currentQuestTier.Value;

            foreach (var quest in allQuests)
            {
                if (quest != null && (int)quest.tier <= maxTier)
                {
                    available.Add(quest);
                }
            }

            return available;
        }

        /// <summary>
        /// D1 (Faz4 §B.9): 3 günlük teklifin her biri FARKLI tier'dan seçilir (maxTier=Hard iken
        /// 1 Easy + 1 Medium + 1 Hard). Henüz üst tier'lar kilitliyse (maxTier &lt; Hard) kalan
        /// slotlar açık tier'ların havuzundan rastgele doldurulur - eski davranışla aynı sonuç.
        /// </summary>
        private List<QuestData> SelectDailyQuestsStratified(int maxTier)
        {
            var selected = new List<QuestData>();
            var usedIds = new HashSet<string>();

            int highestTier = Mathf.Min(maxTier, (int)QuestTier.Hard);

            for (int t = 0; t <= highestTier; t++)
            {
                var tierPool = new List<QuestData>();
                foreach (var quest in allQuests)
                {
                    if (quest != null && (int)quest.tier == t && !usedIds.Contains(quest.questId))
                    {
                        tierPool.Add(quest);
                    }
                }

                if (tierPool.Count == 0) continue;

                var pick = tierPool[UnityEngine.Random.Range(0, tierPool.Count)];
                selected.Add(pick);
                usedIds.Add(pick.questId);
            }

            if (selected.Count < DAILY_QUEST_COUNT)
            {
                var remainingPool = new List<QuestData>();
                foreach (var quest in allQuests)
                {
                    if (quest != null && (int)quest.tier <= maxTier && !usedIds.Contains(quest.questId))
                    {
                        remainingPool.Add(quest);
                    }
                }

                int need = DAILY_QUEST_COUNT - selected.Count;
                for (int i = 0; i < need && remainingPool.Count > 0; i++)
                {
                    int randomIndex = UnityEngine.Random.Range(0, remainingPool.Count);
                    var pick = remainingPool[randomIndex];
                    selected.Add(pick);
                    usedIds.Add(pick.questId);
                    remainingPool.RemoveAt(randomIndex);
                }
            }

            return selected;
        }

        /// <summary>
        /// D2 (Faz4 §B.9): görev hedefini oyuncu sayısına göre ölçekler.
        ///
        /// ⚠️ ŞU ANDA HİÇBİR CANLI GÖREV TİPİNDE ETKİLİ DEĞİL — dört fiilin dördü de muaf.
        /// Bilerek böyle: 2026-08-06 kontrol kapısı ÇİFTE ÖLÇEKLEME buldu. Mevcut 30 asset'in
        /// targetCount'ları (Hard 12/5, Medium 7/3, Easy 4/2) 2026-07-29 economist turunda ZATEN
        /// tüm P bantlarında ~%85 tamamlanma hedeflenerek kalibre edilmişti; D2 onların üstüne bir
        /// kez daha çarpınca sim'de renksiz raf/paket tamamlanma olasılığı 3P 0.76→0.13,
        /// 4P 0.87→0.13'e düşüyordu (bkz. .claude/agent-memory/economist/quest_d2_double_scaling_bug_2026-08-06.md).
        ///
        /// Muafiyet gerekçeleri — hepsinde arz zaten P ile doğal ölçekleniyor, hedefi de
        /// ölçeklemek çift sayım olur:
        ///   AnswerPhone       - telefon arzı P-flat.
        ///   CompleteTruck     - tır kargosu P ile zaten küçülüyor.
        ///   PlaceBoxOnShelf   - raflama hızı doğrudan oyuncu sayısıyla artıyor.
        ///   PackToy           - paketleme masası çekişmesi P ile zaten dengeleniyor.
        ///
        /// Mekanizma, arzı P ile ölçeklenMEYEN gelecekteki görev tipleri için duruyor. Yeni bir
        /// tip eklerken önce economist'e sor: arzı P'den bağımsızsa muafiyet listesine EKLEME.
        ///
        /// Ölçek vektörü DifficultyManager.UpgradeCostMultiplier ile aynı kaynaktan okunur
        /// ({1.00, 2.00, 2.95, 3.70}) - aynı sabiti iki yerde tanımlamamak için.
        /// </summary>
        private int CalculateEffectiveTargetCount(QuestData quest)
        {
            int baseTarget = quest.requirement != null ? quest.requirement.targetCount : 1;

            if (quest.questType == QuestType.AnswerPhone ||
                quest.questType == QuestType.CompleteTruck ||
                quest.questType == QuestType.PlaceBoxOnShelf ||
                quest.questType == QuestType.PackToy)
            {
                return baseTarget;
            }

            float scale = DifficultyManager.Instance != null ? DifficultyManager.Instance.UpgradeCostMultiplier : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(baseTarget * scale));
        }

        [ClientRpc]
        private void NotifyQuestsAssignedClientRpc()
        {
            OnQuestsAssigned?.Invoke();
        }

        #endregion

        #region Quest Progress

        private void UpdateQuestProgress(QuestType questType, BoxInfo.BoxType boxType, int amount)
        {
            if (!IsServer) return;

            // Renk filtreleri YALNIZ o rengi gerçekten taşıyan quest tipleri için anlamlıdır.
            // Eskiden her iki filtre de tipe bakmadan körlemesine uygulanıyordu; `HandleTruckCompleted`
            // sabit `Red` gönderdiği için renk-kilitli bir CompleteTruck quest'i SESSİZ SOFT-LOCK
            // oluyordu: oyuncu kabul eder, kırmızı olmayan her tır ilerlemeyi atlar, gün sonu ceza yer.
            // (2026-07-25 tespiti, plans/quest-redesign-2026-07-25.md §7.0-1.)
            bool boxTypeApplies = questType == QuestType.PlaceBoxOnShelf || questType == QuestType.PackToy;
            bool truckColorApplies = questType == QuestType.CompleteSpecificColorTruck;

            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var progress = _dailyQuests[i];

                // Skip if not active
                if (progress.status != QuestStatus.Active) continue;

                // Get quest data
                if (!_questDatabase.TryGetValue(progress.questId.ToString(), out QuestData questData)) continue;

                // Check quest type match
                if (questData.questType != questType) continue;

                // Check box type if required (PlaceBoxOnShelf, PackToy için)
                if (boxTypeApplies && questData.requirement.requireSpecificBoxType
                    && questData.requirement.requiredBoxType != boxType) continue;

                // Check truck color if required (CompleteSpecificColorTruck için)
                if (truckColorApplies && questData.requirement.requireSpecificTruckColor
                    && questData.requirement.requiredTruckColor != boxType) continue;

                // Update progress
                progress.currentProgress += amount;

                // Check if completed
                if (progress.IsCompleted)
                {
                    progress.status = QuestStatus.Completed;
                    LogDebug($"Quest completed: {questData.questTitle}");
                }

                _dailyQuests[i] = progress;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Görevi kabul eder
        /// </summary>
        public void AcceptQuest(int slotIndex)
        {
            // Q6 fix: RPC gönderilirken o an bilinen (senkronize) generation da eklenir.
            int knownGeneration = _questGeneration.Value;

            if (!IsServer)
            {
                AcceptQuestServerRpc(slotIndex, knownGeneration);
                return;
            }

            // Host durumu: server ve client aynı süreçte, istek sahibi host'un kendisi
            AcceptQuestInternal(slotIndex, NetworkManager.LocalClientId, knownGeneration);
        }

        /// <summary>
        /// Belirli slot'taki görev bilgisini döndürür
        /// </summary>
        public QuestData GetQuestData(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _dailyQuests.Count) return null;

            var progress = _dailyQuests[slotIndex];

            if (_questDatabase.TryGetValue(progress.questId.ToString(), out QuestData questData))
            {
                // F9 fix: her okumada server'ın ürettiği seed ile deterministik reroll -> server/client
                // birebir aynı seçim + gün değişince (yeni seed) otomatik tazelenir (eski _isInitialized
                // bayatlığı da böyle çözülür, çünkü artık lazy-init'e hiç güvenilmiyor).
                questData.RerollSelection(progress.rewardSeed);
                return questData;
            }

            return null;
        }

        /// <summary>
        /// Belirli slot'taki görev ilerlemesini döndürür
        /// </summary>
        public QuestProgress GetQuestProgress(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _dailyQuests.Count)
            {
                return default;
            }

            return _dailyQuests[slotIndex];
        }

        /// <summary>
        /// Quest tier'ını ayarlar (UpgradePanel tarafından çağrılır)
        /// </summary>
        public void SetQuestTier(int tier)
        {
            if (!IsServer)
            {
                SetQuestTierServerRpc(tier);
                return;
            }

            SetQuestTierInternal(tier);
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void AcceptQuestServerRpc(int slotIndex, int clientGeneration, ServerRpcParams rpcParams = default)
        {
            // SenderClientId ServerRpcParams'tan alınır - client tarafından spoof edilemez
            AcceptQuestInternal(slotIndex, rpcParams.Receive.SenderClientId, clientGeneration);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetQuestTierServerRpc(int tier)
        {
            SetQuestTierInternal(tier);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// F11 fix: SetQuestTierServerRpc (RequireOwnership = false) doğrulamasızdı - herhangi bir client
        /// tier'ı örn. 999 yapabiliyordu. Çağıran denetimi: tek çağıran UpgradePanel.ApplyQuestTierUpgrade
        /// (Assets/NewCss/UpgradeScripts/UpgradePanel.cs:718-725), ve o metod HandleUpgradeLevelsChanged
        /// üzerinden TÜM client'larda (host dahil) tetikleniyor - bkz. UpgradePanel.cs:729-731 yorum satırı:
        /// "PerkEffect metodları idempotent olmalı (level'dan mutlak değer hesaplar, += yapmaz)". Yani meşru
        /// bir client-context çağıran var; RPC'yi server-only yapıp silmek (G1-b) burada uygun değil.
        /// Bunun yerine: (1) geçerli aralığa clamp (QuestTier enum'unun tanımladığı 0..Hard aralığı - bu bir
        /// ekonomik değer değil, yapısal bir enum sınırı), (2) yalnız-artar kuralı (tier permanent upgrade,
        /// düşürme isteği spoof/eski istek sayılır ve sessizce yok sayılır).
        /// </summary>
        private void SetQuestTierInternal(int tier)
        {
            int clamped = Mathf.Clamp(tier, 0, (int)QuestTier.Hard);

            if (clamped <= _currentQuestTier.Value)
            {
                return;
            }

            _currentQuestTier.Value = clamped;
        }

        private void AcceptQuestInternal(int slotIndex, ulong requesterClientId, int clientGeneration)
        {
            // Q6 fix: gün geçişi ile yarışan eski generation'lı istek - sessizce değil LOG'lu reddedilir.
            if (clientGeneration != _questGeneration.Value)
            {
                LogDebug($"Accept reddedildi: generation uyuşmuyor (client={clientGeneration}, server={_questGeneration.Value}), slot={slotIndex}");
                return;
            }

            if (slotIndex < 0 || slotIndex >= _dailyQuests.Count) return;

            var progress = _dailyQuests[slotIndex];

            if (progress.status != QuestStatus.Available) return;

            // Günde sadece 1 görev kabul edilebilir kontrolü
            if (HasAcceptedQuestToday())
            {
                LogDebug("Already accepted a quest today - limit is 1 per day");
                NotifyAcceptRejectedClientRpc(BuildTargetedClientRpcParams(requesterClientId));
                return;
            }

            progress.status = QuestStatus.Active;
            _dailyQuests[slotIndex] = progress;
            _hasAcceptedToday.Value = true;

            LogDebug($"Quest accepted: {progress.questId}");
        }

        /// <summary>
        /// Yalnızca isteği yapan client'ı hedefleyen ClientRpcParams üretir (broadcast değil).
        /// </summary>
        private static ClientRpcParams BuildTargetedClientRpcParams(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
        }

        /// <summary>
        /// Günlük limit nedeniyle reddedilen kabul isteğini yalnızca istek sahibi client'a bildirir.
        /// </summary>
        [ClientRpc]
        private void NotifyAcceptRejectedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            OnAcceptRejectedLocal?.Invoke();
        }

        /// <summary>
        /// Bugün zaten bir görev kabul edilmiş mi kontrol eder
        /// </summary>
        private bool HasAcceptedQuestToday()
        {
            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var quest = _dailyQuests[i];
                // Active, Completed, Collected veya Failed durumunda olan görevler kabul edilmiş sayılır
                if (quest.status == QuestStatus.Active || 
                    quest.status == QuestStatus.Completed || 
                    quest.status == QuestStatus.Collected ||
                    quest.status == QuestStatus.Failed)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gün 16 settlement (Faz4 §B.9): oyun kazanılarak biterken DayCycleManager.NextDay()
        /// erken çıkış yapıp OnNewDay'i HİÇ tetiklemiyor (bkz. DayCycleManager.cs upcomingDay
        /// &gt;= MAX_DAYS dalı) - yani normalde HandleNewDay üzerinden çalışan
        /// SettleAcceptedQuestsForDayEnd() son günün kabul edilmiş görevleri için hiç çalışmıyordu.
        /// Sonuç: son gün alınan görev cezasız/ödülsüz bedava bir opsiyon oluyordu. DayCycleManager
        /// win dalından bu wrapper çağrılarak son günün kabul edilmiş görevleri de kapatılır.
        /// Idempotent'tir: SettleAcceptedQuestsForDayEnd zaten Collected/Failed durumundaki
        /// görevleri atlar, o yüzden yanlışlıkla iki kez çağrılsa bile ödül/ceza tekrarlanmaz.
        /// </summary>
        public void SettleAcceptedQuestsOnGameEnd()
        {
            if (!IsServer) return;

            LogDebug("Game ending (day 16 reached) - settling final day's accepted quests");
            SettleAcceptedQuestsForDayEnd();
        }

        /// <summary>
        /// Gün sonunda kabul edilmiş görevleri kapatır: tamamlananlar ödülünü, tamamlanamayanlar
        /// cezasını burada alır. Oyuncunun ayrıca "Topla" demesi GEREKMEZ - eskiden toplanmayan
        /// tamamlanmış görevin ödülü gün dönümünde sessizce kayboluyordu, artık kaybolmuyor.
        ///
        /// ÇİFT-TETİKLEME GÜVENLİĞİ: host'ta DayCycleManager.OnNewDay iki kez tetiklenebiliyor
        /// (bkz. EventEffectManager.cs:18). Burada tetikleyici durumdan (Completed/Active) ÇIKILDIĞI
        /// için ikinci çağrı hiçbir görevi eşleştiremez, yani ödül/ceza iki kez uygulanmaz.
        /// </summary>
        private void SettleAcceptedQuestsForDayEnd()
        {
            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var progress = _dailyQuests[i];

                bool isCompleted = progress.status == QuestStatus.Completed;
                bool isUnfinished = progress.status == QuestStatus.Active;

                // Kabul edilmemiş (Available) ya da zaten kapatılmış (Collected/Failed) görevler atlanır
                if (!isCompleted && !isUnfinished) continue;

                // Get quest data (F9 fix: GetQuestData artık progress.rewardSeed ile deterministik reroll yapıyor)
                QuestData questData = GetQuestData(i);
                if (questData == null) continue;

                if (isCompleted)
                {
                    ApplyRewards(questData.SelectedRewards);
                    progress.status = QuestStatus.Collected;
                    LogDebug($"Quest completed, reward applied: {questData.questTitle}");
                }
                else
                {
                    ApplyPenalties(questData.SelectedPenalties);
                    progress.status = QuestStatus.Failed;
                    LogDebug($"Quest failed, penalty applied: {questData.questTitle}");
                }

                _dailyQuests[i] = progress;
            }
        }

        private void ApplyRewards(List<QuestReward> rewards)
        {
            if (rewards == null) return;

            foreach (var reward in rewards)
            {
                ApplyRewardOrPenalty(reward, false);
            }
        }

        private void ApplyPenalties(List<QuestReward> penalties)
        {
            if (penalties == null) return;

            // Check for penalty reduction buff
            float penaltyMultiplier = 1f;
            if (BuffManager.Instance != null)
            {
                float reduction = BuffManager.Instance.GetBuffAmount(BuffType.PenaltyReduction);
                penaltyMultiplier = 1f - (reduction / 100f);
                penaltyMultiplier = Mathf.Max(0f, penaltyMultiplier);
            }

            foreach (var penalty in penalties)
            {
                ApplyRewardOrPenalty(penalty, true, penaltyMultiplier);
            }
        }

        private void ApplyRewardOrPenalty(QuestReward reward, bool isPenalty, float multiplier = 1f)
        {
            float amount = reward.amount * multiplier;

            switch (reward.rewardType)
            {
                case RewardType.Money:
                    if (MoneySystem.Instance != null)
                    {
                        MoneySystem.Instance.ModifyMoney((int)amount);
                    }
                    break;

                case RewardType.Prestige:
                    if (PrestigeManager.Instance != null)
                    {
                        PrestigeManager.Instance.ModifyPrestige(amount);
                    }
                    break;

                case RewardType.MaxStamina:
                case RewardType.MoveSpeed:
                case RewardType.CustomerWaitTime:
                case RewardType.WalkSpeed:
                case RewardType.StaminaRegenRate:
                case RewardType.DayDuration:
                case RewardType.MaxQueueSize:
                case RewardType.PenaltyReduction:
                    ApplyPermanentBuff(reward, amount);
                    break;

                case RewardType.TempMoneyBoost:
                case RewardType.TempSpeedBoost:
                    ApplyTemporaryBuff(reward, amount);
                    break;
            }
        }

        private void ApplyPermanentBuff(QuestReward reward, float amount)
        {
            if (BuffManager.Instance == null) return;

            BuffType buffType = reward.rewardType switch
            {
                RewardType.MaxStamina => BuffType.MaxStamina,
                RewardType.MoveSpeed => BuffType.MoveSpeed,
                RewardType.CustomerWaitTime => BuffType.CustomerWaitTime,
                RewardType.WalkSpeed => BuffType.WalkSpeed,
                RewardType.StaminaRegenRate => BuffType.StaminaRegenRate,
                RewardType.DayDuration => BuffType.DayDuration,
                RewardType.MaxQueueSize => BuffType.MaxQueueSize,
                RewardType.PenaltyReduction => BuffType.PenaltyReduction,
                _ => BuffType.MoveSpeed
            };

            BuffManager.Instance.AddPermanentBuff(buffType, amount);
        }

        private void ApplyTemporaryBuff(QuestReward reward, float amount)
        {
            if (BuffManager.Instance == null) return;

            BuffType buffType = reward.rewardType switch
            {
                RewardType.TempMoneyBoost => BuffType.TempMoneyPerBox,
                RewardType.TempSpeedBoost => BuffType.TempSpeedBoost,
                _ => BuffType.TempMoneyPerBox
            };

            BuffManager.Instance.AddTemporaryBuff(buffType, amount, reward.durationDays);
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print Daily Quests")]
        private void DebugPrintDailyQuests()
        {
            Debug.Log($"{LOG_PREFIX} === DAILY QUESTS ({_dailyQuests.Count}) ===");

            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var progress = _dailyQuests[i];
                var questData = GetQuestData(i);
                string title = questData != null ? questData.questTitle : "Unknown";

                Debug.Log($"  [{i}] {title}: {progress.status} - {progress.GetProgressText()}");
            }
        }

        [ContextMenu("Debug: Force Assign New Quests")]
        private void DebugForceAssignNewQuests()
        {
            if (IsServer)
            {
                AssignDailyQuests();
            }
        }

        [ContextMenu("Debug: Print Quest Database")]
        private void DebugPrintQuestDatabase()
        {
            Debug.Log($"{LOG_PREFIX} === QUEST DATABASE ({_questDatabase.Count}) ===");

            foreach (var kvp in _questDatabase)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value.questTitle} (Tier: {kvp.Value.tier})");
            }
        }
#endif

        #endregion
    }
}