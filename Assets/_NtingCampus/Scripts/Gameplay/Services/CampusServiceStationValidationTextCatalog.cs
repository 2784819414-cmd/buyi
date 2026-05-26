using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.Gameplay.Services
{
    internal enum CampusServiceStationValidationTextId
    {
        MissingId = 0,
        UnknownType = 1,
        MissingRoom = 2,
        InvalidRoomType = 3,
        MissingOwner = 4,
        InvalidOwnerType = 5,
        OwnerRoomMismatch = 6,
        SlotBelowMinimum = 7,
        SlotAboveMaximum = 8,
        SlotMissingFacility = 9,
        SlotInvalidFacilityType = 10,
        SlotRoomMismatch = 11,
        ReferenceMissing = 12,
        FailedToParsePreset = 13
    }

    internal static class CampusServiceStationValidationTextCatalog
    {
        private static readonly Dictionary<CampusServiceStationValidationTextId, Entry> Entries = new()
        {
            { CampusServiceStationValidationTextId.MissingId, new Entry("服务站缺少稳定 Id。", "Service station is missing Id.") },
            { CampusServiceStationValidationTextId.UnknownType, new Entry("服务站引用了未知 StationTypeId：{0}。", "Service station references unknown StationTypeId: {0}.") },
            { CampusServiceStationValidationTextId.MissingRoom, new Entry("服务站引用了不存在的房间：{0}。", "Service station references a missing room: {0}.") },
            { CampusServiceStationValidationTextId.InvalidRoomType, new Entry("服务站类型 {0} 不能放在房间类型 {1}。", "Service station type {0} is not allowed in room type {1}.") },
            { CampusServiceStationValidationTextId.MissingOwner, new Entry("服务站所属设施不存在：{0}。", "Service station owner facility is missing: {0}.") },
            { CampusServiceStationValidationTextId.InvalidOwnerType, new Entry("服务站所属设施类型 {0} 不被 {1} 允许。", "Service station owner type {0} is not allowed by {1}.") },
            { CampusServiceStationValidationTextId.OwnerRoomMismatch, new Entry("服务站所属设施必须和服务站在同一房间。", "Service station owner facility must be in the same room as the station.") },
            { CampusServiceStationValidationTextId.SlotBelowMinimum, new Entry("服务站槽位 {0} 至少需要 {1} 个设施点。", "Service station slot {0} needs at least {1} facility point(s).") },
            { CampusServiceStationValidationTextId.SlotAboveMaximum, new Entry("服务站槽位 {0} 最多允许 {1} 个设施点。", "Service station slot {0} allows at most {1} facility point(s).") },
            { CampusServiceStationValidationTextId.SlotMissingFacility, new Entry("服务站槽位 {0} 引用了不存在的设施：{1}。", "Service station slot {0} references missing facility: {1}.") },
            { CampusServiceStationValidationTextId.SlotInvalidFacilityType, new Entry("服务站槽位 {0} 不能使用设施类型 {1}。", "Service station slot {0} cannot use facility type {1}.") },
            { CampusServiceStationValidationTextId.SlotRoomMismatch, new Entry("服务站槽位 {0} 的设施必须和服务站在同一房间。", "Service station slot {0} facility must be in the same room as the station.") },
            { CampusServiceStationValidationTextId.ReferenceMissing, new Entry("{0} 引用了不存在的服务站：{1}。", "{0} references a missing service station: {1}.") },
            { CampusServiceStationValidationTextId.FailedToParsePreset, new Entry("[CampusServiceStationPresetCatalog] 解析 {0} 失败：{1}", "[CampusServiceStationPresetCatalog] Failed to parse {0}: {1}") }
        };

        public static string Get(CampusServiceStationValidationTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());
            return entry.Get(CampusLanguageState.CurrentLanguage);
        }

        public static string Format(CampusServiceStationValidationTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}
