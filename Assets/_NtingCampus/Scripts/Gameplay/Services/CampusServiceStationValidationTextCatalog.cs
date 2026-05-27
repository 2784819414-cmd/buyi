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
        FailedToParsePreset = 13,
        MissingInteractionAction = 14,
        OwnerMissingPlacedObject = 15,
        OwnerMissingInteractionPreset = 16,
        OwnerUnknownInteractionPreset = 17,
        OwnerInteractionActionMissing = 18
    }

    internal static class CampusServiceStationValidationTextCatalog
    {
        private static readonly Dictionary<CampusServiceStationValidationTextId, Entry> Entries = new()
        {
            { CampusServiceStationValidationTextId.MissingId, new Entry("\u670d\u52a1\u7ad9\u7f3a\u5c11\u7a33\u5b9a Id\u3002", "Service station is missing Id.") },
            { CampusServiceStationValidationTextId.UnknownType, new Entry("\u670d\u52a1\u7ad9\u5f15\u7528\u4e86\u672a\u77e5 StationTypeId\uff1a{0}\u3002", "Service station references unknown StationTypeId: {0}.") },
            { CampusServiceStationValidationTextId.MissingRoom, new Entry("\u670d\u52a1\u7ad9\u5f15\u7528\u4e86\u4e0d\u5b58\u5728\u7684\u623f\u95f4\uff1a{0}\u3002", "Service station references a missing room: {0}.") },
            { CampusServiceStationValidationTextId.InvalidRoomType, new Entry("\u670d\u52a1\u7ad9\u7c7b\u578b {0} \u4e0d\u80fd\u653e\u5728\u623f\u95f4\u7c7b\u578b {1}\u3002", "Service station type {0} is not allowed in room type {1}.") },
            { CampusServiceStationValidationTextId.MissingOwner, new Entry("\u670d\u52a1\u7ad9\u6240\u5c5e\u8bbe\u65bd\u4e0d\u5b58\u5728\uff1a{0}\u3002", "Service station owner facility is missing: {0}.") },
            { CampusServiceStationValidationTextId.InvalidOwnerType, new Entry("\u670d\u52a1\u7ad9\u6240\u5c5e\u8bbe\u65bd\u7c7b\u578b {0} \u4e0d\u88ab {1} \u5141\u8bb8\u3002", "Service station owner type {0} is not allowed by {1}.") },
            { CampusServiceStationValidationTextId.OwnerRoomMismatch, new Entry("\u670d\u52a1\u7ad9\u6240\u5c5e\u8bbe\u65bd\u5fc5\u987b\u548c\u670d\u52a1\u7ad9\u5728\u540c\u4e00\u623f\u95f4\u3002", "Service station owner facility must be in the same room as the station.") },
            { CampusServiceStationValidationTextId.SlotBelowMinimum, new Entry("\u670d\u52a1\u7ad9\u69fd\u4f4d {0} \u81f3\u5c11\u9700\u8981 {1} \u4e2a\u8bbe\u65bd\u70b9\u3002", "Service station slot {0} needs at least {1} facility point(s).") },
            { CampusServiceStationValidationTextId.SlotAboveMaximum, new Entry("\u670d\u52a1\u7ad9\u69fd\u4f4d {0} \u6700\u591a\u5141\u8bb8 {1} \u4e2a\u8bbe\u65bd\u70b9\u3002", "Service station slot {0} allows at most {1} facility point(s).") },
            { CampusServiceStationValidationTextId.SlotMissingFacility, new Entry("\u670d\u52a1\u7ad9\u69fd\u4f4d {0} \u5f15\u7528\u4e86\u4e0d\u5b58\u5728\u7684\u8bbe\u65bd\uff1a{1}\u3002", "Service station slot {0} references missing facility: {1}.") },
            { CampusServiceStationValidationTextId.SlotInvalidFacilityType, new Entry("\u670d\u52a1\u7ad9\u69fd\u4f4d {0} \u4e0d\u80fd\u4f7f\u7528\u8bbe\u65bd\u7c7b\u578b {1}\u3002", "Service station slot {0} cannot use facility type {1}.") },
            { CampusServiceStationValidationTextId.SlotRoomMismatch, new Entry("\u670d\u52a1\u7ad9\u69fd\u4f4d {0} \u7684\u8bbe\u65bd\u5fc5\u987b\u548c\u670d\u52a1\u7ad9\u5728\u540c\u4e00\u623f\u95f4\u3002", "Service station slot {0} facility must be in the same room as the station.") },
            { CampusServiceStationValidationTextId.ReferenceMissing, new Entry("{0} \u5f15\u7528\u4e86\u4e0d\u5b58\u5728\u7684\u670d\u52a1\u7ad9\uff1a{1}\u3002", "{0} references a missing service station: {1}.") },
            { CampusServiceStationValidationTextId.FailedToParsePreset, new Entry("[CampusServiceStationPresetCatalog] \u89e3\u6790 {0} \u5931\u8d25\uff1a{1}", "[CampusServiceStationPresetCatalog] Failed to parse {0}: {1}") },
            { CampusServiceStationValidationTextId.MissingInteractionAction, new Entry("\u670d\u52a1\u7ad9\u7c7b\u578b {0} \u7f3a\u5c11 InteractionActionId\u3002", "Service station type {0} is missing InteractionActionId.") },
            { CampusServiceStationValidationTextId.OwnerMissingPlacedObject, new Entry("\u670d\u52a1\u7ad9\u6240\u5c5e\u8bbe\u65bd\u5fc5\u987b\u7ed1\u5b9a\u5230\u5730\u56fe\u7269\u4f53\uff0c\u624d\u80fd\u63d0\u4f9b\u5bf9\u8c61\u4ea4\u4e92\u3002", "Service station owner facility must be bound to a placed object to provide object interaction.") },
            { CampusServiceStationValidationTextId.OwnerMissingInteractionPreset, new Entry("\u670d\u52a1\u7ad9\u6240\u5c5e\u7269\u4f53\u5fc5\u987b\u663e\u5f0f\u8bbe\u7f6e InteractionPresetEid\uff0c\u4e0d\u80fd\u4f9d\u8d56\u8bbe\u65bd\u7c7b\u578b\u9ed8\u8ba4\u4ea4\u4e92\u3002", "Service station owner object must set InteractionPresetEid explicitly instead of relying on facility-type default interaction.") },
            { CampusServiceStationValidationTextId.OwnerUnknownInteractionPreset, new Entry("\u670d\u52a1\u7ad9\u6240\u5c5e\u7269\u4f53\u5f15\u7528\u4e86\u4e0d\u5b58\u5728\u7684 InteractionPresetEid\uff1a{0}\u3002", "Service station owner object references an unknown InteractionPresetEid: {0}.") },
            { CampusServiceStationValidationTextId.OwnerInteractionActionMissing, new Entry("\u670d\u52a1\u7ad9\u6240\u5c5e\u7269\u4f53\u7684\u4ea4\u4e92 preset \u5fc5\u987b\u5305\u542b action\uff1a{0}\u3002", "Service station owner object interaction preset must contain action: {0}.") }
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

    internal enum CampusServiceStationRuntimeTextId
    {
        StationUnavailable = 0,
        StationMissing = 1
    }

    internal static class CampusServiceStationRuntimeTextCatalog
    {
        private static readonly Dictionary<CampusServiceStationRuntimeTextId, Entry> Entries = new()
        {
            {
                CampusServiceStationRuntimeTextId.StationUnavailable,
                new Entry("\u670d\u52a1\u70b9\u6682\u65f6\u4e0d\u53ef\u7528\u3002", "This service station is not available right now.")
            },
            {
                CampusServiceStationRuntimeTextId.StationMissing,
                new Entry("\u670d\u52a1\u70b9\u6682\u65f6\u4e0d\u53ef\u7528\u3002", "This service station is not available right now.")
            }
        };

        public static string Get(CampusServiceStationRuntimeTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());
            return entry.Get(CampusLanguageState.CurrentLanguage);
        }
    }
}
