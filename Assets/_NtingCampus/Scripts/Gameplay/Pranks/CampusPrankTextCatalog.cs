using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Pranks
{
    public enum CampusPrankTextId
    {
        MissingPrankService = 0,
        UnknownFormalPrankPayload = 1,
        UnknownPrankPayloadLog = 2,
        PrankNotWired = 3,
        NoNearbyStudent = 4,
        PassNoteSucceededDistracted = 5,
        PassNoteSucceeded = 6,
        TeacherNoticedNote = 7,
        MissingActorRuntime = 8,
        PassNotesOnlyDuringClass = 9,
        ActorNeedsClassroom = 10,
        ActorNeedsCanteen = 11,
        NoDeliveryWaiting = 12,
        ActorNeedsOutdoorDelivery = 13,
        CanteenTheftLog = 14,
        DeliveryTakenLikelyReported = 15,
        DeliveryTakenBeforeOwner = 16,
        SecretDeliveryWaiting = 17,
        DeliveryPickedUp = 18,
        DeliveryMissingFound = 19,
        DeliveryReportedMissing = 20,
        DeliveryGaveUpSearching = 21,
        FailedCreateStolenItem = 22,
        StolenItemDescription = 23,
        AteItem = 24,
        RuntimeStolenItemDescription = 25,
        SuspicionIncrease = 26,
        SuspicionThresholdReached = 27,
        ActionCoolingDown = 28,
        PrankActionCoolingDown = 29,
        DeliveryWaitingPrompt = 30,
        DeliveryOwnerLookingPrompt = 31,
        DeliveryOwnerReportingPrompt = 32,
        CanteenClerkStatePrompt = 33,
        DefaultPromptNoClass = 34,
        DefaultPromptClass = 35,
        ActorFallback = 36,
        PassNoteActionName = 37,
        DeliveryActionName = 38,
        DeliveryOwnerReportsLossReason = 39,
        StolenDeliveryReason = 40,
        DeliveryOwnerReportedMissingReason = 41,
        StolenFriedChicken = 42,
        StolenBurger = 43,
        StolenOdenCup = 44,
        DeliveryFriedChickenRice = 45,
        DeliveryMilkTea = 46,
        DeliverySpicyNoodles = 47,
        DeliveryBurgerSet = 48,
        DeliveryOdenCup = 49,
        StolenDeliveryItem = 50,
        OutdoorNeedsDeliveryDropPoint = 51,
        DefaultPrankSpot = 52,
        DefaultUnsupportedPrankSpot = 53
    }

    public static class CampusPrankTextCatalog
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

        private static readonly Dictionary<CampusPrankTextId, Entry> Entries = new Dictionary<CampusPrankTextId, Entry>
        {
            { CampusPrankTextId.MissingPrankService, new Entry("正式恶作剧服务缺失。", "Formal prank service is missing.") },
            { CampusPrankTextId.UnknownFormalPrankPayload, new Entry("未知正式恶作剧内容。", "Unknown formal prank payload.") },
            { CampusPrankTextId.UnknownPrankPayloadLog, new Entry("[恶作剧] 未知恶作剧内容：{0}。", "[Prank] Unknown prank payload: {0}.") },
            { CampusPrankTextId.PrankNotWired, new Entry("[恶作剧] {0} 还没接入正式玩法闭环。", "[Prank] {0} is not wired into the formal gameplay loop yet.") },
            { CampusPrankTextId.NoNearbyStudent, new Entry("[恶作剧] 附近没有可接收纸条的学生。", "[Prank] No nearby student is available to receive the note.") },
            { CampusPrankTextId.PassNoteSucceededDistracted, new Entry("[恶作剧] 老师正在分心。{0} 顺利传出了纸条。神力 +{1}。", "[Prank] The teacher is distracted. {0} passed the note cleanly. Divine Power +{1}.") },
            { CampusPrankTextId.PassNoteSucceeded, new Entry("[恶作剧] {0} 顺利传出了纸条。神力 +{1}。", "[Prank] {0} passed the note cleanly. Divine Power +{1}.") },
            { CampusPrankTextId.TeacherNoticedNote, new Entry("[恶作剧] 老师发现了传纸条。", "[Prank] The teacher noticed the note passing.") },
            { CampusPrankTextId.MissingActorRuntime, new Entry("没有可用的正式角色运行时。", "No formal actor runtime is available.") },
            { CampusPrankTextId.PassNotesOnlyDuringClass, new Entry("传纸条只在上课时计入正式玩法。", "Passing notes only counts during class sessions.") },
            { CampusPrankTextId.ActorNeedsClassroom, new Entry("角色需要在正式教室内。", "Actor needs to be inside a formal classroom.") },
            { CampusPrankTextId.ActorNeedsCanteen, new Entry("角色需要在已声明的食堂房间内。", "Actor needs to be in a declared Canteen room.") },
            { CampusPrankTextId.NoDeliveryWaiting, new Entry("没有学生外卖正在已声明的室外取餐点等待。", "No student delivery order is waiting at a declared outdoor delivery point.") },
            { CampusPrankTextId.ActorNeedsOutdoorDelivery, new Entry("角色需要在已声明的室外外卖区域。", "Actor needs to be in a declared Outdoor delivery area.") },
            { CampusPrankTextId.CanteenTheftLog, new Entry("[食堂] 店员状态={0}。{1} 拿走了 {2}。", "[Canteen] Clerk state={0}. {1} picked up {2}.") },
            { CampusPrankTextId.DeliveryTakenLikelyReported, new Entry("[外卖] {0} 拿走了 {1}。失主很可能会报告。", "[Delivery] {0} took {1}. The owner is likely to report it.") },
            { CampusPrankTextId.DeliveryTakenBeforeOwner, new Entry("[外卖] {0} 在失主到达前拿走了 {1}。", "[Delivery] {0} took {1} before the owner arrived.") },
            { CampusPrankTextId.SecretDeliveryWaiting, new Entry("[外卖] {0} 有一份秘密外卖正在等待：{1}。", "[Delivery] {0} has a secret delivery waiting: {1}.") },
            { CampusPrankTextId.DeliveryPickedUp, new Entry("[外卖] {0} 取走了 {1}。", "[Delivery] {0} picked up {1}.") },
            { CampusPrankTextId.DeliveryMissingFound, new Entry("[外卖] {0} 到达取餐点，发现外卖不见了。", "[Delivery] {0} reached the pickup point and found the delivery missing.") },
            { CampusPrankTextId.DeliveryReportedMissing, new Entry("[外卖] {0} 报告了外卖丢失。", "[Delivery] {0} reported the missing delivery.") },
            { CampusPrankTextId.DeliveryGaveUpSearching, new Entry("[外卖] {0} 放弃寻找 {1}。", "[Delivery] {0} gave up searching for {1}.") },
            { CampusPrankTextId.FailedCreateStolenItem, new Entry("无法创建偷取物品定义：{0}。", "Failed to create stolen item definition: {0}.") },
            { CampusPrankTextId.StolenItemDescription, new Entry("从 {0} 偷来。气味={1}，怀疑风险={2}。", "Stolen from {0}. Smell={1}, suspicion risk={2}.") },
            { CampusPrankTextId.AteItem, new Entry("吃掉了 {0}。", "Ate {0}.") },
            { CampusPrankTextId.RuntimeStolenItemDescription, new Entry("运行时偷取物品。来源={0}。", "Runtime stolen item. Source={0}.") },
            { CampusPrankTextId.SuspicionIncrease, new Entry("[怀疑] +{0}（{1}）。总计={2}。", "[Suspicion] +{0} ({1}). Total={2}.") },
            { CampusPrankTextId.SuspicionThresholdReached, new Entry("[怀疑] 达到阈值，已排入口头警告/查包后果。当日警告={0}。", "[Suspicion] Threshold reached. A warning/check-bag consequence is queued. Daily warnings={0}.") },
            { CampusPrankTextId.ActionCoolingDown, new Entry("[恶作剧] {0} 仍在冷却。", "[Prank] {0} is still cooling down.") },
            { CampusPrankTextId.PrankActionCoolingDown, new Entry("恶作剧动作正在冷却。", "Prank action is cooling down.") },
            { CampusPrankTextId.DeliveryWaitingPrompt, new Entry("外卖等待中：{0} 给 {1}。", "Delivery waiting: {0} for {1}.") },
            { CampusPrankTextId.DeliveryOwnerLookingPrompt, new Entry("外卖失主正在寻找：{0}。", "Delivery owner is looking for: {0}.") },
            { CampusPrankTextId.DeliveryOwnerReportingPrompt, new Entry("外卖失主正在报告丢单。", "Delivery owner is reporting a missing order.") },
            { CampusPrankTextId.CanteenClerkStatePrompt, new Entry("食堂店员状态：{0}。", "Canteen clerk state: {0}.") },
            { CampusPrankTextId.DefaultPromptNoClass, new Entry("放置已声明恶作剧点来进行食堂食物或外卖偷取。", "Place declared prank spots for canteen food or delivery theft.") },
            { CampusPrankTextId.DefaultPromptClass, new Entry("上课时可传纸条，也可溜到已声明的食堂/外卖点。", "Pass Note available during class, or sneak out to declared canteen/delivery spots.") },
            { CampusPrankTextId.ActorFallback, new Entry("角色", "Actor") },
            { CampusPrankTextId.PassNoteActionName, new Entry("传纸条", "Pass note") },
            { CampusPrankTextId.DeliveryActionName, new Entry("外卖", "delivery") },
            { CampusPrankTextId.DeliveryOwnerReportsLossReason, new Entry("外卖失主报告丢失", "delivery owner reports the loss") },
            { CampusPrankTextId.StolenDeliveryReason, new Entry("偷取外卖", "stolen delivery") },
            { CampusPrankTextId.DeliveryOwnerReportedMissingReason, new Entry("外卖失主报告了丢单", "delivery owner reported a missing delivery") },
            { CampusPrankTextId.StolenFriedChicken, new Entry("偷来的炸鸡", "stolen fried chicken") },
            { CampusPrankTextId.StolenBurger, new Entry("偷来的汉堡", "stolen burger") },
            { CampusPrankTextId.StolenOdenCup, new Entry("偷来的关东煮杯", "stolen oden cup") },
            { CampusPrankTextId.DeliveryFriedChickenRice, new Entry("炸鸡饭", "fried chicken rice") },
            { CampusPrankTextId.DeliveryMilkTea, new Entry("奶茶", "milk tea") },
            { CampusPrankTextId.DeliverySpicyNoodles, new Entry("麻辣面", "spicy noodles") },
            { CampusPrankTextId.DeliveryBurgerSet, new Entry("汉堡套餐", "burger set") },
            { CampusPrankTextId.DeliveryOdenCup, new Entry("关东煮杯", "oden cup") },
            { CampusPrankTextId.StolenDeliveryItem, new Entry("偷来的{0}", "stolen {0}") },
            { CampusPrankTextId.OutdoorNeedsDeliveryDropPoint, new Entry("这个室外区域需要 DeliveryDropPoint 设施。", "This outdoor area needs a DeliveryDropPoint facility.") },
            { CampusPrankTextId.DefaultPrankSpot, new Entry("缺德点", "Prank Spot") },
            { CampusPrankTextId.DefaultUnsupportedPrankSpot, new Entry("该缺德点还没接入正式玩法。", "This prank spot is not wired into formal gameplay yet.") }
        };

        public static string Get(CampusPrankTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusPrankTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return language switch
            {
                CampusDisplayLanguage.English => entry.English,
                CampusDisplayLanguage.Bilingual => entry.Chinese + " / " + entry.English,
                _ => entry.Chinese
            };
        }

        public static string Format(CampusPrankTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static CampusLocalizedText Localized(CampusPrankTextId id, params object[] args)
        {
            string chinese = args != null && args.Length > 0
                ? string.Format(Get(CampusDisplayLanguage.Chinese, id), args)
                : Get(CampusDisplayLanguage.Chinese, id);
            string english = args != null && args.Length > 0
                ? string.Format(Get(CampusDisplayLanguage.English, id), args)
                : Get(CampusDisplayLanguage.English, id);
            return new CampusLocalizedText(chinese, english);
        }
    }
}
