using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Canteen
{
    internal enum CampusCanteenTextId
    {
        OrderMealPrompt = 0,
        WaitMealPrompt = 1,
        TakeMealPrompt = 2,
        ServeMealPrompt = 3,
        OrderedMealLog = 4,
        WaitingMealLog = 5,
        TookMealLog = 6,
        PreparedMealLog = 7,
        WorkstationIdlePrompt = 8,
        WorkstationIdleLog = 9,
        HandsFullLog = 10,
        WindowBusyLog = 11
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
                CampusCanteenTextId.WaitMealPrompt => Localize(language, "等餐", "Wait For Meal"),
                CampusCanteenTextId.TakeMealPrompt => Localize(language, "取餐", "Take Meal"),
                CampusCanteenTextId.ServeMealPrompt => Localize(language, "出餐", "Serve Meal"),
                CampusCanteenTextId.OrderedMealLog => Localize(language, "已下单，等待出餐。", "Order placed. Waiting for service."),
                CampusCanteenTextId.WaitingMealLog => Localize(language, "正在等待出餐。", "Waiting for the meal."),
                CampusCanteenTextId.TookMealLog => Localize(language, "已取餐。", "Meal collected."),
                CampusCanteenTextId.PreparedMealLog => Localize(language, "已将餐品放到取餐点。", "Meal placed on the pickup point."),
                CampusCanteenTextId.WorkstationIdlePrompt => Localize(language, "值守", "Stand By"),
                CampusCanteenTextId.WorkstationIdleLog => Localize(language, "当前没有待处理的餐单。", "No pending meal right now."),
                CampusCanteenTextId.HandsFullLog => Localize(language, "手上已有别的物品。", "Hands are already occupied."),
                CampusCanteenTextId.WindowBusyLog => Localize(language, "这个窗口正在服务其他人。", "This window is serving someone else."),
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
