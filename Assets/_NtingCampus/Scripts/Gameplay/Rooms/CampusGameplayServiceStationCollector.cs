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
            Func<string, CampusGameplayRoom> findRoomById)
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

                CampusGameplayRoom room = findRoomById(marker.RoomId);
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
    }
}
