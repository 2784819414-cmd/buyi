using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Pranks;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace NtingCampusMapEditor
{
    public enum CampusRuntimeEditorTab
    {
        Build,
        Rooms,
        Gameplay,
        Objects,
        Lighting
    }

    public enum CampusRuntimeBrushMode
    {
        Pan,
        PaintFloor,
        PaintWall,
        RectangleFloor,
        RectangleWall,
        PlaceObject,
        PlaceStair,
        PlaceRoom,
        RectangleRoom,
        PlaceGameplayMarker,
        EraseGameplayMarker,
        CreateRoomPrefab,
        PlaceRoomPrefab,
        PlaceLight,
        Erase,
        RectangleErase,
        Pick
    }

    public enum CampusRuntimeImportTarget
    {
        Floor,
        Wall,
        Object,
        Room,
        WallFace,
        WallCap
    }

    public enum CampusRuntimeMapLoadSource
    {
        Scene,
        PlayerSave,
        AuthoringPackage
    }

    internal static class CampusWallBuildProfiler
    {
        internal static readonly ProfilerMarker PaintWall = new ProfilerMarker("CampusRuntimeMapEditor.PaintWall");
        internal static readonly ProfilerMarker PaintWallWriteTiles = new ProfilerMarker("CampusRuntimeMapEditor.PaintWall.WriteTiles");
        internal static readonly ProfilerMarker RebuildWallVisuals = new ProfilerMarker("CampusRuntimeMapEditor.RebuildWallVisuals");
        internal static readonly ProfilerMarker RebuildWallVisualsFull = new ProfilerMarker("CampusRuntimeMapEditor.RebuildWallVisuals.Full");
        internal static readonly ProfilerMarker RebuildWallVisualsChanged = new ProfilerMarker("CampusRuntimeMapEditor.RebuildWallVisuals.ChangedCells");
        internal static readonly ProfilerMarker EnsureWallCollision = new ProfilerMarker("CampusRuntimeMapEditor.EnsureWallCollision");
        internal static readonly ProfilerMarker RecordUndo = new ProfilerMarker("CampusRuntimeMapEditor.RecordUndo");
        internal static readonly ProfilerMarker BuildSnapshot = new ProfilerMarker("CampusRuntimeMapEditor.BuildSnapshot");
        internal static readonly ProfilerMarker BuildSnapshotJson = new ProfilerMarker("CampusRuntimeMapEditor.BuildSnapshotJson");
        internal static readonly ProfilerMarker BeginWallStrokeUndo = new ProfilerMarker("CampusRuntimeMapEditor.BeginWallStrokeUndo");
        internal static readonly ProfilerMarker FinalizeWallStrokeUndo = new ProfilerMarker("CampusRuntimeMapEditor.FinalizeWallStrokeUndo");
    }

    /// <summary>
    /// Runtime-only room marker used by the packaged internal map editor and export pipeline.
    /// </summary>
    public sealed class CampusRuntimeRoomMarker : MonoBehaviour
    {
        public string RoomName = "Unnamed Room";
        public int FloorIndex = 1;
        public Vector3Int Cell;
        public bool HideMarkerVisual;
    }

    /// <summary>
    /// Packaged internal map editor. It intentionally avoids UnityEditor APIs so the same tools work in builds.
    /// </summary>
    public sealed class CampusRuntimeMapEditor : MonoBehaviour
    {
        public static CampusRuntimeMapEditor Instance { get; private set; }

        private sealed class CampusRuntimeGameplayMarkerPreset
        {
            public readonly string ChineseLabel;
            public readonly string EnglishLabel;
            public readonly string ChineseDisplayName;
            public readonly string EnglishDisplayName;
            public readonly CampusRoomType RoomType;
            public readonly CampusFacilityType FacilityType;
            public readonly string PrankPayload;
            public readonly CampusPrankSpotVisualKind VisualKind;
            public readonly Color Color;

            private CampusRuntimeGameplayMarkerPreset(
                string chineseLabel,
                string englishLabel,
                string chineseDisplayName,
                string englishDisplayName,
                CampusRoomType roomType,
                CampusFacilityType facilityType,
                string prankPayload,
                CampusPrankSpotVisualKind visualKind,
                Color color)
            {
                ChineseLabel = chineseLabel;
                EnglishLabel = englishLabel;
                ChineseDisplayName = chineseDisplayName;
                EnglishDisplayName = englishDisplayName;
                RoomType = roomType;
                FacilityType = facilityType;
                PrankPayload = prankPayload;
                VisualKind = visualKind;
                Color = color;
            }

            public bool UsesInteractionSpot => !string.IsNullOrWhiteSpace(PrankPayload);

            public static CampusRuntimeGameplayMarkerPreset FacilityPoint(
                string chineseLabel,
                string englishLabel,
                CampusFacilityType facilityType,
                Color color)
            {
                return new CampusRuntimeGameplayMarkerPreset(
                    chineseLabel,
                    englishLabel,
                    chineseLabel,
                    englishLabel,
                    CampusRoomType.Unknown,
                    facilityType,
                    string.Empty,
                    CampusPrankSpotVisualKind.Envelope,
                    color);
            }

            public static CampusRuntimeGameplayMarkerPreset InteractionFacilityPoint(
                string chineseLabel,
                string englishLabel,
                string payload,
                CampusRoomType requiredRoomType,
                CampusPrankSpotVisualKind visualKind,
                Color color)
            {
                return new CampusRuntimeGameplayMarkerPreset(
                    chineseLabel,
                    englishLabel,
                    chineseLabel,
                    englishLabel,
                    requiredRoomType,
                    CampusFacilityType.Unknown,
                    payload,
                    visualKind,
                    color);
            }
        }

        private sealed class CampusRuntimeAreaPreset
        {
            public readonly string RoomName;
            public readonly string ChineseLabel;
            public readonly string EnglishLabel;
            public readonly int RequiredCount;

            public CampusRuntimeAreaPreset(
                string roomName,
                string chineseLabel,
                string englishLabel,
                int requiredCount)
            {
                RoomName = roomName;
                ChineseLabel = chineseLabel;
                EnglishLabel = englishLabel;
                RequiredCount = Mathf.Max(0, requiredCount);
            }
        }

        private const string RuntimeResourceFolder = "NtingCampusRuntime";
        private const string RuntimeImportFolder = "CampusRuntimeImports";
        private const string FloorImportFolder = "Floors";
        private const string WallImportFolder = "Walls";
        private const string ObjectImportFolder = "Objects";
        private const string ObjectSettingsFolder = "ObjectSettings";
        private const string RoomImportFile = "Rooms.txt";
        private const string RoomPrefabFolder = "RoomPrefabs";
        private const string PlayerSaveFolder = "CampusPlayerMapSave";
        private const string PlayerSaveMapFile = "CampusMap_PlayerSave.json";
        private const string PlayerSaveManifestFile = "save_manifest.json";
        private const string AuthoringPackageFolder = "UserGeneratedRuntimeContent";
        private const string AuthoringPackageMapFile = "CampusMap_AuthoringPackage.json";
        private const string AuthoringPackageManifestFile = "authoring_manifest.json";
        private const string GameplayOverlaySchema = "NtingCampusGameplayOverlay.v1";
        private const string GameplayOverlayExtension = ".gameplay.json";
        private const string WallStrokeUndoPrefix = "WALLSTROKE:";
        private const int MaxUndoSnapshots = 64;
        private const float SceneReferenceRetryInterval = 0.5f;
        private const float AmbientLightIntensity = 0.3f;
        private const float PlacedLightIntensity = 1.15f;
        private const int PaletteTileSize = 92;
        private const int ToolbarButtonWidth = 110;
        private const float ZoomStep = 0.12f;
        private const float PanelMargin = 28f;
        private const float TopMargin = 72f;
        private const float BottomToolbarHeight = 74f;
        private const float ObjectSettingsMinScale = 0.05f;
        private const float ObjectSettingsMaxScale = 8f;
        private const byte RuntimeObjectSpriteAlphaThreshold = 8;
        private const string TextInputControlPrefix = "CampusRuntimeTextInput_";
        private const int WallStrokeVisualBatchCellThreshold = 8;
        private const int WallStrokeVisualBatchChunkThreshold = 4;

        private static readonly CampusRuntimeAreaPreset[] DefaultAreaPresets =
        {
            new CampusRuntimeAreaPreset("Classroom", "\u6559\u5ba4", "Classroom", 2),
            new CampusRuntimeAreaPreset("Corridor", "\u8d70\u5eca", "Corridor", 1),
            new CampusRuntimeAreaPreset("Office", "\u529e\u516c\u5ba4", "Office", 1),
            new CampusRuntimeAreaPreset("CommonActivityZone", "\u516c\u5171\u6d3b\u52a8\u533a", "Common Activity Zone", 1),
            new CampusRuntimeAreaPreset("Canteen", "\u98df\u5802", "Canteen", 1),
            new CampusRuntimeAreaPreset("Store", "\u8d85\u5e02", "Store", 1),
            new CampusRuntimeAreaPreset("Outdoor", "\u5ba4\u5916", "Outdoor", 1),
            new CampusRuntimeAreaPreset("Dormitory", "\u5bbf\u820d", "Dormitory", 0),
            new CampusRuntimeAreaPreset("Restroom", "\u536b\u751f\u95f4", "Restroom", 0),
            new CampusRuntimeAreaPreset("Library", "\u56fe\u4e66\u9986", "Library", 0),
            new CampusRuntimeAreaPreset("Stairwell", "\u697c\u68af\u95f4", "Stairwell", 0),
            new CampusRuntimeAreaPreset("HumanResources", "\u4eba\u4e8b\u5904", "Human Resources", 0),
            new CampusRuntimeAreaPreset("ShrineRoom", "\u795e\u9f9b\u5ba4", "Shrine Room", 0)
        };

        private static readonly CampusRuntimeGameplayMarkerPreset[] GameplayMarkerPresets =
        {
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u95e8",
                "Door",
                CampusFacilityType.Door,
                new Color(0.72f, 0.72f, 0.72f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u9ed1\u677f",
                "Blackboard",
                CampusFacilityType.Blackboard,
                new Color(0.12f, 0.46f, 0.36f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8bfe\u684c",
                "Student Desk",
                CampusFacilityType.StudentDesk,
                new Color(0.26f, 0.56f, 0.96f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8bb2\u53f0",
                "Podium",
                CampusFacilityType.Podium,
                new Color(0.18f, 0.38f, 0.86f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u529e\u516c\u684c",
                "Office Desk",
                CampusFacilityType.OfficeDesk,
                new Color(0.72f, 0.48f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5e8a",
                "Bed",
                CampusFacilityType.Bed,
                new Color(0.56f, 0.42f, 0.88f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u516c\u544a\u680f",
                "Bulletin Board",
                CampusFacilityType.BulletinBoard,
                new Color(0.88f, 0.62f, 0.24f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u62db\u52df\u70b9",
                "Recruitment",
                CampusFacilityType.Recruitment,
                new Color(0.74f, 0.36f, 0.88f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u6d17\u624b\u6c60",
                "Sink",
                CampusFacilityType.Sink,
                new Color(0.22f, 0.68f, 0.92f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u50a8\u7269\u70b9",
                "Storage",
                CampusFacilityType.Storage,
                new Color(0.62f, 0.56f, 0.46f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u98df\u5802\u67dc\u53f0",
                "Canteen Counter",
                CampusFacilityType.CanteenCounter,
                new Color(0.18f, 0.68f, 0.72f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u98df\u5802\u6392\u961f\u70b9",
                "Canteen Queue",
                CampusFacilityType.CanteenQueuePoint,
                new Color(0.95f, 0.76f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u6253\u996d\u6258\u76d8",
                "Food Tray",
                CampusFacilityType.CanteenFoodTray,
                new Color(0.96f, 0.64f, 0.24f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5403\u996d\u533a\u5ea7\u4f4d",
                "Dining Table",
                CampusFacilityType.DiningTable,
                new Color(0.55f, 0.76f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5916\u5356\u653e\u7f6e\u70b9",
                "Delivery Drop",
                CampusFacilityType.DeliveryDropPoint,
                new Color(0.32f, 0.54f, 0.98f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8d85\u5e02\u8d27\u67b6",
                "Store Shelf",
                CampusFacilityType.StoreShelf,
                new Color(0.88f, 0.36f, 0.72f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8d85\u5e02\u6392\u961f\u70b9",
                "Store Queue",
                CampusFacilityType.StoreQueuePoint,
                new Color(0.94f, 0.48f, 0.82f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8d85\u5e02\u6536\u94f6\u53f0",
                "Store Checkout",
                CampusFacilityType.StoreCheckout,
                new Color(0.76f, 0.24f, 0.64f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u70b8\u9e21",
                "Steal Chicken",
                CampusPrankPayloadIds.StealFriedChicken,
                CampusRoomType.Canteen,
                CampusPrankSpotVisualKind.Snack,
                new Color(0.95f, 0.52f, 0.22f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u6c49\u5821",
                "Steal Burger",
                CampusPrankPayloadIds.StealBurger,
                CampusRoomType.Canteen,
                CampusPrankSpotVisualKind.Snack,
                new Color(0.88f, 0.64f, 0.24f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u5173\u4e1c\u716e",
                "Steal Oden",
                CampusPrankPayloadIds.StealOden,
                CampusRoomType.Canteen,
                CampusPrankSpotVisualKind.Snack,
                new Color(0.78f, 0.48f, 0.9f, 1f)),
            CampusRuntimeGameplayMarkerPreset.InteractionFacilityPoint(
                "\u5077\u5916\u5356",
                "Steal Delivery",
                CampusPrankPayloadIds.StealDelivery,
                CampusRoomType.Outdoor,
                CampusPrankSpotVisualKind.DeliveryBox,
                new Color(0.28f, 0.66f, 0.98f, 1f))
        };

        [SerializeField] private bool openOnStart = false;
        [SerializeField] private bool showGridOverlay = true;
        [SerializeField] private bool showHelpOverlay;
        [SerializeField] private bool showSettings;
        [SerializeField] private bool showObjectSettings;
        [SerializeField] private bool autoSavePlayerMap = true;
        [SerializeField] private bool autoLoadPlayerMapOnStart = true;
        [SerializeField] private float autoSavePlayerMapDelay = 1.5f;
        [SerializeField] private int selectedFloorIndex = 1;
        [SerializeField] private int selectedFloorTileIndex;
        [SerializeField] private int selectedWallTileIndex;
        [SerializeField] private int selectedObjectIndex;
        [SerializeField] private int selectedRoomIndex;
        [SerializeField] private int selectedRoomPrefabIndex;
        [SerializeField] private int selectedGameplayPresetIndex;
        [SerializeField] private int selectedWallProfileIndex;
        [SerializeField] private int brushSize = 1;
        [SerializeField] private int rotation90;
        [SerializeField] private int stairTargetFloorIndex = 2;
        [SerializeField] private int newRoomRequiredCount = 1;
        [SerializeField] private int selectedObjectFootprintX = 1;
        [SerializeField] private int selectedObjectFootprintY = 1;
        [SerializeField] private float minCameraSize = 3f;
        [SerializeField] private float maxCameraSize = 80f;
        [SerializeField] private float lightOuterRadius = 4f;
        [SerializeField] private float lightInnerRadius = 1.5f;
        [SerializeField] private float lightIntensity = PlacedLightIntensity;
        [SerializeField] private Color lightColor = new Color(1f, 0.96f, 0.88f, 1f);
        [SerializeField] private bool lightShadowsEnabled = true;
        [SerializeField] private float lightShadowIntensity = 0.45f;
        [SerializeField] private float lightShadowSoftness = 0.75f;
        [SerializeField] private float lightShadowSoftnessFalloff = 0.85f;
        [SerializeField] private CampusRuntimeEditorTab activeTab = CampusRuntimeEditorTab.Build;
        [SerializeField] private CampusRuntimeBrushMode brushMode = CampusRuntimeBrushMode.PaintFloor;
        [SerializeField] private Light2D.LightType lightBrushType = Light2D.LightType.Point;

        private CampusMapRoot mapRoot;
        private CampusWallVisualCatalog wallVisualCatalog;
        private CampusWallRenderProfile fallbackWallProfile;
        private GameObject stairPrefab;
        private Camera sceneCamera;
        private bool isOpen;
        private bool isReady;
        private bool sceneReferencesDirty = true;
        private bool runtimeSessionInitialized;
        private bool strokeActive;
        private bool strokeUndoRecorded;
        private bool wallStrokeVisualPreviewInitialized;
        private bool rectangleDragActive;
        private bool cameraDragActive;
        private bool playerSavePending;
        private bool playerSaveInProgress;
        private bool authoringPackageInProgress;
        private bool suppressPlayerSaveScheduling;
        private bool importLibraryMigrationChecked;
        private float playerSaveDueTime;
        private float nextSceneReferenceRetryTime;
        private Vector3Int rectangleStartCell;
        private Vector3Int hoverCell;
        private Vector3Int lastPaintCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        private Vector2 lastCameraDragMouse;
        private string lastMapLoadPath = string.Empty;
        private string newObjectName = string.Empty;
        private int newObjectFootprintX = 1;
        private int newObjectFootprintY = 1;
        private Color newObjectColor = new Color(0.28f, 0.72f, 0.88f, 1f);
        private bool newObjectBlocksMovement = true;
        private bool newObjectIsInteractable;
        private bool newObjectIsStorageContainer;
        private string newRoomPrefabName = string.Empty;
        [SerializeField] private CampusDisplayLanguage displayLanguage = CampusDisplayLanguage.Chinese;
        private string statusText = string.Empty;
        private CampusRuntimeMapLoadSource lastMapLoadSource = CampusRuntimeMapLoadSource.Scene;
        private float statusUntil;
        private Vector2 leftScroll;
        private Vector2 objectScroll;
        private Vector2 floorScroll;
        private Vector2 checklistScroll;
        private Vector2 lightScroll;
        private Vector2 settingsScroll;
        private Vector2 objectSettingsScroll;
        private Rect leftPanelRect;
        private Rect floorPanelRect;
        private Rect checklistPanelRect;
        private Rect bottomToolbarRect;
        private Rect settingsPanelRect;
        private Rect objectSettingsPanelRect;
        private Rect helpPanelRect;
        private Light2D selectedLight;
        private CampusDayNightController dayNightController;
        private Transform runtimeImportPrefabRoot;
        private CampusRuntimeImportTarget activeImportTarget = CampusRuntimeImportTarget.Floor;
        private string activeImportLabel = string.Empty;
        private string customWallName = string.Empty;
        private Texture2D customWallFaceTexture;
        private Texture2D customWallCapTexture;
        private GameObject lastObjectSettingsPrefab;
        private GameObject lastFootprintSyncedPrefab;
        private int objectSettingsPreviewRotation90;
        private int objectSettingsDirectionDropRotation90 = -1;
        private int selectedCustomAnchorIndex;
        private string objectSettingsNameDraft = string.Empty;
        private bool textInputFocused;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private CampusRuntimeFileDropBridge fileDropBridge;
#endif

        private readonly List<TileBase> floorTiles = new List<TileBase>();
        private readonly List<TileBase> wallTiles = new List<TileBase>();
        private readonly List<GameObject> objectPrefabs = new List<GameObject>();
        private readonly List<CampusWallRenderProfile> wallProfiles = new List<CampusWallRenderProfile>();
        private readonly List<TileBase> runtimeCustomWallTiles = new List<TileBase>();
        private readonly List<CampusWallRenderProfile> runtimeCustomWallProfiles = new List<CampusWallRenderProfile>();
        private readonly List<string> roomNames = new List<string>();
        private readonly List<int> roomRequiredCounts = new List<int>();
        private readonly List<CampusRuntimeRoomPrefab> roomPrefabs = new List<CampusRuntimeRoomPrefab>();
        private readonly List<CampusRuntimeGameplayActorSnapshot> cachedGameplayActors =
            new List<CampusRuntimeGameplayActorSnapshot>();
        private readonly List<Texture2D> importedTextures = new List<Texture2D>();
        private readonly List<Sprite> importedSprites = new List<Sprite>();
        private readonly List<UnityEngine.Object> importedAssets = new List<UnityEngine.Object>();
        private readonly Dictionary<string, Texture2D> importedTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> importedTextureRevisionCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> runtimeObjectSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> pendingDroppedPaths = new List<string>();
        private readonly List<string> undoSnapshots = new List<string>();
        private readonly List<string> redoSnapshots = new List<string>();
        private readonly List<Vector3Int> wallVisualRebuildCells = new List<Vector3Int>(64);
        private readonly List<Vector3Int> pendingWallVisualRebuildCells = new List<Vector3Int>(128);
        private readonly HashSet<Vector3Int> pendingWallVisualRebuildCellSet = new HashSet<Vector3Int>();
        private readonly HashSet<Vector2Int> pendingWallVisualRebuildChunks = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector3Int, CampusRuntimeTileSnapshot> wallStrokeUndoBeforeTiles = new Dictionary<Vector3Int, CampusRuntimeTileSnapshot>();
        private readonly List<Vector3Int> wallStrokeUndoCells = new List<Vector3Int>(64);
        private readonly Dictionary<string, string> textInputDrafts = new Dictionary<string, string>();
        private CampusFloorRoot wallStrokeUndoFloor;
        private CampusFloorRoot pendingWallVisualRebuildFloor;

        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallBodyStyle;
        private GUIStyle mutedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle iconButtonStyle;
        private GUIStyle warningStyle;
        private GUIStyle inputStyle;
        private GUIStyle objectSettingsHighlightStyle;
        private Texture2D panelTexture;
        private Texture2D headerTexture;
        private Texture2D buttonTexture;
        private Texture2D selectedTexture;
        private Texture2D hoverTexture;
        private Texture2D inputTexture;
        private Texture2D inputFocusedTexture;
        private Texture2D objectSettingsHighlightTexture;
        private Texture2D lineTexture;
        private Texture2D tileFallbackTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            CampusRuntimeMapEditor existing = FindFirstObjectByType<CampusRuntimeMapEditor>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.InitializeRuntimeSession(false);
            }
        }

        private void Awake()
        {
            Instance = this;
            displayLanguage = CampusLanguageState.CurrentLanguage;
            CampusLanguageState.LanguageChanged += HandleLanguageChanged;
            InitializeRuntimeSession(false);
        }

        private void HandleLanguageChanged(CampusDisplayLanguage language)
        {
            displayLanguage = language;
            activeImportLabel = ResolveImportTargetLabel(activeImportTarget);
        }

        private void InitializeRuntimeSession(bool forceRestore)
        {
            if (runtimeSessionInitialized && !forceRestore)
            {
                return;
            }

            runtimeSessionInitialized = true;
            isReady = false;
            isOpen = openOnStart;
            CampusDynamicShadowUtility.ApplyHighestRuntimeShadowQuality();
            LoadRuntimeResources();
            RefreshSceneReferences(false);
            RememberMapLoadSource(CampusRuntimeMapLoadSource.Scene, GetActiveScenePath());
            if (autoLoadPlayerMapOnStart)
            {
                TryAutoLoadPlayerMap();
            }

            PrepareRuntimeMapPresentationSafe();

            EnsureAreaDefinitionsAvailable(true);
            if (string.IsNullOrWhiteSpace(activeImportLabel))
            {
                activeImportLabel = Tr(CampusRuntimeEditorTextId.FloorImports);
            }

            if (string.IsNullOrWhiteSpace(customWallName))
            {
                customWallName = Tr(CampusRuntimeEditorTextId.CustomWall);
            }

            if (string.IsNullOrWhiteSpace(statusText))
            {
                statusText = Tr(CampusRuntimeEditorTextId.F10ToggleHintStatus);
            }

            isReady = true;
            EnsureFileDropBridge();
            SetStatus(Tr(CampusRuntimeEditorTextId.EditorReadyStatus));
            Debug.Log("[NtingCampusRuntimeMapEditor] Active map source: " + DescribeMapLoadSource());
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }

            CampusLanguageState.LanguageChanged -= HandleLanguageChanged;
            FlushPendingPlayerMapSaveBeforeShutdown();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (fileDropBridge != null)
            {
                fileDropBridge.Dispose();
                fileDropBridge = null;
            }
#endif
        }

        private void OnApplicationQuit()
        {
            FlushPendingPlayerMapSaveBeforeShutdown();
        }

        private void Update()
        {
            if (!IsEditingTextInput() && WasKeyPressed(KeyCode.F10))
            {
                isOpen = !isOpen;
                if (isOpen)
                {
                    MarkSceneReferencesDirty();
                    RefreshSceneReferencesIfNeeded(true);
                }
                else
                {
                    SaveCurrentMapSource(false);
                }
                SetStatus(isOpen
                    ? CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.EditorOpenedStatus)
                    : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.EditorClosedStatus));
            }

            if (isReady)
            {
                ProcessAutoPlayerMapSave();
            }

            if (!isOpen || !isReady)
            {
                return;
            }

            RefreshSceneReferencesIfNeeded();
            EnsureFileDropBridge();
            ProcessPendingDroppedPaths();
            HandleShortcuts();
            if (!HandleCameraNavigation())
            {
                HandleBrushInput();
            }

        }

        private void OnGUI()
        {
            EnsureStyles();
            if (!isOpen)
            {
                HideAllRoomMarkerSpriteRenderers();
                textInputFocused = false;
                return;
            }

            ResolveLayoutRects();
            HandleGuiScrollWheel();
            DrawGridOverlay();
            DrawWorldPreviewOverlay();
            DrawLeftPanel();
            DrawFloorPanel();
            DrawChecklistPanel();
            DrawBottomToolbar();
            DrawSettingsPanel();
            DrawObjectSettingsPanel();
            DrawHelpPanel();
            DrawStatusLine();
            RefreshTextInputFocusState();
        }

        private void LoadRuntimeResources()
        {
            ClearImportedRuntimeAssets();
            EnsureImportFolders();
            floorTiles.Clear();
            wallTiles.Clear();
            objectPrefabs.Clear();
            wallProfiles.Clear();
            stairPrefab = null;

            CampusTilePalette[] tilePalettes = Resources.LoadAll<CampusTilePalette>(RuntimeResourceFolder);
            for (int i = 0; i < tilePalettes.Length; i++)
            {
                CampusTilePalette palette = tilePalettes[i];
                if (palette == null)
                {
                    continue;
                }

                palette.RemoveInvalidEntries();
                for (int tileIndex = 0; tileIndex < palette.FloorTiles.Count; tileIndex++)
                {
                    AddUnique(floorTiles, palette.FloorTiles[tileIndex]);
                }
            }

            CampusWallPalette[] wallPalettes = Resources.LoadAll<CampusWallPalette>(RuntimeResourceFolder);
            for (int i = 0; i < wallPalettes.Length; i++)
            {
                CampusWallPalette palette = wallPalettes[i];
                if (palette == null)
                {
                    continue;
                }

                palette.RemoveInvalidEntries();
                AddUnique(wallTiles, palette.HorizontalWall);
                AddUnique(wallTiles, palette.VerticalWall);
                AddUnique(wallTiles, palette.CornerWall);
                AddUnique(wallTiles, palette.HighWall);
                for (int tileIndex = 0; tileIndex < palette.WallTiles.Count; tileIndex++)
                {
                    AddUnique(wallTiles, palette.WallTiles[tileIndex]);
                }
            }

            CampusPrefabPalette[] prefabPalettes = Resources.LoadAll<CampusPrefabPalette>(RuntimeResourceFolder);
            for (int i = 0; i < prefabPalettes.Length; i++)
            {
                CampusPrefabPalette palette = prefabPalettes[i];
                if (palette == null)
                {
                    continue;
                }

                palette.RemoveInvalidEntries();
                for (int prefabIndex = 0; prefabIndex < palette.Prefabs.Count; prefabIndex++)
                {
                    AddUnique(objectPrefabs, palette.Prefabs[prefabIndex]);
                }
            }

            CampusWallVisualCatalog[] catalogs = Resources.LoadAll<CampusWallVisualCatalog>(RuntimeResourceFolder);
            if (catalogs.Length > 0)
            {
                wallVisualCatalog = catalogs[0];
            }

            CampusWallRenderProfile[] profiles = Resources.LoadAll<CampusWallRenderProfile>(RuntimeResourceFolder);
            for (int i = 0; i < profiles.Length; i++)
            {
                AddUnique(wallProfiles, profiles[i]);
            }

            if (wallVisualCatalog != null)
            {
                AddUnique(wallProfiles, wallVisualCatalog.DefaultProfile);
                if (wallVisualCatalog.Profiles != null)
                {
                    for (int i = 0; i < wallVisualCatalog.Profiles.Count; i++)
                    {
                        AddUnique(wallProfiles, wallVisualCatalog.Profiles[i]);
                    }
                }
            }

            RestoreRuntimeCustomWalls();
            fallbackWallProfile = wallProfiles.Count > 0 ? wallProfiles[Mathf.Clamp(selectedWallProfileIndex, 0, wallProfiles.Count - 1)] : null;
            GameObject[] runtimePrefabs = Resources.LoadAll<GameObject>(RuntimeResourceFolder);
            for (int i = 0; i < runtimePrefabs.Length; i++)
            {
                GameObject prefab = runtimePrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                if (prefab.GetComponent<CampusStairLink>() != null || prefab.name.Contains("妤兼") || prefab.name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    stairPrefab = prefab;
                }
            }

            for (int i = 0; i < objectPrefabs.Count && stairPrefab == null; i++)
            {
                GameObject prefab = objectPrefabs[i];
                if (prefab != null && prefab.GetComponent<CampusStairLink>() != null)
                {
                    stairPrefab = prefab;
                }
            }

            selectedFloorTileIndex = Mathf.Clamp(selectedFloorTileIndex, 0, Mathf.Max(0, floorTiles.Count - 1));
            selectedWallTileIndex = Mathf.Clamp(selectedWallTileIndex, 0, Mathf.Max(0, wallTiles.Count - 1));
            selectedObjectIndex = Mathf.Clamp(selectedObjectIndex, 0, Mathf.Max(0, objectPrefabs.Count - 1));
            LoadUserImports();
            ApplySavedObjectSettingsToPalette();
            EnsureAreaDefinitionsAvailable(false);
        }

        private void LoadUserImports()
        {
            LoadImportedTiles(GetFloorImportFolder(), floorTiles);
            LoadImportedTiles(GetWallImportFolder(), wallTiles);
            LoadImportedObjects(GetObjectImportFolder());
            LoadImportedRooms();
            LoadImportedRoomPrefabs();
            selectedFloorTileIndex = Mathf.Clamp(selectedFloorTileIndex, 0, Mathf.Max(0, floorTiles.Count - 1));
            selectedWallTileIndex = Mathf.Clamp(selectedWallTileIndex, 0, Mathf.Max(0, wallTiles.Count - 1));
            selectedObjectIndex = Mathf.Clamp(selectedObjectIndex, 0, Mathf.Max(0, objectPrefabs.Count - 1));
            selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, Mathf.Max(0, roomNames.Count - 1));
            selectedRoomPrefabIndex = Mathf.Clamp(selectedRoomPrefabIndex, 0, Mathf.Max(0, roomPrefabs.Count - 1));
        }

        private void ReloadUserImportsFromUi()
        {
            LoadRuntimeResources();
            EnsureAreaDefinitionsAvailable(true);
            SetStatus(CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RuntimeImportsRefreshedStatus));
        }

        private void EnsureFileDropBridge()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (fileDropBridge != null)
            {
                return;
            }

            fileDropBridge = CampusRuntimeFileDropBridge.TryCreate(delegate(string[] paths)
            {
                if (paths == null || paths.Length == 0)
                {
                    return;
                }

                pendingDroppedPaths.AddRange(paths);
            });
#endif
        }

        private void ProcessPendingDroppedPaths()
        {
            if (pendingDroppedPaths.Count == 0)
            {
                return;
            }

            List<string> paths = new List<string>(pendingDroppedPaths);
            pendingDroppedPaths.Clear();
            if (TryImportDroppedObjectDirectionSprite(paths))
            {
                return;
            }

            ImportDroppedPaths(paths.ToArray());
        }

        private void EnsureImportFolders()
        {
            Directory.CreateDirectory(GetImportRootFolder());
            Directory.CreateDirectory(GetFloorImportFolder());
            Directory.CreateDirectory(GetWallImportFolder());
            Directory.CreateDirectory(GetObjectImportFolder());
            Directory.CreateDirectory(GetObjectSettingsRootFolder());
            Directory.CreateDirectory(GetRoomPrefabFolder());
            string roomFile = GetRoomImportFile();
            if (!File.Exists(roomFile))
            {
                File.WriteAllText(roomFile, "# One room per line: RoomName or RoomName,Count\n", Encoding.UTF8);
            }

            MigratePersistentImportLibraryToProjectIfNeeded();
        }

        private void LoadImportedTiles(string folder, List<TileBase> destination)
        {
            string[] files = GetImportImageFiles(folder);
            for (int i = 0; i < files.Length; i++)
            {
                Texture2D texture = LoadImportedTexture(files[i]);
                if (texture == null)
                {
                    continue;
                }

                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(1, Mathf.Max(texture.width, texture.height)));
                sprite.name = Path.GetFileNameWithoutExtension(files[i]);
                sprite.hideFlags = HideFlags.DontSave;
                Tile tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = sprite.name;
                tile.sprite = sprite;
                tile.hideFlags = HideFlags.DontSave;
                importedSprites.Add(sprite);
                importedAssets.Add(sprite);
                importedAssets.Add(tile);
                AddUnique(destination, tile);
            }
        }

        private void LoadImportedObjects(string folder)
        {
            string[] files = GetImportImageFiles(folder);
            if (files.Length == 0)
            {
                return;
            }

            Transform root = EnsureRuntimeImportPrefabRoot();
            List<RuntimeImportedObjectDefinition> definitions = BuildImportedObjectDefinitions(files);
            for (int i = 0; i < definitions.Count; i++)
            {
                RuntimeImportedObjectDefinition definition = definitions[i];
                Texture2D texture = LoadImportedTexture(definition.BaseSpritePath);
                if (texture == null)
                {
                    continue;
                }

                Vector2Int footprint = ResolveImportedObjectFootprint(definition.ObjectName, texture);
                Sprite sprite = CreateObjectSprite(texture, definition.ObjectName, footprint);

                GameObject prefab = new GameObject(definition.ObjectName);
                prefab.hideFlags = HideFlags.DontSave;
                prefab.transform.SetParent(root, false);
                prefab.SetActive(false);
                SpriteRenderer renderer = prefab.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                BoxCollider2D collider = prefab.AddComponent<BoxCollider2D>();
                collider.isTrigger = false;
                collider.size = new Vector2(footprint.x, footprint.y);
                CampusPlacedObject placed = prefab.AddComponent<CampusPlacedObject>();
                placed.ObjectId = definition.ObjectName;
                placed.FootprintSize = footprint;
                placed.BlocksMovement = true;
                if (definition.HasDirectionalSprites)
                {
                    placed.OverrideAllowRotation = true;
                    placed.AllowRotation = true;
                    for (int rotation90 = 0; rotation90 < 4; rotation90++)
                    {
                        if (!string.IsNullOrWhiteSpace(definition.DirectionSpritePaths[rotation90]))
                        {
                            AssignRuntimeObjectDirectionSprite(
                                placed,
                                rotation90,
                                true,
                                definition.DirectionSpritePaths[rotation90],
                                definition.ObjectName);
                        }
                    }

                    placed.ApplyRotationVisualState();
                }

                importedAssets.Add(prefab);
                AddUnique(objectPrefabs, prefab);
            }
        }

        private List<RuntimeImportedObjectDefinition> BuildImportedObjectDefinitions(string[] files)
        {
            Dictionary<string, RuntimeImportedObjectDefinition> definitionMap = new Dictionary<string, RuntimeImportedObjectDefinition>(StringComparer.OrdinalIgnoreCase);
            List<RuntimeImportedObjectDefinition> definitions = new List<RuntimeImportedObjectDefinition>();
            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string objectName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    continue;
                }

                if (TryParseDirectionalObjectImportName(objectName, out string groupedObjectName, out int rotation90))
                {
                    RuntimeImportedObjectDefinition definition = GetOrCreateImportedObjectDefinition(definitionMap, definitions, groupedObjectName);
                    definition.DirectionSpritePaths[rotation90] = NormalizeSerializedImportPath(filePath);
                    if (string.IsNullOrWhiteSpace(definition.BaseSpritePath) || rotation90 == 0)
                    {
                        definition.BaseSpritePath = filePath;
                    }

                    continue;
                }

                RuntimeImportedObjectDefinition standalone = GetOrCreateImportedObjectDefinition(definitionMap, definitions, objectName);
                standalone.BaseSpritePath = filePath;
            }

            definitions.Sort((a, b) => string.Compare(a.ObjectName, b.ObjectName, StringComparison.OrdinalIgnoreCase));
            return definitions;
        }

        private static RuntimeImportedObjectDefinition GetOrCreateImportedObjectDefinition(
            Dictionary<string, RuntimeImportedObjectDefinition> definitionMap,
            List<RuntimeImportedObjectDefinition> definitions,
            string objectName)
        {
            if (definitionMap.TryGetValue(objectName, out RuntimeImportedObjectDefinition definition))
            {
                return definition;
            }

            definition = new RuntimeImportedObjectDefinition(objectName);
            definitionMap.Add(objectName, definition);
            definitions.Add(definition);
            return definition;
        }

        private static bool TryParseDirectionalObjectImportName(string objectName, out string baseObjectName, out int rotation90)
        {
            baseObjectName = objectName;
            rotation90 = 0;
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            Match match = Regex.Match(
                objectName,
                @"^(?<base>.+?)(?:[_\-\s]+)(?<dir>0|90|180|270|front|right|back|left|up|down|north|east|south|west|qian|hou|zuo|you|shang|xia|bei|dong|nan|xi|前|后|後|左|右|上|下|北|东|東|南|西)$",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            string rawBaseName = match.Groups["base"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawBaseName))
            {
                return false;
            }

            string dir = match.Groups["dir"].Value.Trim().ToLowerInvariant();
            switch (dir)
            {
                case "0":
                case "front":
                case "up":
                case "north":
                case "qian":
                case "shang":
                case "bei":
                case "前":
                case "上":
                case "北":
                    rotation90 = 0;
                    break;
                case "90":
                case "right":
                case "east":
                case "you":
                case "dong":
                case "右":
                case "东":
                case "東":
                    rotation90 = 1;
                    break;
                case "180":
                case "back":
                case "down":
                case "south":
                case "hou":
                case "xia":
                case "nan":
                case "后":
                case "後":
                case "下":
                case "南":
                    rotation90 = 2;
                    break;
                case "270":
                case "left":
                case "west":
                case "zuo":
                case "xi":
                case "左":
                case "西":
                    rotation90 = 3;
                    break;
                default:
                    return false;
            }

            baseObjectName = rawBaseName;
            return true;
        }

        private Sprite CreateObjectSprite(Texture2D texture, string spriteName, Vector2Int footprint)
        {
            if (texture == null)
            {
                return null;
            }

            Vector2Int normalizedFootprint = CampusPlacedObject.NormalizeFootprintSize(footprint);
            float pixelsPerUnit = Mathf.Max(
                1f,
                Mathf.Max(
                    texture.width / (float)normalizedFootprint.x,
                    texture.height / (float)normalizedFootprint.y));
            Rect spriteRect = ResolveRuntimeObjectSpriteRect(texture);
            Vector2 pivot = ResolveRuntimeObjectSpritePivot(texture, spriteRect, new Vector2(0.5f, 0.5f));
            Sprite sprite = Sprite.Create(texture, spriteRect, pivot, pixelsPerUnit);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            importedSprites.Add(sprite);
            importedAssets.Add(sprite);
            return sprite;
        }

        private static Rect ResolveRuntimeObjectSpriteRect(Texture2D texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            Rect fullRect = new Rect(0f, 0f, texture.width, texture.height);
            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException)
            {
                return fullRect;
            }

            if (pixels == null || pixels.Length == 0)
            {
                return fullRect;
            }

            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < texture.height; y++)
            {
                int row = y * texture.width;
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[row + x].a <= RuntimeObjectSpriteAlphaThreshold)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return fullRect;
            }

            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static Vector2 ResolveRuntimeObjectSpritePivot(Texture2D texture, Rect spriteRect, Vector2 fullTexturePivot)
        {
            if (texture == null || spriteRect.width <= 0f || spriteRect.height <= 0f)
            {
                return new Vector2(0.5f, 0.5f);
            }

            Vector2 pivotPixels = new Vector2(
                Mathf.Clamp01(fullTexturePivot.x) * texture.width,
                Mathf.Clamp01(fullTexturePivot.y) * texture.height);
            return new Vector2(
                Mathf.Clamp01((pivotPixels.x - spriteRect.xMin) / spriteRect.width),
                Mathf.Clamp01((pivotPixels.y - spriteRect.yMin) / spriteRect.height));
        }

        private void CreateRuntimeObjectFromEditorFields()
        {
            string displayName = string.IsNullOrWhiteSpace(newObjectName) ? Tr("\u65b0\u7269\u4f53", "New Object") : newObjectName.Trim();
            Vector2Int footprint = new Vector2Int(
                Mathf.Clamp(newObjectFootprintX, 1, 32),
                Mathf.Clamp(newObjectFootprintY, 1, 32));

            EnsureImportFolders();
            string safeName = SanitizeFileName(displayName);
            if (safeName == "_")
            {
                safeName = "Object";
            }

            string path = MakeUniqueImportPath(Path.Combine(GetObjectImportFolder(), safeName + "_" + footprint.x + "x" + footprint.y + ".png"));
            Texture2D texture = CreateGeneratedObjectTexture(footprint, newObjectColor);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                RefreshImportAssetDatabaseIfProjectBacked();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to create object image '" + path + "': " + exception.Message);
                SetStatus(TrFormat("\u521b\u5efa\u7269\u4f53\u5931\u8d25\uff1a{0}", "Create object failed: {0}", exception.Message));
                return;
            }
            finally
            {
                DestroyRuntimeObject(texture);
            }

            string objectId = Path.GetFileNameWithoutExtension(path);
            LoadRuntimeResources();
            int prefabIndex = FindPrefabIndexByName(objectId);
            if (prefabIndex < 0)
            {
                SetStatus(Tr("\u5df2\u751f\u6210\u7269\u4f53\u56fe\u7247\uff0c\u4f46\u672a\u80fd\u52a0\u8f7d\u5230\u8d44\u6e90\u9762\u677f\u3002", "Object image was generated, but it could not be loaded into the resource panel."));
                return;
            }

            selectedObjectIndex = prefabIndex;
            brushMode = CampusRuntimeBrushMode.PlaceObject;
            activeTab = CampusRuntimeEditorTab.Objects;

            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed != null)
            {
                placed.ObjectId = objectId;
                placed.TypeId = InferObjectTypeId(objectId, displayName, newObjectIsStorageContainer);
                placed.DisplayNameOverride = displayName;
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = footprint;
                placed.BlocksMovement = newObjectBlocksMovement;
                placed.BlocksSight = false;
                placed.IsInteractable = newObjectIsInteractable || newObjectIsStorageContainer;
                placed.IsStorageContainer = newObjectIsStorageContainer;
                placed.StorageSize = CampusPlacedObject.DefaultStorageSize;
                placed.StorageMaxWeight = CampusPlacedObject.DefaultStorageMaxWeight;
                placed.ApplyRotationVisualState();
                placed.ApplyInteractionState();
                SaveRuntimeObjectSettings(CaptureRuntimeObjectSettings(prefab, placed));
            }

            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u521b\u5efa\u7269\u4f53\uff1a{0} {1}x{2}", "Created object: {0} {1}x{2}", displayName, footprint.x, footprint.y));
        }

        private Texture2D CreateGeneratedObjectTexture(Vector2Int footprint, Color baseColor)
        {
            const int cellPixels = 32;
            Vector2Int normalizedFootprint = CampusPlacedObject.NormalizeFootprintSize(footprint);
            int width = Mathf.Clamp(normalizedFootprint.x, 1, 32) * cellPixels;
            int height = Mathf.Clamp(normalizedFootprint.y, 1, 32) * cellPixels;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color body = new Color(
                Mathf.Clamp01(baseColor.r),
                Mathf.Clamp01(baseColor.g),
                Mathf.Clamp01(baseColor.b),
                1f);
            Color outline = Color.Lerp(body, Color.black, 0.58f);
            Color highlight = Color.Lerp(body, Color.white, 0.28f);
            Color shadow = Color.Lerp(body, Color.black, 0.22f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, GetGeneratedObjectPixel(x, y, width, height, cellPixels, body, outline, highlight, shadow));
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static Color GetGeneratedObjectPixel(int x, int y, int width, int height, int cellPixels, Color body, Color outline, Color highlight, Color shadow)
        {
            bool border = x < 2 || y < 2 || x >= width - 2 || y >= height - 2;
            if (border)
            {
                return outline;
            }

            bool grid = (width > cellPixels && x % cellPixels <= 1) || (height > cellPixels && y % cellPixels <= 1);
            if (grid)
            {
                return Color.Lerp(body, outline, 0.22f);
            }

            float vertical = Mathf.InverseLerp(2f, Mathf.Max(3f, height - 3f), y);
            Color pixel = Color.Lerp(shadow, highlight, vertical);
            pixel = Color.Lerp(pixel, body, 0.72f);

            bool cornerDot =
                (x >= 5 && x <= 8 && y >= 5 && y <= 8) ||
                (x >= width - 9 && x <= width - 6 && y >= 5 && y <= 8) ||
                (x >= 5 && x <= 8 && y >= height - 9 && y <= height - 6) ||
                (x >= width - 9 && x <= width - 6 && y >= height - 9 && y <= height - 6);
            if (cornerDot)
            {
                return Color.Lerp(body, Color.white, 0.42f);
            }

            if ((x * 11 + y * 7) % 53 == 0)
            {
                pixel = Color.Lerp(pixel, Color.white, 0.08f);
            }

            pixel.a = 1f;
            return pixel;
        }

        private void LoadImportedRooms()
        {
            string roomFile = GetRoomImportFile();
            if (!File.Exists(roomFile))
            {
                return;
            }

            ImportRoomDefinitionsFromText(File.ReadAllText(roomFile));
        }

        private void LoadImportedRoomPrefabs()
        {
            roomPrefabs.Clear();
            string folder = GetRoomPrefabFolder();
            Directory.CreateDirectory(folder);
            string[] files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    CampusRuntimeRoomPrefab roomPrefab = JsonUtility.FromJson<CampusRuntimeRoomPrefab>(File.ReadAllText(files[i], Encoding.UTF8));
                    if (roomPrefab == null)
                    {
                        continue;
                    }

                    NormalizeRoomPrefab(roomPrefab, Path.GetFileNameWithoutExtension(files[i]));
                    roomPrefab.SourcePath = files[i];
                    if (!string.IsNullOrWhiteSpace(roomPrefab.RoomName))
                    {
                        roomPrefabs.Add(roomPrefab);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to load room prefab '" + files[i] + "': " + exception.Message);
                }
            }

            selectedRoomPrefabIndex = roomPrefabs.Count > 0 ? Mathf.Clamp(selectedRoomPrefabIndex, 0, roomPrefabs.Count - 1) : 0;
        }

        private Texture2D LoadImportedTexture(string path)
        {
            try
            {
                string resolvedPath = ResolveImportContentPath(path);
                if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
                {
                    return null;
                }

                long revision = GetImportedTextureRevision(resolvedPath);
                if (importedTextureCache.TryGetValue(resolvedPath, out Texture2D cachedTexture) &&
                    cachedTexture != null &&
                    importedTextureRevisionCache.TryGetValue(resolvedPath, out long cachedRevision) &&
                    cachedRevision == revision)
                {
                    return cachedTexture;
                }

                byte[] bytes = File.ReadAllBytes(resolvedPath);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.DontSave;
                if (!texture.LoadImage(bytes))
                {
                    DestroyRuntimeObject(texture);
                    return null;
                }

                texture.name = Path.GetFileNameWithoutExtension(resolvedPath);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                importedTextureCache[resolvedPath] = texture;
                importedTextureRevisionCache[resolvedPath] = revision;
                importedTextures.Add(texture);
                importedAssets.Add(texture);
                return texture;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to import image '" + path + "': " + exception.Message);
                return null;
            }
        }

        private long GetImportedTextureRevision(string resolvedPath)
        {
            try
            {
                FileInfo info = new FileInfo(resolvedPath);
                return info.Exists ? (info.LastWriteTimeUtc.Ticks ^ info.Length) : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        private string[] GetImportImageFiles(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return Array.Empty<string>();
            }

            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(folder, "*.png"));
            files.AddRange(Directory.GetFiles(folder, "*.jpg"));
            files.AddRange(Directory.GetFiles(folder, "*.jpeg"));
            files.AddRange(Directory.GetFiles(folder, "*.bmp"));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files.ToArray();
        }

        private Vector2Int ResolveImportedObjectFootprint(string objectName, Texture2D texture)
        {
            Match match = Regex.Match(objectName, @"(?:^|[_\-\s])(\d+)x(\d+)(?:$|[_\-\s])", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return new Vector2Int(
                    Mathf.Clamp(int.Parse(match.Groups[1].Value), 1, 32),
                    Mathf.Clamp(int.Parse(match.Groups[2].Value), 1, 32));
            }

            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return Vector2Int.one;
            }

            float basePixels = Mathf.Max(1f, Mathf.Min(texture.width, texture.height));
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(texture.width / basePixels), 1, 32),
                Mathf.Clamp(Mathf.RoundToInt(texture.height / basePixels), 1, 32));
        }

        private Transform EnsureRuntimeImportPrefabRoot()
        {
            if (runtimeImportPrefabRoot != null)
            {
                return runtimeImportPrefabRoot;
            }

            GameObject root = new GameObject("Runtime Imported Object Sources");
            root.hideFlags = HideFlags.DontSave;
            root.SetActive(false);
            DontDestroyOnLoad(root);
            runtimeImportPrefabRoot = root.transform;
            return runtimeImportPrefabRoot;
        }

        private void ClearImportedRuntimeAssets()
        {
            if (runtimeImportPrefabRoot != null)
            {
                DestroyRuntimeObject(runtimeImportPrefabRoot.gameObject);
                runtimeImportPrefabRoot = null;
            }

            // Keep generated tiles/sprites/textures alive because existing tilemaps and placed objects may still reference them.
            importedAssets.Clear();
            importedTextures.Clear();
            importedSprites.Clear();
            runtimeObjectSpriteCache.Clear();
        }

        private void RestoreRuntimeCustomWalls()
        {
            for (int i = 0; i < runtimeCustomWallTiles.Count; i++)
            {
                AddUnique(wallTiles, runtimeCustomWallTiles[i]);
            }

            for (int i = 0; i < runtimeCustomWallProfiles.Count; i++)
            {
                CampusWallRenderProfile profile = runtimeCustomWallProfiles[i];
                AddUnique(wallProfiles, profile);
                if (profile != null)
                {
                    EnsureRuntimeWallCatalog(profile);
                }
            }
        }

        private string GetImportRootFolder()
        {
#if UNITY_EDITOR
            return GetAuthoringPackageImportFolder();
#else
            return GetPersistentImportRootFolder();
#endif
        }

        private string NormalizeSerializedImportPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = NormalizeClipboardPath(path).Replace('\\', '/');
            if (!Path.IsPathRooted(normalized))
            {
                const string importFolderPrefix = RuntimeImportFolder + "/";
                if (normalized.StartsWith(importFolderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(importFolderPrefix.Length);
                }

                return normalized.TrimStart('/');
            }

            string importRoot = Path.GetFullPath(GetImportRootFolder()).Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(importRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(importRoot.Length + 1);
            }

            return Path.GetFileName(normalized);
        }

        private string ResolveImportContentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = NormalizeClipboardPath(path);
            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            normalized = normalized.Replace('\\', '/');
            const string importFolderPrefix = RuntimeImportFolder + "/";
            if (normalized.StartsWith(importFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(importFolderPrefix.Length);
            }

            string relativePath = normalized.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(GetImportRootFolder(), relativePath);
        }

        private string GetPersistentImportRootFolder()
        {
            return Path.Combine(Application.persistentDataPath, RuntimeImportFolder);
        }

        private string GetFloorImportFolder()
        {
            return Path.Combine(GetImportRootFolder(), FloorImportFolder);
        }

        private string GetWallImportFolder()
        {
            return Path.Combine(GetImportRootFolder(), WallImportFolder);
        }

        private string GetObjectImportFolder()
        {
            return Path.Combine(GetImportRootFolder(), ObjectImportFolder);
        }

        private string GetObjectSettingsRootFolder()
        {
            return Path.Combine(GetImportRootFolder(), ObjectSettingsFolder);
        }

        private string GetObjectSettingsFolder(string objectId)
        {
            return Path.Combine(GetObjectSettingsRootFolder(), SanitizeFileName(objectId));
        }

        private string GetObjectSettingsPath(string objectId)
        {
            return Path.Combine(GetObjectSettingsFolder(objectId), "settings.json");
        }

        private string GetRoomImportFile()
        {
            return Path.Combine(GetImportRootFolder(), RoomImportFile);
        }

        private string GetRoomPrefabFolder()
        {
            return Path.Combine(GetImportRootFolder(), RoomPrefabFolder);
        }

        private void RefreshSceneReferences()
        {
            RefreshSceneReferences(false);
        }

        private void MarkSceneReferencesDirty()
        {
            sceneReferencesDirty = true;
        }

        private void RefreshSceneReferencesIfNeeded(bool force = false)
        {
            bool missingCriticalReference = mapRoot == null || dayNightController == null;
            if (!force && !sceneReferencesDirty && !missingCriticalReference)
            {
                RefreshSceneCameraReference();
                return;
            }

            if (!force && !sceneReferencesDirty && Application.isPlaying && Time.unscaledTime < nextSceneReferenceRetryTime)
            {
                RefreshSceneCameraReference();
                return;
            }

            RefreshSceneReferences(false);
        }

        private void RefreshSceneCameraReference()
        {
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
                if (sceneCamera == null)
                {
                    sceneCamera = FindFirstObjectByType<Camera>();
                }
            }
        }

        private void RefreshSceneReferences(bool createMissingSceneContent)
        {
            if (mapRoot == null)
            {
                mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            }

            if (mapRoot == null && createMissingSceneContent)
            {
                mapRoot = CreateMapRoot();
            }

            sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                sceneCamera = FindFirstObjectByType<Camera>();
            }

            if (mapRoot != null)
            {
                if (createMissingSceneContent)
                {
                    EnsureFloor(selectedFloorIndex);
                }

                mapRoot.RebuildFloorReferences();
                if (selectedFloorIndex <= 0)
                {
                    selectedFloorIndex = 1;
                }

                if (!createMissingSceneContent && mapRoot.GetFloor(selectedFloorIndex) == null && mapRoot.Floors.Count > 0)
                {
                    selectedFloorIndex = mapRoot.Floors[0].FloorIndex;
                }
            }

            dayNightController = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            if (dayNightController != null)
            {
                dayNightController.RefreshCampusCenter();
            }

            stairTargetFloorIndex = Mathf.Max(1, stairTargetFloorIndex);
            sceneReferencesDirty = false;
            if (Application.isPlaying)
            {
                nextSceneReferenceRetryTime = Time.unscaledTime + SceneReferenceRetryInterval;
            }
        }

        private void PrepareRuntimeMapPresentation()
        {
            if (mapRoot == null)
            {
                return;
            }

            CampusRenderSortingUtility.ConfigureTopDownTransparencySort();
            mapRoot.RebuildFloorReferences();
            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = mapRoot.Floors[i];
                if (floor == null)
                {
                    continue;
                }

                EnsureFloorStructure(floor);
                RebuildWallVisuals(floor);
                CampusDynamicShadowUtility.EnsureObjectShadowCasters(floor);
                CampusWallAutoRenderer.ApplyDebugView(floor, CampusWallDebugView.ShowFinalWallVisuals);
                CampusWallTileUtility.SetTilemapVisible(floor.CollisionDebugTilemap, false);
                CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            }
        }

        private void PrepareRuntimeMapPresentationSafe()
        {
            try
            {
                PrepareRuntimeMapPresentation();
            }
            catch (Exception exception)
            {
                SetStatus(TrFormat("\u5730\u56fe\u8868\u73b0\u5237\u65b0\u5931\u8d25\uff1a{0}", "Map presentation refresh failed: {0}", exception.Message));
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Map presentation setup failed, keeping runtime editor UI alive: " + exception.Message);
            }
        }

        private void EnsureRoomRequirements()
        {
            while (roomRequiredCounts.Count < roomNames.Count)
            {
                roomRequiredCounts.Add(1);
            }

            while (roomRequiredCounts.Count > roomNames.Count)
            {
                roomRequiredCounts.RemoveAt(roomRequiredCounts.Count - 1);
            }

            selectedRoomIndex = roomNames.Count > 0 ? Mathf.Clamp(selectedRoomIndex, 0, roomNames.Count - 1) : 0;
            selectedRoomPrefabIndex = roomPrefabs.Count > 0 ? Mathf.Clamp(selectedRoomPrefabIndex, 0, roomPrefabs.Count - 1) : 0;
        }

        private void EnsureAreaDefinitionsAvailable(bool addDefaultDefinitions)
        {
            int previousSelectedIndex = selectedRoomIndex;
            EnsureRoomRequirements();
            SyncPresetAreaDefinitions();
            EnsureRoomRequirements();
            if (roomNames.Count > 0)
            {
                selectedRoomIndex = Mathf.Clamp(previousSelectedIndex, 0, roomNames.Count - 1);
            }
        }

        private void SyncPresetAreaDefinitions()
        {
            roomNames.Clear();
            roomRequiredCounts.Clear();
            for (int i = 0; i < DefaultAreaPresets.Length; i++)
            {
                CampusRuntimeAreaPreset preset = DefaultAreaPresets[i];
                if (preset == null || string.IsNullOrWhiteSpace(preset.RoomName))
                {
                    continue;
                }

                roomNames.Add(preset.RoomName);
                roomRequiredCounts.Add(preset.RequiredCount);
            }
        }

        private void AddAreaDefinitionsFromPlacedMarkers()
        {
            SyncPresetAreaDefinitions();
        }

        private void AddRoomDefinitionIfMissing(string roomName, int required)
        {
            if (FindRoomDefinitionIndex(roomName) >= 0)
            {
                return;
            }

            AddOrUpdateRoomDefinition(roomName, required);
        }

        private int FindRoomDefinitionIndex(string roomName)
        {
            if (!TryResolvePresetRoomName(roomName, out string presetRoomName))
            {
                return -1;
            }

            for (int i = 0; i < roomNames.Count; i++)
            {
                if (string.Equals(roomNames[i], presetRoomName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryResolvePresetRoomName(string roomName, out string presetRoomName)
        {
            presetRoomName = string.Empty;
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return false;
            }

            string key = NormalizeAreaPresetKey(roomName);
            for (int i = 0; i < DefaultAreaPresets.Length; i++)
            {
                CampusRuntimeAreaPreset preset = DefaultAreaPresets[i];
                if (preset == null)
                {
                    continue;
                }

                if (key == NormalizeAreaPresetKey(preset.RoomName) ||
                    key == NormalizeAreaPresetKey(preset.EnglishLabel) ||
                    key == NormalizeAreaPresetKey(preset.ChineseLabel))
                {
                    presetRoomName = preset.RoomName;
                    return true;
                }
            }

            if (ContainsRoomNameToken(key, "\u6559\u5ba4", "class"))
            {
                presetRoomName = "Classroom";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u8d70\u5eca", "\u8fc7\u9053", "corridor", "hall"))
            {
                presetRoomName = "Corridor";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u529e\u516c\u5ba4", "\u6559\u5e08", "office", "teacher"))
            {
                presetRoomName = "Office";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u516c\u5171", "\u6d3b\u52a8", "common", "activity"))
            {
                presetRoomName = "CommonActivityZone";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u98df\u5802", "\u9910\u5385", "canteen", "dining"))
            {
                presetRoomName = "Canteen";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u8d85\u5e02", "\u5546\u5e97", "\u5c0f\u5356", "shop", "store", "market"))
            {
                presetRoomName = "Store";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u5ba4\u5916", "\u6821\u5916", "\u64cd\u573a", "\u5916\u5356", "outdoor", "outside", "delivery"))
            {
                presetRoomName = "Outdoor";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u5bbf\u820d", "dorm"))
            {
                presetRoomName = "Dormitory";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u536b\u751f\u95f4", "\u5395\u6240", "\u6d17\u624b\u95f4", "restroom", "toilet", "bath"))
            {
                presetRoomName = "Restroom";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u56fe\u4e66\u9986", "library"))
            {
                presetRoomName = "Library";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u697c\u68af", "stair"))
            {
                presetRoomName = "Stairwell";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u4eba\u4e8b", "humanresources", "hr"))
            {
                presetRoomName = "HumanResources";
                return true;
            }

            if (ContainsRoomNameToken(key, "\u795e\u9f9b", "shrine"))
            {
                presetRoomName = "ShrineRoom";
                return true;
            }

            return false;
        }

        private static string NormalizeAreaPresetKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private CampusRuntimeAreaPreset GetAreaPreset(string roomName)
        {
            if (!TryResolvePresetRoomName(roomName, out string presetRoomName))
            {
                return null;
            }

            for (int i = 0; i < DefaultAreaPresets.Length; i++)
            {
                CampusRuntimeAreaPreset preset = DefaultAreaPresets[i];
                if (preset != null && string.Equals(preset.RoomName, presetRoomName, StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }
            }

            return null;
        }

        private string GetAreaPresetLabel(string roomName)
        {
            CampusRuntimeAreaPreset preset = GetAreaPreset(roomName);
            return preset != null
                ? Tr(preset.ChineseLabel, preset.EnglishLabel)
                : roomName;
        }

        private void HandleShortcuts()
        {
            if (IsEditingTextInput())
            {
                return;
            }

            if (WasKeyPressed(KeyCode.Escape))
            {
                if (showObjectSettings)
                {
                    showObjectSettings = false;
                }
                else if (showHelpOverlay)
                {
                    showHelpOverlay = false;
                }
                else if (showSettings)
                {
                    showSettings = false;
                }
                else
                {
                    isOpen = false;
                }
            }

            if (WasKeyPressed(KeyCode.G))
            {
                showGridOverlay = !showGridOverlay;
            }

            if (WasKeyPressed(KeyCode.R))
            {
                rotation90 = (rotation90 + 1) % 4;
                SetStatus(TrFormat("\u65cb\u8f6c\uff1a{0} \u5ea6", "Rotation: {0} deg", rotation90 * 90));
            }

            if (WasKeyPressed(KeyCode.LeftBracket))
            {
                brushSize = Mathf.Max(1, brushSize - 1);
            }

            if (WasKeyPressed(KeyCode.RightBracket))
            {
                brushSize = Mathf.Min(8, brushSize + 1);
            }

            if ((IsKeyHeld(KeyCode.LeftControl) || IsKeyHeld(KeyCode.RightControl)) && WasKeyPressed(KeyCode.Z))
            {
                UndoSnapshot();
            }

            if ((IsKeyHeld(KeyCode.LeftControl) || IsKeyHeld(KeyCode.RightControl)) && WasKeyPressed(KeyCode.Y))
            {
                RedoSnapshot();
            }
        }

        private bool HandleCameraNavigation()
        {
            if (sceneCamera == null)
            {
                return false;
            }

            Vector2 mouse = GetMouseScreenPosition();
            if (IsPointerOverEditorUi(mouse))
            {
                cameraDragActive = false;
                return false;
            }

            bool panTool = brushMode == CampusRuntimeBrushMode.Pan;
            bool dragStart = WasMouseButtonPressed(2) || (IsKeyHeld(KeyCode.Space) && WasMouseButtonPressed(0)) || (panTool && WasMouseButtonPressed(0));
            bool dragging = IsMouseButtonHeld(2) || (IsKeyHeld(KeyCode.Space) && IsMouseButtonHeld(0)) || (panTool && IsMouseButtonHeld(0));
            if (dragStart)
            {
                cameraDragActive = true;
                lastCameraDragMouse = mouse;
            }

            if (cameraDragActive && dragging)
            {
                PanCamera(mouse);
                lastCameraDragMouse = mouse;
                return true;
            }

            if (!dragging)
            {
                cameraDragActive = false;
            }

            return false;
        }

        private void PanCamera(Vector2 currentMouse)
        {
            Vector3 previousWorld = sceneCamera.ScreenToWorldPoint(new Vector3(lastCameraDragMouse.x, lastCameraDragMouse.y, GetCameraPlaneDistance()));
            Vector3 currentWorld = sceneCamera.ScreenToWorldPoint(new Vector3(currentMouse.x, currentMouse.y, GetCameraPlaneDistance()));
            Vector3 delta = previousWorld - currentWorld;
            delta.z = 0f;
            sceneCamera.transform.position += delta;
        }

        private void ZoomCameraAt(Vector2 screenPosition, float scroll)
        {
            if (sceneCamera == null)
            {
                return;
            }

            Vector3 before = sceneCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, GetCameraPlaneDistance()));
            float zoomFactor = Mathf.Pow(1f - ZoomStep, scroll);
            if (sceneCamera.orthographic)
            {
                sceneCamera.orthographicSize = Mathf.Clamp(sceneCamera.orthographicSize * zoomFactor, minCameraSize, maxCameraSize);
            }
            else
            {
                sceneCamera.fieldOfView = Mathf.Clamp(sceneCamera.fieldOfView * zoomFactor, 20f, 90f);
            }

            Vector3 after = sceneCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, GetCameraPlaneDistance()));
            Vector3 delta = before - after;
            delta.z = 0f;
            sceneCamera.transform.position += delta;
        }

        private void HandleGuiScrollWheel()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.ScrollWheel)
            {
                return;
            }

            if (IsGuiPositionOverEditorUi(current.mousePosition))
            {
                return;
            }

            Vector2 screenPosition = new Vector2(current.mousePosition.x, Screen.height - current.mousePosition.y);
            ZoomCameraAt(screenPosition, -current.delta.y);
            current.Use();
        }

        private void HandleBrushInput()
        {
            CampusFloorRoot floor = ResolveSelectedFloorForBrushInput();
            if (floor == null || floor.Grid == null || sceneCamera == null)
            {
                return;
            }

            Vector3 mouseWorld = ScreenToWorld(GetMouseScreenPosition());
            hoverCell = floor.Grid.WorldToCell(mouseWorld);
            hoverCell.z = 0;

            bool leftDown = WasMouseButtonPressed(0);
            bool leftHeld = IsMouseButtonHeld(0);
            bool leftUp = WasMouseButtonReleased(0);
            bool rightDown = WasMouseButtonPressed(1);
            bool pointerOverUi = IsPointerOverEditorUi(GetMouseScreenPosition());
            bool forceErase = rightDown || (leftDown && (IsKeyHeld(KeyCode.LeftShift) || IsKeyHeld(KeyCode.RightShift)));

            if (!leftHeld && !leftUp && !IsMouseButtonHeld(1))
            {
                FlushPendingWallVisualRebuild();
                FinalizePendingWallStrokeUndo();
                strokeActive = false;
                strokeUndoRecorded = false;
                wallStrokeVisualPreviewInitialized = false;
                rectangleDragActive = false;
                lastPaintCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            }

            if (pointerOverUi)
            {
                return;
            }

            if (brushMode == CampusRuntimeBrushMode.Pan)
            {
                return;
            }

            if (forceErase)
            {
                if (IsGameplayBrushMode(brushMode))
                {
                    EraseGameplayMarkersAtCell(floor, hoverCell, true, true, true, true);
                }
                else
                {
                    BeginStrokeUndo(floor);
                    EraseAtCell(floor, hoverCell);
                }

                lastPaintCell = hoverCell;
                return;
            }

            if (IsRectangleDragBrushMode(brushMode))
            {
                if (leftDown)
                {
                    rectangleDragActive = true;
                    rectangleStartCell = hoverCell;
                }

                if (rectangleDragActive && leftUp)
                {
                    if (brushMode == CampusRuntimeBrushMode.CreateRoomPrefab)
                    {
                        CreateRoomPrefabFromSelection(floor, rectangleStartCell, hoverCell);
                    }
                    else
                    {
                        RecordUndo();
                        ApplyRectangleBrush(floor, rectangleStartCell, hoverCell, brushMode);
                    }

                    rectangleDragActive = false;
                }

                return;
            }

            if (!leftDown && !leftHeld)
            {
                return;
            }

            bool isContinuous = brushMode == CampusRuntimeBrushMode.PaintFloor ||
                                brushMode == CampusRuntimeBrushMode.PaintWall ||
                                brushMode == CampusRuntimeBrushMode.Erase;
            if (isContinuous && hoverCell == lastPaintCell)
            {
                return;
            }

            switch (brushMode)
            {
                case CampusRuntimeBrushMode.PaintFloor:
                    BeginStrokeUndo(floor);
                    PaintFloor(floor, hoverCell);
                    break;
                case CampusRuntimeBrushMode.PaintWall:
                    BeginStrokeUndo(floor);
                    PaintWall(floor, hoverCell);
                    break;
                case CampusRuntimeBrushMode.PlaceObject:
                    if (leftDown)
                    {
                        RecordUndo();
                        PlaceObject(floor, hoverCell);
                    }

                    break;
                case CampusRuntimeBrushMode.PlaceStair:
                    if (leftDown)
                    {
                        RecordUndo();
                        PlaceStair(floor, hoverCell);
                    }

                    break;
                case CampusRuntimeBrushMode.PlaceRoom:
                    if (leftDown)
                    {
                        RecordUndo();
                        PlaceRoomMarker(floor, hoverCell);
                    }

                    break;
                case CampusRuntimeBrushMode.PlaceGameplayMarker:
                    if (leftDown)
                    {
                        PlaceGameplayMarker(floor, hoverCell);
                    }

                    break;
                case CampusRuntimeBrushMode.PlaceRoomPrefab:
                    if (leftDown)
                    {
                        RecordUndo();
                        PlaceRoomPrefab(floor, hoverCell);
                    }

                    break;
                case CampusRuntimeBrushMode.PlaceLight:
                    if (leftDown)
                    {
                        RecordUndo();
                        PlaceLight(floor, hoverCell, mouseWorld);
                    }

                    break;
                case CampusRuntimeBrushMode.Erase:
                    BeginStrokeUndo(floor);
                    EraseAtCell(floor, hoverCell);
                    break;
                case CampusRuntimeBrushMode.EraseGameplayMarker:
                    if (leftDown)
                    {
                        EraseGameplayMarkersAtCell(floor, hoverCell, true, true, true, true);
                    }

                    break;
                case CampusRuntimeBrushMode.Pick:
                    if (leftDown)
                    {
                        PickAtCell(floor, hoverCell);
                    }

                    break;
            }

            lastPaintCell = hoverCell;
        }

        private CampusFloorRoot ResolveSelectedFloorForBrushInput()
        {
            if (mapRoot == null)
            {
                return null;
            }

            if (sceneReferencesDirty)
            {
                RefreshSceneReferencesIfNeeded(true);
            }

            CampusFloorRoot floor = mapRoot.GetFloor(selectedFloorIndex);
            if (floor != null)
            {
                return floor;
            }

            return EnsureFloor(selectedFloorIndex);
        }

        private void BeginStrokeUndo(CampusFloorRoot floor)
        {
            if (!strokeActive)
            {
                strokeActive = true;
                strokeUndoRecorded = false;
            }

            if (!strokeUndoRecorded)
            {
                if (brushMode == CampusRuntimeBrushMode.PaintWall)
                {
                    BeginWallStrokeUndo(floor);
                }
                else
                {
                    RecordUndo();
                }

                strokeUndoRecorded = true;
            }
        }

        private void BeginWallStrokeUndo(CampusFloorRoot floor)
        {
            using (CampusWallBuildProfiler.BeginWallStrokeUndo.Auto())
            {
                wallStrokeUndoFloor = floor;
                wallStrokeUndoBeforeTiles.Clear();
                wallStrokeUndoCells.Clear();
                ClearPendingWallVisualRebuild();
                wallStrokeVisualPreviewInitialized = false;
            }
        }

        private void CaptureWallStrokeUndoBeforeTile(CampusFloorRoot floor, Tilemap wallLogic, Vector3Int cell)
        {
            if (brushMode != CampusRuntimeBrushMode.PaintWall || floor == null || wallLogic == null || wallStrokeUndoFloor != floor)
            {
                return;
            }

            if (wallStrokeUndoBeforeTiles.ContainsKey(cell))
            {
                return;
            }

            wallStrokeUndoBeforeTiles.Add(cell, CaptureTileSnapshot(wallLogic, wallTiles, cell));
            wallStrokeUndoCells.Add(cell);
        }

        private void FinalizePendingWallStrokeUndo()
        {
            using (CampusWallBuildProfiler.FinalizeWallStrokeUndo.Auto())
            {
                FlushPendingWallVisualRebuild();
                if (wallStrokeUndoFloor == null || wallStrokeUndoCells.Count == 0)
                {
                    ClearWallStrokeUndo();
                    return;
                }

                Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(wallStrokeUndoFloor);
                if (wallLogic == null)
                {
                    ClearWallStrokeUndo();
                    return;
                }

                CampusRuntimeWallStrokeUndoEntry entry = new CampusRuntimeWallStrokeUndoEntry();
                entry.FloorIndex = wallStrokeUndoFloor.FloorIndex;
                for (int i = 0; i < wallStrokeUndoCells.Count; i++)
                {
                    Vector3Int cell = wallStrokeUndoCells[i];
                    CampusRuntimeTileSnapshot before = wallStrokeUndoBeforeTiles.TryGetValue(cell, out CampusRuntimeTileSnapshot capturedBefore)
                        ? CloneTileSnapshot(capturedBefore)
                        : null;
                    CampusRuntimeTileSnapshot after = CaptureTileSnapshot(wallLogic, wallTiles, cell);
                    if (AreTileSnapshotsEquivalent(before, after))
                    {
                        continue;
                    }

                    CampusRuntimeWallStrokeCellUndoEntry cellEntry = new CampusRuntimeWallStrokeCellUndoEntry();
                    cellEntry.Cell = cell;
                    cellEntry.Before = before;
                    cellEntry.After = after;
                    entry.Cells.Add(cellEntry);
                }

                if (entry.Cells.Count > 0)
                {
                    AddUndoEntry(WallStrokeUndoPrefix + JsonUtility.ToJson(entry, false));
                }

                ClearWallStrokeUndo();
            }
        }

        private void ClearWallStrokeUndo()
        {
            wallStrokeUndoFloor = null;
            wallStrokeUndoBeforeTiles.Clear();
            wallStrokeUndoCells.Clear();
            wallStrokeVisualPreviewInitialized = false;
            ClearPendingWallVisualRebuild();
        }

        private void PaintFloor(CampusFloorRoot floor, Vector3Int anchorCell)
        {
            TileBase tile = GetSelectedFloorTile();
            if (floor == null || floor.FloorTilemap == null || tile == null)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u7528\u5730\u677f\u5730\u5757\u3002", "No floor tile is available."));
                return;
            }

            PaintTileArea(floor.FloorTilemap, anchorCell, brushSize, tile, BuildTileTransform());
            floor.MarkUsedBoundsDirty();
        }

        private void PaintWall(CampusFloorRoot floor, Vector3Int anchorCell)
        {
            using (CampusWallBuildProfiler.PaintWall.Auto())
            {
            TileBase tile = GetSelectedWallTile();
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null || tile == null)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u7528\u5899\u4f53\u5730\u5757\u3002", "No wall tile is available."));
                return;
            }

            wallVisualRebuildCells.Clear();
            Matrix4x4 transform = BuildTileTransform();
            int radius = Mathf.Max(1, brushSize);
            using (CampusWallBuildProfiler.PaintWallWriteTiles.Auto())
            {
            for (int y = 0; y < radius; y++)
            {
                for (int x = 0; x < radius; x++)
                {
                    Vector3Int cell = new Vector3Int(anchorCell.x + x, anchorCell.y + y, anchorCell.z);
                    CaptureWallStrokeUndoBeforeTile(floor, wallLogic, cell);
                    wallLogic.SetTile(cell, tile);
                    wallLogic.SetTileFlags(cell, TileFlags.None);
                    wallLogic.SetTransformMatrix(cell, transform);
                    wallVisualRebuildCells.Add(cell);
                }
            }
            }

            QueueWallVisualRebuild(floor, wallVisualRebuildCells);
            }
        }

        private static bool IsRectangleDragBrushMode(CampusRuntimeBrushMode mode)
        {
            return mode == CampusRuntimeBrushMode.RectangleFloor ||
                   mode == CampusRuntimeBrushMode.RectangleWall ||
                   mode == CampusRuntimeBrushMode.RectangleErase ||
                   mode == CampusRuntimeBrushMode.RectangleRoom ||
                   mode == CampusRuntimeBrushMode.CreateRoomPrefab;
        }

        private static bool IsGameplayBrushMode(CampusRuntimeBrushMode mode)
        {
            return mode == CampusRuntimeBrushMode.PlaceGameplayMarker ||
                   mode == CampusRuntimeBrushMode.EraseGameplayMarker;
        }

        private void ApplyRectangleBrush(CampusFloorRoot floor, Vector3Int start, Vector3Int end, CampusRuntimeBrushMode mode)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (mode == CampusRuntimeBrushMode.RectangleFloor)
                    {
                        TileBase tile = GetSelectedFloorTile();
                        if (tile != null && floor.FloorTilemap != null)
                        {
                            floor.FloorTilemap.SetTile(cell, tile);
                            floor.FloorTilemap.SetTileFlags(cell, TileFlags.None);
                            floor.FloorTilemap.SetTransformMatrix(cell, BuildTileTransform());
                        }
                    }
                    else if (mode == CampusRuntimeBrushMode.RectangleWall)
                    {
                        TileBase tile = GetSelectedWallTile();
                        Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
                        if (tile != null && wallLogic != null)
                        {
                            wallLogic.SetTile(cell, tile);
                            wallLogic.SetTileFlags(cell, TileFlags.None);
                            wallLogic.SetTransformMatrix(cell, BuildTileTransform());
                        }
                    }
                    else if (mode == CampusRuntimeBrushMode.RectangleRoom)
                    {
                        PlaceRoomMarker(floor, cell, false, false);
                    }
                    else
                    {
                        EraseAtCell(floor, cell, false);
                    }
                }
            }

            if (mode == CampusRuntimeBrushMode.RectangleWall || mode == CampusRuntimeBrushMode.RectangleErase)
            {
                RebuildWallVisuals(floor);
            }

            floor.MarkUsedBoundsDirty();
            if (mode == CampusRuntimeBrushMode.RectangleRoom)
            {
                RebuildGameplayRoomRegistrySafe();
                SchedulePlayerMapSave();
                SetStatus(TrFormat("\u5df2\u6807\u8bb0\u533a\u57df\uff1a{0}", "Marked area: {0}", GetAreaPresetLabel(GetSelectedRoomName())));
            }
        }

        private void PaintTileArea(Tilemap tilemap, Vector3Int anchorCell, int size, TileBase tile, Matrix4x4 transform)
        {
            int radius = Mathf.Max(1, size);
            for (int y = 0; y < radius; y++)
            {
                for (int x = 0; x < radius; x++)
                {
                    Vector3Int cell = new Vector3Int(anchorCell.x + x, anchorCell.y + y, anchorCell.z);
                    tilemap.SetTile(cell, tile);
                    tilemap.SetTileFlags(cell, TileFlags.None);
                    tilemap.SetTransformMatrix(cell, transform);
                }
            }

            tilemap.RefreshAllTiles();
        }

        private void PlaceObject(CampusFloorRoot floor, Vector3Int cell)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (prefab == null || floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u7528\u7269\u4ef6\u9884\u5236\u4f53\u3002", "No object prefab is available."));
                return;
            }

            CampusPlacedObject prefabPlaced = prefab.GetComponent<CampusPlacedObject>();
            Vector2Int footprint = prefabPlaced != null ? prefabPlaced.NormalizedFootprintSize : Vector2Int.one;
            int effectiveRotation90 = prefabPlaced != null ? prefabPlaced.ResolveAllowedRotation90(rotation90) : 0;
            if (prefabPlaced != null && prefabPlaced.IsWallMounted)
            {
                footprint = Vector2Int.one;
                if (!CanPlaceWallMountedObject(floor, cell, effectiveRotation90, out string wallPlacementError))
                {
                    SetStatus(wallPlacementError);
                    return;
                }
            }

            Vector2Int rotatedFootprint = CampusPlacedObject.RotateFootprintSize(footprint, effectiveRotation90);
            EraseObjectsAtCells(floor, cell, rotatedFootprint);

            GameObject instance = Instantiate(prefab, floor.PropsRoot);
            CampusSceneInstanceUtility.NormalizeSceneInstance(instance);
            instance.SetActive(true);
            string displayName = GetObjectDisplayName(prefab);
            instance.name = displayName + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y;
            instance.transform.rotation = Quaternion.identity;
            CampusPlacedObject placed = instance.GetComponent<CampusPlacedObject>();
            if (placed == null)
            {
                placed = instance.AddComponent<CampusPlacedObject>();
            }

            placed.ObjectId = prefab.name;
            placed.TypeId = prefabPlaced != null ? prefabPlaced.TypeId : string.Empty;
            placed.FloorIndex = floor.FloorIndex;
            placed.Cell = cell;
            placed.FootprintSize = footprint;
            placed.ApplyPlacementRotation(effectiveRotation90);
            if (prefabPlaced != null)
            {
                placed.BlocksMovement = prefabPlaced.BlocksMovement;
                placed.BlocksSight = prefabPlaced.BlocksSight;
                placed.IsInteractable = prefabPlaced.IsInteractable;
                placed.IsStorageContainer = prefabPlaced.IsStorageContainer;
                placed.TypeId = prefabPlaced.TypeId;
                placed.StorageSize = prefabPlaced.NormalizedStorageSize;
                placed.StorageMaxWeight = prefabPlaced.NormalizedStorageMaxWeight;
                placed.SortingOrderOffset = prefabPlaced.SortingOrderOffset;
                if (prefabPlaced.IsWallMounted)
                {
                    placed.SortingOrderOffset = Mathf.Max(placed.SortingOrderOffset, 1);
                }
            }

            placed.ApplyCellToTransform(floor.Grid);
            placed.ApplyInteractionState();
            placed.EnsureShadowRegistration();
            CampusDynamicShadowUtility.EnsureObjectShadowCasters(placed, floor.Grid);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            NtingCustomShadowSystem.EnsureSceneSystem().RefreshNow();
            floor.MarkUsedBoundsDirty();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u7269\u4ef6\uff1a{0}", "Placed object: {0}", displayName));
        }

        private bool CanPlaceWallMountedObject(CampusFloorRoot floor, Vector3Int cell, int rotation90Value, out string error)
        {
            error = string.Empty;
            if (floor == null)
            {
                error = Tr("\u627e\u4e0d\u5230\u5f53\u524d\u697c\u5c42\u3002", "Current floor was not found.");
                return false;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                error = Tr("\u5f53\u524d\u697c\u5c42\u6ca1\u6709\u5899\u4f53\u903b\u8f91\u5c42\u3002", "Current floor has no wall logic layer.");
                return false;
            }

            if (!CampusWallTileUtility.HasWall(wallLogic, cell))
            {
                error = Tr("\u58c1\u6302\u7269\u4f53\u53ea\u80fd\u653e\u5728\u5899\u4f53\u683c\u5b50\u4e0a\u3002", "Wall-mounted objects can only be placed on wall cells.");
                return false;
            }

            int exposedMask = CampusWallTileUtility.GetExposedMask(wallLogic, cell);
            int targetFaceMask = Rotation90ToWallMask(rotation90Value);
            if ((exposedMask & targetFaceMask) == 0)
            {
                error = Tr("\u5f53\u524d\u65cb\u8f6c\u65b9\u5411\u6ca1\u6709\u53ef\u5438\u9644\u7684\u5899\u9762\u3002", "No wall face is available for the current rotation.");
                return false;
            }

            return true;
        }

        private static int Rotation90ToWallMask(int rotation90Value)
        {
            switch (CampusPlacedObject.NormalizeRotation90(rotation90Value))
            {
                case 1:
                    return CampusWallTileUtility.EastMask;
                case 2:
                    return CampusWallTileUtility.SouthMask;
                case 3:
                    return CampusWallTileUtility.WestMask;
                default:
                    return CampusWallTileUtility.NorthMask;
            }
        }

        private void PlaceStair(CampusFloorRoot floor, Vector3Int cell)
        {
            if (stairPrefab == null || floor == null || floor.Grid == null || floor.StairsRoot == null)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u7528\u697c\u68af\u9884\u5236\u4f53\u3002", "No stair prefab is available."));
                return;
            }

            int targetFloor = Mathf.Max(1, stairTargetFloorIndex);
            if (targetFloor == floor.FloorIndex)
            {
                targetFloor = floor.FloorIndex + 1;
                stairTargetFloorIndex = targetFloor;
            }

            CampusFloorRoot target = EnsureFloor(targetFloor);
            if (target == null)
            {
                return;
            }

            Vector3Int secondaryCell = cell + CampusStairLink.DirectionFromRotation(rotation90);
            EraseStairsAtCell(floor, cell);
            EraseStairsAtCell(floor, secondaryCell);
            string linkId = Guid.NewGuid().ToString("N");
            CreateStairInstance(stairPrefab, floor, floor.FloorIndex, targetFloor, cell, secondaryCell, secondaryCell, rotation90, linkId, false);

            int returnRotation = (rotation90 + 2) % 4;
            Vector3Int returnSecondary = secondaryCell + CampusStairLink.DirectionFromRotation(returnRotation);
            CreateStairInstance(stairPrefab, target, targetFloor, floor.FloorIndex, secondaryCell, returnSecondary, cell, returnRotation, linkId, true);

            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            CampusRenderSortingUtility.ApplyFloorSorting(target, target.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            floor.MarkUsedBoundsDirty();
            target.MarkUsedBoundsDirty();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u697c\u68af\u8fde\u63a5\uff1aF{0} -> F{1}", "Placed stair link: F{0} -> F{1}", floor.FloorIndex, targetFloor));
        }

        private void CreateStairInstance(GameObject prefab, CampusFloorRoot floor, int fromFloor, int toFloor, Vector3Int fromCell, Vector3Int secondaryCell, Vector3Int toCell, int stairRotation90, string linkId, bool isAutoReturn)
        {
            GameObject instance = Instantiate(prefab, floor.StairsRoot);
            CampusSceneInstanceUtility.NormalizeSceneInstance(instance);
            instance.name = CampusObjectNames.GetDisplayName(prefab.name) + "_F" + fromFloor + "_To_F" + toFloor + "_" + fromCell.x + "_" + fromCell.y;
            instance.transform.position = GetStairWorldCenter(floor.Grid, fromCell, secondaryCell);
            instance.transform.rotation = Quaternion.Euler(0f, 0f, stairRotation90 * 90f);
            CampusStairLink link = instance.GetComponent<CampusStairLink>();
            if (link == null)
            {
                link = instance.AddComponent<CampusStairLink>();
            }

            link.FromFloor = fromFloor;
            link.ToFloor = toFloor;
            link.FromCell = fromCell;
            link.SecondaryCell = secondaryCell;
            link.ToCell = toCell;
            link.Rotation90 = stairRotation90;
            link.FootprintLength = 2;
            link.LinkId = linkId;
            link.IsAutoReturnStair = isAutoReturn;
            link.AutoUnlockTargetFloor = true;
            EnsureTriggerCollider(instance, new Vector2(0.8f, 1.8f));
            CampusDynamicShadowUtility.EnsureRendererShadowCasters(instance);
        }

        private void PlaceRoomMarker(CampusFloorRoot floor, Vector3Int cell, bool showStatus = true, bool rebuildGameplayRooms = true)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return;
            }

            string roomName = GetSelectedRoomName();
            if (string.IsNullOrEmpty(roomName))
            {
                SetStatus(Tr("\u8bf7\u5148\u5728\u533a\u57df\u9875\u9009\u62e9\u4e00\u4e2a\u9884\u8bbe\u533a\u57df\u3002", "Select a preset area in the Areas tab first."));
                return;
            }

            EraseRoomMarkersAtCell(floor, cell);

            GameObject markerObject = new GameObject("Room_" + roomName + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
            markerObject.transform.SetParent(floor.PropsRoot, false);
            markerObject.transform.position = floor.Grid.GetCellCenterWorld(cell);
            CampusRuntimeRoomMarker marker = markerObject.AddComponent<CampusRuntimeRoomMarker>();
            marker.RoomName = roomName;
            marker.FloorIndex = floor.FloorIndex;
            marker.Cell = cell;
            marker.HideMarkerVisual = false;
            AddRoomMarkerVisual(markerObject, floor);
            floor.MarkUsedBoundsDirty();
            SchedulePlayerMapSave();
            if (rebuildGameplayRooms)
            {
                RebuildGameplayRoomRegistrySafe();
            }

            if (showStatus)
            {
                SetStatus(TrFormat("\u5df2\u653e\u7f6e\u533a\u57df\u6807\u8bb0\uff1a{0}", "Placed area marker: {0}", GetAreaPresetLabel(roomName)));
            }
        }

        private void PlaceGameplayMarker(CampusFloorRoot floor, Vector3Int cell)
        {
            CampusRuntimeGameplayMarkerPreset preset = GetSelectedGameplayPreset();
            if (preset == null)
            {
                SetStatus(Tr("\u8bf7\u5148\u9009\u62e9\u8bbe\u65bd\u70b9\u3002", "Select a facility point first."));
                return;
            }

            if (preset.UsesInteractionSpot)
            {
                PlaceGameplayInteractionFacilityPoint(floor, cell, preset);
                return;
            }

            PlaceGameplayFacilityMarker(floor, cell, preset);
        }

        private void PlaceGameplayFacilityMarker(
            CampusFloorRoot floor,
            Vector3Int cell,
            CampusRuntimeGameplayMarkerPreset preset)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null || preset == null)
            {
                return;
            }

            EraseGameplayMarkersAtCell(floor, cell, false, true, false, false);
            GameObject markerObject = CreateGameplayMarkerObject(
                floor,
                cell,
                "GameplayFacility_" + preset.FacilityType + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
            if (markerObject == null)
            {
                return;
            }

            CampusGameplayFacilityMarker marker = markerObject.AddComponent<CampusGameplayFacilityMarker>();
            marker.Configure(
                GetGameplayPresetDisplayName(preset),
                preset.FacilityType,
                floor.FloorIndex,
                NormalizeCell(cell),
                true,
                null);

            floor.MarkUsedBoundsDirty();
            RebuildGameplayRoomRegistrySafe();
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u8bbe\u65bd\u70b9\uff1a{0}", "Placed facility point: {0}", GetGameplayPresetLabel(preset)));
        }

        private void PlaceGameplayInteractionFacilityPoint(
            CampusFloorRoot floor,
            Vector3Int cell,
            CampusRuntimeGameplayMarkerPreset preset)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null || preset == null)
            {
                return;
            }

            EraseGameplayMarkersAtCell(floor, cell, false, false, true, false);
            GameObject markerObject = CreateGameplayMarkerObject(
                floor,
                cell,
                "GameplayPrank_" + SanitizeFileName(preset.EnglishLabel) + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
            if (markerObject == null)
            {
                return;
            }

            CampusPrankInteractionSpot spot = markerObject.AddComponent<CampusPrankInteractionSpot>();
            spot.Configure(
                GetGameplayPresetDisplayName(preset),
                preset.PrankPayload,
                preset.RoomType,
                preset.VisualKind,
                0.95f,
                preset.Color,
                "This prank is unavailable here.");

            floor.MarkUsedBoundsDirty();
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u8bbe\u65bd\u70b9\uff1a{0}", "Placed facility point: {0}", GetGameplayPresetLabel(preset)));
        }

        private GameObject CreateGameplayMarkerObject(CampusFloorRoot floor, Vector3Int cell, string objectName)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return null;
            }

            Vector3Int normalizedCell = NormalizeCell(cell);
            GameObject markerObject = new GameObject(objectName);
            markerObject.transform.SetParent(floor.PropsRoot, false);
            markerObject.transform.position = floor.Grid.GetCellCenterWorld(normalizedCell);

            CampusRuntimeGameplayOverlayEntity entity =
                markerObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
            entity.Configure(false, floor.FloorIndex, normalizedCell);
            return markerObject;
        }

        private bool EraseGameplayMarkersAtCell(
            CampusFloorRoot floor,
            Vector3Int cell,
            bool eraseRooms,
            bool eraseFacilities,
            bool erasePranks,
            bool showStatus)
        {
            if (floor == null)
            {
                return false;
            }

            Vector3Int normalizedCell = NormalizeCell(cell);
            bool erased = false;

            if (eraseRooms)
            {
                CampusGameplayRoomMarker[] roomMarkers =
                    FindObjectsByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = roomMarkers.Length - 1; i >= 0; i--)
                {
                    CampusGameplayRoomMarker marker = roomMarkers[i];
                    if (marker == null || marker.FloorIndex != floor.FloorIndex)
                    {
                        continue;
                    }

                    if (CellInBounds(marker.BuildBounds(), normalizedCell))
                    {
                        DestroyRuntimeObject(marker.gameObject);
                        erased = true;
                    }
                }
            }

            if (eraseFacilities)
            {
                CampusGameplayFacilityMarker[] facilityMarkers =
                    FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = facilityMarkers.Length - 1; i >= 0; i--)
                {
                    CampusGameplayFacilityMarker marker = facilityMarkers[i];
                    if (marker != null &&
                        marker.FloorIndex == floor.FloorIndex &&
                        NormalizeCell(marker.Cell) == normalizedCell)
                    {
                        DestroyRuntimeObject(marker.gameObject);
                        erased = true;
                    }
                }
            }

            if (erasePranks)
            {
                CampusPrankInteractionSpot[] prankSpots =
                    FindObjectsByType<CampusPrankInteractionSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = prankSpots.Length - 1; i >= 0; i--)
                {
                    CampusPrankInteractionSpot spot = prankSpots[i];
                    if (spot == null || !TryResolveGameplayMarkerCell(spot, out int floorIndex, out Vector3Int spotCell))
                    {
                        continue;
                    }

                    if (floorIndex == floor.FloorIndex && NormalizeCell(spotCell) == normalizedCell)
                    {
                        DestroyRuntimeObject(spot.gameObject);
                        erased = true;
                    }
                }
            }

            if (erased)
            {
                floor.MarkUsedBoundsDirty();
                RebuildGameplayRoomRegistrySafe();
                SchedulePlayerMapSave();
                if (showStatus)
                {
                    SetStatus(Tr("\u5df2\u5220\u9664\u8bbe\u65bd\u70b9\u3002", "Erased facility point."));
                }
            }
            else if (showStatus)
            {
                SetStatus(Tr("\u5f53\u524d\u683c\u5b50\u6ca1\u6709\u8bbe\u65bd\u70b9\u3002", "No facility point at the current cell."));
            }

            return erased;
        }

        private bool EraseGameplayMarkersInBounds(CampusFloorRoot floor, BoundsInt bounds)
        {
            if (floor == null)
            {
                return false;
            }

            bool erased = false;
            CampusGameplayRoomMarker[] roomMarkers =
                FindObjectsByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = roomMarkers.Length - 1; i >= 0; i--)
            {
                CampusGameplayRoomMarker marker = roomMarkers[i];
                if (marker != null &&
                    marker.FloorIndex == floor.FloorIndex &&
                    BoundsOverlap2D(marker.BuildBounds(), bounds))
                {
                    DestroyRuntimeObject(marker.gameObject);
                    erased = true;
                }
            }

            CampusGameplayFacilityMarker[] facilityMarkers =
                FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = facilityMarkers.Length - 1; i >= 0; i--)
            {
                CampusGameplayFacilityMarker marker = facilityMarkers[i];
                if (marker != null &&
                    marker.FloorIndex == floor.FloorIndex &&
                    CellInBounds(bounds, NormalizeCell(marker.Cell)))
                {
                    DestroyRuntimeObject(marker.gameObject);
                    erased = true;
                }
            }

            CampusPrankInteractionSpot[] prankSpots =
                FindObjectsByType<CampusPrankInteractionSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = prankSpots.Length - 1; i >= 0; i--)
            {
                CampusPrankInteractionSpot spot = prankSpots[i];
                if (spot == null || !TryResolveGameplayMarkerCell(spot, out int floorIndex, out Vector3Int cell))
                {
                    continue;
                }

                if (floorIndex == floor.FloorIndex && CellInBounds(bounds, NormalizeCell(cell)))
                {
                    DestroyRuntimeObject(spot.gameObject);
                    erased = true;
                }
            }

            if (erased)
            {
                RebuildGameplayRoomRegistrySafe();
            }

            return erased;
        }

        private void CreateRoomPrefabFromSelection(CampusFloorRoot floor, Vector3Int start, Vector3Int end)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            string roomName = ResolveNewRoomPrefabName();
            if (string.IsNullOrWhiteSpace(roomName))
            {
                SetStatus(Tr("\u8bf7\u5148\u8f93\u5165\u623f\u95f4\u6a21\u5757\u540d\u3002", "Enter a room module name first."));
                return;
            }

            BoundsInt bounds = BuildInclusiveCellBounds(start, end);
            CampusRuntimeRoomPrefab roomPrefab = CaptureRoomPrefab(floor, bounds, roomName.Trim());
            if (!HasRoomPrefabContent(roomPrefab))
            {
                SetStatus(Tr("\u9009\u4e2d\u533a\u57df\u6ca1\u6709\u5730\u677f\u3001\u5899\u4f53\u3001\u7269\u4ef6\u3001\u706f\u5149\u3001\u533a\u57df\u6216\u70b9\u4f4d\u5185\u5bb9\uff0c\u65e0\u6cd5\u4fdd\u5b58\u4e3a\u6a21\u5757\u3002", "The selected area has no floor, wall, object, light, area, or point content to save as a module."));
                return;
            }

            SaveRuntimeRoomPrefab(roomPrefab);
            LoadImportedRoomPrefabs();
            SelectRoomPrefabByName(roomPrefab.RoomName);
            newRoomPrefabName = string.Empty;
            brushMode = CampusRuntimeBrushMode.PlaceRoomPrefab;
            SchedulePlayerMapSave();
            SavePlayerMap(false);
            SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u623f\u95f4\u6a21\u5757\uff1a{0} ({1}x{2})\u3002", "Saved room module: {0} ({1}x{2}).", roomPrefab.RoomName, roomPrefab.Size.x, roomPrefab.Size.y));
        }

        private CampusRuntimeRoomPrefab CaptureRoomPrefab(CampusFloorRoot floor, BoundsInt bounds, string roomName)
        {
            CampusRuntimeRoomPrefab roomPrefab = new CampusRuntimeRoomPrefab();
            roomPrefab.Schema = "NtingCampusRuntimeRoomPrefab.v1";
            roomPrefab.RoomName = roomName;
            roomPrefab.CreatedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            roomPrefab.Size = new Vector2Int(Mathf.Max(1, bounds.size.x), Mathf.Max(1, bounds.size.y));
            Vector3Int originCell = new Vector3Int(bounds.xMin, bounds.yMin, 0);

            CaptureRoomTiles(floor.FloorTilemap, floorTiles, bounds, originCell, roomPrefab.FloorTiles);
            CaptureRoomTiles(CampusWallTileUtility.GetWallLogicTilemap(floor), wallTiles, bounds, originCell, roomPrefab.WallTiles);
            CaptureRoomObjects(floor, bounds, originCell, roomPrefab.Objects);
            CaptureRoomMarkers(floor, bounds, originCell, roomPrefab.RoomMarkers);
            CaptureRoomGameplayRooms(floor, bounds, originCell, roomPrefab.GameplayRooms);
            CaptureRoomGameplayFacilities(floor, bounds, originCell, roomPrefab.GameplayFacilities);
            CaptureRoomGameplayPrankSpots(floor, bounds, originCell, roomPrefab.GameplayPrankSpots);
            CaptureRoomLights(floor, bounds, originCell, roomPrefab.Lights);
            return roomPrefab;
        }

        private void CaptureRoomTiles(Tilemap tilemap, List<TileBase> palette, BoundsInt bounds, Vector3Int originCell, List<CampusRuntimeTileSnapshot> output)
        {
            output.Clear();
            if (tilemap == null)
            {
                return;
            }

            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    TileBase tile = tilemap.GetTile(cell);
                    if (tile == null)
                    {
                        continue;
                    }

                    CampusRuntimeTileSnapshot tileSnapshot = new CampusRuntimeTileSnapshot();
                    tileSnapshot.Cell = ToRelativeCell(cell, originCell);
                    tileSnapshot.AssetName = tile.name;
                    tileSnapshot.PaletteIndex = palette.IndexOf(tile);
                    tileSnapshot.Transform = tilemap.GetTransformMatrix(cell);
                    output.Add(tileSnapshot);
                }
            }
        }

        private void CaptureRoomObjects(CampusFloorRoot floor, BoundsInt bounds, Vector3Int originCell, List<CampusRuntimeObjectSnapshot> output)
        {
            output.Clear();
            if (floor == null || floor.PropsRoot == null || floor.Grid == null)
            {
                return;
            }

            Vector3 originWorld = floor.Grid.CellToWorld(originCell);
            CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                CampusPlacedObject placed = objects[i];
                if (placed == null)
                {
                    continue;
                }

                placed.RefreshCellFromTransform(floor.Grid);
                if (!IsPlacedObjectFullyInsideBounds(placed, bounds))
                {
                    continue;
                }

                CampusRuntimeObjectSnapshot objectSnapshot = CreateObjectSnapshot(floor, placed);
                objectSnapshot.Cell = ToRelativeCell(objectSnapshot.Cell, originCell);
                objectSnapshot.FloorIndex = 0;
                objectSnapshot.Position = placed.transform.position - originWorld;
                output.Add(objectSnapshot);
            }
        }

        private void CaptureRoomMarkers(CampusFloorRoot floor, BoundsInt bounds, Vector3Int originCell, List<CampusRuntimeRoomSnapshot> output)
        {
            output.Clear();
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex || !CellInBounds(bounds, marker.Cell))
                {
                    continue;
                }

                if (!TryResolvePresetRoomName(marker.RoomName, out string roomName))
                {
                    continue;
                }

                CampusRuntimeRoomSnapshot roomSnapshot = new CampusRuntimeRoomSnapshot();
                roomSnapshot.RoomName = roomName;
                roomSnapshot.FloorIndex = 0;
                roomSnapshot.Cell = ToRelativeCell(marker.Cell, originCell);
                output.Add(roomSnapshot);
            }
        }

        private void CaptureRoomGameplayRooms(
            CampusFloorRoot floor,
            BoundsInt bounds,
            Vector3Int originCell,
            List<CampusRuntimeGameplayRoomSnapshot> output)
        {
            output.Clear();
            if (floor == null)
            {
                return;
            }

            CampusGameplayRoomMarker[] markers =
                FindObjectsByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayRoomMarker marker = markers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                BoundsInt markerBounds = marker.BuildBounds();
                if (!BoundsContains2D(bounds, markerBounds))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayRoomSnapshot
                {
                    Id = marker.RoomIdOverride,
                    DisplayName = marker.RoomDisplayName,
                    RoomType = marker.RoomType,
                    FloorIndex = 0,
                    AnchorCell = ToRelativeCell(NormalizeCell(marker.AnchorCell), originCell),
                    Size = marker.RoomSize,
                    UsableForGameplay = marker.UsableForGameplay
                });
            }
        }

        private void CaptureRoomGameplayFacilities(
            CampusFloorRoot floor,
            BoundsInt bounds,
            Vector3Int originCell,
            List<CampusRuntimeGameplayFacilitySnapshot> output)
        {
            output.Clear();
            if (floor == null)
            {
                return;
            }

            CampusGameplayFacilityMarker[] markers =
                FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = markers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Vector3Int cell = NormalizeCell(marker.Cell);
                if (!CellInBounds(bounds, cell))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayFacilitySnapshot
                {
                    DisplayName = marker.DisplayName,
                    FacilityType = marker.FacilityType,
                    FloorIndex = 0,
                    Cell = ToRelativeCell(cell, originCell),
                    CountsAsCoreFacility = marker.CountsAsCoreFacility
                });
            }
        }

        private void CaptureRoomGameplayPrankSpots(
            CampusFloorRoot floor,
            BoundsInt bounds,
            Vector3Int originCell,
            List<CampusRuntimeGameplayPrankSpotSnapshot> output)
        {
            output.Clear();
            if (floor == null)
            {
                return;
            }

            CampusPrankInteractionSpot[] spots =
                FindObjectsByType<CampusPrankInteractionSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < spots.Length; i++)
            {
                CampusPrankInteractionSpot spot = spots[i];
                if (spot == null || !TryResolveGameplayMarkerCell(spot, out int floorIndex, out Vector3Int cell))
                {
                    continue;
                }

                cell = NormalizeCell(cell);
                if (floorIndex != floor.FloorIndex || !CellInBounds(bounds, cell))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayPrankSpotSnapshot
                {
                    DisplayName = spot.DisplayName,
                    Payload = spot.PrankPayload,
                    RequiredRoomType = spot.RequiredRoomType,
                    VisualKind = spot.VisualKind,
                    FloorIndex = 0,
                    Cell = ToRelativeCell(cell, originCell),
                    InteractionRadius = spot.InteractionRadius,
                    AccentColor = spot.AccentColor,
                    UnsupportedReason = spot.UnsupportedReason
                });
            }
        }

        private void CaptureRoomLights(CampusFloorRoot floor, BoundsInt bounds, Vector3Int originCell, List<CampusRuntimeRoomLightSnapshot> output)
        {
            output.Clear();
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            Vector3 originWorld = floor.Grid.CellToWorld(originCell);
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (!IsRuntimeEditableLight(light))
                {
                    continue;
                }

                CampusRuntimeLightSnapshot lightSnapshot = CreateLightSnapshot(light);
                if (lightSnapshot.FloorIndex != floor.FloorIndex || !CellInBounds(bounds, lightSnapshot.Cell))
                {
                    continue;
                }

                CampusRuntimeRoomLightSnapshot roomLight = new CampusRuntimeRoomLightSnapshot();
                roomLight.Light = lightSnapshot;
                roomLight.Light.FloorIndex = 0;
                roomLight.Light.Cell = ToRelativeCell(lightSnapshot.Cell, originCell);
                roomLight.RelativeCell = roomLight.Light.Cell;
                roomLight.RelativePosition = light.transform.position - originWorld;
                roomLight.HasRelativePosition = true;
                output.Add(roomLight);
            }
        }

        private void PlaceRoomPrefab(CampusFloorRoot floor, Vector3Int anchorCell)
        {
            CampusRuntimeRoomPrefab roomPrefab = GetSelectedRoomPrefab();
            if (roomPrefab == null)
            {
                SetStatus(Tr("\u8bf7\u5148\u9009\u62e9\u623f\u95f4\u6a21\u5757\uff0c\u6216\u7528\u6846\u9009\u6a21\u5757\u521b\u5efa\u4e00\u4e2a\u3002", "Select a room module first, or create one with Box Module."));
                return;
            }

            if (floor == null || floor.Grid == null)
            {
                return;
            }

            EraseRoomPrefabArea(floor, anchorCell, roomPrefab.Size);
            ApplyRoomPrefabTiles(floor.FloorTilemap, roomPrefab.FloorTiles, floorTiles, anchorCell);
            ApplyRoomPrefabTiles(CampusWallTileUtility.GetWallLogicTilemap(floor), roomPrefab.WallTiles, wallTiles, anchorCell);
            ApplyRoomPrefabObjects(floor, roomPrefab.Objects, anchorCell);
            ApplyRoomPrefabMarkers(floor, roomPrefab.RoomMarkers, anchorCell, roomPrefab.RoomName);
            ApplyRoomPrefabGameplayMarkers(floor, roomPrefab, anchorCell);
            ApplyRoomPrefabLights(floor, roomPrefab.Lights, anchorCell);
            AddRoomDefinitionsFromRoomPrefab(roomPrefab);
            RebuildWallVisuals(floor);
            floor.MarkUsedBoundsDirty();
            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u623f\u95f4\u6a21\u5757\uff1a{0}", "Placed room module: {0}", roomPrefab.RoomName));
        }

        private void EraseRoomPrefabArea(CampusFloorRoot floor, Vector3Int anchorCell, Vector2Int size)
        {
            size = NormalizeRoomPrefabSize(size);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    EraseAtCell(floor, new Vector3Int(anchorCell.x + x, anchorCell.y + y, 0), false);
                }
            }

            EraseGameplayMarkersInBounds(
                floor,
                new BoundsInt(anchorCell, new Vector3Int(size.x, size.y, 1)));
        }

        private void ApplyRoomPrefabTiles(Tilemap tilemap, List<CampusRuntimeTileSnapshot> tiles, List<TileBase> palette, Vector3Int anchorCell)
        {
            if (tilemap == null || tiles == null)
            {
                return;
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                CampusRuntimeTileSnapshot tileSnapshot = tiles[i];
                TileBase tile = ResolveTile(tileSnapshot, palette);
                if (tile == null)
                {
                    continue;
                }

                Vector3Int cell = ToAbsoluteCell(anchorCell, tileSnapshot.Cell);
                tilemap.SetTile(cell, tile);
                tilemap.SetTileFlags(cell, TileFlags.None);
                tilemap.SetTransformMatrix(cell, HasUsableMatrix(tileSnapshot.Transform) ? tileSnapshot.Transform : Matrix4x4.identity);
            }

            tilemap.RefreshAllTiles();
        }

        private void ApplyRoomPrefabObjects(CampusFloorRoot floor, List<CampusRuntimeObjectSnapshot> objects, Vector3Int anchorCell)
        {
            if (objects == null || objects.Count == 0)
            {
                return;
            }

            List<CampusRuntimeObjectSnapshot> shiftedObjects = new List<CampusRuntimeObjectSnapshot>(objects.Count);
            for (int i = 0; i < objects.Count; i++)
            {
                CampusRuntimeObjectSnapshot shifted = CloneObjectSnapshot(objects[i]);
                shifted.Cell = ToAbsoluteCell(anchorCell, objects[i].Cell);
                shifted.FloorIndex = floor != null ? floor.FloorIndex : 1;
                shiftedObjects.Add(shifted);
            }

            ApplyObjects(floor, shiftedObjects);
        }

        private void ApplyRoomPrefabMarkers(CampusFloorRoot floor, List<CampusRuntimeRoomSnapshot> markers, Vector3Int anchorCell, string fallbackRoomName)
        {
            List<CampusRuntimeRoomSnapshot> shiftedMarkers = new List<CampusRuntimeRoomSnapshot>();
            if (markers != null)
            {
                for (int i = 0; i < markers.Count; i++)
                {
                    CampusRuntimeRoomSnapshot marker = markers[i];
                    if (marker == null)
                    {
                        continue;
                    }

                    string sourceRoomName = string.IsNullOrWhiteSpace(marker.RoomName) ? fallbackRoomName : marker.RoomName;
                    if (!TryResolvePresetRoomName(sourceRoomName, out string roomName))
                    {
                        continue;
                    }

                    CampusRuntimeRoomSnapshot shifted = new CampusRuntimeRoomSnapshot();
                    shifted.RoomName = roomName;
                    shifted.FloorIndex = floor != null ? floor.FloorIndex : 1;
                    shifted.Cell = ToAbsoluteCell(anchorCell, marker.Cell);
                    shifted.HideMarkerVisual = true;
                    shiftedMarkers.Add(shifted);
                }
            }

            if (shiftedMarkers.Count > 0)
            {
                ApplyRooms(floor, shiftedMarkers);
            }
        }

        private void ApplyRoomPrefabGameplayMarkers(CampusFloorRoot floor, CampusRuntimeRoomPrefab roomPrefab, Vector3Int anchorCell)
        {
            if (floor == null || roomPrefab == null)
            {
                return;
            }

            if (roomPrefab.GameplayRooms != null && roomPrefab.GameplayRooms.Count > 0)
            {
                List<CampusRuntimeGameplayRoomSnapshot> shiftedRooms =
                    new List<CampusRuntimeGameplayRoomSnapshot>(roomPrefab.GameplayRooms.Count);
                for (int i = 0; i < roomPrefab.GameplayRooms.Count; i++)
                {
                    CampusRuntimeGameplayRoomSnapshot source = roomPrefab.GameplayRooms[i];
                    if (source == null)
                    {
                        continue;
                    }

                    shiftedRooms.Add(new CampusRuntimeGameplayRoomSnapshot
                    {
                        Id = string.IsNullOrWhiteSpace(source.Id)
                            ? string.Empty
                            : source.Id.Trim() + "_F" + floor.FloorIndex + "_" + anchorCell.x + "_" + anchorCell.y,
                        DisplayName = source.DisplayName,
                        RoomType = source.RoomType,
                        FloorIndex = floor.FloorIndex,
                        AnchorCell = ToAbsoluteCell(anchorCell, source.AnchorCell),
                        Size = source.Size,
                        UsableForGameplay = source.UsableForGameplay
                    });
                }

                SpawnGameplayRooms(shiftedRooms);
            }

            if (roomPrefab.GameplayFacilities != null && roomPrefab.GameplayFacilities.Count > 0)
            {
                List<CampusRuntimeGameplayFacilitySnapshot> shiftedFacilities =
                    new List<CampusRuntimeGameplayFacilitySnapshot>(roomPrefab.GameplayFacilities.Count);
                for (int i = 0; i < roomPrefab.GameplayFacilities.Count; i++)
                {
                    CampusRuntimeGameplayFacilitySnapshot source = roomPrefab.GameplayFacilities[i];
                    if (source == null)
                    {
                        continue;
                    }

                    shiftedFacilities.Add(new CampusRuntimeGameplayFacilitySnapshot
                    {
                        DisplayName = source.DisplayName,
                        FacilityType = source.FacilityType,
                        FloorIndex = floor.FloorIndex,
                        Cell = ToAbsoluteCell(anchorCell, source.Cell),
                        CountsAsCoreFacility = source.CountsAsCoreFacility
                    });
                }

                SpawnGameplayFacilities(shiftedFacilities);
            }

            if (roomPrefab.GameplayPrankSpots != null && roomPrefab.GameplayPrankSpots.Count > 0)
            {
                List<CampusRuntimeGameplayPrankSpotSnapshot> shiftedSpots =
                    new List<CampusRuntimeGameplayPrankSpotSnapshot>(roomPrefab.GameplayPrankSpots.Count);
                for (int i = 0; i < roomPrefab.GameplayPrankSpots.Count; i++)
                {
                    CampusRuntimeGameplayPrankSpotSnapshot source = roomPrefab.GameplayPrankSpots[i];
                    if (source == null)
                    {
                        continue;
                    }

                    shiftedSpots.Add(new CampusRuntimeGameplayPrankSpotSnapshot
                    {
                        DisplayName = source.DisplayName,
                        Payload = source.Payload,
                        RequiredRoomType = source.RequiredRoomType,
                        VisualKind = source.VisualKind,
                        FloorIndex = floor.FloorIndex,
                        Cell = ToAbsoluteCell(anchorCell, source.Cell),
                        InteractionRadius = source.InteractionRadius,
                        AccentColor = source.AccentColor,
                        UnsupportedReason = source.UnsupportedReason
                    });
                }

                SpawnGameplayPrankSpots(shiftedSpots);
            }

            RebuildGameplayRoomRegistrySafe();
        }

        private void AddRoomDefinitionsFromRoomPrefab(CampusRuntimeRoomPrefab roomPrefab)
        {
            if (roomPrefab == null)
            {
                return;
            }

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (roomPrefab.RoomMarkers != null)
            {
                for (int i = 0; i < roomPrefab.RoomMarkers.Count; i++)
                {
                    CampusRuntimeRoomSnapshot marker = roomPrefab.RoomMarkers[i];
                    if (marker != null && !string.IsNullOrWhiteSpace(marker.RoomName))
                    {
                        names.Add(marker.RoomName.Trim());
                    }
                }
            }

            foreach (string name in names)
            {
                AddOrUpdateRoomDefinition(name, Mathf.Max(1, newRoomRequiredCount));
            }
        }

        private void ApplyRoomPrefabLights(CampusFloorRoot floor, List<CampusRuntimeRoomLightSnapshot> lights, Vector3Int anchorCell)
        {
            if (floor == null || floor.Grid == null || lights == null)
            {
                return;
            }

            Vector3 originWorld = floor.Grid.CellToWorld(anchorCell);
            for (int i = 0; i < lights.Count; i++)
            {
                CampusRuntimeRoomLightSnapshot roomLight = lights[i];
                if (roomLight == null || roomLight.Light == null)
                {
                    continue;
                }

                CampusRuntimeLightSnapshot shifted = CloneLightSnapshot(roomLight.Light);
                shifted.FloorIndex = floor.FloorIndex;
                Vector3Int relativeCell = roomLight.RelativeCell;
                if (relativeCell == Vector3Int.zero && roomLight.Light.Cell != Vector3Int.zero)
                {
                    relativeCell = roomLight.Light.Cell;
                }

                shifted.Cell = ToAbsoluteCell(anchorCell, relativeCell);
                shifted.Position = roomLight.HasRelativePosition
                    ? originWorld + roomLight.RelativePosition
                    : floor.Grid.GetCellCenterWorld(shifted.Cell);
                CreateLightInstance(shifted);
            }
        }

        private void PlaceLight(CampusFloorRoot floor, Vector3Int cell, Vector3 mouseWorld)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }
            lightBrushType = Light2D.LightType.Point;

            Vector3 position = floor.Grid.GetCellCenterWorld(cell);
            if (IsKeyHeld(KeyCode.LeftAlt) || IsKeyHeld(KeyCode.RightAlt))
            {
                position = mouseWorld;
            }

            position.z = floor.Grid.transform.position.z;
            GameObject lightObject = new GameObject("RuntimeLight_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
            lightObject.transform.position = position;
            lightObject.transform.rotation = Quaternion.Euler(0f, 0f, rotation90 * 90f);
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = lightBrushType;
            light.blendStyleIndex = 0;
            light.targetSortingLayers = GetAllSortingLayerIds();
            light.color = lightColor;
            light.intensity = lightIntensity;
            CampusDynamicShadowUtility.ConfigureLightShadows(light, lightShadowsEnabled, lightShadowIntensity, lightShadowSoftness, lightShadowSoftnessFalloff);
            if (lightBrushType == Light2D.LightType.Point)
            {
                light.pointLightInnerAngle = 360f;
                light.pointLightOuterAngle = 360f;
                light.pointLightInnerRadius = Mathf.Max(0f, lightInnerRadius);
                light.pointLightOuterRadius = Mathf.Max(light.pointLightInnerRadius + 0.1f, lightOuterRadius);
                light.falloffIntensity = 0.18f;
            }

            selectedLight = light;
            SchedulePlayerMapSave();
            SetStatus(Tr("\u5df2\u653e\u7f6e\u706f\u5149\u3002", "Placed light."));
        }

        private void EraseAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            EraseAtCell(floor, cell, true);
        }

        private void EraseAtCell(CampusFloorRoot floor, Vector3Int cell, bool rebuildWalls)
        {
            if (floor == null)
            {
                return;
            }

            if (floor.FloorTilemap != null)
            {
                floor.FloorTilemap.SetTile(cell, null);
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic != null)
            {
                wallLogic.SetTile(cell, null);
            }

            EraseObjectsAtCell(floor, cell);
            EraseStairsAtCell(floor, cell);
            bool erasedRoomMarker = EraseRoomMarkersAtCell(floor, cell);
            EraseLightsAtCell(floor, cell);

            if (rebuildWalls)
            {
                wallVisualRebuildCells.Clear();
                wallVisualRebuildCells.Add(cell);
                RebuildWallVisuals(floor, wallVisualRebuildCells);
                floor.MarkUsedBoundsDirty();
            }

            if (erasedRoomMarker)
            {
                SchedulePlayerMapSave();
                RebuildGameplayRoomRegistrySafe();
            }
        }

        private void EraseObjectsAtCells(CampusFloorRoot floor, Vector3Int anchorCell, Vector2Int size)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    EraseObjectsAtCell(floor, new Vector3Int(anchorCell.x + x, anchorCell.y + y, anchorCell.z));
                }
            }
        }

        private void EraseObjectsAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                CampusPlacedObject placed = objects[i];
                if (placed != null && placed.ContainsCell(cell))
                {
                    DestroyRuntimeObject(placed.gameObject);
                }
            }
        }

        private bool EraseRoomMarkersAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return false;
            }

            bool erased = false;
            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker != null && marker.Cell == cell && marker.FloorIndex == floor.FloorIndex)
                {
                    DestroyRuntimeObject(marker.gameObject);
                    erased = true;
                }
            }

            return erased;
        }

        private void EraseStairsAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.StairsRoot == null)
            {
                return;
            }

            CampusStairLink[] stairs = floor.StairsRoot.GetComponentsInChildren<CampusStairLink>(true);
            for (int i = stairs.Length - 1; i >= 0; i--)
            {
                CampusStairLink stair = stairs[i];
                if (stair != null && stair.ContainsCell(cell))
                {
                    DestroyRuntimeObject(stair.gameObject);
                }
            }
        }

        private void EraseLightsAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = lights.Length - 1; i >= 0; i--)
            {
                Light2D light = lights[i];
                if (!IsRuntimeEditableLight(light))
                {
                    continue;
                }

                Vector3Int lightCell = floor.Grid.WorldToCell(light.transform.position);
                lightCell.z = 0;
                if (lightCell == cell)
                {
                    if (selectedLight == light)
                    {
                        selectedLight = null;
                    }

                    DestroyRuntimeObject(light.gameObject);
                }
            }
        }

        private Light2D FindLightAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.Grid == null)
            {
                return null;
            }

            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (!IsRuntimeEditableLight(light))
                {
                    continue;
                }

                Vector3Int lightCell = floor.Grid.WorldToCell(light.transform.position);
                lightCell.z = 0;
                if (lightCell == cell)
                {
                    return light;
                }
            }

            return null;
        }

        private void SyncShadowFieldsFromSelectedLight()
        {
            if (selectedLight == null)
            {
                return;
            }

            lightShadowsEnabled = selectedLight.shadowsEnabled;
            lightShadowIntensity = Mathf.Clamp01(selectedLight.shadowIntensity);
            lightShadowSoftness = Mathf.Clamp01(selectedLight.shadowSoftness);
            lightShadowSoftnessFalloff = Mathf.Clamp01(selectedLight.shadowSoftnessFalloffIntensity);
        }

        private void PickAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            Light2D lightAtCell = FindLightAtCell(floor, cell);
            if (lightAtCell != null)
            {
                selectedLight = lightAtCell;
                activeTab = CampusRuntimeEditorTab.Lighting;
                brushMode = CampusRuntimeBrushMode.PlaceLight;
                lightIntensity = Mathf.Max(0f, selectedLight.intensity);
                lightInnerRadius = Mathf.Max(0f, selectedLight.pointLightInnerRadius);
                lightOuterRadius = Mathf.Max(0.2f, selectedLight.pointLightOuterRadius);
                SyncShadowFieldsFromSelectedLight();
                SetStatus(TrFormat("\u5df2\u9009\u4e2d\u706f\u5149\uff1a{0}", "Selected light: {0}", selectedLight.gameObject.name));
                return;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic != null)
            {
                TileBase wallTile = wallLogic.GetTile(cell);
                int wallIndex = wallTiles.IndexOf(wallTile);
                if (wallTile != null && wallIndex >= 0)
                {
                    selectedWallTileIndex = wallIndex;
                    activeTab = CampusRuntimeEditorTab.Build;
                    brushMode = CampusRuntimeBrushMode.PaintWall;
                    SetStatus(TrFormat("\u5df2\u62fe\u53d6\u5899\u4f53\u5730\u5757\uff1a{0}", "Picked wall tile: {0}", GetDisplayName(wallTile)));
                    return;
                }
            }

            if (floor.FloorTilemap != null)
            {
                TileBase floorTile = floor.FloorTilemap.GetTile(cell);
                int floorIndex = floorTiles.IndexOf(floorTile);
                if (floorTile != null && floorIndex >= 0)
                {
                    selectedFloorTileIndex = floorIndex;
                    activeTab = CampusRuntimeEditorTab.Build;
                    brushMode = CampusRuntimeBrushMode.PaintFloor;
                    SetStatus(TrFormat("\u5df2\u62fe\u53d6\u5730\u677f\u5730\u5757\uff1a{0}", "Picked floor tile: {0}", GetDisplayName(floorTile)));
                    return;
                }
            }

            if (floor.PropsRoot != null)
            {
                CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int i = 0; i < objects.Length; i++)
                {
                    CampusPlacedObject placed = objects[i];
                    if (placed == null || !placed.ContainsCell(cell))
                    {
                        continue;
                    }

                    int objectIndex = FindPrefabIndexByName(placed.ObjectId);
                    if (objectIndex >= 0)
                    {
                        selectedObjectIndex = objectIndex;
                        activeTab = CampusRuntimeEditorTab.Objects;
                        brushMode = CampusRuntimeBrushMode.PlaceObject;
                        SetStatus(TrFormat("\u5df2\u62fe\u53d6\u7269\u4ef6\uff1a{0}", "Picked object: {0}", CampusObjectNames.GetDisplayName(placed.ObjectId)));
                        return;
                    }
                }
            }

            SetStatus(Tr("\u5f53\u524d\u683c\u5b50\u6ca1\u6709\u53ef\u62fe\u53d6\u7684\u5185\u5bb9\u3002", "There is nothing to pick at the current cell."));
        }

        private void RecordUndo()
        {
            using (CampusWallBuildProfiler.RecordUndo.Auto())
            {
            bool refreshReferences = sceneReferencesDirty || mapRoot == null;
            string snapshot = BuildSnapshotJson(false, refreshReferences);
            AddUndoEntry(snapshot);
            }
        }

        private void AddUndoEntry(string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return;
            }

            if (undoSnapshots.Count > 0 && undoSnapshots[undoSnapshots.Count - 1] == entry)
            {
                return;
            }

            undoSnapshots.Add(entry);
            if (undoSnapshots.Count > MaxUndoSnapshots)
            {
                undoSnapshots.RemoveAt(0);
            }

            redoSnapshots.Clear();
            SchedulePlayerMapSave();
        }

        private void UndoSnapshot()
        {
            if (undoSnapshots.Count == 0)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u64a4\u9500\u7684\u64cd\u4f5c\u3002", "No undo operation is available."));
                return;
            }

            string previous = undoSnapshots[undoSnapshots.Count - 1];
            undoSnapshots.RemoveAt(undoSnapshots.Count - 1);
            if (TryApplyWallStrokeUndoEntry(previous, false))
            {
                redoSnapshots.Add(previous);
            }
            else
            {
                bool refreshReferences = sceneReferencesDirty || mapRoot == null;
                string current = BuildSnapshotJson(false, refreshReferences);
                redoSnapshots.Add(current);
                LoadSnapshotJson(previous);
            }
            SchedulePlayerMapSave();
            SetStatus(Tr("\u64a4\u9500\u5b8c\u6210\u3002", "Undo complete."));
        }

        private void RedoSnapshot()
        {
            if (redoSnapshots.Count == 0)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u91cd\u505a\u7684\u64cd\u4f5c\u3002", "No redo operation is available."));
                return;
            }

            string next = redoSnapshots[redoSnapshots.Count - 1];
            redoSnapshots.RemoveAt(redoSnapshots.Count - 1);
            if (TryApplyWallStrokeUndoEntry(next, true))
            {
                undoSnapshots.Add(next);
            }
            else
            {
                bool refreshReferences = sceneReferencesDirty || mapRoot == null;
                string current = BuildSnapshotJson(false, refreshReferences);
                undoSnapshots.Add(current);
                LoadSnapshotJson(next);
            }
            SchedulePlayerMapSave();
            SetStatus(Tr("\u91cd\u505a\u5b8c\u6210\u3002", "Redo complete."));
        }

        private string BuildSnapshotJson(bool prettyPrint = true, bool refreshReferences = true)
        {
            using (CampusWallBuildProfiler.BuildSnapshotJson.Auto())
            {
                return JsonUtility.ToJson(BuildSnapshot(refreshReferences), prettyPrint);
            }
        }

        private bool TryApplyWallStrokeUndoEntry(string encodedEntry, bool useAfterState)
        {
            if (string.IsNullOrEmpty(encodedEntry) || !encodedEntry.StartsWith(WallStrokeUndoPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string json = encodedEntry.Substring(WallStrokeUndoPrefix.Length);
            CampusRuntimeWallStrokeUndoEntry entry = JsonUtility.FromJson<CampusRuntimeWallStrokeUndoEntry>(json);
            if (entry == null || entry.Cells == null || entry.Cells.Count == 0)
            {
                return true;
            }

            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            CampusFloorRoot floor = mapRoot != null ? mapRoot.GetFloor(entry.FloorIndex) : null;
            if (floor == null)
            {
                floor = EnsureFloor(entry.FloorIndex);
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (floor == null || wallLogic == null)
            {
                return true;
            }

            wallVisualRebuildCells.Clear();
            for (int i = 0; i < entry.Cells.Count; i++)
            {
                CampusRuntimeWallStrokeCellUndoEntry cellEntry = entry.Cells[i];
                ApplyTileSnapshotToCell(wallLogic, cellEntry.Cell, useAfterState ? cellEntry.After : cellEntry.Before, wallTiles);
                wallVisualRebuildCells.Add(cellEntry.Cell);
            }

            RebuildWallVisuals(floor, wallVisualRebuildCells);
            floor.MarkUsedBoundsDirty();
            return true;
        }

        private CampusRuntimeMapSnapshot BuildSnapshot(bool refreshReferences = true)
        {
            using (CampusWallBuildProfiler.BuildSnapshot.Auto())
            {
            if (refreshReferences)
            {
                RefreshSceneReferences();
            }
            else
            {
                RefreshSceneCameraReference();
            }

            CampusRuntimeMapSnapshot snapshot = new CampusRuntimeMapSnapshot();
            snapshot.Schema = "NtingCampusRuntimeMapEditor.v1";
            snapshot.MapName = mapRoot != null ? mapRoot.gameObject.name : "CampusMap";
            snapshot.ExportedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            snapshot.SortingOrderStepPerFloor = mapRoot != null ? mapRoot.SortingOrderStepPerFloor : 1000;
            snapshot.SelectedFloorIndex = selectedFloorIndex;
            snapshot.HasRoomDefinitions = true;
            snapshot.RoomNames.Clear();
            snapshot.RoomRequiredCounts.Clear();
            for (int i = 0; i < roomNames.Count; i++)
            {
                snapshot.RoomNames.Add(roomNames[i]);
                snapshot.RoomRequiredCounts.Add(i < roomRequiredCounts.Count ? roomRequiredCounts[i] : 1);
            }

            if (mapRoot != null)
            {
                if (refreshReferences)
                {
                    mapRoot.RebuildFloorReferences();
                }

                for (int i = 0; i < mapRoot.Floors.Count; i++)
                {
                    CampusFloorRoot floor = mapRoot.Floors[i];
                    if (floor == null)
                    {
                        continue;
                    }

                    CampusRuntimeFloorSnapshot floorSnapshot = new CampusRuntimeFloorSnapshot();
                    floorSnapshot.FloorIndex = floor.FloorIndex;
                    floorSnapshot.IsUnlocked = floor.IsUnlocked;
                    CaptureTiles(floor.FloorTilemap, floorTiles, floorSnapshot.FloorTiles);
                    CaptureTiles(CampusWallTileUtility.GetWallLogicTilemap(floor), wallTiles, floorSnapshot.WallTiles);
                    CaptureObjects(floor, floorSnapshot.Objects);
                    CaptureStairs(floor, floorSnapshot.Stairs);
                    CaptureRooms(floor, floorSnapshot.Rooms);
                    snapshot.Floors.Add(floorSnapshot);
                }
            }

            CaptureLights(snapshot.Lights);
            return snapshot;
            }
        }

        private void LoadSnapshotJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            CampusRuntimeMapSnapshot snapshot = JsonUtility.FromJson<CampusRuntimeMapSnapshot>(json);
            if (snapshot == null)
            {
                SetStatus(Tr("\u5bfc\u5165\u5931\u8d25\uff1aJSON \u65e0\u6548\u3002", "Import failed: invalid JSON."));
                return;
            }

            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(CampusRuntimeMapSnapshot snapshot)
        {
            RefreshSceneReferences(true);
            if (mapRoot == null)
            {
                return;
            }

            mapRoot.SortingOrderStepPerFloor = snapshot.SortingOrderStepPerFloor <= 0 ? 1000 : snapshot.SortingOrderStepPerFloor;
            if (snapshot.HasRoomDefinitions)
            {
                roomNames.Clear();
                roomRequiredCounts.Clear();
                if (snapshot.RoomNames != null)
                {
                    for (int i = 0; i < snapshot.RoomNames.Count; i++)
                    {
                        int required = snapshot.RoomRequiredCounts != null && i < snapshot.RoomRequiredCounts.Count ? snapshot.RoomRequiredCounts[i] : 1;
                        AddOrUpdateRoomDefinition(snapshot.RoomNames[i], required);
                    }
                }
            }

            mapRoot.RebuildFloorReferences();
            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                ClearFloorAuthoredContent(mapRoot.Floors[i]);
            }

            for (int i = 0; i < snapshot.Floors.Count; i++)
            {
                CampusRuntimeFloorSnapshot floorSnapshot = snapshot.Floors[i];
                CampusFloorRoot floor = EnsureFloor(floorSnapshot.FloorIndex);
                floor.IsUnlocked = floorSnapshot.IsUnlocked;
                ApplyTiles(floor.FloorTilemap, floorSnapshot.FloorTiles, floorTiles);
                ApplyTiles(CampusWallTileUtility.GetWallLogicTilemap(floor), floorSnapshot.WallTiles, wallTiles);
                ApplyObjects(floor, floorSnapshot.Objects);
                ApplyStairs(floor, floorSnapshot.Stairs);
                ApplyRooms(floor, floorSnapshot.Rooms);
                RebuildWallVisuals(floor);
                CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
                floor.RefreshUsedBounds();
            }

            ApplySnapshotLights(snapshot.Lights);
            selectedFloorIndex = snapshot.SelectedFloorIndex > 0 ? snapshot.SelectedFloorIndex : selectedFloorIndex;
            mapRoot.CurrentPreviewFloor = selectedFloorIndex;
            mapRoot.RebuildFloorReferences();
            MarkSceneReferencesDirty();
            RefreshSceneReferencesIfNeeded(true);
            EnsureAreaDefinitionsAvailable(true);
            PrepareRuntimeMapPresentationSafe();
        }

        private void ExportToJson()
        {
            string folder = GetExportFolder();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "CampusMap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(path, BuildSnapshotJson());
            SaveGameplayOverlayForMapPath(path, false);
            SetStatus(TrFormat("\u5df2\u5bfc\u51fa\u5730\u56fe JSON\uff1a{0}", "Exported map JSON: {0}", path));
            Debug.Log("[NtingCampusRuntimeMapEditor] Exported map to " + path);
        }

        private void SavePlayerMap()
        {
            SavePlayerMap(true);
        }

        public void CreateBlankMap(bool savePlayerMap)
        {
            RefreshSceneReferences(true);
            if (mapRoot == null)
            {
                return;
            }

            roomNames.Clear();
            roomRequiredCounts.Clear();
            mapRoot.RebuildFloorReferences();
            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = mapRoot.Floors[i];
                if (floor == null)
                {
                    continue;
                }

                ClearFloorAuthoredContent(floor);
                floor.IsUnlocked = floor.FloorIndex <= 1;
                CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
                floor.RefreshUsedBounds();
            }

            ClearRuntimeEditableLights();
            ClearRuntimeGameplayOverlayMarkers();
            cachedGameplayActors.Clear();
            selectedFloorIndex = 1;
            mapRoot.CurrentPreviewFloor = selectedFloorIndex;
            RememberMapLoadSource(CampusRuntimeMapLoadSource.Scene, GetActiveScenePath());
            MarkSceneReferencesDirty();
            RefreshSceneReferencesIfNeeded(true);
            PrepareRuntimeMapPresentationSafe();
            EnsureAreaDefinitionsAvailable(true);

            if (savePlayerMap)
            {
                SavePlayerMap(false);
            }

            SetStatus(Tr("\u5df2\u521b\u5efa\u7a7a\u767d\u5730\u56fe\u3002", "Created blank map."));
        }

        public bool SaveNamedMap(string path, CampusRuntimeMapLoadSource source, bool showStatus)
        {
            return SaveMapToPath(path, source, showStatus);
        }

        private void SavePlayerMap(bool showStatus)
        {
            if (playerSaveInProgress)
            {
                return;
            }

            try
            {
                playerSaveInProgress = true;
                EnsureImportFolders();
                string saveRoot = GetPlayerSaveRootFolder();
                Directory.CreateDirectory(saveRoot);
                File.WriteAllText(GetPlayerSaveMapPath(), BuildSnapshotJson(), Encoding.UTF8);
                SaveGameplayOverlayForMapPath(GetPlayerSaveMapPath(), false);
                CampusRuntimePlayerMapSaveManifest manifest = new CampusRuntimePlayerMapSaveManifest
                {
                    SavedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UnityPersistentDataPath = string.Empty,
                    ImportRootFolderName = RuntimeImportFolder,
                    MapFileName = PlayerSaveMapFile
                };
                File.WriteAllText(GetPlayerSaveManifestPath(), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
                playerSavePending = false;
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u73a9\u5bb6\u5730\u56fe\uff1a{0}", "Saved player map: {0}", saveRoot));
                }

                Debug.Log("[NtingCampusRuntimeMapEditor] Saved player map to " + saveRoot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Save player map failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u4fdd\u5b58\u73a9\u5bb6\u5730\u56fe\u5931\u8d25\uff1a{0}", "Save player map failed: {0}", exception.Message));
                }
            }
            finally
            {
                playerSaveInProgress = false;
            }
        }

        private bool SaveCurrentMapSource(bool showStatus)
        {
            switch (lastMapLoadSource)
            {
                case CampusRuntimeMapLoadSource.AuthoringPackage:
                    if (!string.IsNullOrWhiteSpace(lastMapLoadPath))
                    {
                        return SaveMapToPath(lastMapLoadPath, CampusRuntimeMapLoadSource.AuthoringPackage, showStatus);
                    }

                    return false;
                case CampusRuntimeMapLoadSource.PlayerSave:
                    SavePlayerMap(showStatus);
                    return true;
                default:
                    return false;
            }
        }

        private bool SaveMapToPath(string path, CampusRuntimeMapLoadSource source, bool showStatus)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                EnsureImportFolders();
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(path, BuildSnapshotJson(), Encoding.UTF8);
                SaveGameplayOverlayForMapPath(path, false);
                RememberMapLoadSource(source, path);
                RefreshAssetDatabaseIfAvailable();
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u5730\u56fe\uff1a{0}", "Saved map: {0}", path));
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Save map failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u4fdd\u5b58\u5730\u56fe\u5931\u8d25\uff1a{0}", "Save map failed: {0}", exception.Message));
                }

                return false;
            }
        }

        private void LoadPlayerMap()
        {
            LoadPlayerMap(true, true);
        }

        private void LoadPlayerMap(bool recordUndo, bool showStatus)
        {
            string savePath = GetPlayerSaveMapPath();
            if (!File.Exists(savePath))
            {
                if (showStatus)
                {
                    SetStatus(TrFormat("\u6ca1\u6709\u627e\u5230\u73a9\u5bb6\u5730\u56fe\u5b58\u6863\uff1a{0}", "Player map save was not found: {0}", savePath));
                }

                return;
            }

            bool previousSuppress = suppressPlayerSaveScheduling;
            try
            {
                suppressPlayerSaveScheduling = true;
                if (recordUndo)
                {
                    RecordUndo();
                }

                EnsureImportFolders();
                LoadRuntimeResources();
                LoadSnapshotJson(File.ReadAllText(savePath, Encoding.UTF8));
                RememberMapLoadSource(CampusRuntimeMapLoadSource.PlayerSave, savePath);
                ApplyGameplayOverlayFromPath(savePath, false);
                playerSavePending = false;
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u8bfb\u53d6\u73a9\u5bb6\u5730\u56fe\uff1a{0}", "Loaded player map: {0}", savePath));
                }

                Debug.Log("[NtingCampusRuntimeMapEditor] Loaded player map from " + savePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Load player map failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u8bfb\u53d6\u73a9\u5bb6\u5730\u56fe\u5931\u8d25\uff1a{0}", "Load player map failed: {0}", exception.Message));
                }
            }
            finally
            {
                suppressPlayerSaveScheduling = previousSuppress;
            }
        }

        private void ExportRuntimeAuthoringPackage()
        {
            ExportRuntimeAuthoringPackage(true);
        }

        private void ExportRuntimeAuthoringPackage(bool showStatus)
        {
            if (authoringPackageInProgress)
            {
                return;
            }

            try
            {
                authoringPackageInProgress = true;
                SavePlayerMap(false);
                EnsureImportFolders();
                string packageRoot = GetAuthoringPackageRootFolder();
                string packageImportFolder = GetAuthoringPackageImportFolder();
                Directory.CreateDirectory(packageRoot);
                if (!AreSamePath(GetImportRootFolder(), packageImportFolder))
                {
                    MirrorDirectory(GetImportRootFolder(), packageImportFolder, true);
                }

                File.WriteAllText(GetAuthoringPackageMapPath(), BuildSnapshotJson(), Encoding.UTF8);
                SaveGameplayOverlayForMapPath(GetAuthoringPackageMapPath(), false);
                CampusRuntimeAuthoringPackageManifest manifest = new CampusRuntimeAuthoringPackageManifest
                {
                    ExportedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UnityPersistentDataPath = string.Empty,
                    ImportRootFolderName = RuntimeImportFolder,
                    MapFileName = AuthoringPackageMapFile
                };
                File.WriteAllText(GetAuthoringPackageManifestPath(), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
                RefreshAssetDatabaseIfAvailable();
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u5bfc\u51fa\u5f00\u53d1\u671f\u5730\u56fe\u5305\uff1a{0}", "Exported authoring map package: {0}", packageRoot));
                }

                Debug.Log("[NtingCampusRuntimeMapEditor] Exported authoring map package to " + packageRoot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Export authoring package failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5bfc\u51fa\u5f00\u53d1\u671f\u5730\u56fe\u5305\u5931\u8d25\uff1a{0}", "Export authoring map package failed: {0}", exception.Message));
                }
            }
            finally
            {
                authoringPackageInProgress = false;
            }
        }

        private void RestoreRuntimeAuthoringPackage()
        {
            RestoreRuntimeAuthoringPackage(true, true);
        }

        private void RestoreRuntimeAuthoringPackage(bool recordUndo, bool showStatus)
        {
            string packageRoot = GetAuthoringPackageRootFolder();
            string packageImportFolder = GetAuthoringPackageImportFolder();
            string packageMapPath = GetAuthoringPackageMapPath();
            if (!Directory.Exists(packageImportFolder) && !File.Exists(packageMapPath))
            {
                if (showStatus)
                {
                    SetStatus(TrFormat("\u6ca1\u6709\u627e\u5230\u5f00\u53d1\u671f\u5730\u56fe\u5305\uff1a{0}", "Authoring map package was not found: {0}", packageRoot));
                }

                return;
            }

            bool previousSuppress = suppressPlayerSaveScheduling;
            try
            {
                suppressPlayerSaveScheduling = true;
                if (recordUndo)
                {
                    RecordUndo();
                }

                if (!AreSamePath(packageImportFolder, GetImportRootFolder()))
                {
                    BackupLocalRuntimeImportFolder();
                    if (Directory.Exists(packageImportFolder))
                    {
                        MirrorDirectory(packageImportFolder, GetImportRootFolder(), true);
                    }
                }

                LoadRuntimeResources();
                if (File.Exists(packageMapPath))
                {
                    LoadSnapshotJson(File.ReadAllText(packageMapPath, Encoding.UTF8));
                    RememberMapLoadSource(CampusRuntimeMapLoadSource.AuthoringPackage, packageMapPath);
                    ApplyGameplayOverlayFromPath(packageMapPath, false);
                }

                playerSavePending = false;
                if (showStatus)
                {
                    SetStatus(Tr("\u5df2\u4ece\u5f00\u53d1\u671f\u5730\u56fe\u5305\u6062\u590d\u5730\u56fe\u3002", "Restored map from authoring package."));
                }

                Debug.Log("[NtingCampusRuntimeMapEditor] Restored authoring map package from " + packageRoot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Restore authoring package failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u6062\u590d\u5f00\u53d1\u671f\u5730\u56fe\u5305\u5931\u8d25\uff1a{0}", "Restore authoring map package failed: {0}", exception.Message));
                }
            }
            finally
            {
                suppressPlayerSaveScheduling = previousSuppress;
            }
        }

        private void TryAutoLoadPlayerMap()
        {
            if (!autoLoadPlayerMapOnStart || !File.Exists(GetPlayerSaveMapPath()))
            {
                return;
            }

            LoadPlayerMap(false, false);
        }

        private void SchedulePlayerMapSave()
        {
            if (!autoSavePlayerMap || suppressPlayerSaveScheduling || playerSaveInProgress)
            {
                return;
            }

            playerSavePending = true;
            playerSaveDueTime = Time.realtimeSinceStartup + Mathf.Max(0.1f, autoSavePlayerMapDelay);
        }

        private void ProcessAutoPlayerMapSave()
        {
            if (!autoSavePlayerMap || !playerSavePending || playerSaveInProgress)
            {
                return;
            }

            if (Time.realtimeSinceStartup < playerSaveDueTime)
            {
                return;
            }

            SavePlayerMap(false);
        }

        private void FlushPendingPlayerMapSaveBeforeShutdown()
        {
            if (!autoSavePlayerMap || !playerSavePending || playerSaveInProgress)
            {
                return;
            }

            if (mapRoot == null)
            {
                mapRoot = FindFirstObjectByType<CampusMapRoot>();
            }

            if (mapRoot == null)
            {
                return;
            }

            SavePlayerMap(false);
        }

        private void ImportLatestJson()
        {
            string folder = GetExportFolder();
            if (!Directory.Exists(folder))
            {
                SetStatus(TrFormat("\u672a\u627e\u5230\u5bfc\u51fa\u6587\u4ef6\u5939\uff1a{0}", "Export folder not found: {0}", folder));
                return;
            }

            string[] files = Directory.GetFiles(folder, "CampusMap_*.json");
            if (files.Length == 0)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u5bfc\u5165\u7684\u5730\u56fe JSON\u3002", "No map JSON is available to import."));
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            string path = files[files.Length - 1];
            RecordUndo();
            LoadSnapshotJson(File.ReadAllText(path));
            ApplyGameplayOverlayFromPath(path, false);
            SetStatus(TrFormat("\u5df2\u5bfc\u5165\u5730\u56fe JSON\uff1a{0}", "Imported map JSON: {0}", path));
        }

        private string GetExportFolder()
        {
            return Path.Combine(Application.persistentDataPath, "CampusMapExports");
        }

        private string GetPlayerSaveRootFolder()
        {
            return Path.Combine(Application.persistentDataPath, PlayerSaveFolder);
        }

        private string GetPlayerSaveMapPath()
        {
            return Path.Combine(GetPlayerSaveRootFolder(), PlayerSaveMapFile);
        }

        private string GetPlayerSaveManifestPath()
        {
            return Path.Combine(GetPlayerSaveRootFolder(), PlayerSaveManifestFile);
        }

        private string GetAuthoringPackageRootFolder()
        {
            return Path.Combine(Application.dataPath, "NtingCampus", AuthoringPackageFolder);
        }

        private string GetAuthoringPackageImportFolder()
        {
            return Path.Combine(GetAuthoringPackageRootFolder(), RuntimeImportFolder);
        }

        private string GetAuthoringPackageMapPath()
        {
            return Path.Combine(GetAuthoringPackageRootFolder(), AuthoringPackageMapFile);
        }

        private string GetAuthoringPackageManifestPath()
        {
            return Path.Combine(GetAuthoringPackageRootFolder(), AuthoringPackageManifestFile);
        }

        private static string GetGameplayOverlayPathForMapPath(string mapPath)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                return string.Empty;
            }

            string folder = Path.GetDirectoryName(mapPath) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(mapPath);
            return Path.Combine(folder, fileName + GameplayOverlayExtension);
        }

        private bool SaveGameplayOverlayForMapPath(string mapPath, bool showStatus)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                return false;
            }

            try
            {
                string overlayPath = GetGameplayOverlayPathForMapPath(mapPath);
                string folder = Path.GetDirectoryName(overlayPath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                CampusRuntimeGameplayOverlaySnapshot snapshot = BuildGameplayOverlaySnapshot(mapPath);
                File.WriteAllText(overlayPath, JsonUtility.ToJson(snapshot, true), Encoding.UTF8);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u73a9\u6cd5\u5c42\uff1a{0}", "Saved gameplay overlay: {0}", overlayPath));
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Save gameplay overlay failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u4fdd\u5b58\u73a9\u6cd5\u5c42\u5931\u8d25\uff1a{0}", "Save gameplay overlay failed: {0}", exception.Message));
                }

                return false;
            }
        }

        private void SaveGameplayOverlayForCurrentSource(bool showStatus)
        {
            string mapPath = ResolveCurrentWritableMapPath();
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                if (showStatus)
                {
                    SetStatus(Tr("\u6ca1\u6709\u53ef\u5199\u5165\u7684\u5730\u56fe\u8def\u5f84\u7528\u4e8e\u4fdd\u5b58\u73a9\u6cd5\u5c42\u3002", "No writable map path is available for gameplay overlay."));
                }

                return;
            }

            SaveGameplayOverlayForMapPath(mapPath, showStatus);
        }

        private void ReloadGameplayOverlayForCurrentSource(bool showStatus)
        {
            string mapPath = ResolveCurrentWritableMapPath();
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                if (showStatus)
                {
                    SetStatus(Tr("\u6ca1\u6709\u53ef\u7528\u5730\u56fe\u8def\u5f84\u7528\u4e8e\u91cd\u8bfb\u73a9\u6cd5\u5c42\u3002", "No map path is available for gameplay overlay reload."));
                }

                return;
            }

            ApplyGameplayOverlayFromPath(mapPath, showStatus);
        }

        private string ResolveCurrentWritableMapPath()
        {
            if (!string.IsNullOrWhiteSpace(lastMapLoadPath) &&
                (lastMapLoadSource == CampusRuntimeMapLoadSource.AuthoringPackage ||
                 lastMapLoadSource == CampusRuntimeMapLoadSource.PlayerSave))
            {
                return lastMapLoadPath;
            }

            return GetPlayerSaveMapPath();
        }

        private void ApplyGameplayOverlayFromPath(string mapPath, bool showStatus)
        {
            ClearRuntimeGameplayOverlayMarkers();
            cachedGameplayActors.Clear();

            string overlayPath = GetGameplayOverlayPathForMapPath(mapPath);
            if (string.IsNullOrWhiteSpace(overlayPath) || !File.Exists(overlayPath))
            {
                RebuildGameplayRoomRegistrySafe();
                if (showStatus)
                {
                    SetStatus(Tr("\u8be5\u5730\u56fe\u6ca1\u6709\u627e\u5230\u73a9\u6cd5\u5c42\u6587\u4ef6\u3002", "No gameplay overlay found for this map."));
                }

                return;
            }

            try
            {
                string json = File.ReadAllText(overlayPath, Encoding.UTF8);
                CampusRuntimeGameplayOverlaySnapshot snapshot =
                    JsonUtility.FromJson<CampusRuntimeGameplayOverlaySnapshot>(json);
                if (snapshot == null)
                {
                    throw new InvalidOperationException(Tr("\u73a9\u6cd5\u5c42 JSON \u65e0\u6548\u3002", "Invalid gameplay overlay JSON."));
                }

                NormalizeGameplayOverlaySnapshot(snapshot);
                CacheGameplayActors(snapshot.Actors);
                SpawnGameplayRooms(snapshot.Rooms);
                SpawnGameplayFacilities(snapshot.Facilities);
                SpawnGameplayPrankSpots(snapshot.PrankSpots);
                RebuildGameplayRoomRegistrySafe();
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u8bfb\u53d6\u73a9\u6cd5\u5c42\uff1a{0}", "Loaded gameplay overlay: {0}", overlayPath));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Load gameplay overlay failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u8bfb\u53d6\u73a9\u6cd5\u5c42\u5931\u8d25\uff1a{0}", "Load gameplay overlay failed: {0}", exception.Message));
                }
            }
        }

        private CampusRuntimeGameplayOverlaySnapshot BuildGameplayOverlaySnapshot(string targetMapPath)
        {
            CampusRuntimeGameplayOverlaySnapshot snapshot = new CampusRuntimeGameplayOverlaySnapshot();
            snapshot.Schema = GameplayOverlaySchema;
            snapshot.MapName = mapRoot != null ? mapRoot.gameObject.name : "CampusMap";
            snapshot.Actors = ResolveGameplayActorsForSave(targetMapPath);
            snapshot.Rooms = new List<CampusRuntimeGameplayRoomSnapshot>();
            snapshot.Facilities = new List<CampusRuntimeGameplayFacilitySnapshot>();
            snapshot.PrankSpots = new List<CampusRuntimeGameplayPrankSpotSnapshot>();

            CaptureGameplayRooms(snapshot.Rooms);
            CaptureGameplayFacilities(snapshot.Facilities);
            CaptureGameplayPrankSpots(snapshot.PrankSpots);
            return snapshot;
        }

        private List<CampusRuntimeGameplayActorSnapshot> ResolveGameplayActorsForSave(string targetMapPath)
        {
            if (cachedGameplayActors.Count > 0)
            {
                return CloneGameplayActors(cachedGameplayActors);
            }

            CampusRuntimeGameplayOverlaySnapshot existing = ReadGameplayOverlaySnapshot(targetMapPath);
            return existing != null ? CloneGameplayActors(existing.Actors) : new List<CampusRuntimeGameplayActorSnapshot>();
        }

        private CampusRuntimeGameplayOverlaySnapshot ReadGameplayOverlaySnapshot(string mapPath)
        {
            string overlayPath = GetGameplayOverlayPathForMapPath(mapPath);
            if (string.IsNullOrWhiteSpace(overlayPath) || !File.Exists(overlayPath))
            {
                return null;
            }

            try
            {
                CampusRuntimeGameplayOverlaySnapshot snapshot =
                    JsonUtility.FromJson<CampusRuntimeGameplayOverlaySnapshot>(File.ReadAllText(overlayPath, Encoding.UTF8));
                if (snapshot == null)
                {
                    return null;
                }

                NormalizeGameplayOverlaySnapshot(snapshot);
                return snapshot;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Read gameplay overlay failed: " + exception.Message);
                return null;
            }
        }

        private static void NormalizeGameplayOverlaySnapshot(CampusRuntimeGameplayOverlaySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(snapshot.Schema))
            {
                snapshot.Schema = GameplayOverlaySchema;
            }

            snapshot.Actors = snapshot.Actors ?? new List<CampusRuntimeGameplayActorSnapshot>();
            snapshot.Rooms = snapshot.Rooms ?? new List<CampusRuntimeGameplayRoomSnapshot>();
            snapshot.Facilities = snapshot.Facilities ?? new List<CampusRuntimeGameplayFacilitySnapshot>();
            snapshot.PrankSpots = snapshot.PrankSpots ?? new List<CampusRuntimeGameplayPrankSpotSnapshot>();
        }

        private void CacheGameplayActors(List<CampusRuntimeGameplayActorSnapshot> actors)
        {
            cachedGameplayActors.Clear();
            if (actors == null)
            {
                return;
            }

            List<CampusRuntimeGameplayActorSnapshot> clones = CloneGameplayActors(actors);
            for (int i = 0; i < clones.Count; i++)
            {
                cachedGameplayActors.Add(clones[i]);
            }
        }

        private static List<CampusRuntimeGameplayActorSnapshot> CloneGameplayActors(
            List<CampusRuntimeGameplayActorSnapshot> actors)
        {
            List<CampusRuntimeGameplayActorSnapshot> clones = new List<CampusRuntimeGameplayActorSnapshot>();
            if (actors == null)
            {
                return clones;
            }

            for (int i = 0; i < actors.Count; i++)
            {
                CampusRuntimeGameplayActorSnapshot source = actors[i];
                if (source == null)
                {
                    continue;
                }

                CampusRuntimeGameplayActorSnapshot clone = new CampusRuntimeGameplayActorSnapshot
                {
                    Id = source.Id,
                    DisplayName = source.DisplayName,
                    LocalizedDisplayName = source.LocalizedDisplayName,
                    Role = source.Role,
                    TeacherDuty = source.TeacherDuty,
                    StaffDuty = source.StaffDuty,
                    ClassId = source.ClassId,
                    InitialState = source.InitialState,
                    IsPlayerControlled = source.IsPlayerControlled,
                    FloorIndex = Mathf.Max(1, source.FloorIndex),
                    Cell = NormalizeCell(source.Cell),
                    Sleepiness = source.Sleepiness,
                    Mischief = source.Mischief,
                    Traits = source.Traits != null
                        ? (CampusCharacterTrait[])source.Traits.Clone()
                        : Array.Empty<CampusCharacterTrait>()
                };
                clones.Add(clone);
            }

            return clones;
        }

        private void CaptureGameplayRooms(List<CampusRuntimeGameplayRoomSnapshot> output)
        {
            output.Clear();
            CampusGameplayRoomMarker[] markers =
                FindObjectsByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<string> capturedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                Vector3Int anchor = NormalizeCell(marker.AnchorCell);
                Vector2Int size = marker.RoomSize;
                string key = marker.FloorIndex + "|" + marker.RoomType + "|" + anchor + "|" + size + "|" + marker.RoomDisplayName;
                if (!capturedKeys.Add(key))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayRoomSnapshot
                {
                    Id = marker.RoomIdOverride,
                    DisplayName = marker.RoomDisplayName,
                    RoomType = marker.RoomType,
                    FloorIndex = Mathf.Max(1, marker.FloorIndex),
                    AnchorCell = anchor,
                    Size = size,
                    UsableForGameplay = marker.UsableForGameplay
                });
            }
        }

        private void CaptureGameplayFacilities(List<CampusRuntimeGameplayFacilitySnapshot> output)
        {
            output.Clear();
            CampusGameplayFacilityMarker[] markers =
                FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<string> capturedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                Vector3Int cell = NormalizeCell(marker.Cell);
                string key = marker.FloorIndex + "|" + marker.FacilityType + "|" + cell + "|" + marker.DisplayName;
                if (!capturedKeys.Add(key))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayFacilitySnapshot
                {
                    DisplayName = marker.DisplayName,
                    FacilityType = marker.FacilityType,
                    FloorIndex = Mathf.Max(1, marker.FloorIndex),
                    Cell = cell,
                    CountsAsCoreFacility = marker.CountsAsCoreFacility
                });
            }
        }

        private void CaptureGameplayPrankSpots(List<CampusRuntimeGameplayPrankSpotSnapshot> output)
        {
            output.Clear();
            CampusPrankInteractionSpot[] spots =
                FindObjectsByType<CampusPrankInteractionSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<string> capturedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < spots.Length; i++)
            {
                CampusPrankInteractionSpot spot = spots[i];
                if (spot == null || !TryResolveGameplayMarkerCell(spot, out int floorIndex, out Vector3Int cell))
                {
                    continue;
                }

                cell = NormalizeCell(cell);
                string key = floorIndex + "|" + spot.PrankPayload + "|" + cell + "|" + spot.DisplayName;
                if (!capturedKeys.Add(key))
                {
                    continue;
                }

                output.Add(new CampusRuntimeGameplayPrankSpotSnapshot
                {
                    DisplayName = spot.DisplayName,
                    Payload = spot.PrankPayload,
                    RequiredRoomType = spot.RequiredRoomType,
                    VisualKind = spot.VisualKind,
                    FloorIndex = Mathf.Max(1, floorIndex),
                    Cell = cell,
                    InteractionRadius = spot.InteractionRadius,
                    AccentColor = spot.AccentColor,
                    UnsupportedReason = spot.UnsupportedReason
                });
            }
        }

        private void SpawnGameplayRooms(List<CampusRuntimeGameplayRoomSnapshot> rooms)
        {
            if (rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                CampusRuntimeGameplayRoomSnapshot room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                CampusFloorRoot floor = EnsureFloor(Mathf.Max(1, room.FloorIndex));
                if (floor == null || floor.Grid == null || floor.PropsRoot == null)
                {
                    continue;
                }

                Vector3Int anchor = NormalizeCell(room.AnchorCell);
                GameObject markerObject = CreateGameplayMarkerObject(
                    floor,
                    anchor,
                    "GameplayRoom_" + room.RoomType + "_F" + floor.FloorIndex + "_" + anchor.x + "_" + anchor.y);
                if (markerObject == null)
                {
                    continue;
                }

                CampusGameplayRoomMarker marker = markerObject.AddComponent<CampusGameplayRoomMarker>();
                marker.Configure(
                    room.Id,
                    room.DisplayName,
                    room.RoomType,
                    floor.FloorIndex,
                    anchor,
                    room.Size,
                    room.UsableForGameplay);
            }
        }

        private void SpawnGameplayFacilities(List<CampusRuntimeGameplayFacilitySnapshot> facilities)
        {
            if (facilities == null)
            {
                return;
            }

            for (int i = 0; i < facilities.Count; i++)
            {
                CampusRuntimeGameplayFacilitySnapshot facility = facilities[i];
                if (facility == null)
                {
                    continue;
                }

                CampusFloorRoot floor = EnsureFloor(Mathf.Max(1, facility.FloorIndex));
                if (floor == null || floor.Grid == null || floor.PropsRoot == null)
                {
                    continue;
                }

                Vector3Int cell = NormalizeCell(facility.Cell);
                GameObject markerObject = CreateGameplayMarkerObject(
                    floor,
                    cell,
                    "GameplayFacility_" + facility.FacilityType + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
                if (markerObject == null)
                {
                    continue;
                }

                CampusGameplayFacilityMarker marker = markerObject.AddComponent<CampusGameplayFacilityMarker>();
                marker.Configure(
                    facility.DisplayName,
                    facility.FacilityType,
                    floor.FloorIndex,
                    cell,
                    facility.CountsAsCoreFacility,
                    null);
            }
        }

        private void SpawnGameplayPrankSpots(List<CampusRuntimeGameplayPrankSpotSnapshot> spots)
        {
            if (spots == null)
            {
                return;
            }

            for (int i = 0; i < spots.Count; i++)
            {
                CampusRuntimeGameplayPrankSpotSnapshot spot = spots[i];
                if (spot == null)
                {
                    continue;
                }

                CampusFloorRoot floor = EnsureFloor(Mathf.Max(1, spot.FloorIndex));
                if (floor == null || floor.Grid == null || floor.PropsRoot == null)
                {
                    continue;
                }

                Vector3Int cell = NormalizeCell(spot.Cell);
                GameObject markerObject = CreateGameplayMarkerObject(
                    floor,
                    cell,
                    "GameplayPrank_" + SanitizeFileName(spot.DisplayName) + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
                if (markerObject == null)
                {
                    continue;
                }

                CampusPrankInteractionSpot interactionSpot = markerObject.AddComponent<CampusPrankInteractionSpot>();
                interactionSpot.Configure(
                    spot.DisplayName,
                    spot.Payload,
                    spot.RequiredRoomType,
                    spot.VisualKind,
                    spot.InteractionRadius,
                    spot.AccentColor,
                    spot.UnsupportedReason);
            }
        }

        private void ClearRuntimeGameplayOverlayMarkers()
        {
            CampusRuntimeGameplayOverlayEntity[] entities =
                FindObjectsByType<CampusRuntimeGameplayOverlayEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = entities.Length - 1; i >= 0; i--)
            {
                CampusRuntimeGameplayOverlayEntity entity = entities[i];
                if (entity == null || entity.IsActorEntity)
                {
                    continue;
                }

                if (entity.GetComponent<CampusGameplayRoomMarker>() != null ||
                    entity.GetComponent<CampusGameplayFacilityMarker>() != null ||
                    entity.GetComponent<CampusPrankInteractionSpot>() != null)
                {
                    DestroyRuntimeObject(entity.gameObject);
                }
            }
        }

        private void RememberMapLoadSource(CampusRuntimeMapLoadSource source, string path)
        {
            lastMapLoadSource = source;
            lastMapLoadPath = path ?? string.Empty;
        }

        private string DescribeMapLoadSource()
        {
            string sourceName;
            switch (lastMapLoadSource)
            {
                case CampusRuntimeMapLoadSource.PlayerSave:
                    sourceName = Tr("\u73a9\u5bb6\u5b58\u6863", "Player Save");
                    break;
                case CampusRuntimeMapLoadSource.AuthoringPackage:
                    sourceName = Tr("\u5f00\u53d1\u671f\u5730\u56fe\u5305", "Authoring Package");
                    break;
                default:
                    sourceName = Tr("\u573a\u666f\u9ed8\u8ba4", "Scene Default");
                    break;
            }

            return string.IsNullOrEmpty(lastMapLoadPath)
                ? sourceName
                : sourceName + " -> " + lastMapLoadPath;
        }

        private string GetActiveScenePath()
        {
            UnityEngine.SceneManagement.Scene scene = gameObject.scene;
            return scene.IsValid() && !string.IsNullOrEmpty(scene.path)
                ? scene.path
                : "Scene";
        }

        private void MigratePersistentImportLibraryToProjectIfNeeded()
        {
            if (importLibraryMigrationChecked)
            {
                return;
            }

            importLibraryMigrationChecked = true;
#if UNITY_EDITOR
            string persistentImportRoot = GetPersistentImportRootFolder();
            string projectImportRoot = GetAuthoringPackageImportFolder();
            if (AreSamePath(persistentImportRoot, projectImportRoot) ||
                !Directory.Exists(persistentImportRoot) ||
                ImportLibraryHasContent(projectImportRoot) ||
                !ImportLibraryHasContent(persistentImportRoot))
            {
                return;
            }

            MirrorDirectory(persistentImportRoot, projectImportRoot, false);
            RefreshAssetDatabaseIfAvailable();
            Debug.Log("[NtingCampusRuntimeMapEditor] Migrated runtime import library into project folder: " + projectImportRoot);
#endif
        }

        private bool ImportLibraryHasContent(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return false;
            }

            string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string extension = Path.GetExtension(file);
                if (string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = Path.GetFileName(file);
                if (string.Equals(name, RoomImportFile, StringComparison.OrdinalIgnoreCase))
                {
                    string text = File.ReadAllText(file, Encoding.UTF8);
                    if (text.StartsWith("# One room per line", StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                return true;
            }

            return false;
        }

        private void RefreshImportAssetDatabaseIfProjectBacked()
        {
#if UNITY_EDITOR
            if (AreSamePath(GetImportRootFolder(), GetAuthoringPackageImportFolder()))
            {
                RefreshAssetDatabaseIfAvailable();
            }
#endif
        }

        private void CaptureTiles(Tilemap tilemap, List<TileBase> palette, List<CampusRuntimeTileSnapshot> output)
        {
            output.Clear();
            if (tilemap == null)
            {
                return;
            }

            tilemap.CompressBounds();
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(cell);
                if (tile == null)
                {
                    continue;
                }

                CampusRuntimeTileSnapshot tileSnapshot = new CampusRuntimeTileSnapshot();
                tileSnapshot.Cell = cell;
                tileSnapshot.AssetName = tile.name;
                tileSnapshot.PaletteIndex = palette.IndexOf(tile);
                tileSnapshot.Transform = tilemap.GetTransformMatrix(cell);
                output.Add(tileSnapshot);
            }
        }

        private CampusRuntimeTileSnapshot CaptureTileSnapshot(Tilemap tilemap, List<TileBase> palette, Vector3Int cell)
        {
            if (tilemap == null)
            {
                return null;
            }

            TileBase tile = tilemap.GetTile(cell);
            if (tile == null)
            {
                return null;
            }

            CampusRuntimeTileSnapshot tileSnapshot = new CampusRuntimeTileSnapshot();
            tileSnapshot.Cell = cell;
            tileSnapshot.AssetName = tile.name;
            tileSnapshot.PaletteIndex = palette != null ? palette.IndexOf(tile) : -1;
            tileSnapshot.Transform = tilemap.GetTransformMatrix(cell);
            return tileSnapshot;
        }

        private static CampusRuntimeTileSnapshot CloneTileSnapshot(CampusRuntimeTileSnapshot source)
        {
            if (source == null)
            {
                return null;
            }

            CampusRuntimeTileSnapshot clone = new CampusRuntimeTileSnapshot();
            clone.Cell = source.Cell;
            clone.AssetName = source.AssetName;
            clone.PaletteIndex = source.PaletteIndex;
            clone.Transform = source.Transform;
            return clone;
        }

        private static bool AreTileSnapshotsEquivalent(CampusRuntimeTileSnapshot left, CampusRuntimeTileSnapshot right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return left.Cell == right.Cell &&
                   left.PaletteIndex == right.PaletteIndex &&
                   string.Equals(left.AssetName, right.AssetName, StringComparison.Ordinal) &&
                   left.Transform.Equals(right.Transform);
        }

        private void ApplyTiles(Tilemap tilemap, List<CampusRuntimeTileSnapshot> tiles, List<TileBase> palette)
        {
            if (tilemap == null || tiles == null)
            {
                return;
            }

            tilemap.ClearAllTiles();
            for (int i = 0; i < tiles.Count; i++)
            {
                CampusRuntimeTileSnapshot tileSnapshot = tiles[i];
                TileBase tile = ResolveTile(tileSnapshot, palette);
                if (tile == null)
                {
                    continue;
                }

                tilemap.SetTile(tileSnapshot.Cell, tile);
                tilemap.SetTileFlags(tileSnapshot.Cell, TileFlags.None);
                tilemap.SetTransformMatrix(tileSnapshot.Cell, HasUsableMatrix(tileSnapshot.Transform) ? tileSnapshot.Transform : Matrix4x4.identity);
            }

            tilemap.RefreshAllTiles();
        }

        private void ApplyTileSnapshotToCell(Tilemap tilemap, Vector3Int cell, CampusRuntimeTileSnapshot tileSnapshot, List<TileBase> palette)
        {
            if (tilemap == null)
            {
                return;
            }

            if (tileSnapshot == null)
            {
                tilemap.SetTile(cell, null);
                tilemap.SetTransformMatrix(cell, Matrix4x4.identity);
                return;
            }

            TileBase tile = ResolveTile(tileSnapshot, palette);
            if (tile == null)
            {
                tilemap.SetTile(cell, null);
                tilemap.SetTransformMatrix(cell, Matrix4x4.identity);
                return;
            }

            tilemap.SetTile(cell, tile);
            tilemap.SetTileFlags(cell, TileFlags.None);
            tilemap.SetTransformMatrix(cell, HasUsableMatrix(tileSnapshot.Transform) ? tileSnapshot.Transform : Matrix4x4.identity);
        }

        private void CaptureObjects(CampusFloorRoot floor, List<CampusRuntimeObjectSnapshot> output)
        {
            output.Clear();
            if (floor == null || floor.PropsRoot == null || floor.Grid == null)
            {
                return;
            }

            CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                CampusPlacedObject placed = objects[i];
                if (placed == null)
                {
                    continue;
                }

                placed.RefreshCellFromTransform(floor.Grid);
                placed.NormalizeCustomInteractionAnchors();
                output.Add(CreateObjectSnapshot(floor, placed));
            }
        }

        private CampusRuntimeObjectSnapshot CreateObjectSnapshot(CampusFloorRoot floor, CampusPlacedObject placed)
        {
            CampusRuntimeObjectSnapshot objectSnapshot = new CampusRuntimeObjectSnapshot();
            if (placed == null)
            {
                return objectSnapshot;
            }

            objectSnapshot.ObjectId = string.IsNullOrEmpty(placed.ObjectId) ? placed.gameObject.name : placed.ObjectId;
            objectSnapshot.TypeId = string.IsNullOrWhiteSpace(placed.TypeId) ? string.Empty : placed.TypeId.Trim();
            objectSnapshot.DisplayNameOverride = placed.DisplayNameOverride;
            objectSnapshot.PaletteIndex = FindPrefabIndexByName(objectSnapshot.ObjectId);
            objectSnapshot.Position = placed.transform.position;
            objectSnapshot.Cell = placed.Cell;
            objectSnapshot.FootprintSize = placed.NormalizedFootprintSize;
            objectSnapshot.FloorIndex = floor != null ? floor.FloorIndex : placed.FloorIndex;
            objectSnapshot.OverrideFootprintSize = placed.OverrideFootprintSize;
            objectSnapshot.VisualScale = placed.NormalizedVisualScale;
            objectSnapshot.LockVisualScaleAspect = placed.LockVisualScaleAspect;
            objectSnapshot.IsWallMounted = placed.IsWallMounted;
            objectSnapshot.OverrideAllowRotation = placed.OverrideAllowRotation;
            objectSnapshot.AllowRotation = placed.AllowRotation;
            objectSnapshot.OverrideRotation0Sprite = placed.OverrideRotation0Sprite;
            objectSnapshot.Rotation0SpritePath = NormalizeSerializedImportPath(placed.Rotation0SpritePath);
            objectSnapshot.OverrideRotation90Sprite = placed.OverrideRotation90Sprite;
            objectSnapshot.Rotation90SpritePath = NormalizeSerializedImportPath(placed.Rotation90SpritePath);
            objectSnapshot.OverrideRotation180Sprite = placed.OverrideRotation180Sprite;
            objectSnapshot.Rotation180SpritePath = NormalizeSerializedImportPath(placed.Rotation180SpritePath);
            objectSnapshot.OverrideRotation270Sprite = placed.OverrideRotation270Sprite;
            objectSnapshot.Rotation270SpritePath = NormalizeSerializedImportPath(placed.Rotation270SpritePath);
            objectSnapshot.Rotation90 = placed.Rotation90;
            objectSnapshot.BlocksMovement = placed.BlocksMovement;
            objectSnapshot.BlocksSight = placed.BlocksSight;
            objectSnapshot.IsInteractable = placed.IsInteractable;
            objectSnapshot.IsStorageContainer = placed.IsStorageContainer;
            objectSnapshot.StorageSize = placed.NormalizedStorageSize;
            objectSnapshot.StorageMaxWeight = placed.NormalizedStorageMaxWeight;
            objectSnapshot.UseCustomInteractionAnchor = placed.UseCustomInteractionAnchor;
            objectSnapshot.CustomInteractionAnchorLocalPosition = placed.CustomInteractionAnchorLocalPosition;
            objectSnapshot.CustomInteractionAnchorRadius = placed.CustomInteractionAnchorRadius;
            objectSnapshot.CustomInteractionPromptText = placed.CustomInteractionPromptText;
            objectSnapshot.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(placed.CustomInteractionAnchors);
            return objectSnapshot;
        }

        private static CampusRuntimeObjectSnapshot CloneObjectSnapshot(CampusRuntimeObjectSnapshot source)
        {
            CampusRuntimeObjectSnapshot clone = new CampusRuntimeObjectSnapshot();
            if (source == null)
            {
                return clone;
            }

            clone.ObjectId = source.ObjectId;
            clone.TypeId = source.TypeId;
            clone.DisplayNameOverride = source.DisplayNameOverride;
            clone.PaletteIndex = source.PaletteIndex;
            clone.Position = source.Position;
            clone.Cell = source.Cell;
            clone.FootprintSize = source.FootprintSize;
            clone.FloorIndex = source.FloorIndex;
            clone.OverrideFootprintSize = source.OverrideFootprintSize;
            clone.VisualScale = source.VisualScale;
            clone.LockVisualScaleAspect = source.LockVisualScaleAspect;
            clone.IsWallMounted = source.IsWallMounted;
            clone.OverrideAllowRotation = source.OverrideAllowRotation;
            clone.AllowRotation = source.AllowRotation;
            clone.OverrideRotation0Sprite = source.OverrideRotation0Sprite;
            clone.Rotation0SpritePath = source.Rotation0SpritePath;
            clone.OverrideRotation90Sprite = source.OverrideRotation90Sprite;
            clone.Rotation90SpritePath = source.Rotation90SpritePath;
            clone.OverrideRotation180Sprite = source.OverrideRotation180Sprite;
            clone.Rotation180SpritePath = source.Rotation180SpritePath;
            clone.OverrideRotation270Sprite = source.OverrideRotation270Sprite;
            clone.Rotation270SpritePath = source.Rotation270SpritePath;
            clone.Rotation90 = source.Rotation90;
            clone.BlocksMovement = source.BlocksMovement;
            clone.BlocksSight = source.BlocksSight;
            clone.IsInteractable = source.IsInteractable;
            clone.IsStorageContainer = source.IsStorageContainer;
            clone.StorageSize = source.StorageSize;
            clone.StorageMaxWeight = source.StorageMaxWeight;
            clone.UseCustomInteractionAnchor = source.UseCustomInteractionAnchor;
            clone.CustomInteractionAnchorLocalPosition = source.CustomInteractionAnchorLocalPosition;
            clone.CustomInteractionAnchorRadius = source.CustomInteractionAnchorRadius;
            clone.CustomInteractionPromptText = source.CustomInteractionPromptText;
            clone.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(source.CustomInteractionAnchors);
            return clone;
        }

        private void ApplyObjects(CampusFloorRoot floor, List<CampusRuntimeObjectSnapshot> objects)
        {
            if (floor == null || floor.PropsRoot == null || objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                CampusRuntimeObjectSnapshot objectSnapshot = objects[i];
                GameObject prefab = ResolvePrefab(objectSnapshot);
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(prefab, floor.PropsRoot);
                CampusSceneInstanceUtility.NormalizeSceneInstance(instance);
                instance.SetActive(true);
                string displayName = string.IsNullOrWhiteSpace(objectSnapshot.DisplayNameOverride)
                    ? GetObjectDisplayName(prefab)
                    : objectSnapshot.DisplayNameOverride.Trim();
                instance.name = displayName + "_F" + floor.FloorIndex + "_" + objectSnapshot.Cell.x + "_" + objectSnapshot.Cell.y;
                CampusPlacedObject placed = instance.GetComponent<CampusPlacedObject>();
                if (placed == null)
                {
                    placed = instance.AddComponent<CampusPlacedObject>();
                }

                placed.ObjectId = objectSnapshot.ObjectId;
                placed.TypeId = objectSnapshot.TypeId;
                placed.DisplayNameOverride = objectSnapshot.DisplayNameOverride;
                placed.FloorIndex = floor.FloorIndex;
                placed.Cell = objectSnapshot.Cell;
                placed.OverrideFootprintSize = objectSnapshot.OverrideFootprintSize;
                placed.FootprintSize = objectSnapshot.FootprintSize;
                placed.VisualScale = CampusPlacedObject.NormalizeVisualScale(objectSnapshot.VisualScale);
                placed.LockVisualScaleAspect = objectSnapshot.LockVisualScaleAspect;
                placed.IsWallMounted = objectSnapshot.IsWallMounted;
                if (placed.IsWallMounted)
                {
                    placed.OverrideFootprintSize = true;
                    placed.FootprintSize = Vector2Int.one;
                    placed.OverrideAllowRotation = true;
                    placed.AllowRotation = true;
                    placed.SortingOrderOffset = Mathf.Max(placed.SortingOrderOffset, 1);
                    placed.BlocksMovement = false;
                    placed.BlocksSight = false;
                }

                if (objectSnapshot.OverrideAllowRotation)
                {
                    placed.OverrideAllowRotation = true;
                    placed.AllowRotation = objectSnapshot.AllowRotation;
                }

                if (placed.IsWallMounted)
                {
                    placed.OverrideAllowRotation = true;
                    placed.AllowRotation = true;
                }

                AssignRuntimeObjectDirectionSprite(placed, 0, objectSnapshot.OverrideRotation0Sprite, objectSnapshot.Rotation0SpritePath, objectSnapshot.ObjectId);
                AssignRuntimeObjectDirectionSprite(placed, 1, objectSnapshot.OverrideRotation90Sprite, objectSnapshot.Rotation90SpritePath, objectSnapshot.ObjectId);
                AssignRuntimeObjectDirectionSprite(placed, 2, objectSnapshot.OverrideRotation180Sprite, objectSnapshot.Rotation180SpritePath, objectSnapshot.ObjectId);
                AssignRuntimeObjectDirectionSprite(placed, 3, objectSnapshot.OverrideRotation270Sprite, objectSnapshot.Rotation270SpritePath, objectSnapshot.ObjectId);
                placed.Rotation90 = objectSnapshot.Rotation90;
                if (placed.AllowRotation)
                {
                    placed.ApplyRotationVisualState();
                }
                else
                {
                    instance.transform.rotation = Quaternion.Euler(0f, 0f, objectSnapshot.Rotation90 * 90f);
                    placed.ApplyVisualScaleState();
                }
                placed.BlocksMovement = objectSnapshot.BlocksMovement;
                placed.BlocksSight = objectSnapshot.BlocksSight;
                placed.IsInteractable = objectSnapshot.IsInteractable;
                placed.IsStorageContainer = objectSnapshot.IsStorageContainer;
                placed.StorageSize = CampusPlacedObject.NormalizeStorageSize(objectSnapshot.StorageSize);
                placed.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(objectSnapshot.StorageMaxWeight);
                placed.UseCustomInteractionAnchor = objectSnapshot.UseCustomInteractionAnchor;
                placed.CustomInteractionAnchorLocalPosition = objectSnapshot.CustomInteractionAnchorLocalPosition;
                placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(objectSnapshot.CustomInteractionAnchorRadius);
                placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(objectSnapshot.CustomInteractionPromptText)
                    ? Tr("\u4ea4\u4e92", "Interact")
                    : objectSnapshot.CustomInteractionPromptText;
                placed.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(objectSnapshot.CustomInteractionAnchors);
                placed.ApplyInteractionState();
                if (floor.Grid != null)
                {
                    placed.ApplyCellToTransform(floor.Grid);
                }
                else
                {
                    instance.transform.position = objectSnapshot.Position;
                }

                placed.EnsureShadowRegistration();
                CampusDynamicShadowUtility.EnsureObjectShadowCasters(placed, floor.Grid);
            }

            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            NtingCustomShadowSystem.EnsureSceneSystem().RefreshNow();
        }

        private void CaptureStairs(CampusFloorRoot floor, List<CampusRuntimeStairSnapshot> output)
        {
            output.Clear();
            if (floor == null || floor.StairsRoot == null)
            {
                return;
            }

            CampusStairLink[] stairs = floor.StairsRoot.GetComponentsInChildren<CampusStairLink>(true);
            for (int i = 0; i < stairs.Length; i++)
            {
                CampusStairLink stair = stairs[i];
                if (stair == null)
                {
                    continue;
                }

                CampusRuntimeStairSnapshot stairSnapshot = new CampusRuntimeStairSnapshot();
                stairSnapshot.FromFloor = stair.FromFloor;
                stairSnapshot.ToFloor = stair.ToFloor;
                stairSnapshot.FromCell = stair.FromCell;
                stairSnapshot.ToCell = stair.ToCell;
                stairSnapshot.SecondaryCell = stair.GetSecondaryCell();
                stairSnapshot.Rotation90 = stair.Rotation90;
                stairSnapshot.LinkId = stair.LinkId;
                stairSnapshot.IsAutoReturnStair = stair.IsAutoReturnStair;
                output.Add(stairSnapshot);
            }
        }

        private void ApplyStairs(CampusFloorRoot floor, List<CampusRuntimeStairSnapshot> stairs)
        {
            if (floor == null || stairs == null || stairPrefab == null)
            {
                return;
            }

            for (int i = 0; i < stairs.Count; i++)
            {
                CampusRuntimeStairSnapshot stairSnapshot = stairs[i];
                CreateStairInstance(
                    stairPrefab,
                    floor,
                    stairSnapshot.FromFloor,
                    stairSnapshot.ToFloor,
                    stairSnapshot.FromCell,
                    stairSnapshot.SecondaryCell,
                    stairSnapshot.ToCell,
                    stairSnapshot.Rotation90,
                    stairSnapshot.LinkId,
                    stairSnapshot.IsAutoReturnStair);
            }
        }

        private void CaptureRooms(CampusFloorRoot floor, List<CampusRuntimeRoomSnapshot> output)
        {
            output.Clear();
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                if (!TryResolvePresetRoomName(marker.RoomName, out string roomName))
                {
                    continue;
                }

                CampusRuntimeRoomSnapshot roomSnapshot = new CampusRuntimeRoomSnapshot();
                roomSnapshot.RoomName = roomName;
                roomSnapshot.FloorIndex = floor.FloorIndex;
                roomSnapshot.Cell = marker.Cell;
                roomSnapshot.HideMarkerVisual = marker.HideMarkerVisual;
                output.Add(roomSnapshot);
            }
        }

        private void ApplyRooms(CampusFloorRoot floor, List<CampusRuntimeRoomSnapshot> rooms)
        {
            if (floor == null || rooms == null || floor.PropsRoot == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                CampusRuntimeRoomSnapshot roomSnapshot = rooms[i];
                if (roomSnapshot == null || !TryResolvePresetRoomName(roomSnapshot.RoomName, out string roomName))
                {
                    continue;
                }

                GameObject markerObject = new GameObject("Room_" + roomName + "_F" + floor.FloorIndex + "_" + roomSnapshot.Cell.x + "_" + roomSnapshot.Cell.y);
                markerObject.transform.SetParent(floor.PropsRoot, false);
                if (floor.Grid != null)
                {
                    markerObject.transform.position = floor.Grid.GetCellCenterWorld(roomSnapshot.Cell);
                }

                CampusRuntimeRoomMarker marker = markerObject.AddComponent<CampusRuntimeRoomMarker>();
                marker.RoomName = roomName;
                marker.FloorIndex = floor.FloorIndex;
                marker.Cell = roomSnapshot.Cell;
                marker.HideMarkerVisual = roomSnapshot.HideMarkerVisual;
                if (!marker.HideMarkerVisual)
                {
                    AddRoomMarkerVisual(markerObject, floor);
                }
            }
        }

        private void CaptureLights(List<CampusRuntimeLightSnapshot> output)
        {
            output.Clear();
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (!IsRuntimeEditableLight(light))
                {
                    continue;
                }

                output.Add(CreateLightSnapshot(light));
            }
        }

        private CampusRuntimeLightSnapshot CreateLightSnapshot(Light2D light)
        {
            CampusRuntimeLightSnapshot lightSnapshot = new CampusRuntimeLightSnapshot();
            if (light == null)
            {
                return lightSnapshot;
            }

            lightSnapshot.Name = light.gameObject.name;
            lightSnapshot.LightType = light.lightType.ToString();
            lightSnapshot.Position = light.transform.position;
            lightSnapshot.Rotation = light.transform.eulerAngles;
            lightSnapshot.Color = light.color;
            lightSnapshot.Intensity = light.intensity;
            lightSnapshot.InnerRadius = light.pointLightInnerRadius;
            lightSnapshot.OuterRadius = light.pointLightOuterRadius;
            lightSnapshot.InnerAngle = light.pointLightInnerAngle;
            lightSnapshot.OuterAngle = light.pointLightOuterAngle;
            lightSnapshot.FalloffIntensity = light.falloffIntensity;
            lightSnapshot.ShadowsEnabled = light.shadowsEnabled;
            lightSnapshot.ShadowIntensity = light.shadowIntensity;
            lightSnapshot.ShadowSoftness = light.shadowSoftness;
            lightSnapshot.ShadowSoftnessFalloff = light.shadowSoftnessFalloffIntensity;
            ResolveLightCell(light, out lightSnapshot.FloorIndex, out lightSnapshot.Cell);
            return lightSnapshot;
        }

        private static CampusRuntimeLightSnapshot CloneLightSnapshot(CampusRuntimeLightSnapshot source)
        {
            CampusRuntimeLightSnapshot clone = new CampusRuntimeLightSnapshot();
            if (source == null)
            {
                return clone;
            }

            clone.Name = source.Name;
            clone.LightType = source.LightType;
            clone.Position = source.Position;
            clone.Rotation = source.Rotation;
            clone.Color = source.Color;
            clone.Intensity = source.Intensity;
            clone.InnerRadius = source.InnerRadius;
            clone.OuterRadius = source.OuterRadius;
            clone.InnerAngle = source.InnerAngle;
            clone.OuterAngle = source.OuterAngle;
            clone.FalloffIntensity = source.FalloffIntensity;
            clone.ShadowsEnabled = source.ShadowsEnabled;
            clone.ShadowIntensity = source.ShadowIntensity;
            clone.ShadowSoftness = source.ShadowSoftness;
            clone.ShadowSoftnessFalloff = source.ShadowSoftnessFalloff;
            clone.FloorIndex = source.FloorIndex;
            clone.Cell = source.Cell;
            return clone;
        }

        private void ApplyLights(List<CampusRuntimeLightSnapshot> lights)
        {
            if (lights == null || lights.Count == 0)
            {
                return;
            }

            for (int i = 0; i < lights.Count; i++)
            {
                CampusRuntimeLightSnapshot lightSnapshot = lights[i];
                CreateLightInstance(lightSnapshot);
            }
        }

        private Light2D CreateLightInstance(CampusRuntimeLightSnapshot lightSnapshot)
        {
            if (lightSnapshot == null)
            {
                return null;
            }

            GameObject lightObject = new GameObject(string.IsNullOrEmpty(lightSnapshot.Name) ? "Runtime Light" : lightSnapshot.Name);
            lightObject.transform.position = lightSnapshot.Position;
            lightObject.transform.rotation = Quaternion.Euler(lightSnapshot.Rotation);
            Light2D light = lightObject.AddComponent<Light2D>();
            Light2D.LightType type;
            if (!Enum.TryParse(lightSnapshot.LightType, out type))
            {
                type = Light2D.LightType.Point;
            }

            if (type != Light2D.LightType.Point)
            {
                return null;
            }

            light.lightType = type;
            light.blendStyleIndex = 0;
            light.targetSortingLayers = GetAllSortingLayerIds();
            light.color = lightSnapshot.Color;
            light.intensity = lightSnapshot.Intensity;
            CampusDynamicShadowUtility.ConfigureLightShadows(light, type != Light2D.LightType.Global && lightSnapshot.ShadowsEnabled, lightSnapshot.ShadowIntensity, lightSnapshot.ShadowSoftness, lightSnapshot.ShadowSoftnessFalloff);
            if (type == Light2D.LightType.Point)
            {
                light.pointLightInnerRadius = Mathf.Max(0f, lightSnapshot.InnerRadius);
                light.pointLightOuterRadius = Mathf.Max(light.pointLightInnerRadius + 0.1f, lightSnapshot.OuterRadius);
                light.pointLightInnerAngle = lightSnapshot.InnerAngle <= 0f ? 360f : lightSnapshot.InnerAngle;
                light.pointLightOuterAngle = lightSnapshot.OuterAngle <= 0f ? 360f : lightSnapshot.OuterAngle;
                light.falloffIntensity = lightSnapshot.FalloffIntensity;
            }

            return light;
        }

        private void ClearFloorAuthoredContent(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            if (floor.FloorTilemap != null)
            {
                floor.FloorTilemap.ClearAllTiles();
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic != null)
            {
                wallLogic.ClearAllTiles();
            }

            CampusWallAutoRenderer.ClearVisualLayers(floor);
            DestroyFloorAuthoredProps(floor.PropsRoot);
            DestroyChildren(floor.StairsRoot);
            floor.MarkUsedBoundsDirty();
        }

        private void ApplySnapshotLights(List<CampusRuntimeLightSnapshot> lightSnapshots)
        {
            ClearLightsReplacedBySnapshot(lightSnapshots);
            ApplyLights(lightSnapshots);
        }

        private void ClearLightsReplacedBySnapshot(List<CampusRuntimeLightSnapshot> lightSnapshots)
        {
            HashSet<string> snapshotLightNames = new HashSet<string>(StringComparer.Ordinal);
            if (lightSnapshots != null)
            {
                for (int i = 0; i < lightSnapshots.Count; i++)
                {
                    CampusRuntimeLightSnapshot lightSnapshot = lightSnapshots[i];
                    if (lightSnapshot == null || string.IsNullOrWhiteSpace(lightSnapshot.Name))
                    {
                        continue;
                    }

                    string lightName = lightSnapshot.Name.Trim();
                    snapshotLightNames.Add(lightName);
                }
            }

            Light2D[] sceneLights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = sceneLights.Length - 1; i >= 0; i--)
            {
                Light2D light = sceneLights[i];
                if (light == null || IsManagedSceneLight(light))
                {
                    continue;
                }

                string lightName = light.gameObject.name;
                bool isReplacedBySnapshot = snapshotLightNames.Contains(lightName);

                if (isReplacedBySnapshot)
                {
                    DestroyRuntimeObject(light.gameObject);
                }
            }

            selectedLight = null;
        }

        private void ClearRuntimeEditableLights()
        {
            Light2D[] sceneLights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = sceneLights.Length - 1; i >= 0; i--)
            {
                Light2D light = sceneLights[i];
                if (!IsRuntimeEditableLight(light))
                {
                    continue;
                }

                DestroyRuntimeObject(light.gameObject);
            }

            selectedLight = null;
        }

        private static bool IsManagedSceneLight(Light2D light)
        {
            if (light == null)
            {
                return false;
            }

            return CampusObjectNames.MatchesAny(
                light.gameObject.name,
                CampusObjectNames.GlobalLight2D,
                CampusObjectNames.LegacyGlobalLight2D,
                CampusObjectNames.SunLight2D,
                CampusObjectNames.LegacySunLight2D);
        }

        private static bool IsRuntimeEditableLight(Light2D light)
        {
            return light != null &&
                   light.lightType == Light2D.LightType.Point &&
                   !IsManagedSceneLight(light);
        }

        public bool IsOpen => isOpen;

        private void ResolveLayoutRects()
        {
            float toolbarY = Mathf.Max(Screen.height - BottomToolbarHeight - PanelMargin, TopMargin + 340f);
            bottomToolbarRect = new Rect(PanelMargin, toolbarY, Mathf.Max(360f, Screen.width - PanelMargin * 2f), BottomToolbarHeight);

            float rightWidth = Mathf.Clamp(Screen.width * 0.2f, 320f, 420f);
            float availableLeftWidth = Screen.width - rightWidth - PanelMargin * 3f;
            float leftWidth = Mathf.Clamp(Screen.width * 0.28f, 360f, 520f);
            leftWidth = Mathf.Min(leftWidth, Mathf.Max(320f, availableLeftWidth));
            float panelHeight = Mathf.Max(340f, toolbarY - TopMargin - PanelMargin);
            leftPanelRect = new Rect(PanelMargin, TopMargin, leftWidth, panelHeight);

            float rightX = Mathf.Max(leftPanelRect.xMax + PanelMargin, Screen.width - rightWidth - PanelMargin);
            rightWidth = Mathf.Min(rightWidth, Screen.width - rightX - PanelMargin);
            if (rightWidth < 280f)
            {
                rightWidth = 280f;
                rightX = Screen.width - rightWidth - PanelMargin;
            }

            float floorHeight = Mathf.Clamp(Screen.height * 0.24f, 220f, 320f);
            floorPanelRect = new Rect(rightX, TopMargin, rightWidth, floorHeight);
            float checklistY = floorPanelRect.yMax + 18f;
            float checklistHeight = Mathf.Max(220f, toolbarY - checklistY - PanelMargin);
            checklistPanelRect = new Rect(rightX, checklistY, rightWidth, checklistHeight);
            settingsPanelRect = new Rect(Mathf.Clamp(Screen.width - 460f - PanelMargin, PanelMargin, Screen.width - 460f), Mathf.Max(TopMargin, toolbarY - 390f), 430f, Mathf.Min(360f, toolbarY - TopMargin - PanelMargin));
            float objectSettingsWidth = Mathf.Clamp(Screen.width * 0.36f, 520f, 760f);
            float objectSettingsHeight = Mathf.Clamp(toolbarY - TopMargin - PanelMargin, 500f, 800f);
            float objectSettingsX = leftPanelRect.xMax + PanelMargin;
            float objectSettingsRightLimit = floorPanelRect.x - PanelMargin;
            if (objectSettingsRightLimit - objectSettingsX < objectSettingsWidth)
            {
                objectSettingsX = Mathf.Clamp((Screen.width - objectSettingsWidth) * 0.5f, PanelMargin, Mathf.Max(PanelMargin, Screen.width - objectSettingsWidth - PanelMargin));
            }

            objectSettingsPanelRect = new Rect(objectSettingsX, TopMargin, objectSettingsWidth, Mathf.Min(objectSettingsHeight, toolbarY - TopMargin - PanelMargin));
            helpPanelRect = new Rect(Mathf.Max(PanelMargin, Screen.width * 0.5f - 360f), TopMargin + 30f, Mathf.Min(720f, Screen.width - PanelMargin * 2f), Mathf.Min(480f, toolbarY - TopMargin - 56f));
        }

        private void DrawLeftPanel()
        {
            GUI.Box(leftPanelRect, GUIContent.none, panelStyle);
            Rect tabRect = new Rect(leftPanelRect.x + 14f, leftPanelRect.y + 12f, leftPanelRect.width - 28f, 54f);
            float tabGap = 8f;
            float tabWidth = (tabRect.width - tabGap * 4f) / 5f;
            DrawTabButton(new Rect(tabRect.x, tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Build, Tr("\u5efa\u9020", "Build"));
            DrawTabButton(new Rect(tabRect.x + (tabWidth + tabGap), tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Rooms, Tr("\u623f\u95f4", "Rooms"));
            DrawTabButton(new Rect(tabRect.x + (tabWidth + tabGap) * 2f, tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Gameplay, Tr("\u533a\u57df", "Areas"));
            DrawTabButton(new Rect(tabRect.x + (tabWidth + tabGap) * 3f, tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Objects, Tr("\u7269\u4ef6", "Objects"));
            DrawTabButton(new Rect(tabRect.x + (tabWidth + tabGap) * 4f, tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Lighting, Tr("\u706f\u5149", "Lighting"));

            Rect titleRect = new Rect(leftPanelRect.x + 18f, leftPanelRect.y + 76f, leftPanelRect.width - 36f, 48f);
            GUI.Box(titleRect, GetActiveTabTitle(), headerStyle);

            Rect contentRect = new Rect(leftPanelRect.x + 16f, leftPanelRect.y + 132f, leftPanelRect.width - 32f, leftPanelRect.height - 150f);
            switch (activeTab)
            {
                case CampusRuntimeEditorTab.Build:
                    DrawBuildTab(contentRect);
                    break;
                case CampusRuntimeEditorTab.Rooms:
                    DrawRoomTab(contentRect);
                    break;
                case CampusRuntimeEditorTab.Gameplay:
                    DrawGameplayTab(contentRect);
                    break;
                case CampusRuntimeEditorTab.Objects:
                    DrawObjectTab(contentRect);
                    break;
                case CampusRuntimeEditorTab.Lighting:
                    DrawLightingTab(contentRect);
                    break;
            }
        }

        private void DrawTabButton(Rect rect, CampusRuntimeEditorTab tab, string label)
        {
            GUIStyle style = activeTab == tab ? selectedButtonStyle : iconButtonStyle;
            if (GUI.Button(rect, label, style))
            {
                activeTab = tab;
                switch (tab)
                {
                    case CampusRuntimeEditorTab.Build:
                        brushMode = CampusRuntimeBrushMode.PaintFloor;
                        break;
                    case CampusRuntimeEditorTab.Rooms:
                        brushMode = roomPrefabs.Count > 0
                            ? CampusRuntimeBrushMode.PlaceRoomPrefab
                            : CampusRuntimeBrushMode.CreateRoomPrefab;
                        break;
                    case CampusRuntimeEditorTab.Gameplay:
                        brushMode = CampusRuntimeBrushMode.PlaceRoom;
                        break;
                    case CampusRuntimeEditorTab.Objects:
                        brushMode = CampusRuntimeBrushMode.PlaceObject;
                        break;
                    case CampusRuntimeEditorTab.Lighting:
                        brushMode = CampusRuntimeBrushMode.PlaceLight;
                        break;
                }
            }
        }

        private string GetActiveTabTitle()
        {
            switch (activeTab)
            {
                case CampusRuntimeEditorTab.Build:
                    return Tr("\u5efa\u9020", "Build");
                case CampusRuntimeEditorTab.Rooms:
                    return Tr("\u623f\u95f4", "Rooms");
                case CampusRuntimeEditorTab.Gameplay:
                    return Tr("\u533a\u57df", "Areas");
                case CampusRuntimeEditorTab.Objects:
                    return Tr("\u7269\u4ef6", "Objects");
                case CampusRuntimeEditorTab.Lighting:
                    return Tr("\u706f\u5149", "Lighting");
                default:
                    return Tr("\u5efa\u9020", "Build");
            }
        }

        private float GetBuildContentHeight(float width)
        {
            float height = 34f + 40f * 3f + 8f + 38f;
            height += 80f * 2f + 8f;
            height += 250f;
            height += 34f + GetTileGridHeight(floorTiles.Count, width);
            height += 10f + 34f + GetTileGridHeight(wallTiles.Count, width);
            if (wallProfiles.Count > 0)
            {
                height += 10f + 34f + wallProfiles.Count * 36f;
            }

            return height + 36f;
        }

        private float GetRoomContentHeight(float width)
        {
            float height = 34f + 40f * 1f;
            height += 34f + 38f + 42f + (roomPrefabs.Count == 0 ? 66f : roomPrefabs.Count * 42f) + 46f;
            return height;
        }

        private float GetGameplayContentHeight(float width)
        {
            int pointCount = GameplayMarkerPresets.Length;
            float height = 34f + 40f + 12f;
            height += 34f + 38f + 80f + 8f;
            height += roomNames.Count == 0 ? 66f : roomNames.Count * 40f;
            height += 12f + (roomNames.Count > 0 ? 42f : 0f) + 70f;
            height += 34f + 40f + Mathf.CeilToInt(pointCount / 2f) * 38f + 18f;
            height += 96f;
            return height;
        }

        private float GetObjectContentHeight(float width)
        {
            float height = 34f + 40f * 2f + 8f + 44f;
            height += 46f;
            height += 258f;
            height += 80f + 8f;
            height += 34f + GetPrefabGridHeight(objectPrefabs.Count, width);
            if (stairPrefab == null)
            {
                height += 76f;
            }

            return height + 36f;
        }

        private float GetLightingContentHeight(float width)
        {
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int editableLightCount = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                if (IsRuntimeEditableLight(lights[i]))
                {
                    editableLightCount++;
                }
            }

            float height = 34f + 40f * 2f + 6f + 44f + 30f + 92f + 30f * 5f + 128f + 130f + 34f + editableLightCount * 38f;
            if (selectedLight != null)
            {
                height += 370f;
            }

            return height + 110f;
        }

        private float GetTileGridHeight(int count, float width)
        {
            if (count == 0)
            {
                return 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            int rows = Mathf.CeilToInt((float)count / columns);
            return rows * (PaletteTileSize + 22f);
        }

        private float GetPrefabGridHeight(int count, float width)
        {
            return GetTileGridHeight(count, width);
        }

        private void DrawBuildTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetBuildContentHeight(viewWidth)));
            leftScroll = GUI.BeginScrollView(contentRect, leftScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.BuildTools), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PaintFloor, CampusRuntimeBrushMode.PaintWall },
                new string[]
                {
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Pan),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PaintFloor),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PaintWall)
                });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.RectangleFloor, CampusRuntimeBrushMode.RectangleWall, CampusRuntimeBrushMode.RectangleErase },
                new string[]
                {
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RectFloor),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RectWall),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RectErase)
                });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Erase, CampusRuntimeBrushMode.Pick },
                new string[]
                {
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Erase),
                    CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Pick)
                });

            y += 8f;
            GUI.Label(new Rect(0f, y, 90f, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.BrushSize), bodyStyle);
            brushSize = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(90f, y + 8f, viewRect.width - 150f, 20f), brushSize, 1f, 8f));
            GUI.Label(new Rect(viewRect.width - 50f, y, 50f, 24f), brushSize.ToString(), bodyStyle);
            y += 38f;

            DrawImportFolderRow(ref y, viewRect.width, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.FloorImports), GetFloorImportFolder(), CampusRuntimeImportTarget.Floor);
            DrawImportFolderRow(ref y, viewRect.width, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallImports), GetWallImportFolder(), CampusRuntimeImportTarget.Wall);
            y += 8f;

            DrawCustomWallPanel(ref y, viewRect.width);
            y += 8f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.FloorPalette), headerStyle);
            y += 34f;
            y = DrawTilePaletteGrid(floorTiles, selectedFloorTileIndex, y, viewRect.width,
                delegate(int index)
                {
                    selectedFloorTileIndex = index;
                    brushMode = CampusRuntimeBrushMode.PaintFloor;
                },
                delegate(int index)
                {
                    DeleteImportedTileResource(GetFloorImportFolder(), floorTiles, index, "floor");
                });

            y += 10f;
            GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallPalette), headerStyle);
            y += 34f;
            y = DrawTilePaletteGrid(wallTiles, selectedWallTileIndex, y, viewRect.width,
                delegate(int index)
                {
                    selectedWallTileIndex = index;
                    brushMode = CampusRuntimeBrushMode.PaintWall;
                },
                delegate(int index)
                {
                    DeleteImportedTileResource(GetWallImportFolder(), wallTiles, index, "wall");
                });

            if (wallProfiles.Count > 0)
            {
                y += 10f;
                GUI.Label(new Rect(0f, y, viewRect.width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallProfiles), headerStyle);
                y += 34f;
                for (int i = 0; i < wallProfiles.Count; i++)
                {
                    CampusWallRenderProfile profile = wallProfiles[i];
                    string label = profile != null ? CampusObjectNames.GetDisplayName(profile.name) : "Missing Profile";
                    if (GUI.Button(new Rect(0f, y, viewRect.width, 32f), label, i == selectedWallProfileIndex ? selectedButtonStyle : buttonStyle))
                    {
                        selectedWallProfileIndex = i;
                        fallbackWallProfile = profile;
                        PrepareRuntimeMapPresentationSafe();
                    }

                    y += 36f;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawRoomTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetRoomContentHeight(viewWidth)));
            leftScroll = GUI.BeginScrollView(contentRect, leftScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u6a21\u5757\u5de5\u5177", "Module Tools"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.CreateRoomPrefab, CampusRuntimeBrushMode.PlaceRoomPrefab },
                new string[] { Tr("\u6846\u9009\u6a21\u5757", "Box Module"), Tr("\u653e\u7f6e\u6a21\u5757", "Place Module") });

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u623f\u95f4\u6a21\u5757", "Room Modules"), headerStyle);
            y += 34f;
            GUI.Label(new Rect(0f, y, 64f, 30f), Tr("\u6a21\u5757", "Module"), bodyStyle);
            newRoomPrefabName = DrawTextInput(new Rect(66f, y, viewRect.width - 178f, 30f), newRoomPrefabName, "room_prefab_name");
            if (GUI.Button(new Rect(viewRect.width - 104f, y, 104f, 30f), Tr("\u6846\u9009\u6a21\u5757", "Box Module"), buttonStyle))
            {
                brushMode = CampusRuntimeBrushMode.CreateRoomPrefab;
                activeTab = CampusRuntimeEditorTab.Rooms;
                SetStatus(Tr("\u62d6\u51fa\u77e9\u5f62\u533a\u57df\uff0c\u677e\u5f00\u540e\u4fdd\u5b58\u4e3a\u623f\u95f4\u6a21\u5757\u3002", "Drag a rectangle area, then release to save it as a room module."));
            }

            y += 38f;
            float moduleButtonWidth = Mathf.Max(72f, (viewRect.width - 16f) / 3f);
            if (GUI.Button(new Rect(0f, y, moduleButtonWidth, 30f), Tr(CampusRuntimeEditorTextId.OpenFolder), buttonStyle))
            {
                OpenImportLocation(GetRoomPrefabFolder());
            }

            if (GUI.Button(new Rect(moduleButtonWidth + 8f, y, moduleButtonWidth, 30f), Tr(CampusRuntimeEditorTextId.Refresh), buttonStyle))
            {
                LoadImportedRoomPrefabs();
                SchedulePlayerMapSave();
                SetStatus(Tr("\u623f\u95f4\u6a21\u5757\u5df2\u5237\u65b0\u3002", "Room modules refreshed."));
            }

            if (roomPrefabs.Count > 0 && GUI.Button(new Rect((moduleButtonWidth + 8f) * 2f, y, moduleButtonWidth, 30f), Tr(CampusRuntimeEditorTextId.Delete), buttonStyle))
            {
                DeleteSelectedRoomPrefab();
            }

            y += 42f;
            if (roomPrefabs.Count == 0)
            {
                GUI.Label(new Rect(0f, y, viewRect.width, 58f), Tr("\u5c1a\u65e0\u623f\u95f4\u6a21\u5757\u3002\u8f93\u5165\u6a21\u5757\u540d\uff0c\u70b9\u51fb\u6846\u9009\u6a21\u5757\uff0c\u518d\u5728\u5730\u56fe\u4e0a\u62d6\u51fa\u533a\u57df\u3002", "No room modules exist. Enter a module name, click Box Module, then drag an area on the map."), mutedStyle);
                y += 66f;
            }
            else
            {
                for (int i = 0; i < roomPrefabs.Count; i++)
                {
                    CampusRuntimeRoomPrefab roomPrefab = roomPrefabs[i];
                    string label = roomPrefab.RoomName + "  " + roomPrefab.Size.x + "x" + roomPrefab.Size.y;
                    if (GUI.Button(new Rect(0f, y, viewRect.width, 34f), label, i == selectedRoomPrefabIndex ? selectedButtonStyle : buttonStyle))
                    {
                        selectedRoomPrefabIndex = i;
                        brushMode = CampusRuntimeBrushMode.PlaceRoomPrefab;
                    }

                    y += 42f;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawGameplayTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetGameplayContentHeight(viewWidth)));
            leftScroll = GUI.BeginScrollView(contentRect, leftScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u533a\u57df", "Areas"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[]
                {
                    CampusRuntimeBrushMode.PlaceRoom,
                    CampusRuntimeBrushMode.RectangleRoom
                },
                new string[] { Tr("\u6807\u8bb0\u533a\u57df", "Mark Area"), Tr("\u6846\u9009\u533a\u57df", "Box Area") });

            y += 12f;
            GUI.Label(
                new Rect(0f, y, viewRect.width, 44f),
                Tr("\u533a\u57df\u7c7b\u578b\u5df2\u7531\u9879\u76ee\u9884\u8bbe\u9501\u5b9a\uff1a\u53ea\u80fd\u9009\u62e9\u4e0b\u65b9\u533a\u57df\uff0c\u4e0d\u518d\u652f\u6301\u73a9\u5bb6\u81ea\u5b9a\u4e49\u533a\u57df\u540d\u3002", "Area types are locked to project presets. Choose one below; custom player area names are no longer supported."),
                mutedStyle);
            y += 52f;

            if (roomNames.Count == 0)
            {
                GUI.Label(new Rect(0f, y, viewRect.width, 58f), Tr("\u5c1a\u65e0\u533a\u57df\u9884\u8bbe\u3002", "No area presets exist."), mutedStyle);
                y += 66f;
            }

            for (int i = 0; i < roomNames.Count; i++)
            {
                Rect rowRect = new Rect(0f, y, viewRect.width, 34f);
                int count = CountRoomMarkers(roomNames[i]);
                int required = roomRequiredCounts[i];
                if (GUI.Button(rowRect, GUIContent.none, i == selectedRoomIndex ? selectedButtonStyle : buttonStyle))
                {
                    selectedRoomIndex = i;
                    brushMode = CampusRuntimeBrushMode.PlaceRoom;
                }

                DrawAreaDefinitionRow(rowRect, roomNames[i], count, required);
                y += 40f;
            }

            y += 12f;
            GUI.Label(new Rect(0f, y, viewRect.width, 48f), Tr("\u533a\u57df\u68c0\u67e5\u6e05\u5355\u4f1a\u5b9e\u65f6\u7edf\u8ba1\u8fd0\u884c\u65f6\u6807\u8bb0\uff0c\u5e76\u968f JSON \u5bfc\u51fa\u3002", "The area checklist counts runtime area markers in real time and is included in exported JSON."), mutedStyle);
            y += 58f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u8bbe\u65bd\u70b9", "Facility Points"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.PlaceGameplayMarker, CampusRuntimeBrushMode.EraseGameplayMarker },
                new string[] { Tr("\u653e\u7f6e\u8bbe\u65bd\u70b9", "Place Facility Point"), Tr("\u5220\u9664\u8bbe\u65bd\u70b9", "Erase Facility Point") });
            DrawGameplayPresetSection(ref y, viewRect.width);

            y += 8f;
            Rect noteRect = new Rect(0f, y, viewRect.width, 76f);
            GUI.Label(
                noteRect,
                Tr("\u533a\u57df\u4fdd\u5b58\u5728\u5730\u56fe JSON\uff0c\u8bbe\u65bd\u70b9\u4fdd\u5b58\u5728\u540c\u76ee\u5f55\u7684 .gameplay.json\u3002\u5148\u6846\u9009\u533a\u57df\uff0c\u518d\u653e\u7f6e\u6559\u5ba4\u3001\u529e\u516c\u5ba4\u3001\u98df\u5802\u3001\u8d85\u5e02\u3001\u5bbf\u820d\u7b49\u8bbe\u65bd\u70b9\u3002", "Areas are saved in the map JSON. Facility points are saved next to the map as .gameplay.json. Box areas first, then place classroom, office, canteen, store, dormitory, and other facility points."),
                mutedStyle);
            y += 84f;

            float halfWidth = (viewRect.width - 8f) * 0.5f;
            if (GUI.Button(new Rect(0f, y, halfWidth, 30f), Tr("\u4fdd\u5b58\u8bbe\u65bd\u70b9", "Save Facility Points"), buttonStyle))
            {
                SaveGameplayOverlayForCurrentSource(true);
            }

            if (GUI.Button(new Rect(halfWidth + 8f, y, halfWidth, 30f), Tr("\u91cd\u8bfb\u8bbe\u65bd\u70b9", "Reload Facility Points"), buttonStyle))
            {
                ReloadGameplayOverlayForCurrentSource(true);
            }

            GUI.EndScrollView();
        }

        private void DrawObjectTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetObjectContentHeight(viewWidth)));
            objectScroll = GUI.BeginScrollView(contentRect, objectScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u7269\u4ef6\u5de5\u5177", "Object Tools"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PlaceObject, CampusRuntimeBrushMode.PlaceStair },
                new string[] { Tr(CampusRuntimeEditorTextId.Pan), Tr("\u653e\u7f6e\u7269\u4ef6", "Place Object"), Tr("\u653e\u7f6e\u697c\u68af", "Place Stair") });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Erase, CampusRuntimeBrushMode.Pick },
                new string[] { Tr(CampusRuntimeEditorTextId.Erase), Tr(CampusRuntimeEditorTextId.Pick) });

            y += 8f;
            GUI.Label(new Rect(0f, y, 70f, 24f), Tr("\u65cb\u8f6c", "Rotation"), bodyStyle);
            if (GUI.Button(new Rect(72f, y, 84f, 30f), TrFormat("{0} \u5ea6", "{0} deg", rotation90 * 90), buttonStyle))
            {
                rotation90 = (rotation90 + 1) % 4;
            }

            GUI.Label(new Rect(170f, y, 76f, 24f), Tr("\u76ee\u6807\u697c\u5c42", "Target Floor"), bodyStyle);
            stairTargetFloorIndex = Mathf.Clamp(ParseIntField(new Rect(246f, y, 58f, 30f), stairTargetFloorIndex), 1, 99);
            y += 44f;

            DrawSelectedObjectSettingsLauncher(ref y, viewRect.width);
            DrawCreateObjectPanel(ref y, viewRect.width);
            y += 8f;

            DrawImportFolderRow(ref y, viewRect.width, Tr(CampusRuntimeEditorTextId.ObjectImports), GetObjectImportFolder(), CampusRuntimeImportTarget.Object);
            y += 8f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u7269\u4ef6\u8c03\u8272\u76d8", "Object Palette"), headerStyle);
            y += 34f;
            y = DrawPrefabPaletteGrid(objectPrefabs, selectedObjectIndex, y, viewRect.width,
                delegate(int index)
                {
                    selectedObjectIndex = index;
                    brushMode = CampusRuntimeBrushMode.PlaceObject;
                },
                delegate(int index)
                {
                    DeleteImportedObjectResource(index);
                });

            if (stairPrefab == null)
            {
                y += 12f;
                GUI.Label(new Rect(0f, y, viewRect.width, 52f), Tr("\u672a\u627e\u5230\u697c\u68af\u9884\u5236\u4f53\u3002\u8bf7\u5c06\u697c\u68af\u9884\u5236\u4f53\u653e\u5165 Resources/NtingCampusRuntime\uff0c\u624d\u80fd\u5728\u6784\u5efa\u7248\u4e2d\u653e\u7f6e\u697c\u68af\u3002", "No stair prefab found. Put a stair prefab in Resources/NtingCampusRuntime to place stairs in builds."), warningStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawCreateObjectPanel(ref float y, float width)
        {
            GUI.Label(new Rect(0f, y, width, 26f), Tr("\u65b0\u589e\u7269\u4f53", "Create Object"), headerStyle);
            y += 34f;

            GUI.Label(new Rect(0f, y, 52f, 30f), Tr(CampusRuntimeEditorTextId.Name), bodyStyle);
            newObjectName = DrawTextInput(new Rect(54f, y, width - 54f, 30f), newObjectName, "create_object_name");
            y += 38f;

            GUI.Label(new Rect(0f, y, 52f, 30f), Tr(CampusRuntimeEditorTextId.Footprint), bodyStyle);
            newObjectFootprintX = Mathf.Clamp(ParseIntField(new Rect(54f, y, 52f, 30f), newObjectFootprintX, "create_object_x"), 1, 32);
            GUI.Label(new Rect(112f, y, 20f, 30f), "x", bodyStyle);
            newObjectFootprintY = Mathf.Clamp(ParseIntField(new Rect(134f, y, 52f, 30f), newObjectFootprintY, "create_object_y"), 1, 32);
            newObjectBlocksMovement = GUI.Toggle(new Rect(198f, y + 3f, 72f, 24f), newObjectBlocksMovement, Tr("\u963b\u6321", "Block"), bodyStyle);
            y += 34f;

            newObjectIsInteractable = GUI.Toggle(new Rect(0f, y, 96f, 24f), newObjectIsInteractable, Tr("\u53ef\u4ea4\u4e92", "Interactable"), bodyStyle);
            newObjectIsStorageContainer = GUI.Toggle(new Rect(104f, y, 96f, 24f), newObjectIsStorageContainer, Tr("\u50a8\u7269", "Storage"), bodyStyle);
            if (newObjectIsStorageContainer)
            {
                newObjectIsInteractable = true;
            }

            y += 30f;
            DrawColorControls(ref y, width, Tr("\u989c\u8272", "Color"), ref newObjectColor);

            if (GUI.Button(new Rect(0f, y, width, 32f), Tr("\u521b\u5efa\u5e76\u9009\u4e2d\u7269\u4f53", "Create And Select Object"), buttonStyle))
            {
                CreateRuntimeObjectFromEditorFields();
            }

            y += 42f;
        }

        private void DrawLightingTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetLightingContentHeight(viewWidth)));
            lightScroll = GUI.BeginScrollView(contentRect, lightScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u706f\u5149\u5de5\u5177", "Lighting Tools"), headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PlaceLight, CampusRuntimeBrushMode.Erase },
                new string[] { Tr(CampusRuntimeEditorTextId.Pan), Tr("\u653e\u7f6e\u706f\u5149", "Place Light"), Tr(CampusRuntimeEditorTextId.Erase) });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pick },
                new string[] { Tr(CampusRuntimeEditorTextId.Pick) });

            y += 6f;
            if (GUI.Button(new Rect(0f, y, 116f, 32f), Tr(CampusRuntimeEditorTextId.PointLight), lightBrushType == Light2D.LightType.Point ? selectedButtonStyle : buttonStyle))
            {
                lightBrushType = Light2D.LightType.Point;
                brushMode = CampusRuntimeBrushMode.PlaceLight;
            }

            y += 44f;
            y = DrawSlider(y, viewRect.width, Tr("\u5f3a\u5ea6", "Intensity"), ref lightIntensity, 0f, 4f);
            DrawColorControls(ref y, viewRect.width, Tr("\u989c\u8272", "Color"), ref lightColor);
            y = DrawSlider(y, viewRect.width, Tr("\u5185\u534a\u5f84", "Inner Radius"), ref lightInnerRadius, 0f, 12f);
            y = DrawSlider(y, viewRect.width, Tr("\u5916\u534a\u5f84", "Outer Radius"), ref lightOuterRadius, 0.2f, 24f);
            lightShadowsEnabled = GUI.Toggle(new Rect(0f, y, viewRect.width, 24f), lightShadowsEnabled, Tr("\u542f\u7528\u9634\u5f71", "Enable Shadows"), bodyStyle);
            y += 30f;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && lightShadowsEnabled;
            y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u5f3a\u5ea6", "Shadow Intensity"), ref lightShadowIntensity, 0f, 1f);
            y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u67d4\u548c", "Shadow Softness"), ref lightShadowSoftness, 0f, 1f);
            y = DrawSlider(y, viewRect.width, Tr("\u67d4\u548c\u8870\u51cf", "Softness Falloff"), ref lightShadowSoftnessFalloff, 0f, 1f);
            GUI.enabled = previousGuiEnabled;

            DrawLightPreviewCard(ref y, viewRect.width);
            DrawDayNightControls(ref y, viewRect.width);

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u573a\u666f\u70b9\u5149\u6e90", "Point Lights In Scene"), headerStyle);
            y += 34f;
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (!IsRuntimeEditableLight(light))
                {
                    continue;
                }

                if (GUI.Button(new Rect(0f, y, viewRect.width - 44f, 32f), light.gameObject.name, selectedLight == light ? selectedButtonStyle : buttonStyle))
                {
                    selectedLight = light;
                    lightIntensity = Mathf.Max(0f, selectedLight.intensity);
                    lightInnerRadius = Mathf.Max(0f, selectedLight.pointLightInnerRadius);
                    lightOuterRadius = Mathf.Max(0.2f, selectedLight.pointLightOuterRadius);
                    SyncShadowFieldsFromSelectedLight();
                }

                if (GUI.Button(new Rect(viewRect.width - 38f, y, 38f, 32f), "X", buttonStyle))
                {
                    RecordUndo();
                    if (selectedLight == light)
                    {
                        selectedLight = null;
                    }

                    DestroyRuntimeObject(light.gameObject);
                    SchedulePlayerMapSave();
                }

                y += 38f;
            }

            if (selectedLight != null && !IsRuntimeEditableLight(selectedLight))
            {
                selectedLight = null;
            }

            if (selectedLight != null)
            {
                bool selectedLightChanged = false;
                y += 8f;
                GUI.Label(new Rect(0f, y, viewRect.width, 26f), Tr("\u5f53\u524d\u70b9\u5149\u6e90", "Selected Point Light"), headerStyle);
                y += 34f;

                GUI.Label(new Rect(0f, y, 66f, 24f), Tr("\u5f3a\u5ea6", "Intensity"), bodyStyle);
                float adjustedIntensity = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 178f, 20f), selectedLight.intensity, 0f, 8f);
                if (GUI.Button(new Rect(viewRect.width - 100f, y, 28f, 26f), "-", buttonStyle))
                {
                    adjustedIntensity = Mathf.Max(0f, selectedLight.intensity - 0.1f);
                }

                if (GUI.Button(new Rect(viewRect.width - 68f, y, 28f, 26f), "+", buttonStyle))
                {
                    adjustedIntensity = Mathf.Min(8f, selectedLight.intensity + 0.1f);
                }

                if (!Mathf.Approximately(selectedLight.intensity, adjustedIntensity))
                {
                    selectedLight.intensity = adjustedIntensity;
                    lightIntensity = adjustedIntensity;
                    selectedLightChanged = true;
                }

                GUI.Label(new Rect(viewRect.width - 36f, y, 36f, 24f), selectedLight.intensity.ToString("0.0"), smallBodyStyle);
                y += 30f;

                Color selectedColor = selectedLight.color;
                DrawColorControls(ref y, viewRect.width, Tr("\u989c\u8272", "Color"), ref selectedColor);
                if (selectedLight.color != selectedColor)
                {
                    selectedLight.color = selectedColor;
                    lightColor = selectedColor;
                    selectedLightChanged = true;
                }

                bool previousShadowsEnabled = selectedLight.shadowsEnabled;
                bool selectedShadowsEnabled = GUI.Toggle(new Rect(0f, y, viewRect.width, 24f), selectedLight.shadowsEnabled, Tr("\u542f\u7528\u9634\u5f71", "Enable Shadows"), bodyStyle);
                y += 30f;
                bool selectedPreviousGuiEnabled = GUI.enabled;
                GUI.enabled = selectedPreviousGuiEnabled && selectedShadowsEnabled;
                float selectedShadowIntensity = selectedLight.shadowIntensity;
                float selectedShadowSoftness = selectedLight.shadowSoftness;
                float selectedShadowSoftnessFalloff = selectedLight.shadowSoftnessFalloffIntensity;
                y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u5f3a\u5ea6", "Shadow Intensity"), ref selectedShadowIntensity, 0f, 1f);
                y = DrawSlider(y, viewRect.width, Tr("\u9634\u5f71\u67d4\u548c", "Shadow Softness"), ref selectedShadowSoftness, 0f, 1f);
                y = DrawSlider(y, viewRect.width, Tr("\u67d4\u548c\u8870\u51cf", "Softness Falloff"), ref selectedShadowSoftnessFalloff, 0f, 1f);
                GUI.enabled = selectedPreviousGuiEnabled;
                bool selectedShadowSettingsChanged = previousShadowsEnabled != selectedShadowsEnabled ||
                                                     !Mathf.Approximately(selectedLight.shadowIntensity, selectedShadowIntensity) ||
                                                     !Mathf.Approximately(selectedLight.shadowSoftness, selectedShadowSoftness) ||
                                                     !Mathf.Approximately(selectedLight.shadowSoftnessFalloffIntensity, selectedShadowSoftnessFalloff);
                CampusDynamicShadowUtility.ConfigureLightShadows(selectedLight, selectedShadowsEnabled, selectedShadowIntensity, selectedShadowSoftness, selectedShadowSoftnessFalloff);
                SyncShadowFieldsFromSelectedLight();
                selectedLightChanged |= selectedShadowSettingsChanged;

                float selectedInnerRadius = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 120f, 20f), selectedLight.pointLightInnerRadius, 0f, 12f);
                if (!Mathf.Approximately(selectedLight.pointLightInnerRadius, selectedInnerRadius))
                {
                    selectedLight.pointLightInnerRadius = selectedInnerRadius;
                    selectedLightChanged = true;
                }

                GUI.Label(new Rect(0f, y, 66f, 24f), Tr("\u5185\u5708", "Inner"), bodyStyle);
                GUI.Label(new Rect(viewRect.width - 44f, y, 44f, 24f), selectedLight.pointLightInnerRadius.ToString("0.0"), smallBodyStyle);
                y += 30f;

                float selectedOuterRadius = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 120f, 20f), Mathf.Max(selectedLight.pointLightInnerRadius + 0.1f, selectedLight.pointLightOuterRadius), selectedLight.pointLightInnerRadius + 0.1f, 24f);
                if (!Mathf.Approximately(selectedLight.pointLightOuterRadius, selectedOuterRadius))
                {
                    selectedLight.pointLightOuterRadius = selectedOuterRadius;
                    selectedLightChanged = true;
                }

                GUI.Label(new Rect(0f, y, 66f, 24f), Tr("\u5916\u5708", "Outer"), bodyStyle);
                GUI.Label(new Rect(viewRect.width - 44f, y, 44f, 24f), selectedLight.pointLightOuterRadius.ToString("0.0"), smallBodyStyle);
                y += 30f;

                if (selectedLightChanged)
                {
                    SchedulePlayerMapSave();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawFloorPanel()
        {
            GUI.Box(floorPanelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(floorPanelRect.x + 12f, floorPanelRect.y + 12f, floorPanelRect.width - 24f, 40f), Tr("\u697c\u5c42", "Floors"), headerStyle);
            Rect listRect = new Rect(floorPanelRect.x + 12f, floorPanelRect.y + 60f, floorPanelRect.width - 24f, floorPanelRect.height - 110f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(180f, mapRoot != null ? mapRoot.Floors.Count * 40f : 180f));
            floorScroll = GUI.BeginScrollView(listRect, floorScroll, viewRect);
            if (mapRoot != null)
            {
                for (int i = mapRoot.Floors.Count - 1; i >= 0; i--)
                {
                    CampusFloorRoot floor = mapRoot.Floors[i];
                    if (floor == null)
                    {
                        continue;
                    }

                    float rowY = (mapRoot.Floors.Count - 1 - i) * 40f;
                    string label = CampusRuntimeEditorTextCatalog.FormatFloorButton(displayLanguage, floor.FloorIndex, floor.IsUnlocked);
                    if (GUI.Button(new Rect(0f, rowY, viewRect.width, 34f), label, selectedFloorIndex == floor.FloorIndex ? selectedButtonStyle : buttonStyle))
                    {
                        selectedFloorIndex = floor.FloorIndex;
                        mapRoot.CurrentPreviewFloor = selectedFloorIndex;
                    }
                }
            }

            GUI.EndScrollView();
            float buttonY = floorPanelRect.yMax - 42f;
            if (GUI.Button(new Rect(floorPanelRect.x + 12f, buttonY, 78f, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Add), buttonStyle))
            {
                RecordUndo();
                selectedFloorIndex = mapRoot.GetHighestFloorIndex() + 1;
                EnsureFloor(selectedFloorIndex);
            }

            if (GUI.Button(new Rect(floorPanelRect.x + 96f, buttonY, 78f, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Lock), buttonStyle))
            {
                CampusFloorRoot floor = EnsureFloor(selectedFloorIndex);
                if (floor != null)
                {
                    RecordUndo();
                    floor.IsUnlocked = !floor.IsUnlocked;
                }
            }

            if (GUI.Button(new Rect(floorPanelRect.x + 180f, buttonY, 78f, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Delete), buttonStyle))
            {
                DeleteSelectedFloor();
            }
        }

        private void DrawChecklistPanel()
        {
            GUI.Box(checklistPanelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(checklistPanelRect.x + 12f, checklistPanelRect.y + 12f, checklistPanelRect.width - 24f, 40f), Tr("\u533a\u57df\u68c0\u67e5\u6e05\u5355", "Area Checklist"), headerStyle);
            Rect listRect = new Rect(checklistPanelRect.x + 12f, checklistPanelRect.y + 62f, checklistPanelRect.width - 24f, checklistPanelRect.height - 78f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, roomNames.Count * 30f));
            checklistScroll = GUI.BeginScrollView(listRect, checklistScroll, viewRect);
            if (roomNames.Count == 0)
            {
                GUI.Label(new Rect(0f, 0f, viewRect.width, 58f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoRoomRequirementsExist), mutedStyle);
            }

            for (int i = 0; i < roomNames.Count; i++)
            {
                int count = CountRoomMarkers(roomNames[i]);
                int required = roomRequiredCounts[i];
                string label = GetAreaPresetLabel(roomNames[i]);
                string value = count + "/" + required;
                Rect swatchRect = new Rect(0f, i * 30f + 6f, 18f, 16f);
                DrawAreaColorSwatch(swatchRect, roomNames[i]);
                GUI.Label(new Rect(26f, i * 30f, viewRect.width - 92f, 28f), label, bodyStyle);
                GUI.Label(new Rect(viewRect.width - 66f, i * 30f, 62f, 28f), value, count >= required ? bodyStyle : warningStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawBottomToolbar()
        {
            GUI.Box(bottomToolbarRect, GUIContent.none, panelStyle);
            float x = bottomToolbarRect.x + 14f;
            float y = bottomToolbarRect.y + 14f;
            DrawToolbarButton(ref x, y, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Close), delegate { isOpen = false; });
            DrawToolbarButton(ref x, y, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Help), delegate { showHelpOverlay = !showHelpOverlay; });
            DrawToolbarButton(ref x, y, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Import), ImportLatestJson);
            DrawToolbarButton(ref x, y, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Export), ExportToJson);
            DrawToolbarButton(ref x, y, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Undo), UndoSnapshot, undoSnapshots.Count > 0);
            DrawToolbarButton(ref x, y, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Redo), RedoSnapshot, redoSnapshots.Count > 0);

            float rightX = bottomToolbarRect.xMax - 354f;
            if (GUI.Button(new Rect(rightX, y, 106f, 46f), showGridOverlay
                    ? CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.GridOn)
                    : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.GridOff), showGridOverlay ? selectedButtonStyle : buttonStyle))
            {
                showGridOverlay = !showGridOverlay;
            }

            if (GUI.Button(new Rect(rightX + 116f, y, 106f, 46f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Settings), showSettings ? selectedButtonStyle : buttonStyle))
            {
                showSettings = !showSettings;
            }

            if (GUI.Button(new Rect(rightX + 232f, y, 106f, 46f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Rebuild), buttonStyle))
            {
                PrepareRuntimeMapPresentationSafe();
                SetStatus(CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.WallVisualsRebuiltStatus));
            }
        }

                private void DrawHelpPanel()
        {
            if (!showHelpOverlay)
            {
                return;
            }

            GUI.Box(helpPanelRect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(helpPanelRect.x + 18f, helpPanelRect.y + 16f, helpPanelRect.width - 36f, 32f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Controls), headerStyle);
            string text = CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ControlsBody);
            GUI.Label(new Rect(helpPanelRect.x + 22f, helpPanelRect.y + 66f, helpPanelRect.width - 44f, helpPanelRect.height - 88f), text, bodyStyle);
        }
        private void DrawStatusLine()
        {
            if (Time.realtimeSinceStartup > statusUntil && !string.IsNullOrEmpty(statusText))
            {
                return;
            }

            Rect rect = new Rect(Screen.width * 0.5f - 320f, 18f, 640f, 36f);
            GUI.Box(rect, statusText, panelStyle);
        }

        private void DrawGridOverlay()
        {
            if (!showGridOverlay || sceneCamera == null || mapRoot == null)
            {
                return;
            }

            CampusFloorRoot floor = mapRoot.GetFloor(selectedFloorIndex);
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            Vector3 minWorld = sceneCamera.ScreenToWorldPoint(new Vector3(0f, 0f, GetCameraPlaneDistance()));
            Vector3 maxWorld = sceneCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, GetCameraPlaneDistance()));
            Vector3Int minCell = floor.Grid.WorldToCell(new Vector3(Mathf.Min(minWorld.x, maxWorld.x), Mathf.Min(minWorld.y, maxWorld.y), 0f));
            Vector3Int maxCell = floor.Grid.WorldToCell(new Vector3(Mathf.Max(minWorld.x, maxWorld.x), Mathf.Max(minWorld.y, maxWorld.y), 0f));
            int minX = Mathf.Max(minCell.x - 1, hoverCell.x - 80);
            int maxX = Mathf.Min(maxCell.x + 1, hoverCell.x + 80);
            int minY = Mathf.Max(minCell.y - 1, hoverCell.y - 80);
            int maxY = Mathf.Min(maxCell.y + 1, hoverCell.y + 80);

            GUI.color = new Color(0.72f, 0.9f, 1f, 0.18f);
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 a = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(x, minY, 0)));
                Vector2 b = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(x, maxY + 1, 0)));
                DrawGuiLine(a, b, 1f);
            }

            for (int y = minY; y <= maxY; y++)
            {
                Vector2 a = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(minX, y, 0)));
                Vector2 b = WorldToGuiPoint(floor.Grid.CellToWorld(new Vector3Int(maxX + 1, y, 0)));
                DrawGuiLine(a, b, 1f);
            }

            GUI.color = Color.white;
        }

        private void DrawWorldPreviewOverlay()
        {
            if (sceneCamera == null || mapRoot == null)
            {
                return;
            }

            CampusFloorRoot floor = mapRoot.GetFloor(selectedFloorIndex);
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            DrawSelectedLightRangeOverlay();
            DrawRoomMarkerOverlay(floor);
            DrawGameplayMarkerOverlay(floor);

            if (rectangleDragActive)
            {
                if (brushMode == CampusRuntimeBrushMode.RectangleRoom)
                {
                    Color roomColor = ResolveSelectedRoomOverlayColor();
                    DrawFilledCellRect(
                        floor.Grid,
                        rectangleStartCell,
                        hoverCell,
                        new Color(roomColor.r, roomColor.g, roomColor.b, 0.22f),
                        new Color(roomColor.r, roomColor.g, roomColor.b, 0.82f),
                        2f);
                }
                else
                {
                    DrawCellRect(floor.Grid, rectangleStartCell, hoverCell, new Color(0.2f, 0.85f, 1f, 0.45f), 2f);
                }
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceLight)
            {
                Vector3 center = floor.Grid.GetCellCenterWorld(hoverCell);
                DrawLightPlacementOverlay(floor.Grid, center);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceObject)
            {
                Vector2Int footprint = GetSelectedObjectFootprint();
                DrawCellGrid(floor.Grid, hoverCell, footprint, new Color(1f, 0.96f, 0.25f, 0.72f), 2f);
                DrawObjectPlacementPreview(floor.Grid, hoverCell, footprint);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceRoomPrefab)
            {
                CampusRuntimeRoomPrefab roomPrefab = GetSelectedRoomPrefab();
                Vector2Int size = roomPrefab != null ? NormalizeRoomPrefabSize(roomPrefab.Size) : Vector2Int.one;
                DrawCellGrid(floor.Grid, hoverCell, size, new Color(0.2f, 0.85f, 1f, 0.72f), 2f);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceStair)
            {
                Vector3Int secondary = hoverCell + CampusStairLink.DirectionFromRotation(rotation90);
                DrawCellRect(floor.Grid, hoverCell, secondary, new Color(0.2f, 0.85f, 1f, 0.72f), 2f);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceRoom || brushMode == CampusRuntimeBrushMode.RectangleRoom)
            {
                Color roomColor = ResolveSelectedRoomOverlayColor();
                DrawFilledCellRect(
                    floor.Grid,
                    hoverCell,
                    hoverCell,
                    new Color(roomColor.r, roomColor.g, roomColor.b, 0.22f),
                    new Color(roomColor.r, roomColor.g, roomColor.b, 0.82f),
                    2f);
            }
            else if (brushMode == CampusRuntimeBrushMode.PlaceGameplayMarker ||
                     brushMode == CampusRuntimeBrushMode.EraseGameplayMarker)
            {
                if (brushMode == CampusRuntimeBrushMode.PlaceGameplayMarker)
                {
                    Color color = ResolveSelectedGameplayOverlayColor();
                    DrawFilledCellRect(
                        floor.Grid,
                        hoverCell,
                        hoverCell,
                        new Color(color.r, color.g, color.b, 0.2f),
                        new Color(color.r, color.g, color.b, 0.82f),
                        2f);
                }
                else
                {
                    DrawFilledCellRect(
                        floor.Grid,
                        hoverCell,
                        hoverCell,
                        new Color(1f, 0.18f, 0.18f, 0.16f),
                        new Color(1f, 0.25f, 0.18f, 0.86f),
                        2f);
                }
            }
            else
            {
                Vector3Int end = new Vector3Int(hoverCell.x + Mathf.Max(1, brushSize) - 1, hoverCell.y + Mathf.Max(1, brushSize) - 1, 0);
                DrawCellRect(floor.Grid, hoverCell, end, new Color(1f, 0.96f, 0.25f, 0.55f), 2f);
            }
        }

        private bool ShouldDrawRoomMarkerOverlay()
        {
            return activeTab == CampusRuntimeEditorTab.Gameplay &&
                   (brushMode == CampusRuntimeBrushMode.PlaceRoom ||
                    brushMode == CampusRuntimeBrushMode.RectangleRoom);
        }

        private void DrawRoomMarkerOverlay(CampusFloorRoot floor)
        {
            HideRoomMarkerSpriteRenderers(floor);
            if (!ShouldDrawRoomMarkerOverlay() || floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            if (markers == null || markers.Length == 0)
            {
                return;
            }

            Dictionary<string, HashSet<Vector3Int>> cellsByRoomName =
                new Dictionary<string, HashSet<Vector3Int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Vector3Int cell = marker.Cell;
                cell.z = 0;
                string roomName = string.IsNullOrWhiteSpace(marker.RoomName)
                    ? "Unnamed Room"
                    : marker.RoomName.Trim();
                if (!cellsByRoomName.TryGetValue(roomName, out HashSet<Vector3Int> cells))
                {
                    cells = new HashSet<Vector3Int>();
                    cellsByRoomName[roomName] = cells;
                }

                cells.Add(cell);
            }

            if (cellsByRoomName.Count == 0)
            {
                return;
            }

            Color oldColor = GUI.color;
            foreach (KeyValuePair<string, HashSet<Vector3Int>> pair in cellsByRoomName)
            {
                Color roomColor = ResolveRoomOverlayColor(pair.Key);
                GUI.color = new Color(roomColor.r, roomColor.g, roomColor.b, 0.24f);
                foreach (Vector3Int cell in pair.Value)
                {
                    DrawCellFill(floor.Grid, cell);
                }

                GUI.color = new Color(roomColor.r, roomColor.g, roomColor.b, 0.82f);
                foreach (Vector3Int cell in pair.Value)
                {
                    DrawCellOuterEdges(floor.Grid, pair.Value, cell, 2f);
                }
            }

            GUI.color = oldColor;
        }

        private void DrawGameplayMarkerOverlay(CampusFloorRoot floor)
        {
            if (activeTab != CampusRuntimeEditorTab.Gameplay || floor == null || floor.Grid == null)
            {
                return;
            }

            Color oldColor = GUI.color;

            CampusGameplayRoomMarker[] roomMarkers =
                FindObjectsByType<CampusGameplayRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roomMarkers.Length; i++)
            {
                CampusGameplayRoomMarker marker = roomMarkers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Color color = ResolveGameplayRoomOverlayColor(marker.RoomType);
                BoundsInt bounds = marker.BuildBounds();
                DrawFilledCellRect(
                    floor.Grid,
                    new Vector3Int(bounds.xMin, bounds.yMin, 0),
                    new Vector3Int(bounds.xMax - 1, bounds.yMax - 1, 0),
                    new Color(color.r, color.g, color.b, 0.16f),
                    new Color(color.r, color.g, color.b, 0.68f),
                    2f);
                DrawWorldLabel(floor.Grid.GetCellCenterWorld(marker.AnchorCell), marker.RoomDisplayName, color);
            }

            CampusGameplayFacilityMarker[] facilityMarkers =
                FindObjectsByType<CampusGameplayFacilityMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < facilityMarkers.Length; i++)
            {
                CampusGameplayFacilityMarker marker = facilityMarkers[i];
                if (marker == null || marker.FloorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Color color = ResolveGameplayFacilityOverlayColor(marker.FacilityType);
                Vector3Int cell = NormalizeCell(marker.Cell);
                DrawFilledCellRect(
                    floor.Grid,
                    cell,
                    cell,
                    new Color(color.r, color.g, color.b, 0.2f),
                    new Color(color.r, color.g, color.b, 0.82f),
                    2f);
                DrawWorldLabel(floor.Grid.GetCellCenterWorld(cell), marker.DisplayName, color);
            }

            CampusPrankInteractionSpot[] prankSpots =
                FindObjectsByType<CampusPrankInteractionSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < prankSpots.Length; i++)
            {
                CampusPrankInteractionSpot spot = prankSpots[i];
                if (spot == null || !TryResolveGameplayMarkerCell(spot, out int floorIndex, out Vector3Int cell))
                {
                    continue;
                }

                if (floorIndex != floor.FloorIndex)
                {
                    continue;
                }

                Color color = spot.AccentColor;
                cell = NormalizeCell(cell);
                DrawFilledCellRect(
                    floor.Grid,
                    cell,
                    cell,
                    new Color(color.r, color.g, color.b, 0.18f),
                    new Color(color.r, color.g, color.b, 0.92f),
                    2f);
                DrawWorldLabel(floor.Grid.GetCellCenterWorld(cell), spot.DisplayName, color);
            }

            GUI.color = oldColor;
        }

        private void DrawWorldLabel(Vector3 worldPosition, string label, Color accent)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            Vector2 guiPoint = WorldToGuiPoint(worldPosition);
            Rect rect = new Rect(guiPoint.x - 58f, guiPoint.y - 30f, 116f, 22f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            if (lineTexture != null)
            {
                GUI.DrawTexture(rect, lineTexture);
            }

            GUI.color = new Color(accent.r, accent.g, accent.b, 1f);
            GUI.Label(rect, Truncate(label, 16), smallBodyStyle);
            GUI.color = oldColor;
        }

        private Color ResolveSelectedGameplayOverlayColor()
        {
            CampusRuntimeGameplayMarkerPreset preset = GetSelectedGameplayPreset();
            return preset != null ? preset.Color : new Color(0.2f, 0.85f, 1f, 1f);
        }

        private Color ResolveSelectedRoomOverlayColor()
        {
            return ResolveRoomOverlayColor(GetSelectedRoomName());
        }

        private static Color ResolveGameplayRoomOverlayColor(CampusRoomType roomType)
        {
            switch (roomType)
            {
                case CampusRoomType.Classroom:
                    return new Color(0.25f, 0.55f, 0.98f, 1f);
                case CampusRoomType.Corridor:
                    return new Color(0.96f, 0.66f, 0.22f, 1f);
                case CampusRoomType.Office:
                    return new Color(0.72f, 0.48f, 0.28f, 1f);
                case CampusRoomType.Dormitory:
                    return new Color(0.56f, 0.42f, 0.88f, 1f);
                case CampusRoomType.Restroom:
                    return new Color(0.22f, 0.68f, 0.92f, 1f);
                case CampusRoomType.Canteen:
                    return new Color(0.22f, 0.72f, 0.46f, 1f);
                case CampusRoomType.Store:
                    return new Color(0.88f, 0.36f, 0.72f, 1f);
                case CampusRoomType.Library:
                    return new Color(0.16f, 0.72f, 0.72f, 1f);
                case CampusRoomType.CommonActivityZone:
                    return new Color(0.88f, 0.62f, 0.24f, 1f);
                case CampusRoomType.Outdoor:
                    return new Color(0.22f, 0.58f, 0.95f, 1f);
                case CampusRoomType.HumanResources:
                    return new Color(0.74f, 0.36f, 0.88f, 1f);
                case CampusRoomType.ShrineRoom:
                    return new Color(0.92f, 0.38f, 0.45f, 1f);
                default:
                    return new Color(0.42f, 0.82f, 0.95f, 1f);
            }
        }

        private static Color ResolveGameplayFacilityOverlayColor(CampusFacilityType facilityType)
        {
            switch (facilityType)
            {
                case CampusFacilityType.Door:
                    return new Color(0.72f, 0.72f, 0.72f, 1f);
                case CampusFacilityType.Blackboard:
                    return new Color(0.12f, 0.46f, 0.36f, 1f);
                case CampusFacilityType.StudentDesk:
                    return new Color(0.26f, 0.56f, 0.96f, 1f);
                case CampusFacilityType.Podium:
                    return new Color(0.18f, 0.38f, 0.86f, 1f);
                case CampusFacilityType.OfficeDesk:
                    return new Color(0.72f, 0.48f, 0.28f, 1f);
                case CampusFacilityType.Bed:
                    return new Color(0.56f, 0.42f, 0.88f, 1f);
                case CampusFacilityType.BulletinBoard:
                    return new Color(0.88f, 0.62f, 0.24f, 1f);
                case CampusFacilityType.Recruitment:
                    return new Color(0.74f, 0.36f, 0.88f, 1f);
                case CampusFacilityType.Sink:
                    return new Color(0.22f, 0.68f, 0.92f, 1f);
                case CampusFacilityType.Storage:
                    return new Color(0.62f, 0.56f, 0.46f, 1f);
                case CampusFacilityType.CanteenCounter:
                    return new Color(0.18f, 0.68f, 0.72f, 1f);
                case CampusFacilityType.CanteenQueuePoint:
                    return new Color(0.95f, 0.76f, 0.28f, 1f);
                case CampusFacilityType.CanteenFoodTray:
                    return new Color(0.96f, 0.64f, 0.24f, 1f);
                case CampusFacilityType.DeliveryDropPoint:
                    return new Color(0.32f, 0.54f, 0.98f, 1f);
                case CampusFacilityType.DiningTable:
                    return new Color(0.55f, 0.76f, 0.28f, 1f);
                case CampusFacilityType.StoreShelf:
                    return new Color(0.88f, 0.36f, 0.72f, 1f);
                case CampusFacilityType.StoreQueuePoint:
                    return new Color(0.94f, 0.48f, 0.82f, 1f);
                case CampusFacilityType.StoreCheckout:
                    return new Color(0.76f, 0.24f, 0.64f, 1f);
                default:
                    return new Color(0.78f, 0.78f, 0.78f, 1f);
            }
        }

        private static Color ResolveRoomOverlayColor(string roomName)
        {
            string key = string.IsNullOrWhiteSpace(roomName) ? "Unnamed Room" : roomName.Trim().ToLowerInvariant();
            if (ContainsRoomNameToken(key, "\u6559\u5ba4", "class"))
            {
                return new Color(0.25f, 0.55f, 0.98f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u8d70\u5eca", "\u8fc7\u9053", "corridor", "hall"))
            {
                return new Color(0.96f, 0.66f, 0.22f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u529e\u516c\u5ba4", "\u6559\u5e08", "office", "teacher"))
            {
                return new Color(0.72f, 0.48f, 0.28f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u5bbf\u820d", "dorm"))
            {
                return new Color(0.56f, 0.42f, 0.88f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u536b\u751f\u95f4", "\u5395\u6240", "\u6d17\u624b\u95f4", "restroom", "toilet", "bath"))
            {
                return new Color(0.22f, 0.68f, 0.92f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u98df\u5802", "\u9910\u5385", "canteen", "dining"))
            {
                return new Color(0.18f, 0.70f, 0.42f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u5916\u5356", "delivery"))
            {
                return new Color(0.35f, 0.55f, 1f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u8d85\u5e02", "\u5546\u5e97", "\u5c0f\u5356", "shop", "store", "market"))
            {
                return new Color(0.88f, 0.36f, 0.72f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u4e66\u5e97", "bookstore", "bookshop"))
            {
                return new Color(0.58f, 0.42f, 0.94f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u56fe\u4e66\u9986", "library"))
            {
                return new Color(0.16f, 0.72f, 0.72f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u516c\u5171", "\u6d3b\u52a8", "common", "activity"))
            {
                return new Color(0.88f, 0.62f, 0.24f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u697c\u68af", "stair"))
            {
                return new Color(0.62f, 0.62f, 0.62f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u4eba\u4e8b", "humanresources", "hr"))
            {
                return new Color(0.74f, 0.36f, 0.88f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u795e\u9f9b", "shrine"))
            {
                return new Color(0.92f, 0.38f, 0.45f, 1f);
            }

            if (ContainsRoomNameToken(key, "\u6821\u5916", "\u5ba4\u5916", "\u64cd\u573a", "outdoor", "outside"))
            {
                return new Color(0.88f, 0.48f, 0.24f, 1f);
            }

            Color[] palette =
            {
                new Color(0.28f, 0.68f, 0.95f, 1f),
                new Color(0.88f, 0.52f, 0.28f, 1f),
                new Color(0.54f, 0.72f, 0.24f, 1f),
                new Color(0.78f, 0.42f, 0.88f, 1f),
                new Color(0.22f, 0.72f, 0.58f, 1f),
                new Color(0.92f, 0.38f, 0.45f, 1f),
                new Color(0.42f, 0.58f, 0.92f, 1f),
                new Color(0.72f, 0.62f, 0.22f, 1f)
            };

            int hash = 23;
            for (int i = 0; i < key.Length; i++)
            {
                hash = unchecked(hash * 37 + key[i]);
            }

            int index = Mathf.Abs(hash == int.MinValue ? 0 : hash) % palette.Length;
            return palette[index];
        }

        private static bool ContainsRoomNameToken(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && value.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        private void HideRoomMarkerSpriteRenderers(CampusFloorRoot floor)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void HideAllRoomMarkerSpriteRenderers()
        {
            CampusRuntimeRoomMarker[] markers =
                FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void RebuildGameplayRoomRegistrySafe()
        {
            try
            {
                CampusRoomRegistry registry =
                    FindFirstObjectByType<CampusRoomRegistry>(FindObjectsInactive.Include);
                if (registry != null)
                {
                    registry.RebuildRegistry();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to rebuild gameplay rooms: " + exception.Message);
            }
        }

        private void DrawSelectedLightRangeOverlay()
        {
            if (selectedLight == null || selectedLight.lightType != Light2D.LightType.Point)
            {
                return;
            }

            DrawWorldCircle(selectedLight.transform.position, selectedLight.pointLightOuterRadius, new Color(1f, 0.74f, 0.2f, 0.65f), 2f, 56);
            DrawWorldCircle(selectedLight.transform.position, selectedLight.pointLightInnerRadius, new Color(1f, 0.95f, 0.55f, 0.55f), 1f, 40);
        }

        private void DrawLightPlacementOverlay(Grid grid, Vector3 center)
        {
            DrawCellRect(grid, hoverCell, hoverCell, new Color(1f, 0.96f, 0.25f, 0.75f), 2f);
            if (lightBrushType != Light2D.LightType.Point)
            {
                return;
            }

            DrawWorldCircle(center, Mathf.Max(lightInnerRadius, 0.05f), new Color(1f, 0.96f, 0.55f, 0.7f), 1f, 40);
            DrawWorldCircle(center, Mathf.Max(lightOuterRadius, lightInnerRadius + 0.1f), new Color(1f, 0.76f, 0.2f, 0.78f), 2f, 56);
        }

        private void DrawCellRect(Grid grid, Vector3Int start, Vector3Int end, Color color, float thickness)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x) + 1;
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y) + 1;
            Vector2 a = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(minX, minY, 0)));
            Vector2 b = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(maxX, minY, 0)));
            Vector2 c = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(maxX, maxY, 0)));
            Vector2 d = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(minX, maxY, 0)));
            GUI.color = color;
            DrawGuiLine(a, b, thickness);
            DrawGuiLine(b, c, thickness);
            DrawGuiLine(c, d, thickness);
            DrawGuiLine(d, a, thickness);
            GUI.color = Color.white;
        }

        private void DrawFilledCellRect(Grid grid, Vector3Int start, Vector3Int end, Color fillColor, Color borderColor, float thickness)
        {
            if (grid == null)
            {
                return;
            }

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            Color oldColor = GUI.color;
            GUI.color = fillColor;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    DrawCellFill(grid, new Vector3Int(x, y, 0));
                }
            }

            GUI.color = borderColor;
            DrawCellRect(grid, start, end, borderColor, thickness);
            GUI.color = oldColor;
        }

        private void DrawCellFill(Grid grid, Vector3Int cell)
        {
            if (grid == null || lineTexture == null)
            {
                return;
            }

            Vector2 min = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0)));
            Vector2 max = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0)));
            Rect rect = Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
            GUI.DrawTexture(rect, lineTexture);
        }

        private void DrawCellOuterEdges(Grid grid, HashSet<Vector3Int> markedCells, Vector3Int cell, float thickness)
        {
            if (grid == null || markedCells == null)
            {
                return;
            }

            Vector3Int right = new Vector3Int(cell.x + 1, cell.y, 0);
            Vector3Int left = new Vector3Int(cell.x - 1, cell.y, 0);
            Vector3Int up = new Vector3Int(cell.x, cell.y + 1, 0);
            Vector3Int down = new Vector3Int(cell.x, cell.y - 1, 0);

            if (!markedCells.Contains(down))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y, 0))),
                    thickness);
            }

            if (!markedCells.Contains(right))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0))),
                    thickness);
            }

            if (!markedCells.Contains(up))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x + 1, cell.y + 1, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y + 1, 0))),
                    thickness);
            }

            if (!markedCells.Contains(left))
            {
                DrawGuiLine(
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y + 1, 0))),
                    WorldToGuiPoint(grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0))),
                    thickness);
            }
        }

        private void DrawCellGrid(Grid grid, Vector3Int anchor, Vector2Int size, Color color, float thickness)
        {
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            Vector3Int end = new Vector3Int(anchor.x + size.x - 1, anchor.y + size.y - 1, anchor.z);
            DrawCellRect(grid, anchor, end, color, thickness);

            GUI.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 0.72f));
            for (int x = 1; x < size.x; x++)
            {
                Vector2 a = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x + x, anchor.y, 0)));
                Vector2 b = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x + x, anchor.y + size.y, 0)));
                DrawGuiLine(a, b, 1f);
            }

            for (int y = 1; y < size.y; y++)
            {
                Vector2 a = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x, anchor.y + y, 0)));
                Vector2 b = WorldToGuiPoint(grid.CellToWorld(new Vector3Int(anchor.x + size.x, anchor.y + y, 0)));
                DrawGuiLine(a, b, 1f);
            }

            GUI.color = Color.white;
        }

        private void DrawObjectPlacementPreview(Grid grid, Vector3Int anchor, Vector2Int footprint)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (grid == null || prefab == null)
            {
                return;
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Sprite sprite = ResolvePrefabPreviewSprite(prefab, placed, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90);
            if (sprite == null || renderer == null)
            {
                return;
            }

            Vector3 worldCenter = CampusPlacedObject.GetPlacementWorldCenter(
                grid,
                anchor,
                footprint,
                placed != null && placed.IsWallMounted,
                effectiveRotation90);
            Vector2 previewScale = placed != null ? placed.NormalizedVisualScale : new Vector2(renderer.transform.localScale.x, renderer.transform.localScale.y);
            Rect rect = BuildWorldPreviewRect(worldCenter, sprite, new Vector3(previewScale.x, previewScale.y, renderer.transform.localScale.z));
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float previewRotation = placed != null && placed.AllowRotation && !usesAuthoredDirectionalSprite && !placed.SuppressFlatSpriteRotation ? -effectiveRotation90 * 90f : 0f;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.58f);
            if (!Mathf.Approximately(previewRotation, 0f))
            {
                GUIUtility.RotateAroundPivot(previewRotation, rect.center);
            }

            DrawSprite(rect, sprite);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private Rect BuildWorldPreviewRect(Vector3 worldCenter, Sprite sprite, Vector3 visualScale)
        {
            if (sceneCamera == null || sprite == null)
            {
                return Rect.zero;
            }

            Vector2 spriteWorldSize = sprite.bounds.size;
            float worldWidth = Mathf.Abs(spriteWorldSize.x * visualScale.x);
            float worldHeight = Mathf.Abs(spriteWorldSize.y * visualScale.y);
            if (worldWidth <= 0f || worldHeight <= 0f)
            {
                return Rect.zero;
            }

            Vector2 center = WorldToGuiPoint(worldCenter);
            float guiWidth = Mathf.Abs(WorldToGuiPoint(worldCenter + Vector3.right * worldWidth).x - center.x);
            float guiHeight = Mathf.Abs(WorldToGuiPoint(worldCenter + Vector3.up * worldHeight).y - center.y);
            return new Rect(center.x - guiWidth * 0.5f, center.y - guiHeight * 0.5f, guiWidth, guiHeight);
        }

        private void DrawGuiLine(Vector2 pointA, Vector2 pointB, float thickness)
        {
            Matrix4x4 matrix = GUI.matrix;
            float angle = Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(pointA, pointB);
            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.DrawTexture(new Rect(pointA.x, pointA.y - thickness * 0.5f, length, thickness), lineTexture);
            GUI.matrix = matrix;
        }

        private void DrawWorldCircle(Vector3 center, float radius, Color color, float thickness, int segments)
        {
            if (radius <= 0f || sceneCamera == null)
            {
                return;
            }

            GUI.color = color;
            Vector2 previous = WorldToGuiPoint(center + new Vector3(radius, 0f, 0f));
            for (int i = 1; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                Vector2 next = WorldToGuiPoint(center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                DrawGuiLine(previous, next, thickness);
                previous = next;
            }

            GUI.color = Color.white;
        }

        private void DrawGuiCircle(Vector2 center, float radius, Color color, float thickness, int segments)
        {
            GUI.color = color;
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                DrawGuiLine(previous, next, thickness);
                previous = next;
            }

            GUI.color = Color.white;
        }

        private float DrawTilePaletteGrid(List<TileBase> tiles, int selectedIndex, float y, float width, Action<int> onSelect, Action<int> onDelete)
        {
            if (tiles.Count == 0)
            {
                GUI.Label(new Rect(0f, y, width, 46f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoTileAvailable), warningStyle);
                return y + 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            int pendingDeleteIndex = -1;
            for (int i = 0; i < tiles.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(column * (PaletteTileSize + 10f), y + row * (PaletteTileSize + 22f), PaletteTileSize, PaletteTileSize + 16f);
                if (IsRightClickDeleteRequested(cellRect))
                {
                    pendingDeleteIndex = i;
                }

                if (GUI.Button(cellRect, GUIContent.none, i == selectedIndex ? selectedButtonStyle : buttonStyle))
                {
                    onSelect(i);
                }

                Rect imageRect = new Rect(cellRect.x + 8f, cellRect.y + 8f, PaletteTileSize - 16f, PaletteTileSize - 16f);
                DrawTilePreview(imageRect, tiles[i]);
                GUI.Label(new Rect(cellRect.x + 4f, cellRect.y + PaletteTileSize - 2f, PaletteTileSize - 8f, 18f), Truncate(GetDisplayName(tiles[i]), 5), smallBodyStyle);
            }

            int rows = Mathf.CeilToInt((float)tiles.Count / columns);
            if (pendingDeleteIndex >= 0 && onDelete != null)
            {
                onDelete(pendingDeleteIndex);
            }

            return y + rows * (PaletteTileSize + 22f);
        }

        private float DrawPrefabPaletteGrid(List<GameObject> prefabs, int selectedIndex, float y, float width, Action<int> onSelect, Action<int> onDelete)
        {
            if (prefabs.Count == 0)
            {
                GUI.Label(new Rect(0f, y, width, 46f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoObjectAvailable), warningStyle);
                return y + 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            int pendingDeleteIndex = -1;
            for (int i = 0; i < prefabs.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(column * (PaletteTileSize + 10f), y + row * (PaletteTileSize + 22f), PaletteTileSize, PaletteTileSize + 16f);
                if (IsRightClickDeleteRequested(cellRect))
                {
                    pendingDeleteIndex = i;
                }

                if (GUI.Button(cellRect, GUIContent.none, i == selectedIndex ? selectedButtonStyle : buttonStyle))
                {
                    onSelect(i);
                }

                Rect imageRect = new Rect(cellRect.x + 8f, cellRect.y + 8f, PaletteTileSize - 16f, PaletteTileSize - 16f);
                DrawPrefabPreview(imageRect, prefabs[i]);
                GUI.Label(new Rect(cellRect.x + 4f, cellRect.y + PaletteTileSize - 2f, PaletteTileSize - 8f, 18f), Truncate(GetObjectDisplayName(prefabs[i]), 5), smallBodyStyle);
            }

            int rows = Mathf.CeilToInt((float)prefabs.Count / columns);
            if (pendingDeleteIndex >= 0 && onDelete != null)
            {
                onDelete(pendingDeleteIndex);
            }

            return y + rows * (PaletteTileSize + 22f);
        }

        private bool IsRightClickDeleteRequested(Rect rect)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 1)
            {
                return false;
            }

            if (!rect.Contains(current.mousePosition))
            {
                return false;
            }

            current.Use();
            return true;
        }

        private void DrawModeButtonRow(ref float y, float width, CampusRuntimeBrushMode[] modes, string[] labels)
        {
            float gap = 8f;
            float buttonWidth = (width - gap * (modes.Length - 1)) / modes.Length;
            for (int i = 0; i < modes.Length; i++)
            {
                Rect rect = new Rect(i * (buttonWidth + gap), y, buttonWidth, 32f);
                if (GUI.Button(rect, labels[i], brushMode == modes[i] ? selectedButtonStyle : buttonStyle))
                {
                    brushMode = modes[i];
                }
            }

            y += 40f;
        }

        private void DrawAreaDefinitionRow(Rect rect, string roomName, int count, int required)
        {
            DrawAreaColorSwatch(new Rect(rect.x + 10f, rect.y + 8f, 18f, 18f), roomName);
            GUI.Label(new Rect(rect.x + 38f, rect.y + 4f, rect.width - 108f, 26f), GetAreaPresetLabel(roomName), bodyStyle);
            GUI.Label(new Rect(rect.xMax - 68f, rect.y + 4f, 60f, 26f), count + "/" + required, count >= required ? bodyStyle : warningStyle);
        }

        private void DrawAreaColorSwatch(Rect rect, string roomName)
        {
            if (lineTexture == null)
            {
                return;
            }

            Color color = ResolveRoomOverlayColor(roomName);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, lineTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), lineTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), lineTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), lineTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), lineTexture);
            GUI.color = oldColor;
        }

        private void DrawGameplayPresetSection(ref float y, float width)
        {
            float gap = 8f;
            float buttonWidth = (width - gap) * 0.5f;
            int column = 0;
            for (int i = 0; i < GameplayMarkerPresets.Length; i++)
            {
                CampusRuntimeGameplayMarkerPreset preset = GameplayMarkerPresets[i];
                if (preset == null)
                {
                    continue;
                }

                Rect rect = new Rect(column * (buttonWidth + gap), y, buttonWidth, 30f);
                Color oldColor = GUI.color;
                GUI.color = selectedGameplayPresetIndex == i
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.92f);
                if (GUI.Button(rect, GetGameplayPresetLabel(preset), selectedGameplayPresetIndex == i ? selectedButtonStyle : buttonStyle))
                {
                    selectedGameplayPresetIndex = i;
                    brushMode = CampusRuntimeBrushMode.PlaceGameplayMarker;
                }

                GUI.color = oldColor;
                column++;
                if (column >= 2)
                {
                    column = 0;
                    y += 38f;
                }
            }

            if (column != 0)
            {
                y += 38f;
            }

            y += 10f;
        }

        private float DrawSlider(float y, float width, string label, ref float value, float min, float max)
        {
            GUI.Label(new Rect(0f, y, 64f, 24f), label, bodyStyle);
            value = GUI.HorizontalSlider(new Rect(70f, y + 8f, width - 126f, 18f), value, min, max);
            GUI.Label(new Rect(width - 48f, y, 48f, 24f), value.ToString("0.0"), smallBodyStyle);
            return y + 30f;
        }

        private void DrawLightPreviewCard(ref float y, float width)
        {
            Rect rect = new Rect(0f, y + 4f, width, 112f);
            GUI.Box(rect, GUIContent.none, buttonStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.LightPreview), bodyStyle);
            Rect swatch = new Rect(rect.x + 14f, rect.y + 42f, 32f, 32f);
            Color oldColor = GUI.color;
            GUI.color = lightColor;
            GUI.DrawTexture(swatch, lineTexture);
            GUI.color = oldColor;

            Vector2 center = new Vector2(rect.x + rect.width - 70f, rect.y + 60f);
            float outer = 38f;
            float inner = Mathf.Clamp(lightInnerRadius / Mathf.Max(0.1f, lightOuterRadius), 0f, 1f) * outer;
            DrawGuiCircle(center, outer, new Color(1f, 0.88f, 0.35f, 0.88f), 2f, 40);
            DrawGuiCircle(center, Mathf.Max(4f, inner), new Color(1f, 0.96f, 0.65f, 0.8f), 1f, 32);
            GUI.Label(new Rect(rect.x + 54f, rect.y + 39f, rect.width - 140f, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PointLight), bodyStyle);
            GUI.Label(new Rect(rect.x + 54f, rect.y + 66f, rect.width - 140f, 24f), CampusRuntimeEditorTextCatalog.FormatPointLightStats(displayLanguage, lightIntensity.ToString("0.0"), lightOuterRadius.ToString("0.0"), lightInnerRadius.ToString("0.0")), mutedStyle);
            y += 124f;
        }

        private void DrawDayNightControls(ref float y, float width)
        {
            if (dayNightController == null)
            {
                dayNightController = FindFirstObjectByType<CampusDayNightController>(FindObjectsInactive.Include);
            }

            GUI.Label(new Rect(0f, y, width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DayNight), headerStyle);
            y += 34f;

            if (dayNightController == null)
            {
                GUI.Label(new Rect(0f, y, width, 40f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.MissingDayNightController), warningStyle);
                y += 48f;
                return;
            }

            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.FormatGameTime(displayLanguage, FormatGameTime(dayNightController.GameHour)), bodyStyle);
            y += 30f;

            float speed = dayNightController.DaySpeedMultiplier;
            float editedSpeed = speed;
            y = DrawSlider(y, width, Tr("\u901f\u5ea6", "Speed"), ref editedSpeed, 0.1f, 200f);
            if (!Mathf.Approximately(speed, editedSpeed))
            {
                dayNightController.DaySpeedMultiplier = editedSpeed;
            }

            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.Format(displayLanguage, CampusRuntimeEditorTextId.RealMinutesPerGameDay, dayNightController.RealMinutesPerGameDay.ToString("0.0")), mutedStyle);
            y += 30f;

            float halfWidth = (width - 8f) * 0.5f;
            if (GUI.Button(new Rect(0f, y, halfWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Set1x), buttonStyle))
            {
                dayNightController.DaySpeedMultiplier = 1f;
            }

            if (GUI.Button(new Rect(halfWidth + 8f, y, halfWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Set200x), buttonStyle))
            {
                dayNightController.DaySpeedMultiplier = 200f;
            }

            y += 42f;
        }

        private static string FormatGameTime(float gameHour)
        {
            gameHour = Mathf.Repeat(gameHour, 24f);
            int hour = Mathf.FloorToInt(gameHour);
            int minute = Mathf.FloorToInt((gameHour - hour) * 60f);
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        private void DrawCustomWallPanel(ref float y, float width)
        {
            Rect rect = new Rect(0f, y, width, 238f);
            GUI.Box(rect, GUIContent.none, buttonStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.CustomWall), headerStyle);

            float rowY = rect.y + 42f;
            DrawWallTexturePicker(rect.x + 12f, rowY, rect.width - 24f, Tr("\u5899\u9762\u8d34\u56fe", "Wall Face Texture"), customWallFaceTexture, CampusRuntimeImportTarget.WallFace);
            rowY += 56f;
            DrawWallTexturePicker(rect.x + 12f, rowY, rect.width - 24f, Tr("\u5899\u9876\u8d34\u56fe", "Wall Cap Texture"), customWallCapTexture, CampusRuntimeImportTarget.WallCap);
            rowY += 58f;

            GUI.Label(new Rect(rect.x + 12f, rowY, 78f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Name), bodyStyle);
            customWallName = GUI.TextField(new Rect(rect.x + 92f, rowY, rect.width - 104f, 30f), customWallName, buttonStyle);
            rowY += 40f;

            float buttonWidth = (rect.width - 36f) / 2f;
            if (GUI.Button(new Rect(rect.x + 12f, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.CreateProfile), buttonStyle))
            {
                CreateCustomWallProfile();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ApplyToSelected), buttonStyle))
            {
                ApplyCustomTexturesToSelectedWall();
            }

            rowY += 36f;
            if (GUI.Button(new Rect(rect.x + 12f, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RebuildSelectedFloor), buttonStyle))
            {
                RebuildWallVisuals(EnsureFloor(selectedFloorIndex));
                SchedulePlayerMapSave();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, rowY, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RefreshPresentation), buttonStyle))
            {
                PrepareRuntimeMapPresentationSafe();
            }

            y += rect.height + 12f;
        }

                private void DrawWallTexturePicker(float x, float y, float width, string label, Texture2D texture, CampusRuntimeImportTarget target)
        {
            GUI.Label(new Rect(x, y + 12f, 82f, 28f), label, bodyStyle);
            Rect preview = new Rect(x + width - 48f, y + 4f, 44f, 44f);
            GUI.DrawTexture(preview, texture != null ? texture : tileFallbackTexture, ScaleMode.ScaleToFit);
            float buttonX = x + 86f;
            float buttonWidth = Mathf.Max(62f, (width - 146f) / 2f);
            if (GUI.Button(new Rect(buttonX, y + 10f, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ChooseFile), buttonStyle))
            {
                string path = SelectSingleImageFile(label);
                if (!string.IsNullOrEmpty(path))
                {
                    LoadCustomWallTexture(path, target);
                }
            }

            GUIStyle targetStyle = activeImportTarget == target ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect(buttonX + buttonWidth + 8f, y + 10f, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.UseDragTarget), targetStyle))
            {
                SetActiveImportTarget(target, label);
            }
        }
        private void DrawSelectedObjectSettingsLauncher(ref float y, float width)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            string name = prefab != null ? GetObjectDisplayName(prefab) : Tr("\u672a\u9009\u7269\u54c1", "No Object Selected");

            bool previousEnabled = GUI.enabled;
            GUI.enabled = prefab != null;
            GUI.Box(new Rect(0f, y - 4f, width, 42f), GUIContent.none, objectSettingsHighlightStyle);
            if (GUI.Button(new Rect(8f, y, 156f, 34f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ObjectSettings), showObjectSettings ? selectedButtonStyle : buttonStyle))
            {
                showObjectSettings = !showObjectSettings;
                if (showObjectSettings)
                {
                    SyncObjectSettingsSelection(prefab, placed, true);
                }
            }

            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(174f, y + 3f, Mathf.Max(10f, width - 174f), 28f), Truncate(name, 18), mutedStyle);
            y += 46f;
        }

        private void DrawObjectSettingsPanel()
        {
            if (!showObjectSettings)
            {
                return;
            }

            Rect panelRect = objectSettingsPanelRect;
            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(panelRect.x + 8f, panelRect.y + 8f, panelRect.width - 16f, 84f), GUIContent.none, objectSettingsHighlightStyle);
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 62f, 38f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ObjectSettings), headerStyle);
            if (GUI.Button(new Rect(panelRect.xMax - 46f, panelRect.y + 12f, 32f, 32f), "X", buttonStyle))
            {
                showObjectSettings = false;
                return;
            }

            GameObject prefab = GetSelectedObjectPrefab();
            if (prefab == null)
            {
                GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 62f, panelRect.width - 36f, 40f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectObjectFirst), warningStyle);
                return;
            }

            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed == null)
            {
                GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 62f, panelRect.width - 36f, 70f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.MissingCampusPlacedObject), warningStyle);
                return;
            }

            SyncObjectSettingsSelection(prefab, placed, false);

            float actionY = panelRect.y + 56f;
            if (GUI.Button(new Rect(panelRect.x + 14f, actionY, 132f, 32f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SaveAndSync), buttonStyle))
            {
                CommitObjectSettingsDraft(prefab, placed);
                SaveSelectedObjectSettings();
            }

            if (GUI.Button(new Rect(panelRect.x + 154f, actionY, 184f, 32f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ApplyToAllSameType), buttonStyle))
            {
                CommitObjectSettingsDraft(prefab, placed);
                ApplySelectedObjectSettingsToPlacedInstances();
            }

            GUI.Label(new Rect(panelRect.x + 348f, actionY + 2f, Mathf.Max(10f, panelRect.width - 408f), 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SyncPlacedObjects), mutedStyle);

            Rect scrollRect = new Rect(panelRect.x + 14f, panelRect.y + 98f, panelRect.width - 28f, panelRect.height - 112f);
            float viewWidth = scrollRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(scrollRect.height + 1f, 1430f));
            objectSettingsScroll = GUI.BeginScrollView(scrollRect, objectSettingsScroll, viewRect);
            float y = 0f;

            DrawObjectSettingsRenameControls(ref y, viewWidth, prefab, placed);
            DrawObjectSettingsTypeIdControls(ref y, viewWidth, placed);
            DrawObjectSettingsWallMountControls(ref y, viewWidth, placed);
            DrawObjectSettingsPreviewControls(ref y, viewWidth, prefab, placed);
            DrawObjectSettingsFootprintControls(ref y, viewWidth, placed);
            DrawObjectSettingsStorageControls(ref y, viewWidth, placed);
            DrawObjectSettingsScaleControls(ref y, viewWidth, placed);
            DrawObjectSettingsRotationControls(ref y, viewWidth, placed);
            DrawObjectSettingsAnchorControls(ref y, viewWidth, placed);
            GUI.EndScrollView();
        }

        private void SyncObjectSettingsSelection(GameObject prefab, CampusPlacedObject placed, bool force)
        {
            if (!force && prefab == lastObjectSettingsPrefab)
            {
                return;
            }

            lastObjectSettingsPrefab = prefab;
            lastFootprintSyncedPrefab = null;
            SyncSelectedObjectFootprintFields();
            if (placed != null)
            {
                placed.NormalizeCustomInteractionAnchors();
                placed.NormalizeStorageSettings();
                objectSettingsPreviewRotation90 = CampusPlacedObject.NormalizeRotation90(placed.Rotation90);
                objectSettingsDirectionDropRotation90 = -1;
                selectedCustomAnchorIndex = ResolveInitialCustomAnchorIndex(placed);
            }
        }

        private int ResolveInitialCustomAnchorIndex(CampusPlacedObject placed)
        {
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < placed.CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor anchor = placed.CustomInteractionAnchors[i];
                if (anchor != null && anchor.Enabled)
                {
                    return i;
                }
            }

            return 0;
        }

        private void EnsureSelectedCustomAnchorIndex(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                selectedCustomAnchorIndex = 0;
                return;
            }

            placed.CustomInteractionAnchors = placed.CustomInteractionAnchors ?? new List<CampusPlacedObjectInteractionAnchor>();
            if (placed.CustomInteractionAnchors.Count == 0)
            {
                selectedCustomAnchorIndex = 0;
                return;
            }

            selectedCustomAnchorIndex = Mathf.Clamp(selectedCustomAnchorIndex, 0, placed.CustomInteractionAnchors.Count - 1);
            if (placed.CustomInteractionAnchors[selectedCustomAnchorIndex] == null)
            {
                placed.CustomInteractionAnchors[selectedCustomAnchorIndex] = new CampusPlacedObjectInteractionAnchor();
            }
        }

        private CampusPlacedObjectInteractionAnchor GetSelectedCustomAnchor(CampusPlacedObject placed)
        {
            EnsureSelectedCustomAnchorIndex(placed);
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return null;
            }

            return placed.CustomInteractionAnchors[selectedCustomAnchorIndex];
        }

        private void SyncLegacyAnchorFieldsFromSelectedAnchor(CampusPlacedObject placed)
        {
            CampusPlacedObjectInteractionAnchor anchor = GetSelectedCustomAnchor(placed);
            if (placed == null || anchor == null)
            {
                return;
            }

            placed.CustomInteractionAnchorLocalPosition = anchor.LocalPosition;
            placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(anchor.Radius);
            string fallbackPrompt = !string.IsNullOrWhiteSpace(placed.CustomInteractionPromptText)
                ? placed.CustomInteractionPromptText.Trim()
                : new CampusPlacedObjectInteractionAnchor().PromptText;
            placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(anchor.PromptText) ? fallbackPrompt : anchor.PromptText.Trim();
        }

        private CampusPlacedObjectInteractionAnchor CreateDefaultCustomAnchor(int index)
        {
            int ordinal = Mathf.Max(1, index + 1);
            return new CampusPlacedObjectInteractionAnchor
            {
                AnchorId = "custom_" + ordinal,
                DisplayName = CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectedAnchor) + " " + ordinal,
                Enabled = true,
                LocalPosition = Vector3.zero,
                Radius = CampusPlacedObject.DefaultInteractionAnchorRadius,
                Priority = 120,
                LogInteraction = true
            };
        }

        private void AddCustomInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            placed.CustomInteractionAnchors = placed.CustomInteractionAnchors ?? new List<CampusPlacedObjectInteractionAnchor>();
            placed.CustomInteractionAnchors.Add(CreateDefaultCustomAnchor(placed.CustomInteractionAnchors.Count));
            selectedCustomAnchorIndex = placed.CustomInteractionAnchors.Count - 1;
            placed.UseCustomInteractionAnchor = true;
            SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
        }

        private void RemoveSelectedCustomInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return;
            }

            EnsureSelectedCustomAnchorIndex(placed);
            placed.CustomInteractionAnchors.RemoveAt(selectedCustomAnchorIndex);
            if (placed.CustomInteractionAnchors.Count == 0)
            {
                selectedCustomAnchorIndex = 0;
                placed.UseCustomInteractionAnchor = false;
                placed.CustomInteractionAnchorLocalPosition = Vector3.zero;
                placed.CustomInteractionAnchorRadius = CampusPlacedObject.DefaultInteractionAnchorRadius;
                placed.CustomInteractionPromptText = new CampusPlacedObjectInteractionAnchor().PromptText;
            }
            else
            {
                selectedCustomAnchorIndex = Mathf.Clamp(selectedCustomAnchorIndex, 0, placed.CustomInteractionAnchors.Count - 1);
                SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            }

            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
        }
        private void DrawSelectedObjectFootprintControls(ref float y, float width)
        {
            SyncSelectedObjectFootprintFields();
            GUI.Label(new Rect(0f, y, 70f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Footprint), bodyStyle);
            selectedObjectFootprintX = Mathf.Clamp(ParseIntField(new Rect(72f, y, 48f, 30f), selectedObjectFootprintX), 1, 32);
            GUI.Label(new Rect(126f, y, 20f, 28f), "x", bodyStyle);
            selectedObjectFootprintY = Mathf.Clamp(ParseIntField(new Rect(148f, y, 48f, 30f), selectedObjectFootprintY), 1, 32);
            if (GUI.Button(new Rect(208f, y, 92f, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ApplySize), buttonStyle))
            {
                ApplySelectedObjectFootprint();
            }

            GameObject prefab = GetSelectedObjectPrefab();
            string name = prefab != null ? CampusObjectNames.GetDisplayName(prefab.name) : Tr("\u672a\u9009\u7269\u54c1", "Unselected Object");
            GUI.Label(new Rect(306f, y, Mathf.Max(10f, width - 306f), 28f), Truncate(name, 12), mutedStyle);
            y += 46f;
        }
        private void DrawSelectedObjectSettingsControls(ref float y, float width)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (prefab == null)
            {
                return;
            }

            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed == null)
            {
                return;
            }

            GUI.Label(new Rect(0f, y, width, 26f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ObjectSettings), headerStyle);
            y += 32f;

            bool nextAllowRotation = GUI.Toggle(new Rect(0f, y, width, 24f), placed.AllowRotation, Tr(CampusRuntimeEditorTextId.AllowFourDirections));
            if (nextAllowRotation != placed.AllowRotation)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = nextAllowRotation;
                placed.ApplyRotationVisualState();
            }

            y += 30f;
            DrawObjectDirectionSpriteRow(ref y, width, placed, 0);
            DrawObjectDirectionSpriteRow(ref y, width, placed, 1);
            DrawObjectDirectionSpriteRow(ref y, width, placed, 2);
            DrawObjectDirectionSpriteRow(ref y, width, placed, 3);

            y += 6f;
            bool nextUseAnchor = GUI.Toggle(new Rect(0f, y, width, 24f), placed.UseCustomInteractionAnchor, Tr(CampusRuntimeEditorTextId.UseCustomInteractionAnchor));
            if (nextUseAnchor != placed.UseCustomInteractionAnchor)
            {
                placed.UseCustomInteractionAnchor = nextUseAnchor;
                placed.ApplyInteractionState();
            }

            y += 30f;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = placed.UseCustomInteractionAnchor;
            GUI.Label(new Rect(0f, y, 42f, 28f), "X", bodyStyle);
            Vector3 anchor = placed.CustomInteractionAnchorLocalPosition;
            anchor.x = ParseFloatField(new Rect(44f, y, 58f, 30f), anchor.x, BuildObjectSettingsInputKey(placed, "legacy_anchor_x"));
            GUI.Label(new Rect(110f, y, 42f, 28f), "Y", bodyStyle);
            anchor.y = ParseFloatField(new Rect(152f, y, 58f, 30f), anchor.y, BuildObjectSettingsInputKey(placed, "legacy_anchor_y"));
            placed.CustomInteractionAnchorLocalPosition = anchor;
            GUI.Label(new Rect(218f, y, 48f, 28f), "R", bodyStyle);
            placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(ParseFloatField(new Rect(268f, y, 58f, 30f), placed.CustomInteractionAnchorRadius, BuildObjectSettingsInputKey(placed, "legacy_anchor_radius")));
            y += 36f;

            GUI.Label(new Rect(0f, y, 70f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Prompt), bodyStyle);
            placed.CustomInteractionPromptText = DrawTextInput(new Rect(72f, y, width - 72f, 30f), string.IsNullOrEmpty(placed.CustomInteractionPromptText) ? string.Empty : placed.CustomInteractionPromptText, BuildObjectSettingsInputKey(placed, "legacy_prompt"));
            GUI.enabled = previousEnabled;
            y += 40f;

            if (GUI.Button(new Rect(0f, y, Mathf.Min(150f, width), 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SaveObjectSettings), buttonStyle))
            {
                SaveSelectedObjectSettings();
            }

            GUI.Label(new Rect(160f, y, Mathf.Max(10f, width - 160f), 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SameNamedObjectsSync), mutedStyle);
            y += 42f;
        }

        private void DrawObjectDirectionSpriteRow(ref float y, float width, CampusPlacedObject placed, int rotation90Index)
        {
            int degrees = rotation90Index * 90;
            GUI.Label(new Rect(0f, y, 42f, 28f), degrees.ToString(), bodyStyle);
            Sprite sprite = GetObjectDirectionSprite(placed, rotation90Index);
            string spriteName = sprite != null ? Truncate(sprite.name, 14) : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NotSet);
            GUI.Label(new Rect(46f, y, Mathf.Max(10f, width - 180f), 28f), spriteName, mutedStyle);

            if (GUI.Button(new Rect(width - 122f, y, 56f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PickSprite), buttonStyle))
            {
                SetSelectedObjectDirectionSprite(rotation90Index);
            }

            if (GUI.Button(new Rect(width - 60f, y, 56f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Clear), buttonStyle))
            {
                ClearSelectedObjectDirectionSprite(rotation90Index);
            }

            y += 32f;
        }

        private void DrawColorControls(ref float y, float width, string label, ref Color color)
        {
            GUI.Label(new Rect(0f, y, width, 24f), label, bodyStyle);
            Rect swatch = new Rect(width - 38f, y + 2f, 32f, 22f);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(swatch, lineTexture);
            GUI.color = oldColor;
            y += 28f;
            color.r = DrawColorChannel(y, width, "R", color.r);
            y += 22f;
            color.g = DrawColorChannel(y, width, "G", color.g);
            y += 22f;
            color.b = DrawColorChannel(y, width, "B", color.b);
            color.a = 1f;
            y += 22f;
        }

        private float DrawColorChannel(float y, float width, string label, float value)
        {
            GUI.Label(new Rect(0f, y, 22f, 20f), label, smallBodyStyle);
            value = GUI.HorizontalSlider(new Rect(28f, y + 6f, width - 78f, 16f), value, 0f, 1f);
            GUI.Label(new Rect(width - 44f, y, 44f, 20f), Mathf.RoundToInt(value * 255f).ToString(), smallBodyStyle);
            return value;
        }

        private void DrawImportFolderRow(ref float y, float width, string label, string folder, CampusRuntimeImportTarget target)
        {
            GUI.Label(new Rect(0f, y, width, 26f), label, headerStyle);
            y += 32f;
            float buttonWidth = Mathf.Max(68f, (width - 24f) / 4f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ImportFiles), buttonStyle))
            {
                ImportSelectedFilesIntoFolder(folder, label);
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectFolder), buttonStyle))
            {
                ImportSelectedFolderIntoFolder(folder, label);
            }

            GUIStyle targetStyle = activeImportTarget == target ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect((buttonWidth + 8f) * 2f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DropTarget), targetStyle))
            {
                SetActiveImportTarget(target, label);
            }

            if (GUI.Button(new Rect((buttonWidth + 8f) * 3f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PasteImage), buttonStyle))
            {
                ImportClipboardImagesIntoFolder(folder, label);
            }

            y += 36f;
            if (GUI.Button(new Rect(0f, y, buttonWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.OpenFolder), buttonStyle))
            {
                OpenImportLocation(folder);
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Refresh), buttonStyle))
            {
                ReloadUserImportsFromUi();
                SchedulePlayerMapSave();
            }

            GUI.Label(new Rect((buttonWidth + 8f) * 2f, y, width - (buttonWidth + 8f) * 2f, 28f), activeImportTarget == target ? CampusRuntimeEditorTextCatalog.FormatActiveDropTarget(displayLanguage, label) : Truncate(folder, 22), activeImportTarget == target ? warningStyle : mutedStyle);
            y += 38f;
        }

        private void DrawImportFileRow(ref float y, float width, string label, string filePath)
        {
            GUI.Label(new Rect(0f, y, width, 26f), label, headerStyle);
            y += 32f;
            float buttonWidth = Mathf.Max(68f, (width - 24f) / 4f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ImportText), buttonStyle))
            {
                string path = SelectSingleFile(CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectRoomDefinitionText), "Text|*.txt|All|*.*");
                if (!string.IsNullOrEmpty(path))
                {
                    RecordUndo();
                    int count = ImportRoomDefinitionsFromText(File.ReadAllText(path));
                    SetStatus(count > 0
                        ? CampusRuntimeEditorTextCatalog.Format(displayLanguage, CampusRuntimeEditorTextId.ImportRoomTypesStatus, count)
                        : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoRoomTypesFoundToImport));
                }
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PasteText), buttonStyle))
            {
                RecordUndo();
                int count = ImportRoomDefinitionsFromText(GUIUtility.systemCopyBuffer ?? string.Empty);
                SetStatus(count > 0
                    ? CampusRuntimeEditorTextCatalog.Format(displayLanguage, CampusRuntimeEditorTextId.ImportRoomTypesClipboardStatus, count)
                    : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoRoomTypesFoundInClipboard));
            }

            GUIStyle targetStyle = activeImportTarget == CampusRuntimeImportTarget.Room ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect((buttonWidth + 8f) * 2f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DropTarget), targetStyle))
            {
                SetActiveImportTarget(CampusRuntimeImportTarget.Room, label);
            }

            if (GUI.Button(new Rect((buttonWidth + 8f) * 3f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.OpenFile), buttonStyle))
            {
                OpenImportLocation(filePath);
            }

            y += 36f;
            if (GUI.Button(new Rect(0f, y, buttonWidth, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Refresh), buttonStyle))
            {
                LoadImportedRoomDefinitions();
            }

            GUI.Label(new Rect(buttonWidth + 8f, y, width - buttonWidth - 8f, 28f), activeImportTarget == CampusRuntimeImportTarget.Room ? CampusRuntimeEditorTextCatalog.FormatActiveDropTarget(displayLanguage, label) : Truncate(filePath, 24), activeImportTarget == CampusRuntimeImportTarget.Room ? warningStyle : mutedStyle);
            y += 38f;
        }

        private void SetActiveImportTarget(CampusRuntimeImportTarget target, string label)
        {
            activeImportTarget = target;
            activeImportLabel = label;
            SetStatus(TrFormat("\u62d6\u653e\u76ee\u6807\uff1a{0}\u3002\u5c06\u6587\u4ef6\u6216\u6587\u4ef6\u5939\u62d6\u5165\u6e38\u620f\u89c6\u56fe\u3002", "Drag target: {0}. Drag files or folders into the game view.", label));
        }

        private void SetActiveObjectDirectionSpriteDropTarget(int rotation90Index)
        {
            objectSettingsDirectionDropRotation90 = CampusPlacedObject.NormalizeRotation90(rotation90Index);
            SetStatus(TrFormat("\u62d6\u653e\u76ee\u6807\uff1a\u7269\u4ef6 {0} \u5ea6\u8d34\u56fe\u3002", "Drag target: object {0} deg sprite.", objectSettingsDirectionDropRotation90 * 90));
        }

        private bool TryImportDroppedObjectDirectionSprite(List<string> paths)
        {
            int targetRotation90 = ResolveDroppedObjectDirectionTargetRotation90();
            if (targetRotation90 < 0)
            {
                return false;
            }

            string sourcePath = ResolveFirstDroppedImagePath(paths);
            if (string.IsNullOrEmpty(sourcePath))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                objectSettingsDirectionDropRotation90 = -1;
                return true;
            }

            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed != null && placed.IsWallMounted)
            {
                TryAssignSelectedWallMountedSprite(sourcePath);
            }
            else
            {
                TryAssignSelectedObjectDirectionSprite(targetRotation90, sourcePath);
            }

            objectSettingsDirectionDropRotation90 = -1;
            return true;
        }

        private int ResolveDroppedObjectDirectionTargetRotation90()
        {
            if (objectSettingsDirectionDropRotation90 >= 0)
            {
                return CampusPlacedObject.NormalizeRotation90(objectSettingsDirectionDropRotation90);
            }

            if (!showObjectSettings || !IsMouseOverObjectSettingsPanel() || GetSelectedObjectPrefab() == null)
            {
                return -1;
            }

            return CampusPlacedObject.NormalizeRotation90(objectSettingsPreviewRotation90);
        }

        private bool IsMouseOverObjectSettingsPanel()
        {
            if (!showObjectSettings)
            {
                return false;
            }

            Vector2 mouseScreenPosition =
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                Mouse.current != null ? Mouse.current.position.ReadValue() : Input.mousePosition;
#else
                Input.mousePosition;
#endif
            Vector2 guiPosition = new Vector2(mouseScreenPosition.x, Screen.height - mouseScreenPosition.y);
            return objectSettingsPanelRect.Contains(guiPosition);
        }

        private string ResolveFirstDroppedImagePath(List<string> paths)
        {
            if (paths == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (File.Exists(path) && IsSupportedImportImage(path))
                {
                    return path;
                }

                if (Directory.Exists(path))
                {
                    string[] files = GetImportImageFiles(path);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
            }

            return string.Empty;
        }

        private void LoadCustomWallTexture(string path, CampusRuntimeImportTarget target)
        {
            if (!IsSupportedImportImage(path))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                return;
            }

            Texture2D texture = LoadImportedTexture(path);
            if (texture == null)
            {
                return;
            }

            if (target == CampusRuntimeImportTarget.WallFace)
            {
                customWallFaceTexture = texture;
            }
            else if (target == CampusRuntimeImportTarget.WallCap)
            {
                customWallCapTexture = texture;
            }
        }

        private void CreateCustomWallProfile()
        {
            Texture2D face = customWallFaceTexture != null ? customWallFaceTexture : customWallCapTexture;
            Texture2D cap = customWallCapTexture != null ? customWallCapTexture : customWallFaceTexture;
            if (face == null && cap == null)
            {
                SetStatus(Tr("\u8bf7\u5148\u9009\u62e9\u5899\u9762\u6216\u5899\u9876\u8d34\u56fe\u3002", "Choose a wall face or wall cap texture first."));
                return;
            }

            string cleanName = string.IsNullOrWhiteSpace(customWallName) ? "CustomWall" : customWallName.Trim();
            Sprite sprite = Sprite.Create(cap != null ? cap : face, new Rect(0f, 0f, (cap != null ? cap.width : face.width), (cap != null ? cap.height : face.height)), new Vector2(0.5f, 0.5f), Mathf.Max(1f, Mathf.Max(cap != null ? cap.width : face.width, cap != null ? cap.height : face.height)));
            sprite.name = cleanName + "_Preview";
            sprite.hideFlags = HideFlags.DontSave;
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = cleanName + "_WallLogic";
            tile.sprite = sprite;

            CampusWallRenderProfile profile = ScriptableObject.CreateInstance<CampusWallRenderProfile>();
            profile.name = cleanName + " Wall Profile";
            profile.ProfileId = cleanName;
            profile.FaceSourceTexture = face;
            profile.CapSourceTexture = cap;
            profile.LogicTile = tile;
            profile.hideFlags = HideFlags.DontSave;

            importedAssets.Add(sprite);
            importedAssets.Add(tile);
            importedAssets.Add(profile);
            AddUnique(runtimeCustomWallTiles, tile);
            AddUnique(runtimeCustomWallProfiles, profile);
            AddUnique(wallTiles, tile);
            AddUnique(wallProfiles, profile);
            EnsureRuntimeWallCatalog(profile);

            selectedWallTileIndex = wallTiles.IndexOf(tile);
            selectedWallProfileIndex = wallProfiles.IndexOf(profile);
            fallbackWallProfile = profile;
            brushMode = CampusRuntimeBrushMode.PaintWall;
            RebuildWallVisuals(EnsureFloor(selectedFloorIndex));
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u521b\u5efa\u5899\u4f53\u914d\u7f6e\uff1a{0}", "Created wall profile: {0}", cleanName));
        }

        private void ApplyCustomTexturesToSelectedWall()
        {
            CampusWallRenderProfile profile = selectedWallProfileIndex >= 0 && selectedWallProfileIndex < wallProfiles.Count ? wallProfiles[selectedWallProfileIndex] : fallbackWallProfile;
            if (profile == null)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u7528\u5899\u4f53\u914d\u7f6e\u3002", "No wall profile is available."));
                return;
            }

            if (customWallFaceTexture != null)
            {
                profile.FaceSourceTexture = customWallFaceTexture;
            }

            if (customWallCapTexture != null)
            {
                profile.CapSourceTexture = customWallCapTexture;
            }

            RebuildWallVisuals(EnsureFloor(selectedFloorIndex));
            SchedulePlayerMapSave();
            SetStatus(Tr("\u5899\u4f53\u8d34\u56fe\u5df2\u5e94\u7528\u3002", "Wall textures applied."));
        }

        private void EnsureRuntimeWallCatalog(CampusWallRenderProfile profile)
        {
            if (wallVisualCatalog == null)
            {
                wallVisualCatalog = ScriptableObject.CreateInstance<CampusWallVisualCatalog>();
                wallVisualCatalog.name = "Runtime Wall Visual Catalog";
                wallVisualCatalog.hideFlags = HideFlags.DontSave;
                wallVisualCatalog.Profiles = new List<CampusWallRenderProfile>();
                importedAssets.Add(wallVisualCatalog);
            }

            if (wallVisualCatalog.DefaultProfile == null)
            {
                wallVisualCatalog.DefaultProfile = fallbackWallProfile != null ? fallbackWallProfile : profile;
            }

            if (wallVisualCatalog.Profiles == null)
            {
                wallVisualCatalog.Profiles = new List<CampusWallRenderProfile>();
            }

            if (!wallVisualCatalog.Profiles.Contains(profile))
            {
                wallVisualCatalog.Profiles.Add(profile);
            }
        }

        private void SyncSelectedObjectFootprintFields()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (prefab == lastFootprintSyncedPrefab)
            {
                return;
            }

            lastFootprintSyncedPrefab = prefab;
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            if (placed == null)
            {
                selectedObjectFootprintX = Mathf.Max(1, selectedObjectFootprintX);
                selectedObjectFootprintY = Mathf.Max(1, selectedObjectFootprintY);
                return;
            }

            selectedObjectFootprintX = Mathf.Max(1, placed.NormalizedFootprintSize.x);
            selectedObjectFootprintY = Mathf.Max(1, placed.NormalizedFootprintSize.y);
        }

        private void ApplySelectedObjectFootprint()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            if (prefab == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed == null)
            {
                return;
            }

            placed.FootprintSize = new Vector2Int(Mathf.Clamp(selectedObjectFootprintX, 1, 32), Mathf.Clamp(selectedObjectFootprintY, 1, 32));
            placed.OverrideFootprintSize = true;
            lastFootprintSyncedPrefab = prefab;
            SaveSelectedObjectSettings();
            SetStatus(TrFormat("\u5df2\u5e94\u7528\u5360\u5730\u5c3a\u5bf8\uff1a{0}x{1}", "Applied footprint size: {0}x{1}", placed.FootprintSize.x, placed.FootprintSize.y));
        }

        private void ConfigureWallMountedSettings(CampusPlacedObject placed, bool enabled, bool clearDirectionalOverrides)
        {
            if (placed == null)
            {
                return;
            }

            placed.IsWallMounted = enabled;
            if (enabled)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = Vector2Int.one;
                placed.SortingOrderOffset = Mathf.Max(placed.SortingOrderOffset, 1);
                placed.BlocksMovement = false;
                placed.BlocksSight = false;
                if (clearDirectionalOverrides)
                {
                    AssignRuntimeObjectDirectionSprite(placed, 1, false, string.Empty, placed.ObjectId);
                    AssignRuntimeObjectDirectionSprite(placed, 2, false, string.Empty, placed.ObjectId);
                    AssignRuntimeObjectDirectionSprite(placed, 3, false, string.Empty, placed.ObjectId);
                }
            }

            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
        }

        private void SetSelectedWallMountedSprite()
        {
            string sourcePath = SelectSingleImageFile(Tr("\u9009\u62e9\u58c1\u6302\u4e3b\u8d34\u56fe", "Choose Wall-Mounted Main Sprite"));
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            TryAssignSelectedWallMountedSprite(sourcePath);
        }

        private bool TryAssignSelectedWallMountedSprite(string sourcePath)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return false;
            }

            if (!IsSupportedImportImage(sourcePath))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                return false;
            }

            try
            {
                ConfigureWallMountedSettings(placed, true, true);
                string storedPath = CopyObjectDirectionSprite(prefab.name, 0, sourcePath);
                AssignRuntimeObjectDirectionSprite(placed, 0, true, storedPath, prefab.name);
                placed.ApplyRotationVisualState();
                SaveSelectedObjectSettings();
                SetStatus(Tr("\u5df2\u8bbe\u7f6e\u58c1\u6302\u4e3b\u8d34\u56fe\u3002", "Wall-mounted main sprite set."));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to set wall-mounted sprite: " + exception.Message);
                SetStatus(Tr("\u58c1\u6302\u8d34\u56fe\u8bbe\u7f6e\u5931\u8d25\u3002", "Failed to set wall-mounted sprite."));
                return false;
            }
        }

        private void ClearSelectedWallMountedSprite()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            AssignRuntimeObjectDirectionSprite(placed, 0, false, string.Empty, prefab.name);
            placed.ApplyRotationVisualState();
            SaveSelectedObjectSettings();
            SetStatus(Tr("\u5df2\u6e05\u7a7a\u58c1\u6302\u4e3b\u8d34\u56fe\u3002", "Wall-mounted main sprite cleared."));
        }

        private void SetSelectedObjectDirectionSprite(int rotation90Index)
        {
            string sourcePath = SelectSingleImageFile(TrFormat("\u9009\u62e9 {0} \u5ea6\u8d34\u56fe", "Choose {0} deg Sprite", rotation90Index * 90));
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            TryAssignSelectedObjectDirectionSprite(rotation90Index, sourcePath);
        }

        private bool TryAssignSelectedObjectDirectionSprite(int rotation90Index, string sourcePath)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return false;
            }

            if (placed.IsWallMounted)
            {
                return rotation90Index == 0 && TryAssignSelectedWallMountedSprite(sourcePath);
            }

            if (!IsSupportedImportImage(sourcePath))
            {
                SetStatus(Tr("\u8bf7\u9009\u62e9 png/jpg/jpeg/bmp \u56fe\u7247\u3002", "Choose a png/jpg/jpeg/bmp image."));
                return false;
            }

            try
            {
                string storedPath = CopyObjectDirectionSprite(prefab.name, rotation90Index, sourcePath);
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                AssignRuntimeObjectDirectionSprite(placed, rotation90Index, true, storedPath, prefab.name);
                objectSettingsPreviewRotation90 = CampusPlacedObject.NormalizeRotation90(rotation90Index);
                placed.ApplyRotationVisualState();
                SaveSelectedObjectSettings();
                SetStatus(TrFormat("\u5df2\u8bbe\u7f6e\u65cb\u8f6c\u8d34\u56fe\uff1a{0}", "Direction sprite set: {0}", rotation90Index * 90));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to set object direction sprite: " + exception.Message);
                SetStatus(Tr("\u65cb\u8f6c\u8d34\u56fe\u8bbe\u7f6e\u5931\u8d25\u3002", "Failed to set direction sprite."));
                return false;
            }
        }

        private void ClearSelectedObjectDirectionSprite(int rotation90Index)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            if (placed.IsWallMounted)
            {
                if (rotation90Index == 0)
                {
                    ClearSelectedWallMountedSprite();
                }

                return;
            }

            AssignRuntimeObjectDirectionSprite(placed, rotation90Index, false, string.Empty, prefab.name);
            placed.ApplyRotationVisualState();
            SaveSelectedObjectSettings();
            SetStatus(TrFormat("\u5df2\u6e05\u7a7a\u65cb\u8f6c\u8d34\u56fe\uff1a{0}", "Direction sprite cleared: {0}", rotation90Index * 90));
        }

        private void SaveSelectedObjectSettings()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            if (placed.UseCustomInteractionAnchor)
            {
                placed.IsInteractable = true;
            }

            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
            CampusRuntimeObjectSettings settings = CaptureRuntimeObjectSettings(prefab, placed);
            SaveRuntimeObjectSettings(settings);
            int appliedCount = ApplyObjectSettingsToPlacedInstances(prefab, settings, true);
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u7269\u54c1\u8bbe\u7f6e\uff1a{0}\uff0c\u5df2\u540c\u6b65 {1} \u4e2a\u573a\u4e0a\u540c\u7c7b\u7269\u54c1\u3002", "Saved object settings: {0}. Synced {1} placed objects of the same type.", GetObjectDisplayName(prefab), appliedCount));
        }

        private void ApplySelectedObjectSettingsToPlacedInstances()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus(Tr(CampusRuntimeEditorTextId.SelectObjectFirst));
                return;
            }

            if (placed.UseCustomInteractionAnchor)
            {
                placed.IsInteractable = true;
            }

            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
            CampusRuntimeObjectSettings settings = CaptureRuntimeObjectSettings(prefab, placed);
            SaveRuntimeObjectSettings(settings);
            int appliedCount = ApplyObjectSettingsToPlacedInstances(prefab, settings, true);
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u5e94\u7528\u5230\u573a\u4e0a\u540c\u7c7b\u7269\u54c1\uff1a{0} \u4e2a {1}", "Applied to same-type placed objects: {0} {1}", appliedCount, GetObjectDisplayName(prefab)));
        }

        private int ApplyObjectSettingsToPlacedInstances(GameObject prefab, CampusRuntimeObjectSettings settings, bool recordUndo)
        {
            if (prefab == null || settings == null || mapRoot == null)
            {
                return 0;
            }

            string targetObjectTypeKey = ResolveObjectTypeKey(settings.TypeId, settings.ObjectId, prefab.name);
            if (string.IsNullOrEmpty(targetObjectTypeKey))
            {
                return 0;
            }

            int appliedCount = 0;
            bool undoRecorded = false;
            HashSet<CampusFloorRoot> affectedFloors = new HashSet<CampusFloorRoot>();
            mapRoot.RebuildFloorReferences();
            CampusPlacedObject[] objects = mapRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
            {
                CampusPlacedObject placed = objects[objectIndex];
                if (!DoesPlacedObjectMatchTypeKey(placed, targetObjectTypeKey))
                {
                    continue;
                }

                if (recordUndo && !undoRecorded)
                {
                    RecordUndo();
                    undoRecorded = true;
                }

                int preservedRotation = placed.Rotation90;
                Vector3Int preservedCell = placed.Cell;
                int preservedFloor = placed.FloorIndex;
                ApplyRuntimeObjectSettings(placed.gameObject, settings);
                placed.Rotation90 = preservedRotation;
                placed.Cell = preservedCell;
                placed.FloorIndex = preservedFloor;
                placed.ApplyRotationVisualState();
                placed.ApplyInteractionState();

                CampusFloorRoot floor = ResolveFloorForPlacedObject(placed);
                if (floor != null)
                {
                    affectedFloors.Add(floor);
                    if (floor.Grid != null)
                    {
                        placed.ApplyCellToTransform(floor.Grid);
                    }
                }

                appliedCount++;
            }

            foreach (CampusFloorRoot floor in affectedFloors)
            {
                if (floor == null)
                {
                    continue;
                }

                CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            }

            return appliedCount;
        }

        private void ApplySavedObjectSettingsToPalette()
        {
            for (int i = 0; i < objectPrefabs.Count; i++)
            {
                GameObject prefab = objectPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                CampusRuntimeObjectSettings settings = LoadRuntimeObjectSettings(prefab.name);
                if (settings != null)
                {
                    ApplyRuntimeObjectSettings(prefab, settings);
                }
            }
        }

        private CampusPlacedObject ApplyRuntimeObjectSettings(GameObject target, CampusRuntimeObjectSettings settings)
        {
            if (target == null || settings == null)
            {
                return null;
            }

            CampusPlacedObject placed = EnsureRuntimePlacedObject(target);
            if (placed == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(placed.ObjectId))
            {
                placed.ObjectId = !string.IsNullOrWhiteSpace(settings.ObjectId) ? settings.ObjectId : target.name;
            }

            placed.TypeId = string.IsNullOrWhiteSpace(settings.TypeId)
                ? string.Empty
                : settings.TypeId.Trim();
            placed.DisplayNameOverride = string.IsNullOrWhiteSpace(settings.DisplayNameOverride)
                ? string.Empty
                : settings.DisplayNameOverride.Trim();
            placed.VisualScale = CampusPlacedObject.NormalizeVisualScale(settings.VisualScale);
            placed.LockVisualScaleAspect = settings.LockVisualScaleAspect;
            if (placed.LockVisualScaleAspect)
            {
                float uniform = Mathf.Max(placed.VisualScale.x, placed.VisualScale.y);
                placed.VisualScale = new Vector2(uniform, uniform);
            }

            placed.OverrideFootprintSize = settings.OverrideFootprintSize;
            placed.FootprintSize = CampusPlacedObject.NormalizeFootprintSize(settings.FootprintSize);
            placed.IsWallMounted = settings.IsWallMounted;
            if (placed.IsWallMounted)
            {
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = Vector2Int.one;
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                placed.BlocksMovement = false;
                placed.BlocksSight = false;
            }

            placed.OverrideAllowRotation = settings.OverrideAllowRotation;
            placed.AllowRotation = settings.AllowRotation;
            if (placed.IsWallMounted)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
            }

            AssignRuntimeObjectDirectionSprite(placed, 0, settings.OverrideRotation0Sprite, settings.Rotation0SpritePath, target.name);
            AssignRuntimeObjectDirectionSprite(placed, 1, settings.OverrideRotation90Sprite, settings.Rotation90SpritePath, target.name);
            AssignRuntimeObjectDirectionSprite(placed, 2, settings.OverrideRotation180Sprite, settings.Rotation180SpritePath, target.name);
            AssignRuntimeObjectDirectionSprite(placed, 3, settings.OverrideRotation270Sprite, settings.Rotation270SpritePath, target.name);

            placed.UseCustomInteractionAnchor = settings.UseCustomInteractionAnchor;
            placed.CustomInteractionAnchorLocalPosition = settings.CustomInteractionAnchorLocalPosition;
            placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(settings.CustomInteractionAnchorRadius);
            placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(settings.CustomInteractionPromptText)
                ? Tr("\u4ea4\u4e92", "Interact")
                : settings.CustomInteractionPromptText;
            placed.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(settings.CustomInteractionAnchors);
            placed.IsStorageContainer = settings.IsStorageContainer;
            placed.StorageSize = CampusPlacedObject.NormalizeStorageSize(settings.StorageSize);
            placed.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(settings.StorageMaxWeight);
            if (placed.IsStorageContainer)
            {
                EnsureStorageInteractionAnchor(placed);
            }

            placed.IsInteractable = placed.UseCustomInteractionAnchor || placed.IsStorageContainer;

            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
            return placed;
        }

        private CampusRuntimeObjectSettings CaptureRuntimeObjectSettings(GameObject prefab, CampusPlacedObject placed)
        {
            CampusRuntimeObjectSettings settings = new CampusRuntimeObjectSettings();
            settings.ObjectId = prefab != null ? prefab.name : (placed != null ? placed.ObjectId : string.Empty);
            if (placed == null)
            {
                return settings;
            }

            placed.NormalizeCustomInteractionAnchors();
            placed.NormalizeStorageSettings();
            settings.TypeId = string.IsNullOrWhiteSpace(placed.TypeId) ? string.Empty : placed.TypeId.Trim();
            settings.DisplayNameOverride = string.IsNullOrWhiteSpace(placed.DisplayNameOverride) ? string.Empty : placed.DisplayNameOverride.Trim();
            settings.OverrideFootprintSize = placed.OverrideFootprintSize;
            settings.FootprintSize = placed.NormalizedFootprintSize;
            settings.VisualScale = placed.NormalizedVisualScale;
            settings.LockVisualScaleAspect = placed.LockVisualScaleAspect;
            settings.IsWallMounted = placed.IsWallMounted;
            settings.OverrideAllowRotation = placed.OverrideAllowRotation;
            settings.AllowRotation = placed.AllowRotation;
            settings.OverrideRotation0Sprite = placed.OverrideRotation0Sprite;
            settings.Rotation0SpritePath = NormalizeSerializedImportPath(placed.Rotation0SpritePath);
            settings.OverrideRotation90Sprite = placed.OverrideRotation90Sprite;
            settings.Rotation90SpritePath = NormalizeSerializedImportPath(placed.Rotation90SpritePath);
            settings.OverrideRotation180Sprite = placed.OverrideRotation180Sprite;
            settings.Rotation180SpritePath = NormalizeSerializedImportPath(placed.Rotation180SpritePath);
            settings.OverrideRotation270Sprite = placed.OverrideRotation270Sprite;
            settings.Rotation270SpritePath = NormalizeSerializedImportPath(placed.Rotation270SpritePath);
            settings.UseCustomInteractionAnchor = placed.UseCustomInteractionAnchor;
            settings.CustomInteractionAnchorLocalPosition = placed.CustomInteractionAnchorLocalPosition;
            settings.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(placed.CustomInteractionAnchorRadius);
            settings.CustomInteractionPromptText = string.IsNullOrWhiteSpace(placed.CustomInteractionPromptText)
                ? Tr("\u4ea4\u4e92", "Interact")
                : placed.CustomInteractionPromptText.Trim();
            settings.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(placed.CustomInteractionAnchors);
            settings.IsStorageContainer = placed.IsStorageContainer;
            settings.StorageSize = placed.NormalizedStorageSize;
            settings.StorageMaxWeight = placed.NormalizedStorageMaxWeight;
            return settings;
        }

        private void SaveRuntimeObjectSettings(CampusRuntimeObjectSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.ObjectId))
            {
                return;
            }

            string folder = GetObjectSettingsFolder(settings.ObjectId);
            Directory.CreateDirectory(folder);
            File.WriteAllText(GetObjectSettingsPath(settings.ObjectId), JsonUtility.ToJson(settings, true), Encoding.UTF8);
            RefreshImportAssetDatabaseIfProjectBacked();
        }

        private CampusRuntimeObjectSettings LoadRuntimeObjectSettings(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            string path = GetObjectSettingsPath(objectId);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<CampusRuntimeObjectSettings>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to load object settings '" + path + "': " + exception.Message);
                return null;
            }
        }

        private void AssignRuntimeObjectDirectionSprite(CampusPlacedObject placed, int rotation90Index, bool hasOverride, string spritePath, string objectName)
        {
            if (placed == null)
            {
                return;
            }

            string normalizedSpritePath = NormalizeSerializedImportPath(spritePath);
            bool enableOverride = hasOverride && !string.IsNullOrWhiteSpace(normalizedSpritePath);
            Vector2Int spriteFootprint = CampusPlacedObject.RotateFootprintSize(placed.NormalizedFootprintSize, rotation90Index);
            Sprite sprite = enableOverride
                ? LoadRuntimeObjectSprite(normalizedSpritePath, objectName + "_" + (rotation90Index * 90), spriteFootprint)
                : null;
            switch (CampusPlacedObject.NormalizeRotation90(rotation90Index))
            {
                case 0:
                    placed.OverrideRotation0Sprite = enableOverride;
                    placed.Rotation0SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation0Sprite = sprite;
                    break;
                case 1:
                    placed.OverrideRotation90Sprite = enableOverride;
                    placed.Rotation90SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation90Sprite = sprite;
                    break;
                case 2:
                    placed.OverrideRotation180Sprite = enableOverride;
                    placed.Rotation180SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation180Sprite = sprite;
                    break;
                case 3:
                    placed.OverrideRotation270Sprite = enableOverride;
                    placed.Rotation270SpritePath = enableOverride ? normalizedSpritePath : string.Empty;
                    placed.Rotation270Sprite = sprite;
                    break;
            }
        }

        private Sprite LoadRuntimeObjectSprite(string path, string spriteName, Vector2Int footprint)
        {
            string normalizedPath = NormalizeSerializedImportPath(path);
            string resolvedPath = ResolveImportContentPath(normalizedPath);
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                return null;
            }

            Vector2Int normalizedFootprint = CampusPlacedObject.NormalizeFootprintSize(footprint);
            string cacheKey = resolvedPath.Replace('\\', '/') + "|" + normalizedFootprint.x + "x" + normalizedFootprint.y;
            if (runtimeObjectSpriteCache.TryGetValue(cacheKey, out Sprite cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            Texture2D texture = LoadImportedTexture(resolvedPath);
            if (texture == null)
            {
                return null;
            }

            Sprite sprite = CreateObjectSprite(texture, spriteName, normalizedFootprint);
            if (sprite != null)
            {
                runtimeObjectSpriteCache[cacheKey] = sprite;
            }

            return sprite;
        }

        private string CopyObjectDirectionSprite(string objectId, int rotation90Index, string sourcePath)
        {
            string folder = GetObjectSettingsFolder(objectId);
            Directory.CreateDirectory(folder);
            string prefix = "rotation_" + (rotation90Index * 90);
            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".png";
            }

            string targetPath = Path.Combine(folder, prefix + extension.ToLowerInvariant());
            string sourceFullPath = Path.GetFullPath(sourcePath);
            string targetFullPath = Path.GetFullPath(targetPath);
            if (string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeSerializedImportPath(targetPath);
            }

            string[] existing = Directory.GetFiles(folder, prefix + ".*");
            for (int i = 0; i < existing.Length; i++)
            {
                if (!string.Equals(Path.GetFullPath(existing[i]), sourceFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(existing[i]);
                }
            }

            File.Copy(sourcePath, targetPath, true);
            RefreshImportAssetDatabaseIfProjectBacked();
            return NormalizeSerializedImportPath(targetPath);
        }

        private Sprite GetObjectDirectionSprite(CampusPlacedObject placed, int rotation90Index)
        {
            if (placed == null)
            {
                return null;
            }

            switch (CampusPlacedObject.NormalizeRotation90(rotation90Index))
            {
                case 0:
                    return placed.Rotation0Sprite;
                case 1:
                    return placed.Rotation90Sprite;
                case 2:
                    return placed.Rotation180Sprite;
                case 3:
                    return placed.Rotation270Sprite;
                default:
                    return null;
            }
        }

        private static string InferObjectTypeId(string objectId, string displayName, bool isStorageContainer)
        {
            if (isStorageContainer)
            {
                return nameof(CampusFacilityType.Storage);
            }

            string key = NormalizeObjectTypeIdSeed(objectId) + "|" + NormalizeObjectTypeIdSeed(displayName);
            if (ContainsObjectTypeToken(key, "studentdesk", "student_desk", "desk_1x1", "\u8bfe\u684c", "\u4e66\u684c"))
            {
                return nameof(CampusFacilityType.StudentDesk);
            }

            if (ContainsObjectTypeToken(key, "officedesk", "office_desk", "teacherdesk", "teacher_desk", "\u529e\u516c\u684c", "\u6559\u5e08\u684c"))
            {
                return nameof(CampusFacilityType.OfficeDesk);
            }

            if (ContainsObjectTypeToken(key, "blackboard", "whiteboard", "chalkboard", "\u9ed1\u677f", "\u767d\u677f"))
            {
                return nameof(CampusFacilityType.Blackboard);
            }

            if (ContainsObjectTypeToken(key, "podium", "teacherpodium", "teacher_podium", "\u8bb2\u53f0"))
            {
                return nameof(CampusFacilityType.Podium);
            }

            if (ContainsObjectTypeToken(key, "canteencounter", "canteen_counter", "foodcounter", "food_counter", "\u98df\u5802\u67dc\u53f0", "\u7a97\u53e3"))
            {
                return nameof(CampusFacilityType.CanteenCounter);
            }

            if (ContainsObjectTypeToken(key, "canteenqueue", "canteen_queue", "mealqueue", "meal_queue", "\u98df\u5802\u6392\u961f"))
            {
                return nameof(CampusFacilityType.CanteenQueuePoint);
            }

            if (ContainsObjectTypeToken(key, "foodtray", "food_tray", "friedchicken", "burger", "oden", "\u6258\u76d8", "\u70b8\u9e21", "\u6c49\u5821", "\u5173\u4e1c\u716e"))
            {
                return nameof(CampusFacilityType.CanteenFoodTray);
            }

            if (ContainsObjectTypeToken(key, "diningtable", "dining_table", "\u9910\u684c", "\u5403\u996d"))
            {
                return nameof(CampusFacilityType.DiningTable);
            }

            if (ContainsObjectTypeToken(key, "storeshelf", "store_shelf", "shopshelf", "shop_shelf", "snackshelf", "\u8d27\u67b6", "\u96f6\u98df\u67b6"))
            {
                return nameof(CampusFacilityType.StoreShelf);
            }

            if (ContainsObjectTypeToken(key, "storecheckout", "store_checkout", "cashregister", "cash_register", "checkout", "\u6536\u94f6"))
            {
                return nameof(CampusFacilityType.StoreCheckout);
            }

            if (ContainsObjectTypeToken(key, "storequeue", "store_queue", "checkoutqueue", "checkout_queue", "\u6536\u94f6\u961f", "\u8d85\u5e02\u6392\u961f"))
            {
                return nameof(CampusFacilityType.StoreQueuePoint);
            }

            if (ContainsObjectTypeToken(key, "delivery", "takeout", "waimai", "\u5916\u5356"))
            {
                return nameof(CampusFacilityType.DeliveryDropPoint);
            }

            if (ContainsObjectTypeToken(key, "bulletin", "\u516c\u544a\u680f"))
            {
                return nameof(CampusFacilityType.BulletinBoard);
            }

            if (ContainsObjectTypeToken(key, "recruitment", "recruit", "\u62db\u52df"))
            {
                return nameof(CampusFacilityType.Recruitment);
            }

            if (ContainsObjectTypeToken(key, "restroomstall", "restroom_stall", "stall", "\u9694\u95f4"))
            {
                return nameof(CampusFacilityType.RestroomStall);
            }

            if (ContainsObjectTypeToken(key, "urinal", "\u5c0f\u4fbf\u6c60"))
            {
                return nameof(CampusFacilityType.Urinal);
            }

            if (ContainsObjectTypeToken(key, "sink", "\u6d17\u624b\u6c60", "\u6c34\u6c60"))
            {
                return nameof(CampusFacilityType.Sink);
            }

            if (ContainsObjectTypeToken(key, "bed", "\u5e8a"))
            {
                return nameof(CampusFacilityType.Bed);
            }

            if (ContainsObjectTypeToken(key, "chair", "\u6905\u5b50"))
            {
                return nameof(CampusFacilityType.Chair);
            }

            if (ContainsObjectTypeToken(key, "door", "\u95e8"))
            {
                return nameof(CampusFacilityType.Door);
            }

            if (ContainsObjectTypeToken(key, "stair", "\u697c\u68af"))
            {
                return nameof(CampusFacilityType.Stair);
            }

            return string.Empty;
        }

        private static string NormalizeObjectTypeIdSeed(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", string.Empty).Replace("-", "_").ToLowerInvariant();
        }

        private static bool ContainsObjectTypeToken(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = NormalizeObjectTypeIdSeed(tokens[i]);
                if (!string.IsNullOrEmpty(token) && value.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        private CampusPlacedObject EnsureRuntimePlacedObject(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            CampusPlacedObject placed = target.GetComponent<CampusPlacedObject>();
            if (placed == null)
            {
                if (!target.scene.IsValid())
                {
                    SetStatus(Tr("\u8be5\u7269\u54c1\u9884\u5236\u4f53\u7f3a\u5c11 CampusPlacedObject\uff0c\u8bf7\u5148\u5728\u9879\u76ee\u4e2d\u914d\u7f6e\u3002", "This object prefab is missing CampusPlacedObject. Configure it in the project first."));
                    return null;
                }

                placed = target.AddComponent<CampusPlacedObject>();
            }

            if (string.IsNullOrWhiteSpace(placed.ObjectId))
            {
                placed.ObjectId = target.name;
            }

            return placed;
        }

        private bool DoesPlacedObjectMatchTypeKey(CampusPlacedObject placed, string targetObjectTypeKey)
        {
            if (placed == null || string.IsNullOrEmpty(targetObjectTypeKey))
            {
                return false;
            }

            string objectTypeKey = ResolvePlacedObjectTypeKey(placed);
            return string.Equals(objectTypeKey, targetObjectTypeKey, StringComparison.Ordinal);
        }

        private string ResolvePlacedObjectTypeKey(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return string.Empty;
            }

            return ResolveObjectTypeKey(placed.TypeId, placed.ObjectId, placed.gameObject != null ? placed.gameObject.name : string.Empty);
        }

        private static string ResolveObjectTypeKey(string typeId, string objectId, string fallbackName)
        {
            string resolved = !string.IsNullOrWhiteSpace(typeId)
                ? typeId
                : !string.IsNullOrWhiteSpace(objectId)
                    ? objectId
                    : fallbackName;
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return string.Empty;
            }

            return resolved.Trim();
        }

        private CampusFloorRoot ResolveFloorForPlacedObject(CampusPlacedObject placed)
        {
            if (placed == null || mapRoot == null || mapRoot.Floors == null)
            {
                return null;
            }

            Transform placedTransform = placed.transform;
            for (int floorIndex = 0; floorIndex < mapRoot.Floors.Count; floorIndex++)
            {
                CampusFloorRoot floor = mapRoot.Floors[floorIndex];
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                if (placedTransform.IsChildOf(floor.PropsRoot))
                {
                    return floor;
                }
            }

            return mapRoot.GetFloor(placed.FloorIndex);
        }

        private void OpenImportLocation(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            GUIUtility.systemCopyBuffer = path;
            try
            {
                Application.OpenURL(new Uri(path).AbsoluteUri);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to open folder '" + path + "': " + exception.Message);
            }

            SetStatus(TrFormat("\u5df2\u6253\u5f00\u5bfc\u5165\u76ee\u5f55\uff1a{0}", "Opened import folder: {0}", path));
        }

        private void DrawToolbarButton(ref float x, float y, string label, Action action)
        {
            DrawToolbarButton(ref x, y, label, action, true);
        }

        private void DrawToolbarButton(ref float x, float y, string label, Action action, bool enabled)
        {
            GUI.enabled = enabled;
            if (GUI.Button(new Rect(x, y, ToolbarButtonWidth, 46f), label, buttonStyle))
            {
                action();
            }

            GUI.enabled = true;
            x += ToolbarButtonWidth + 10f;
        }

        private void DrawTilePreview(Rect rect, TileBase tile)
        {
            Sprite sprite = GetTileSprite(tile);
            if (sprite == null)
            {
                GUI.DrawTexture(rect, tileFallbackTexture, ScaleMode.ScaleToFit);
                return;
            }

            DrawSprite(rect, sprite);
        }

        private void DrawPrefabPreview(Rect rect, GameObject prefab)
        {
            Sprite sprite = GetPrefabSprite(prefab);
            if (sprite == null)
            {
                GUI.DrawTexture(rect, tileFallbackTexture, ScaleMode.ScaleToFit);
                return;
            }

            DrawSprite(rect, sprite);
        }

        private void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            Rect textureRect = sprite.textureRect;
            Rect texCoords = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, texCoords, true);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelTexture = MakeTexture(new Color(0.22f, 0.30f, 0.37f, 0.94f));
            headerTexture = MakeTexture(new Color(0.30f, 0.39f, 0.48f, 0.98f));
            buttonTexture = MakeTexture(new Color(0.24f, 0.31f, 0.38f, 0.98f));
            selectedTexture = MakeTexture(new Color(0.55f, 0.66f, 0.78f, 0.98f));
            hoverTexture = MakeTexture(new Color(0.65f, 0.77f, 0.88f, 0.95f));
            inputTexture = MakeTexture(new Color(0.20f, 0.28f, 0.35f, 0.98f));
            inputFocusedTexture = MakeTexture(new Color(0.28f, 0.39f, 0.50f, 0.98f));
            objectSettingsHighlightTexture = MakeTexture(new Color(0.97f, 0.75f, 0.26f, 0.28f));
            lineTexture = MakeTexture(Color.white);
            tileFallbackTexture = MakeCheckerTexture(new Color(0.32f, 0.38f, 0.45f, 1f), new Color(0.20f, 0.24f, 0.29f, 1f));

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.normal.textColor = Color.white;
            panelStyle.border = new RectOffset(4, 4, 4, 4);
            panelStyle.padding = new RectOffset(10, 10, 8, 8);
            panelStyle.alignment = TextAnchor.MiddleCenter;
            panelStyle.fontSize = 22;

            headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.normal.background = headerTexture;
            headerStyle.normal.textColor = Color.white;
            headerStyle.fontSize = 28;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.padding = new RectOffset(14, 10, 0, 0);

            bodyStyle = new GUIStyle(GUI.skin.label);
            bodyStyle.normal.textColor = Color.white;
            bodyStyle.fontSize = 24;
            bodyStyle.wordWrap = true;

            smallBodyStyle = new GUIStyle(GUI.skin.label);
            smallBodyStyle.normal.textColor = Color.white;
            smallBodyStyle.fontSize = 17;
            smallBodyStyle.alignment = TextAnchor.MiddleCenter;
            smallBodyStyle.clipping = TextClipping.Clip;

            mutedStyle = new GUIStyle(bodyStyle);
            mutedStyle.normal.textColor = new Color(0.76f, 0.84f, 0.93f, 1f);
            mutedStyle.fontSize = 19;

            warningStyle = new GUIStyle(bodyStyle);
            warningStyle.normal.textColor = new Color(1f, 0.86f, 0.58f, 1f);
            warningStyle.fontSize = 22;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = hoverTexture;
            buttonStyle.active.background = selectedTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.fontSize = 21;
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.padding = new RectOffset(8, 8, 6, 6);

            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = selectedTexture;

            iconButtonStyle = new GUIStyle(buttonStyle);
            iconButtonStyle.fontSize = 20;

            inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.normal.background = inputTexture;
            inputStyle.focused.background = inputFocusedTexture;
            inputStyle.hover.background = inputFocusedTexture;
            inputStyle.active.background = inputFocusedTexture;
            inputStyle.normal.textColor = Color.white;
            inputStyle.focused.textColor = Color.white;
            inputStyle.hover.textColor = Color.white;
            inputStyle.active.textColor = Color.white;
            inputStyle.fontSize = 21;
            inputStyle.alignment = TextAnchor.MiddleLeft;
            inputStyle.padding = new RectOffset(10, 10, 6, 6);

            objectSettingsHighlightStyle = new GUIStyle(GUI.skin.box);
            objectSettingsHighlightStyle.normal.background = objectSettingsHighlightTexture;
            objectSettingsHighlightStyle.border = new RectOffset(4, 4, 4, 4);
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private Texture2D MakeCheckerTexture(Color a, Color b)
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    texture.SetPixel(x, y, ((x / 4 + y / 4) % 2) == 0 ? a : b);
                }
            }

            texture.Apply();
            return texture;
        }

        private CampusMapRoot CreateMapRoot()
        {
            GameObject rootObject = new GameObject(CampusObjectNames.MapRoot);
            CampusMapRoot root = rootObject.AddComponent<CampusMapRoot>();
            root.SortingOrderStepPerFloor = 1000;
            root.CurrentPreviewFloor = 1;
            rootObject.AddComponent<CampusFloorVisibilityController>().MapRoot = root;
            EnsureChild(rootObject.transform, CampusObjectNames.FloorsRoot);
            EnsureChild(rootObject.transform, CampusObjectNames.EditorDataRoot);
            mapRoot = root;
            EnsureFloor(1);
            return root;
        }

        private CampusFloorRoot EnsureFloor(int floorIndex)
        {
            if (mapRoot == null)
            {
                return null;
            }

            floorIndex = Mathf.Max(1, floorIndex);
            mapRoot.RebuildFloorReferences();
            CampusFloorRoot floor = mapRoot.GetFloor(floorIndex);
            if (floor != null)
            {
                EnsureFloorStructure(floor);
                return floor;
            }

            Transform floorsRoot = EnsureChild(mapRoot.transform, CampusObjectNames.FloorsRoot);
            GameObject floorObject = new GameObject(CampusObjectNames.GetFloorName(floorIndex));
            floorObject.transform.SetParent(floorsRoot, false);
            floor = floorObject.AddComponent<CampusFloorRoot>();
            floor.FloorIndex = floorIndex;
            floor.IsUnlocked = true;

            Transform gridTransform = EnsureChild(floorObject.transform, CampusObjectNames.Grid);
            Grid grid = gridTransform.GetComponent<Grid>();
            if (grid == null)
            {
                grid = gridTransform.gameObject.AddComponent<Grid>();
            }

            grid.cellSize = Vector3.one;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            floor.Grid = grid;
            int sortingBase = floorIndex * mapRoot.SortingOrderStepPerFloor;
            floor.FloorTilemap = CreateTilemap(gridTransform, CampusObjectNames.FloorTilemap, sortingBase + CampusRenderSortingUtility.FloorOffset, true);
            floor.WallLogicTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallLogicTilemap, sortingBase + CampusRenderSortingUtility.WallLogicOffset, false);
            floor.WallTilemap = floor.WallLogicTilemap;
            floor.WallFaceTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallFaceTilemap, sortingBase + CampusRenderSortingUtility.WallFaceOffset, false);
            floor.WallSideTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallSideTilemap, sortingBase + CampusRenderSortingUtility.WallSideOffset, false);
            floor.WallCapTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallCapTilemap, sortingBase + CampusRenderSortingUtility.WallCapOffset, false);
            floor.WallOverlayTilemap = CreateTilemap(gridTransform, CampusObjectNames.WallOverlayTilemap, sortingBase + CampusRenderSortingUtility.WallVisualOverlayOffset, false);
            floor.OverlayTilemap = CreateTilemap(gridTransform, CampusObjectNames.OverlayTilemap, sortingBase + CampusRenderSortingUtility.OverlayOffset, true);
            floor.CollisionDebugTilemap = CreateTilemap(gridTransform, CampusObjectNames.CollisionDebugTilemap, sortingBase + CampusRenderSortingUtility.CollisionDebugOffset, false);
            floor.WallMeshRoot = EnsureChild(gridTransform, CampusObjectNames.WallMeshRoot);
            floor.PropsRoot = EnsureChild(floorObject.transform, CampusObjectNames.PropsRoot);
            floor.StairsRoot = EnsureChild(floorObject.transform, CampusObjectNames.StairsRoot);
            EnsureWallCollision(floor);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);
            mapRoot.RebuildFloorReferences();
            MarkSceneReferencesDirty();
            return floor;
        }

        private void EnsureFloorStructure(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            if (floor.Grid == null)
            {
                floor.Grid = floor.GetComponentInChildren<Grid>(true);
            }

            if (floor.Grid == null)
            {
                Transform gridTransform = EnsureChild(floor.transform, CampusObjectNames.Grid);
                floor.Grid = gridTransform.gameObject.AddComponent<Grid>();
            }

            floor.Grid.cellSize = Vector3.one;
            floor.Grid.cellLayout = GridLayout.CellLayout.Rectangle;
            int sortingBase = floor.FloorIndex * (mapRoot != null ? mapRoot.SortingOrderStepPerFloor : 1000);

            floor.FloorTilemap = floor.FloorTilemap != null ? floor.FloorTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.FloorTilemap, sortingBase + CampusRenderSortingUtility.FloorOffset, true, CampusObjectNames.LegacyFloorTilemap);
            floor.WallLogicTilemap = floor.WallLogicTilemap != null ? floor.WallLogicTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallLogicTilemap, sortingBase + CampusRenderSortingUtility.WallLogicOffset, false, CampusObjectNames.LegacyWallLogicTilemap, CampusObjectNames.LegacyWallsTilemap);
            floor.WallTilemap = floor.WallLogicTilemap;
            CampusDynamicShadowUtility.RemoveFixedWallShadowTilemaps(floor);
            floor.WallFaceTilemap = floor.WallFaceTilemap != null ? floor.WallFaceTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallFaceTilemap, sortingBase + CampusRenderSortingUtility.WallFaceOffset, false, CampusObjectNames.LegacyWallFaceTilemap);
            floor.WallSideTilemap = floor.WallSideTilemap != null ? floor.WallSideTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallSideTilemap, sortingBase + CampusRenderSortingUtility.WallSideOffset, false, CampusObjectNames.LegacyWallSideTilemap);
            floor.WallCapTilemap = floor.WallCapTilemap != null ? floor.WallCapTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallCapTilemap, sortingBase + CampusRenderSortingUtility.WallCapOffset, false, CampusObjectNames.LegacyWallCapTilemap);
            floor.WallOverlayTilemap = floor.WallOverlayTilemap != null ? floor.WallOverlayTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.WallOverlayTilemap, sortingBase + CampusRenderSortingUtility.WallVisualOverlayOffset, false, CampusObjectNames.LegacyWallOverlayTilemap);
            floor.OverlayTilemap = floor.OverlayTilemap != null ? floor.OverlayTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.OverlayTilemap, sortingBase + CampusRenderSortingUtility.OverlayOffset, true, CampusObjectNames.LegacyOverlayTilemap);
            floor.CollisionDebugTilemap = floor.CollisionDebugTilemap != null ? floor.CollisionDebugTilemap : FindOrCreateTilemap(floor.Grid.transform, CampusObjectNames.CollisionDebugTilemap, sortingBase + CampusRenderSortingUtility.CollisionDebugOffset, false, CampusObjectNames.LegacyCollisionDebugTilemap);
            floor.WallMeshRoot = floor.WallMeshRoot != null ? floor.WallMeshRoot : EnsureChild(floor.Grid.transform, CampusObjectNames.WallMeshRoot);
            floor.PropsRoot = floor.PropsRoot != null ? floor.PropsRoot : EnsureChild(floor.transform, CampusObjectNames.PropsRoot);
            floor.StairsRoot = floor.StairsRoot != null ? floor.StairsRoot : EnsureChild(floor.transform, CampusObjectNames.StairsRoot);
            EnsureWallCollision(floor);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);
            CampusWallTileUtility.SetTilemapVisible(floor.CollisionDebugTilemap, false);
            floor.CaptureOriginalRenderState();
        }

        private Tilemap FindOrCreateTilemap(Transform parent, string name, int sortingOrder, bool visible, params string[] legacyNames)
        {
            Tilemap existing = FindTilemapByName(parent, name, legacyNames);
            if (existing != null)
            {
                existing.name = name;
                TilemapRenderer renderer = existing.GetComponent<TilemapRenderer>();
                if (renderer == null)
                {
                    renderer = existing.gameObject.AddComponent<TilemapRenderer>();
                }

                renderer.sortingOrder = sortingOrder;
                renderer.enabled = visible;
                return existing;
            }

            return CreateTilemap(parent, name, sortingOrder, visible);
        }

        private Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, bool visible)
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = visible;
            return tilemap;
        }

        private Tilemap FindTilemapByName(Transform parent, string name, params string[] legacyNames)
        {
            if (parent == null)
            {
                return null;
            }

            Tilemap[] tilemaps = parent.GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null)
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(tilemap.name, name))
                {
                    return tilemap;
                }

                for (int legacyIndex = 0; legacyIndex < legacyNames.Length; legacyIndex++)
                {
                    if (CampusObjectNames.MatchesAny(tilemap.name, legacyNames[legacyIndex]))
                    {
                        return tilemap;
                    }
                }
            }

            return null;
        }

        private Transform EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private void EnsureWallCollision(CampusFloorRoot floor)
        {
            using (CampusWallBuildProfiler.EnsureWallCollision.Auto())
            {
                CampusWallCollisionRenderer.EnsureForFloor(floor);
            }
        }

        private void RebuildWallVisuals(CampusFloorRoot floor)
        {
            using (CampusWallBuildProfiler.RebuildWallVisuals.Auto())
            using (CampusWallBuildProfiler.RebuildWallVisualsFull.Auto())
            {
            if (floor == null)
            {
                return;
            }

            if (wallProfiles.Count > 0)
            {
                selectedWallProfileIndex = Mathf.Clamp(selectedWallProfileIndex, 0, wallProfiles.Count - 1);
                fallbackWallProfile = wallProfiles[selectedWallProfileIndex];
            }

            CampusWallAutoRenderer.RebuildFloor(floor, wallVisualCatalog, fallbackWallProfile);
            CampusWallAutoRenderer.ApplyFinalWallVisualState(floor);
            }
        }

        private void RebuildWallVisuals(CampusFloorRoot floor, IReadOnlyList<Vector3Int> affectedCells)
        {
            using (CampusWallBuildProfiler.RebuildWallVisuals.Auto())
            using (CampusWallBuildProfiler.RebuildWallVisualsChanged.Auto())
            {
            if (floor == null)
            {
                return;
            }

            if (affectedCells == null || affectedCells.Count == 0)
            {
                RebuildWallVisuals(floor);
                return;
            }

            if (wallProfiles.Count > 0)
            {
                selectedWallProfileIndex = Mathf.Clamp(selectedWallProfileIndex, 0, wallProfiles.Count - 1);
                fallbackWallProfile = wallProfiles[selectedWallProfileIndex];
            }

            CampusWallAutoRenderer.RebuildChangedCells(floor, affectedCells, wallVisualCatalog, fallbackWallProfile);
            CampusWallAutoRenderer.ApplyFinalWallVisualState(floor);
            }
        }

        private void QueueWallVisualRebuild(CampusFloorRoot floor, IReadOnlyList<Vector3Int> affectedCells)
        {
            if (floor == null || affectedCells == null || affectedCells.Count == 0)
            {
                return;
            }

            if (pendingWallVisualRebuildFloor != null && pendingWallVisualRebuildFloor != floor)
            {
                FlushPendingWallVisualRebuild();
            }

            pendingWallVisualRebuildFloor = floor;
            for (int i = 0; i < affectedCells.Count; i++)
            {
                Vector3Int cell = affectedCells[i];
                if (pendingWallVisualRebuildCellSet.Add(cell))
                {
                    pendingWallVisualRebuildCells.Add(cell);
                    CampusWallChunkSystem.AddAffectedChunksForCell(pendingWallVisualRebuildChunks, cell);
                }
            }

            bool shouldFlush = !wallStrokeVisualPreviewInitialized ||
                               pendingWallVisualRebuildCells.Count >= WallStrokeVisualBatchCellThreshold ||
                               pendingWallVisualRebuildChunks.Count >= WallStrokeVisualBatchChunkThreshold;
            if (shouldFlush)
            {
                FlushPendingWallVisualRebuild();
                wallStrokeVisualPreviewInitialized = true;
            }
        }

        private void FlushPendingWallVisualRebuild()
        {
            if (pendingWallVisualRebuildFloor == null || pendingWallVisualRebuildCells.Count == 0)
            {
                ClearPendingWallVisualRebuild();
                return;
            }

            RebuildWallVisuals(pendingWallVisualRebuildFloor, pendingWallVisualRebuildCells);
            ClearPendingWallVisualRebuild();
        }

        private void ClearPendingWallVisualRebuild()
        {
            pendingWallVisualRebuildFloor = null;
            pendingWallVisualRebuildCells.Clear();
            pendingWallVisualRebuildCellSet.Clear();
            pendingWallVisualRebuildChunks.Clear();
        }


        private void DeleteSelectedFloor()
        {
            if (mapRoot == null)
            {
                return;
            }

            mapRoot.RebuildFloorReferences();
            if (mapRoot.Floors.Count <= 1)
            {
                SetStatus(Tr("\u81f3\u5c11\u4fdd\u7559\u4e00\u4e2a\u697c\u5c42\u3002", "Keep at least one floor."));
                return;
            }

            CampusFloorRoot floor = mapRoot.GetFloor(selectedFloorIndex);
            if (floor == null)
            {
                return;
            }

            RecordUndo();
            DestroyRuntimeObject(floor.gameObject);
            selectedFloorIndex = 1;
            mapRoot.RebuildFloorReferences();
            if (mapRoot.Floors.Count > 0 && mapRoot.GetFloor(selectedFloorIndex) == null)
            {
                selectedFloorIndex = mapRoot.Floors[0].FloorIndex;
            }

            MarkSceneReferencesDirty();
            RefreshSceneReferencesIfNeeded(true);
            SetStatus(Tr("\u5df2\u5220\u9664\u697c\u5c42\u3002", "Floor deleted."));
        }

        private int[] GetAllSortingLayerIds()
        {
            SortingLayer[] layers = SortingLayer.layers;
            int[] ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                ids[i] = layers[i].id;
            }

            return ids;
        }

        private void AddRoomRequirement(string roomName, int required)
        {
            AddOrUpdateRoomDefinition(roomName, required);
        }

        private void AddOrUpdateRoomDefinition(string roomName, int required)
        {
            if (!TryResolvePresetRoomName(roomName, out string presetRoomName))
            {
                return;
            }

            CampusRuntimeAreaPreset preset = GetAreaPreset(presetRoomName);
            int resolvedRequired = preset != null ? preset.RequiredCount : Mathf.Max(0, required);
            int index = FindRoomDefinitionIndex(presetRoomName);
            if (index >= 0)
            {
                roomRequiredCounts[index] = resolvedRequired;
                return;
            }

            roomNames.Add(presetRoomName);
            roomRequiredCounts.Add(resolvedRequired);
            selectedRoomIndex = roomNames.Count - 1;
        }

        private void DeleteSelectedRoomDefinition()
        {
            if (roomNames.Count == 0)
            {
                return;
            }

            int index = Mathf.Clamp(selectedRoomIndex, 0, roomNames.Count - 1);
            string roomName = roomNames[index];
            RecordUndo();
            roomNames.RemoveAt(index);
            roomRequiredCounts.RemoveAt(index);
            selectedRoomIndex = roomNames.Count > 0 ? Mathf.Clamp(index, 0, roomNames.Count - 1) : 0;

            CampusRuntimeRoomMarker[] markers = FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker != null && marker.RoomName == roomName)
                {
                    DestroyRuntimeObject(marker.gameObject);
                }
            }
        }

        private void ClearRoomDefinitions()
        {
            RecordUndo();
            roomNames.Clear();
            roomRequiredCounts.Clear();
            selectedRoomIndex = 0;
            CampusRuntimeRoomMarker[] markers = FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                if (markers[i] != null)
                {
                    DestroyRuntimeObject(markers[i].gameObject);
                }
            }
        }

        private int CountRoomMarkers(string roomName)
        {
            if (!TryResolvePresetRoomName(roomName, out string targetRoomName))
            {
                return 0;
            }

            CampusRuntimeRoomMarker[] markers = FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null &&
                    TryResolvePresetRoomName(markers[i].RoomName, out string markerRoomName) &&
                    string.Equals(markerRoomName, targetRoomName, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private string GetSelectedRoomName()
        {
            EnsureRoomRequirements();
            if (roomNames.Count == 0)
            {
                return string.Empty;
            }

            selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, roomNames.Count - 1);
            return roomNames[selectedRoomIndex];
        }

        private CampusRuntimeRoomPrefab GetSelectedRoomPrefab()
        {
            if (roomPrefabs.Count == 0)
            {
                return null;
            }

            selectedRoomPrefabIndex = Mathf.Clamp(selectedRoomPrefabIndex, 0, roomPrefabs.Count - 1);
            return roomPrefabs[selectedRoomPrefabIndex];
        }

        private CampusRuntimeGameplayMarkerPreset GetSelectedGameplayPreset()
        {
            if (GameplayMarkerPresets.Length == 0)
            {
                return null;
            }

            selectedGameplayPresetIndex = Mathf.Clamp(selectedGameplayPresetIndex, 0, GameplayMarkerPresets.Length - 1);
            return GameplayMarkerPresets[selectedGameplayPresetIndex];
        }

        private string GetGameplayPresetLabel(CampusRuntimeGameplayMarkerPreset preset)
        {
            if (preset == null)
            {
                return Tr("\u533a\u57df\u70b9\u4f4d", "Area Point");
            }

            return Tr(preset.ChineseLabel, preset.EnglishLabel);
        }

        private string GetGameplayPresetDisplayName(CampusRuntimeGameplayMarkerPreset preset)
        {
            if (preset == null)
            {
                return Tr("\u533a\u57df\u70b9\u4f4d", "Area Point");
            }

            return Tr(preset.ChineseDisplayName, preset.EnglishDisplayName);
        }

        private bool TryResolveGameplayMarkerCell(Component component, out int floorIndex, out Vector3Int cell)
        {
            floorIndex = 1;
            cell = Vector3Int.zero;
            if (component == null)
            {
                return false;
            }

            CampusRuntimeGameplayOverlayEntity entity =
                component.GetComponent<CampusRuntimeGameplayOverlayEntity>();
            if (entity != null)
            {
                floorIndex = entity.FloorIndex;
                cell = NormalizeCell(entity.Cell);
                return true;
            }

            CampusGameplayFacilityMarker facility = component.GetComponent<CampusGameplayFacilityMarker>();
            if (facility != null)
            {
                floorIndex = Mathf.Max(1, facility.FloorIndex);
                cell = NormalizeCell(facility.Cell);
                return true;
            }

            CampusGameplayRoomMarker room = component.GetComponent<CampusGameplayRoomMarker>();
            if (room != null)
            {
                floorIndex = Mathf.Max(1, room.FloorIndex);
                cell = NormalizeCell(room.AnchorCell);
                return true;
            }

            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            if (mapRoot == null || mapRoot.Floors == null)
            {
                return false;
            }

            Vector3 position = component.transform.position;
            float bestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = mapRoot.Floors[i];
                if (floor == null || floor.Grid == null)
                {
                    continue;
                }

                Vector3Int candidate = NormalizeCell(floor.Grid.WorldToCell(position));
                Vector3 center = floor.Grid.GetCellCenterWorld(candidate);
                float distance = Vector2.Distance(center, position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    floorIndex = floor.FloorIndex;
                    cell = candidate;
                    found = true;
                }
            }

            return found;
        }

        private string ResolveNewRoomPrefabName()
        {
            if (!string.IsNullOrWhiteSpace(newRoomPrefabName))
            {
                return newRoomPrefabName.Trim();
            }

            return GetSelectedRoomName();
        }

        private static Vector2Int NormalizeRoomPrefabSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        private static BoundsInt BuildInclusiveCellBounds(Vector3Int start, Vector3Int end)
        {
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            return new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        }

        private static bool CellInBounds(BoundsInt bounds, Vector3Int cell)
        {
            return cell.x >= bounds.xMin &&
                   cell.x < bounds.xMax &&
                   cell.y >= bounds.yMin &&
                   cell.y < bounds.yMax;
        }

        private static bool BoundsOverlap2D(BoundsInt a, BoundsInt b)
        {
            return a.xMin < b.xMax &&
                   a.xMax > b.xMin &&
                   a.yMin < b.yMax &&
                   a.yMax > b.yMin;
        }

        private static bool BoundsContains2D(BoundsInt container, BoundsInt contained)
        {
            return contained.xMin >= container.xMin &&
                   contained.xMax <= container.xMax &&
                   contained.yMin >= container.yMin &&
                   contained.yMax <= container.yMax;
        }

        private static Vector3Int NormalizeCell(Vector3Int cell)
        {
            return new Vector3Int(cell.x, cell.y, 0);
        }

        private static Vector3Int ToRelativeCell(Vector3Int cell, Vector3Int originCell)
        {
            return new Vector3Int(cell.x - originCell.x, cell.y - originCell.y, 0);
        }

        private static Vector3Int ToAbsoluteCell(Vector3Int anchorCell, Vector3Int relativeCell)
        {
            return new Vector3Int(anchorCell.x + relativeCell.x, anchorCell.y + relativeCell.y, 0);
        }

        private static bool IsPlacedObjectFullyInsideBounds(CampusPlacedObject placed, BoundsInt bounds)
        {
            if (placed == null)
            {
                return false;
            }

            Vector2Int footprint = placed.RotatedFootprintSize;
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    Vector3Int cell = new Vector3Int(placed.Cell.x + x, placed.Cell.y + y, 0);
                    if (!CellInBounds(bounds, cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool HasRoomPrefabContent(CampusRuntimeRoomPrefab roomPrefab)
        {
            return roomPrefab != null &&
                   ((roomPrefab.FloorTiles != null && roomPrefab.FloorTiles.Count > 0) ||
                    (roomPrefab.WallTiles != null && roomPrefab.WallTiles.Count > 0) ||
                    (roomPrefab.Objects != null && roomPrefab.Objects.Count > 0) ||
                    (roomPrefab.RoomMarkers != null && roomPrefab.RoomMarkers.Count > 0) ||
                    (roomPrefab.GameplayRooms != null && roomPrefab.GameplayRooms.Count > 0) ||
                    (roomPrefab.GameplayFacilities != null && roomPrefab.GameplayFacilities.Count > 0) ||
                    (roomPrefab.GameplayPrankSpots != null && roomPrefab.GameplayPrankSpots.Count > 0) ||
                    (roomPrefab.Lights != null && roomPrefab.Lights.Count > 0));
        }

        private static void NormalizeRoomPrefab(CampusRuntimeRoomPrefab roomPrefab, string fallbackName)
        {
            if (roomPrefab == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(roomPrefab.Schema))
            {
                roomPrefab.Schema = "NtingCampusRuntimeRoomPrefab.v1";
            }

            roomPrefab.RoomName = string.IsNullOrWhiteSpace(roomPrefab.RoomName)
                ? (string.IsNullOrWhiteSpace(fallbackName) ? "Unnamed Room" : fallbackName.Trim())
                : roomPrefab.RoomName.Trim();
            roomPrefab.Size = NormalizeRoomPrefabSize(roomPrefab.Size);
            roomPrefab.FloorTiles = roomPrefab.FloorTiles ?? new List<CampusRuntimeTileSnapshot>();
            roomPrefab.WallTiles = roomPrefab.WallTiles ?? new List<CampusRuntimeTileSnapshot>();
            roomPrefab.Objects = roomPrefab.Objects ?? new List<CampusRuntimeObjectSnapshot>();
            roomPrefab.RoomMarkers = roomPrefab.RoomMarkers ?? new List<CampusRuntimeRoomSnapshot>();
            roomPrefab.GameplayRooms = roomPrefab.GameplayRooms ?? new List<CampusRuntimeGameplayRoomSnapshot>();
            roomPrefab.GameplayFacilities = roomPrefab.GameplayFacilities ?? new List<CampusRuntimeGameplayFacilitySnapshot>();
            roomPrefab.GameplayPrankSpots = roomPrefab.GameplayPrankSpots ?? new List<CampusRuntimeGameplayPrankSpotSnapshot>();
            roomPrefab.Lights = roomPrefab.Lights ?? new List<CampusRuntimeRoomLightSnapshot>();
        }

        private TileBase GetSelectedFloorTile()
        {
            return floorTiles.Count == 0 ? null : floorTiles[Mathf.Clamp(selectedFloorTileIndex, 0, floorTiles.Count - 1)];
        }

        private TileBase GetSelectedWallTile()
        {
            if (wallTiles.Count > 0)
            {
                return wallTiles[Mathf.Clamp(selectedWallTileIndex, 0, wallTiles.Count - 1)];
            }

            return fallbackWallProfile != null ? fallbackWallProfile.GetLogicTile() : null;
        }

        private GameObject GetSelectedObjectPrefab()
        {
            return objectPrefabs.Count == 0 ? null : objectPrefabs[Mathf.Clamp(selectedObjectIndex, 0, objectPrefabs.Count - 1)];
        }

        private string GetObjectDisplayName(GameObject prefab)
        {
            if (prefab == null)
            {
                return Tr("\u672a\u9009\u7269\u54c1", "No Object Selected");
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            if (placed != null && !string.IsNullOrWhiteSpace(placed.DisplayNameOverride))
            {
                return placed.DisplayNameOverride.Trim();
            }

            return GetObjectFallbackDisplayName(prefab);
        }

        private string GetObjectDisplayName(string objectId)
        {
            int index = FindPrefabIndexByName(objectId);
            return index >= 0 ? GetObjectDisplayName(objectPrefabs[index]) : CampusObjectNames.GetDisplayName(objectId);
        }

        private string GetObjectFallbackDisplayName(GameObject prefab)
        {
            if (prefab == null)
            {
                return Tr("\u672a\u9009\u7269\u54c1", "No Object Selected");
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            string objectId = placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId) ? placed.ObjectId : prefab.name;
            return CampusObjectNames.GetDisplayName(objectId);
        }

        private Vector2Int GetSelectedObjectFootprint()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            Vector2Int footprint = placed != null ? placed.NormalizedFootprintSize : Vector2Int.one;
            int effectiveRotation90 = placed != null ? placed.ResolveAllowedRotation90(rotation90) : 0;
            return CampusPlacedObject.RotateFootprintSize(footprint, effectiveRotation90);
        }

        private Matrix4x4 BuildTileTransform()
        {
            return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, rotation90 * 90f), Vector3.one);
        }

        private TileBase ResolveTile(CampusRuntimeTileSnapshot tileSnapshot, List<TileBase> palette)
        {
            if (tileSnapshot.PaletteIndex >= 0 && tileSnapshot.PaletteIndex < palette.Count && palette[tileSnapshot.PaletteIndex] != null)
            {
                return palette[tileSnapshot.PaletteIndex];
            }

            for (int i = 0; i < palette.Count; i++)
            {
                if (palette[i] != null && palette[i].name == tileSnapshot.AssetName)
                {
                    return palette[i];
                }
            }

            return null;
        }

        private GameObject ResolvePrefab(CampusRuntimeObjectSnapshot objectSnapshot)
        {
            if (objectSnapshot.PaletteIndex >= 0 && objectSnapshot.PaletteIndex < objectPrefabs.Count && objectPrefabs[objectSnapshot.PaletteIndex] != null)
            {
                return objectPrefabs[objectSnapshot.PaletteIndex];
            }

            int index = FindPrefabIndexByName(objectSnapshot.ObjectId);
            return index >= 0 ? objectPrefabs[index] : null;
        }

        private int FindPrefabIndexByName(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return -1;
            }

            for (int i = 0; i < objectPrefabs.Count; i++)
            {
                GameObject prefab = objectPrefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                if (prefab.name == objectId || CampusObjectNames.GetDisplayName(prefab.name) == CampusObjectNames.GetDisplayName(objectId))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ResolveLightCell(Light2D light, out int floorIndex, out Vector3Int cell)
        {
            floorIndex = 0;
            cell = Vector3Int.zero;
            if (mapRoot == null || light == null)
            {
                return;
            }

            mapRoot.RebuildFloorReferences();
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < mapRoot.Floors.Count; i++)
            {
                CampusFloorRoot floor = mapRoot.Floors[i];
                if (floor == null || floor.Grid == null)
                {
                    continue;
                }

                Vector3Int candidateCell = floor.Grid.WorldToCell(light.transform.position);
                Vector3 center = floor.Grid.GetCellCenterWorld(candidateCell);
                float distance = Vector2.Distance(center, light.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    floorIndex = floor.FloorIndex;
                    cell = candidateCell;
                    cell.z = 0;
                }
            }
        }

        private bool HasUsableMatrix(Matrix4x4 matrix)
        {
            return !Mathf.Approximately(matrix.m33, 0f);
        }

        private Vector3 GetStairWorldCenter(Grid grid, Vector3Int primaryCell, Vector3Int secondaryCell)
        {
            if (grid == null)
            {
                return Vector3.zero;
            }

            return (grid.GetCellCenterWorld(primaryCell) + grid.GetCellCenterWorld(secondaryCell)) * 0.5f;
        }

        private void EnsureTriggerCollider(GameObject target, Vector2 size)
        {
            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null)
            {
                collider = target.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            BoxCollider2D box = collider as BoxCollider2D;
            if (box != null)
            {
                box.size = size;
            }
        }

        private void AddRoomMarkerVisual(GameObject markerObject, CampusFloorRoot floor)
        {
            if (markerObject == null)
            {
                return;
            }

            SpriteRenderer renderer = markerObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private void DestroyChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                DestroyRuntimeObject(root.GetChild(i).gameObject);
            }
        }

        private void DestroyFloorAuthoredProps(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                CampusRuntimeGameplayOverlayEntity gameplayEntity =
                    child.GetComponent<CampusRuntimeGameplayOverlayEntity>();
                if (gameplayEntity != null && !gameplayEntity.IsActorEntity)
                {
                    continue;
                }

                DestroyRuntimeObject(child);
            }
        }

        private void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private bool IsPointerOverEditorUi(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return IsGuiPositionOverEditorUi(guiPosition);
        }

        private bool IsGuiPositionOverEditorUi(Vector2 guiPosition)
        {
            return leftPanelRect.Contains(guiPosition) ||
                   floorPanelRect.Contains(guiPosition) ||
                   checklistPanelRect.Contains(guiPosition) ||
                   bottomToolbarRect.Contains(guiPosition) ||
                   (showSettings && settingsPanelRect.Contains(guiPosition)) ||
                   (showObjectSettings && objectSettingsPanelRect.Contains(guiPosition)) ||
                   (showHelpOverlay && helpPanelRect.Contains(guiPosition));
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            if (sceneCamera == null)
            {
                return Vector3.zero;
            }

            Vector3 world = sceneCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, GetCameraPlaneDistance()));
            world.z = 0f;
            return world;
        }

        private float GetCameraPlaneDistance()
        {
            if (sceneCamera == null)
            {
                return 0f;
            }

            return sceneCamera.orthographic ? Mathf.Abs(sceneCamera.transform.position.z) : Mathf.Max(sceneCamera.nearClipPlane, Mathf.Abs(sceneCamera.transform.position.z));
        }

        private Vector2 WorldToGuiPoint(Vector3 worldPosition)
        {
            Vector3 screen = sceneCamera.WorldToScreenPoint(worldPosition);
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        private int ParseIntField(Rect rect, int value)
        {
            return ParseIntField(rect, value, null);
        }

        private int ParseIntField(Rect rect, int value, string key)
        {
            string text = DrawTextInput(rect, value.ToString(CultureInfo.InvariantCulture), key ?? BuildRectTextInputKey("int", rect));
            int parsed;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : value;
        }

        private float ParseFloatField(Rect rect, float value)
        {
            return ParseFloatField(rect, value, null);
        }

        private float ParseFloatField(Rect rect, float value, string key)
        {
            string text = DrawTextInput(rect, FormatFloat(value), key ?? BuildRectTextInputKey("float", rect));
            float parsed;
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : value;
        }

        private string DrawTextInput(Rect rect, string value, string key)
        {
            string safeKey = string.IsNullOrEmpty(key) ? BuildRectTextInputKey("text", rect) : key;
            string controlName = TextInputControlPrefix + safeKey;
            bool focusedBefore = GUI.GetNameOfFocusedControl() == controlName;
            string draft;
            if (!focusedBefore || !textInputDrafts.TryGetValue(safeKey, out draft))
            {
                draft = value ?? string.Empty;
            }

            GUI.SetNextControlName(controlName);
            string next = GUI.TextField(rect, draft, inputStyle);
            bool focusedAfter = GUI.GetNameOfFocusedControl() == controlName;
            if (focusedAfter)
            {
                textInputDrafts[safeKey] = next;
            }
            else
            {
                textInputDrafts.Remove(safeKey);
            }

            return next;
        }

        private void RefreshTextInputFocusState()
        {
            string focusedControl = GUI.GetNameOfFocusedControl();
            textInputFocused = !string.IsNullOrEmpty(focusedControl) &&
                               focusedControl.StartsWith(TextInputControlPrefix, StringComparison.Ordinal);
        }

        private bool IsEditingTextInput()
        {
            return textInputFocused;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string BuildRectTextInputKey(string prefix, Rect rect)
        {
            return prefix + "_" +
                   Mathf.RoundToInt(rect.x) + "_" +
                   Mathf.RoundToInt(rect.y) + "_" +
                   Mathf.RoundToInt(rect.width) + "_" +
                   Mathf.RoundToInt(rect.height);
        }

        private Sprite GetTileSprite(TileBase tile)
        {
            Tile spriteTile = tile as Tile;
            return spriteTile != null ? spriteTile.sprite : null;
        }

        private Sprite GetPrefabSprite(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            if (placed != null)
            {
                Sprite configuredSprite = placed.ResolveSpriteForRotation(0, out _, out _);
                if (configuredSprite != null)
                {
                    return configuredSprite;
                }
            }

            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private Sprite ResolvePrefabPreviewSprite(GameObject prefab, CampusPlacedObject placed, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90)
        {
            usesAuthoredDirectionalSprite = false;
            effectiveRotation90 = 0;
            if (prefab == null)
            {
                return null;
            }

            if (placed != null)
            {
                return placed.ResolveSpriteForRotation(rotation90, out usesAuthoredDirectionalSprite, out effectiveRotation90);
            }

            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private string GetDisplayName(TileBase tile)
        {
            return tile == null ? Tr("\u7a7a", "Empty") : CampusObjectNames.GetDisplayName(tile.name);
        }

        private string Truncate(string value, int maxCharacters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            {
                return value;
            }

            return value.Substring(0, maxCharacters);
        }

        private void DrawSettingsPanel()
        {
            if (!showSettings)
            {
                return;
            }

            Rect rect = settingsPanelRect;
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Settings), headerStyle);
            GUI.Label(new Rect(rect.x + 16f, rect.y + 44f, rect.width - 32f, 44f), CampusRuntimeEditorTextCatalog.FormatMapSource(displayLanguage, DescribeMapLoadSource()), bodyStyle);

            autoSavePlayerMap = GUI.Toggle(new Rect(rect.x + 16f, rect.y + 92f, rect.width - 32f, 24f), autoSavePlayerMap, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AutosavePlayerMap));
            autoLoadPlayerMapOnStart = GUI.Toggle(new Rect(rect.x + 16f, rect.y + 120f, rect.width - 32f, 24f), autoLoadPlayerMapOnStart, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AutoloadPlayerMapOnStart));

            float y = rect.y + 156f;
            float width = rect.width - 32f;
            float buttonWidth = (width - 8f) * 0.5f;
            if (GUI.Button(new Rect(rect.x + 16f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SavePlayerMap), buttonStyle))
            {
                SavePlayerMap();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.LoadPlayerMap), buttonStyle))
            {
                LoadPlayerMap();
            }

            y += 38f;
            if (GUI.Button(new Rect(rect.x + 16f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ExportAuthoring), buttonStyle))
            {
                ExportRuntimeAuthoringPackage();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RestoreAuthoring), buttonStyle))
            {
                RestoreRuntimeAuthoringPackage();
            }
        }

        private void ImportDroppedPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            string targetFolder = GetImportFolderForTarget(activeImportTarget);
            Directory.CreateDirectory(targetFolder);
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (Directory.Exists(path))
                {
                    MirrorDirectory(path, Path.Combine(targetFolder, Path.GetFileName(path)), false);
                    continue;
                }

                if (!File.Exists(path))
                {
                    continue;
                }

                if ((activeImportTarget == CampusRuntimeImportTarget.Floor ||
                     activeImportTarget == CampusRuntimeImportTarget.Wall ||
                     activeImportTarget == CampusRuntimeImportTarget.Object ||
                     activeImportTarget == CampusRuntimeImportTarget.WallFace ||
                     activeImportTarget == CampusRuntimeImportTarget.WallCap) &&
                    !IsSupportedImportImage(path))
                {
                    continue;
                }

                string destination = MakeUniqueImportPath(Path.Combine(targetFolder, Path.GetFileName(path)));
                File.Copy(path, destination, false);
            }

            LoadRuntimeResources();
            RefreshImportAssetDatabaseIfProjectBacked();
        }

        private string MakeUniqueImportPath(string path)
        {
            string folder = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string candidate = path;
            int suffix = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder ?? string.Empty, fileName + "_" + suffix + extension);
                suffix++;
            }

            return candidate;
        }

        private int ImportRoomDefinitionsFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            int count = 0;
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split(',');
                string roomName = parts[0].Trim();
                if (string.IsNullOrEmpty(roomName))
                {
                    continue;
                }

                int required = 1;
                if (parts.Length > 1)
                {
                    int.TryParse(parts[1].Trim(), out required);
                    required = Mathf.Max(1, required);
                }

                AddOrUpdateRoomDefinition(roomName, required);
                count++;
            }

            return count;
        }

        private void SaveRuntimeRoomPrefab(CampusRuntimeRoomPrefab roomPrefab)
        {
            if (roomPrefab == null)
            {
                return;
            }

            NormalizeRoomPrefab(roomPrefab, roomPrefab.RoomName);
            Directory.CreateDirectory(GetRoomPrefabFolder());
            string filePath = Path.Combine(GetRoomPrefabFolder(), SanitizeFileName(roomPrefab.RoomName) + ".json");
            File.WriteAllText(filePath, JsonUtility.ToJson(roomPrefab, true), Encoding.UTF8);
            roomPrefab.SourcePath = filePath;
            RefreshImportAssetDatabaseIfProjectBacked();
        }

        private void SelectRoomPrefabByName(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return;
            }

            for (int i = 0; i < roomPrefabs.Count; i++)
            {
                CampusRuntimeRoomPrefab roomPrefab = roomPrefabs[i];
                if (roomPrefab != null && string.Equals(roomPrefab.RoomName, roomName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedRoomPrefabIndex = i;
                    return;
                }
            }
        }

        private static bool AreSamePath(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            {
                return false;
            }

            string left = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string right = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private void MirrorDirectory(string source, string destination, bool deleteExtra)
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string target = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, target, true);
            }

            string[] directories = Directory.GetDirectories(source);
            for (int i = 0; i < directories.Length; i++)
            {
                string directory = directories[i];
                string target = Path.Combine(destination, Path.GetFileName(directory));
                MirrorDirectory(directory, target, deleteExtra);
            }

            if (!deleteExtra)
            {
                return;
            }

            foreach (string destinationFile in Directory.GetFiles(destination))
            {
                string sourceFile = Path.Combine(source, Path.GetFileName(destinationFile));
                if (!File.Exists(sourceFile))
                {
                    File.Delete(destinationFile);
                }
            }

            foreach (string destinationDirectory in Directory.GetDirectories(destination))
            {
                string sourceDirectory = Path.Combine(source, Path.GetFileName(destinationDirectory));
                if (!Directory.Exists(sourceDirectory))
                {
                    Directory.Delete(destinationDirectory, true);
                }
            }
        }

        private void RefreshAssetDatabaseIfAvailable()
        {
#if UNITY_EDITOR
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
#endif
        }

        private string BackupLocalRuntimeImportFolder()
        {
            string source = GetImportRootFolder();
            if (!Directory.Exists(source))
            {
                return string.Empty;
            }

            string backup = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            "_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            MirrorDirectory(source, backup, false);
            return backup;
        }

        private void DeleteImportedTileResource(string folder, List<TileBase> tiles, int index, string resourceLabel)
        {
            if (tiles == null || index < 0 || index >= tiles.Count)
            {
                return;
            }

            TileBase tile = tiles[index];
            string assetName = tile != null ? tile.name : string.Empty;
            string path = FindImportedImagePathByName(folder, assetName);
            if (string.IsNullOrEmpty(path))
            {
                SetStatus(TrFormat("\u53ea\u80fd\u5220\u9664\u5bfc\u5165\u7684 {0} \u8d44\u6e90\u3002", "Only imported {0} resources can be deleted.", resourceLabel));
                return;
            }

            try
            {
                File.Delete(path);
                LoadRuntimeResources();
                SchedulePlayerMapSave();
                SetStatus(TrFormat("\u5df2\u5220\u9664 {0} \u8d44\u6e90\uff1a{1}", "Deleted {0} resource: {1}", resourceLabel, assetName));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to delete " + resourceLabel + " resource '" + path + "': " + exception.Message);
                SetStatus(TrFormat("\u5220\u9664\u5931\u8d25\uff1a{0}", "Delete failed: {0}", exception.Message));
            }

            RefreshImportAssetDatabaseIfProjectBacked();
        }

        private void DeleteImportedObjectResource(int index)
        {
            if (index < 0 || index >= objectPrefabs.Count)
            {
                return;
            }

            GameObject prefab = objectPrefabs[index];
            string objectId = prefab != null ? prefab.name : string.Empty;
            string path = FindImportedImagePathByName(GetObjectImportFolder(), objectId);
            if (string.IsNullOrEmpty(path))
            {
                SetStatus(Tr("\u53ea\u80fd\u5220\u9664\u5bfc\u5165\u7684\u7269\u4ef6\u8d44\u6e90\u3002", "Only imported object resources can be deleted."));
                return;
            }

            try
            {
                File.Delete(path);
                string settingsFolder = GetObjectSettingsFolder(objectId);
                if (Directory.Exists(settingsFolder))
                {
                    Directory.Delete(settingsFolder, true);
                }

                if (lastObjectSettingsPrefab == prefab)
                {
                    showObjectSettings = false;
                    lastObjectSettingsPrefab = null;
                }

                LoadRuntimeResources();
                SchedulePlayerMapSave();
                SetStatus(TrFormat("\u5df2\u5220\u9664\u7269\u4ef6\u8d44\u6e90\uff1a{0}", "Deleted object resource: {0}", objectId));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to delete object resource '" + path + "': " + exception.Message);
                SetStatus(TrFormat("\u5220\u9664\u5931\u8d25\uff1a{0}", "Delete failed: {0}", exception.Message));
            }

            RefreshImportAssetDatabaseIfProjectBacked();
        }

        private string FindImportedImagePathByName(string folder, string assetName)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(assetName))
            {
                return string.Empty;
            }

            string[] files = GetImportImageFiles(folder);
            for (int i = 0; i < files.Length; i++)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(files[i]), assetName, StringComparison.OrdinalIgnoreCase))
                {
                    return files[i];
                }
            }

            return string.Empty;
        }

        private void DeleteSelectedRoomPrefab()
        {
            CampusRuntimeRoomPrefab roomPrefab = GetSelectedRoomPrefab();
            if (roomPrefab == null)
            {
                return;
            }

            string path = string.IsNullOrWhiteSpace(roomPrefab.SourcePath)
                ? Path.Combine(GetRoomPrefabFolder(), SanitizeFileName(roomPrefab.RoomName) + ".json")
                : roomPrefab.SourcePath;
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            roomPrefabs.Remove(roomPrefab);
            selectedRoomPrefabIndex = roomPrefabs.Count > 0 ? Mathf.Clamp(selectedRoomPrefabIndex, 0, roomPrefabs.Count - 1) : 0;
            RefreshImportAssetDatabaseIfProjectBacked();
            SetStatus(TrFormat("\u5df2\u5220\u9664\u623f\u95f4\u6a21\u5757\uff1a{0}", "Deleted room module: {0}", roomPrefab.RoomName));
        }

        private string SelectSingleImageFile(string title)
        {
            return SelectSingleFile(title, Tr("\u56fe\u7247", "Image") + "|*.png;*.jpg;*.jpeg;*.bmp|" + Tr("\u5168\u90e8", "All") + "|*.*");
        }

        private void CommitObjectSettingsDraft(GameObject prefab, CampusPlacedObject placed)
        {
            if (prefab == null || placed == null)
            {
                return;
            }

            placed.NormalizeStorageSettings();
            placed.NormalizeCustomInteractionAnchors();
            placed.TypeId = string.IsNullOrWhiteSpace(placed.TypeId) ? string.Empty : placed.TypeId.Trim();
            placed.ApplyVisualScaleState();
            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
        }

        private void DrawObjectSettingsRenameControls(ref float y, float width, GameObject prefab, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, 96f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DisplayName), bodyStyle);
            string key = BuildObjectSettingsInputKey(placed, "display_name");
            string current = string.IsNullOrEmpty(placed.DisplayNameOverride) ? string.Empty : placed.DisplayNameOverride;
            string next = DrawTextInput(new Rect(102f, y, width - 102f, 30f), current, key);
            placed.DisplayNameOverride = string.IsNullOrWhiteSpace(next) ? string.Empty : next.Trim();
            y += 40f;
        }

        private void DrawObjectSettingsTypeIdControls(ref float y, float width, CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            GUI.Label(new Rect(0f, y, 96f, 28f), Tr("\u7c7b\u578bID", "Type ID"), bodyStyle);
            string key = BuildObjectSettingsInputKey(placed, "type_id");
            string current = string.IsNullOrEmpty(placed.TypeId) ? string.Empty : placed.TypeId;
            string next = DrawTextInput(new Rect(102f, y, Mathf.Max(40f, width - 168f), 30f), current, key);
            placed.TypeId = string.IsNullOrWhiteSpace(next) ? string.Empty : next.Trim();
            if (GUI.Button(new Rect(width - 58f, y, 58f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Clear), buttonStyle))
            {
                placed.TypeId = string.Empty;
                textInputDrafts[key] = string.Empty;
            }

            y += 34f;
            GUI.Label(
                new Rect(0f, y, width, 38f),
                Tr("\u7528\u4e8e\u7269\u54c1/\u8bbe\u65bd\u5224\u5b9a\u7684\u7a33\u5b9aID\u3002\u4f8b\uff1aStudentDesk\u3001OfficeDesk\u3001CanteenCounter\u3001StoreShelf\u3001Storage\u3002", "Stable ID for object/facility checks. Examples: StudentDesk, OfficeDesk, CanteenCounter, StoreShelf, Storage."),
                mutedStyle);
            y += 46f;
        }

        private void DrawObjectSettingsWallMountControls(ref float y, float width, CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            bool nextWallMounted = GUI.Toggle(new Rect(0f, y, width, 24f), placed.IsWallMounted, Tr("\u58c1\u6302\u7269\u4f53", "Wall Mounted Object"));
            if (nextWallMounted != placed.IsWallMounted)
            {
                ConfigureWallMountedSettings(placed, nextWallMounted, true);
            }

            y += 30f;
            if (!placed.IsWallMounted)
            {
                return;
            }

            string spriteName = placed.Rotation0Sprite != null
                ? Truncate(placed.Rotation0Sprite.name, 22)
                : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NotSet);
            GUI.Label(new Rect(0f, y, 84f, 28f), Tr("\u4e3b\u8d34\u56fe", "Main Sprite"), bodyStyle);
            GUI.Box(new Rect(88f, y, Mathf.Max(10f, width - 216f), 30f), spriteName, buttonStyle);
            if (GUI.Button(new Rect(width - 120f, y, 56f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PickSprite), buttonStyle))
            {
                SetSelectedWallMountedSprite();
            }

            if (GUI.Button(new Rect(width - 58f, y, 56f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Clear), buttonStyle))
            {
                ClearSelectedWallMountedSprite();
            }

            y += 34f;
            GUI.Label(new Rect(0f, y, width, 42f), Tr("\u58c1\u6302\u6a21\u5f0f\u53ea\u4f7f\u7528\u8fd9\u4e00\u5f20\u8d34\u56fe\uff0c\u573a\u666f\u4e2d\u4f1a\u81ea\u52a8\u751f\u6210 3D \u8584\u7247\u5e76\u5438\u9644\u5230\u5899\u4f53\u3002", "Wall-mounted mode only uses this sprite. The scene generates a thin 3D plate and snaps it to the wall."), mutedStyle);
            y += 48f;
        }

        private void DrawObjectSettingsPreviewControls(ref float y, float width, GameObject prefab, CampusPlacedObject placed)
        {
            bool usesDirectionalSprite;
            int effectiveRotation;
            Sprite sprite = ResolveObjectSettingsPreviewSprite(prefab, placed, out usesDirectionalSprite, out effectiveRotation);
            string spriteName = sprite != null ? sprite.name : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NoSprite);
            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PreviewSprite), headerStyle);
            y += 28f;
            Vector2Int footprint = CampusPlacedObject.RotateFootprintSize(placed.NormalizedFootprintSize, objectSettingsPreviewRotation90);
            float previewSize = Mathf.Clamp(width * 0.52f, 156f, 232f);
            Rect previewRect = new Rect(0f, y, previewSize, previewSize);
            GUI.Box(previewRect, GUIContent.none, buttonStyle);
            Rect gridRect = DrawObjectSettingsPreviewGrid(previewRect, footprint, sprite, placed, usesDirectionalSprite, effectiveRotation);
            HandleObjectSettingsPreviewAnchorInput(previewRect, gridRect, footprint, placed);

            float textX = previewRect.xMax + 12f;
            float textWidth = Mathf.Max(10f, width - textX);
            GUI.Label(new Rect(textX, y + 4f, textWidth, 24f), Truncate(spriteName, 32), mutedStyle);
            GUI.Label(new Rect(textX, y + 30f, textWidth, 24f), Tr(CampusRuntimeEditorTextId.PreviewGrid) + ": " + footprint.x + "x" + footprint.y, mutedStyle);
            string previewMode = placed != null && placed.IsWallMounted
                ? Tr("\u58c1\u6302\u5438\u9644", "Wall Snap")
                : usesDirectionalSprite
                ? Tr(CampusRuntimeEditorTextId.DirectionalSprite)
                : Tr(CampusRuntimeEditorTextId.DefaultSprite);
            GUI.Label(new Rect(textX, y + 56f, textWidth, 24f), previewMode + " / " + (effectiveRotation * 90) + "\u00b0", mutedStyle);
            GUI.Label(new Rect(textX, y + 82f, textWidth, 44f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ClickPreviewToPlaceAnchor), mutedStyle);
            y += previewSize + 14f;
        }

        private void DrawObjectSettingsFootprintControls(ref float y, float width, CampusPlacedObject placed)
        {
            if (placed != null && placed.IsWallMounted)
            {
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = Vector2Int.one;
                GUI.Label(new Rect(0f, y, width, 24f), Tr("\u58c1\u6302\u7269\u4f53\u56fa\u5b9a\u4f7f\u7528 1x1 \u5899\u4f53\u951a\u70b9\u3002", "Wall-mounted objects always use a 1x1 wall anchor."), mutedStyle);
                y += 34f;
                return;
            }

            DrawSelectedObjectFootprintControls(ref y, width);
        }

        private void DrawObjectSettingsStorageControls(ref float y, float width, CampusPlacedObject placed)
        {
            if (placed != null && placed.IsWallMounted)
            {
                placed.BlocksMovement = false;
                placed.BlocksSight = false;
            }

            placed.IsStorageContainer = GUI.Toggle(new Rect(0f, y, width, 24f), placed.IsStorageContainer, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.StorageContainer));
            y += 30f;
            GUI.enabled = placed.IsStorageContainer;
            GUI.Label(new Rect(0f, y, 56f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Size), bodyStyle);
            placed.StorageSize = new Vector2Int(
                Mathf.Clamp(ParseIntField(new Rect(60f, y, 48f, 30f), placed.NormalizedStorageSize.x), 1, 64),
                Mathf.Clamp(ParseIntField(new Rect(136f, y, 48f, 30f), placed.NormalizedStorageSize.y), 1, 64));
            GUI.Label(new Rect(114f, y, 18f, 28f), "x", bodyStyle);
            GUI.enabled = true;
            y += 40f;
        }

        private void DrawObjectSettingsScaleControls(ref float y, float width, CampusPlacedObject placed)
        {
            Vector2 scale = placed.NormalizedVisualScale;
            GUI.Label(new Rect(0f, y, 80f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Scale), bodyStyle);
            placed.LockVisualScaleAspect = GUI.Toggle(new Rect(84f, y + 4f, width - 84f, 24f), placed.LockVisualScaleAspect, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.LockAspect));
            y += 34f;
            if (placed.LockVisualScaleAspect)
            {
                float uniform = Mathf.Clamp(scale.x, ObjectSettingsMinScale, ObjectSettingsMaxScale);
                GUI.Label(new Rect(0f, y, 90f, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.UniformScale), bodyStyle);
                uniform = GUI.HorizontalSlider(new Rect(96f, y + 8f, Mathf.Max(40f, width - 170f), 16f), uniform, ObjectSettingsMinScale, ObjectSettingsMaxScale);
                uniform = ParseFloatField(new Rect(width - 66f, y, 66f, 30f), uniform, BuildObjectSettingsInputKey(placed, "uniform_scale"));
                uniform = Mathf.Clamp(uniform, ObjectSettingsMinScale, ObjectSettingsMaxScale);
                scale = new Vector2(uniform, uniform);
                y += 34f;
            }
            else
            {
                scale.x = Mathf.Clamp(scale.x, ObjectSettingsMinScale, ObjectSettingsMaxScale);
                scale.y = Mathf.Clamp(scale.y, ObjectSettingsMinScale, ObjectSettingsMaxScale);
            }

            scale.x = ParseFloatField(new Rect(0f, y, 70f, 30f), scale.x, BuildObjectSettingsInputKey(placed, "scale_x"));
            GUI.Label(new Rect(76f, y, 18f, 28f), "x", bodyStyle);
            scale.y = ParseFloatField(new Rect(98f, y, 70f, 30f), scale.y, BuildObjectSettingsInputKey(placed, "scale_y"));
            GUI.Label(new Rect(176f, y, Mathf.Max(10f, width - 176f), 24f), Tr(CampusRuntimeEditorTextId.PreviewGrid) + " " + placed.NormalizedFootprintSize.x + "x" + placed.NormalizedFootprintSize.y, mutedStyle);

            placed.VisualScale = CampusPlacedObject.NormalizeVisualScale(scale);
            placed.ApplyVisualScaleState();
            y += 42f;
        }

        private void DrawObjectSettingsRotationControls(ref float y, float width, CampusPlacedObject placed)
        {
            if (placed != null && placed.IsWallMounted)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                GUI.Label(new Rect(0f, y, width, 24f), Tr("\u65cb\u8f6c\u51b3\u5b9a\u5438\u9644\u5899\u9762", "Rotation chooses the snapped wall face"), bodyStyle);
                y += 28f;
                float wallFaceButtonWidth = Mathf.Max(54f, (width - 24f) * 0.25f);
                string[] faceLabels =
                {
                    Tr("0\u00b0 \u5317", "0\u00b0 North"),
                    Tr("90\u00b0 \u4e1c", "90\u00b0 East"),
                    Tr("180\u00b0 \u5357", "180\u00b0 South"),
                    Tr("270\u00b0 \u897f", "270\u00b0 West")
                };
                for (int i = 0; i < 4; i++)
                {
                    Rect buttonRect = new Rect((wallFaceButtonWidth + 8f) * i, y, wallFaceButtonWidth, 28f);
                    GUIStyle style = objectSettingsPreviewRotation90 == i ? selectedButtonStyle : buttonStyle;
                    if (GUI.Button(buttonRect, faceLabels[i], style))
                    {
                        objectSettingsPreviewRotation90 = i;
                    }
                }

                y += 40f;
                GUI.Label(new Rect(0f, y, width, 42f), Tr("\u58c1\u6302\u7269\u4f53\u53ea\u4f7f\u7528\u4e00\u5f20\u4e3b\u8d34\u56fe\uff0c\u9884\u89c8\u65cb\u8f6c\u8868\u793a\u5438\u9644\u7684\u5899\u9762\u3002", "Wall-mounted objects use one main sprite. Preview rotation shows the snapped wall face."), mutedStyle);
                y += 48f;
                return;
            }

            bool allowRotation = GUI.Toggle(new Rect(0f, y, width, 24f), placed.AllowRotation, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AllowFourDirections));
            if (allowRotation != placed.AllowRotation)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = allowRotation;
                placed.ApplyRotationVisualState();
            }

            y += 30f;
            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RotationPreview), bodyStyle);
            y += 28f;
            float previewButtonWidth = Mathf.Max(54f, (width - 24f) * 0.25f);
            for (int i = 0; i < 4; i++)
            {
                Rect buttonRect = new Rect((previewButtonWidth + 8f) * i, y, previewButtonWidth, 28f);
                GUIStyle style = objectSettingsPreviewRotation90 == i ? selectedButtonStyle : buttonStyle;
                if (GUI.Button(buttonRect, (i * 90) + "\u00b0", style))
                {
                    objectSettingsPreviewRotation90 = i;
                }
            }

            y += 36f;
            for (int i = 0; i < 4; i++)
            {
                int degrees = i * 90;
                GUI.Label(new Rect(0f, y, 42f, 24f), degrees + "\u00b0", bodyStyle);
                Sprite directionSprite = GetObjectDirectionSprite(placed, i);
                string spriteName = directionSprite != null
                    ? Truncate(directionSprite.name, 18)
                    : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.NotSet);
                Rect directionRect = new Rect(48f, y, Mathf.Max(90f, width - 176f), 28f);
                bool dragHover = HandleObjectDirectionSpriteDrop(directionRect, i);
                GUIStyle rowStyle = objectSettingsPreviewRotation90 == i ? selectedButtonStyle : buttonStyle;
                if (dragHover)
                {
                    GUI.Box(directionRect, GUIContent.none, selectedButtonStyle);
                }

                if (GUI.Button(directionRect, spriteName, rowStyle))
                {
                    objectSettingsPreviewRotation90 = i;
                }

                if (GUI.Button(new Rect(width - 120f, y, 56f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.PickSprite), buttonStyle))
                {
                    SetSelectedObjectDirectionSprite(i);
                }

                if (GUI.Button(new Rect(width - 58f, y, 56f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Clear), buttonStyle))
                {
                    ClearSelectedObjectDirectionSprite(i);
                }

                y += 32f;
            }

            y += 4f;
        }

        private bool HandleObjectDirectionSpriteDrop(Rect rect, int rotation90Index)
        {
#if UNITY_EDITOR
            Event current = Event.current;
            if (current == null || !rect.Contains(current.mousePosition))
            {
                return false;
            }

            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
            {
                return false;
            }

            string sourcePath = ResolveEditorDraggedImagePath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                return false;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                TryAssignSelectedObjectDirectionSprite(rotation90Index, sourcePath);
            }

            current.Use();
            return true;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        private string ResolveEditorDraggedImagePath()
        {
            string[] paths = DragAndDrop.paths;
            if (paths != null)
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) && IsSupportedImportImage(path))
                    {
                        return path;
                    }
                }
            }

            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            if (references != null)
            {
                for (int i = 0; i < references.Length; i++)
                {
                    string assetPath = AssetDatabase.GetAssetPath(references[i]);
                    if (string.IsNullOrWhiteSpace(assetPath))
                    {
                        continue;
                    }

                    string fullPath = Path.GetFullPath(assetPath);
                    if (File.Exists(fullPath) && IsSupportedImportImage(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return string.Empty;
        }
#endif

        private void DrawObjectSettingsAnchorControls(ref float y, float width, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.MultiInteractionAnchors), headerStyle);
            y += 28f;
            placed.UseCustomInteractionAnchor = GUI.Toggle(new Rect(0f, y, width, 24f), placed.UseCustomInteractionAnchor, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.UseCustomInteractionAnchor));
            y += 30f;
            if (!placed.UseCustomInteractionAnchor)
            {
                return;
            }

            placed.CustomInteractionAnchors = placed.CustomInteractionAnchors ?? new List<CampusPlacedObjectInteractionAnchor>();
            if (placed.CustomInteractionAnchors.Count == 0)
            {
                AddCustomInteractionAnchor(placed);
            }

            EnsureSelectedCustomAnchorIndex(placed);
            float buttonWidth = Mathf.Max(92f, (width - 8f) * 0.5f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AddAnchor), buttonStyle))
            {
                AddCustomInteractionAnchor(placed);
            }

            GUI.enabled = placed.CustomInteractionAnchors.Count > 0;
            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RemoveAnchor), buttonStyle))
            {
                RemoveSelectedCustomInteractionAnchor(placed);
            }

            GUI.enabled = true;
            y += 38f;
            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AnchorList), bodyStyle);
            y += 28f;
            for (int i = 0; i < placed.CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor listAnchor = placed.CustomInteractionAnchors[i];
                if (listAnchor == null)
                {
                    listAnchor = CreateDefaultCustomAnchor(i);
                    placed.CustomInteractionAnchors[i] = listAnchor;
                }

                Rect selectRect = new Rect(0f, y, 148f, 28f);
                GUIStyle selectStyle = selectedCustomAnchorIndex == i ? selectedButtonStyle : buttonStyle;
                string anchorLabel = string.IsNullOrWhiteSpace(listAnchor.DisplayName) ? listAnchor.AnchorId : listAnchor.DisplayName;
                if (GUI.Button(selectRect, Truncate(anchorLabel, 16), selectStyle))
                {
                    selectedCustomAnchorIndex = i;
                    SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
                }

                listAnchor.Enabled = GUI.Toggle(new Rect(156f, y + 4f, width - 156f, 24f), listAnchor.Enabled, CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Enabled));
                y += 32f;
            }

            CampusPlacedObjectInteractionAnchor anchor = GetSelectedCustomAnchor(placed);
            if (anchor == null)
            {
                return;
            }

            GUI.Label(new Rect(0f, y, width, 24f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.SelectedAnchor), bodyStyle);
            y += 28f;
            GUI.Label(new Rect(0f, y, 84f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.AnchorId), bodyStyle);
            anchor.AnchorId = DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.AnchorId) ? string.Empty : anchor.AnchorId, BuildObjectSettingsInputKey(placed, "anchor_id_" + selectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.DisplayName), bodyStyle);
            anchor.DisplayName = DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.DisplayName) ? string.Empty : anchor.DisplayName, BuildObjectSettingsInputKey(placed, "anchor_name_" + selectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.ActionId), bodyStyle);
            anchor.ActionId = DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.ActionId) ? string.Empty : anchor.ActionId, BuildObjectSettingsInputKey(placed, "anchor_action_" + selectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Payload), bodyStyle);
            anchor.Payload = DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.Payload) ? string.Empty : anchor.Payload, BuildObjectSettingsInputKey(placed, "anchor_payload_" + selectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 84f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Prompt), bodyStyle);
            anchor.PromptText = DrawTextInput(new Rect(88f, y, width - 88f, 30f), string.IsNullOrEmpty(anchor.PromptText) ? string.Empty : anchor.PromptText, BuildObjectSettingsInputKey(placed, "anchor_prompt_" + selectedCustomAnchorIndex));
            y += 36f;
            GUI.Label(new Rect(0f, y, 18f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.X), bodyStyle);
            Vector3 localPosition = anchor.LocalPosition;
            localPosition.x = ParseFloatField(new Rect(22f, y, 56f, 30f), localPosition.x, BuildObjectSettingsInputKey(placed, "anchor_x_" + selectedCustomAnchorIndex));
            GUI.Label(new Rect(84f, y, 18f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.Y), bodyStyle);
            localPosition.y = ParseFloatField(new Rect(106f, y, 56f, 30f), localPosition.y, BuildObjectSettingsInputKey(placed, "anchor_y_" + selectedCustomAnchorIndex));
            GUI.Label(new Rect(168f, y, 18f, 28f), CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.R), bodyStyle);
            anchor.Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(ParseFloatField(new Rect(190f, y, 56f, 30f), anchor.Radius, BuildObjectSettingsInputKey(placed, "anchor_r_" + selectedCustomAnchorIndex)));
            anchor.LocalPosition = localPosition;
            y += 38f;
            SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
        }

        private Sprite ResolveObjectSettingsPreviewSprite(GameObject prefab, CampusPlacedObject placed, out bool usesDirectionalSprite, out int effectiveRotation90)
        {
            usesDirectionalSprite = false;
            effectiveRotation90 = 0;
            if (prefab == null)
            {
                return null;
            }

            if (placed != null)
            {
                return placed.ResolveSpriteForRotation(objectSettingsPreviewRotation90, out usesDirectionalSprite, out effectiveRotation90);
            }

            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private Rect DrawObjectSettingsPreviewGrid(Rect previewRect, Vector2Int footprint, Sprite sprite, CampusPlacedObject placed, bool usesDirectionalSprite, int effectiveRotation90)
        {
            float cellSize = Mathf.Min((previewRect.width - 12f) / footprint.x, (previewRect.height - 12f) / footprint.y);
            cellSize = Mathf.Max(8f, cellSize);
            float gridWidth = cellSize * footprint.x;
            float gridHeight = cellSize * footprint.y;
            Rect gridRect = new Rect(
                previewRect.x + (previewRect.width - gridWidth) * 0.5f,
                previewRect.y + (previewRect.height - gridHeight) * 0.5f,
                gridWidth,
                gridHeight);
            DrawPreviewGridCells(gridRect, footprint, cellSize);

            if (sprite != null)
            {
                DrawObjectSettingsPreviewSprite(gridRect, cellSize, sprite, placed, usesDirectionalSprite, effectiveRotation90);
            }
            else
            {
                GUI.DrawTexture(new Rect(gridRect.x + 4f, gridRect.y + 4f, gridRect.width - 8f, gridRect.height - 8f), tileFallbackTexture, ScaleMode.ScaleToFit);
            }

            if (placed != null && placed.UseCustomInteractionAnchor)
            {
                DrawObjectSettingsPreviewAnchorMarker(gridRect, footprint, GetSelectedCustomAnchor(placed));
            }

            return gridRect;
        }

        private void DrawPreviewGridCells(Rect gridRect, Vector2Int footprint, float cellSize)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.09f);
            GUI.DrawTexture(gridRect, lineTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            for (int x = 0; x <= footprint.x; x++)
            {
                GUI.DrawTexture(new Rect(gridRect.x + x * cellSize, gridRect.y, 1f, gridRect.height), lineTexture);
            }

            for (int y = 0; y <= footprint.y; y++)
            {
                GUI.DrawTexture(new Rect(gridRect.x, gridRect.y + y * cellSize, gridRect.width, 1f), lineTexture);
            }

            GUI.color = oldColor;
        }

        private void DrawObjectSettingsPreviewSprite(Rect gridRect, float cellSize, Sprite sprite, CampusPlacedObject placed, bool usesDirectionalSprite, int effectiveRotation90)
        {
            if (sprite == null)
            {
                return;
            }

            Vector2 spriteWorldSize = sprite.bounds.size;
            Vector2 visualScale = placed != null ? placed.NormalizedVisualScale : Vector2.one;
            float pixelWidth = Mathf.Max(8f, spriteWorldSize.x * visualScale.x * cellSize);
            float pixelHeight = Mathf.Max(8f, spriteWorldSize.y * visualScale.y * cellSize);
            Rect spriteRect = new Rect(
                gridRect.center.x - pixelWidth * 0.5f,
                gridRect.center.y - pixelHeight * 0.5f,
                pixelWidth,
                pixelHeight);

            Matrix4x4 oldMatrix = GUI.matrix;
            if (!usesDirectionalSprite &&
                (placed == null || !placed.SuppressFlatSpriteRotation) &&
                !Mathf.Approximately(effectiveRotation90 * 90f, 0f))
            {
                GUIUtility.RotateAroundPivot(-effectiveRotation90 * 90f, spriteRect.center);
            }

            DrawSprite(spriteRect, sprite);
            GUI.matrix = oldMatrix;
        }

        private void DrawObjectSettingsPreviewAnchorMarker(Rect gridRect, Vector2Int footprint, CampusPlacedObjectInteractionAnchor anchor)
        {
            if (anchor == null)
            {
                return;
            }

            Vector2 previewPoint = ConvertAnchorLocalPositionToPreviewPoint(gridRect, footprint, anchor.LocalPosition);
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.78f, 0.22f, 1f);
            GUI.DrawTexture(new Rect(previewPoint.x - 2f, previewPoint.y - 6f, 4f, 12f), lineTexture);
            GUI.DrawTexture(new Rect(previewPoint.x - 6f, previewPoint.y - 2f, 12f, 4f), lineTexture);
            GUI.color = oldColor;
        }

        private void HandleObjectSettingsPreviewAnchorInput(Rect previewRect, Rect gridRect, Vector2Int footprint, CampusPlacedObject placed)
        {
            if (placed == null || !placed.UseCustomInteractionAnchor)
            {
                return;
            }

            CampusPlacedObjectInteractionAnchor anchor = GetSelectedCustomAnchor(placed);
            if (anchor == null)
            {
                return;
            }

            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (!previewRect.Contains(current.mousePosition) || !gridRect.Contains(current.mousePosition))
            {
                return;
            }

            anchor.LocalPosition = ConvertPreviewPointToAnchorLocalPosition(gridRect, footprint, current.mousePosition);
            SyncLegacyAnchorFieldsFromSelectedAnchor(placed);
            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
            GUI.changed = true;
            current.Use();
        }

        private Vector2 ConvertAnchorLocalPositionToPreviewPoint(Rect gridRect, Vector2Int footprint, Vector3 localPosition)
        {
            float normalizedX = Mathf.InverseLerp(-footprint.x * 0.5f, footprint.x * 0.5f, localPosition.x);
            float normalizedY = Mathf.InverseLerp(-footprint.y * 0.5f, footprint.y * 0.5f, localPosition.y);
            return new Vector2(
                Mathf.Lerp(gridRect.xMin, gridRect.xMax, normalizedX),
                Mathf.Lerp(gridRect.yMax, gridRect.yMin, normalizedY));
        }

        private Vector3 ConvertPreviewPointToAnchorLocalPosition(Rect gridRect, Vector2Int footprint, Vector2 previewPoint)
        {
            float normalizedX = Mathf.InverseLerp(gridRect.xMin, gridRect.xMax, previewPoint.x);
            float normalizedY = Mathf.InverseLerp(gridRect.yMax, gridRect.yMin, previewPoint.y);
            return new Vector3(
                Mathf.Lerp(-footprint.x * 0.5f, footprint.x * 0.5f, normalizedX),
                Mathf.Lerp(-footprint.y * 0.5f, footprint.y * 0.5f, normalizedY),
                0f);
        }

        private string BuildObjectSettingsInputKey(CampusPlacedObject placed, string fieldName)
        {
            string objectId = placed != null && !string.IsNullOrWhiteSpace(placed.ObjectId) ? placed.ObjectId : "object";
            return "object_settings_" + objectId + "_" + fieldName;
        }

        private void ImportSelectedFilesIntoFolder(string folder, string label)
        {
#if UNITY_EDITOR
            string path = SelectSingleFile(TrFormat("\u5bfc\u5165 {0}", "Import {0}", label), Tr("\u5168\u90e8", "All") + "|*.*");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            File.Copy(path, MakeUniqueImportPath(Path.Combine(folder, Path.GetFileName(path))), false);
            LoadRuntimeResources();
            RefreshImportAssetDatabaseIfProjectBacked();
#endif
        }

        private void ImportSelectedFolderIntoFolder(string folder, string label)
        {
#if UNITY_EDITOR
            string source = EditorUtility.OpenFolderPanel(TrFormat("\u5bfc\u5165 {0}", "Import {0}", label), Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            MirrorDirectory(source, folder, false);
            LoadRuntimeResources();
            RefreshImportAssetDatabaseIfProjectBacked();
#endif
        }

        private int ImportClipboardImagesIntoFolder(string folder, string label)
        {
            string buffer = GUIUtility.systemCopyBuffer ?? string.Empty;
            if (string.IsNullOrWhiteSpace(buffer))
            {
                return 0;
            }

            int count = 0;
            string[] candidates = buffer.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Directory.CreateDirectory(folder);
            for (int i = 0; i < candidates.Length; i++)
            {
                string normalized = NormalizeClipboardPath(candidates[i]);
                if (!File.Exists(normalized) || !IsSupportedImportImage(normalized))
                {
                    continue;
                }

                File.Copy(normalized, MakeUniqueImportPath(Path.Combine(folder, Path.GetFileName(normalized))), false);
                count++;
            }

            if (count > 0)
            {
                LoadRuntimeResources();
                RefreshImportAssetDatabaseIfProjectBacked();
            }

            return count;
        }

        private string SelectSingleFile(string title, string filter)
        {
#if UNITY_EDITOR
            string[] parts = (filter ?? "All|*.*").Split('|');
            if (parts.Length >= 2)
            {
                string extension = ExtractEditorFileExtension(parts[1]);
                return EditorUtility.OpenFilePanel(title, Application.dataPath, extension);
            }
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return OpenSingleFilePanelWindows(title, filter);
#else
            return string.Empty;
#endif
        }

        private static string ExtractEditorFileExtension(string rawFilterPattern)
        {
            if (string.IsNullOrWhiteSpace(rawFilterPattern))
            {
                return string.Empty;
            }

            string[] patterns = rawFilterPattern.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < patterns.Length; i++)
            {
                string pattern = patterns[i].Trim();
                if (pattern.StartsWith("*.", StringComparison.Ordinal))
                {
                    return pattern.Substring(2);
                }
            }

            return string.Empty;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static string OpenSingleFilePanelWindows(string title, string filter)
        {
            try
            {
                return CampusRuntimeNativeFileDialog.OpenSingleFile(title, filter);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Native file dialog failed: " + exception.Message);
                return string.Empty;
            }
        }
#endif

        private void LoadImportedRoomDefinitions()
        {
            ClearRoomDefinitions();
            string path = GetRoomImportFile();
            if (File.Exists(path))
            {
                ImportRoomDefinitionsFromText(File.ReadAllText(path, Encoding.UTF8));
            }
        }

        private bool IsSupportedImportImage(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureStorageInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            placed.CustomInteractionAnchors = placed.CustomInteractionAnchors ?? new List<CampusPlacedObjectInteractionAnchor>();
            if (placed.CustomInteractionAnchors.Count == 0)
            {
                placed.CustomInteractionAnchors.Add(new CampusPlacedObjectInteractionAnchor
                {
                    AnchorId = "storage",
                    DisplayName = "Storage",
                    LocalPosition = Vector3.zero,
                    Radius = CampusPlacedObject.DefaultInteractionAnchorRadius,
                    PromptText = "Storage",
                    Priority = 120
                });
            }

            placed.NormalizeCustomInteractionAnchors();
        }

        private string NormalizeClipboardPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string value = path.Trim().Trim('"');
            if (value.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                value = Uri.UnescapeDataString(value.Substring("file:///".Length));
            }

            return value.Replace('/', Path.DirectorySeparatorChar);
        }

        private string GetImportFolderForTarget(CampusRuntimeImportTarget target)
        {
            switch (target)
            {
                case CampusRuntimeImportTarget.Floor:
                    return GetFloorImportFolder();
                case CampusRuntimeImportTarget.Wall:
                case CampusRuntimeImportTarget.WallFace:
                case CampusRuntimeImportTarget.WallCap:
                    return GetWallImportFolder();
                case CampusRuntimeImportTarget.Object:
                    return GetObjectImportFolder();
                case CampusRuntimeImportTarget.Room:
                    return GetRoomPrefabFolder();
                default:
                    return GetImportRootFolder();
            }
        }

        private string ResolveImportTargetLabel(CampusRuntimeImportTarget target)
        {
            switch (target)
            {
                case CampusRuntimeImportTarget.Floor:
                    return Tr(CampusRuntimeEditorTextId.FloorImports);
                case CampusRuntimeImportTarget.Wall:
                    return Tr(CampusRuntimeEditorTextId.WallImports);
                case CampusRuntimeImportTarget.Object:
                    return Tr(CampusRuntimeEditorTextId.ObjectImports);
                case CampusRuntimeImportTarget.Room:
                    return Tr(CampusRuntimeEditorTextId.RoomList);
                case CampusRuntimeImportTarget.WallFace:
                    return Tr("\u5899\u9762\u8d34\u56fe", "Wall Face Texture");
                case CampusRuntimeImportTarget.WallCap:
                    return Tr("\u5899\u9876\u8d34\u56fe", "Wall Cap Texture");
                default:
                    return Tr("\u5bfc\u5165", "Import");
            }
        }

        private string Tr(CampusRuntimeEditorTextId id)
        {
            return CampusRuntimeEditorTextCatalog.Get(displayLanguage, id);
        }

        private string Tr(string chinese, string english)
        {
            return CampusRuntimeEditorTextCatalog.Get(displayLanguage, chinese, english);
        }

        private string TrFormat(string chinese, string english, params object[] args)
        {
            return CampusRuntimeEditorTextCatalog.Format(displayLanguage, chinese, english, args);
        }

        private void SetStatus(string message)
        {
            statusText = message;
            statusUntil = Time.realtimeSinceStartup + 4f;
        }

        private static void AddUnique<T>(List<T> list, T item) where T : UnityEngine.Object
        {
            if (item != null && !list.Contains(item))
            {
                list.Add(item);
            }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = value.Trim();
            for (int i = 0; i < invalid.Length; i++)
            {
                sanitized = sanitized.Replace(invalid[i], '_');
            }

            sanitized = sanitized.Replace('/', '_').Replace('\\', '_');
            return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
        }

        private static bool WasKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            KeyControl control = GetKeyControl(key);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        private static bool IsKeyHeld(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            KeyControl control = GetKeyControl(key);
            return control != null && control.isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        private static bool WasMouseButtonPressed(int button)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current == null)
            {
                return false;
            }

            if (button == 0)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }

            if (button == 1)
            {
                return Mouse.current.rightButton.wasPressedThisFrame;
            }

            return Mouse.current.middleButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        private static bool WasMouseButtonReleased(int button)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current == null)
            {
                return false;
            }

            if (button == 0)
            {
                return Mouse.current.leftButton.wasReleasedThisFrame;
            }

            if (button == 1)
            {
                return Mouse.current.rightButton.wasReleasedThisFrame;
            }

            return Mouse.current.middleButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(button);
#endif
        }

        private static bool IsMouseButtonHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current == null)
            {
                return false;
            }

            if (button == 0)
            {
                return Mouse.current.leftButton.isPressed;
            }

            if (button == 1)
            {
                return Mouse.current.rightButton.isPressed;
            }

            return Mouse.current.middleButton.isPressed;
#else
            return Input.GetMouseButton(button);
#endif
        }

        private static Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private static float GetMouseScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

#if UNITY_EDITOR
        public static void BakeRuntimeGeneratedContentIntoCampusMapScene()
        {
            const string scenePath = "Assets/Scenes/CampusMap.unity";
            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            CampusRuntimeMapEditor editor = FindFirstObjectByType<CampusRuntimeMapEditor>(FindObjectsInactive.Include);
            if (editor == null)
            {
                GameObject host = new GameObject("Campus Runtime Map Editor");
                editor = host.AddComponent<CampusRuntimeMapEditor>();
            }

            editor.autoLoadPlayerMapOnStart = false;
            editor.LoadRuntimeResources();
            editor.RefreshSceneReferences(true);
            editor.RestoreRuntimeAuthoringPackage(false, false);
            editor.autoLoadPlayerMapOnStart = false;
            editor.RefreshSceneReferences(true);

            CampusMapRoot root = editor.mapRoot != null
                ? editor.mapRoot
                : FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            CampusDayNightController.EnsureSceneController(root);
            NtingCustomShadowSystem.EnsureSceneSystem();

            NtingCampus.Gameplay.Core.CampusGameBootstrap bootstrap =
                NtingCampus.Gameplay.Core.CampusGameBootstrap.EnsureSceneBootstrap();

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                UnityEditor.EditorUtility.SetDirty(roots[i]);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("[NtingCampusRuntimeMapEditor] Baked runtime generated content into " + scenePath);
        }
#endif

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private static KeyControl GetKeyControl(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return null;
            }

            switch (key)
            {
                case KeyCode.F10:
                    return keyboard.f10Key;
                case KeyCode.Escape:
                    return keyboard.escapeKey;
                case KeyCode.G:
                    return keyboard.gKey;
                case KeyCode.R:
                    return keyboard.rKey;
                case KeyCode.Z:
                    return keyboard.zKey;
                case KeyCode.Y:
                    return keyboard.yKey;
                case KeyCode.LeftBracket:
                    return keyboard.leftBracketKey;
                case KeyCode.RightBracket:
                    return keyboard.rightBracketKey;
                case KeyCode.LeftShift:
                    return keyboard.leftShiftKey;
                case KeyCode.RightShift:
                    return keyboard.rightShiftKey;
                case KeyCode.LeftControl:
                    return keyboard.leftCtrlKey;
                case KeyCode.RightControl:
                    return keyboard.rightCtrlKey;
                case KeyCode.LeftAlt:
                    return keyboard.leftAltKey;
                case KeyCode.RightAlt:
                    return keyboard.rightAltKey;
                case KeyCode.Space:
                    return keyboard.spaceKey;
                default:
                    return null;
            }
        }
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    internal sealed class CampusRuntimeFileDropBridge : IDisposable
    {
        private const int WmDropFiles = 0x0233;
        private const int GwlpWndProc = -4;
        private readonly Action<string[]> onDrop;
        private readonly IntPtr windowHandle;
        private readonly WndProcDelegate wndProcDelegate;
        private readonly IntPtr oldWndProc;
        private bool disposed;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private CampusRuntimeFileDropBridge(IntPtr handle, Action<string[]> dropHandler)
        {
            windowHandle = handle;
            onDrop = dropHandler;
            wndProcDelegate = WndProc;
            oldWndProc = SetWindowLongPtr(windowHandle, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(wndProcDelegate));
            DragAcceptFiles(windowHandle, true);
        }

        public static CampusRuntimeFileDropBridge TryCreate(Action<string[]> dropHandler)
        {
            if (dropHandler == null)
            {
                return null;
            }

            IntPtr handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                handle = GetActiveWindow();
            }

            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return new CampusRuntimeFileDropBridge(handle, dropHandler);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Native file drop failed: " + exception.Message);
                return null;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (windowHandle != IntPtr.Zero && oldWndProc != IntPtr.Zero)
            {
                DragAcceptFiles(windowHandle, false);
                SetWindowLongPtr(windowHandle, GwlpWndProc, oldWndProc);
            }
        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmDropFiles)
            {
                string[] paths = ReadDroppedFiles(wParam);
                DragFinish(wParam);
                if (paths.Length > 0)
                {
                    onDrop(paths);
                }

                return IntPtr.Zero;
            }

            return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
        }

        private static string[] ReadDroppedFiles(IntPtr dropHandle)
        {
            uint count = DragQueryFile(dropHandle, 0xFFFFFFFF, null, 0);
            string[] paths = new string[count];
            for (uint i = 0; i < count; i++)
            {
                uint length = DragQueryFile(dropHandle, i, null, 0);
                StringBuilder builder = new StringBuilder((int)length + 1);
                DragQueryFile(dropHandle, i, builder, builder.Capacity);
                paths[i] = builder.ToString();
            }

            return paths;
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, index, value);
            }

            return new IntPtr(SetWindowLong32(hWnd, index, value.ToInt32()));
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr hWnd, bool accept);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr hDrop);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }

    internal static class CampusRuntimeNativeFileDialog
    {
        private const int MaxFilePathLength = 4096;
        private const int OfnFileMustExist = 0x00001000;
        private const int OfnPathMustExist = 0x00000800;
        private const int OfnNoChangeDir = 0x00000008;
        private const int OfnExplorer = 0x00080000;
        private const int OfnHideReadOnly = 0x00000004;

        public static string OpenSingleFile(string title, string filter)
        {
            StringBuilder fileBuffer = new StringBuilder(MaxFilePathLength);
            OpenFileName dialog = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                title = string.IsNullOrWhiteSpace(title) ? "Select File" : title,
                filter = BuildWindowsFilter(filter),
                file = fileBuffer,
                maxFile = fileBuffer.Capacity,
                flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHideReadOnly
            };

            return GetOpenFileName(ref dialog) ? dialog.file.ToString() : string.Empty;
        }

        private static string BuildWindowsFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return "All Files\0*.*\0\0";
            }

            string[] parts = filter.Split('|');
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                builder.Append(parts[i]);
                builder.Append('\0');
                builder.Append(parts[i + 1]);
                builder.Append('\0');
            }

            if (builder.Length == 0)
            {
                builder.Append("All Files\0*.*\0");
            }

            builder.Append('\0');
            return builder.ToString();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner;
            public IntPtr instance;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public StringBuilder file;
            public int maxFile;
            public StringBuilder fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData;
            public IntPtr hook;
            public string templateName;
            public IntPtr reservedPtr;
            public int reservedInt;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OpenFileName openFileName);
    }
#endif

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
    public sealed class CampusRuntimeRoomPrefab
    {
        public string Schema;
        public string RoomName;
        public string CreatedAtLocal;
        public Vector2Int Size = Vector2Int.one;
        public List<CampusRuntimeTileSnapshot> FloorTiles = new List<CampusRuntimeTileSnapshot>();
        public List<CampusRuntimeTileSnapshot> WallTiles = new List<CampusRuntimeTileSnapshot>();
        public List<CampusRuntimeObjectSnapshot> Objects = new List<CampusRuntimeObjectSnapshot>();
        public List<CampusRuntimeRoomSnapshot> RoomMarkers = new List<CampusRuntimeRoomSnapshot>();
        public List<CampusRuntimeGameplayRoomSnapshot> GameplayRooms = new List<CampusRuntimeGameplayRoomSnapshot>();
        public List<CampusRuntimeGameplayFacilitySnapshot> GameplayFacilities = new List<CampusRuntimeGameplayFacilitySnapshot>();
        public List<CampusRuntimeGameplayPrankSpotSnapshot> GameplayPrankSpots = new List<CampusRuntimeGameplayPrankSpotSnapshot>();
        public List<CampusRuntimeRoomLightSnapshot> Lights = new List<CampusRuntimeRoomLightSnapshot>();

        [NonSerialized] public string SourcePath;
    }

    [Serializable]
    public sealed class CampusRuntimeRoomLightSnapshot
    {
        public CampusRuntimeLightSnapshot Light = new CampusRuntimeLightSnapshot();
        public Vector3Int RelativeCell;
        public Vector3 RelativePosition;
        public bool HasRelativePosition;
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
    }

    internal sealed class RuntimeImportedObjectDefinition
    {
        public RuntimeImportedObjectDefinition(string objectName)
        {
            ObjectName = objectName;
        }

        public string ObjectName;
        public string BaseSpritePath;
        public readonly string[] DirectionSpritePaths = new string[4];

        public bool HasDirectionalSprites
        {
            get
            {
                for (int i = 0; i < DirectionSpritePaths.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(DirectionSpritePaths[i]))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
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


