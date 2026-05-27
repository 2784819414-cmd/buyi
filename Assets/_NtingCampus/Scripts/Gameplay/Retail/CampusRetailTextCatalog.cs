using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.Retail
{
    internal enum CampusRetailTextId
    {
        ShelfEmpty = 0,
        ShelfUnconfigured = 1,
        PickedShelfItem = 2
    }

    internal static class CampusRetailTextCatalog
    {
        public static string Get(CampusRetailTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusRetailTextId id)
        {
            return id switch
            {
                CampusRetailTextId.ShelfEmpty => Localize(language, "\u8d27\u67b6\u73b0\u5728\u6ca1\u6709\u53ef\u62ff\u7684\u5546\u54c1\u3002", "The shelf is empty right now."),
                CampusRetailTextId.ShelfUnconfigured => Localize(language, "\u8fd9\u4e2a\u8d27\u67b6\u8fd8\u6ca1\u6709\u914d\u7f6e\u5546\u54c1\u3002", "This shelf has not been configured yet."),
                CampusRetailTextId.PickedShelfItem => Localize(language, "\u5df2\u4ece\u8d27\u67b6\u53d6\u8d70\u5546\u54c1\u3002", "Picked an item from the shelf."),
                _ => string.Empty
            };
        }

        public static string Format(CampusRetailTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        private static string Localize(CampusDisplayLanguage language, string chinese, string english)
        {
            return CampusDisplayLanguageCatalog.Resolve(language, chinese, english);
        }
    }
}

