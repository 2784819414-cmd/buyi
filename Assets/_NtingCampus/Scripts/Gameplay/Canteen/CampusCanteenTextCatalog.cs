using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Canteen
{
    public enum CampusCanteenTextId
    {
        WindowUnavailable = 0,
        WindowPrompt = 1,
        NoCharacter = 2,
        NoServingWindow = 3,
        NoStockContainer = 4,
        NoClerkAtStation = 5,
        NoMatchingFood = 6,
        CouldNotCreateFood = 7,
        FoodUnavailable = 8,
        StandAtWindow = 9,
        WindowClosed = 10,
        MealAlreadyOnCounter = 11,
        CouldNotPlaceMeal = 12,
        ClerkPreparedMeal = 13,
        AlreadyReceivedMeal = 14,
        NoMealAtWindow = 15,
        CustomerTookMeal = 16,
        Summary = 17,
        CanteenFallback = 18,
        DishDescription = 19,
        DishUseText = 20,
        LogLine = 21,
        GenericWindowName = 22,
        UnknownActor = 23,
        FoodBoxName = 24
    }

    public static class CampusCanteenTextCatalog
    {
        private readonly struct Entry
        {
            public Entry(string chinese, string english)
            {
                Chinese = chinese;
                English = english;
            }

            public string Chinese { get; }
            public string English { get; }
        }

        private static readonly Dictionary<CampusCanteenTextId, Entry> Entries =
            new Dictionary<CampusCanteenTextId, Entry>
            {
                { CampusCanteenTextId.WindowUnavailable, new Entry("食堂窗口不可用。", "Canteen window is unavailable.") },
                { CampusCanteenTextId.WindowPrompt, new Entry("窗口：{0}", "Window: {0}") },
                { CampusCanteenTextId.NoCharacter, new Entry("没有可用于食堂交互的角色。", "No character is available for canteen interaction.") },
                { CampusCanteenTextId.NoServingWindow, new Entry("没有可用的食堂窗口。", "No canteen window is available.") },
                { CampusCanteenTextId.NoStockContainer, new Entry("没有可用的食堂库存容器。", "No canteen stock container is available.") },
                { CampusCanteenTextId.NoClerkAtStation, new Entry("该角色不是这个窗口的店员。", "This actor is not a clerk for this window.") },
                { CampusCanteenTextId.NoMatchingFood, new Entry("该窗口没有可出的餐品。", "This window has no matching dish.") },
                { CampusCanteenTextId.CouldNotCreateFood, new Entry("无法创建食堂餐品。", "Could not create canteen food.") },
                { CampusCanteenTextId.FoodUnavailable, new Entry("该食物已不可用。", "That food is no longer available.") },
                { CampusCanteenTextId.StandAtWindow, new Entry("请站到 {0}。", "Please stand at {0}.") },
                { CampusCanteenTextId.WindowClosed, new Entry("食堂窗口还没有开放。", "The canteen window is not open.") },
                { CampusCanteenTextId.MealAlreadyOnCounter, new Entry("窗口已经有餐品。", "There is already food at the window.") },
                { CampusCanteenTextId.CouldNotPlaceMeal, new Entry("无法把餐品放到窗口。", "Could not place the meal at the window.") },
                { CampusCanteenTextId.ClerkPreparedMeal, new Entry("{0} 在 {2} 放好了 {1}。", "{0} placed {1} at {2}.") },
                { CampusCanteenTextId.AlreadyReceivedMeal, new Entry("今天已经取过餐。", "This actor already received a meal today.") },
                { CampusCanteenTextId.NoMealAtWindow, new Entry("窗口暂时没有餐品。", "There is no food at the window.") },
                { CampusCanteenTextId.CustomerTookMeal, new Entry("{0} 取走了 {1}。", "{0} took {1}.") },
                { CampusCanteenTextId.Summary, new Entry("窗口={0}，窗口餐品={1}，今日已取餐={2}。", "Windows={0}, food at windows={1}, meals taken today={2}.") },
                { CampusCanteenTextId.CanteenFallback, new Entry("食堂", "Canteen") },
                { CampusCanteenTextId.DishDescription, new Entry("{0}，来自食堂。", "{0} from the canteen.") },
                { CampusCanteenTextId.DishUseText, new Entry("吃掉了{0}。", "Ate {0}.") },
                { CampusCanteenTextId.LogLine, new Entry("[食堂] {0}", "[Canteen] {0}") },
                { CampusCanteenTextId.GenericWindowName, new Entry("窗口{0}", "Window {0}") },
                { CampusCanteenTextId.UnknownActor, new Entry("未知角色", "Unknown actor") },
                { CampusCanteenTextId.FoodBoxName, new Entry("{0} 食物箱", "{0} Food Box") }
            };

        public static string Get(CampusCanteenTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusCanteenTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return Resolve(language, entry.Chinese, entry.English);
        }

        public static string Format(CampusCanteenTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Format(CampusDisplayLanguage language, CampusCanteenTextId id, params object[] args)
        {
            return string.Format(Get(language, id), args);
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
