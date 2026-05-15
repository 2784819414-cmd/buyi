using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NtingCampusMapEditor
{
    [CreateAssetMenu(menuName = "Nting Campus/Tile Palette", fileName = "CampusTilePalette")]
    public sealed class CampusTilePalette : ScriptableObject
    {
        public List<TileBase> FloorTiles = new List<TileBase>();

        public void RemoveInvalidEntries()
        {
            if (FloorTiles == null)
            {
                FloorTiles = new List<TileBase>();
                return;
            }

            FloorTiles.RemoveAll(tile => !IsUsableTile(tile));
        }

        public TileBase GetTileOrNull(int index)
        {
            RemoveInvalidEntries();
            if (FloorTiles == null || index < 0 || index >= FloorTiles.Count)
            {
                return null;
            }

            return FloorTiles[index];
        }

        public static bool IsUsableTile(TileBase tile)
        {
            if (tile == null)
            {
                return false;
            }

            Tile spriteTile = tile as Tile;
            return spriteTile == null || spriteTile.sprite != null;
        }
    }
}
