using System;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [Serializable]
    public struct CampusLocalizedText
    {
        [SerializeField] private string chinese;
        [SerializeField] private string english;
        [SerializeField] private string traditionalChinese;
        [SerializeField] private string russian;
        [SerializeField] private string japanese;

        public CampusLocalizedText(string chineseText, string englishText)
            : this(chineseText, englishText, string.Empty, string.Empty, string.Empty)
        {
        }

        public CampusLocalizedText(
            string chineseText,
            string englishText,
            string traditionalChineseText,
            string russianText,
            string japaneseText)
        {
            chinese = string.IsNullOrWhiteSpace(chineseText) ? string.Empty : chineseText.Trim();
            english = string.IsNullOrWhiteSpace(englishText) ? string.Empty : englishText.Trim();
            traditionalChinese = string.IsNullOrWhiteSpace(traditionalChineseText) ? string.Empty : traditionalChineseText.Trim();
            russian = string.IsNullOrWhiteSpace(russianText) ? string.Empty : russianText.Trim();
            japanese = string.IsNullOrWhiteSpace(japaneseText) ? string.Empty : japaneseText.Trim();
        }

        public bool HasAnyText =>
            !string.IsNullOrWhiteSpace(chinese) ||
            !string.IsNullOrWhiteSpace(english) ||
            !string.IsNullOrWhiteSpace(traditionalChinese) ||
            !string.IsNullOrWhiteSpace(russian) ||
            !string.IsNullOrWhiteSpace(japanese);

        public string Chinese => chinese;

        public string English => english;

        public string TraditionalChinese => traditionalChinese;

        public string Russian => russian;

        public string Japanese => japanese;

        public string Current(params string[] fallbacks)
        {
            return Get(CampusLanguageState.CurrentLanguage, fallbacks);
        }

        public string Get(CampusDisplayLanguage language, params string[] fallbacks)
        {
            string resolved = CampusDisplayLanguageCatalog.Resolve(
                language,
                chinese,
                english,
                traditionalChinese,
                russian,
                japanese);

            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            return ResolveFallback(fallbacks);
        }

        public string ResolvePrimary(params string[] fallbacks)
        {
            if (!string.IsNullOrWhiteSpace(chinese))
            {
                return chinese;
            }

            if (!string.IsNullOrWhiteSpace(english))
            {
                return english;
            }

            if (!string.IsNullOrWhiteSpace(traditionalChinese))
            {
                return traditionalChinese;
            }

            if (!string.IsNullOrWhiteSpace(russian))
            {
                return russian;
            }

            if (!string.IsNullOrWhiteSpace(japanese))
            {
                return japanese;
            }

            return ResolveFallback(fallbacks);
        }

        private static string ResolveFallback(string[] fallbacks)
        {
            if (fallbacks == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < fallbacks.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(fallbacks[i]))
                {
                    return fallbacks[i].Trim();
                }
            }

            return string.Empty;
        }
    }
}

