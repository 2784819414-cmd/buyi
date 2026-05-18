using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public static class CampusNpcProfileBuilder
    {
        private static readonly CampusFacilityType[] StudentDeskTypes = { CampusFacilityType.StudentDesk };
        private static readonly CampusFacilityType[] PodiumTypes = { CampusFacilityType.Podium, CampusFacilityType.Blackboard };
        private static readonly CampusFacilityType[] OfficeDeskTypes = { CampusFacilityType.OfficeDesk, CampusFacilityType.Desk };
        private static readonly CampusFacilityType[] CanteenWorkTypes =
        {
            CampusFacilityType.CanteenClerkStandPoint,
            CampusFacilityType.CanteenCounter,
            CampusFacilityType.CanteenCustomerPickupPoint
        };

        private static readonly CampusFacilityType[] StoreWorkTypes =
        {
            CampusFacilityType.StoreCheckout,
            CampusFacilityType.StoreQueuePoint
        };

        private static readonly CampusFacilityType[] DeliveryPointTypes = { CampusFacilityType.DeliveryDropPoint };
        private static readonly CampusFacilityType[] ShelfTypes =
        {
            CampusFacilityType.StoreShelf,
            CampusFacilityType.Storage,
            CampusFacilityType.CanteenFoodTray
        };

        public static CampusNpcPersonalProfile Build(
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusNpcPersonalProfile profile = new CampusNpcPersonalProfile();
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            profile.Reset(data);

            if (data == null || worldService == null)
            {
                return profile;
            }

            BuildCommonAnchors(profile, data, worldService, rosterService);
            switch (data.Role)
            {
                case CampusCharacterRole.Teacher:
                    BuildTeacherProfile(profile, runtime, worldService, rosterService);
                    break;
                case CampusCharacterRole.Staff:
                    BuildStaffProfile(profile, runtime, worldService, rosterService);
                    break;
                default:
                    BuildStudentProfile(profile, runtime, worldService, rosterService);
                    break;
            }

            return profile;
        }

        private static void BuildStudentProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;
            List<CampusGameplayRoom> classrooms = GetRooms(worldService, CampusRoomType.Classroom);
            CampusGameplayRoom classroom = ResolveAssignedRoom(worldService, assignments != null ? assignments.StudentClassroomId : string.Empty, CampusRoomType.Classroom);
            if (classroom == null)
            {
                string classroomKey = data != null && !string.IsNullOrWhiteSpace(data.ClassId) ? data.ClassId : data != null ? data.Id : string.Empty;
                classroom = ChooseRoom(classrooms, classroomKey, 0);
            }

            if (TryResolveUniqueStudentDesk(
                    runtime,
                    rosterService,
                    classrooms,
                    classroom,
                    out CampusGameplayRoom assignedDeskRoom,
                    out CampusGameplayRoom.FacilityRecord assignedDesk))
            {
                profile.SetStudentDesk(assignedDeskRoom, BuildFacilityKey(assignedDeskRoom, assignedDesk), ToWorldPosition(assignedDesk));
                return;
            }

            int studentIndex = ResolveStudentIndexInClassroom(runtime, rosterService, classrooms, classroom);

            if (TryChooseUniqueStudentDesk(classroom, studentIndex, out CampusGameplayRoom.FacilityRecord desk))
            {
                profile.SetStudentDesk(classroom, BuildFacilityKey(classroom, desk), ToWorldPosition(desk));
            }
            else
            {
                profile.SetStudentDesk(classroom, string.Empty, ResolveRoomPoint(classroom, studentIndex, 0.45f));
            }
        }

        private static void BuildTeacherProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusCharacterAssignmentData assignments = data != null ? data.Assignments : null;
            int teacherIndex = ResolvePeerIndex(runtime, rosterService, IsTeacher);
            List<CampusGameplayRoom> classrooms = GetRooms(worldService, CampusRoomType.Classroom);
            CampusGameplayRoom classroom = ResolveAssignedRoom(worldService, assignments != null ? assignments.TeacherClassroomId : string.Empty, CampusRoomType.Classroom);
            if (classroom == null)
            {
                classroom = ChooseRoom(classrooms, data != null ? data.Id : string.Empty, teacherIndex);
            }

            if (TryFindAssignedFacility(
                    worldService,
                    assignments != null ? assignments.TeacherPodiumId : string.Empty,
                    PodiumTypes,
                    out CampusGameplayRoom assignedClassroom,
                    out CampusGameplayRoom.FacilityRecord assignedPodium))
            {
                profile.SetTeacherClassroom(assignedClassroom, BuildFacilityKey(assignedClassroom, assignedPodium), ToWorldPosition(assignedPodium));
            }
            else if (TryChooseFacility(classroom, PodiumTypes, teacherIndex, out CampusGameplayRoom.FacilityRecord podium))
            {
                profile.SetTeacherClassroom(classroom, BuildFacilityKey(classroom, podium), ToWorldPosition(podium));
            }
            else
            {
                profile.SetTeacherClassroom(classroom, string.Empty, ResolveRoomPoint(classroom, teacherIndex, 0.2f));
            }

            List<CampusGameplayRoom> offices = GetRooms(worldService, CampusRoomType.Office);
            CampusGameplayRoom office = ResolveAssignedRoom(worldService, assignments != null ? assignments.OfficeRoomId : string.Empty, CampusRoomType.Office);
            if (office == null)
            {
                office = ChooseRoom(offices, data != null ? data.Id : string.Empty, teacherIndex);
            }

            int officeTeacherIndex = ResolveRoleIndexInChosenRoom(runtime, rosterService, offices, office, CampusCharacterRole.Teacher);
            if (TryFindAssignedFacility(
                    worldService,
                    assignments != null ? assignments.OfficeDeskId : string.Empty,
                    OfficeDeskTypes,
                    out CampusGameplayRoom assignedOffice,
                    out CampusGameplayRoom.FacilityRecord assignedOfficeDesk))
            {
                profile.SetOfficeDesk(assignedOffice, BuildFacilityKey(assignedOffice, assignedOfficeDesk), ToWorldPosition(assignedOfficeDesk));
            }
            else if (TryChooseUniqueFacility(office, OfficeDeskTypes, officeTeacherIndex, out CampusGameplayRoom.FacilityRecord desk))
            {
                profile.SetOfficeDesk(office, BuildFacilityKey(office, desk), ToWorldPosition(desk));
            }
            else
            {
                profile.SetOfficeDesk(office, string.Empty, ResolveRoomPoint(office, teacherIndex, 0.35f));
            }
        }

        private static void BuildStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            int staffIndex = ResolvePeerIndex(runtime, rosterService, IsStaff);
            CampusStaffDuty duty = data != null ? data.StaffDuty : CampusStaffDuty.None;

            if ((duty & CampusStaffDuty.StoreOwner) != 0 || (duty & CampusStaffDuty.BookstoreOwner) != 0)
            {
                BuildStoreStaffProfile(profile, data, worldService, staffIndex);
                return;
            }

            if ((duty & CampusStaffDuty.DeliveryWatcher) != 0)
            {
                BuildDeliveryStaffProfile(profile, data, worldService, staffIndex);
                return;
            }

            BuildCanteenStaffProfile(profile, data, worldService, staffIndex);
        }

        private static void BuildCanteenStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            int staffIndex)
        {
            if (TryFindAssignedFacility(
                    worldService,
                    data != null ? data.Assignments.PrimaryWorkstationId : string.Empty,
                    CanteenWorkTypes,
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedCounter))
            {
                profile.SetPrimaryWorkstation(assignedRoom, BuildFacilityKey(assignedRoom, assignedCounter), ToWorldPosition(assignedCounter));
                AddFacilityPositions(assignedRoom, CanteenWorkTypes, profile.SecondaryWorkstationPositions);
                return;
            }

            CampusGameplayRoom canteen = ChooseRoom(GetRooms(worldService, CampusRoomType.Canteen), data != null ? data.Id : string.Empty, staffIndex);
            if (TryChooseFacility(canteen, CanteenWorkTypes, staffIndex, out CampusGameplayRoom.FacilityRecord counter))
            {
                profile.SetPrimaryWorkstation(canteen, BuildFacilityKey(canteen, counter), ToWorldPosition(counter));
            }
            else
            {
                profile.SetPrimaryWorkstation(canteen, string.Empty, ResolveRoomPoint(canteen, staffIndex, 0.25f));
            }

            AddFacilityPositions(canteen, CanteenWorkTypes, profile.SecondaryWorkstationPositions);
        }

        private static void BuildStoreStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            int staffIndex)
        {
            if (TryFindAssignedFacility(
                    worldService,
                    data != null ? data.Assignments.PrimaryWorkstationId : string.Empty,
                    StoreWorkTypes,
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedCheckout))
            {
                profile.SetPrimaryWorkstation(assignedRoom, BuildFacilityKey(assignedRoom, assignedCheckout), ToWorldPosition(assignedCheckout));
                AddFacilityPositions(assignedRoom, StoreWorkTypes, profile.SecondaryWorkstationPositions);
                AddFacilityPositions(assignedRoom, ShelfTypes, profile.ShelfPositions);
                return;
            }

            CampusGameplayRoom store = ChooseRoom(GetRooms(worldService, CampusRoomType.Store), data != null ? data.Id : string.Empty, staffIndex);
            if (TryChooseFacility(store, StoreWorkTypes, staffIndex, out CampusGameplayRoom.FacilityRecord checkout))
            {
                profile.SetPrimaryWorkstation(store, BuildFacilityKey(store, checkout), ToWorldPosition(checkout));
            }
            else
            {
                profile.SetPrimaryWorkstation(store, string.Empty, ResolveRoomPoint(store, staffIndex, 0.25f));
            }

            AddFacilityPositions(store, StoreWorkTypes, profile.SecondaryWorkstationPositions);
            AddFacilityPositions(store, ShelfTypes, profile.ShelfPositions);
        }

        private static void BuildDeliveryStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            int staffIndex)
        {
            string assignedPointId = data != null && !string.IsNullOrWhiteSpace(data.Assignments.DeliveryPointId)
                ? data.Assignments.DeliveryPointId
                : data != null ? data.Assignments.PrimaryWorkstationId : string.Empty;
            if (TryFindAssignedFacility(
                    worldService,
                    assignedPointId,
                    DeliveryPointTypes,
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedPoint))
            {
                Vector3 assignedPosition = ToWorldPosition(assignedPoint);
                string key = BuildFacilityKey(assignedRoom, assignedPoint);
                profile.SetPrimaryWorkstation(assignedRoom, key, assignedPosition);
                profile.SetDeliveryPoint(assignedRoom, key, assignedPosition);
                return;
            }

            CampusGameplayRoom outdoor = ChooseRoom(GetRooms(worldService, CampusRoomType.Outdoor), data != null ? data.Id : string.Empty, staffIndex);
            if (TryChooseFacility(outdoor, DeliveryPointTypes, staffIndex, out CampusGameplayRoom.FacilityRecord point))
            {
                profile.SetPrimaryWorkstation(outdoor, BuildFacilityKey(outdoor, point), ToWorldPosition(point));
                profile.SetDeliveryPoint(outdoor, BuildFacilityKey(outdoor, point), ToWorldPosition(point));
            }
            else
            {
                Vector3 fallback = ResolveRoomPoint(outdoor, staffIndex, 0.25f);
                profile.SetPrimaryWorkstation(outdoor, string.Empty, fallback);
                profile.SetDeliveryPoint(outdoor, string.Empty, fallback);
            }
        }

        private static void BuildCommonAnchors(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            int peerIndex = ResolvePeerIndexByRole(data, rosterService);
            CampusGameplayRoom dorm = ChooseRoom(GetRooms(worldService, CampusRoomType.Dormitory), data != null ? data.Id : string.Empty, peerIndex);
            profile.SetDorm(dorm, ResolveRoomPoint(dorm, peerIndex, 0.35f));

            List<CampusGameplayRoom> commonRooms = GetRooms(worldService, CampusRoomType.CommonActivityZone);
            if (commonRooms.Count == 0)
            {
                commonRooms = GetRooms(worldService, CampusRoomType.Corridor);
            }

            CampusGameplayRoom common = ChooseRoom(commonRooms, data != null ? data.Id : string.Empty, peerIndex);
            profile.SetCommonRoom(common, ResolveRoomPoint(common, peerIndex, 0.55f));

            CampusGameplayRoom deliveryRoom = ChooseRoom(GetRooms(worldService, CampusRoomType.Outdoor), data != null ? data.Id : string.Empty, peerIndex);
            if (TryFindAssignedFacility(
                    worldService,
                    data != null ? data.Assignments.DeliveryPointId : string.Empty,
                    DeliveryPointTypes,
                    out CampusGameplayRoom assignedDeliveryRoom,
                    out CampusGameplayRoom.FacilityRecord assignedDeliveryPoint))
            {
                profile.SetDeliveryPoint(assignedDeliveryRoom, BuildFacilityKey(assignedDeliveryRoom, assignedDeliveryPoint), ToWorldPosition(assignedDeliveryPoint));
            }
            else if (TryChooseFacility(deliveryRoom, DeliveryPointTypes, peerIndex, out CampusGameplayRoom.FacilityRecord deliveryPoint))
            {
                profile.SetDeliveryPoint(deliveryRoom, BuildFacilityKey(deliveryRoom, deliveryPoint), ToWorldPosition(deliveryPoint));
            }
            else
            {
                profile.SetDeliveryPoint(deliveryRoom, string.Empty, ResolveRoomPoint(deliveryRoom, peerIndex, 0.25f));
            }
        }

        private static List<CampusGameplayRoom> GetRooms(CampusWorldService worldService, CampusRoomType roomType)
        {
            List<CampusGameplayRoom> rooms = worldService != null
                ? worldService.GetRoomsByType(roomType, true)
                : new List<CampusGameplayRoom>();
            if (rooms.Count == 0 && worldService != null)
            {
                rooms = worldService.GetRoomsByType(roomType, false);
            }

            rooms.Sort(CompareRooms);
            return rooms;
        }

        private static CampusGameplayRoom ChooseRoom(List<CampusGameplayRoom> rooms, string key, int salt)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return null;
            }

            int index = PositiveModulo(StableHash(key) + salt, rooms.Count);
            return rooms[index];
        }

        private static CampusGameplayRoom ResolveAssignedRoom(
            CampusWorldService worldService,
            string roomId,
            CampusRoomType expectedType)
        {
            if (worldService == null || string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            CampusGameplayRoom room = worldService.FindRoomById(roomId.Trim());
            if (room == null)
            {
                return null;
            }

            return expectedType == CampusRoomType.Unknown || room.RoomType == expectedType
                ? room
                : null;
        }

        private static bool TryFindAssignedFacility(
            CampusWorldService worldService,
            string facilityId,
            CampusFacilityType[] allowedTypes,
            out CampusGameplayRoom room,
            out CampusGameplayRoom.FacilityRecord record)
        {
            room = null;
            record = null;
            if (worldService == null ||
                worldService.RoomRegistry == null ||
                string.IsNullOrWhiteSpace(facilityId))
            {
                return false;
            }

            IReadOnlyList<CampusGameplayRoom> rooms = worldService.RoomRegistry.Rooms;
            string normalizedId = facilityId.Trim();
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                CampusGameplayRoom candidateRoom = rooms[roomIndex];
                if (candidateRoom == null)
                {
                    continue;
                }

                IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = candidateRoom.Facilities;
                for (int facilityIndex = 0; facilityIndex < facilities.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord candidate = facilities[facilityIndex];
                    if (candidate == null ||
                        !MatchesFacilityType(candidate.FacilityType, allowedTypes) ||
                        !MatchesFacilityId(candidateRoom, candidate, normalizedId))
                    {
                        continue;
                    }

                    room = candidateRoom;
                    record = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesFacilityType(CampusFacilityType type, CampusFacilityType[] allowedTypes)
        {
            if (allowedTypes == null || allowedTypes.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (type == allowedTypes[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesFacilityId(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord record,
            string expectedId)
        {
            if (record == null || string.IsNullOrWhiteSpace(expectedId))
            {
                return false;
            }

            if (string.Equals(record.FacilityId, expectedId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string stableId = CampusGameplayFacilityMarker.BuildStableFacilityId(
                room != null ? room.FloorIndex : 1,
                record.FacilityType,
                record.Cell);
            return string.Equals(stableId, expectedId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(BuildLegacyFacilityKey(room, record), expectedId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryChooseFacility(
            CampusGameplayRoom room,
            CampusFacilityType[] types,
            int ownerIndex,
            out CampusGameplayRoom.FacilityRecord record)
        {
            List<CampusGameplayRoom.FacilityRecord> matches = CollectFacilities(room, types);
            if (matches.Count == 0)
            {
                record = null;
                return false;
            }

            record = matches[PositiveModulo(ownerIndex, matches.Count)];
            return true;
        }

        private static bool TryChooseUniqueStudentDesk(
            CampusGameplayRoom classroom,
            int studentIndexInClassroom,
            out CampusGameplayRoom.FacilityRecord record)
        {
            return TryChooseUniqueFacility(classroom, StudentDeskTypes, studentIndexInClassroom, out record);
        }

        private static bool TryChooseUniqueFacility(
            CampusGameplayRoom room,
            CampusFacilityType[] facilityTypes,
            int ownerIndex,
            out CampusGameplayRoom.FacilityRecord record)
        {
            List<CampusGameplayRoom.FacilityRecord> facilities = CollectFacilities(room, facilityTypes);
            if (facilities.Count == 0 || ownerIndex < 0 || ownerIndex >= facilities.Count)
            {
                record = null;
                return false;
            }

            record = facilities[ownerIndex];
            return true;
        }

        private static void AddFacilityPositions(
            CampusGameplayRoom room,
            CampusFacilityType[] types,
            List<Vector3> target)
        {
            if (target == null)
            {
                return;
            }

            List<CampusGameplayRoom.FacilityRecord> records = CollectFacilities(room, types);
            for (int i = 0; i < records.Count; i++)
            {
                target.Add(ToWorldPosition(records[i]));
            }
        }

        private static List<CampusGameplayRoom.FacilityRecord> CollectFacilities(
            CampusGameplayRoom room,
            CampusFacilityType[] types)
        {
            List<CampusGameplayRoom.FacilityRecord> matches = new List<CampusGameplayRoom.FacilityRecord>();
            if (room == null || types == null)
            {
                return matches;
            }

            IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = room.Facilities;
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord record = facilities[i];
                if (record == null)
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    if (record.FacilityType == types[typeIndex])
                    {
                        matches.Add(record);
                        break;
                    }
                }
            }

            matches.Sort(CompareFacilities);
            return matches;
        }

        private static int ResolvePeerIndex(
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
                if (peer == null || peer.Data == null || !predicate(runtime, peer))
                {
                    continue;
                }

                ids.Add(string.IsNullOrWhiteSpace(peer.CharacterId) ? peer.GetInstanceID().ToString() : peer.CharacterId);
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            string targetId = string.IsNullOrWhiteSpace(runtime.CharacterId) ? runtime.GetInstanceID().ToString() : runtime.CharacterId;
            int index = ids.IndexOf(targetId);
            return index >= 0 ? index : PositiveModulo(StableHash(targetId), Math.Max(1, ids.Count));
        }

        private static int ResolveStudentIndexInClassroom(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
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

                CampusGameplayRoom peerClassroom = ChooseRoom(
                    classrooms,
                    string.IsNullOrWhiteSpace(peer.Data.ClassId) ? peer.Data.Id : peer.Data.ClassId,
                    0);
                if (!SameRoom(peerClassroom, classroom))
                {
                    continue;
                }

                studentIds.Add(string.IsNullOrWhiteSpace(peer.CharacterId) ? peer.GetInstanceID().ToString() : peer.CharacterId);
            }

            studentIds.Sort(StringComparer.OrdinalIgnoreCase);
            string targetId = string.IsNullOrWhiteSpace(runtime.CharacterId) ? runtime.GetInstanceID().ToString() : runtime.CharacterId;
            int index = studentIds.IndexOf(targetId);
            return index >= 0 ? index : PositiveModulo(StableHash(targetId), Math.Max(1, studentIds.Count));
        }

        private static bool TryResolveUniqueStudentDesk(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            List<CampusGameplayRoom> classrooms,
            CampusGameplayRoom classroom,
            out CampusGameplayRoom room,
            out CampusGameplayRoom.FacilityRecord record)
        {
            room = classroom;
            record = null;
            if (runtime == null || runtime.Data == null || rosterService == null || classroom == null)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> desks = CollectFacilities(classroom, StudentDeskTypes);
            if (desks.Count == 0)
            {
                return false;
            }

            Dictionary<string, CampusGameplayRoom.FacilityRecord> desksById =
                new Dictionary<string, CampusGameplayRoom.FacilityRecord>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < desks.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord desk = desks[i];
                if (desk == null)
                {
                    continue;
                }

                string deskKey = BuildFacilityKey(classroom, desk);
                if (!string.IsNullOrWhiteSpace(deskKey))
                {
                    desksById[deskKey] = desk;
                }
            }

            List<string> studentIds = new List<string>();
            Dictionary<string, string> savedDeskByStudent =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (CampusCharacterRuntime peer in rosterService.Runtimes)
            {
                if (peer == null || peer.Data == null || peer.Data.Role != CampusCharacterRole.Student)
                {
                    continue;
                }

                CampusGameplayRoom peerClassroom = ChooseRoom(
                    classrooms,
                    string.IsNullOrWhiteSpace(peer.Data.ClassId) ? peer.Data.Id : peer.Data.ClassId,
                    0);
                if (!SameRoom(peerClassroom, classroom))
                {
                    continue;
                }

                string peerId = ResolveRuntimeKey(peer);
                studentIds.Add(peerId);

                string savedDeskId = peer.Data.Assignments != null
                    ? peer.Data.Assignments.StudentDeskId
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(savedDeskId) &&
                    TryFindFacilityInRoom(classroom, desks, savedDeskId, out CampusGameplayRoom.FacilityRecord savedDesk))
                {
                    savedDeskByStudent[peerId] = BuildFacilityKey(classroom, savedDesk);
                }
            }

            studentIds.Sort(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CampusGameplayRoom.FacilityRecord> finalDeskByStudent =
                new Dictionary<string, CampusGameplayRoom.FacilityRecord>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> reservedDeskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int studentIndex = 0; studentIndex < studentIds.Count; studentIndex++)
            {
                string studentId = studentIds[studentIndex];
                if (!savedDeskByStudent.TryGetValue(studentId, out string savedDeskId) ||
                    !desksById.TryGetValue(savedDeskId, out CampusGameplayRoom.FacilityRecord savedDesk) ||
                    reservedDeskIds.Contains(savedDeskId))
                {
                    continue;
                }

                finalDeskByStudent[studentId] = savedDesk;
                reservedDeskIds.Add(savedDeskId);
            }

            for (int studentIndex = 0; studentIndex < studentIds.Count; studentIndex++)
            {
                string studentId = studentIds[studentIndex];
                if (finalDeskByStudent.ContainsKey(studentId))
                {
                    continue;
                }

                for (int deskIndex = 0; deskIndex < desks.Count; deskIndex++)
                {
                    CampusGameplayRoom.FacilityRecord candidate = desks[deskIndex];
                    string deskId = BuildFacilityKey(classroom, candidate);
                    if (string.IsNullOrWhiteSpace(deskId) || reservedDeskIds.Contains(deskId))
                    {
                        continue;
                    }

                    finalDeskByStudent[studentId] = candidate;
                    reservedDeskIds.Add(deskId);
                    break;
                }
            }

            string targetId = ResolveRuntimeKey(runtime);
            return finalDeskByStudent.TryGetValue(targetId, out record) && record != null;
        }

        private static bool TryFindFacilityInRoom(
            CampusGameplayRoom room,
            List<CampusGameplayRoom.FacilityRecord> facilities,
            string facilityId,
            out CampusGameplayRoom.FacilityRecord record)
        {
            record = null;
            if (room == null || facilities == null || string.IsNullOrWhiteSpace(facilityId))
            {
                return false;
            }

            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord candidate = facilities[i];
                if (candidate != null && MatchesFacilityId(room, candidate, facilityId.Trim()))
                {
                    record = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string ResolveRuntimeKey(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.GetInstanceID().ToString()
                : runtime.CharacterId;
        }

        private static int ResolveRoleIndexInChosenRoom(
            CampusCharacterRuntime runtime,
            CampusRosterService rosterService,
            List<CampusGameplayRoom> candidateRooms,
            CampusGameplayRoom chosenRoom,
            CampusCharacterRole role)
        {
            if (runtime == null || runtime.Data == null || rosterService == null || chosenRoom == null)
            {
                return 0;
            }

            List<string> peerIds = new List<string>();
            foreach (CampusCharacterRuntime peer in rosterService.Runtimes)
            {
                if (peer == null || peer.Data == null || peer.Data.Role != role)
                {
                    continue;
                }

                int peerIndex = ResolvePeerIndex(peer, rosterService, (_, candidate) =>
                    candidate != null && candidate.Data != null && candidate.Data.Role == role);
                CampusGameplayRoom peerRoom = ChooseRoom(candidateRooms, peer.Data.Id, peerIndex);
                if (!SameRoom(peerRoom, chosenRoom))
                {
                    continue;
                }

                peerIds.Add(string.IsNullOrWhiteSpace(peer.CharacterId) ? peer.GetInstanceID().ToString() : peer.CharacterId);
            }

            peerIds.Sort(StringComparer.OrdinalIgnoreCase);
            string targetId = string.IsNullOrWhiteSpace(runtime.CharacterId) ? runtime.GetInstanceID().ToString() : runtime.CharacterId;
            int index = peerIds.IndexOf(targetId);
            return index >= 0 ? index : PositiveModulo(StableHash(targetId), Math.Max(1, peerIds.Count));
        }

        private static int ResolvePeerIndexByRole(CampusCharacterData data, CampusRosterService rosterService)
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

            return PositiveModulo(StableHash(data.Id), Math.Max(1, index));
        }

        private static bool IsTeacher(CampusCharacterRuntime owner, CampusCharacterRuntime peer)
        {
            return peer != null && peer.Data != null && peer.Data.Role == CampusCharacterRole.Teacher;
        }

        private static bool IsStaff(CampusCharacterRuntime owner, CampusCharacterRuntime peer)
        {
            return peer != null && peer.Data != null && peer.Data.Role == CampusCharacterRole.Staff;
        }

        private static Vector3 ResolveRoomPoint(CampusGameplayRoom room, int seed, float radius)
        {
            if (room == null)
            {
                return Vector3.zero;
            }

            float angle = PositiveModulo(seed * 97, 360) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Mathf.Max(0f, radius);
            Vector3 result = room.WorldCenter + offset;
            result.z = 0f;
            return result;
        }

        private static Vector3 ToWorldPosition(CampusGameplayRoom.FacilityRecord record)
        {
            if (record == null)
            {
                return Vector3.zero;
            }

            return new Vector3(record.Cell.x + 0.5f, record.Cell.y + 0.5f, 0f);
        }

        private static string BuildFacilityKey(CampusGameplayRoom room, CampusGameplayRoom.FacilityRecord record)
        {
            if (room == null || record == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(record.FacilityId))
            {
                return record.FacilityId.Trim();
            }

            return CampusGameplayFacilityMarker.BuildStableFacilityId(room.FloorIndex, record.FacilityType, record.Cell);
        }

        private static string BuildLegacyFacilityKey(CampusGameplayRoom room, CampusGameplayRoom.FacilityRecord record)
        {
            if (room == null || record == null)
            {
                return string.Empty;
            }

            return room.RoomId + ":" + record.FacilityType + ":" + record.Cell.x + ":" + record.Cell.y;
        }

        private static bool SameRoom(CampusGameplayRoom left, CampusGameplayRoom right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(ResolveRoomKey(left), ResolveRoomKey(right), StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareRooms(CampusGameplayRoom left, CampusGameplayRoom right)
        {
            return string.Compare(ResolveRoomKey(left), ResolveRoomKey(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRoomKey(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(room.RoomId)
                ? room.RoomId
                : room.RoomType + ":" + room.WorldCenter.x + ":" + room.WorldCenter.y;
        }

        private static int CompareFacilities(CampusGameplayRoom.FacilityRecord left, CampusGameplayRoom.FacilityRecord right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int typeCompare = left.FacilityType.CompareTo(right.FacilityType);
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            int xCompare = left.Cell.x.CompareTo(right.Cell.x);
            if (xCompare != 0)
            {
                return xCompare;
            }

            int yCompare = left.Cell.y.CompareTo(right.Cell.y);
            return yCompare != 0
                ? yCompare
                : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                string normalized = value ?? string.Empty;
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash = hash * 31 + char.ToUpperInvariant(normalized[i]);
                }

                return hash;
            }
        }

        private static int PositiveModulo(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int result = value % count;
            return result < 0 ? result + count : result;
        }
    }
}
