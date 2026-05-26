using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.Gameplay.Rooms
{
    internal enum CampusEcologyValidationTextId
    {
        WorldFactsMissing = 0,
        ValidationPassed = 1,
        ClassroomRequired = 2,
        OfficeRecommended = 3,
        DormitoryMissing = 4,
        FreeMovementFallbackMissing = 5,
        FacilityTypeUnknown = 6,
        FacilityMissingId = 7,
        FacilityDuplicateId = 8,
        ActorMissingId = 9,
        ActorDuplicateId = 10,
        OfficeDeskDuplicateOwners = 11,
        ServiceStationDuplicateOwners = 12,
        SupportStaffExceedsStations = 13,
        RoomReferenceMissing = 14,
        FacilityReferenceMissing = 15,
        FacilityReferenceWrongType = 16,
        DuplicateOwners = 17,
        ValidationPassedLog = 18
    }

    internal static class CampusEcologyValidationTextCatalog
    {
        private static readonly Dictionary<CampusEcologyValidationTextId, Entry> Entries = new()
        {
            { CampusEcologyValidationTextId.WorldFactsMissing, new Entry("世界事实缺失。", "World facts are missing.") },
            { CampusEcologyValidationTextId.ValidationPassed, new Entry("生态校验通过。", "Validation passed.") },
            { CampusEcologyValidationTextId.ClassroomRequired, new Entry("有上课职责的角色至少需要一个教室。", "Characters with class duties need at least one classroom.") },
            { CampusEcologyValidationTextId.OfficeRecommended, new Entry("教师和职员最好至少有一个办公室。", "Teachers and staff work better with at least one office room.") },
            { CampusEcologyValidationTextId.DormitoryMissing, new Entry("学生没有宿舍 fallback 房间。", "Students have no dormitory fallback room.") },
            { CampusEcologyValidationTextId.FreeMovementFallbackMissing, new Entry("NPC 自由移动需要 CommonActivityZone 或 Corridor。", "NPC free movement needs a CommonActivityZone or Corridor fallback.") },
            { CampusEcologyValidationTextId.FacilityTypeUnknown, new Entry("设施类型是 Unknown，不能可靠作为目标。", "Facility type is Unknown and cannot be targeted reliably.") },
            { CampusEcologyValidationTextId.FacilityMissingId, new Entry("设施缺少稳定 facility id。", "Facility is missing a stable facility id.") },
            { CampusEcologyValidationTextId.FacilityDuplicateId, new Entry("设施 id 重复，角色绑定可能解析到错误目标。", "Duplicate facility id. Actor bindings may resolve to the wrong target.") },
            { CampusEcologyValidationTextId.ActorMissingId, new Entry("角色缺少稳定 actor id。", "Actor is missing a stable actor id.") },
            { CampusEcologyValidationTextId.ActorDuplicateId, new Entry("角色 id 重复。", "Duplicate actor id.") },
            { CampusEcologyValidationTextId.OfficeDeskDuplicateOwners, new Entry("OfficeDeskId 被多个教师分配。", "OfficeDeskId is assigned to multiple teachers.") },
            { CampusEcologyValidationTextId.ServiceStationDuplicateOwners, new Entry("ServiceStationId 被多个支援职员分配。", "ServiceStationId is assigned to multiple support staff.") },
            { CampusEcologyValidationTextId.SupportStaffExceedsStations, new Entry("支援职员数量超过可运行服务站数量，多余支援职员不会获得饭点服务站。", "SupportStaff count exceeds operational service station count. Extra support staff will not receive a meal-duty station.") },
            { CampusEcologyValidationTextId.RoomReferenceMissing, new Entry("{0} 引用了不存在的房间：{1}。", "{0} references a missing room: {1}.") },
            { CampusEcologyValidationTextId.FacilityReferenceMissing, new Entry("{0} 引用了不存在的设施：{1}。", "{0} references a missing facility: {1}.") },
            { CampusEcologyValidationTextId.FacilityReferenceWrongType, new Entry("{0} 指向 {1}，预期 {2}。", "{0} points to {1}, expected {2}.") },
            { CampusEcologyValidationTextId.DuplicateOwners, new Entry("{0} Owners={1}。", "{0} Owners={1}.") },
            { CampusEcologyValidationTextId.ValidationPassedLog, new Entry("[Ecology] 生态校验通过。", "[Ecology] Validation passed.") }
        };

        public static string Get(CampusEcologyValidationTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());
            return entry.Get(CampusLanguageState.CurrentLanguage);
        }

        public static string Format(CampusEcologyValidationTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}
