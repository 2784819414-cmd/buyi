using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeAreaRegionCounter
    {
        private static readonly Vector3Int[] CardinalDirections =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        internal static void CountRegionsByRoomName(
            IReadOnlyList<CampusRuntimeRoomMarker> markers,
            Dictionary<string, int> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            if (markers == null || markers.Count == 0)
            {
                return;
            }

            Dictionary<AreaRegionKey, HashSet<Vector3Int>> cellsByRegion =
                new Dictionary<AreaRegionKey, HashSet<Vector3Int>>();

            for (int i = 0; i < markers.Count; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null ||
                    !CampusRuntimeAreaPresetCatalog.TryResolveRoomName(marker.RoomName, out string roomName))
                {
                    continue;
                }

                AreaRegionKey key = new AreaRegionKey(marker.FloorIndex, roomName);
                if (!cellsByRegion.TryGetValue(key, out HashSet<Vector3Int> cells))
                {
                    cells = new HashSet<Vector3Int>();
                    cellsByRegion[key] = cells;
                }

                Vector3Int cell = marker.Cell;
                cell.z = 0;
                cells.Add(cell);
            }

            foreach (KeyValuePair<AreaRegionKey, HashSet<Vector3Int>> pair in cellsByRegion)
            {
                int regionCount = CountConnectedRegions(pair.Value);
                output.TryGetValue(pair.Key.RoomName, out int existingCount);
                output[pair.Key.RoomName] = existingCount + regionCount;
            }
        }

        private static int CountConnectedRegions(HashSet<Vector3Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return 0;
            }

            int count = 0;
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();

            foreach (Vector3Int start in cells)
            {
                if (!visited.Add(start))
                {
                    continue;
                }

                count++;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    Vector3Int current = queue.Dequeue();
                    for (int i = 0; i < CardinalDirections.Length; i++)
                    {
                        Vector3Int neighbor = current + CardinalDirections[i];
                        if (cells.Contains(neighbor) && visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return count;
        }

        private readonly struct AreaRegionKey : IEquatable<AreaRegionKey>
        {
            internal AreaRegionKey(int floorIndex, string roomName)
            {
                FloorIndex = floorIndex;
                RoomName = roomName ?? string.Empty;
            }

            internal int FloorIndex { get; }
            internal string RoomName { get; }

            public bool Equals(AreaRegionKey other)
            {
                return FloorIndex == other.FloorIndex &&
                       string.Equals(RoomName, other.RoomName, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is AreaRegionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                int hash = FloorIndex;
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(RoomName);
                return hash;
            }
        }
    }
}
