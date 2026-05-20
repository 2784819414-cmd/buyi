using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Schedule
{
    public enum CampusClassroomValidationTextId
    {
        StudentMissingClassroomId = 0,
        StudentMissingDeskId = 1,
        StudentDeskCountTooLow = 2,
        StudentDeskShared = 3,
        TeacherMissingClassroomId = 4,
        TeacherMissingPodiumId = 5,
        TeacherPodiumShared = 6,
        RoomReferenceMissing = 7,
        RoomReferenceWrongType = 8,
        FacilityReferenceEmpty = 9,
        FacilityReferenceMissing = 10,
        FacilityReferenceWrongType = 11,
        FacilityOutsideAssignedRoom = 12
    }

    public static class CampusClassroomValidationTextCatalog
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

        private static readonly Dictionary<CampusClassroomValidationTextId, Entry> Entries =
            new Dictionary<CampusClassroomValidationTextId, Entry>
            {
                { CampusClassroomValidationTextId.StudentMissingClassroomId, new Entry("学生缺少显式 StudentClassroomId；运行时可能退回到默认教室。", "Student has no explicit StudentClassroomId; runtime may fall back to a default classroom.") },
                { CampusClassroomValidationTextId.StudentMissingDeskId, new Entry("学生缺少显式 StudentDeskId；运行时可能自动分配课桌。", "Student has no explicit StudentDeskId; runtime may assign a desk automatically.") },
                { CampusClassroomValidationTextId.StudentDeskCountTooLow, new Entry("课桌数量不足。学生={0}，StudentDesk 设施={1}。", "Student desks are not enough. Students={0}, StudentDesk facilities={1}.") },
                { CampusClassroomValidationTextId.StudentDeskShared, new Entry("StudentDeskId 被多个学生共用：{0}。", "StudentDeskId is assigned to multiple students: {0}.") },
                { CampusClassroomValidationTextId.TeacherMissingClassroomId, new Entry("老师缺少显式 TeacherClassroomId；运行时可能退回到默认教室。", "Teacher has no explicit TeacherClassroomId; runtime may fall back to a default classroom.") },
                { CampusClassroomValidationTextId.TeacherMissingPodiumId, new Entry("老师缺少显式 TeacherPodiumId。", "Teacher has no explicit TeacherPodiumId.") },
                { CampusClassroomValidationTextId.TeacherPodiumShared, new Entry("TeacherPodiumId 被多个老师共用；只有真实课程表支持时才应这样配置：{0}。", "TeacherPodiumId is shared; this should only be used when a real course schedule supports it: {0}.") },
                { CampusClassroomValidationTextId.RoomReferenceMissing, new Entry("{0} 引用了不存在的房间：{1}。", "{0} references a missing room: {1}.") },
                { CampusClassroomValidationTextId.RoomReferenceWrongType, new Entry("{0} 指向 {1}，应为 {2}。", "{0} points to {1}, expected {2}.") },
                { CampusClassroomValidationTextId.FacilityReferenceEmpty, new Entry("{0} 为空。", "{0} is empty.") },
                { CampusClassroomValidationTextId.FacilityReferenceMissing, new Entry("{0} 引用了不存在的设施：{1}。", "{0} references a missing facility: {1}.") },
                { CampusClassroomValidationTextId.FacilityReferenceWrongType, new Entry("{0} 指向 {1}，应为 {2}。", "{0} points to {1}, expected {2}.") },
                { CampusClassroomValidationTextId.FacilityOutsideAssignedRoom, new Entry("{0} 指向的设施在 {1}，不在 {2} 指定的教室 {3} 内。", "{0} points to a facility in {1}, not inside the classroom {3} assigned by {2}.") }
            };

        public static string Format(CampusClassroomValidationTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Get(CampusClassroomValidationTextId id)
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
