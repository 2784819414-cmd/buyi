using System.Collections.Generic;

namespace NtingCampus.UI.Runtime.Gameplay
{
    public enum CampusDisplayLanguage
    {
        Chinese = 0,
        English = 1,
        TraditionalChinese = 3,
        Russian = 4,
        Japanese = 5
    }

    public static class CampusDisplayLanguageCatalog
    {
        private static readonly CampusDisplayLanguage[] SupportedLanguages =
        {
            CampusDisplayLanguage.Chinese,
            CampusDisplayLanguage.TraditionalChinese,
            CampusDisplayLanguage.English,
            CampusDisplayLanguage.Russian,
            CampusDisplayLanguage.Japanese
        };

        public static IReadOnlyList<CampusDisplayLanguage> All => SupportedLanguages;

        public static bool IsSupported(CampusDisplayLanguage language)
        {
            for (int i = 0; i < SupportedLanguages.Length; i++)
            {
                if (SupportedLanguages[i] == language)
                {
                    return true;
                }
            }

            return false;
        }

        public static CampusDisplayLanguage Normalize(CampusDisplayLanguage language)
        {
            return IsSupported(language) ? language : CampusDisplayLanguage.Chinese;
        }

        public static string Resolve(
            CampusDisplayLanguage language,
            string chinese,
            string english,
            string traditionalChinese = null,
            string russian = null,
            string japanese = null)
        {
            switch (Normalize(language))
            {
                case CampusDisplayLanguage.English:
                    return FirstNonEmpty(english, chinese);
                case CampusDisplayLanguage.TraditionalChinese:
                    return FirstNonEmpty(traditionalChinese, chinese, english);
                case CampusDisplayLanguage.Russian:
                    return FirstNonEmpty(russian, english, chinese);
                case CampusDisplayLanguage.Japanese:
                    return FirstNonEmpty(japanese, english, chinese);
                default:
                    return FirstNonEmpty(chinese, english);
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }
    }

    public readonly struct CampusLocalizedTextEntry
    {
        public CampusLocalizedTextEntry(string chinese, string english)
            : this(chinese, english, string.Empty, string.Empty, string.Empty)
        {
        }

        public CampusLocalizedTextEntry(
            string chinese,
            string english,
            string traditionalChinese,
            string russian,
            string japanese)
        {
            Chinese = chinese;
            English = english;
            TraditionalChinese = traditionalChinese;
            Russian = russian;
            Japanese = japanese;
        }

        public string Chinese { get; }
        public string English { get; }
        public string TraditionalChinese { get; }
        public string Russian { get; }
        public string Japanese { get; }

        public bool HasAnyText =>
            !string.IsNullOrWhiteSpace(Chinese) ||
            !string.IsNullOrWhiteSpace(English) ||
            !string.IsNullOrWhiteSpace(TraditionalChinese) ||
            !string.IsNullOrWhiteSpace(Russian) ||
            !string.IsNullOrWhiteSpace(Japanese);

        public string Get(CampusDisplayLanguage language)
        {
            return CampusDisplayLanguageCatalog.Resolve(
                language,
                Chinese,
                English,
                TraditionalChinese,
                Russian,
                Japanese);
        }

        public CampusLocalizedText ToLocalizedText()
        {
            return new CampusLocalizedText(
                Chinese,
                English,
                TraditionalChinese,
                Russian,
                Japanese);
        }
    }
}
