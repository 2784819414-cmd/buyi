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
            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    assignments != null ? assignments.StudentDeskId : string.Empty,
                    CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.StudentDesks),
                    out CampusGameplayRoom classroom,
                    out CampusGameplayRoom.FacilityRecord desk))
            {
                profile.SetStudentDesk(
                    classroom,
                    CampusNpcFacilitySelector.KeyFor(classroom, desk),
                    CampusNpcFacilitySelector.PositionOf(desk));
                return;
            }

            CampusGameplayRoom assignedClassroom = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.StudentClassroomId : string.Empty,
                CampusRoomType.Classroom);
            profile.SetStudentDesk(assignedClassroom, string.Empty, UnityEngine.Vector3.zero);
        }
    }
}
