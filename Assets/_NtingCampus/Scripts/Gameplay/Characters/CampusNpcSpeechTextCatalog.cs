using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcSpeechTextId
    {
        StudentDeliveryArrived = 0,
        StudentNeedsDesk = 1,
        StudentDeciding = 2,
        StudentBackToDesk = 3,
        StudentOrderingDelivery = 4,
        StudentPickingUpDelivery = 5,
        StudentGoingStoreShelf = 6,
        StudentGoingCheckout = 7,
        StudentBackToDorm = 8,
        StudentHeadingOut = 9,
        DeliveryIsHere = 10,
        DeliveryOrderPlaced = 11,
        DeliveryPickedUp = 12,
        StudentGoingCanteenMeal = 13,
        StaffRegisterOpen = 20,
        StaffCheckingShelves = 21,
        StaffWatchingDeliveryArea = 22,
        StaffCounterOpen = 23,
        StaffCoveringWindows = 24,
        StaffCounterDuty = 25,
        StaffCoveringWindowsIntent = 26,
        StaffRegisterDuty = 27,
        StaffCheckingShelvesIntent = 28,
        StaffWatchingDeliveriesIntent = 29,
        StaffHeadingOut = 30,
        TeacherClassInProgress = 40,
        TeacherBackOfficeInteractive = 41,
        TeacherHeadingToClass = 42,
        TeacherBackOffice = 43,
        TeacherHeadingOut = 44
    }

    public static class CampusNpcSpeechTextCatalog
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

        private static readonly Dictionary<CampusNpcSpeechTextId, Entry> Entries =
            new Dictionary<CampusNpcSpeechTextId, Entry>
            {
                { CampusNpcSpeechTextId.StudentDeliveryArrived, new Entry("我的外卖到了。", "My delivery arrived.") },
                { CampusNpcSpeechTextId.StudentNeedsDesk, new Entry("我要回到座位。", "I need to get to my desk.") },
                { CampusNpcSpeechTextId.StudentDeciding, new Entry("我在想接下来做什么。", "I am deciding what to do next.") },
                { CampusNpcSpeechTextId.StudentBackToDesk, new Entry("回到我的座位。", "Back to my desk.") },
                { CampusNpcSpeechTextId.StudentOrderingDelivery, new Entry("正在点外卖。", "Ordering delivery.") },
                { CampusNpcSpeechTextId.StudentPickingUpDelivery, new Entry("去取外卖。", "Picking up delivery.") },
                { CampusNpcSpeechTextId.StudentGoingStoreShelf, new Entry("去商店货架。", "Going to the store shelf.") },
                { CampusNpcSpeechTextId.StudentGoingCheckout, new Entry("去收银台。", "Going to the checkout.") },
                { CampusNpcSpeechTextId.StudentBackToDorm, new Entry("回宿舍。", "Back to the dorm.") },
                { CampusNpcSpeechTextId.StudentHeadingOut, new Entry("我要出发了。", "I am heading out.") },
                { CampusNpcSpeechTextId.DeliveryIsHere, new Entry("外卖到了。", "Delivery is here.") },
                { CampusNpcSpeechTextId.DeliveryOrderPlaced, new Entry("订单已提交。", "Order placed.") },
                { CampusNpcSpeechTextId.DeliveryPickedUp, new Entry("拿到外卖了。", "Got my delivery.") },
                { CampusNpcSpeechTextId.StudentGoingCanteenMeal, new Entry("去食堂窗口取餐。", "Going to the canteen window.") },
                { CampusNpcSpeechTextId.StaffRegisterOpen, new Entry("收银台开放中。", "The register is open.") },
                { CampusNpcSpeechTextId.StaffCheckingShelves, new Entry("我在检查货架。", "I am checking the shelves.") },
                { CampusNpcSpeechTextId.StaffWatchingDeliveryArea, new Entry("我在看着外卖区。", "I am watching the delivery area.") },
                { CampusNpcSpeechTextId.StaffCounterOpen, new Entry("窗口开放中。", "The counter is open.") },
                { CampusNpcSpeechTextId.StaffCoveringWindows, new Entry("我在负责窗口。", "I am covering the windows.") },
                { CampusNpcSpeechTextId.StaffCounterDuty, new Entry("窗口值班。", "Counter duty.") },
                { CampusNpcSpeechTextId.StaffCoveringWindowsIntent, new Entry("去负责窗口。", "Covering the windows.") },
                { CampusNpcSpeechTextId.StaffRegisterDuty, new Entry("收银台值班。", "Register duty.") },
                { CampusNpcSpeechTextId.StaffCheckingShelvesIntent, new Entry("检查货架。", "Checking shelves.") },
                { CampusNpcSpeechTextId.StaffWatchingDeliveriesIntent, new Entry("看着外卖。", "Watching deliveries.") },
                { CampusNpcSpeechTextId.StaffHeadingOut, new Entry("我要出去了。", "I am heading out.") },
                { CampusNpcSpeechTextId.TeacherClassInProgress, new Entry("正在上课。", "Class is in progress.") },
                { CampusNpcSpeechTextId.TeacherBackOfficeInteractive, new Entry("我正要回办公室。", "I am heading back to the office.") },
                { CampusNpcSpeechTextId.TeacherHeadingToClass, new Entry("去上课。", "Heading to class.") },
                { CampusNpcSpeechTextId.TeacherBackOffice, new Entry("回办公室。", "Back to the office.") },
                { CampusNpcSpeechTextId.TeacherHeadingOut, new Entry("我要出去了。", "I am heading out.") }
            };

        public static string Get(CampusNpcSpeechTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            switch (CampusLanguageState.CurrentLanguage)
            {
                case CampusDisplayLanguage.English:
                    return entry.English;
                case CampusDisplayLanguage.Bilingual:
                    return entry.Chinese + " / " + entry.English;
                default:
                    return entry.Chinese;
            }
        }
    }
}
