using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace NewCss
{
    /// <summary>
    /// Localization yardımcı sınıfı - string tablosundan çeviri almayı ve dil değişikliği olaylarını yönetir.
    /// </summary>
    public static class LocalizationHelper
    {
        #region Constants

        private const string LOG_PREFIX = "[LocalizationHelper]";
        private const string DEFAULT_TABLE = "StringTable";

        #endregion

        #region Events

        /// <summary>
        /// Dil değiştirildiğinde tetiklenir
        /// </summary>
        public static event Action OnLocaleChanged;

        private static bool _isSubscribed = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Localization sistemini başlatır ve dil değişikliği olaylarına abone olur
        /// </summary>
        public static void Initialize()
        {
            if (_isSubscribed) return;

            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            _isSubscribed = true;

            Debug.Log($"{LOG_PREFIX} Initialized and subscribed to locale changes");
        }

        /// <summary>
        /// Olaylardan aboneliği kaldırır
        /// </summary>
        public static void Cleanup()
        {
            if (!_isSubscribed) return;

            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            _isSubscribed = false;

            Debug.Log($"{LOG_PREFIX} Cleaned up and unsubscribed from locale changes");
        }

        #endregion

        #region Event Handlers

        private static void HandleLocaleChanged(Locale newLocale)
        {
            Debug.Log($"{LOG_PREFIX} Locale changed to: {newLocale?.Identifier.Code ?? "null"}");
            OnLocaleChanged?.Invoke();
        }

        #endregion

        #region Public API

        /// <summary>
        /// String tablosundan yerelleştirilmiş metin alır
        /// </summary>
        /// <param name="key">Localization key'i</param>
        /// <param name="tableName">Tablo adı (varsayılan: StringTable)</param>
        /// <returns>Yerelleştirilmiş metin veya key'in kendisi (fallback)</returns>
        public static string GetLocalizedString(string key, string tableName = DEFAULT_TABLE)
        {
            try
            {
                if (!LocalizationSettings.InitializationOperation.IsDone)
                {
                    return key;
                }

                var stringTable = LocalizationSettings.StringDatabase.GetTable(tableName);
                if (stringTable != null)
                {
                    var entry = stringTable.GetEntry(key);
                    if (entry != null && !string.IsNullOrEmpty(entry.LocalizedValue))
                    {
                        return entry.LocalizedValue;
                    }
                }

                return key;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_PREFIX} Error getting localized string for key '{key}': {e.Message}");
                return key;
            }
        }

        /// <summary>
        /// String tablosundan yerelleştirilmiş metin alır ve format parametreleri uygular
        /// </summary>
        /// <param name="key">Localization key'i</param>
        /// <param name="args">Format parametreleri</param>
        /// <returns>Formatlanmış yerelleştirilmiş metin</returns>
        public static string GetLocalizedStringFormat(string key, params object[] args)
        {
            string template = GetLocalizedString(key);
            
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"{LOG_PREFIX} Format error for key '{key}': {e.Message}");
                return template;
            }
        }

        /// <summary>
        /// Mevcut dilin Türkçe olup olmadığını kontrol eder
        /// </summary>
        public static bool IsTurkish()
        {
            return LocalizationSettings.SelectedLocale != null &&
                   LocalizationSettings.SelectedLocale.Identifier.Code == "tr";
        }

        /// <summary>
        /// Localization sisteminin hazır olup olmadığını kontrol eder
        /// </summary>
        public static bool IsReady()
        {
            return LocalizationSettings.InitializationOperation.IsDone;
        }

        #endregion
    }
}
