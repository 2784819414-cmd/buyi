using System;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [Serializable]
    public struct CampusLocalizedText
    {
        [SerializeField] private string chinese;
        [SerializeField] private string english;

        public CampusLocalizedText(string chineseText, string englishText)
        {
            chinese = string.IsNullOrWhiteSpace(chineseText) ? string.Empty : chineseText.Trim();
            english = string.IsNullOrWhiteSpace(englishText) ? string.Empty : englishText.Trim();
        }

        public bool HasAnyText => !string.IsNullOrWhiteSpace(chinese) || !string.IsNullOrWhiteSpace(english);

        public string Chinese => chinese;

        public string English => english;

        public string Current(params string[] fallbacks)
        {
            return Get(CampusLanguageState.CurrentLanguage, fallbacks);
        }

        public string Get(CampusDisplayLanguage language, params string[] fallbacks)
        {
            string resolved = language switch
            {
                CampusDisplayLanguage.English => ResolveEnglishFirst(),
                CampusDisplayLanguage.Bilingual => ResolveBilingual(),
                _ => ResolveChineseFirst()
            };

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

            return ResolveFallback(fallbacks);
        }

        private string ResolveChineseFirst()
        {
            if (!string.IsNullOrWhiteSpace(chinese))
            {
                return chinese;
            }

            return string.IsNullOrWhiteSpace(english) ? string.Empty : english;
        }

        private string ResolveEnglishFirst()
        {
            if (!string.IsNullOrWhiteSpace(english))
            {
                return english;
            }

            return string.IsNullOrWhiteSpace(chinese) ? string.Empty : chinese;
        }

        private string ResolveBilingual()
        {
            bool hasChinese = !string.IsNullOrWhiteSpace(chinese);
            bool hasEnglish = !string.IsNullOrWhiteSpace(english);
            if (hasChinese && hasEnglish)
            {
                return chinese + " / " + english;
            }

            if (hasChinese)
            {
                return chinese;
            }

            return hasEnglish ? english : string.Empty;
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

