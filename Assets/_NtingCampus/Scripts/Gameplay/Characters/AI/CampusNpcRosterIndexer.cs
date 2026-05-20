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
            foreach (CampusCharacterRuntime peer in rosterService.Runtimes)
            {
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

            int index = 0;
            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null || runtime.Data.Role != data.Role)
                {
                    continue;
                }

                if (string.Equals(runtime.Data.Id, data.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }

                index++;
            }

            return CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(data.Id), Math.Max(1, index));
        }

        public static int StudentIndexInClassroom(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService,
            List<CampusGameplayRoom> classrooms,
            CampusGameplayRoom classroom)
        {
            if (runtime == null || runtime.Data == null || rosterService == null || classroom == null)
            {
                return 0;
            }

            List<string> studentIds = new List<string>();
            foreach (CampusCharacterRuntime peer in rosterService.Runtimes)
            {
                if (peer == null || peer.Data == null || peer.Data.Role != CampusCharacterRole.Student)
                {
                    continue;
                }

                CampusGameplayRoom peerClassroom = ResolveStudentClassroom(peer, worldService, classrooms);
                if (CampusNpcRoomSelector.SameRoom(peerClassroom, classroom))
                {
                    studentIds.Add(CampusNpcStableIds.CharacterKey(peer));
                }
            }

            studentIds.Sort(StringComparer.OrdinalIgnoreCase);
            string targetId = CampusNpcStableIds.CharacterKey(runtime);
            int index = studentIds.IndexOf(targetId);
            return index >= 0 ? index : CampusNpcStableIds.PositiveModulo(CampusNpcStableIds.Hash(targetId), Math.Max(1, studentIds.Count));
        }

        public static bool TryResolveUniqueStudentDesk(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            CampusWorldService worldService,
            List<CampusGameplayRoom> classrooms,
            CampusGameplayRoom classroom,
            out CampusGameplayRoom.FacilityRecord record)
        {
            return TryResolveUniqueFacility(
                runtime,
                rosterService,
                classroom,
                CampusNpcFacilityGroups.StudentDesks,
                peer => peer != null &&
                        peer.Data != null &&
                        peer.Data.Role == CampusCharacterRole.Student &&
                        CampusNpcRoomSelector.SameRoom(ResolveStudentClassroom(peer, worldService, classrooms), classroom),
                peer => peer != null && peer.Data != null && peer.Data.Assignments != null
                    ? peer.Data.Assignments.StudentDeskId
                    : string.Empty,
                out record);
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
            foreach (CampusCharacterRuntime peer in rosterService.Runtimes)
            {
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
                CampusNpcFacilityGroups.OfficeDesks,
                peer => peer != null &&
                        peer.Data != null &&
                        peer.Data.Role == CampusCharacterRole.Teacher &&
                        CampusNpcRoomSelector.SameRoom(ResolveTeacherOffice(peer, rosterService, worldService, offices), office),
                peer => peer != null && peer.Data != null && peer.Data.Assignments != null
                    ? peer.Data.Assignments.OfficeDeskId
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
            foreach (CampusCharacterRuntime peer in rosterService.Runtimes)
            {
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

        private static CampusGameplayRoom ResolveStudentClassroom(
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            List<CampusGameplayRoom> classrooms)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;
            CampusGameplayRoom assigned = CampusNpcRoomSelector.ResolveAssigned(
                worldService,
                assignments != null ? assignments.StudentClassroomId : string.Empty,
                CampusRoomType.Classroom);
            if (assigned != null)
            {
                return assigned;
            }

            string key = data != null && !string.IsNullOrWhiteSpace(data.ClassId)
                ? data.ClassId
                : data != null ? data.Id : string.Empty;
            return CampusNpcRoomSelector.Choose(classrooms, key, 0);
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
    }
}
