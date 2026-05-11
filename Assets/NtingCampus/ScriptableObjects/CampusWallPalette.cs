using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [CreateAssetMenu(menuName = "Nting Campus/Wall Palette", fileName = "CampusWallPalette")]
    public sealed class CampusWallPalette : ScriptableObject
    {
        public TileBase HorizontalWall;
        public TileBase VerticalWall;
        public TileBase CornerWall;
        public TileBase HighWall;
        public List<TileBase> WallTiles = new List<TileBase>();

        public void RemoveInvalidEntries()
        {
            if (WallTiles == null)
            {
                WallTiles = new List<TileBase>();
                return;
            }

            WallTiles.RemoveAll(tile => !CampusTilePalette.IsUsableTile(tile));
        }

        public TileBase GetTileOrNull(int index)
        {
            RemoveInvalidEntries();
            if (WallTiles != null && index >= 0 && index < WallTiles.Count && WallTiles[index] != null)
            {
                return WallTiles[index];
            }

            if (HorizontalWall != null)
            {
                return HorizontalWall;
            }

            if (HighWall != null)
            {
                return HighWall;
            }

            if (VerticalWall != null)
            {
                return VerticalWall;
            }

            return CornerWall;
        }
    }
}
