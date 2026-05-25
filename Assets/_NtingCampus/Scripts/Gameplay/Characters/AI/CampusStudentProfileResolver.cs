using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusStudentProfileResolver
    {
        public static void Build(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;
            List<CampusGameplayRoom> classrooms = CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Classroom);
            CampusGameplayRoom classroom = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.StudentClassroomId : string.Empty,
                CampusRoomType.Classroom);
            if (classroom == null)
            {
                string classroomKey = data != null && !string.IsNullOrWhiteSpace(data.ClassId)
                    ? data.ClassId
                    : data != null ? data.Id : string.Empty;
                classroom = CampusNpcRoomSelector.Choose(classrooms, classroomKey, 0);
            }

            if (CampusNpcRosterIndexer.TryResolveUniqueStudentDesk(
                    runtime,
                    rosterService,
                    worldService,
                    classrooms,
                    classroom,
                    out CampusGameplayRoom.FacilityRecord assignedDesk))
            {
                profile.SetStudentDesk(
                    classroom,
                    CampusNpcFacilitySelector.KeyFor(classroom, assignedDesk),
                    CampusNpcFacilitySelector.PositionOf(assignedDesk));
                return;
            }

            int studentIndex = CampusNpcRosterIndexer.StudentIndexInClassroom(
                runtime,
                rosterService,
                worldService,
                classrooms,
                classroom);

            if (CampusNpcFacilitySelector.TryChooseUnique(
                    classroom,
                    CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.StudentDesks),
                    studentIndex,
                    out CampusGameplayRoom.FacilityRecord desk))
            {
                profile.SetStudentDesk(
                    classroom,
                    CampusNpcFacilitySelector.KeyFor(classroom, desk),
                    CampusNpcFacilitySelector.PositionOf(desk));
            }
            else
            {
                profile.SetStudentDesk(
                    classroom,
                    string.Empty,
                    CampusNpcRoomSelector.PointNearCenter(classroom, studentIndex, 0.45f));
            }
        }
    }
}
