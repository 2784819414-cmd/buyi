using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    internal sealed class CampusRuntimeRoomPrefabPlacementContext
    {
        internal Action<CampusFloorRoot, Vector3Int, Vector2Int> EraseArea;
        internal Action<Tilemap, List<CampusRuntimeTileSnapshot>, List<TileBase>, Vector3Int> ApplyTiles;
        internal Action<CampusFloorRoot, List<CampusRuntimeObjectSnapshot>, Vector3Int> ApplyObjects;
        internal Action<CampusFloorRoot, List<CampusRuntimeRoomSnapshot>, Vector3Int, string> ApplyMarkers;
        internal Action<CampusFloorRoot, CampusRuntimeRoomPrefab, Vector3Int> ApplyGameplayMarkers;
        internal Action<CampusFloorRoot, List<CampusRuntimeRoomLightSnapshot>, Vector3Int> ApplyLights;
        internal Action<CampusRuntimeRoomPrefab> AddRoomDefinitions;
        internal Action<CampusFloorRoot> FinishPlacement;
    }

    internal static class CampusRuntimeRoomPrefabAuthoring
    {
        internal static void Place(
            CampusRuntimeRoomPrefab roomPrefab,
            CampusFloorRoot floor,
            Vector3Int anchorCell,
            List<TileBase> floorTiles,
            List<TileBase> wallTiles,
            CampusRuntimeRoomPrefabPlacementContext context)
        {
            if (roomPrefab == null || floor == null || context == null)
            {
                return;
            }

            context.EraseArea?.Invoke(floor, anchorCell, roomPrefab.Size);
            context.ApplyTiles?.Invoke(floor.FloorTilemap, roomPrefab.FloorTiles, floorTiles, anchorCell);
            context.ApplyTiles?.Invoke(
                CampusWallTileUtility.GetWallLogicTilemap(floor),
                roomPrefab.WallTiles,
                wallTiles,
                anchorCell);
            context.ApplyObjects?.Invoke(floor, roomPrefab.Objects, anchorCell);
            context.ApplyMarkers?.Invoke(floor, roomPrefab.RoomMarkers, anchorCell, roomPrefab.RoomName);
            context.ApplyGameplayMarkers?.Invoke(floor, roomPrefab, anchorCell);
            context.ApplyLights?.Invoke(floor, roomPrefab.Lights, anchorCell);
            context.AddRoomDefinitions?.Invoke(roomPrefab);
            context.FinishPlacement?.Invoke(floor);
        }

        internal static BoundsInt BuildInclusiveCellBounds(Vector3Int start, Vector3Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            return new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }

        internal static bool CellInBounds(BoundsInt bounds, Vector3Int cell)
        {
            return cell.x >= bounds.xMin &&
                   cell.x < bounds.xMax &&
                   cell.y >= bounds.yMin &&
                   cell.y < bounds.yMax;
        }

        internal static Vector3Int ToRelativeCell(Vector3Int cell, Vector3Int originCell)
        {
            return new Vector3Int(cell.x - originCell.x, cell.y - originCell.y, 0);
        }

        internal static Vector3Int ToAbsoluteCell(Vector3Int anchorCell, Vector3Int relativeCell)
        {
            return new Vector3Int(anchorCell.x + relativeCell.x, anchorCell.y + relativeCell.y, 0);
        }
    }
}
