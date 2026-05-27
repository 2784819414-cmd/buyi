using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    public sealed class CampusAiMapAuthoringDocument
    {
        public string MapId;
        public List<CampusAiMapAuthoringFloor> Floors = new List<CampusAiMapAuthoringFloor>();
    }

    [Serializable]
    public sealed class CampusAiMapAuthoringFloor
    {
        public int FloorIndex = 1;
        public bool IsUnlocked = true;
        public List<CampusAiMapAuthoringRoom> Rooms = new List<CampusAiMapAuthoringRoom>();
        public List<CampusAiMapAuthoringTileStamp> FloorTiles = new List<CampusAiMapAuthoringTileStamp>();
        public List<CampusAiMapAuthoringTileStamp> WallTiles = new List<CampusAiMapAuthoringTileStamp>();
        public List<CampusAiMapAuthoringObject> Objects = new List<CampusAiMapAuthoringObject>();
        public List<CampusAiMapAuthoringStair> Stairs = new List<CampusAiMapAuthoringStair>();
    }

    [Serializable]
    public sealed class CampusAiMapAuthoringRoom
    {
        public string Id;
        public CampusAiMapAuthoringRect Rect;
        public string FloorTileId;
        public string WallTileId;
    }

    [Serializable]
    public sealed class CampusAiMapAuthoringTileStamp
    {
        public string TileId;
        public CampusAiMapAuthoringCell Cell;
        public int Size = 1;
        public int Rotation90;
        public bool FlipX;
        public bool FlipY;
    }

    [Serializable]
    public sealed class CampusAiMapAuthoringObject
    {
        public string ObjectId;
        public string TypeId;
        public CampusAiMapAuthoringCell Cell;
        public int Rotation90;
        public Vector2Int FootprintSize = Vector2Int.one;
        public bool OverrideFootprintSize;
        public Vector2 VisualScale = Vector2.one;
        public bool LockVisualScaleAspect = true;
    }

    [Serializable]
    public sealed class CampusAiMapAuthoringStair
    {
        public CampusAiMapAuthoringCell FromCell;
        public int ToFloor;
        public int Rotation90;
        public string LinkId;
    }

    [Serializable]
    public struct CampusAiMapAuthoringCell
    {
        public int X;
        public int Y;

        public Vector3Int ToVector3Int()
        {
            return new Vector3Int(X, Y, 0);
        }
    }

    [Serializable]
    public struct CampusAiMapAuthoringRect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }
}
