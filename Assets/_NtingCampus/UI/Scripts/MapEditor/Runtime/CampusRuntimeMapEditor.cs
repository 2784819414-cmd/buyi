using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Retail;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
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
        PlaceGameplayActor,
        EraseGameplayActor,
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
    public sealed partial class CampusRuntimeMapEditor : MonoBehaviour, ICampusRuntimeMapEditorObjectSettingsInspectorHost
    {
        public static CampusRuntimeMapEditor Instance { get; private set; }

        private const string RuntimeResourceFolder = "NtingCampusRuntime";
        private const string WallStrokeUndoPrefix = "WALLSTROKE:";
        private const int MaxUndoSnapshots = 64;
        private const float SceneReferenceRetryInterval = 0.5f;
        private const float AmbientLightIntensity = 0.3f;
        private const float PlacedLightIntensity = 1.15f;
        private const int PaletteTileSize = 92;
        private const string BuiltInRetailShelfContainerObjectId = "retail_shelf_container_2x1";
        private const string BuiltInRetailShelfDisplayObjectId = "retail_shelf_display_2x1";
        private const int ToolbarButtonWidth = 110;
        private const float ZoomStep = 0.12f;
        private const float PanelMargin = 28f;
        private const float TopMargin = 72f;
        private const float BottomToolbarHeight = 74f;
        private const float ObjectSettingsMinScale = 0.05f;
        private const float ObjectSettingsMaxScale = 8f;
        private const byte RuntimeObjectSpriteAlphaThreshold = 8;
        private const string RuntimeImportedVisualNodeName = "Visual";
        private const string TextInputControlPrefix = "CampusRuntimeTextInput_";
        private const int WallStrokeVisualBatchCellThreshold = 8;
        private const int WallStrokeVisualBatchChunkThreshold = 4;

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
        [SerializeField] private int selectedGameplayActorPresetIndex;
        [SerializeField] private int selectedWallProfileIndex;
        [SerializeField] private string selectedFacilityOwnerId = string.Empty;
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
        private bool roomMarkerVisualsHidden;
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
        private bool gameplayActorCacheInitialized;
        private bool roomRegionCountCacheDirty = true;
        private bool editableLightCacheDirty = true;
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
        private string newGameplayActorId = string.Empty;
        private string newGameplayActorChineseName = string.Empty;
        private string newGameplayActorEnglishName = string.Empty;
        private string newGameplayActorClassId = "class_1";
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
        private readonly CampusRuntimeMapEditorObjectSettingsSession objectSettingsSession =
            new CampusRuntimeMapEditorObjectSettingsSession();
        private CampusRuntimeObjectDefinitionCatalog objectDefinitionCatalog = CampusRuntimeObjectDefinitionCatalog.Empty;
        private readonly List<UnityEngine.Object> importedAssets = new List<UnityEngine.Object>();
        private readonly Dictionary<string, Texture2D> importedTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> importedTextureRevisionCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> runtimeObjectSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> roomRegionCountsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Light2D> editableLights = new List<Light2D>();
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
            roomMarkerVisualsHidden = false;
            CampusDynamicShadowUtility.ApplyHighestRuntimeShadowQuality();
            LoadRuntimeResources();
            RefreshSceneReferences(false);
            RememberMapLoadSource(CampusRuntimeMapLoadSource.Scene, GetActiveScenePath());
            if (autoLoadPlayerMapOnStart)
            {
                TryAutoLoadPlayerMap();
            }

            PrepareRuntimeMapPresentationSafe();
            if (!isOpen)
            {
                HideAllRoomMarkerSpriteRenderersOnce();
            }
            else
            {
                RefreshOpenPanelCaches(true);
            }

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
            CampusRuntimeMapEditorLogTextCatalog.Log(
                CampusRuntimeMapEditorLogTextId.ActiveMapSource,
                DescribeMapLoadSource());
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
                SetEditorOpen(!isOpen);
                if (isOpen)
                {
                    MarkSceneReferencesDirty();
                    RefreshSceneReferencesIfNeeded(true);
                    RefreshOpenPanelCaches(true);
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
            RefreshOpenPanelCaches(false);
            EnsureFileDropBridge();
            ProcessPendingDroppedPaths();
            HandleShortcuts();
            if (!HandleCameraNavigation())
            {
                HandleBrushInput();
            }

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

            AddBuiltInRetailShelfPrefabs();

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
            NormalizePlacedObjectTypeIdsFromPalette();
            EnsureAreaDefinitionsAvailable(false);
        }

        private void LoadUserImports()
        {
            objectDefinitionCatalog = CampusRuntimeObjectDefinitionCatalog.Load(
                GetImportRootFolder(),
                message => CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.WarningMessage,
                    message));
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
            CampusRuntimeImportLibrary.EnsureFolders(GetImportRootFolder());
            MigratePersistentImportLibraryToProjectIfNeeded();
        }

        private void LoadImportedTiles(string folder, List<TileBase> destination)
        {
            string[] files = CampusRuntimeImportLibrary.GetImageFiles(folder);
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
            string[] files = CampusRuntimeImportLibrary.GetImageFiles(folder);
            if (files.Length == 0)
            {
                return;
            }

            Transform root = EnsureRuntimeImportPrefabRoot();
            List<RuntimeImportedObjectDefinition> definitions =
                CampusRuntimeImportedObjectLibrary.BuildDefinitions(files, GetImportRootFolder());
            for (int i = 0; i < definitions.Count; i++)
            {
                RuntimeImportedObjectDefinition definition = definitions[i];
                string objectId = objectDefinitionCatalog.ResolveObjectIdForSource(definition.ObjectName);
                if (string.IsNullOrWhiteSpace(objectId))
                {
                    objectId = definition.ObjectName;
                }

                Texture2D texture = LoadImportedTexture(definition.BaseSpritePath);
                if (texture == null)
                {
                    continue;
                }

                Vector2Int footprint = ResolveImportedObjectFootprint(definition.ObjectName, texture);
                Sprite sprite = CreateObjectSprite(texture, objectId, footprint);

                GameObject prefab = new GameObject(objectId);
                prefab.hideFlags = HideFlags.DontSave;
                prefab.transform.SetParent(root, false);
                prefab.SetActive(false);
                CreateRuntimeImportedObjectVisual(prefab.transform, sprite);
                BoxCollider2D collider = prefab.AddComponent<BoxCollider2D>();
                collider.isTrigger = false;
                collider.size = new Vector2(footprint.x, footprint.y);
                CampusPlacedObject placed = prefab.AddComponent<CampusPlacedObject>();
                placed.ObjectId = objectId;
                placed.TypeId = objectDefinitionCatalog.ResolveTypeId(objectId, placed.TypeId);
                placed.LocalizedDisplayNameOverride =
                    objectDefinitionCatalog.ResolveDisplayName(objectId, placed.LocalizedDisplayNameOverride);
                placed.DisplayNameOverride =
                    objectDefinitionCatalog.ResolveDisplayNameText(objectId, placed.DisplayNameOverride);
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
                                objectId);
                        }
                    }

                    placed.ApplyRotationVisualState();
                }

                importedAssets.Add(prefab);
                AddUnique(objectPrefabs, prefab);
            }
        }

        private void AddBuiltInRetailShelfPrefabs()
        {
            Transform root = EnsureRuntimeImportPrefabRoot();
            AddBuiltInRetailShelfPrefab(
                root,
                BuiltInRetailShelfContainerObjectId,
                new CampusLocalizedText(
                    "超市货架（容器）",
                    "Retail Shelf (Container)",
                    "超市貨架（容器）",
                    "Полка магазина (контейнер)",
                    "売店棚（コンテナ）"),
                CampusRetailShelfMode.Container,
                "retail_water",
                true,
                true,
                new Vector2Int(2, 1),
                new Vector2Int(6, 4),
                24f,
                new Color(0.51f, 0.41f, 0.31f, 1f),
                new Color(0.72f, 0.82f, 0.91f, 1f));
            AddBuiltInRetailShelfPrefab(
                root,
                BuiltInRetailShelfDisplayObjectId,
                new CampusLocalizedText(
                    "超市货架（直摆）",
                    "Retail Shelf (Display)",
                    "超市貨架（直擺）",
                    "Полка магазина (витрина)",
                    "売店棚（陳列）"),
                CampusRetailShelfMode.DirectPickupDisplay,
                "retail_potato_chips",
                false,
                false,
                new Vector2Int(2, 1),
                new Vector2Int(4, 4),
                18f,
                new Color(0.54f, 0.35f, 0.24f, 1f),
                new Color(0.95f, 0.82f, 0.41f, 1f));
        }

        private void AddBuiltInRetailShelfPrefab(
            Transform root,
            string objectId,
            CampusLocalizedText displayName,
            CampusRetailShelfMode shelfMode,
            string itemDefinitionId,
            bool isStorageContainer,
            bool isInteractable,
            Vector2Int footprint,
            Vector2Int storageSize,
            float storageMaxWeight,
            Color frameColor,
            Color productColor)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectId))
            {
                return;
            }

            Sprite sprite = CreateBuiltInRetailShelfSprite(objectId, footprint, frameColor, productColor, shelfMode);
            if (sprite == null)
            {
                return;
            }

            GameObject prefab = new GameObject(objectId.Trim());
            prefab.hideFlags = HideFlags.DontSave;
            prefab.transform.SetParent(root, false);
            prefab.SetActive(false);
            CreateRuntimeImportedObjectVisual(prefab.transform, sprite);

            BoxCollider2D collider = prefab.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = new Vector2(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));

            CampusPlacedObject placed = prefab.AddComponent<CampusPlacedObject>();
            placed.ObjectId = objectId.Trim();
            placed.TypeId = nameof(CampusFacilityType.GoodsShelf);
            placed.DisplayNameOverride = displayName.ResolvePrimary(objectId);
            placed.LocalizedDisplayNameOverride = displayName;
            placed.OverrideFootprintSize = true;
            placed.FootprintSize = CampusPlacedObject.NormalizeFootprintSize(footprint);
            placed.BlocksMovement = true;
            placed.BlocksSight = false;
            placed.IsInteractable = isInteractable;
            placed.IsStorageContainer = isStorageContainer;
            placed.StorageSize = CampusPlacedObject.NormalizeStorageSize(storageSize);
            placed.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(storageMaxWeight);
            placed.VisualScale = Vector2.one;
            placed.LockVisualScaleAspect = true;
            placed.OverrideAllowRotation = false;
            placed.AllowRotation = false;

            CampusRetailShelf shelf = prefab.AddComponent<CampusRetailShelf>();
            shelf.ShelfId = objectId.Trim();
            shelf.ItemDefinitionId = string.IsNullOrWhiteSpace(itemDefinitionId) ? string.Empty : itemDefinitionId.Trim();
            shelf.ShelfMode = shelfMode;
            shelf.StockCount = shelfMode == CampusRetailShelfMode.Container ? 12 : 8;
            shelf.AutoRestock = true;
            shelf.DisplaySlotCount = shelfMode == CampusRetailShelfMode.DirectPickupDisplay ? 4 : 3;
            shelf.DisplaySpread = shelfMode == CampusRetailShelfMode.DirectPickupDisplay
                ? new Vector2(1.05f, 0.22f)
                : new Vector2(0.9f, 0.18f);
            shelf.DisplayHeight = shelfMode == CampusRetailShelfMode.DirectPickupDisplay ? 0.44f : 0.36f;

            importedAssets.Add(prefab);
            AddUnique(objectPrefabs, prefab);
        }

        private Sprite CreateBuiltInRetailShelfSprite(
            string spriteName,
            Vector2Int footprint,
            Color frameColor,
            Color productColor,
            CampusRetailShelfMode shelfMode)
        {
            Vector2Int normalizedFootprint = CampusPlacedObject.NormalizeFootprintSize(footprint);
            int pixelWidth = Mathf.Max(96, normalizedFootprint.x * 64);
            int pixelHeight = Mathf.Max(72, normalizedFootprint.y * 64);
            Texture2D texture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.RGBA32, false);
            texture.name = spriteName + "_texture";
            texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color shelfShadow = new Color(0.08f, 0.09f, 0.12f, 0.22f);
            Color shelfBoard = Color.Lerp(frameColor, Color.white, 0.16f);
            Color trimColor = Color.Lerp(frameColor, Color.black, 0.22f);
            Color shelfProduct = productColor;

            Color[] pixels = new Color[pixelWidth * pixelHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            FillRect(pixels, pixelWidth, pixelHeight, 12, 6, pixelWidth - 24, 10, shelfShadow);
            FillRect(pixels, pixelWidth, pixelHeight, 18, 18, pixelWidth - 36, pixelHeight - 30, shelfBoard);
            FillRect(pixels, pixelWidth, pixelHeight, 18, 18, 10, pixelHeight - 30, trimColor);
            FillRect(pixels, pixelWidth, pixelHeight, pixelWidth - 28, 18, 10, pixelHeight - 30, trimColor);
            FillRect(pixels, pixelWidth, pixelHeight, 18, pixelHeight - 20, pixelWidth - 36, 8, trimColor);
            FillRect(pixels, pixelWidth, pixelHeight, 18, pixelHeight / 2 - 4, pixelWidth - 36, 6, trimColor);

            if (shelfMode == CampusRetailShelfMode.Container)
            {
                FillRect(pixels, pixelWidth, pixelHeight, 28, 28, pixelWidth - 56, 18, shelfProduct);
                FillRect(pixels, pixelWidth, pixelHeight, 28, pixelHeight / 2 + 10, pixelWidth - 56, 18, shelfProduct);
            }
            else
            {
                int productWidth = 16;
                int gap = 8;
                int startX = 28;
                int lowerY = 28;
                int upperY = pixelHeight / 2 + 10;
                for (int x = startX; x + productWidth <= pixelWidth - 28; x += productWidth + gap)
                {
                    FillRect(pixels, pixelWidth, pixelHeight, x, lowerY, productWidth, 22, shelfProduct);
                    FillRect(pixels, pixelWidth, pixelHeight, x + 3, upperY, productWidth - 4, 18, Color.Lerp(shelfProduct, Color.white, 0.24f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            importedTextures.Add(texture);
            importedAssets.Add(texture);

            return CreateObjectSprite(texture, spriteName, normalizedFootprint);
        }

        private static void FillRect(
            Color[] pixels,
            int textureWidth,
            int textureHeight,
            int x,
            int y,
            int width,
            int height,
            Color color)
        {
            if (pixels == null || textureWidth <= 0 || textureHeight <= 0)
            {
                return;
            }

            int xMin = Mathf.Clamp(x, 0, textureWidth);
            int yMin = Mathf.Clamp(y, 0, textureHeight);
            int xMax = Mathf.Clamp(x + width, 0, textureWidth);
            int yMax = Mathf.Clamp(y + height, 0, textureHeight);
            for (int py = yMin; py < yMax; py++)
            {
                int row = py * textureWidth;
                for (int px = xMin; px < xMax; px++)
                {
                    pixels[row + px] = color;
                }
            }
        }

        private static SpriteRenderer CreateRuntimeImportedObjectVisual(Transform parent, Sprite sprite)
        {
            GameObject visual = new GameObject(RuntimeImportedVisualNodeName);
            visual.hideFlags = HideFlags.DontSave;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            return renderer;
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
            string objectId = CampusRuntimeObjectDefinitionCatalog.BuildStableObjectId(
                displayName,
                footprint,
                CandidateObjectIdExists);
            string path = CampusRuntimeImportLibrary.MakeUniquePath(Path.Combine(GetObjectImportFolder(), objectId + ".png"));
            Texture2D texture = CreateGeneratedObjectTexture(footprint, newObjectColor);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
                RefreshImportAssetDatabaseIfProjectBacked();
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToCreateObjectImage,
                    path,
                    exception.Message);
                SetStatus(TrFormat("\u521b\u5efa\u7269\u4f53\u5931\u8d25\uff1a{0}", "Create object failed: {0}", exception.Message));
                return;
            }
            finally
            {
                DestroyRuntimeObject(texture);
            }

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
                placed.TypeId = CampusRuntimeObjectAuthoring.InferObjectTypeId(objectId, displayName, newObjectIsStorageContainer);
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
                SaveRuntimeObjectSettings(CampusRuntimeObjectAuthoring.CaptureSettings(
                    prefab,
                    placed,
                    GetImportRootFolder(),
                    Tr("\u4ea4\u4e92", "Interact")));
            }

            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u521b\u5efa\u7269\u4f53\uff1a{0} {1}x{2}", "Created object: {0} {1}x{2}", displayName, footprint.x, footprint.y));
        }

        private bool CandidateObjectIdExists(string objectId)
        {
            return !string.IsNullOrWhiteSpace(objectId) &&
                   (FindPrefabIndexByName(objectId) >= 0 ||
                    File.Exists(CampusRuntimeImportLibrary.GetObjectSettingsPath(GetImportRootFolder(), objectId)) ||
                    File.Exists(Path.Combine(GetObjectImportFolder(), objectId + ".png")));
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
            roomPrefabs.AddRange(CampusRuntimeRoomPrefabLibrary.LoadAll(
                GetRoomPrefabFolder(),
                message => CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.WarningMessage,
                    message)));
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
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToImportImage,
                    path,
                    exception.Message);
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
            return CampusRuntimeImportLibrary.NormalizeSerializedPath(path, GetImportRootFolder());
        }

        private string ResolveImportContentPath(string path)
        {
            return CampusRuntimeImportLibrary.ResolveContentPath(path, GetImportRootFolder());
        }

        private string GetPersistentImportRootFolder()
        {
            return CampusRuntimeImportLibrary.GetPersistentImportRootFolder();
        }

        private string GetFloorImportFolder()
        {
            return CampusRuntimeImportLibrary.GetFloorImportFolder(GetImportRootFolder());
        }

        private string GetWallImportFolder()
        {
            return CampusRuntimeImportLibrary.GetWallImportFolder(GetImportRootFolder());
        }

        private string GetObjectImportFolder()
        {
            return CampusRuntimeImportLibrary.GetObjectImportFolder(GetImportRootFolder());
        }

        private string GetRoomImportFile()
        {
            return CampusRuntimeImportLibrary.GetRoomImportFile(GetImportRootFolder());
        }

        private string GetRoomPrefabFolder()
        {
            return CampusRuntimeImportLibrary.GetRoomPrefabFolder(GetImportRootFolder());
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
            InvalidateOpenPanelCaches();
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
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.MapPresentationSetupFailed,
                    exception.Message);
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
            for (int i = 0; i < CampusRuntimeAreaPresetCatalog.Presets.Length; i++)
            {
                CampusRuntimeAreaPreset preset = CampusRuntimeAreaPresetCatalog.Presets[i];
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
            if (!CampusRuntimeAreaPresetCatalog.TryResolveRoomName(roomName, out string presetRoomName))
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

        private CampusRuntimeAreaPreset GetAreaPreset(string roomName)
        {
            return CampusRuntimeAreaPresetCatalog.GetPreset(roomName);
        }

        private string GetAreaPresetLabel(string roomName)
        {
            CampusRuntimeAreaPreset preset = GetAreaPreset(roomName);
            return preset != null
                ? Tr(preset.Label)
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
                    SetEditorOpen(false);
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
                if (brushMode == CampusRuntimeBrushMode.PlaceGameplayActor ||
                    brushMode == CampusRuntimeBrushMode.EraseGameplayActor)
                {
                    EraseGameplayActorsAtCell(floor, hoverCell, true);
                }
                else if (IsGameplayBrushMode(brushMode))
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
                case CampusRuntimeBrushMode.PlaceGameplayActor:
                    if (leftDown)
                    {
                        PlaceGameplayActor(floor, hoverCell);
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
                case CampusRuntimeBrushMode.EraseGameplayActor:
                    if (leftDown)
                    {
                        EraseGameplayActorsAtCell(floor, hoverCell, true);
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
                   mode == CampusRuntimeBrushMode.EraseGameplayMarker ||
                   mode == CampusRuntimeBrushMode.PlaceGameplayActor ||
                   mode == CampusRuntimeBrushMode.EraseGameplayActor;
        }

        private void ApplyRectangleBrush(CampusFloorRoot floor, Vector3Int start, Vector3Int end, CampusRuntimeBrushMode mode)
        {
            if (floor == null)
            {
                return;
            }

            switch (mode)
            {
                case CampusRuntimeBrushMode.RectangleFloor:
                    PaintFloorRectangle(floor, start, end);
                    break;
                case CampusRuntimeBrushMode.RectangleWall:
                    PaintWallRectangle(floor, start, end);
                    RebuildWallVisuals(floor);
                    break;
                case CampusRuntimeBrushMode.RectangleRoom:
                    MarkRoomRectangle(floor, start, end);
                    RebuildGameplayRoomRegistrySafe();
                    SchedulePlayerMapSave();
                    SetStatus(TrFormat("\u5df2\u6807\u8bb0\u533a\u57df\uff1a{0}", "Marked area: {0}", GetAreaPresetLabel(GetSelectedRoomName())));
                    break;
                case CampusRuntimeBrushMode.RectangleErase:
                    EraseRectangle(floor, start, end);
                    RebuildWallVisuals(floor);
                    break;
            }

            floor.MarkUsedBoundsDirty();
        }

        private void PaintFloorRectangle(CampusFloorRoot floor, Vector3Int start, Vector3Int end)
        {
            TileBase tile = GetSelectedFloorTile();
            if (tile == null || floor == null || floor.FloorTilemap == null)
            {
                return;
            }

            Matrix4x4 transform = BuildTileTransform();
            foreach (Vector3Int cell in RectangleCells(start, end))
            {
                floor.FloorTilemap.SetTile(cell, tile);
                floor.FloorTilemap.SetTileFlags(cell, TileFlags.None);
                floor.FloorTilemap.SetTransformMatrix(cell, transform);
            }
        }

        private void PaintWallRectangle(CampusFloorRoot floor, Vector3Int start, Vector3Int end)
        {
            TileBase tile = GetSelectedWallTile();
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (tile == null || wallLogic == null)
            {
                return;
            }

            Matrix4x4 transform = BuildTileTransform();
            foreach (Vector3Int cell in RectanglePerimeterCells(start, end))
            {
                wallLogic.SetTile(cell, tile);
                wallLogic.SetTileFlags(cell, TileFlags.None);
                wallLogic.SetTransformMatrix(cell, transform);
            }
        }

        private void MarkRoomRectangle(CampusFloorRoot floor, Vector3Int start, Vector3Int end)
        {
            foreach (Vector3Int cell in RectangleCells(start, end))
            {
                PlaceRoomMarker(floor, cell, false, false);
            }
        }

        private void EraseRectangle(CampusFloorRoot floor, Vector3Int start, Vector3Int end)
        {
            foreach (Vector3Int cell in RectangleCells(start, end))
            {
                EraseAtCell(floor, cell, false);
            }
        }

        private static IEnumerable<Vector3Int> RectangleCells(Vector3Int start, Vector3Int end)
        {
            GetRectangleBounds(start, end, out int minX, out int maxX, out int minY, out int maxY);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    yield return new Vector3Int(x, y, 0);
                }
            }
        }

        private static IEnumerable<Vector3Int> RectanglePerimeterCells(Vector3Int start, Vector3Int end)
        {
            GetRectangleBounds(start, end, out int minX, out int maxX, out int minY, out int maxY);
            for (int x = minX; x <= maxX; x++)
            {
                yield return new Vector3Int(x, minY, 0);
                if (maxY != minY)
                {
                    yield return new Vector3Int(x, maxY, 0);
                }
            }

            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                yield return new Vector3Int(minX, y, 0);
                if (maxX != minX)
                {
                    yield return new Vector3Int(maxX, y, 0);
                }
            }
        }

        private static void GetRectangleBounds(Vector3Int start, Vector3Int end, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = Mathf.Min(start.x, end.x);
            maxX = Mathf.Max(start.x, end.x);
            minY = Mathf.Min(start.y, end.y);
            maxY = Mathf.Max(start.y, end.y);
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
            string displayName = GetObjectDisplayName(prefab);
            CampusFacilityType placementType = CampusRuntimeObjectAuthoring.ResolveFacilityType(prefab, prefabPlaced, displayName);
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

            if (CampusRuntimeObjectAuthoring.IsStackableFacilityObject(placementType))
            {
                footprint = Vector2Int.one;
            }

            Vector2Int rotatedFootprint = CampusPlacedObject.RotateFootprintSize(footprint, effectiveRotation90);
            if (CampusRuntimeObjectAuthoring.IsStackableFacilityObject(placementType))
            {
                EraseStackableFacilityObjectsAtCells(floor, cell, rotatedFootprint);
            }
            else
            {
                EraseObjectsAtCells(floor, cell, rotatedFootprint);
            }

            GameObject instance = Instantiate(prefab, floor.PropsRoot);
            CampusSceneInstanceUtility.NormalizeSceneInstance(instance);
            instance.SetActive(true);
            instance.name = displayName + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y;
            instance.transform.rotation = Quaternion.identity;
            CampusPlacedObject placed = instance.GetComponent<CampusPlacedObject>();
            if (placed == null)
            {
                placed = instance.AddComponent<CampusPlacedObject>();
            }

            placed.ObjectId = prefabPlaced != null && !string.IsNullOrWhiteSpace(prefabPlaced.ObjectId)
                ? prefabPlaced.ObjectId.Trim()
                : prefab.name;
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
                placed.InteractionPresetEid =
                    CampusRuntimeObjectAuthoring.NormalizeInteractionPresetEid(prefabPlaced.InteractionPresetEid);
                placed.TypeId = prefabPlaced.TypeId;
                placed.StorageSize = prefabPlaced.NormalizedStorageSize;
                placed.StorageMaxWeight = prefabPlaced.NormalizedStorageMaxWeight;
                placed.SortingOrderOffset = prefabPlaced.SortingOrderOffset;
                if (prefabPlaced.IsWallMounted)
                {
                    placed.SortingOrderOffset = Mathf.Max(placed.SortingOrderOffset, 1);
                }
            }

            CampusRuntimeObjectAuthoring.NormalizeStackableFacilityObject(placed, placementType);
            placed.ApplyCellToTransform(floor.Grid);
            placed.ApplyInteractionState();
            RefreshPlacedRetailShelf(placed);
            placed.EnsureShadowRegistration();
            CampusDynamicShadowUtility.EnsureObjectShadowCasters(placed, floor.Grid);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            NtingCustomShadowSystem.EnsureSceneSystem().RefreshNow();
            floor.MarkUsedBoundsDirty();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u7269\u4ef6\uff1a{0}", "Placed object: {0}", displayName));
        }

        private static void RefreshPlacedRetailShelf(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            CampusRetailShelf retailShelf = placed.GetComponent<CampusRetailShelf>();
            if (retailShelf != null)
            {
                retailShelf.RefreshAfterPlacement();
            }
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
            InvalidateRoomRegionCountCache();
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

            PlaceGameplayFacilityMarker(floor, cell, preset);
        }

        private void PlaceGameplayActor(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            CampusRuntimeGameplayActorPreset preset = GetSelectedGameplayActorPreset();
            if (preset == null)
            {
                SetStatus(Tr("\u8bf7\u5148\u9009\u62e9 NPC \u9884\u8bbe\u3002", "Select an NPC preset first."));
                return;
            }

            EnsureCachedGameplayActorsForEditing();
            Vector3Int normalizedCell = NormalizeCell(cell);
            EraseGameplayActorsAtCell(floor, normalizedCell, false);

            CampusRuntimeGameplayActorDraft draft = new CampusRuntimeGameplayActorDraft(
                newGameplayActorId,
                newGameplayActorChineseName,
                newGameplayActorEnglishName,
                newGameplayActorClassId);
            CampusRuntimeGameplayActorSnapshot actor =
                CampusRuntimeGameplayActorAuthoring.CreateActor(
                    preset,
                    draft,
                    floor.FloorIndex,
                    normalizedCell,
                    cachedGameplayActors);
            cachedGameplayActors.Add(actor);
            CampusRuntimeGameplayActorAuthoring.SpawnActorForEditing(actor, EnsureFloor);
            CampusRuntimeGameplayActorAuthoring.RebuildRosterFromScene();
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e NPC\uff1a{0}", "Placed NPC: {0}", actor.LocalizedDisplayName.Get(displayLanguage, actor.DisplayName, actor.Id)));
        }

        private bool EraseGameplayActorsAtCell(CampusFloorRoot floor, Vector3Int cell, bool showStatus)
        {
            if (floor == null)
            {
                return false;
            }

            EnsureCachedGameplayActorsForEditing();
            bool erased = CampusRuntimeGameplayActorAuthoring.EraseActorsAtCell(
                cachedGameplayActors,
                floor,
                cell,
                DestroyRuntimeObject);

            if (erased)
            {
                CampusRuntimeGameplayActorAuthoring.RebuildRosterFromScene();
                SchedulePlayerMapSave();
                if (showStatus)
                {
                    SetStatus(Tr("\u5df2\u5220\u9664 NPC\u3002", "Erased NPC."));
                }
            }
            else if (showStatus)
            {
                SetStatus(Tr("\u5f53\u524d\u683c\u5b50\u6ca1\u6709 NPC\u3002", "No NPC at the current cell."));
            }

            return erased;
        }

        private void EnsureCachedGameplayActorsForEditing()
        {
            if (gameplayActorCacheInitialized)
            {
                return;
            }

            gameplayActorCacheInitialized = true;
            string overlayPath;
            CampusRuntimeGameplayOverlaySnapshot existing;
            string readError;
            bool loaded = CampusRuntimeGameplayOverlayWorkflow.TryLoadExistingSnapshot(
                ResolveCurrentWritableMapPath(),
                out overlayPath,
                out existing,
                out readError);
            if (!loaded && !string.IsNullOrWhiteSpace(readError))
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.ReadGameplayOverlayFailed,
                    readError);
            }

            if (loaded && existing.Actors != null && existing.Actors.Count > 0)
            {
                CacheGameplayActors(existing.Actors);
                return;
            }

            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            CampusRuntimeGameplayActorAuthoring.CaptureSceneActors(cachedGameplayActors, mapRoot);
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
            if (!CampusRuntimeGameplayOverlayAuthoring.CreateFacilityMarker(
                    floor,
                    cell,
                    GetGameplayPresetDisplayName(preset),
                    preset.FacilityType))
            {
                return;
            }

            RebuildGameplayRoomRegistrySafe();
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u8bbe\u65bd\u70b9\uff1a{0}", "Placed facility point: {0}", GetGameplayPresetLabel(preset)));
        }

        private bool EraseGameplayMarkersAtCell(
            CampusFloorRoot floor,
            Vector3Int cell,
            bool eraseRooms,
            bool eraseFacilities,
            bool reservedEraseFlag,
            bool showStatus)
        {
            if (floor == null)
            {
                return false;
            }

            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            bool erased = CampusRuntimeGameplayOverlayAuthoring.EraseMarkersAtCell(
                floor,
                cell,
                eraseRooms,
                eraseFacilities,
                reservedEraseFlag,
                mapRoot,
                DestroyRuntimeObject);

            if (erased)
            {
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

            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            bool erased = CampusRuntimeGameplayOverlayAuthoring.EraseMarkersInBounds(
                floor,
                bounds,
                mapRoot,
                DestroyRuntimeObject);

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

            BoundsInt bounds = CampusRuntimeRoomPrefabAuthoring.BuildInclusiveCellBounds(start, end);
            CampusRuntimeRoomPrefab roomPrefab = CaptureRoomPrefab(floor, bounds, roomName.Trim());
            if (!CampusRuntimeRoomPrefabLibrary.HasContent(roomPrefab))
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
            roomPrefab.Schema = CampusRuntimeRoomPrefabLibrary.Schema;
            roomPrefab.RoomName = roomName;
            roomPrefab.CreatedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            roomPrefab.Size = new Vector2Int(Mathf.Max(1, bounds.size.x), Mathf.Max(1, bounds.size.y));
            Vector3Int originCell = new Vector3Int(bounds.xMin, bounds.yMin, 0);

            CaptureRoomTiles(floor.FloorTilemap, floorTiles, bounds, originCell, roomPrefab.FloorTiles);
            CaptureRoomTiles(CampusWallTileUtility.GetWallLogicTilemap(floor), wallTiles, bounds, originCell, roomPrefab.WallTiles);
            CaptureRoomObjects(floor, bounds, originCell, roomPrefab.Objects);
            CaptureRoomMarkers(floor, bounds, originCell, roomPrefab.RoomMarkers);
            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            CampusRuntimeGameplayOverlayAuthoring.CaptureRoomPrefabRooms(
                floor,
                bounds,
                originCell,
                roomPrefab.GameplayRooms);
            CampusRuntimeGameplayOverlayAuthoring.CaptureRoomPrefabFacilities(
                floor,
                bounds,
                originCell,
                roomPrefab.GameplayFacilities);
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
                    tileSnapshot.Cell = CampusRuntimeRoomPrefabAuthoring.ToRelativeCell(cell, originCell);
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
                objectSnapshot.Cell =
                    CampusRuntimeRoomPrefabAuthoring.ToRelativeCell(objectSnapshot.Cell, originCell);
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
                if (marker == null ||
                    marker.FloorIndex != floor.FloorIndex ||
                    !CampusRuntimeRoomPrefabAuthoring.CellInBounds(bounds, marker.Cell))
                {
                    continue;
                }

                if (!CampusRuntimeAreaPresetCatalog.TryResolveRoomName(marker.RoomName, out string roomName))
                {
                    continue;
                }

                CampusRuntimeRoomSnapshot roomSnapshot = new CampusRuntimeRoomSnapshot();
                roomSnapshot.RoomName = roomName;
                roomSnapshot.FloorIndex = 0;
                roomSnapshot.Cell = CampusRuntimeRoomPrefabAuthoring.ToRelativeCell(marker.Cell, originCell);
                output.Add(roomSnapshot);
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
                if (lightSnapshot.FloorIndex != floor.FloorIndex ||
                    !CampusRuntimeRoomPrefabAuthoring.CellInBounds(bounds, lightSnapshot.Cell))
                {
                    continue;
                }

                CampusRuntimeRoomLightSnapshot roomLight = new CampusRuntimeRoomLightSnapshot();
                roomLight.Light = lightSnapshot;
                roomLight.Light.FloorIndex = 0;
                roomLight.Light.Cell =
                    CampusRuntimeRoomPrefabAuthoring.ToRelativeCell(lightSnapshot.Cell, originCell);
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

            CampusRuntimeRoomPrefabAuthoring.Place(
                roomPrefab,
                floor,
                anchorCell,
                floorTiles,
                wallTiles,
                BuildRoomPrefabPlacementContext());
            SchedulePlayerMapSave();
            SetStatus(TrFormat("\u5df2\u653e\u7f6e\u623f\u95f4\u6a21\u5757\uff1a{0}", "Placed room module: {0}", roomPrefab.RoomName));
        }

        private CampusRuntimeRoomPrefabPlacementContext BuildRoomPrefabPlacementContext()
        {
            return new CampusRuntimeRoomPrefabPlacementContext
            {
                EraseArea = EraseRoomPrefabArea,
                ApplyTiles = ApplyRoomPrefabTiles,
                ApplyObjects = ApplyRoomPrefabObjects,
                ApplyMarkers = ApplyRoomPrefabMarkers,
                ApplyGameplayMarkers = ApplyRoomPrefabGameplayMarkers,
                ApplyLights = ApplyRoomPrefabLights,
                AddRoomDefinitions = AddRoomDefinitionsFromRoomPrefab,
                FinishPlacement = FinishRoomPrefabPlacement
            };
        }

        private void FinishRoomPrefabPlacement(CampusFloorRoot floor)
        {
            RebuildWallVisuals(floor);
            floor.MarkUsedBoundsDirty();
            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
        }

        private void EraseRoomPrefabArea(CampusFloorRoot floor, Vector3Int anchorCell, Vector2Int size)
        {
            size = CampusRuntimeRoomPrefabLibrary.NormalizeSize(size);
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

                Vector3Int cell = CampusRuntimeRoomPrefabAuthoring.ToAbsoluteCell(anchorCell, tileSnapshot.Cell);
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
                shifted.Cell = CampusRuntimeRoomPrefabAuthoring.ToAbsoluteCell(anchorCell, objects[i].Cell);
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
                    if (!CampusRuntimeAreaPresetCatalog.TryResolveRoomName(sourceRoomName, out string roomName))
                    {
                        continue;
                    }

                    CampusRuntimeRoomSnapshot shifted = new CampusRuntimeRoomSnapshot();
                    shifted.RoomName = roomName;
                    shifted.FloorIndex = floor != null ? floor.FloorIndex : 1;
                    shifted.Cell = CampusRuntimeRoomPrefabAuthoring.ToAbsoluteCell(anchorCell, marker.Cell);
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
                        AnchorCell = CampusRuntimeRoomPrefabAuthoring.ToAbsoluteCell(anchorCell, source.AnchorCell),
                        Size = source.Size,
                        UsableForGameplay = source.UsableForGameplay
                    });
                }

                CampusRuntimeGameplayOverlayAuthoring.SpawnRooms(shiftedRooms, EnsureFloor);
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

                    Vector3Int absoluteCell =
                        CampusRuntimeRoomPrefabAuthoring.ToAbsoluteCell(anchorCell, source.Cell);
                    string shiftedFacilityId = CampusGameplayFacilityMarker.BuildStableFacilityId(
                        floor.FloorIndex,
                        source.FacilityType,
                        absoluteCell);
                    shiftedFacilities.Add(new CampusRuntimeGameplayFacilitySnapshot
                    {
                        Id = shiftedFacilityId,
                        DisplayName = source.DisplayName,
                        FacilityType = source.FacilityType,
                        FloorIndex = floor.FloorIndex,
                        Cell = absoluteCell,
                        CountsAsCoreFacility = source.CountsAsCoreFacility
                    });
                }

                CampusRuntimeGameplayOverlayAuthoring.SpawnFacilities(shiftedFacilities, EnsureFloor);
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

                shifted.Cell = CampusRuntimeRoomPrefabAuthoring.ToAbsoluteCell(anchorCell, relativeCell);
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
            InvalidateEditableLightCache();
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

        private void EraseStackableFacilityObjectsAtCells(CampusFloorRoot floor, Vector3Int anchorCell, Vector2Int size)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            HashSet<CampusPlacedObject> targets = new HashSet<CampusPlacedObject>();
            CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    Vector3Int cell = new Vector3Int(anchorCell.x + x, anchorCell.y + y, anchorCell.z);
                    for (int i = 0; i < objects.Length; i++)
                    {
                        CampusPlacedObject placed = objects[i];
                        if (placed != null &&
                            placed.ContainsCell(cell) &&
                            CampusRuntimeObjectAuthoring.IsStackableFacilityObject(
                                CampusRuntimeObjectAuthoring.ResolveFacilityType(placed)))
                        {
                            targets.Add(placed);
                        }
                    }
                }
            }

            foreach (CampusPlacedObject placed in targets)
            {
                if (placed != null)
                {
                    DestroyRuntimeObject(placed.gameObject);
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

            if (erased)
            {
                InvalidateRoomRegionCountCache();
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
                    InvalidateEditableLightCache();
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
            CampusRuntimeMapEditorHistoryResult result = CampusRuntimeMapEditorHistoryCommandService.Undo(
                undoSnapshots,
                redoSnapshots,
                entry => TryApplyWallStrokeUndoEntry(entry, false),
                BuildCurrentHistorySnapshotJson,
                LoadSnapshotJson);
            if (result.Outcome != CampusRuntimeMapEditorHistoryOutcome.Applied)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u64a4\u9500\u7684\u64cd\u4f5c\u3002", "No undo operation is available."));
                return;
            }

            SchedulePlayerMapSave();
            SetStatus(Tr("\u64a4\u9500\u5b8c\u6210\u3002", "Undo complete."));
        }

        private void RedoSnapshot()
        {
            CampusRuntimeMapEditorHistoryResult result = CampusRuntimeMapEditorHistoryCommandService.Redo(
                undoSnapshots,
                redoSnapshots,
                entry => TryApplyWallStrokeUndoEntry(entry, true),
                BuildCurrentHistorySnapshotJson,
                LoadSnapshotJson);
            if (result.Outcome != CampusRuntimeMapEditorHistoryOutcome.Applied)
            {
                SetStatus(Tr("\u6ca1\u6709\u53ef\u91cd\u505a\u7684\u64cd\u4f5c\u3002", "No redo operation is available."));
                return;
            }

            SchedulePlayerMapSave();
            SetStatus(Tr("\u91cd\u505a\u5b8c\u6210\u3002", "Redo complete."));
        }

        private string BuildCurrentHistorySnapshotJson()
        {
            return BuildSnapshotJson(false, sceneReferencesDirty || mapRoot == null);
        }

        private string BuildSnapshotJson(bool prettyPrint = true, bool refreshReferences = true)
        {
            using (CampusWallBuildProfiler.BuildSnapshotJson.Auto())
            {
                return CampusRuntimeMapSnapshotStore.ToJson(BuildSnapshot(refreshReferences), prettyPrint);
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
            snapshot.Schema = CampusRuntimeMapSnapshotStore.Schema;
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

            CampusRuntimeMapSnapshot snapshot = CampusRuntimeMapSnapshotStore.FromJson(json);
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
            CampusRuntimeMapEditorPersistenceResult result =
                CampusRuntimeMapEditorPersistenceCommandService.ExportSnapshotJson(
                    BuildSnapshot(),
                    (path, showStatus) => { SaveGameplayOverlayForMapPath(path, showStatus); });
            if (!result.Succeeded)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.ExportMapJsonFailed,
                    result.ErrorMessage);
                SetStatus(TrFormat("\u5bfc\u51fa\u5730\u56fe JSON \u5931\u8d25\uff1a{0}", "Export map JSON failed: {0}", result.ErrorMessage));
                return;
            }

            SetStatus(TrFormat("\u5df2\u5bfc\u51fa\u5730\u56fe JSON\uff1a{0}", "Exported map JSON: {0}", result.Path));
            CampusRuntimeMapEditorLogTextCatalog.Log(
                CampusRuntimeMapEditorLogTextId.ExportedMap,
                result.Path);
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
            CampusRuntimeGameplayOverlayAuthoring.ClearSceneMarkers(DestroyRuntimeObject);
            cachedGameplayActors.Clear();
            gameplayActorCacheInitialized = false;
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

        private CampusRuntimeMapEditorPersistenceResult SavePlayerMap(bool showStatus)
        {
            if (playerSaveInProgress)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Busy,
                    string.Empty,
                    GetPlayerSaveRootFolder(),
                    string.Empty);
            }

            try
            {
                playerSaveInProgress = true;
                CampusRuntimeMapEditorPersistenceResult result =
                CampusRuntimeMapEditorPersistenceCommandService.SavePlayerMap(
                    BuildSnapshot(),
                    EnsureImportFolders,
                    (path, saveOverlayStatus) => { SaveGameplayOverlayForMapPath(path, saveOverlayStatus); });
                if (result.Succeeded)
                {
                    playerSavePending = false;
                    if (showStatus)
                    {
                        SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u73a9\u5bb6\u5730\u56fe\uff1a{0}", "Saved player map: {0}", result.RootPath));
                    }

                    CampusRuntimeMapEditorLogTextCatalog.Log(
                        CampusRuntimeMapEditorLogTextId.SavedPlayerMap,
                        result.RootPath);
                }
                else if (showStatus)
                {
                    CampusRuntimeMapEditorLogTextCatalog.Warning(
                        CampusRuntimeMapEditorLogTextId.SavePlayerMapFailed,
                        result.ErrorMessage);
                    SetStatus(TrFormat("\u4fdd\u5b58\u73a9\u5bb6\u5730\u56fe\u5931\u8d25\uff1a{0}", "Save player map failed: {0}", result.ErrorMessage));
                }

                return result;
            }
            finally
            {
                playerSaveInProgress = false;
            }
        }

        private bool SaveCurrentMapSource(bool showStatus)
        {
            CampusRuntimeMapEditorPersistenceResult result =
                CampusRuntimeMapEditorPersistenceCommandService.SaveCurrentMapSource(
                    lastMapLoadSource,
                    lastMapLoadPath,
                    () => SavePlayerMap(showStatus),
                    (path, source) => SaveMapToPathResult(path, source, showStatus));
            switch (result.Outcome)
            {
                case CampusRuntimeMapEditorPersistenceOutcome.Succeeded:
                    return true;
                default:
                    return false;
            }
        }

        private bool SaveMapToPath(string path, CampusRuntimeMapLoadSource source, bool showStatus)
        {
            return SaveMapToPathResult(path, source, showStatus).Succeeded;
        }

        private CampusRuntimeMapEditorPersistenceResult SaveMapToPathResult(
            string path,
            CampusRuntimeMapLoadSource source,
            bool showStatus)
        {
            CampusRuntimeMapEditorPersistenceResult result =
                CampusRuntimeMapEditorPersistenceCommandService.SaveMapToPath(
                    path,
                    source,
                    BuildSnapshot(),
                    EnsureImportFolders,
                    (mapPath, saveOverlayStatus) => { SaveGameplayOverlayForMapPath(mapPath, saveOverlayStatus); },
                    RememberMapLoadSource,
                    RefreshAssetDatabaseIfAvailable);
            if (showStatus)
            {
                if (result.Succeeded)
                {
                    SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u5730\u56fe\uff1a{0}", "Saved map: {0}", result.Path));
                }
                else
                {
                    CampusRuntimeMapEditorLogTextCatalog.Warning(
                        CampusRuntimeMapEditorLogTextId.SaveMapFailed,
                        result.ErrorMessage);
                    SetStatus(TrFormat("\u4fdd\u5b58\u5730\u56fe\u5931\u8d25\uff1a{0}", "Save map failed: {0}", result.ErrorMessage));
                }
            }

            return result;
        }

        private void LoadPlayerMap()
        {
            LoadPlayerMap(true, true);
        }

        private void LoadPlayerMap(bool recordUndo, bool showStatus)
        {
            string savePath = GetPlayerSaveMapPath();
            bool previousSuppress = suppressPlayerSaveScheduling;
            try
            {
                suppressPlayerSaveScheduling = true;
                CampusRuntimeMapEditorPersistenceResult result =
                    CampusRuntimeMapEditorPersistenceCommandService.LoadPlayerMap(
                        savePath,
                        recordUndo,
                        RecordUndo,
                        EnsureImportFolders,
                        LoadRuntimeResources,
                        ApplySnapshot,
                        RememberMapLoadSource,
                        ApplyGameplayOverlayFromPath);
                switch (result.Outcome)
                {
                    case CampusRuntimeMapEditorPersistenceOutcome.Succeeded:
                        playerSavePending = false;
                        if (showStatus)
                        {
                            SetStatus(TrFormat("\u5df2\u8bfb\u53d6\u73a9\u5bb6\u5730\u56fe\uff1a{0}", "Loaded player map: {0}", result.Path));
                        }

                        CampusRuntimeMapEditorLogTextCatalog.Log(
                            CampusRuntimeMapEditorLogTextId.LoadedPlayerMap,
                            result.Path);
                        break;
                    case CampusRuntimeMapEditorPersistenceOutcome.MissingPlayerSave:
                        if (showStatus)
                        {
                            SetStatus(TrFormat("\u6ca1\u6709\u627e\u5230\u73a9\u5bb6\u5730\u56fe\u5b58\u6863\uff1a{0}", "Player map save was not found: {0}", savePath));
                        }

                        break;
                    default:
                        CampusRuntimeMapEditorLogTextCatalog.Warning(
                            CampusRuntimeMapEditorLogTextId.LoadPlayerMapFailed,
                            result.ErrorMessage);
                        if (showStatus)
                        {
                            SetStatus(TrFormat("\u8bfb\u53d6\u73a9\u5bb6\u5730\u56fe\u5931\u8d25\uff1a{0}", "Load player map failed: {0}", result.ErrorMessage));
                        }

                        break;
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
                CampusRuntimeMapEditorPersistenceResult result =
                    CampusRuntimeMapEditorPersistenceCommandService.ExportAuthoringPackage(
                        () => SavePlayerMap(false),
                        BuildSnapshot(),
                        GetImportRootFolder(),
                        EnsureImportFolders,
                        (path, saveOverlayStatus) => { SaveGameplayOverlayForMapPath(path, saveOverlayStatus); },
                        RefreshAssetDatabaseIfAvailable);
                if (result.Succeeded)
                {
                    if (showStatus)
                    {
                        SetStatus(TrFormat("\u5df2\u5bfc\u51fa\u5f00\u53d1\u671f\u5730\u56fe\u5305\uff1a{0}", "Exported authoring map package: {0}", result.RootPath));
                    }

                    CampusRuntimeMapEditorLogTextCatalog.Log(
                        CampusRuntimeMapEditorLogTextId.ExportedAuthoringPackage,
                        result.RootPath);
                }
                else
                {
                    CampusRuntimeMapEditorLogTextCatalog.Warning(
                        CampusRuntimeMapEditorLogTextId.ExportAuthoringPackageFailed,
                        result.ErrorMessage);
                    if (showStatus)
                    {
                        SetStatus(TrFormat("\u5bfc\u51fa\u5f00\u53d1\u671f\u5730\u56fe\u5305\u5931\u8d25\uff1a{0}", "Export authoring map package failed: {0}", result.ErrorMessage));
                    }
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
            bool previousSuppress = suppressPlayerSaveScheduling;
            try
            {
                suppressPlayerSaveScheduling = true;
                CampusRuntimeMapEditorPersistenceResult result =
                    CampusRuntimeMapEditorPersistenceCommandService.RestoreAuthoringPackage(
                        packageRoot,
                        packageImportFolder,
                        packageMapPath,
                        GetImportRootFolder(),
                        recordUndo,
                        RecordUndo,
                        LoadRuntimeResources,
                        LoadSnapshotJson,
                        RememberMapLoadSource,
                        ApplyGameplayOverlayFromPath);
                switch (result.Outcome)
                {
                    case CampusRuntimeMapEditorPersistenceOutcome.Succeeded:
                        playerSavePending = false;
                        if (showStatus)
                        {
                            SetStatus(Tr("\u5df2\u4ece\u5f00\u53d1\u671f\u5730\u56fe\u5305\u6062\u590d\u5730\u56fe\u3002", "Restored map from authoring package."));
                        }

                        CampusRuntimeMapEditorLogTextCatalog.Log(
                            CampusRuntimeMapEditorLogTextId.RestoredAuthoringPackage,
                            packageRoot);
                        break;
                    case CampusRuntimeMapEditorPersistenceOutcome.MissingAuthoringPackage:
                        if (showStatus)
                        {
                            SetStatus(TrFormat("\u6ca1\u6709\u627e\u5230\u5f00\u53d1\u671f\u5730\u56fe\u5305\uff1a{0}", "Authoring map package was not found: {0}", packageRoot));
                        }

                        break;
                    default:
                        CampusRuntimeMapEditorLogTextCatalog.Warning(
                            CampusRuntimeMapEditorLogTextId.RestoreAuthoringPackageFailed,
                            result.ErrorMessage);
                        if (showStatus)
                        {
                            SetStatus(TrFormat("\u6062\u590d\u5f00\u53d1\u671f\u5730\u56fe\u5305\u5931\u8d25\uff1a{0}", "Restore authoring map package failed: {0}", result.ErrorMessage));
                        }

                        break;
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
            CampusRuntimeMapEditorPersistenceResult result =
                CampusRuntimeMapEditorPersistenceCommandService.ImportLatestJson(
                    folder,
                    true,
                    RecordUndo,
                    LoadSnapshotJson,
                    ApplyGameplayOverlayFromPath);
            switch (result.Outcome)
            {
                case CampusRuntimeMapEditorPersistenceOutcome.Succeeded:
                    SetStatus(TrFormat("\u5df2\u5bfc\u5165\u5730\u56fe JSON\uff1a{0}", "Imported map JSON: {0}", result.Path));
                    break;
                case CampusRuntimeMapEditorPersistenceOutcome.MissingExportFolder:
                    SetStatus(TrFormat("\u672a\u627e\u5230\u5bfc\u51fa\u6587\u4ef6\u5939\uff1a{0}", "Export folder not found: {0}", folder));
                    break;
                case CampusRuntimeMapEditorPersistenceOutcome.MissingExportJson:
                    SetStatus(Tr("\u6ca1\u6709\u53ef\u5bfc\u5165\u7684\u5730\u56fe JSON\u3002", "No map JSON is available to import."));
                    break;
                default:
                    CampusRuntimeMapEditorLogTextCatalog.Warning(
                        CampusRuntimeMapEditorLogTextId.ImportLatestJsonFailed,
                        result.ErrorMessage);
                    SetStatus(TrFormat("\u5bfc\u5165\u5730\u56fe JSON \u5931\u8d25\uff1a{0}", "Import map JSON failed: {0}", result.ErrorMessage));
                    break;
            }
        }

        private string GetExportFolder()
        {
            return CampusRuntimeMapSnapshotStore.GetExportFolder();
        }

        private string GetPlayerSaveRootFolder()
        {
            return CampusRuntimeMapSnapshotStore.GetPlayerSaveRootFolder();
        }

        private string GetPlayerSaveMapPath()
        {
            return CampusRuntimeMapSnapshotStore.GetPlayerSaveMapPath();
        }

        private string GetPlayerSaveManifestPath()
        {
            return CampusRuntimeMapSnapshotStore.GetPlayerSaveManifestPath();
        }

        private string GetAuthoringPackageRootFolder()
        {
            return CampusRuntimeMapSnapshotStore.GetAuthoringPackageRootFolder();
        }

        private string GetAuthoringPackageImportFolder()
        {
            return CampusRuntimeMapSnapshotStore.GetAuthoringPackageImportFolder();
        }

        private string GetAuthoringPackageMapPath()
        {
            return CampusRuntimeMapSnapshotStore.GetAuthoringPackageMapPath();
        }

        private string GetAuthoringPackageManifestPath()
        {
            return CampusRuntimeMapSnapshotStore.GetAuthoringPackageManifestPath();
        }

        private bool SaveGameplayOverlayForMapPath(string mapPath, bool showStatus)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
            {
                return false;
            }

            try
            {
                string overlayPath = CampusRuntimeGameplayOverlayStore.GetPathForMapPath(mapPath);
                CampusRuntimeGameplayOverlaySnapshot snapshot = BuildGameplayOverlaySnapshot(mapPath);
                CampusRuntimeGameplayOverlayStore.WriteSnapshot(mapPath, snapshot);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u4fdd\u5b58\u73a9\u6cd5\u5c42\uff1a{0}", "Saved gameplay overlay: {0}", overlayPath));
                }

                return true;
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.SaveGameplayOverlayFailed,
                    exception.Message);
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
            CampusRuntimeGameplayOverlayAuthoring.ClearSceneMarkers(DestroyRuntimeObject);
            cachedGameplayActors.Clear();
            gameplayActorCacheInitialized = false;

            string overlayPath;
            CampusRuntimeGameplayOverlaySnapshot snapshot;
            string readError;
            if (!CampusRuntimeGameplayOverlayWorkflow.TryLoadExistingSnapshot(
                    mapPath,
                    out overlayPath,
                    out snapshot,
                    out readError))
            {
                if (!string.IsNullOrWhiteSpace(readError))
                {
                    CampusRuntimeMapEditorLogTextCatalog.Warning(
                        CampusRuntimeMapEditorLogTextId.LoadGameplayOverlayFailed,
                        readError);
                    if (showStatus)
                    {
                        SetStatus(TrFormat("\u8bfb\u53d6\u73a9\u6cd5\u5c42\u5931\u8d25\uff1a{0}", "Load gameplay overlay failed: {0}", readError));
                    }

                    return;
                }

                RebuildGameplayRoomRegistrySafe();
                if (showStatus)
                {
                    SetStatus(Tr("\u8be5\u5730\u56fe\u6ca1\u6709\u627e\u5230\u73a9\u6cd5\u5c42\u6587\u4ef6\u3002", "No gameplay overlay found for this map."));
                }

                return;
            }

            try
            {
                CacheGameplayActors(snapshot.Actors);
                CampusRuntimeGameplayOverlayAuthoring.SpawnSceneMarkers(snapshot, EnsureFloor);
                RebuildGameplayRoomRegistrySafe();
                if (showStatus)
                {
                    SetStatus(TrFormat("\u5df2\u8bfb\u53d6\u73a9\u6cd5\u5c42\uff1a{0}", "Loaded gameplay overlay: {0}", overlayPath));
                }
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.LoadGameplayOverlayFailed,
                    exception.Message);
                if (showStatus)
                {
                    SetStatus(TrFormat("\u8bfb\u53d6\u73a9\u6cd5\u5c42\u5931\u8d25\uff1a{0}", "Load gameplay overlay failed: {0}", exception.Message));
                }
            }
        }

        private CampusRuntimeGameplayOverlaySnapshot BuildGameplayOverlaySnapshot(string targetMapPath)
        {
            RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
            List<CampusRuntimeGameplayActorSnapshot> actors = ResolveGameplayActorsForSave(targetMapPath);
            return CampusRuntimeGameplayOverlayWorkflow.BuildSnapshot(
                mapRoot != null ? mapRoot.gameObject.name : "CampusMap",
                actors,
                snapshot => CampusRuntimeGameplayOverlayAuthoring.CaptureSceneMarkers(snapshot, mapRoot));
        }

        private List<CampusRuntimeGameplayActorSnapshot> ResolveGameplayActorsForSave(string targetMapPath)
        {
            return CampusRuntimeGameplayOverlayWorkflow.ResolveActorsForSave(
                targetMapPath,
                gameplayActorCacheInitialized,
                cachedGameplayActors,
                actors =>
                {
                    RefreshSceneReferencesIfNeeded(sceneReferencesDirty || mapRoot == null);
                    CampusRuntimeGameplayActorAuthoring.CaptureSceneActors(actors, mapRoot);
                },
                error => CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.ReadGameplayOverlayFailed,
                    error));
        }

        private void CacheGameplayActors(List<CampusRuntimeGameplayActorSnapshot> actors)
        {
            gameplayActorCacheInitialized = true;
            CampusRuntimeGameplayOverlayWorkflow.ReplaceActorCache(cachedGameplayActors, actors);
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
            if (CampusRuntimeImportLibrary.AreSamePath(persistentImportRoot, projectImportRoot) ||
                !Directory.Exists(persistentImportRoot) ||
                CampusRuntimeImportLibrary.HasImportContent(projectImportRoot) ||
                !CampusRuntimeImportLibrary.HasImportContent(persistentImportRoot))
            {
                return;
            }

            CampusRuntimeImportLibrary.MirrorDirectory(persistentImportRoot, projectImportRoot, false);
            RefreshAssetDatabaseIfAvailable();
            CampusRuntimeMapEditorLogTextCatalog.Log(
                CampusRuntimeMapEditorLogTextId.MigratedRuntimeImportLibrary,
                projectImportRoot);
#endif
        }

        private void RefreshImportAssetDatabaseIfProjectBacked()
        {
#if UNITY_EDITOR
            if (CampusRuntimeImportLibrary.AreSamePath(GetImportRootFolder(), GetAuthoringPackageImportFolder()))
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

            objectSnapshot.ObjectId = objectDefinitionCatalog.NormalizeObjectId(
                string.IsNullOrEmpty(placed.ObjectId) ? placed.gameObject.name : placed.ObjectId);
            objectSnapshot.DisplayNameOverride = placed.DisplayNameOverride;
            objectSnapshot.TypeId = CampusRuntimeObjectAuthoring.ResolveTypeIdForPlacedObject(placed);
            if (!string.IsNullOrEmpty(objectSnapshot.TypeId) && string.IsNullOrWhiteSpace(placed.TypeId))
            {
                placed.TypeId = objectSnapshot.TypeId;
            }

            CampusRuntimeObjectAuthoring.NormalizeStackableFacilityObject(
                placed,
                CampusRuntimeObjectAuthoring.ResolveFacilityType(placed));
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
            objectSnapshot.InteractionPresetEid =
                CampusRuntimeObjectAuthoring.NormalizeInteractionPresetEid(placed.InteractionPresetEid);
            objectSnapshot.StorageSize = placed.NormalizedStorageSize;
            objectSnapshot.StorageMaxWeight = placed.NormalizedStorageMaxWeight;
            objectSnapshot.UseCustomInteractionAnchor = placed.UseCustomInteractionAnchor;
            objectSnapshot.CustomInteractionAnchorLocalPosition = placed.CustomInteractionAnchorLocalPosition;
            objectSnapshot.CustomInteractionAnchorRadius = placed.CustomInteractionAnchorRadius;
            objectSnapshot.CustomInteractionPromptText = placed.CustomInteractionPromptText;
            objectSnapshot.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(placed.CustomInteractionAnchors);
            objectSnapshot.RetailShelf = CampusRuntimeObjectAuthoring.CaptureRetailShelfData(placed.gameObject);
            objectSnapshot.ProtectedStockContainer =
                CampusRuntimeObjectAuthoring.CaptureProtectedStockContainerData(placed.gameObject);
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
            clone.InteractionPresetEid =
                CampusRuntimeObjectAuthoring.NormalizeInteractionPresetEid(source.InteractionPresetEid);
            clone.StorageSize = source.StorageSize;
            clone.StorageMaxWeight = source.StorageMaxWeight;
            clone.UseCustomInteractionAnchor = source.UseCustomInteractionAnchor;
            clone.CustomInteractionAnchorLocalPosition = source.CustomInteractionAnchorLocalPosition;
            clone.CustomInteractionAnchorRadius = source.CustomInteractionAnchorRadius;
            clone.CustomInteractionPromptText = source.CustomInteractionPromptText;
            clone.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(source.CustomInteractionAnchors);
            clone.RetailShelf = CampusRuntimeObjectAuthoring.CloneRetailShelfData(source.RetailShelf);
            clone.ProtectedStockContainer =
                CampusRuntimeObjectAuthoring.CloneProtectedStockContainerData(source.ProtectedStockContainer);
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
                objectSnapshot.ObjectId = string.IsNullOrWhiteSpace(objectSnapshot.ObjectId)
                    ? objectDefinitionCatalog.NormalizeObjectId(prefab.name)
                    : objectDefinitionCatalog.NormalizeObjectId(objectSnapshot.ObjectId);
                string displayName = string.IsNullOrWhiteSpace(objectSnapshot.DisplayNameOverride)
                    ? GetObjectDisplayName(prefab)
                    : objectSnapshot.DisplayNameOverride.Trim();
                displayName = objectDefinitionCatalog.ResolveDisplayNameText(objectSnapshot.ObjectId, displayName);
                objectSnapshot.TypeId =
                    CampusRuntimeObjectAuthoring.ResolveTypeIdForSnapshot(objectSnapshot, prefab, displayName);
                objectSnapshot.TypeId = objectDefinitionCatalog.ResolveTypeId(objectSnapshot.ObjectId, objectSnapshot.TypeId);
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
                placed.ApplyRotationVisualState();
                placed.BlocksMovement = objectSnapshot.BlocksMovement;
                placed.BlocksSight = objectSnapshot.BlocksSight;
                placed.IsInteractable = objectSnapshot.IsInteractable;
                placed.IsStorageContainer = objectSnapshot.IsStorageContainer;
                placed.InteractionPresetEid =
                    CampusRuntimeObjectAuthoring.NormalizeInteractionPresetEid(objectSnapshot.InteractionPresetEid);
                placed.StorageSize = CampusPlacedObject.NormalizeStorageSize(objectSnapshot.StorageSize);
                placed.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(objectSnapshot.StorageMaxWeight);
                placed.UseCustomInteractionAnchor = objectSnapshot.UseCustomInteractionAnchor;
                placed.CustomInteractionAnchorLocalPosition = objectSnapshot.CustomInteractionAnchorLocalPosition;
                placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(objectSnapshot.CustomInteractionAnchorRadius);
                placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(objectSnapshot.CustomInteractionPromptText)
                    ? Tr("\u4ea4\u4e92", "Interact")
                    : objectSnapshot.CustomInteractionPromptText;
                placed.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(objectSnapshot.CustomInteractionAnchors);
                CampusRuntimeObjectAuthoring.ApplyRetailShelfData(instance, placed, objectSnapshot.RetailShelf);
                CampusRuntimeObjectAuthoring.ApplyProtectedStockContainerData(
                    instance,
                    placed,
                    objectSnapshot.ProtectedStockContainer);
                CampusRuntimeObjectAuthoring.NormalizeStackableFacilityObject(
                    placed,
                    CampusRuntimeObjectAuthoring.ResolveFacilityType(placed));
                placed.ApplyInteractionState();
                if (floor.Grid != null)
                {
                    placed.ApplyCellToTransform(floor.Grid);
                }
                else
                {
                    instance.transform.position = objectSnapshot.Position;
                }
                RefreshPlacedRetailShelf(placed);

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

                if (!CampusRuntimeAreaPresetCatalog.TryResolveRoomName(marker.RoomName, out string roomName))
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
                if (roomSnapshot == null || !CampusRuntimeAreaPresetCatalog.TryResolveRoomName(roomSnapshot.RoomName, out string roomName))
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

            InvalidateRoomRegionCountCache();
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

            InvalidateEditableLightCache();
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
            InvalidateEditableLightCache();
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
            InvalidateEditableLightCache();
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

        private void ImportDroppedPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return;
            }

            string targetFolder = GetImportFolderForTarget(activeImportTarget);
            bool requireImage =
                activeImportTarget == CampusRuntimeImportTarget.Floor ||
                activeImportTarget == CampusRuntimeImportTarget.Wall ||
                activeImportTarget == CampusRuntimeImportTarget.Object ||
                activeImportTarget == CampusRuntimeImportTarget.WallFace ||
                activeImportTarget == CampusRuntimeImportTarget.WallCap;
            if (CampusRuntimeImportLibrary.ImportFiles(paths, targetFolder, requireImage) > 0)
            {
                LoadRuntimeResources();
                RefreshImportAssetDatabaseIfProjectBacked();
            }
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

            CampusRuntimeRoomPrefabLibrary.Save(GetRoomPrefabFolder(), roomPrefab);
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

        private void RefreshAssetDatabaseIfAvailable()
        {
#if UNITY_EDITOR
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
#endif
        }

        private void DeleteImportedTileResource(string folder, List<TileBase> tiles, int index, string resourceLabel)
        {
            if (tiles == null || index < 0 || index >= tiles.Count)
            {
                return;
            }

            TileBase tile = tiles[index];
            string assetName = tile != null ? tile.name : string.Empty;
            string path = CampusRuntimeImportLibrary.FindImagePathByName(folder, assetName);
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
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToDeleteResource,
                    resourceLabel,
                    path,
                    exception.Message);
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
            string path = CampusRuntimeImportLibrary.FindImagePathByName(GetObjectImportFolder(), objectId);
            if (string.IsNullOrEmpty(path))
            {
                SetStatus(Tr("\u53ea\u80fd\u5220\u9664\u5bfc\u5165\u7684\u7269\u4ef6\u8d44\u6e90\u3002", "Only imported object resources can be deleted."));
                return;
            }

            try
            {
                File.Delete(path);
                CampusRuntimeObjectSettingsStore.DeleteFolder(GetImportRootFolder(), objectId);

                if (objectSettingsSession.LastSelectedPrefab == prefab)
                {
                    showObjectSettings = false;
                    objectSettingsSession.ClearSelectionIfMatches(prefab);
                }

                LoadRuntimeResources();
                SchedulePlayerMapSave();
                SetStatus(TrFormat("\u5df2\u5220\u9664\u7269\u4ef6\u8d44\u6e90\uff1a{0}", "Deleted object resource: {0}", objectId));
            }
            catch (Exception exception)
            {
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.FailedToDeleteObjectResource,
                    path,
                    exception.Message);
                SetStatus(TrFormat("\u5220\u9664\u5931\u8d25\uff1a{0}", "Delete failed: {0}", exception.Message));
            }

            RefreshImportAssetDatabaseIfProjectBacked();
        }

        private void DeleteSelectedRoomPrefab()
        {
            CampusRuntimeRoomPrefab roomPrefab = GetSelectedRoomPrefab();
            if (roomPrefab == null)
            {
                return;
            }

            CampusRuntimeRoomPrefabLibrary.Delete(roomPrefab, GetRoomPrefabFolder());
            roomPrefabs.Remove(roomPrefab);
            selectedRoomPrefabIndex = roomPrefabs.Count > 0 ? Mathf.Clamp(selectedRoomPrefabIndex, 0, roomPrefabs.Count - 1) : 0;
            RefreshImportAssetDatabaseIfProjectBacked();
            SetStatus(TrFormat("\u5df2\u5220\u9664\u623f\u95f4\u6a21\u5757\uff1a{0}", "Deleted room module: {0}", roomPrefab.RoomName));
        }

        private CampusRetailShelf EnsureRetailShelfForAuthoring(CampusPlacedObject placed)
        {
            if (!ShouldShowRetailShelfSettings(placed))
            {
                return null;
            }

            CampusRetailShelf shelf = placed.GetComponent<CampusRetailShelf>();
            if (shelf == null)
            {
                shelf = placed.gameObject.AddComponent<CampusRetailShelf>();
                shelf.ShelfMode = placed.IsStorageContainer
                    ? CampusRetailShelfMode.Container
                    : CampusRetailShelfMode.DirectPickupDisplay;
            }

            if (string.IsNullOrWhiteSpace(shelf.ShelfId) && !string.IsNullOrWhiteSpace(placed.ObjectId))
            {
                shelf.ShelfId = placed.ObjectId.Trim();
            }

            shelf.ItemDefinitionId = string.IsNullOrWhiteSpace(shelf.ItemDefinitionId) ? string.Empty : shelf.ItemDefinitionId.Trim();
            shelf.StockCount = Mathf.Max(1, shelf.StockCount);
            shelf.DisplaySlotCount = Mathf.Max(1, shelf.DisplaySlotCount);
            return shelf;
        }

        private bool ShouldShowRetailShelfSettings(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return false;
            }

            if (placed.GetComponent<CampusRetailShelf>() != null)
            {
                return true;
            }

            return CampusRuntimeObjectAuthoring.ResolveFacilityType(placed) == CampusFacilityType.GoodsShelf;
        }

        private string ResolveRetailShelfModeLabel(CampusRetailShelfMode shelfMode)
        {
            return shelfMode == CampusRetailShelfMode.DirectPickupDisplay
                ? CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RetailDisplayMode)
                : CampusRuntimeEditorTextCatalog.Get(displayLanguage, CampusRuntimeEditorTextId.RetailContainerMode);
        }

        private void LoadImportedRoomDefinitions()
        {
            ClearRoomDefinitions();
            string path = GetRoomImportFile();
            if (File.Exists(path))
            {
                ImportRoomDefinitionsFromText(File.ReadAllText(path, Encoding.UTF8));
            }
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

        private static void AddUnique<T>(List<T> list, T item) where T : UnityEngine.Object
        {
            if (item != null && !list.Contains(item))
            {
                list.Add(item);
            }
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
            CampusRuntimeMapEditorLogTextCatalog.Log(
                CampusRuntimeMapEditorLogTextId.BakedRuntimeGeneratedContent,
                scenePath);
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
                CampusRuntimeMapEditorLogTextCatalog.Warning(
                    CampusRuntimeMapEditorLogTextId.NativeFileDropFailed,
                    exception.Message);
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
}




