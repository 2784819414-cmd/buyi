using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.Gameplay.Rooms
{
    public enum CampusRoomValidationTextId
    {
        LegacyRoomTypeInference = 0,
        UnknownRoomTypeSource = 1,
        RegistryNull = 2,
        NullRoomEntry = 3,
        MissingRoomId = 4,
        DuplicateRoomId = 5,
        MissingDisplayName = 6,
        MissingMarkerCells = 7,
        InvalidBounds = 8,
        UnknownRoomType = 9,
        MissingCoreFacility = 10,
        RoomNull = 11,
        MissingFormalRoomType = 12,
        ReadyForGameplay = 13,
        MapRootMissing = 14,
        RegistrationSummary = 15,
        RoomRegistrationSummary = 16
    }

    public static class CampusRoomValidationTextCatalog
    {
        private static readonly Dictionary<CampusRoomValidationTextId, Entry> Entries = new()
        {
            { CampusRoomValidationTextId.LegacyRoomTypeInference, new Entry("房间类型 {0} 来自旧版房间名推断。正常 mod 数据应使用 CampusGameplayRoomMarker 显式设置 RoomType。", "Room type {0} came from legacy room-name inference. Normal mod data should set RoomType explicitly with CampusGameplayRoomMarker.") },
            { CampusRoomValidationTextId.UnknownRoomTypeSource, new Entry("房间类型来源未知。请添加 CampusGameplayRoomMarker 并显式设置 RoomType。", "Room type source is unknown. Add a CampusGameplayRoomMarker and set RoomType explicitly.") },
            { CampusRoomValidationTextId.RegistryNull, new Entry("房间注册表为空。", "Room registry is null.") },
            { CampusRoomValidationTextId.NullRoomEntry, new Entry("发现空房间条目。", "Null room entry found.") },
            { CampusRoomValidationTextId.MissingRoomId, new Entry("房间缺少稳定 room id。", "Room is missing a room id.") },
            { CampusRoomValidationTextId.DuplicateRoomId, new Entry("房间 id 重复。", "Duplicate room id.") },
            { CampusRoomValidationTextId.MissingDisplayName, new Entry("房间标记名称为空。", "Room marker name is empty.") },
            { CampusRoomValidationTextId.MissingMarkerCells, new Entry("房间没有标记格。", "Room has no marker cells.") },
            { CampusRoomValidationTextId.InvalidBounds, new Entry("房间边界无效。", "Room bounds are invalid.") },
            { CampusRoomValidationTextId.UnknownRoomType, new Entry("房间类型仍是 Unknown。", "Room type is still Unknown.") },
            { CampusRoomValidationTextId.MissingCoreFacility, new Entry("缺少核心设施：{0}。", "Missing core facility: {0}.") },
            { CampusRoomValidationTextId.RoomNull, new Entry("房间为空。", "Room is null.") },
            { CampusRoomValidationTextId.MissingFormalRoomType, new Entry("缺少正式房间类型。", "Missing formal room type.") },
            { CampusRoomValidationTextId.ReadyForGameplay, new Entry("可用于玩法。", "Ready for gameplay.") },
            { CampusRoomValidationTextId.MapRootMissing, new Entry("CampusMapRoot 未找到。", "CampusMapRoot was not found.") },
            { CampusRoomValidationTextId.RegistrationSummary, new Entry("[Rooms] 已注册 {0} 个房间。", "[Rooms] Registered {0} rooms.") },
            { CampusRoomValidationTextId.RoomRegistrationSummary, new Entry("[Rooms][{0}] {1} 标记={2} 设施={3} 有效={4} 可用于玩法={5} 摘要={6}", "[Rooms][{0}] {1} markers={2} facilities={3} valid={4} usable={5} summary={6}") }
        };

        public static string Format(CampusRoomValidationTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Get(CampusRoomValidationTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }
    }
}
