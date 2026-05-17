using System;
using UnityEngine;

namespace NtingCampus.Gameplay.UI
{
    public static class CampusLanguageState
    {
        private const string PlayerPrefsKey = "NtingCampus.Gameplay.Language";

        private static bool isInitialized;
        private static CampusDisplayLanguage currentLanguage = CampusDisplayLanguage.Chinese;

        public static event Action<CampusDisplayLanguage> LanguageChanged;

        public static CampusDisplayLanguage CurrentLanguage
        {
            get
            {
                EnsureInitialized();
                return currentLanguage;
            }
        }

        public static void SetLanguage(CampusDisplayLanguage language)
        {
            EnsureInitialized();
            if (currentLanguage == language)
            {
                return;
            }

            currentLanguage = language;
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)currentLanguage);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke(currentLanguage);
        }

        private static void EnsureInitialized()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                int storedValue = PlayerPrefs.GetInt(PlayerPrefsKey, (int)CampusDisplayLanguage.Chinese);
                if (Enum.IsDefined(typeof(CampusDisplayLanguage), storedValue))
                {
                    currentLanguage = (CampusDisplayLanguage)storedValue;
                    return;
                }
            }

            currentLanguage = ResolveDefaultLanguage();
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)currentLanguage);
            PlayerPrefs.Save();
        }

        private static CampusDisplayLanguage ResolveDefaultLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    return CampusDisplayLanguage.Chinese;
                default:
                    return CampusDisplayLanguage.English;
            }
        }
    }
}
