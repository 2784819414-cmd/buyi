using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;

namespace Nting.Storage
{
    public enum StorageTextId
    {
        WindowEyebrow = 0,
        WindowTitle = 1,
        WindowHint = 2,
        PlayerWindowHint = 3,
        PocketTab = 4,
        BackpackTab = 5,
        BackpackUnavailableTab = 6,
        HandAndBackpack = 7,
        HandAndPockets = 8,
        HandsAndPocketsMeta = 9,
        ExternalContainer = 10,
        NoBackpack = 11,
        NoBackpackPage = 12,
        LeftHand = 13,
        RightHand = 14,
        LeftChestPocket = 15,
        RightChestPocket = 16,
        LeftPantsPocket = 17,
        RightPantsPocket = 18,
        StudentBackpack = 19,
        ItemInfo = 20,
        NoItemSelected = 21,
        InspectItemHint = 22,
        Use = 23,
        Status = 24,
        Rotated = 25,
        RotationBlocked = 26,
        TargetBlocked = 27,
        CouldNotDropToGround = 28,
        MissingItem = 29,
        MissingTargetContainer = 30,
        MissingItemOrSource = 31,
        MissingSourceContainer = 32,
        CouldNotRemoveFromSource = 33,
        MovedItem = 34,
        GroundDropNotConfigured = 35,
        ItemCannotBeUsed = 36,
        UnsupportedUseAction = 37,
        MoveIntoHandFirst = 38,
        UsedItem = 39,
        CouldNotConsumeItem = 40,
        ItemFallback = 41,
        UsableFromHand = 42,
        Stolen = 43,
        From = 44,
        Risk = 45,
        TargetSpaceInsufficient = 46,
        TestBox = 47,
        CouldNotRebuildDroppedItem = 48,
        NoValidFloorPropsRoot = 49,
        ItemHasNoGroundSprite = 50,
        NoFreeFloorCellNearby = 51,
        PendingCheckout = 52,
        HandsFullPickup = 53
    }

    public static class StorageTextCatalog
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

