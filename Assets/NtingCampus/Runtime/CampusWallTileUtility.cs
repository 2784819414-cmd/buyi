using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    public enum CampusWallDebugView
    {
        ShowFinalWallVisuals,
        ShowWallLogicOnly,
        ShowBoth
    }

    /// <summary>
    /// Shared wall-grid helpers used by runtime preview and editor rendering.
    /// </summary>
    public static class CampusWallTileUtility
    {
        public const int NorthMask = 1;
        public const int EastMask = 2;
        public const int SouthMask = 4;
        public const int WestMask = 8;

        public static readonly Vector3Int North = new Vector3Int(0, 1, 0);
        public static readonly Vector3Int East = new Vector3Int(1, 0, 0);
        public static readonly Vector3Int South = new Vector3Int(0, -1, 0);
        public static readonly Vector3Int West = new Vector3Int(-1, 0, 0);

        public static bool HasWall(Tilemap wallLogic, Vector3Int cell)
        {
            return wallLogic != null && wallLogic.HasTile(cell);
        }

        public static int GetExposedMask(Tilemap wallLogic, Vector3Int cell)
        {
            return GetExposedMaskFromConnectionMask(GetConnectionMask(wallLogic, cell));
        }

        public static int GetConnectionMask(Tilemap wallLogic, Vector3Int cell)
        {
            int mask = 0;
            if (HasWall(wallLogic, cell + North))
            {
                mask |= NorthMask;
            }

            if (HasWall(wallLogic, cell + East))
            {
                mask |= EastMask;
            }

            if (HasWall(wallLogic, cell + South))
            {
                mask |= SouthMask;
            }

            if (HasWall(wallLogic, cell + West))
            {
                mask |= WestMask;
            }

            return mask;
        }

        public static int GetExposedMaskFromConnectionMask(int connectionMask)
        {
            return (~connectionMask) & (NorthMask | EastMask | SouthMask | WestMask);
        }

        public static bool IsHorizontalConnection(int connectionMask)
        {
            bool horizontal = (connectionMask & (EastMask | WestMask)) != 0;
            bool vertical = (connectionMask & (NorthMask | SouthMask)) != 0;
            return horizontal && !vertical;
        }

        public static bool IsVerticalConnection(int connectionMask)
        {
            bool horizontal = (connectionMask & (EastMask | WestMask)) != 0;
            bool vertical = (connectionMask & (NorthMask | SouthMask)) != 0;
            return vertical && !horizontal;
        }

        public static bool HasConcaveCorner(Tilemap wallLogic, Vector3Int cell, Vector3Int first, Vector3Int second)
        {
            return HasWall(wallLogic, cell + first) &&
                   HasWall(wallLogic, cell + second) &&
                   !HasWall(wallLogic, cell + first + second);
        }

        public static Tilemap GetWallLogicTilemap(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return null;
            }

            return floor.WallLogicTilemap != null ? floor.WallLogicTilemap : floor.WallTilemap;
        }

        public static void SetTilemapVisible(Tilemap tilemap, bool visible)
        {
            if (tilemap == null)
            {
                return;
            }

            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }
}
