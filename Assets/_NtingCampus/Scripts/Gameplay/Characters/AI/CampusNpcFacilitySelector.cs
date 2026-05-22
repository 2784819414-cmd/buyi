using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcFacilitySelector
    {
        public static bool FindAssigned(
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

        public static bool TryChoose(
            CampusGameplayRoom room,
            CampusFacilityType[] types,
            int ownerIndex,
            out CampusGameplayRoom.FacilityRecord record)
        {
            List<CampusGameplayRoom.FacilityRecord> matches = Collect(room, types);
            if (matches.Count == 0)
            {
                record = null;
                return false;
            }

            record = matches[CampusNpcStableIds.PositiveModulo(ownerIndex, matches.Count)];
            return true;
        }

        public static bool TryChooseUnique(
            CampusGameplayRoom room,
            CampusFacilityType[] types,
            int ownerIndex,
            out CampusGameplayRoom.FacilityRecord record)
        {
            List<CampusGameplayRoom.FacilityRecord> matches = Collect(room, types);
            if (matches.Count == 0 || ownerIndex < 0 || ownerIndex >= matches.Count)
            {
                record = null;
                return false;
            }

            record = matches[ownerIndex];
            return true;
        }

        public static void AddPositions(
            CampusGameplayRoom room,
            CampusFacilityType[] types,
            List<Vector3> target)
        {
            if (target == null)
            {
                return;
            }

            List<CampusGameplayRoom.FacilityRecord> records = Collect(room, types);
            for (int i = 0; i < records.Count; i++)
            {
                target.Add(PositionOf(records[i]));
            }
        }

        public static List<CampusGameplayRoom.FacilityRecord> Collect(
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
                if (record != null && MatchesFacilityType(record.FacilityType, types))
                {
                    matches.Add(record);
                }
            }

            matches.Sort(CompareFacilities);
            return matches;
        }

        public static bool TryFindInRoom(
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

            string normalizedId = facilityId.Trim();
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord candidate = facilities[i];
                if (candidate != null && MatchesFacilityId(room, candidate, normalizedId))
                {
                    record = candidate;
                    return true;
                }
            }

            return false;
        }

        public static Vector3 PositionOf(CampusGameplayRoom.FacilityRecord record)
        {
            if (record == null)
            {
                return Vector3.zero;
            }

            return new Vector3(record.Cell.x + 0.5f, record.Cell.y + 0.5f, 0f);
        }

        public static string KeyFor(CampusGameplayRoom room, CampusGameplayRoom.FacilityRecord record)
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

        private static string BuildLegacyFacilityKey(CampusGameplayRoom room, CampusGameplayRoom.FacilityRecord record)
        {
            if (room == null || record == null)
            {
                return string.Empty;
            }

            return room.RoomId + ":" + record.FacilityType + ":" + record.Cell.x + ":" + record.Cell.y;
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

    }
}
