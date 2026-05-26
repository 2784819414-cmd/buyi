using System;
using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    internal static class CampusGameplayServiceStationCollector
    {
        public static void AssignServiceStations(
            CampusRuntimeGameplayOverlayLoader overlayLoader,
            Func<string, CampusGameplayRoom> findRoomById,
            IReadOnlyList<CampusGameplayRoom> rooms)
        {
            if (findRoomById == null)
            {
                return;
            }

            CampusGameplayServiceStationMarker[] markers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayServiceStationMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            HashSet<string> stationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayServiceStationMarker marker = markers[i];
                if (marker == null ||
                    (overlayLoader != null && !overlayLoader.ShouldIncludeExplicitMarker(marker)))
                {
                    continue;
                }

                string stationId = CampusGameplayServiceStationMarker.NormalizeId(marker.StationId);
                if (string.IsNullOrEmpty(stationId) || !stationIds.Add(stationId))
                {
                    continue;
                }

                CampusGameplayRoom room = findRoomById(marker.RoomId) ??
                                          FindRoomByOwnerFacility(rooms, marker.OwnerFacilityId);
                if (room == null)
                {
                    continue;
                }

                CampusGameplayServiceStationRecord record = new CampusGameplayServiceStationRecord();
                record.Bind(
                    marker.StationId,
                    marker.StationTypeId,
                    room.RoomId,
                    marker.OwnerFacilityId,
                    marker.Slots);
                room.AddServiceStation(record);
            }
        }

        private static CampusGameplayRoom FindRoomByOwnerFacility(
            IReadOnlyList<CampusGameplayRoom> rooms,
            string ownerFacilityId)
        {
            string normalizedOwnerId = CampusGameplayServiceStationMarker.NormalizeId(ownerFacilityId);
            if (rooms == null || string.IsNullOrEmpty(normalizedOwnerId))
            {
                return null;
            }

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                CampusGameplayRoom room = rooms[roomIndex];
                if (room == null || room.Facilities == null)
                {
                    continue;
                }

                for (int facilityIndex = 0; facilityIndex < room.Facilities.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord facility = room.Facilities[facilityIndex];
                    if (facility != null &&
                        string.Equals(facility.FacilityId, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
                    {
                        return room;
                    }
                }
            }

            return null;
        }
    }
}
