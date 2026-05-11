using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Serializable map snapshot. GUID fields are authoritative; names remain for older saved data and readable diffs.
    /// </summary>
    [CreateAssetMenu(menuName = "Nting Campus/Map Data", fileName = "CampusMapData")]
    public sealed class CampusMapData : ScriptableObject
    {
        public string MapId = "CampusMap";
        public List<CampusFloorData> Floors = new List<CampusFloorData>();
    }

    [Serializable]
    public sealed class CampusFloorData
    {
        public int FloorIndex;
        public bool IsUnlocked;
        public List<CampusTileCellData> FloorTiles = new List<CampusTileCellData>();
        public List<CampusTileCellData> WallTiles = new List<CampusTileCellData>();
        public List<CampusPlacedObjectData> Objects = new List<CampusPlacedObjectData>();
        public List<CampusStairData> Stairs = new List<CampusStairData>();
    }

    [Serializable]
    public sealed class CampusTileCellData
    {
        public Vector3Int Cell;
        public string TileId;
        public string TileGuid;
        public int Size = 1;
        public int Rotation90;
        public bool FlipX;
        public bool FlipY;
        public Matrix4x4 Transform = Matrix4x4.identity;
    }

    [Serializable]
    public sealed class CampusPlacedObjectData
    {
        public string ObjectId;
        public string ObjectGuid;
        public Vector3 Position;
        public Vector3Int Cell;
        public Vector2Int FootprintSize = Vector2Int.one;
        public int FloorIndex;
        public int Rotation90;
        public bool BlocksMovement;
        public bool BlocksSight;
        public bool IsInteractable;
    }

    [Serializable]
    public sealed class CampusStairData
    {
        public int FromFloor;
        public int ToFloor;
        public Vector3Int FromCell;
        public Vector3Int ToCell;
        public Vector3Int SecondaryCell;
        public int Rotation90;
        public string LinkId;
        public bool IsAutoReturnStair;
    }
}
