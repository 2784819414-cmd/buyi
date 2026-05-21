using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusStaffProfileResolver
    {
        public static void Build(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            int staffIndex = CampusNpcRosterIndexer.PeerIndex(
                runtime,
                rosterService,
                CampusNpcRosterIndexer.IsStaff);

            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    data != null ? data.Assignments.PrimaryWorkstationId : string.Empty,
                    CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.Workstations),
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedWorkstation))
            {
                profile.SetPrimaryWorkstation(
                    assignedRoom,
                    CampusNpcFacilitySelector.KeyFor(assignedRoom, assignedWorkstation),
                    CampusNpcFacilitySelector.PositionOf(assignedWorkstation));
                return;
            }

            CampusGameplayRoom office = CampusNpcRoomSelector.Choose(
                CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Office),
                data != null ? data.Id : string.Empty,
                staffIndex);
            if (CampusNpcFacilitySelector.TryChoose(
                    office,
                    CampusNpcFacilityGroups.Get(CampusNpcFacilityGroups.Workstations),
                    staffIndex,
                    out CampusGameplayRoom.FacilityRecord workstation))
            {
                profile.SetPrimaryWorkstation(
                    office,
                    CampusNpcFacilitySelector.KeyFor(office, workstation),
                    CampusNpcFacilitySelector.PositionOf(workstation));
                return;
            }

            profile.SetPrimaryWorkstation(
                office,
                string.Empty,
                CampusNpcRoomSelector.PointNearCenter(office, staffIndex, 0.25f));
        }
    }
}
