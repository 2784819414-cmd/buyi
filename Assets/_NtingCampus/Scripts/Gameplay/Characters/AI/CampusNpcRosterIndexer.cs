using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcRosterIndexer
    {
        public static int PeerIndex(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            Func<CampusCharacterRuntime, CampusCharacterRuntime, bool> predicate)
        {
            if (runtime == null || rosterService == null)
            {
                return 0;
            }

            List<string> ids = new List<string>();
            IReadOnlyList<CampusCharacterRuntime> peers = rosterService.Index.Runtimes;
            for (int i = 0; i < peers.Count; i++)
            {
                CampusCharacterRuntime peer = peers[i];
                if (peer == null || peer.Data == null || (predicate != null && !predicate(runtime, peer)))
                {
                    continue;
                }

                ids.Add(CampusNpcStableIds.CharacterKey(peer));
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            string targetId = CampusNpcStableIds.CharacterKey(runtime);
            int index = ids.IndexOf(targetId);
            return index >= 0 ? index : CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(targetId), Math.Max(1, ids.Count));
        }

        public static int RoleIndex(CampusCharacterData data, CampusRosterService rosterService)
        {
            if (data == null || rosterService == null)
            {
                return 0;
            }

            IReadOnlyList<CampusCharacterRuntime> peers = rosterService.Index.GetByRole(data.Role);
            for (int index = 0; index < peers.Count; index++)
            {
                CampusCharacterRuntime runtime = peers[index];
                if (runtime == null || runtime.Data == null || runtime.Data.Role != data.Role)
                {
                    continue;
                }

                if (string.Equals(runtime.Data.Id, data.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(data.Id), Math.Max(1, peers.Count));
        }

        public static int TeacherIndexInOffice(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService,
            List<CampusGameplayRoom> offices,
            CampusGameplayRoom office)
        {
            if (runtime == null || runtime.Data == null || rosterService == null || office == null)
            {
                return 0;
            }

            List<string> teacherIds = new List<string>();
            IReadOnlyList<CampusCharacterRuntime> peers = rosterService.Index.GetByRole(CampusCharacterRole.Teacher);
            for (int i = 0; i < peers.Count; i++)
            {
                CampusCharacterRuntime peer = peers[i];
                if (peer == null || peer.Data == null || peer.Data.Role != CampusCharacterRole.Teacher)
                {
                    continue;
                }

                CampusGameplayRoom peerOffice = ResolveTeacherOffice(peer, rosterService, worldService, offices);
                if (CampusNpcRoomSelector.SameRoom(peerOffice, office))
                {
                    teacherIds.Add(CampusNpcStableIds.CharacterKey(peer));
                }
            }

            teacherIds.Sort(StringComparer.OrdinalIgnoreCase);
            string targetId = CampusNpcStableIds.CharacterKey(runtime);
            int index = teacherIds.IndexOf(targetId);
            return index >= 0 ? index : CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(targetId), Math.Max(1, teacherIds.Count));
        }

        public static bool TryResolveUniqueTeacherOfficeDesk(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService,
            List<CampusGameplayRoom> offices,
            CampusGameplayRoom office,
            out CampusGameplayRoom.FacilityRecord record)
        {
            return TryResolveUniqueFacility(
                runtime,
                rosterService,
                office,
                CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.OfficeDesks),
                peer => peer != null &&
                        peer.Data != null &&
                        peer.Data.Role == CampusCharacterRole.Teacher &&
                        CampusNpcRoomSelector.SameRoom(ResolveTeacherOffice(peer, rosterService, worldService, offices), office),
                peer => peer != null && peer.Data != null && peer.Data.Assignments != null
                    ? peer.Data.Assignments.OfficeDeskId
                    : string.Empty,
                out record);
        }

        public static bool TryResolveUniqueStaffPrimaryWorkstation(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService,
            CampusGameplayRoom workRoom,
            CampusFacilityType[] facilityTypes,
            CampusStaffDuty staffDutyMask,
            out CampusGameplayRoom.FacilityRecord record)
        {
            return TryResolveUniqueFacility(
                runtime,
                rosterService,
                workRoom,
                facilityTypes,
                peer => peer != null &&
                        peer.Data != null &&
                        BelongsToStaffCohort(peer.Data, staffDutyMask) &&
                        CampusNpcRoomSelector.SameRoom(
                            ResolveStaffWorkRoom(peer, rosterService, worldService),
                            workRoom),
                peer => peer != null && peer.Data != null && peer.Data.Assignments != null
                    ? peer.Data.Assignments.WorkFacilityId
                    : string.Empty,
                out record);
        }

        public static bool TryResolveUniqueStaffServiceWindow(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService,
            CampusGameplayRoom workRoom,
            CampusStaffDuty staffDutyMask,
            out CampusGameplayRoom.FacilityRecord record)
        {
            return TryResolveUniqueFacility(
                runtime,
                rosterService,
                workRoom,
                CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.ServiceWindows),
                peer => peer != null &&
                        peer.Data != null &&
                        BelongsToStaffCohort(peer.Data, staffDutyMask) &&
                        CampusNpcRoomSelector.SameRoom(
                            ResolveStaffWorkRoom(peer, rosterService, worldService),
                            workRoom),
                peer => peer != null && peer.Data != null && peer.Data.Assignments != null
                    ? peer.Data.Assignments.ServiceStationId
                    : string.Empty,
                out record);
        }

        public static bool IsTeacher(CampusCharacterRuntime owner, CampusCharacterRuntime peer)
        {
            return peer != null && peer.Data != null && peer.Data.Role == CampusCharacterRole.Teacher;
        }

        public static bool IsStaff(CampusCharacterRuntime owner, CampusCharacterRuntime peer)
        {
            return peer != null && peer.Data != null && peer.Data.Role == CampusCharacterRole.Staff;
        }

        private static bool TryResolveUniqueFacility(
            CampusCharacterRuntime target,
            CampusRosterService rosterService,
            CampusGameplayRoom room,
            CampusFacilityType[] facilityTypes,
            Func<CampusCharacterRuntime, bool> ownsRoom,
            Func<CampusCharacterRuntime, string> savedFacilityId,
            out CampusGameplayRoom.FacilityRecord record)
        {
            record = null;
            if (target == null || target.Data == null || rosterService == null || room == null)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> facilities = CampusNpcFacilitySelector.Collect(room, facilityTypes);
            if (facilities.Count == 0)
            {
                return false;
            }

            Dictionary<string, CampusGameplayRoom.FacilityRecord> facilitiesById =
                new Dictionary<string, CampusGameplayRoom.FacilityRecord>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = facilities[i];
                string key = CampusNpcFacilitySelector.KeyFor(room, facility);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    facilitiesById[key] = facility;
                }
            }

            List<string> ownerIds = new List<string>();
            Dictionary<string, string> savedFacilityByOwner =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<CampusCharacterRuntime> peers = rosterService.Index.Runtimes;
            for (int i = 0; i < peers.Count; i++)
            {
                CampusCharacterRuntime peer = peers[i];
                if (peer == null || peer.Data == null || ownsRoom == null || !ownsRoom(peer))
                {
                    continue;
                }

                string ownerId = CampusNpcStableIds.CharacterKey(peer);
                ownerIds.Add(ownerId);

                string savedId = savedFacilityId != null ? savedFacilityId(peer) : string.Empty;
                if (!string.IsNullOrWhiteSpace(savedId) &&
                    CampusNpcFacilitySelector.TryFindInRoom(room, facilities, savedId, out CampusGameplayRoom.FacilityRecord savedFacility))
                {
                    savedFacilityByOwner[ownerId] = CampusNpcFacilitySelector.KeyFor(room, savedFacility);
                }
            }

            ownerIds.Sort(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CampusGameplayRoom.FacilityRecord> finalFacilityByOwner =
                new Dictionary<string, CampusGameplayRoom.FacilityRecord>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedFacilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int ownerIndex = 0; ownerIndex < ownerIds.Count; ownerIndex++)
            {
                string ownerId = ownerIds[ownerIndex];
                if (!savedFacilityByOwner.TryGetValue(ownerId, out string savedId) ||
                    !facilitiesById.TryGetValue(savedId, out CampusGameplayRoom.FacilityRecord savedFacility) ||
                    usedFacilityIds.Contains(savedId))
                {
                    continue;
                }

                finalFacilityByOwner[ownerId] = savedFacility;
                usedFacilityIds.Add(savedId);
            }

            for (int ownerIndex = 0; ownerIndex < ownerIds.Count; ownerIndex++)
            {
                string ownerId = ownerIds[ownerIndex];
                if (finalFacilityByOwner.ContainsKey(ownerId))
                {
                    continue;
                }

                for (int facilityIndex = 0; facilityIndex < facilities.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord candidate = facilities[facilityIndex];
                    string candidateId = CampusNpcFacilitySelector.KeyFor(room, candidate);
                    if (string.IsNullOrWhiteSpace(candidateId) || usedFacilityIds.Contains(candidateId))
                    {
                        continue;
                    }

                    finalFacilityByOwner[ownerId] = candidate;
                    usedFacilityIds.Add(candidateId);
                    break;
                }
            }

            string targetId = CampusNpcStableIds.CharacterKey(target);
            return finalFacilityByOwner.TryGetValue(targetId, out record) && record != null;
        }

        private static CampusGameplayRoom ResolveTeacherOffice(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService,
            List<CampusGameplayRoom> offices)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;
            CampusGameplayRoom assigned = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.OfficeRoomId : string.Empty,
                CampusRoomType.Office);
            if (assigned != null)
            {
                return assigned;
            }

            int teacherIndex = PeerIndex(runtime, rosterService, IsTeacher);
            return CampusNpcRoomSelector.Choose(offices, data != null ? data.Id : string.Empty, teacherIndex);
        }

        private static CampusGameplayRoom ResolveStaffWorkRoom(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;
            bool isSupportStaff = data != null && (data.StaffDuty & CampusStaffDuty.SupportStaff) != 0;

            CampusGameplayRoom assignedRoom = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.WorkRoomId : string.Empty,
                CampusRoomType.Unknown);
            if (assignedRoom != null)
            {
                return assignedRoom;
            }

            if (isSupportStaff &&
                CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    assignments != null ? assignments.WorkFacilityId : string.Empty,
                    CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.WorkerStands),
                    out CampusGameplayRoom serviceWindowRoom,
                    out _))
            {
                return serviceWindowRoom;
            }

            CampusFacilityType[] facilityTypes = ResolveStaffWorkstationFacilityTypes(data);
            if (CampusNpcFacilitySelector.FindAssigned(
                worldService,
                assignments != null ? assignments.WorkFacilityId : string.Empty,
                facilityTypes,
                    out CampusGameplayRoom workstationRoom,
                    out _))
            {
                return workstationRoom;
            }

            CampusRoomType roomType = ResolveStaffWorkRoomType(data);
            List<CampusGameplayRoom> rooms = CampusNpcRoomSelector.GetRooms(worldService, roomType);
            int staffIndex = PeerIndex(runtime, rosterService, IsStaff);
            return CampusNpcRoomSelector.Choose(rooms, data != null ? data.Id : string.Empty, staffIndex);
        }

        private static CampusFacilityType[] ResolveStaffWorkstationFacilityTypes(CampusCharacterData data)
        {
            return data != null && (data.StaffDuty & CampusStaffDuty.SupportStaff) != 0
                ? CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.WorkerStands)
                : CampusNpcFacilityTypeSets.Get(CampusNpcFacilityTypeSets.Workstations);
        }

        private static CampusRoomType ResolveStaffWorkRoomType(CampusCharacterData data)
        {
            return data != null && (data.StaffDuty & CampusStaffDuty.SupportStaff) != 0
                ? CampusRoomType.ServiceArea
                : CampusRoomType.Office;
        }

        private static bool BelongsToStaffCohort(CampusCharacterData data, CampusStaffDuty staffDutyMask)
        {
            if (data == null || data.Role != CampusCharacterRole.Staff)
            {
                return false;
            }

            return staffDutyMask == CampusStaffDuty.None
                ? true
                : (data.StaffDuty & staffDutyMask) != 0;
        }
    }
}
