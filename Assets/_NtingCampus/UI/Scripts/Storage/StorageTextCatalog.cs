using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

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
        HandsFullPickup = 53,
        CloseButton = 54,
        BackpackEquipmentSlot = 55,
        BackpackAlreadyEquipped = 56,
        BackpackEquipped = 57,
        BackpackDropped = 58,
        GeneratedUiSprites = 59,
        MemoryLoadSkippedInvalidPosition = 60,
        MemoryContainerIdEmpty = 61,
        MemoryMissingContainer = 62,
        MemoryMissingItemRegistry = 63,
        MemoryKeptPreviousSize = 64,
        ItemRegistryMissingDefinition = 65,
        StatusLog = 66
    }

    public static class StorageTextCatalog
    {
        private static readonly Dictionary<StorageTextId, Entry> Entries = new()
        {
            { StorageTextId.WindowEyebrow, new Entry("物品收纳", "Inventory") },
            { StorageTextId.WindowTitle, new Entry("随身与容器", "Carried Items And Container") },
            { StorageTextId.WindowHint, new Entry("拖拽物品，右键旋转，双击快速转移，拖出窗口可丢到地面，Esc 关闭。", "Drag items, right click to rotate, double click to transfer, drag outside to drop, Esc closes.") },
            { StorageTextId.PlayerWindowHint, new Entry("拖拽物品，右键旋转，双击快速转移，从手部格使用物品，Esc 关闭。", "Drag items, right click to rotate, double click to transfer, use items from hand slots, Esc closes.") },
            { StorageTextId.PocketTab, new Entry("口袋", "Pockets") },
            { StorageTextId.BackpackTab, new Entry("背包", "Backpack") },
            { StorageTextId.BackpackUnavailableTab, new Entry("背包 / 未装备", "Backpack / Not Equipped") },
            { StorageTextId.HandAndBackpack, new Entry("手部 / 背包", "Hands / Backpack") },
            { StorageTextId.HandAndPockets, new Entry("手部 / 口袋", "Hands / Pockets") },
            { StorageTextId.HandsAndPocketsMeta, new Entry("2 只手 + 4 个口袋", "2 hands + 4 pockets") },
            { StorageTextId.ExternalContainer, new Entry("外部容器", "External Container") },
            { StorageTextId.NoBackpack, new Entry("未装备背包。", "Backpack is not equipped.") },
            { StorageTextId.NoBackpackPage, new Entry("当前角色没有可用背包页。", "This character does not have a backpack page.") },
            { StorageTextId.LeftHand, new Entry("左手", "Left Hand") },
            { StorageTextId.RightHand, new Entry("右手", "Right Hand") },
            { StorageTextId.LeftChestPocket, new Entry("左胸袋", "Left Chest Pocket") },
            { StorageTextId.RightChestPocket, new Entry("右胸袋", "Right Chest Pocket") },
            { StorageTextId.LeftPantsPocket, new Entry("左裤袋", "Left Pants Pocket") },
            { StorageTextId.RightPantsPocket, new Entry("右裤袋", "Right Pants Pocket") },
            { StorageTextId.StudentBackpack, new Entry("学生背包", "Student Backpack") },
            { StorageTextId.ItemInfo, new Entry("物品信息", "Item Info") },
            { StorageTextId.NoItemSelected, new Entry("未选中物品", "No Item Selected") },
            { StorageTextId.InspectItemHint, new Entry("点击物品查看详情。", "Click an item to inspect it.") },
            { StorageTextId.Use, new Entry("使用", "Use") },
            { StorageTextId.Status, new Entry("状态", "Status") },
            { StorageTextId.Rotated, new Entry("已旋转：{0}", "Rotated: {0}") },
            { StorageTextId.RotationBlocked, new Entry("旋转后空间不足。", "Not enough space after rotation.") },
            { StorageTextId.TargetBlocked, new Entry("目标空间被占用。", "Target space is blocked.") },
            { StorageTextId.CouldNotDropToGround, new Entry("无法将物品放到地面。", "Could not drop the item to ground.") },
            { StorageTextId.MissingItem, new Entry("缺少物品。", "Missing item.") },
            { StorageTextId.MissingTargetContainer, new Entry("缺少目标容器。", "Missing target container.") },
            { StorageTextId.MissingItemOrSource, new Entry("缺少物品或来源容器。", "Missing item or source container.") },
            { StorageTextId.MissingSourceContainer, new Entry("缺少来源容器。", "Missing source container.") },
            { StorageTextId.CouldNotRemoveFromSource, new Entry("无法从来源容器移除物品。", "Could not remove the item from the source container.") },
            { StorageTextId.MovedItem, new Entry("已移动 {0}。", "Moved {0}.") },
            { StorageTextId.GroundDropNotConfigured, new Entry("尚未配置地面放置上下文。", "Ground drop context is not configured.") },
            { StorageTextId.ItemCannotBeUsed, new Entry("该物品不能直接使用。", "This item cannot be used directly.") },
            { StorageTextId.UnsupportedUseAction, new Entry("不支持的物品使用动作：{0}。", "Unsupported item use action: {0}.") },
            { StorageTextId.MoveIntoHandFirst, new Entry("请先把它移到手部格。", "Move it into a hand slot first.") },
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
            { StorageTextId.NoValidFloorPropsRoot, new Entry("没有找到可用的楼层 PropsRoot。", "No valid floor PropsRoot was found.") },
            { StorageTextId.ItemHasNoGroundSprite, new Entry("物品没有地面精灵。", "Item has no ground sprite.") },
            { StorageTextId.NoFreeFloorCellNearby, new Entry("附近没有可用的地面格。", "No free floor cell nearby.") },
            { StorageTextId.PendingCheckout, new Entry("待结算", "Pending Checkout") },
            { StorageTextId.HandsFullPickup, new Entry("手上已经拿不下更多东西了。", "My hands are already full.") },
            { StorageTextId.CloseButton, new Entry("×", "×") },
            { StorageTextId.BackpackEquipmentSlot, new Entry("背包栏", "Backpack Slot") },
            { StorageTextId.BackpackAlreadyEquipped, new Entry("已经装备了背包。", "Backpack is already equipped.") },
            { StorageTextId.BackpackEquipped, new Entry("已装备背包。", "Equipped backpack.") },
            { StorageTextId.BackpackDropped, new Entry("已放下背包。", "Dropped backpack.") },
            { StorageTextId.GeneratedUiSprites, new Entry("Storage UI sprites 已生成到 {0}。", "Storage UI sprites generated at {0}.") },
            { StorageTextId.MemoryLoadSkippedInvalidPosition, new Entry("Storage memory load 跳过物品 {0}，因为保存位置无效。", "Storage memory load skipped item '{0}' because its saved position is invalid.") },
            { StorageTextId.MemoryContainerIdEmpty, new Entry("Storage memory 失败：container id 为空。", "Storage memory failed: container id is empty.") },
            { StorageTextId.MemoryMissingContainer, new Entry("Storage memory 失败：缺少容器 {0}。", "Storage memory failed: missing container '{0}'.") },
            { StorageTextId.MemoryMissingItemRegistry, new Entry("Storage memory 失败：item registry 未分配。", "Storage memory failed: item registry is not assigned.") },
            { StorageTextId.MemoryKeptPreviousSize, new Entry("Storage memory 保留容器 {0} 的旧尺寸，因为现有物品会越界。", "Storage memory kept previous size for container '{0}' because existing items would be out of bounds.") },
            { StorageTextId.ItemRegistryMissingDefinition, new Entry("Storage item registry 失败：缺少物品定义 {0}。", "Storage item registry failed: missing item definition '{0}'.") },
            { StorageTextId.StatusLog, new Entry("[Storage] {0}", "[Storage] {0}") }
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

        public static string Resolve(
            CampusDisplayLanguage language,
            string chinese,
            string english,
            string traditionalChinese = null,
            string russian = null,
            string japanese = null)
        {
            return CampusDisplayLanguageCatalog.Resolve(language, chinese, english, traditionalChinese, russian, japanese);
        }
    }
}
