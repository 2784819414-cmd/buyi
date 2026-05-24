using System;
using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    internal static class CampusGameplayRoomCollector
    {
        public static List<CampusGameplayRoom> CollectRooms(
            CampusMapRoot mapRoot,
            CampusRuntimeGameplayOverlayLoader overlayLoader)
        {
            List<CampusGameplayRoom> rooms = new List<CampusGameplayRoom>();
            if (mapRoot == null)
            {
                return rooms;
            }

            Dictionary<int, List<CampusRuntimeRoomMarker>> markersByFloor = CollectLegacyMarkersByFloor(mapRoot);
            foreach (KeyValuePair<int, List<CampusRuntimeRoomMarker>> pair in markersByFloor)
            {
                BuildRoomsForFloor(pair.Key, pair.Value, rooms);
            }

            BuildRoomsFromGameplayMarkers(overlayLoader, rooms);
            return rooms;
        }

        public static string BuildRoomId(string roomName, int floorIndex, int clusterIndex)
        {
            string normalizedName = NormalizeKey(roomName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "room";
            }

            return normalizedName + "_f" + Mathf.Max(1, floorIndex) + "_" + Mathf.Max(1, clusterIndex);
        }

        public static string NormalizeRoomKey(string value)
        {
            return NormalizeKey(value);
        }

        private static Dictionary<int, List<CampusRuntimeRoomMarker>> CollectLegacyMarkersByFloor(CampusMapRoot root)
        {
            Dictionary<int, List<CampusRuntimeRoomMarker>> markersByFloor =
                new Dictionary<int, List<CampusRuntimeRoomMarker>>();
            if (root.Floors == null)
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

                CampusRuntimeRoomMarker[] markers =
                    floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
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

        private static void BuildRoomsForFloor(
            int floorIndex,
            List<CampusRuntimeRoomMarker> floorMarkers,
            List<CampusGameplayRoom> rooms)
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
                CreateClusteredRooms(floorIndex, pair.Key, pair.Value, rooms);
            }
        }

        private static void CreateClusteredRooms(
            int floorIndex,
            string roomName,
            List<CampusRuntimeRoomMarker> sourceMarkers,
            List<CampusGameplayRoom> rooms)
        {
            Dictionary<Vector3Int, CampusRuntimeRoomMarker> markerByCell =
                new Dictionary<Vector3Int, CampusRuntimeRoomMarker>();
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
                    CampusRoomTypeInference.Infer(roomName),
                    floorIndex,
                    bounds,
                    center,
                    clusterMarkers);

                rooms.Add(room);

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

        private static void BuildRoomsFromGameplayMarkers(
            CampusRuntimeGameplayOverlayLoader overlayLoader,
            List<CampusGameplayRoom> rooms)
        {
            CampusGameplayRoomMarker[] gameplayMarkers =
                UnityEngine.Object.FindObjectsByType<CampusGameplayRoomMarker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < gameplayMarkers.Length; i++)
            {
                CampusGameplayRoomMarker gameplayMarker = gameplayMarkers[i];
                if (gameplayMarker == null ||
                    (overlayLoader != null && !overlayLoader.ShouldIncludeExplicitMarker(gameplayMarker)))
                {
                    continue;
                }

                BoundsInt bounds = gameplayMarker.BuildBounds();
                if (HasExistingRoomOverlap(rooms, gameplayMarker.FloorIndex, bounds))
                {
                    continue;
                }

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
            }
        }

        private static bool HasExistingRoomOverlap(
            IReadOnlyList<CampusGameplayRoom> rooms,
            int floorIndex,
            BoundsInt bounds)
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null || room.FloorIndex != floorIndex)
                {
                    continue;
                }

                if (BoundsOverlap2D(room.MarkerBounds, bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BoundsOverlap2D(BoundsInt a, BoundsInt b)
        {
            return a.xMin < b.xMax &&
                   a.xMax > b.xMin &&
                   a.yMin < b.yMax &&
                   a.yMax > b.yMin;
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

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(' ', '_').ToLowerInvariant();
        }
    }
}

