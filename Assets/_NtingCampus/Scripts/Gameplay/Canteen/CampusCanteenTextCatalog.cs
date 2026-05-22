using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Canteen
{
    internal enum CampusCanteenTextId
    {
        OrderMealPrompt = 0,
        WindowInactivePrompt = 1,
        OrderedMealLog = 2,
        HandsFullLog = 3,
        WindowInactiveLog = 4
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
                CampusCanteenTextId.OrderMealPrompt => Localize(language, "点餐", "Order Meal"),
                CampusCanteenTextId.WindowInactivePrompt => Localize(language, "窗口未开", "Window Closed"),
                CampusCanteenTextId.OrderedMealLog => Localize(language, "已取到餐品。", "Meal received."),
                CampusCanteenTextId.HandsFullLog => Localize(language, "手上已有别的物品。", "Hands are already occupied."),
                CampusCanteenTextId.WindowInactiveLog => Localize(language, "窗口当前没有店员值守。", "No clerk is covering the window right now."),
                _ => string.Empty
            };
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
