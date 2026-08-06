using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NewCss.Quest
{
    /// <summary>
    /// Tek görev slot'u için UI kontrolü
    /// Her slot görev başlığı, açıklama, ilerleme ve butonları içerir
    /// </summary>
    public class QuestSlotUI : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QuestSlotUI]";

        /// <summary>Para her zaman tam sayı gösterilir (ör. "+40").</summary>
        private const string MONEY_NUMBER_FORMAT = "0";

        /// <summary>Prestij ondalıklı olabilir; gereksiz sıfır basılmaz (0.8 -> "0.8", 2 -> "2").</summary>
        private const string PRESTIGE_NUMBER_FORMAT = "0.#";

        #endregion

        #region Serialized Fields

        [Header("=== TEXT ELEMENTS ===")]
        [SerializeField, Tooltip("Görev başlığı")]
        private TMP_Text titleText;

        [SerializeField, Tooltip("Görev açıklaması")]
        private TMP_Text descriptionText;

        [Header("=== ICON ===")]
        [SerializeField, Tooltip("Görev ikonu. QuestData.icon boşsa prefab'daki varsayılan sprite korunur.")]
        private Image iconImage;

        [Header("=== ODUL: PARA ALANI ===")]
        [SerializeField, Tooltip("Ödülün PARA kısmı, kendi alanında (ör. 'Para +40'). O gün seçilen ödülde para yoksa boşaltılır ve alan gizlenir.")]
        private TMP_Text rewardMoneyText;

        [SerializeField, Tooltip("Opsiyonel: para alanının kökü (ikon + metin grubu). Atanırsa para yokken bu obje kapanır; atanmazsa metin objesinin kendisi kapanır.")]
        private GameObject rewardMoneyGroup;

        [Header("=== ODUL: PRESTIJ ALANI ===")]
        [SerializeField, Tooltip("Ödülün PRESTİJ kısmı, kendi alanında (ör. 'Prestij +1.4'). O gün seçilen ödülde prestij yoksa boşaltılır ve alan gizlenir.")]
        private TMP_Text rewardPrestigeText;

        [SerializeField, Tooltip("Opsiyonel: prestij alanının kökü (ikon + metin grubu).")]
        private GameObject rewardPrestigeGroup;

        [Header("=== EK ETKILER (BUFF vb.) ===")]
        [SerializeField, Tooltip("Para/prestij DIŞINDAKİ etkiler (buff, stamina, hız, ceza azaltma...) tek kutuda. Hem ödül hem ceza tarafı buraya toplanır. Görevde böyle bir etki yoksa kutu tamamen gizlenir.")]
        [FormerlySerializedAs("rewardExtraText")]
        private TMP_Text extraEffectsText;

        [SerializeField, Tooltip("Opsiyonel ama ÖNERİLEN: ek etki kutusunun kökü (arka plan + ikon + metin). Atanırsa etki yokken bu obje kapanır; atanmazsa yalnız metin objesi kapanır ve arka plan boş kutu olarak kalır.")]
        private GameObject extraEffectsGroup;

        [SerializeField, Tooltip("Birden fazla ek etki varsa aralarına konan ayraç")]
        private string extraEffectsSeparator = "   ";

        [Header("=== CEZA: TEK ORTAK ALAN ===")]
        [SerializeField, Tooltip("Cezanın tamamı TEK kutuda, yan yana (ör. 'Prestij -0.8   Para -22')")]
        private TMP_Text penaltiesText;

        [SerializeField, Tooltip("Opsiyonel: ceza kutusunun kökü. Ceza yoksa kapatılır.")]
        private GameObject penaltyGroup;

        [SerializeField, Tooltip("Ceza parçalarının arasına konan ayraç (boşluk, ' | ', ' • ' vb.)")]
        private string penaltySeparator = "   ";

        [SerializeField, Tooltip("Ceza kutusunda prestij önce mi yazılsın? (kapalıysa önce para)")]
        private bool penaltyPrestigeFirst = true;

        [Header("=== METIN BICIMI ===")]
        [SerializeField, Tooltip("Para biçimi. {0} = işaretli miktar. 'Para {0}' -> 'Para +40' · '{0} Para' -> '+40 Para' · kartta para ikonu varsa sadece '{0}'.")]
        private string moneyFormat = "Para {0}";

        [SerializeField, Tooltip("Prestij biçimi. {0} = işaretli miktar. 'Prestij {0}' -> 'Prestij +1.4'.")]
        private string prestigeFormat = "Prestij {0}";

        [SerializeField, Tooltip("Ceza kutusunun sarmalayıcı biçimi. {0} = yan yana dizilmiş ceza parçaları. Ör. 'Ceza: {0}'.")]
        private string penaltyFormat = "{0}";

        [Header("=== BUTON (TEK BUTON) ===")]
        [SerializeField, Tooltip("Kartın TEK aksiyon butonu: yalnızca görev alınabilirken 'Kabul Et' olarak görünür. Kabul edildikten sonra gizlenir - ödül/ceza gün sonunda otomatik uygulandığı için toplama butonu YOKTUR.")]
        [FormerlySerializedAs("acceptButton")]
        private Button actionButton;

        [SerializeField, Tooltip("Butonun yazısı. Boş bırakılırsa butonun altındaki ilk TMP metni otomatik bulunur.")]
        private TMP_Text actionButtonLabel;

        [SerializeField, Tooltip("Görev alınabilir durumdayken buton yazısı")]
        private string acceptLabel = "Kabul Et";

        [SerializeField, Tooltip("Opsiyonel çeviri anahtarı (StringTable'da varsa yazının yerine geçer, yoksa yukarıdaki düz metin kullanılır)")]
        private string acceptLabelKey = "Quest_Accept";

        [Header("=== PROGRESS BAR (Devre Dışı) ===")]
        [SerializeField, Tooltip("İlerleme çubuğu dolgu - artık kullanılmıyor")]
        private Image progressFill;

        [Header("=== TIER INDICATOR (Devre Dışı) ===")]
        [SerializeField, Tooltip("Zorluk tier göstergesi - artık kullanılmıyor")]
        private Image tierIndicator;

        [SerializeField, Tooltip("Easy tier rengi")]
        private Color easyColor = Color.green;

        [SerializeField, Tooltip("Medium tier rengi")]
        private Color mediumColor = Color.yellow;

        [SerializeField, Tooltip("Hard tier rengi")]
        private Color hardColor = Color.red;

        [Header("=== STATUS INDICATOR (Devre Dışı) ===")]
        [SerializeField, Tooltip("Durum göstergesi - artık kullanılmıyor")]
        private GameObject completedIndicator;

        [SerializeField, Tooltip("Aktif durum göstergesi - artık kullanılmıyor")]
        private GameObject activeIndicator;

        [Header("=== LEGACY (BOS BIRAK) ===")]
        [SerializeField, Tooltip("Eski BİRLEŞİK ödül metni. Yeni ayrı para/prestij alanlarını kullanıyorsan BOŞ BIRAK - atalı kalırsa kartta ödül iki kere yazar.")]
        private TMP_Text rewardsText;

        #endregion

        #region Private Fields

        private int _slotIndex;
        private QuestData _currentQuestData;
        private QuestProgress _currentProgress;

        // Metin kurarken tekrar tekrar alokasyon yapmamak için yeniden kullanılan tamponlar
        private readonly List<string> _extraBuffer = new List<string>();
        private readonly List<string> _partBuffer = new List<string>();

        // Ödül + ceza taraflarından toplanan para/prestij dışı etkiler (ek etki kutusunun içeriği)
        private readonly List<string> _combinedExtras = new List<string>();
        private bool _warnedMissingExtraField;

        #endregion

        #region Public Properties

        /// <summary>
        /// Slot index'i
        /// </summary>
        public int SlotIndex => _slotIndex;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupButtonListeners();
            ValidateWiring();
            HideRemovedElements();
            LocalizationHelper.OnLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
            LocalizationHelper.OnLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged()
        {
            // Refresh UI when locale changes
            if (_currentQuestData != null)
            {
                UpdateUI();
            }
            else
            {
                SetEmptyState();
            }
        }

        #endregion

        #region Initialization

        private void SetupButtonListeners()
        {
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(OnActionClicked);

                // Yazı alanı atanmadıysa butonun altındaki ilk TMP metnini kullan (kapalı objeler dahil)
                if (actionButtonLabel == null)
                {
                    actionButtonLabel = actionButton.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        private void RemoveButtonListeners()
        {
            if (actionButton != null)
            {
                actionButton.onClick.RemoveListener(OnActionClicked);
            }
        }

        /// <summary>
        /// Zorunlu inspector referanslarını başlangıçta denetler ve eksikleri TEK LogError'da bildirir.
        ///
        /// Gerekçe: bağlanmamış bir alan bu sınıfta hiçbir hata üretmiyordu - ilgili kart parçası
        /// sessizce boş kalıyordu (accept butonunun ve açıklamanın hiç görünmemesi tam olarak buydu).
        /// Opsiyonel alanlar (ikon, ek etki kutusu, *Group kökleri, legacy alanlar) bilerek dışarıda.
        /// </summary>
        private void ValidateWiring()
        {
            var missing = new List<string>();

            if (titleText == null) missing.Add(nameof(titleText));
            if (descriptionText == null) missing.Add(nameof(descriptionText));
            if (actionButton == null) missing.Add(nameof(actionButton));
            if (rewardMoneyText == null) missing.Add(nameof(rewardMoneyText));
            if (rewardPrestigeText == null) missing.Add(nameof(rewardPrestigeText));
            if (penaltiesText == null) missing.Add(nameof(penaltiesText));

            if (missing.Count == 0) return;

            Debug.LogError($"{LOG_PREFIX} '{gameObject.name}' slotunda BAĞLANMAMIŞ alan(lar): " +
                           $"{string.Join(", ", missing)} - bu parçalar kartta sessizce boş görünür. " +
                           $"Inspector'dan bağla.", this);
        }

        /// <summary>
        /// Kaldırılan UI elementlerini gizler (Progress Bar, Tier Indicator, Status Indicators)
        /// </summary>
        private void HideRemovedElements()
        {
            // Progress Bar'ı gizle
            if (progressFill != null)
            {
                progressFill.gameObject.SetActive(false);
            }

            // Tier Indicator'ı gizle
            if (tierIndicator != null)
            {
                tierIndicator.gameObject.SetActive(false);
            }

            // Status Indicator'ları gizle
            if (completedIndicator != null)
            {
                completedIndicator.SetActive(false);
            }

            if (activeIndicator != null)
            {
                activeIndicator.SetActive(false);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Slot'u görev verileriyle günceller
        /// </summary>
        public void Setup(int slotIndex, QuestData questData, QuestProgress progress)
        {
            _slotIndex = slotIndex;
            _currentQuestData = questData;
            _currentProgress = progress;

            if (questData == null)
            {
                SetEmptyState();
                return;
            }

            UpdateUI();
        }

        /// <summary>
        /// İlerlemeyi günceller
        /// </summary>
        public void UpdateProgress(QuestProgress progress)
        {
            _currentProgress = progress;
            UpdateButtonStates();
        }

        #endregion

        #region UI Updates

        private void UpdateUI()
        {
            UpdateTextElements();
            UpdateIcon();
            UpdateButtonStates();
            HideRemovedElements(); // Her güncellemede kaldırılan elementleri gizle
        }

        private void UpdateTextElements()
        {
            if (titleText != null)
            {
                titleText.text = _currentQuestData.questTitle;
            }

            if (descriptionText != null)
            {
                // _currentProgress.targetProgress = QuestManager.CalculateEffectiveTargetCount ile
                // ölçeklenmiş GERÇEK hedef (server tarafında QuestProgress oluşturulurken yazılır).
                // Kart, ham requirement.targetCount değil bu sayıyı göstermeli (kontrol P0 fix).
                descriptionText.text = _currentQuestData.GetFullDescription(_currentProgress.targetProgress);
            }

            // Sıra önemli: ödül ve ceza, para/prestij dışı etkileri _combinedExtras'a biriktirir,
            // ek etki kutusu en son bu birikimi yazar (ya da hiç etki yoksa kendini kapatır).
            UpdateRewardTexts();
            UpdatePenaltyText();
            UpdateExtraEffectsText();
            UpdateLegacyRewardsText();
        }

        /// <summary>
        /// Ödülü İKİ AYRI alana yazar: para bir kutuda, prestij ayrı kutuda.
        /// O gün seçilen 2 ödül aynı türdense (ör. +20 ve +40 para) toplanır, diğer alan gizlenir.
        /// </summary>
        private void UpdateRewardTexts()
        {
            // Ek etki birikimi HER güncellemede burada sıfırlanır (bu metot zincirin ilk halkası).
            _combinedExtras.Clear();

            SplitByType(_currentQuestData.SelectedRewards,
                        out float money, out bool hasMoney,
                        out float prestige, out bool hasPrestige,
                        _extraBuffer);

            SetAmountField(rewardMoneyText, rewardMoneyGroup, hasMoney, FormatMoney(money));
            SetAmountField(rewardPrestigeText, rewardPrestigeGroup, hasPrestige, FormatPrestige(prestige));

            _combinedExtras.AddRange(_extraBuffer);
        }

        /// <summary>
        /// Cezanın para/prestij kısmını TEK kutuya yan yana yazar (ör. "Prestij -0.8   Para -22").
        /// Para/prestij dışı cezalar buraya değil, ek etki kutusuna gider.
        /// </summary>
        private void UpdatePenaltyText()
        {
            SplitByType(_currentQuestData.SelectedPenalties,
                        out float money, out bool hasMoney,
                        out float prestige, out bool hasPrestige,
                        _extraBuffer);

            // Ceza tarafındaki buff benzeri etkiler de ek etki kutusuna toplanır (kutu atanmasa bile
            // aşağıdaki uyarı yolu devrede kalsın diye ceza metni null olsa da bu adım atlanmaz).
            _combinedExtras.AddRange(_extraBuffer);

            if (penaltiesText == null)
            {
                return;
            }

            _partBuffer.Clear();

            if (penaltyPrestigeFirst)
            {
                if (hasPrestige) _partBuffer.Add(FormatPrestige(prestige));
                if (hasMoney) _partBuffer.Add(FormatMoney(money));
            }
            else
            {
                if (hasMoney) _partBuffer.Add(FormatMoney(money));
                if (hasPrestige) _partBuffer.Add(FormatPrestige(prestige));
            }

            bool hasPenalty = _partBuffer.Count > 0;
            string joined = string.Join(penaltySeparator ?? " ", _partBuffer);

            penaltiesText.text = hasPenalty ? string.Format(SafeFormat(penaltyFormat), joined) : "";
            SetGroupActive(penaltiesText.gameObject, penaltyGroup, hasPenalty);
        }

        /// <summary>
        /// Ödül ve ceza taraflarından toplanan para/prestij DIŞI etkileri (buff vb.) tek kutuya yazar.
        /// Görevde böyle bir etki yoksa kutu (grup atanmışsa arka planıyla birlikte) tamamen gizlenir.
        /// </summary>
        private void UpdateExtraEffectsText()
        {
            bool hasExtra = _combinedExtras.Count > 0;

            if (extraEffectsText == null)
            {
                // Metin atanmasa bile grup atanmışsa en azından boş kutuyu kapat.
                if (extraEffectsGroup != null)
                {
                    SetGroupActive(null, extraEffectsGroup, hasExtra);
                }
                else if (hasExtra && !_warnedMissingExtraField)
                {
                    // Buff'lı bir görev kartta sessizce eksik görünmesin; en azından bir kez uyaralım.
                    _warnedMissingExtraField = true;
                    Debug.LogWarning($"{LOG_PREFIX} '{_currentQuestData.questTitle}' görevinde para/prestij dışı etki var " +
                                     $"({string.Join(", ", _combinedExtras)}) ama extraEffectsText atanmamış - kartta görünmeyecek.");
                }

                return;
            }

            extraEffectsText.text = hasExtra
                ? string.Join(extraEffectsSeparator ?? " ", _combinedExtras)
                : "";

            SetGroupActive(extraEffectsText.gameObject, extraEffectsGroup, hasExtra);
        }

        /// <summary>
        /// Eski birleşik ödül alanı hâlâ atalıysa doldurulur (geri uyumluluk). Normalde boş bırakılmalı.
        /// </summary>
        private void UpdateLegacyRewardsText()
        {
            if (rewardsText == null) return;

            string rewardLabel = LocalizationHelper.GetLocalizedString("Quest_Reward");
            rewardsText.text = $"{rewardLabel}: {_currentQuestData.GetRewardsSummary()}";
        }

        /// <summary>
        /// Seçilmiş ödül/ceza listesini para ve prestij olarak ayırır, kalan türleri metin olarak biriktirir.
        /// Aynı türden birden fazla giriş varsa TOPLANIR (ikisi de veriliyor).
        /// </summary>
        private static void SplitByType(List<QuestReward> entries,
                                        out float money, out bool hasMoney,
                                        out float prestige, out bool hasPrestige,
                                        List<string> extras)
        {
            money = 0f;
            prestige = 0f;
            hasMoney = false;
            hasPrestige = false;
            extras.Clear();

            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry == null) continue;

                switch (entry.rewardType)
                {
                    case RewardType.Money:
                        money += entry.amount;
                        hasMoney = true;
                        break;

                    case RewardType.Prestige:
                        prestige += entry.amount;
                        hasPrestige = true;
                        break;

                    default:
                        extras.Add(entry.GetDescription());
                        break;
                }
            }
        }

        /// <summary>
        /// Tek bir miktar alanını yazar; değer yoksa metni boşaltıp alanı (varsa grubunu) gizler.
        /// </summary>
        private static void SetAmountField(TMP_Text text, GameObject group, bool hasValue, string value)
        {
            if (text == null)
            {
                if (group != null) SetGroupActive(null, group, hasValue);
                return;
            }

            text.text = hasValue ? value : "";
            SetGroupActive(text.gameObject, group, hasValue);
        }

        /// <summary>
        /// Grup atanmışsa onu, atanmamışsa metin objesinin kendisini açar/kapatır.
        /// </summary>
        private static void SetGroupActive(GameObject textObject, GameObject group, bool active)
        {
            var target = group != null ? group : textObject;
            if (target == null) return;

            if (target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private string FormatMoney(float amount)
        {
            return string.Format(SafeFormat(moneyFormat), Signed(amount, MONEY_NUMBER_FORMAT));
        }

        private string FormatPrestige(float amount)
        {
            return string.Format(SafeFormat(prestigeFormat), Signed(amount, PRESTIGE_NUMBER_FORMAT));
        }

        /// <summary>Biçim metni boş ya da {0} içermiyorsa ham miktara düşer (alan asla boş kalmasın).</summary>
        private static string SafeFormat(string format)
        {
            return string.IsNullOrEmpty(format) || !format.Contains("{0}") ? "{0}" : format;
        }

        /// <summary>Pozitif değerin başına '+' koyar; negatifte işaret zaten sayının içinde.</summary>
        private static string Signed(float amount, string numberFormat)
        {
            string body = amount.ToString(numberFormat, CultureInfo.InvariantCulture);
            return amount > 0f ? "+" + body : body;
        }

        /// <summary>
        /// Görev ikonunu karta yazar. QuestData'da ikon yoksa prefab'ın varsayılan sprite'ı
        /// olduğu gibi bırakılır (kart boş görünmez); hiç sprite yoksa Image kapatılır.
        /// </summary>
        private void UpdateIcon()
        {
            if (iconImage == null) return;

            if (_currentQuestData != null && _currentQuestData.icon != null)
            {
                iconImage.sprite = _currentQuestData.icon;
            }

            iconImage.enabled = iconImage.sprite != null;
        }

        /// <summary>
        /// Buton yalnızca görev HENÜZ ALINABİLİRKEN görünür. Kabul edildikten sonra kartta
        /// tıklanacak bir şey kalmaz; ödül ve ceza gün sonunda QuestManager tarafından otomatik
        /// uygulanır. NOT: ilerleme/durum yazısı (progressText) kaldırıldı — kart artık kabul
        /// sonrası oyuncuya durum geri bildirimi VERMİYOR.
        /// </summary>
        private void UpdateButtonStates()
        {
            if (actionButton == null) return;

            bool canAccept = _currentProgress.status == QuestStatus.Available;

            // Günlük kabul limiti dolduysa (başka bir slottan görev alındıysa) buton görünür
            // kalır ama tıklanamaz + gri görünür (Selectable.interactable=false -> disabledColor).
            bool hasAcceptedToday = QuestManager.Instance != null && QuestManager.Instance.HasAcceptedQuestTodayClient;

            actionButton.gameObject.SetActive(canAccept);
            actionButton.interactable = canAccept && !hasAcceptedToday;

            if (canAccept && actionButtonLabel != null)
            {
                actionButtonLabel.text = Localized(acceptLabelKey, acceptLabel);
            }
        }

        /// <summary>
        /// Çeviri anahtarı StringTable'da yoksa LocalizationHelper anahtarın kendisini döndürüyor;
        /// bu durumda butonda "Quest_Accept" yazmasın diye inspector'daki düz metne düşeriz.
        /// </summary>
        private static string Localized(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;

            string value = LocalizationHelper.GetLocalizedString(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        private void SetEmptyState()
        {
            if (titleText != null) titleText.text = LocalizationHelper.GetLocalizedString("Quest_NoQuest");
            if (descriptionText != null) descriptionText.text = "";
            if (rewardsText != null) rewardsText.text = "";

            // Ödül/ceza alanlarını hem boşalt hem gizle (boş kutu iskeleti kalmasın)
            SetAmountField(rewardMoneyText, rewardMoneyGroup, false, "");
            SetAmountField(rewardPrestigeText, rewardPrestigeGroup, false, "");

            _combinedExtras.Clear();

            if (extraEffectsText != null)
            {
                extraEffectsText.text = "";
                SetGroupActive(extraEffectsText.gameObject, extraEffectsGroup, false);
            }
            else if (extraEffectsGroup != null)
            {
                SetGroupActive(null, extraEffectsGroup, false);
            }

            if (penaltiesText != null)
            {
                penaltiesText.text = "";
                SetGroupActive(penaltiesText.gameObject, penaltyGroup, false);
            }

            if (actionButton != null) actionButton.gameObject.SetActive(false);

            HideRemovedElements();
        }

        #endregion

        #region Button Handlers

        /// <summary>
        /// Butonun tek işi görevi kabul etmek. Buton yalnızca Available durumunda görünür olduğu için
        /// durum kontrolü burada çift-koruma amaçlı duruyor (gizli bir butona gelen tıklama yutulur).
        /// </summary>
        private void OnActionClicked()
        {
            if (_currentProgress.status != QuestStatus.Available) return;

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AcceptQuest(_slotIndex);
                Debug.Log($"{LOG_PREFIX} Quest accepted at slot {_slotIndex}");
            }
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === SLOT {_slotIndex} STATE ===");
            Debug.Log($"Quest: {(_currentQuestData != null ? _currentQuestData.questTitle : "None")}");
            Debug.Log($"Status: {_currentProgress.status}");
            Debug.Log($"Progress: {_currentProgress.GetProgressText()}");
        }
#endif

        #endregion
    }
}
