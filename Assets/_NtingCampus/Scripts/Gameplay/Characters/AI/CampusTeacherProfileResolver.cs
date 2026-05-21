using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusTeacherProfileResolver
    {
        public static void Build(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;
            int teacherIndex = CampusNpcRosterIndexer.PeerIndex(
                runtime,
                rosterService,
                CampusNpcRosterIndexer.IsTeacher);

            List<CampusGameplayRoom> classrooms = CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Classroom);
            CampusGameplayRoom classroom = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.TeacherClassroomId : string.Empty,
                CampusRoomType.Classroom);
            if (classroom == null)
            {
                classroom = CampusNpcRoomSelector.Choose(classrooms, data != null ? data.Id : string.Empty, teacherIndex);
            }

            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    assignments != null ? assignments.TeacherPodiumId : string.Empty,
                    CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.Podiums),
                    out CampusGameplayRoom assignedClassroom,
                    out CampusGameplayRoom.FacilityRecord assignedPodium))
            {
                profile.SetTeacherClassroom(
                    assignedClassroom,
                    CampusNpcFacilitySelector.KeyFor(assignedClassroom, assignedPodium),
                    CampusNpcFacilitySelector.PositionOf(assignedPodium));
            }
            else if (CampusNpcFacilitySelector.TryChoose(
                         classroom,
                         CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.Podiums),
                         teacherIndex,
                         out CampusGameplayRoom.FacilityRecord podium))
            {
                profile.SetTeacherClassroom(
                    classroom,
                    CampusNpcFacilitySelector.KeyFor(classroom, podium),
                    CampusNpcFacilitySelector.PositionOf(podium));
            }
            else
            {
                profile.SetTeacherClassroom(
                    classroom,
                    string.Empty,
                    CampusNpcRoomSelector.PointNearCenter(classroom, teacherIndex, 0.2f));
            }

            List<CampusGameplayRoom> offices = CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Office);
            CampusGameplayRoom office = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.OfficeRoomId : string.Empty,
                CampusRoomType.Office);
            if (office == null)
            {
                office = CampusNpcRoomSelector.Choose(offices, data != null ? data.Id : string.Empty, teacherIndex);
            }

            int officeTeacherIndex = CampusNpcRosterIndexer.TeacherIndexInOffice(
                runtime,
                rosterService,
                worldService,
                offices,
                office);
            if (CampusNpcRosterIndexer.TryResolveUniqueTeacherOfficeDesk(
                    runtime,
                    rosterService,
                    worldService,
                    offices,
                    office,
                    out CampusGameplayRoom.FacilityRecord assignedOfficeDesk))
            {
                profile.SetOfficeDesk(
                    office,
                    CampusNpcFacilitySelector.KeyFor(office, assignedOfficeDesk),
                    CampusNpcFacilitySelector.PositionOf(assignedOfficeDesk));
            }
            else if (CampusNpcFacilitySelector.TryChooseUnique(
                         office,
                         CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.OfficeDesks),
                         officeTeacherIndex,
                         out CampusGameplayRoom.FacilityRecord desk))
            {
                profile.SetOfficeDesk(
                    office,
                    CampusNpcFacilitySelector.KeyFor(office, desk),
                    CampusNpcFacilitySelector.PositionOf(desk));
            }
            else
            {
                profile.SetOfficeDesk(
                    office,
                    string.Empty,
                    CampusNpcRoomSelector.PointNearCenter(office, teacherIndex, 0.35f));
            }
        }
    }
}
