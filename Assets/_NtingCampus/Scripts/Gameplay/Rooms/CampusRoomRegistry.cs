using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    public sealed class CampusRoomRegistry : MonoBehaviour
    {
        [SerializeField] private CampusMapRoot mapRoot;
        [SerializeField] private bool logValidationIssues = true;
        [SerializeField] private List<CampusGameplayRoom> rooms = new List<CampusGameplayRoom>();

        private readonly Dictionary<string, CampusGameplayRoom> roomsById =
            new Dictionary<string, CampusGameplayRoom>(StringComparer.OrdinalIgnoreCase);

        private readonly List<CampusRoomValidator.ValidationIssue> validationIssues =
            new List<CampusRoomValidator.ValidationIssue>();

        public IReadOnlyList<CampusGameplayRoom> Rooms => rooms;
        public IReadOnlyList<CampusRoomValidator.ValidationIssue> ValidationIssues => validationIssues;

        public void RebuildRegistry()
        {
            mapRoot = ResolveMapRoot();
            rooms.Clear();
            roomsById.Clear();
            validationIssues.Clear();

            if (mapRoot == null)
            {
                validationIssues.Add(new CampusRoomValidator.ValidationIssue(
                    CampusRoomValidator.Severity.Error,
                    string.Empty,
                    "CampusMapRoot was not found."));
                MaybeLogValidationIssues();
                return;
            }

            mapRoot.RebuildFloorReferences();
            BuildRoomsFromGameplayMarkers();

            if (rooms.Count == 0)
            {
                Dictionary<int, List<CampusRuntimeRoomMarker>> markersByFloor = CollectMarkersByFloor(mapRoot);
                foreach (KeyValuePair<int, List<CampusRuntimeRoomMarker>> pair in markersByFloor)
                {
                    BuildRoomsForFloor(pair.Key, pair.Value);
                }
            }

            AssignFacilities(mapRoot);
            validationIssues.AddRange(CampusRoomValidator.Validate(rooms));
            ApplyValidationState();
            MaybeLogValidationIssues();
            LogRegistrationSummary();
        }

        public bool TryGetRoom(string roomId, out CampusGameplayRoom room)
        {
            return roomsById.TryGetValue(NormalizeKey(roomId), out room);
        }

        public CampusGameplayRoom FindRoomByCell(int floorIndex, Vector3Int cell)
        {
            CampusGameplayRoom bestRoom = null;
            int bestArea = int.MaxValue;
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null || room.FloorIndex != floorIndex || !room.ContainsCell(cell))
                {
                    continue;
                }

                BoundsInt bounds = room.MarkerBounds;
                int area = Mathf.Max(1, bounds.size.x * bounds.size.y);
                if (bestRoom == null || area < bestArea)
                {
                    bestRoom = room;
                    bestArea = area;
                }
            }

            return bestRoom;
        }

        private CampusMapRoot ResolveMapRoot()
        {
            if (mapRoot != null)
            {
                return mapRoot;
            }

            mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            return mapRoot;
        }

        private static Dictionary<int, List<CampusRuntimeRoomMarker>> CollectMarkersByFloor(CampusMapRoot root)
        {
            Dictionary<int, List<CampusRuntimeRoomMarker>> markersByFloor = new Dictionary<int, List<CampusRuntimeRoomMarker>>();
            if (root == null || root.Floors == null)
            {
                return markersByFloor;
            }

            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusFloorRoot floor = root.Floors[i];
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
                if (!markersByFloor.TryGetValue(floor.FloorIndex, out List<CampusRuntimeRoomMarker> floorMarkers))
                {
                    floorMarkers = new List<CampusRuntimeRoomMarker>();
                    markersByFloor[floor.FloorIndex] = floorMarkers;
                }

                for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                {
                    CampusRuntimeRoomMarker marker = markers[markerIndex];
                    if (marker != null)
                    {
                        floorMarkers.Add(marker);
                    }
                }
            }

            return markersByFloor;
        }

        private void BuildRoomsFromGameplayMarkers()
        {
            CampusRuntimeGameplayOverlayLoader overlayLoader = CampusRuntimeGameplayOverlayLoader.Instance;
            CampusGameplayRoomMarker[] gameplayMarkers =
                FindObjectsByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < gameplayMarkers.Length; i++)
            {
                CampusGameplayRoomMarker gameplayMarker = gameplayMarkers[i];
                if (gameplayMarker == null || (overlayLoader != null && !overlayLoader.ShouldIncludeExplicitMarker(gameplayMarker)))
                {
                    continue;
                }

                BoundsInt bounds = gameplayMarker.BuildBounds();
                CampusGameplayRoom room = new CampusGameplayRoom();
                string roomId = string.IsNullOrWhiteSpace(gameplayMarker.RoomIdOverride)
                    ? BuildRoomId(gameplayMarker.RoomDisplayName, gameplayMarker.FloorIndex, i + 1)
                    : NormalizeKey(gameplayMarker.RoomIdOverride);
                string roomName = string.IsNullOrWhiteSpace(gameplayMarker.RoomDisplayName)
                    ? gameplayMarker.RoomType.ToString()
                    : gameplayMarker.RoomDisplayName;
                room.BindFromGameplayMarker(
                    roomId,
                    roomName,
                    gameplayMarker.RoomType,
                    gameplayMarker.FloorIndex,
                    bounds,
                    gameplayMarker.transform.position,
                    gameplayMarker);

                rooms.Add(room);
                roomsById[room.RoomId] = room;
            }
        }

        private void BuildRoomsForFloor(int floorIndex, List<CampusRuntimeRoomMarker> floorMarkers)
        {
            if (floorMarkers == null || floorMarkers.Count == 0)
            {
                return;
            }

            Dictionary<string, List<CampusRuntimeRoomMarker>> markersByName =
                new Dictionary<string, List<CampusRuntimeRoomMarker>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < floorMarkers.Count; i++)
            {
                CampusRuntimeRoomMarker marker = floorMarkers[i];
                if (marker == null)
                {
                    continue;
                }

                string roomName = string.IsNullOrWhiteSpace(marker.RoomName) ? "UnnamedRoom" : marker.RoomName.Trim();
                if (!markersByName.TryGetValue(roomName, out List<CampusRuntimeRoomMarker> namedMarkers))
                {
                    namedMarkers = new List<CampusRuntimeRoomMarker>();
                    markersByName[roomName] = namedMarkers;
                }

                namedMarkers.Add(marker);
            }

            foreach (KeyValuePair<string, List<CampusRuntimeRoomMarker>> pair in markersByName)
            {
                CreateClusteredRooms(floorIndex, pair.Key, pair.Value);
            }
        }

        private void CreateClusteredRooms(int floorIndex, string roomName, List<CampusRuntimeRoomMarker> sourceMarkers)
        {
            Dictionary<Vector3Int, CampusRuntimeRoomMarker> markerByCell = new Dictionary<Vector3Int, CampusRuntimeRoomMarker>();
            for (int i = 0; i < sourceMarkers.Count; i++)
            {
                CampusRuntimeRoomMarker marker = sourceMarkers[i];
                if (marker != null)
                {
                    markerByCell[marker.Cell] = marker;
                }
            }

            HashSet<Vector3Int> remainingCells = new HashSet<Vector3Int>(markerByCell.Keys);
            int clusterIndex = 0;
            while (remainingCells.Count > 0)
            {
                Vector3Int startCell = default;
                foreach (Vector3Int cell in remainingCells)
                {
                    startCell = cell;
                    break;
                }

                List<CampusRuntimeRoomMarker> clusterMarkers = new List<CampusRuntimeRoomMarker>();
                Queue<Vector3Int> frontier = new Queue<Vector3Int>();
                frontier.Enqueue(startCell);
                remainingCells.Remove(startCell);

                int minX = startCell.x;
                int minY = startCell.y;
                int minZ = startCell.z;
                int maxX = startCell.x;
                int maxY = startCell.y;
                int maxZ = startCell.z;

                while (frontier.Count > 0)
                {
                    Vector3Int cell = frontier.Dequeue();
                    if (markerByCell.TryGetValue(cell, out CampusRuntimeRoomMarker marker) && marker != null)
                    {
                        clusterMarkers.Add(marker);
                    }

                    minX = Mathf.Min(minX, cell.x);
                    minY = Mathf.Min(minY, cell.y);
                    minZ = Mathf.Min(minZ, cell.z);
                    maxX = Mathf.Max(maxX, cell.x);
                    maxY = Mathf.Max(maxY, cell.y);
                    maxZ = Mathf.Max(maxZ, cell.z);

                    EnqueueNeighbor(cell + Vector3Int.right);
                    EnqueueNeighbor(cell + Vector3Int.left);
                    EnqueueNeighbor(cell + Vector3Int.up);
                    EnqueueNeighbor(cell + Vector3Int.down);
                }

                clusterIndex++;
                BoundsInt bounds = new BoundsInt(
                    new Vector3Int(minX, minY, minZ),
                    new Vector3Int(maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1));
                Vector3 center = ResolveWorldCenter(clusterMarkers, bounds);

                CampusGameplayRoom room = new CampusGameplayRoom();
                room.Bind(
                    BuildRoomId(roomName, floorIndex, clusterIndex),
                    roomName,
                    InferRoomType(roomName),
                    floorIndex,
                    bounds,
                    center,
                    clusterMarkers);

                rooms.Add(room);
                roomsById[room.RoomId] = room;

                void EnqueueNeighbor(Vector3Int neighbor)
                {
                    if (!remainingCells.Remove(neighbor))
                    {
                        return;
                    }

                    frontier.Enqueue(neighbor);
                }
            }
        }

        private static Vector3 ResolveWorldCenter(List<CampusRuntimeRoomMarker> markers, BoundsInt bounds)
        {
            if (markers != null && markers.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < markers.Count; i++)
                {
                    CampusRuntimeRoomMarker marker = markers[i];
                    if (marker == null)
                    {
                        continue;
                    }

                    sum += marker.transform.position;
                    count++;
                }

                if (count > 0)
                {
                    return sum / count;
                }
            }

            Vector3Int centerCell = bounds.position + new Vector3Int(bounds.size.x / 2, bounds.size.y / 2, bounds.size.z / 2);
            return centerCell;
        }

        private void AssignFacilities(CampusMapRoot root)
        {
            CampusRuntimeGameplayOverlayLoader overlayLoader = CampusRuntimeGameplayOverlayLoader.Instance;
            CampusGameplayFacilityMarker[] explicitFacilities =
                FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < explicitFacilities.Length; i++)
            {
                CampusGameplayFacilityMarker facilityMarker = explicitFacilities[i];
                if (facilityMarker == null || (overlayLoader != null && !overlayLoader.ShouldIncludeExplicitMarker(facilityMarker)))
                {
                    continue;
                }

                CampusGameplayRoom room = FindRoomByCell(facilityMarker.FloorIndex, facilityMarker.Cell);
                if (room == null)
                {
                    continue;
                }

                if (facilityMarker.LinkedPlacedObject != null)
                {
                    room.AddFacility(facilityMarker.LinkedPlacedObject, facilityMarker.FacilityType);
                }
                else
                {
                    room.AddExplicitFacility(facilityMarker.DisplayName, facilityMarker.FacilityType, facilityMarker.Cell);
                }
            }

            if (root == null || root.Floors == null)
            {
                return;
            }

            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusFloorRoot floor = root.Floors[i];
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                CampusPlacedObject[] placedObjects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int objectIndex = 0; objectIndex < placedObjects.Length; objectIndex++)
                {
                    CampusPlacedObject placedObject = placedObjects[objectIndex];
                    if (placedObject == null)
                    {
                        continue;
                    }

                    CampusGameplayRoom room = FindRoomByCell(floor.FloorIndex, placedObject.Cell);
                    if (room == null)
                    {
                        continue;
                    }

                    room.AddFacility(placedObject, InferFacilityType(placedObject));
                }
            }
        }

        private void ApplyValidationState()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                CampusRoomValidator.ValidationSummary summary = CampusRoomValidator.Summarize(room);
                room.ApplyValidationState(summary.IsValid, summary.IsUsableForGameplay, summary.Message);
            }
        }

        private void MaybeLogValidationIssues()
        {
            if (logValidationIssues)
            {
                CampusRoomValidator.LogIssues(validationIssues);
            }
        }

        private void LogRegistrationSummary()
        {
            Debug.Log("[Rooms] Registered " + rooms.Count + " rooms.");
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                Debug.Log(
                    "[Rooms][" + room.RoomId + "] " +
                    room.RoomType +
                    " markers=" + room.MarkerCount +
                    " facilities=" + room.Facilities.Count +
                    " valid=" + room.IsValid +
                    " usable=" + room.IsUsableForGameplay +
                    " summary=" + room.ValidationSummary);
            }
        }

        private static string BuildRoomId(string roomName, int floorIndex, int clusterIndex)
        {
            string normalizedName = NormalizeKey(roomName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "room";
            }

            return normalizedName + "_f" + Mathf.Max(1, floorIndex) + "_" + Mathf.Max(1, clusterIndex);
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(' ', '_').ToLowerInvariant();
        }

        private static CampusRoomType InferRoomType(string roomName)
        {
            string normalizedName = NormalizeKey(roomName);
            string rawName = string.IsNullOrWhiteSpace(roomName) ? string.Empty : roomName.Trim();
            if (normalizedName.Contains("class"))
            {
                return CampusRoomType.Classroom;
            }

            if (rawName.Contains("教室"))
            {
                return CampusRoomType.Classroom;
            }

            if (normalizedName.Contains("library"))
            {
                return CampusRoomType.Library;
            }

            if (rawName.Contains("图书"))
            {
                return CampusRoomType.Library;
            }

            if (normalizedName.Contains("canteen") || normalizedName.Contains("dining"))
            {
                return CampusRoomType.Canteen;
            }

            if (rawName.Contains("食堂") || rawName.Contains("餐厅"))
            {
                return CampusRoomType.Canteen;
            }

            if (normalizedName.Contains("shop") || normalizedName.Contains("store"))
            {
                return CampusRoomType.Store;
            }

            if (rawName.Contains("超市") || rawName.Contains("商店") || rawName.Contains("小卖部"))
            {
                return CampusRoomType.Store;
            }

            if (normalizedName.Contains("dorm"))
            {
                return CampusRoomType.Dormitory;
            }

            if (rawName.Contains("宿舍"))
            {
                return CampusRoomType.Dormitory;
            }

            if (normalizedName.Contains("restroom") || normalizedName.Contains("toilet") || normalizedName.Contains("bath"))
            {
                return CampusRoomType.Restroom;
            }

            if (rawName.Contains("厕所") || rawName.Contains("卫生间") || rawName.Contains("洗手间"))
            {
                return CampusRoomType.Restroom;
            }

            if (normalizedName.Contains("office") || normalizedName.Contains("teacher"))
            {
                return CampusRoomType.Office;
            }

            if (rawName.Contains("办公室") || rawName.Contains("教师"))
            {
                return CampusRoomType.Office;
            }

            if (normalizedName.Contains("humanresources") || normalizedName.Contains("hr") || rawName.Contains("人事"))
            {
                return CampusRoomType.HumanResources;
            }

            if (normalizedName.Contains("shrine") || rawName.Contains("神龛"))
            {
                return CampusRoomType.ShrineRoom;
            }

            if (normalizedName.Contains("activity") || normalizedName.Contains("common") || rawName.Contains("活动"))
            {
                return CampusRoomType.CommonActivityZone;
            }

            if (normalizedName.Contains("corridor") || normalizedName.Contains("hall"))
            {
                return CampusRoomType.Corridor;
            }

            if (rawName.Contains("走廊") || rawName.Contains("过道"))
            {
                return CampusRoomType.Corridor;
            }

            if (normalizedName.Contains("stair"))
            {
                return CampusRoomType.Stairwell;
            }

            if (rawName.Contains("楼梯"))
            {
                return CampusRoomType.Stairwell;
            }

            if (normalizedName.Contains("outdoor") || normalizedName.Contains("outside"))
            {
                return CampusRoomType.Outdoor;
            }

            if (rawName.Contains("室外") || rawName.Contains("操场") || rawName.Contains("校外"))
            {
                return CampusRoomType.Outdoor;
            }

            return CampusRoomType.Unknown;
        }

        private static CampusFacilityType InferFacilityType(CampusPlacedObject placedObject)
        {
            return CampusFacilityTypeResolver.Resolve(placedObject);
        }
    }
}
