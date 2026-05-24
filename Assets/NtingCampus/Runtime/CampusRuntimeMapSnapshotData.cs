using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Retail;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [Serializable]
    public sealed class CampusRuntimeMapSnapshot
    {
        public string Schema;
        public string MapName;
        public string ExportedAtLocal;
        public int SortingOrderStepPerFloor = 1000;
        public int SelectedFloorIndex = 1;
        public bool HasRoomDefinitions;
        public List<string> RoomNames = new List<string>();
        public List<int> RoomRequiredCounts = new List<int>();
        public List<CampusRuntimeFloorSnapshot> Floors = new List<CampusRuntimeFloorSnapshot>();
        public List<CampusRuntimeLightSnapshot> Lights = new List<CampusRuntimeLightSnapshot>();
    }

    [Serializable]
    public sealed class CampusRuntimeFloorSnapshot
    {
        public int FloorIndex = 1;
        public bool IsUnlocked = true;
        public List<CampusRuntimeTileSnapshot> FloorTiles = new List<CampusRuntimeTileSnapshot>();
        public List<CampusRuntimeTileSnapshot> WallTiles = new List<CampusRuntimeTileSnapshot>();
        public List<CampusRuntimeObjectSnapshot> Objects = new List<CampusRuntimeObjectSnapshot>();
        public List<CampusRuntimeStairSnapshot> Stairs = new List<CampusRuntimeStairSnapshot>();
        public List<CampusRuntimeRoomSnapshot> Rooms = new List<CampusRuntimeRoomSnapshot>();
    }

    [Serializable]
    public sealed class CampusRuntimeTileSnapshot
    {
        public Vector3Int Cell;
        public string AssetName;
        public int PaletteIndex = -1;
        public Matrix4x4 Transform = Matrix4x4.identity;
    }

    [Serializable]
    public sealed class CampusRuntimeWallStrokeUndoEntry
    {
        public int FloorIndex = 1;
        public List<CampusRuntimeWallStrokeCellUndoEntry> Cells = new List<CampusRuntimeWallStrokeCellUndoEntry>();
    }

    [Serializable]
    public sealed class CampusRuntimeWallStrokeCellUndoEntry
    {
        public Vector3Int Cell;
        public CampusRuntimeTileSnapshot Before;
        public CampusRuntimeTileSnapshot After;
    }

    [Serializable]
    public sealed class CampusRuntimeRetailShelfData
    {
        public bool Enabled;
        public CampusRetailShelfMode ShelfMode = CampusRetailShelfMode.Container;
        public string ItemDefinitionId;
        public int StockCount = 8;
        public int DisplaySlotCount = 4;
        public bool AutoRestock = true;
    }

    [Serializable]
    public sealed class CampusRuntimeProtectedStockContainerData
    {
        public bool Enabled;
        public string ContainerId;
        public string OwnerId;
        public string OwnerRole = "Campus";
        public bool AllowTakingContents = true;
        public int SuspicionRisk = 4;
        public bool AutoRestock = true;
        public List<CampusProtectedStockEntry> StockItems = new List<CampusProtectedStockEntry>();
    }

    [Serializable]
    public sealed class CampusRuntimeObjectSnapshot
    {
        public string ObjectId;
        public string TypeId;
        public string DisplayNameOverride;
        public int PaletteIndex = -1;
        public Vector3 Position;
        public Vector3Int Cell;
        public Vector2Int FootprintSize = Vector2Int.one;
        public int FloorIndex = 1;
        public bool OverrideFootprintSize;
        public Vector2 VisualScale = Vector2.one;
        public bool LockVisualScaleAspect = true;
        public bool IsWallMounted;
        public bool OverrideAllowRotation;
        public bool AllowRotation;
        public bool OverrideRotation0Sprite;
        public string Rotation0SpritePath;
        public bool OverrideRotation90Sprite;
        public string Rotation90SpritePath;
        public bool OverrideRotation180Sprite;
        public string Rotation180SpritePath;
        public bool OverrideRotation270Sprite;
        public string Rotation270SpritePath;
        public int Rotation90;
        public bool BlocksMovement;
        public bool BlocksSight;
        public bool IsInteractable;
        public bool IsStorageContainer;
        public Vector2Int StorageSize = new Vector2Int(4, 4);
        public float StorageMaxWeight = CampusPlacedObject.DefaultStorageMaxWeight;
        public bool UseCustomInteractionAnchor;
        public Vector3 CustomInteractionAnchorLocalPosition;
        public float CustomInteractionAnchorRadius = CampusPlacedObject.DefaultInteractionAnchorRadius;
        public string CustomInteractionPromptText;
        public List<CampusPlacedObjectInteractionAnchor> CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();
        public CampusRuntimeRetailShelfData RetailShelf = new CampusRuntimeRetailShelfData();
        public CampusRuntimeProtectedStockContainerData ProtectedStockContainer = new CampusRuntimeProtectedStockContainerData();
    }

    [Serializable]
    public sealed class CampusRuntimeObjectSettings
    {
        public string ObjectId;
        public string TypeId;
        public string DisplayNameOverride;
        public bool OverrideFootprintSize;
        public Vector2Int FootprintSize = Vector2Int.one;
        public Vector2 VisualScale = Vector2.one;
        public bool LockVisualScaleAspect = true;
        public bool IsWallMounted;
        public bool OverrideAllowRotation;
        public bool AllowRotation;
        public bool OverrideRotation0Sprite;
        public string Rotation0SpritePath;
        public bool OverrideRotation90Sprite;
        public string Rotation90SpritePath;
        public bool OverrideRotation180Sprite;
        public string Rotation180SpritePath;
        public bool OverrideRotation270Sprite;
        public string Rotation270SpritePath;
        public bool IsStorageContainer;
        public Vector2Int StorageSize = new Vector2Int(4, 4);
        public float StorageMaxWeight = CampusPlacedObject.DefaultStorageMaxWeight;
        public bool UseCustomInteractionAnchor;
        public Vector3 CustomInteractionAnchorLocalPosition;
        public float CustomInteractionAnchorRadius = CampusPlacedObject.DefaultInteractionAnchorRadius;
        public string CustomInteractionPromptText;
        public List<CampusPlacedObjectInteractionAnchor> CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();
        public CampusRuntimeRetailShelfData RetailShelf = new CampusRuntimeRetailShelfData();
        public CampusRuntimeProtectedStockContainerData ProtectedStockContainer = new CampusRuntimeProtectedStockContainerData();
    }

    [Serializable]
    public sealed class CampusRuntimePlayerMapSaveManifest
    {
        public string SavedAtLocal;
        public string UnityPersistentDataPath;
        public string ImportRootFolderName;
        public string MapFileName;
    }

    [Serializable]
    public sealed class CampusRuntimeAuthoringPackageManifest
    {
        public string ExportedAtLocal;
        public string UnityPersistentDataPath;
        public string ImportRootFolderName;
        public string MapFileName;
    }

    [Serializable]
    public sealed class CampusRuntimeStairSnapshot
    {
        public int FromFloor = 1;
        public int ToFloor = 2;
        public Vector3Int FromCell;
        public Vector3Int ToCell;
        public Vector3Int SecondaryCell;
        public int Rotation90;
        public string LinkId;
        public bool IsAutoReturnStair;
    }

    [Serializable]
    public sealed class CampusRuntimeRoomSnapshot
    {
        public string RoomName;
        public int FloorIndex = 1;
        public Vector3Int Cell;
        public bool HideMarkerVisual;
    }

    [Serializable]
    public sealed class CampusRuntimeLightSnapshot
    {
        public string Name;
        public string LightType;
        public Vector3 Position;
        public Vector3 Rotation;
        public Color Color = Color.white;
        public float Intensity = 1f;
        public float InnerRadius = 1.5f;
        public float OuterRadius = 4f;
        public float InnerAngle = 360f;
        public float OuterAngle = 360f;
        public float FalloffIntensity = 0.18f;
        public bool ShadowsEnabled = true;
        public float ShadowIntensity = 0.75f;
        public float ShadowSoftness = 0.3f;
        public float ShadowSoftnessFalloff = 0.5f;
        public int FloorIndex;
        public Vector3Int Cell;
    }
}
