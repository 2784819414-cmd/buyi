using System.Collections.Generic;
using System.IO;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeGameplayOverlayAuthoring
    {
        internal delegate CampusFloorRoot FloorResolver(int floorIndex);
        internal delegate void RuntimeObjectDestroyer(UnityEngine.Object target);

        internal static void CaptureSceneMarkers(
            CampusRuntimeGameplayOverlaySnapshot snapshot,
            CampusMapRoot mapRoot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.Rooms = snapshot.Rooms ?? new List<CampusRuntimeGameplayRoomSnapshot>();
            snapshot.Facilities = snapshot.Facilities ?? new List<CampusRuntimeGameplayFacilitySnapshot>();
            snapshot.ServiceStations = snapshot.ServiceStations ?? new List<CampusRuntimeGameplayServiceStationSnapshot>();
            CaptureRooms(snapshot.Rooms);
            CaptureFacilities(snapshot.Facilities);
            CaptureServiceStations(snapshot.ServiceStations);
        }

        internal static void CaptureRooms(List<CampusRuntimeGameplayRoomSnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            CampusGameplayRoomMarker[] markers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayRoomMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            HashSet<string> capturedKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                Vector3Int anchor = NormalizeCell(marker.AnchorCell);
                Vector2Int size = marker.RoomSize;
                string key = marker.FloorIndex + "|" + marker.RoomType + "|" + anchor + "|" + size + "|" + marker.RoomDisplayName;
                if (!capturedKeys.Add(key))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayRoomSnapshot
                {
                    Id = marker.RoomIdOverride,
                    DisplayName = marker.GetPrimaryDisplayName(),
                    LocalizedDisplayName = marker.LocalizedDisplayName,
                    RoomType = marker.RoomType,
                    FloorIndex = Mathf.Max(1, marker.FloorIndex),
                    AnchorCell = anchor,
                    Size = size,
                    UsableForGameplay = marker.UsableForGameplay
                });
            }
        }

        internal static void CaptureFacilities(List<CampusRuntimeGameplayFacilitySnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            CampusGameplayFacilityMarker[] markers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayFacilityMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            HashSet<string> capturedKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                Vector3Int cell = NormalizeCell(marker.Cell);
                string id = CampusGameplayFacilityMarker.NormalizeFacilityId(marker.FacilityId);
                if (string.IsNullOrEmpty(id))
                {
                    id = CampusGameplayFacilityMarker.BuildStableFacilityId(
                        marker.FloorIndex,
                        marker.FacilityType,
                        cell);
                }

                if (!capturedKeys.Add(id))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayFacilitySnapshot
                {
                    Id = id,
                    DisplayName = marker.DisplayName,
                    FacilityType = marker.FacilityType,
                    FloorIndex = Mathf.Max(1, marker.FloorIndex),
                    Cell = cell,
                    CountsAsCoreFacility = marker.CountsAsCoreFacility
                });
            }
        }

        internal static void CaptureServiceStations(List<CampusRuntimeGameplayServiceStationSnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            CampusGameplayServiceStationMarker[] markers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayServiceStationMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            HashSet<string> capturedIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayServiceStationMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                string id = CampusGameplayServiceStationMarker.NormalizeId(marker.StationId);
                if (string.IsNullOrEmpty(id) || !capturedIds.Add(id))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayServiceStationSnapshot
                {
                    Id = id,
                    StationTypeId = marker.StationTypeId,
                    RoomId = marker.RoomId,
                    OwnerFacilityId = marker.OwnerFacilityId,
                    Slots = BuildServiceStationSlotSnapshots(marker.Slots)
                });
            }
        }

        private static List<CampusRuntimeGameplayServiceStationSlotSnapshot> BuildServiceStationSlotSnapshots(
            IReadOnlyList<CampusGameplayServiceStationSlotBinding> sourceSlots)
        {
            List<CampusRuntimeGameplayServiceStationSlotSnapshot> slots =
                new List<CampusRuntimeGameplayServiceStationSlotSnapshot>();
            if (sourceSlots == null)
            {
                return slots;
            }

            for (int i = 0; i < sourceSlots.Count; i++)
            {
                CampusGameplayServiceStationSlotBinding source = sourceSlots[i];
                if (source == null)
                {
                    continue;
                }

                slots.Add(new CampusRuntimeGameplayServiceStationSlotSnapshot
                {
                    RoleId = source.RoleId,
                    FacilityIds = source.FacilityIds != null
                        ? new List<string>(source.FacilityIds)
                        : new List<string>()
                });
            }

            return slots;
        }

        private static List<CampusGameplayServiceStationSlotBinding> BuildServiceStationBindings(
            IReadOnlyList<CampusRuntimeGameplayServiceStationSlotSnapshot> sourceSlots)
        {
            List<CampusGameplayServiceStationSlotBinding> bindings =
                new List<CampusGameplayServiceStationSlotBinding>();
            if (sourceSlots == null)
            {
                return bindings;
            }

            for (int i = 0; i < sourceSlots.Count; i++)
            {
                CampusRuntimeGameplayServiceStationSlotSnapshot source = sourceSlots[i];
                if (source == null)
                {
                    continue;
                }

                source.Normalize();
                CampusGameplayServiceStationSlotBinding binding = new CampusGameplayServiceStationSlotBinding();
                binding.Configure(source.RoleId, source.FacilityIds);
                bindings.Add(binding);
            }

            return bindings;
        }

        internal static void CaptureRoomPrefabRooms(
            CampusFloorRoot floor,
            BoundsInt bounds,
            Vector3Int originCell,
            List<CampusRuntimeGameplayRoomSnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            if (floor == null)
            {
                return;
            }

            CampusGameplayRoomMarker[] markers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayRoomMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayRoomMarker marker = markers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                BoundsInt markerBounds = marker.BuildBounds();
                if (!BoundsContains2D(bounds, markerBounds))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayRoomSnapshot
                {
                    Id = marker.RoomIdOverride,
                    DisplayName = marker.GetPrimaryDisplayName(),
                    LocalizedDisplayName = marker.LocalizedDisplayName,
                    RoomType = marker.RoomType,
                    FloorIndex = 0,
                    AnchorCell = ToRelativeCell(NormalizeCell(marker.AnchorCell), originCell),
                    Size = marker.RoomSize,
                    UsableForGameplay = marker.UsableForGameplay
                });
            }
        }

        internal static void CaptureRoomPrefabFacilities(
            CampusFloorRoot floor,
            BoundsInt bounds,
            Vector3Int originCell,
            List<CampusRuntimeGameplayFacilitySnapshot> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            if (floor == null)
            {
                return;
            }

            CampusGameplayFacilityMarker[] markers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayFacilityMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            List<CampusGameplayFacilityMarker> capturedMarkers = new List<CampusGameplayFacilityMarker>();
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = markers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Vector3Int cell = NormalizeCell(marker.Cell);
                if (!CellInBounds(bounds, cell))
                {
                    continue;
                }

                capturedMarkers.Add(marker);
            }

            for (int i = 0; i < capturedMarkers.Count; i++)
            {
                CampusGameplayFacilityMarker marker = capturedMarkers[i];
                Vector3Int cell = NormalizeCell(marker.Cell);
                output.Add(new CampusRuntimeGameplayFacilitySnapshot
                {
                    Id = CampusGameplayFacilityMarker.BuildStableFacilityId(
                        0,
                        marker.FacilityType,
                        ToRelativeCell(cell, originCell)),
                    DisplayName = marker.DisplayName,
                    FacilityType = marker.FacilityType,
                    FloorIndex = 0,
                    Cell = ToRelativeCell(cell, originCell),
                    CountsAsCoreFacility = marker.CountsAsCoreFacility
                });
            }
        }

        internal static void SpawnSceneMarkers(
            CampusRuntimeGameplayOverlaySnapshot snapshot,
            FloorResolver resolveFloor)
        {
            if (snapshot == null)
            {
                return;
            }

            SpawnRooms(snapshot.Rooms, resolveFloor);
            SpawnFacilities(snapshot.Facilities, resolveFloor);
            SpawnServiceStations(snapshot.ServiceStations, resolveFloor);
        }

        internal static void SpawnRooms(
            IReadOnlyList<CampusRuntimeGameplayRoomSnapshot> rooms,
            FloorResolver resolveFloor)
        {
            if (rooms == null || resolveFloor == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                CampusRuntimeGameplayRoomSnapshot room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                room.Normalize();
                CampusFloorRoot floor = resolveFloor(Mathf.Max(1, room.FloorIndex));
                if (floor == null || floor.Grid == null || floor.PropsRoot == null)
                {
                    continue;
                }

                Vector3Int anchor = NormalizeCell(room.AnchorCell);
                GameObject markerObject = CreateMarkerObject(
                    floor,
                    anchor,
                    "GameplayRoom_" + room.RoomType + "_F" + floor.FloorIndex + "_" + anchor.x + "_" + anchor.y);
                if (markerObject == null)
                {
                    continue;
                }

                CampusGameplayRoomMarker marker = markerObject.AddComponent<CampusGameplayRoomMarker>();
                marker.Configure(
                    room.Id,
                    room.DisplayName,
                    room.LocalizedDisplayName,
                    room.RoomType,
                    floor.FloorIndex,
                    anchor,
                    room.Size,
                    room.UsableForGameplay);
            }
        }

        internal static void SpawnFacilities(
            IReadOnlyList<CampusRuntimeGameplayFacilitySnapshot> facilities,
            FloorResolver resolveFloor)
        {
            if (facilities == null || resolveFloor == null)
            {
                return;
            }

            for (int i = 0; i < facilities.Count; i++)
            {
                CampusRuntimeGameplayFacilitySnapshot facility = facilities[i];
                if (facility == null)
                {
                    continue;
                }

                facility.Normalize();
                CampusFloorRoot floor = resolveFloor(Mathf.Max(1, facility.FloorIndex));
                if (floor == null || floor.Grid == null || floor.PropsRoot == null)
                {
                    continue;
                }

                Vector3Int cell = NormalizeCell(facility.Cell);
                GameObject markerObject = CreateMarkerObject(
                    floor,
                    cell,
                    "GameplayFacility_" + facility.FacilityType + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
                if (markerObject == null)
                {
                    continue;
                }

                CampusGameplayFacilityMarker marker = markerObject.AddComponent<CampusGameplayFacilityMarker>();
                marker.Configure(
                    facility.Id,
                    facility.DisplayName,
                    facility.FacilityType,
                    floor.FloorIndex,
                    cell,
                    facility.CountsAsCoreFacility,
                    null);
            }
        }

        internal static void SpawnServiceStations(
            IReadOnlyList<CampusRuntimeGameplayServiceStationSnapshot> serviceStations,
            FloorResolver resolveFloor)
        {
            if (serviceStations == null || resolveFloor == null)
            {
                return;
            }

            for (int i = 0; i < serviceStations.Count; i++)
            {
                CampusRuntimeGameplayServiceStationSnapshot serviceStation = serviceStations[i];
                if (serviceStation == null)
                {
                    continue;
                }

                serviceStation.Normalize();
                CampusFloorRoot floor = resolveFloor(1);
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                GameObject markerObject = new GameObject("GameplayServiceStation_" + serviceStation.Id);
                markerObject.transform.SetParent(floor.PropsRoot, false);
                CampusGameplayServiceStationMarker marker =
                    markerObject.AddComponent<CampusGameplayServiceStationMarker>();
                marker.Configure(
                    serviceStation.Id,
                    serviceStation.StationTypeId,
                    serviceStation.RoomId,
                    serviceStation.OwnerFacilityId,
                    BuildServiceStationBindings(serviceStation.Slots));
            }
        }

        internal static bool CreateFacilityMarker(
            CampusFloorRoot floor,
            Vector3Int cell,
            string displayName,
            CampusFacilityType facilityType)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return false;
            }

            Vector3Int normalizedCell = NormalizeCell(cell);
            GameObject markerObject = CreateMarkerObject(
                floor,
                normalizedCell,
                "GameplayFacility_" + facilityType + "_F" + floor.FloorIndex + "_" + normalizedCell.x + "_" + normalizedCell.y);
            if (markerObject == null)
            {
                return false;
            }

            CampusGameplayFacilityMarker marker = markerObject.AddComponent<CampusGameplayFacilityMarker>();
            marker.Configure(
                string.Empty,
                displayName,
                facilityType,
                floor.FloorIndex,
                normalizedCell,
                true,
                null);

            floor.MarkUsedBoundsDirty();
            return true;
        }

        internal static bool EraseMarkersAtCell(
            CampusFloorRoot floor,
            Vector3Int cell,
            bool eraseRooms,
            bool eraseFacilities,
            bool reservedEraseFlag,
            CampusMapRoot mapRoot,
            RuntimeObjectDestroyer destroyObject)
        {
            if (floor == null || destroyObject == null)
            {
                return false;
            }

            Vector3Int normalizedCell = NormalizeCell(cell);
            bool erased = false;

            if (eraseRooms)
            {
                CampusGameplayRoomMarker[] roomMarkers =
                    UnityEngine.Object.FindObjectsByType<CampusGameplayRoomMarker>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                for (int i = roomMarkers.Length - 1; i >= 0; i--)
                {
                    CampusGameplayRoomMarker marker = roomMarkers[i];
                    if (marker == null || marker.FloorIndex != floor.FloorIndex)
                    {
                        continue;
                    }

                    if (CellInBounds(marker.BuildBounds(), normalizedCell))
                    {
                        destroyObject(marker.gameObject);
                        erased = true;
                    }
                }
            }

            if (eraseFacilities)
            {
                CampusGameplayFacilityMarker[] facilityMarkers =
                    UnityEngine.Object.FindObjectsByType<CampusGameplayFacilityMarker>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                for (int i = facilityMarkers.Length - 1; i >= 0; i--)
                {
                    CampusGameplayFacilityMarker marker = facilityMarkers[i];
                    if (marker != null &&
                        marker.FloorIndex == floor.FloorIndex &&
                        NormalizeCell(marker.Cell) == normalizedCell)
                    {
                        destroyObject(marker.gameObject);
                        erased = true;
                    }
                }
            }

            if (erased)
            {
                floor.MarkUsedBoundsDirty();
            }

            return erased;
        }

        internal static bool EraseMarkersInBounds(
            CampusFloorRoot floor,
            BoundsInt bounds,
            CampusMapRoot mapRoot,
            RuntimeObjectDestroyer destroyObject)
        {
            if (floor == null || destroyObject == null)
            {
                return false;
            }

            bool erased = false;
            CampusGameplayRoomMarker[] roomMarkers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayRoomMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = roomMarkers.Length - 1; i >= 0; i--)
            {
                CampusGameplayRoomMarker marker = roomMarkers[i];
                if (marker != null &&
                    marker.FloorIndex == floor.FloorIndex &&
                    BoundsOverlap2D(marker.BuildBounds(), bounds))
                {
                    destroyObject(marker.gameObject);
                    erased = true;
                }
            }

            CampusGameplayFacilityMarker[] facilityMarkers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayFacilityMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = facilityMarkers.Length - 1; i >= 0; i--)
            {
                CampusGameplayFacilityMarker marker = facilityMarkers[i];
                if (marker != null &&
                    marker.FloorIndex == floor.FloorIndex &&
                    CellInBounds(bounds, NormalizeCell(marker.Cell)))
                {
                    destroyObject(marker.gameObject);
                    erased = true;
                }
            }

            if (erased)
            {
                floor.MarkUsedBoundsDirty();
            }

            return erased;
        }

        internal static void ClearSceneMarkers(RuntimeObjectDestroyer destroyObject)
        {
            if (destroyObject == null)
            {
                return;
            }

            CampusRuntimeGameplayOverlayEntity[] entities =
                UnityEngine.Object.FindObjectsByType<CampusRuntimeGameplayOverlayEntity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = entities.Length - 1; i >= 0; i--)
            {
                CampusRuntimeGameplayOverlayEntity entity = entities[i];
                if (entity == null || entity.IsActorEntity)
                {
                    continue;
                }

                if (entity.GetComponent<CampusGameplayRoomMarker>() != null ||
                    entity.GetComponent<CampusGameplayFacilityMarker>() != null)
                {
                    destroyObject(entity.gameObject);
                }
            }
        }

        internal static bool TryResolveMarkerCell(
            Component component,
            CampusMapRoot mapRoot,
            out int floorIndex,
            out Vector3Int cell)
        {
            floorIndex = 1;
            cell = Vector3Int.zero;
            if (component == null)
            {
                return false;
            }

            CampusRuntimeGameplayOverlayEntity entity =
                component.GetComponent<CampusRuntimeGameplayOverlayEntity>();
            if (entity != null)
            {
                floorIndex = entity.FloorIndex;
                cell = NormalizeCell(entity.Cell);
                return true;
            }

            CampusGameplayFacilityMarker facility = component.GetComponent<CampusGameplayFacilityMarker>();
            if (facility != null)
            {
                floorIndex = Mathf.Max(1, facility.FloorIndex);
                cell = NormalizeCell(facility.Cell);
                return true;
            }

            CampusGameplayRoomMarker room = component.GetComponent<CampusGameplayRoomMarker>();
            if (room != null)
            {
                floorIndex = Mathf.Max(1, room.FloorIndex);
                cell = NormalizeCell(room.AnchorCell);
                return true;
            }

            if (mapRoot == null || mapRoot.Floors == null)
            {
                return false;
            }

            Vector3 position = component.transform.position;
            float bestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = mapRoot.Floors[i];
                if (floor == null || floor.Grid == null)
                {
                    continue;
                }

                Vector3Int candidate = NormalizeCell(floor.Grid.WorldToCell(position));
                Vector3 center = floor.Grid.GetCellCenterWorld(candidate);
                float distance = Vector2.Distance(center, position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    floorIndex = floor.FloorIndex;
                    cell = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static Vector3Int NormalizeCell(Vector3Int cell)
        {
            return new Vector3Int(cell.x, cell.y, 0);
        }

        private static Vector3Int ToRelativeCell(Vector3Int cell, Vector3Int originCell)
        {
            return new Vector3Int(cell.x - originCell.x, cell.y - originCell.y, 0);
        }

        private static bool CellInBounds(BoundsInt bounds, Vector3Int cell)
        {
            return cell.x >= bounds.xMin &&
                   cell.x < bounds.xMax &&
                   cell.y >= bounds.yMin &&
                   cell.y < bounds.yMax;
        }

        private static bool BoundsContains2D(BoundsInt container, BoundsInt contained)
        {
            return contained.xMin >= container.xMin &&
                   contained.xMax <= container.xMax &&
                   contained.yMin >= container.yMin &&
                   contained.yMax <= container.yMax;
        }

        private static bool BoundsOverlap2D(BoundsInt a, BoundsInt b)
        {
            return a.xMin < b.xMax &&
                   a.xMax > b.xMin &&
                   a.yMin < b.yMax &&
                   a.yMax > b.yMin;
        }

        private static GameObject CreateMarkerObject(CampusFloorRoot floor, Vector3Int cell, string objectName)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return null;
            }

            Vector3Int normalizedCell = NormalizeCell(cell);
            GameObject markerObject = new GameObject(objectName);
            markerObject.transform.SetParent(floor.PropsRoot, false);
            markerObject.transform.position = floor.Grid.GetCellCenterWorld(normalizedCell);

            CampusRuntimeGameplayOverlayEntity entity =
                markerObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
            entity.Configure(false, floor.FloorIndex, normalizedCell);
            return markerObject;
        }
    }
}
