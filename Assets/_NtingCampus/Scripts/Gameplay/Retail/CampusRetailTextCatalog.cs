using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.Retail
{
    internal enum CampusRetailTextId
    {
        CheckoutPrompt = 0,
        CheckoutComplete = 1,
        NoPendingItems = 2,
        ShelfEmpty = 3,
        ShelfUnconfigured = 4,
        PickedShelfItem = 5,
        InsufficientFunds = 6
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
                CampusRetailTextId.CheckoutPrompt => Localize(language, "\u7ed3\u8d26", "Checkout"),
                CampusRetailTextId.CheckoutComplete => Localize(language, "\u5df2\u5b8c\u6210\u7ed3\u8d26\uff0c\u82b1\u8d39 {0}\u3002", "Checkout complete. Spent {0}."),
                CampusRetailTextId.NoPendingItems => Localize(language, "\u6ca1\u6709\u5f85\u7ed3\u7b97\u5546\u54c1\u3002", "No pending items to check out."),
                CampusRetailTextId.ShelfEmpty => Localize(language, "\u8d27\u67b6\u73b0\u5728\u6ca1\u6709\u53ef\u62ff\u7684\u5546\u54c1\u3002", "The shelf is empty right now."),
                CampusRetailTextId.ShelfUnconfigured => Localize(language, "\u8fd9\u4e2a\u8d27\u67b6\u8fd8\u6ca1\u6709\u914d\u7f6e\u5546\u54c1\u3002", "This shelf has not been configured yet."),
                CampusRetailTextId.PickedShelfItem => Localize(language, "\u5df2\u4ece\u8d27\u67b6\u53d6\u8d70\u5546\u54c1\u3002", "Picked an item from the shelf."),
                CampusRetailTextId.InsufficientFunds => Localize(language, "\u91d1\u94b1\u4e0d\u8db3\uff0c\u9700\u8981 {0}\u3002", "Not enough money. Need {0}."),
                _ => string.Empty
            };
        }

        public static string Format(CampusRetailTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        private static string Localize(CampusDisplayLanguage language, string chinese, string english)
        {
            return language switch
            {
                CampusDisplayLanguage.English => english,
                CampusDisplayLanguage.Bilingual => chinese + " / " + english,
                _ => chinese
            };
        }
    }
}

