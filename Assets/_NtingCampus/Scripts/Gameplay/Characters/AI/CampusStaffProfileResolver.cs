using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Services;

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
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;

            if (data != null && (data.StaffDuty & CampusStaffDuty.SupportStaff) != 0)
            {
                BuildSupportStaffProfile(profile, runtime, worldService, rosterService, data, assignments);
                return;
            }

            BuildGenericStaffProfile(profile, runtime, worldService, rosterService, data, assignments);
        }

        private static void BuildSupportStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService,
            CampusCharacterData data,
            CampusCharacterAssignmentData assignments)
        {
            if (TryResolveAssignedSupportStaffStation(worldService, assignments, out CampusServiceStation assignedStation))
            {
                ApplySupportStaffStation(profile, assignedStation);
                return;
            }

            BuildGenericStaffProfile(profile, runtime, worldService, rosterService, data, assignments);
        }

        private static void BuildGenericStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService,
            CampusCharacterData data,
            CampusCharacterAssignmentData assignments)
        {
            int staffIndex = CampusNpcRosterIndexer.PeerIndex(
                runtime,
                rosterService,
                CampusNpcRosterIndexer.IsStaff);
            CampusFacilityType[] workstationTypes = CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.Workstations);

            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    assignments != null ? assignments.WorkFacilityId : string.Empty,
                    workstationTypes,
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedWorkstation))
            {
                profile.SetPrimaryWorkstation(
                    assignedRoom,
                    CampusNpcFacilitySelector.KeyFor(assignedRoom, assignedWorkstation),
                    CampusNpcFacilitySelector.PositionOf(assignedWorkstation));
                return;
            }

            CampusGameplayRoom workRoom = ResolveWorkRoom(data, assignments, worldService, staffIndex, CampusRoomType.Office);
            if (CampusNpcRosterIndexer.TryResolveUniqueStaffPrimaryWorkstation(
                    runtime,
                    rosterService,
                    worldService,
                    workRoom,
                    workstationTypes,
                    CampusStaffDuty.None,
                    out CampusGameplayRoom.FacilityRecord workstation))
            {
                profile.SetPrimaryWorkstation(
                    workRoom,
                    CampusNpcFacilitySelector.KeyFor(workRoom, workstation),
                    CampusNpcFacilitySelector.PositionOf(workstation));
                return;
            }

            profile.SetPrimaryWorkstation(
                workRoom,
                string.Empty,
                CampusNpcRoomSelector.PointNearCenter(workRoom, staffIndex, 0.25f));
        }

        private static CampusGameplayRoom ResolveWorkRoom(
            CampusCharacterData data,
            CampusCharacterAssignmentData assignments,
            CampusWorldService worldService,
            int staffIndex,
            CampusRoomType fallbackRoomType)
        {
            CampusGameplayRoom assignedRoom = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.WorkRoomId : string.Empty,
                CampusRoomType.Unknown);
            if (assignedRoom != null)
            {
                return assignedRoom;
            }

            return CampusNpcRoomSelector.Choose(
                CampusNpcRoomSelector.GetRooms(worldService, fallbackRoomType),
                data != null ? data.Id : string.Empty,
                staffIndex);
        }

        private static bool TryResolveAssignedSupportStaffStation(
            CampusWorldService worldService,
            CampusCharacterAssignmentData assignments,
            out CampusServiceStation station)
        {
            station = default;
            if (worldService == null || assignments == null)
            {
                return false;
            }

            if (worldService.ServiceStations.TryResolveById(
                    assignments.ServiceStationId,
                    out station))
            {
                return true;
            }

            return false;
        }

        private static void ApplySupportStaffStation(
            CampusNpcPersonalProfile profile,
            CampusServiceStation station)
        {
            if (profile == null || !station.IsOperational)
            {
                return;
            }

            profile.SetPrimaryServiceWindow(
                station.Room,
                CampusNpcFacilitySelector.KeyFor(station.Room, station.InteractionFacility),
                CampusServiceStation.PositionOf(station.InteractionFacility));
            profile.SetPrimaryWorkstation(
                station.Room,
                CampusNpcFacilitySelector.KeyFor(station.Room, station.OperatorSlot),
                CampusNpcFacilitySelector.PositionOf(station.OperatorSlot));
        }
    }
}
