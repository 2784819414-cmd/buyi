using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Economy
{
    public enum CampusCommerceTextId
    {
        WaitingForShelves = 0,
        MissingShelfOrContainer = 1,
        NotStoreShelf = 2,
        Restocked = 3,
        ShelfAlreadyStocked = 4,
        MissingActorOrCheckout = 5,
        NotStoreCheckout = 6,
        CheckoutNotInsideStore = 7,
        ActorNotInsideStore = 8,
        NoCheckoutCounter = 9,
        NotCloseEnoughToCheckout = 10,
        NoUsableShelf = 11,
        NotCloseEnoughToShelf = 12,
        ShelfNoMerchandise = 13,
        ShelfNoAvailableMerchandise = 14,
        TookFromShelfLog = 15,
        MissingActorOrStoreRoom = 16,
        NoClerkAtCheckout = 17,
        NoUnpaidItem = 18,
        CheckedOut = 19,
        CheckedOutLog = 20,
        LeftWithUnpaidLog = 21,
        StoreCheckout = 22,
        Summary = 23,
        UnknownActor = 24,
        MissingActorOrShelf = 25,
        ShelfNotInsideStore = 26
    }

    public static class CampusCommerceTextCatalog
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

        private static readonly Dictionary<CampusCommerceTextId, Entry> Entries = new Dictionary<CampusCommerceTextId, Entry>
        {
            { CampusCommerceTextId.WaitingForShelves, new Entry("商店交易正在等待货架。", "Store commerce is waiting for store shelves.") },
            { CampusCommerceTextId.MissingShelfOrContainer, new Entry("缺少货架或储物容器。", "Missing shelf or storage container.") },
            { CampusCommerceTextId.NotStoreShelf, new Entry("目标不是商店货架。", "Object is not a store shelf.") },
            { CampusCommerceTextId.Restocked, new Entry("已补货 {0} 件。", "Restocked {0} item(s).") },
            { CampusCommerceTextId.ShelfAlreadyStocked, new Entry("货架库存已满。", "Shelf is already stocked.") },
            { CampusCommerceTextId.MissingActorOrCheckout, new Entry("缺少角色或收银台。", "Missing actor or checkout counter.") },
            { CampusCommerceTextId.NotStoreCheckout, new Entry("目标不是商店收银台。", "Object is not a store checkout.") },
            { CampusCommerceTextId.CheckoutNotInsideStore, new Entry("收银台不在商店房间内。", "Checkout is not inside a store room.") },
            { CampusCommerceTextId.ActorNotInsideStore, new Entry("角色不在商店内。", "Actor is not inside a store.") },
            { CampusCommerceTextId.NoCheckoutCounter, new Entry("商店没有收银台。", "Store has no checkout counter.") },
            { CampusCommerceTextId.NotCloseEnoughToCheckout, new Entry("角色离收银台不够近。", "Actor is not close enough to the checkout counter.") },
            { CampusCommerceTextId.NoUsableShelf, new Entry("商店没有可用货架。", "Store has no usable shelf.") },
            { CampusCommerceTextId.NotCloseEnoughToShelf, new Entry("角色离货架不够近。", "Actor is not close enough to the shelf.") },
            { CampusCommerceTextId.ShelfNoMerchandise, new Entry("货架上没有商品。", "Shelf has no merchandise.") },
            { CampusCommerceTextId.ShelfNoAvailableMerchandise, new Entry("货架上没有可拿取商品。", "Shelf has no available merchandise.") },
            { CampusCommerceTextId.TookFromShelfLog, new Entry("[商店] {0} 从 {2} 拿起了 {1}。", "[Store] {0} took {1} from {2}.") },
            { CampusCommerceTextId.MissingActorOrStoreRoom, new Entry("缺少角色或商店房间。", "Missing actor or store room.") },
            { CampusCommerceTextId.NoClerkAtCheckout, new Entry("没有店员站在收银台。", "No store clerk is standing at the checkout.") },
            { CampusCommerceTextId.NoUnpaidItem, new Entry("没有携带未结账商品。", "No unpaid store item is being carried.") },
            { CampusCommerceTextId.CheckedOut, new Entry("已结账 {0} 件。价格={1}。", "Checked out {0} item(s). Price={1}.") },
            { CampusCommerceTextId.CheckedOutLog, new Entry("[商店] {0} 为 {1} 结账 {2} 件，价格={3}。", "[Store] {0} checked out {2} item(s) for {1}. Price={3}.") },
            { CampusCommerceTextId.LeftWithUnpaidLog, new Entry("[商店] {0} 带着未结账的 {1} 离开了商店。", "[Store] {0} left the store with unpaid {1}.") },
            { CampusCommerceTextId.StoreCheckout, new Entry("商店收银台", "Store checkout") },
            { CampusCommerceTextId.Summary, new Entry("货架={0}，补货={1}，携带未付款={2}，今日付款={3}，今日偷窃={4}。", "Store shelves={0}, restocked={1}, unpaid carried={2}, paid items today={3}, thefts today={4}.") },
            { CampusCommerceTextId.UnknownActor, new Entry("未知角色", "Unknown") },
            { CampusCommerceTextId.MissingActorOrShelf, new Entry("缺少角色或商店货架。", "Missing actor or store shelf.") },
            { CampusCommerceTextId.ShelfNotInsideStore, new Entry("货架不在商店房间内。", "Shelf is not inside a store room.") }
        };

        public static string Get(CampusCommerceTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusCommerceTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return Resolve(language, entry.Chinese, entry.English);
        }

        public static string Format(CampusCommerceTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
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