        private static readonly Dictionary<StorageTextId, Entry> Entries = new()
        {
            { StorageTextId.WindowEyebrow, new Entry("物品储存", "Item Storage") },
            { StorageTextId.WindowTitle, new Entry("储物空间", "Storage") },
            { StorageTextId.WindowHint, new Entry("拖拽物品 / 右键旋转 / 双击转移 / 拖出容器可放到地上 / Esc 关闭", "Drag items / right click rotate / double click transfer / drag out to drop / Esc closes") },
            { StorageTextId.PlayerWindowHint, new Entry("拖拽物品 / 右键旋转 / 双击转移 / 从手部格使用 / Esc 关闭", "Drag items / right click rotate / double click transfer / use from hand slots / Esc closes") },
            { StorageTextId.PocketTab, new Entry("口袋", "Pockets") },
            { StorageTextId.BackpackTab, new Entry("背包", "Backpack") },
            { StorageTextId.BackpackUnavailableTab, new Entry("背包 / 未装备", "Backpack / Not Equipped") },
            { StorageTextId.HandAndBackpack, new Entry("手持 / 学生书包", "Hands / School Backpack") },
            { StorageTextId.HandAndPockets, new Entry("手持 / 衣服口袋", "Hands / Clothing Pockets") },
            { StorageTextId.HandsAndPocketsMeta, new Entry("2 只手 + 4 个口袋", "2 hands + 4 pockets") },
            { StorageTextId.ExternalContainer, new Entry("外部容器", "External Container") },
            { StorageTextId.NoBackpack, new Entry("未装备背包。", "Backpack is not equipped.") },
            { StorageTextId.NoBackpackPage, new Entry("该页签暂不可用。", "This tab is unavailable.") },
            { StorageTextId.LeftHand, new Entry("左手", "Left Hand") },
            { StorageTextId.RightHand, new Entry("右手", "Right Hand") },
            { StorageTextId.LeftChestPocket, new Entry("左胸袋", "Left Chest Pocket") },
            { StorageTextId.RightChestPocket, new Entry("右胸袋", "Right Chest Pocket") },
            { StorageTextId.LeftPantsPocket, new Entry("左裤袋", "Left Pants Pocket") },
            { StorageTextId.RightPantsPocket, new Entry("右裤袋", "Right Pants Pocket") },
            { StorageTextId.StudentBackpack, new Entry("学生书包", "School Backpack") },
            { StorageTextId.ItemInfo, new Entry("物品信息", "Item Info") },
            { StorageTextId.NoItemSelected, new Entry("未选中物品", "No item selected") },
            { StorageTextId.InspectItemHint, new Entry("点击物品查看详情", "Click an item to inspect it.") },
            { StorageTextId.Use, new Entry("使用", "Use") },
            { StorageTextId.Status, new Entry("操作提示", "Status") },
            { StorageTextId.Rotated, new Entry("已旋转：{0}", "Rotated: {0}") },
            { StorageTextId.RotationBlocked, new Entry("旋转后空间不足。", "Not enough space after rotation.") },
            { StorageTextId.TargetBlocked, new Entry("目标空间被占用。", "Target space is blocked.") },
            { StorageTextId.CouldNotDropToGround, new Entry("无法将物品放到地上。", "Could not drop item to ground.") },
            { StorageTextId.MissingItem, new Entry("缺少物品。", "Missing item.") },
            { StorageTextId.MissingTargetContainer, new Entry("缺少目标容器。", "Missing target container.") },
            { StorageTextId.MissingItemOrSource, new Entry("缺少物品或来源容器。", "Missing item or source container.") },
            { StorageTextId.MissingSourceContainer, new Entry("缺少来源容器。", "Missing source container.") },
            { StorageTextId.CouldNotRemoveFromSource, new Entry("无法从来源容器移除物品。", "Could not remove item from source container.") },
            { StorageTextId.MovedItem, new Entry("已移动 {0}。", "Moved {0}.") },
            { StorageTextId.GroundDropNotConfigured, new Entry("地面放置尚未配置。", "Ground drop is not configured.") },
            { StorageTextId.ItemCannotBeUsed, new Entry("该物品不能使用。", "This item cannot be used.") },
            { StorageTextId.UnsupportedUseAction, new Entry("不支持的物品使用动作：{0}。", "Unsupported item use action: {0}.") },
            { StorageTextId.MoveIntoHandFirst, new Entry("请先把它移动到手部格。", "Move it into a hand slot first.") },
            { StorageTextId.UsedItem, new Entry("已使用 {0}。", "Used {0}.") },
            { StorageTextId.CouldNotConsumeItem, new Entry("无法消耗 {0}。", "Could not consume {0}.") },
            { StorageTextId.ItemFallback, new Entry("物品", "item") },
            { StorageTextId.UsableFromHand, new Entry("可从手部格使用", "usable from hand") },
            { StorageTextId.Stolen, new Entry("赃物", "stolen") },
            { StorageTextId.From, new Entry("来自", "from") },
            { StorageTextId.Risk, new Entry("风险", "risk") },
            { StorageTextId.TargetSpaceInsufficient, new Entry("目标空间不足。", "Not enough target space.") },
            { StorageTextId.TestBox, new Entry("测试箱", "Test Box") },
            { StorageTextId.CouldNotRebuildDroppedItem, new Entry("无法重建掉落物品。", "Could not rebuild dropped item.") },
            { StorageTextId.NoValidFloorPropsRoot, new Entry("没有找到可用的楼层道具根节点。", "No valid floor props root was found.") },
            { StorageTextId.ItemHasNoGroundSprite, new Entry("物品没有地面图标。", "Item has no ground sprite.") },
            { StorageTextId.NoFreeFloorCellNearby, new Entry("附近没有可用的地面格。", "No free floor cell nearby.") },
            { StorageTextId.PendingCheckout, new Entry("待结账", "pending checkout") },
            { StorageTextId.HandsFullPickup, new Entry("我拿不下更多东西了", "I can't carry anything else.") }
        };

        public static string Get(StorageTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, StorageTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return Resolve(language, entry.Chinese, entry.English);
        }

        public static string Format(StorageTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Format(CampusDisplayLanguage language, StorageTextId id, params object[] args)
        {
            return string.Format(Get(language, id), args);
        }

        public static string Resolve(CampusDisplayLanguage language, string chinese, string english)
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
