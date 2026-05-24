using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.Canteen
{
    internal enum CampusCanteenTextId
    {
        OrderMealPrompt = 0,
        WindowInactivePrompt = 1,
        OrderedMealLog = 2,
        HandsFullLog = 3,
        WindowInactiveLog = 4,
        OrderedMenuItemLog = 5,
        MenuItemMissingLog = 6,
        OrderPanelTitle = 7,
        OrderPanelBalanceLine = 8,
        PriceLine = 9,
        OrderButton = 10,
        CloseButton = 11,
        InsufficientFundsLog = 12,
        OrderFailedLog = 13,
        PendingMealLog = 14
    }

    internal static class CampusCanteenTextCatalog
    {
        public static string Get(CampusCanteenTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusCanteenTextId id)
        {
            return id switch
            {
                CampusCanteenTextId.OrderMealPrompt => Resolve(language, "\u70b9\u9910", "Order Meal"),
                CampusCanteenTextId.WindowInactivePrompt => Resolve(language, "\u7a97\u53e3\u672a\u5f00", "Window Closed"),
                CampusCanteenTextId.OrderedMealLog => Resolve(language, "\u5df2\u53d6\u5230\u9910\u54c1\u3002", "Meal received."),
                CampusCanteenTextId.HandsFullLog => Resolve(language, "\u624b\u4e0a\u5df2\u6709\u522b\u7684\u7269\u54c1\u3002", "Hands are already occupied."),
                CampusCanteenTextId.WindowInactiveLog => Resolve(language, "\u7a97\u53e3\u5f53\u524d\u6ca1\u6709\u5e97\u5458\u503c\u5b88\u3002", "No clerk is covering the window right now."),
                CampusCanteenTextId.OrderedMenuItemLog => Resolve(language, "\u5df2\u53d6\u5230{0}\u3002", "Received {0}."),
                CampusCanteenTextId.MenuItemMissingLog => Resolve(language, "\u8fd9\u4e2a\u7a97\u53e3\u7684\u83dc\u5355\u9879\u914d\u7f6e\u65e0\u6548\u3002", "This window has an invalid menu item configuration."),
                CampusCanteenTextId.OrderPanelTitle => Resolve(language, "\u98df\u5802\u70b9\u9910", "Canteen Ordering"),
                CampusCanteenTextId.OrderPanelBalanceLine => Resolve(language, "\u4f59\u989d\uff1a{0}", "Balance: {0}"),
                CampusCanteenTextId.PriceLine => Resolve(language, "\u4ef7\u683c\uff1a{0}", "Price: {0}"),
                CampusCanteenTextId.OrderButton => Resolve(language, "\u4e0b\u5355", "Order"),
                CampusCanteenTextId.CloseButton => Resolve(language, "\u5173\u95ed", "Close"),
                CampusCanteenTextId.InsufficientFundsLog => Resolve(language, "\u4f59\u989d\u4e0d\u8db3\uff0c\u9700\u8981 {0}\u3002", "Not enough money. Need {0}."),
                CampusCanteenTextId.OrderFailedLog => Resolve(language, "\u70b9\u9910\u5931\u8d25\u3002", "Order failed."),
                CampusCanteenTextId.PendingMealLog => Resolve(language, "\u5df2\u6709\u672a\u53d6\u8d70\u7684\u9910\u54c1\u3002", "There is already an unclaimed meal."),
                _ => string.Empty
            };
        }

        public static string Format(CampusCanteenTextId id, params object[] args)
        {
            string template = Get(id);
            return string.IsNullOrWhiteSpace(template) ? string.Empty : string.Format(template, args);
        }

        private static string Resolve(CampusDisplayLanguage language, string chinese, string english)
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
