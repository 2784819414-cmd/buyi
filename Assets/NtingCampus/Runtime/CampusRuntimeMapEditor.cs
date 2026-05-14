using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
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

    /// <summary>
    /// Runtime-only room marker used by the packaged internal map editor and export pipeline.
    /// </summary>
    public sealed class CampusRuntimeRoomMarker : MonoBehaviour
    {
        public string RoomName = "未命名房间";
        public int FloorIndex = 1;
        public Vector3Int Cell;
        public bool HideMarkerVisual;
    }

    /// <summary>
    /// Packaged internal map editor. It intentionally avoids UnityEditor APIs so the same tools work in builds.
    /// </summary>
    public sealed class CampusRuntimeMapEditor : MonoBehaviour
    {
        private const string RuntimeResourceFolder = "NtingCampusRuntime";
        private const string RuntimeImportFolder = "CampusRuntimeImports";
        private const string FloorImportFolder = "Floors";
        private const string WallImportFolder = "Walls";
        private const string ObjectImportFolder = "Objects";
        private const string ObjectSettingsFolder = "ObjectSettings";
        private const string RoomImportFile = "Rooms.txt";
        private const string RoomPrefabFolder = "RoomPrefabs";
        private const string ProjectSyncFolder = "UserGeneratedRuntimeContent";
        private const string ProjectSyncMapFile = "CampusMap_ProjectSync.json";
        private const string ProjectSyncManifestFile = "sync_manifest.json";
        private const int MaxUndoSnapshots = 64;
        private const float AmbientLightIntensity = 0.3f;
        private const float PlacedLightIntensity = 1.15f;
        private const int PaletteTileSize = 72;
        private const int ToolbarButtonWidth = 82;
        private const float ZoomStep = 0.12f;
        private const float PanelMargin = 20f;
        private const float TopMargin = 56f;
        private const float BottomToolbarHeight = 56f;
        private const float ObjectSettingsMinScale = 0.05f;
        private const float ObjectSettingsMaxScale = 8f;
        private const string TextInputControlPrefix = "CampusRuntimeTextInput_";

        [SerializeField] private bool openOnStart = true;
        [SerializeField] private bool showGridOverlay = true;
        [SerializeField] private bool showHelpOverlay;
        [SerializeField] private bool showSettings;
        [SerializeField] private bool showObjectSettings;
        [SerializeField] private bool autoProjectSync = true;
        [SerializeField] private bool autoRestoreProjectSyncOnStart = true;
        [SerializeField] private float autoProjectSyncDelay = 1.5f;
        [SerializeField] private int selectedFloorIndex = 1;
        [SerializeField] private int selectedFloorTileIndex;
        [SerializeField] private int selectedWallTileIndex;
        [SerializeField] private int selectedObjectIndex;
        [SerializeField] private int selectedRoomIndex;
        [SerializeField] private int selectedRoomPrefabIndex;
        [SerializeField] private int selectedWallProfileIndex;
        [SerializeField] private int brushSize = 1;
        [SerializeField] private int rotation90;
        [SerializeField] private int stairTargetFloorIndex = 2;
        [SerializeField] private int newRoomRequiredCount = 1;
        [SerializeField] private int selectedObjectFootprintX = 1;
        [SerializeField] private int selectedObjectFootprintY = 1;
        [SerializeField] private int objectSettingsPreviewRotation90;
        [SerializeField] private int selectedObjectInteractionAnchorIndex;
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
        private bool strokeActive;
        private bool strokeUndoRecorded;
        private bool rectangleDragActive;
        private bool cameraDragActive;
        private bool projectSyncPending;
        private bool projectSyncInProgress;
        private bool suppressProjectSyncScheduling;
        private float projectSyncDueTime;
        private Vector3Int rectangleStartCell;
        private Vector3Int hoverCell;
        private Vector3Int lastPaintCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
        private Vector2 lastCameraDragMouse;
        private string newRoomName = string.Empty;
        private string newRoomPrefabName = string.Empty;
        private string statusText = "F10 打开/关闭运行时地图编辑器";
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
        private Sprite roomMarkerSprite;
        private Transform runtimeImportPrefabRoot;
        private CampusRuntimeImportTarget activeImportTarget = CampusRuntimeImportTarget.Floor;
        private string activeImportLabel = "导入地面";
        private string customWallName = "Custom Wall";
        private Texture2D customWallFaceTexture;
        private Texture2D customWallCapTexture;
        private GameObject lastObjectSettingsPrefab;
        private GameObject lastFootprintSyncedPrefab;
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
        private readonly List<Texture2D> importedTextures = new List<Texture2D>();
        private readonly List<Sprite> importedSprites = new List<Sprite>();
        private readonly List<UnityEngine.Object> importedAssets = new List<UnityEngine.Object>();
        private readonly List<string> pendingDroppedPaths = new List<string>();
        private readonly List<string> undoSnapshots = new List<string>();
        private readonly List<string> redoSnapshots = new List<string>();
        private readonly Dictionary<string, string> textInputDrafts = new Dictionary<string, string>();

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
        private Texture2D roomMarkerTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            CampusRuntimeMapEditor existing = FindFirstObjectByType<CampusRuntimeMapEditor>();
            if (existing != null)
            {
                return;
            }

            GameObject host = new GameObject("Campus Runtime Map Editor");
            DontDestroyOnLoad(host);
            host.AddComponent<CampusRuntimeMapEditor>();
        }

        private void Awake()
        {
            isOpen = openOnStart;
            CampusDynamicShadowUtility.ApplyHighestRuntimeShadowQuality();
            LoadRuntimeResources();
            RefreshSceneReferences();
            TryAutoRestoreRuntimeContentFromProject();
            PrepareRuntimeMapPresentationSafe();
            EnsureRoomRequirements();
            isReady = true;
            EnsureFileDropBridge();
            SetStatus("运行时编辑器已就绪：F10 切换，左键放置，右键擦除。");
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (fileDropBridge != null)
            {
                fileDropBridge.Dispose();
                fileDropBridge = null;
            }
#endif
        }

        private void Update()
        {
            if (!IsEditingTextInput() && WasKeyPressed(KeyCode.F10))
            {
                isOpen = !isOpen;
                SetStatus(isOpen ? "已打开运行时地图编辑器。" : "已关闭运行时地图编辑器。");
            }

            if (isReady)
            {
                ProcessAutoProjectSync();
            }

            if (!isOpen || !isReady)
            {
                return;
            }

            RefreshSceneReferences();
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
                textInputFocused = false;
                DrawClosedHint();
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

                if (prefab.GetComponent<CampusStairLink>() != null || prefab.name.Contains("楼梯") || prefab.name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0)
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
            SetStatus("已刷新玩家导入资源。");
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
            ImportDroppedPaths(paths);
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
                File.WriteAllText(roomFile, "# 每行一个房间：房间名 或 房间名,数量\n");
            }
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
            for (int i = 0; i < files.Length; i++)
            {
                Texture2D texture = LoadImportedTexture(files[i]);
                if (texture == null)
                {
                    continue;
                }

                string objectName = Path.GetFileNameWithoutExtension(files[i]);
                Vector2Int footprint = ResolveImportedObjectFootprint(objectName, texture);
                Sprite sprite = CreateObjectSprite(texture, objectName, footprint);

                GameObject prefab = new GameObject(objectName);
                prefab.hideFlags = HideFlags.DontSave;
                prefab.transform.SetParent(root, false);
                prefab.SetActive(false);
                SpriteRenderer renderer = prefab.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                CampusPlacedObject placed = prefab.AddComponent<CampusPlacedObject>();
                placed.ObjectId = objectName;
                placed.FootprintSize = footprint;
                placed.BlocksMovement = true;

                importedAssets.Add(prefab);
                AddUnique(objectPrefabs, prefab);
            }
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
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.DontSave;
            importedSprites.Add(sprite);
            importedAssets.Add(sprite);
            return sprite;
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
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.DontSave;
                if (!texture.LoadImage(bytes))
                {
                    DestroyRuntimeObject(texture);
                    return null;
                }

                texture.name = Path.GetFileNameWithoutExtension(path);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
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
            if (mapRoot == null)
            {
                mapRoot = FindFirstObjectByType<CampusMapRoot>();
            }

            if (mapRoot == null)
            {
                mapRoot = CreateMapRoot();
            }

            sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                sceneCamera = FindFirstObjectByType<Camera>();
            }

            EnsureFloor(selectedFloorIndex);
            dayNightController = CampusDayNightController.EnsureSceneController(mapRoot);
            mapRoot.RebuildFloorReferences();
            if (selectedFloorIndex <= 0)
            {
                selectedFloorIndex = 1;
            }

            stairTargetFloorIndex = Mathf.Max(1, stairTargetFloorIndex);
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
                SetStatus("地图表现刷新失败：" + exception.Message);
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
                SetStatus("旋转：" + rotation90 * 90 + "°");
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
            CampusFloorRoot floor = EnsureFloor(selectedFloorIndex);
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
                strokeActive = false;
                strokeUndoRecorded = false;
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
                BeginStrokeUndo();
                EraseAtCell(floor, hoverCell);
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
                    BeginStrokeUndo();
                    PaintFloor(floor, hoverCell);
                    break;
                case CampusRuntimeBrushMode.PaintWall:
                    BeginStrokeUndo();
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
                    BeginStrokeUndo();
                    EraseAtCell(floor, hoverCell);
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

        private void BeginStrokeUndo()
        {
            if (!strokeActive)
            {
                strokeActive = true;
                strokeUndoRecorded = false;
            }

            if (!strokeUndoRecorded)
            {
                RecordUndo();
                strokeUndoRecorded = true;
            }
        }

        private void PaintFloor(CampusFloorRoot floor, Vector3Int anchorCell)
        {
            TileBase tile = GetSelectedFloorTile();
            if (floor == null || floor.FloorTilemap == null || tile == null)
            {
                SetStatus("没有可用地面瓦片。");
                return;
            }

            PaintTileArea(floor.FloorTilemap, anchorCell, brushSize, tile, BuildTileTransform());
            floor.RefreshUsedBounds();
        }

        private void PaintWall(CampusFloorRoot floor, Vector3Int anchorCell)
        {
            TileBase tile = GetSelectedWallTile();
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null || tile == null)
            {
                SetStatus("没有可用墙体瓦片。");
                return;
            }

            PaintTileArea(wallLogic, anchorCell, brushSize, tile, BuildTileTransform());
            RebuildWallVisuals(floor);
        }

        private static bool IsRectangleDragBrushMode(CampusRuntimeBrushMode mode)
        {
            return mode == CampusRuntimeBrushMode.RectangleFloor ||
                   mode == CampusRuntimeBrushMode.RectangleWall ||
                   mode == CampusRuntimeBrushMode.RectangleErase ||
                   mode == CampusRuntimeBrushMode.CreateRoomPrefab;
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
                    else
                    {
                        EraseAtCell(floor, cell, false);
                    }
                }
            }

            RebuildWallVisuals(floor);
            floor.RefreshUsedBounds();
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
                SetStatus("没有可用物体预制体。");
                return;
            }

            CampusPlacedObject prefabPlaced = prefab.GetComponent<CampusPlacedObject>();
            Vector2Int footprint = prefabPlaced != null ? prefabPlaced.NormalizedFootprintSize : Vector2Int.one;
            int effectiveRotation90 = prefabPlaced != null ? prefabPlaced.ResolveAllowedRotation90(rotation90) : 0;
            Vector2Int rotatedFootprint = CampusPlacedObject.RotateFootprintSize(footprint, effectiveRotation90);
            EraseObjectsAtCells(floor, cell, rotatedFootprint);

            GameObject instance = Instantiate(prefab, floor.PropsRoot);
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
                placed.StorageSize = prefabPlaced.NormalizedStorageSize;
                placed.StorageMaxWeight = prefabPlaced.NormalizedStorageMaxWeight;
                placed.SortingOrderOffset = prefabPlaced.SortingOrderOffset;
            }

            placed.ApplyCellToTransform(floor.Grid);
            placed.ApplyInteractionState();
            CampusDynamicShadowUtility.EnsureObjectShadowCasters(placed, floor.Grid);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            SetStatus("已放置物体：" + displayName);
        }

        private void PlaceStair(CampusFloorRoot floor, Vector3Int cell)
        {
            if (stairPrefab == null || floor == null || floor.Grid == null || floor.StairsRoot == null)
            {
                SetStatus("没有可用楼梯预制体。");
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
            SetStatus("已放置楼梯：F" + floor.FloorIndex + " -> F" + targetFloor);
        }

        private void CreateStairInstance(GameObject prefab, CampusFloorRoot floor, int fromFloor, int toFloor, Vector3Int fromCell, Vector3Int secondaryCell, Vector3Int toCell, int stairRotation90, string linkId, bool isAutoReturn)
        {
            GameObject instance = Instantiate(prefab, floor.StairsRoot);
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

        private void PlaceRoomMarker(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
            {
                return;
            }

            string roomName = GetSelectedRoomName();
            if (string.IsNullOrEmpty(roomName))
            {
                SetStatus("请先在房间面板添加房间类型。");
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
            SetStatus("已标记房间：" + roomName);
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
                SetStatus("请先输入房间模块名称。");
                return;
            }

            BoundsInt bounds = BuildInclusiveCellBounds(start, end);
            CampusRuntimeRoomPrefab roomPrefab = CaptureRoomPrefab(floor, bounds, roomName.Trim());
            if (!HasRoomPrefabContent(roomPrefab))
            {
                SetStatus("框选区域没有可预制的地面、墙体、物品或光源。");
                return;
            }

            SaveRuntimeRoomPrefab(roomPrefab);
            AddOrUpdateRoomDefinition(roomPrefab.RoomName, Mathf.Max(1, newRoomRequiredCount));
            LoadImportedRoomPrefabs();
            SelectRoomPrefabByName(roomPrefab.RoomName);
            newRoomPrefabName = string.Empty;
            brushMode = CampusRuntimeBrushMode.PlaceRoomPrefab;
            ScheduleProjectSync();
            SyncRuntimeContentToProject(false);
            SetStatus("已预制房间：" + roomPrefab.RoomName + "（" + roomPrefab.Size.x + "x" + roomPrefab.Size.y + "）。");
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
            EnsureRoomPrefabMarker(roomPrefab);
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

                CampusRuntimeRoomSnapshot roomSnapshot = new CampusRuntimeRoomSnapshot();
                roomSnapshot.RoomName = marker.RoomName;
                roomSnapshot.FloorIndex = 0;
                roomSnapshot.Cell = ToRelativeCell(marker.Cell, originCell);
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
                if (light == null || light.lightType == Light2D.LightType.Global)
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
                SetStatus("请先选择房间模块，或用“框选预制”创建一个。");
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
            ApplyRoomPrefabLights(floor, roomPrefab.Lights, anchorCell);
            AddOrUpdateRoomDefinition(roomPrefab.RoomName, Mathf.Max(1, newRoomRequiredCount));
            RebuildWallVisuals(floor);
            floor.RefreshUsedBounds();
            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            SetStatus("已放置房间模块：" + roomPrefab.RoomName);
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

                    CampusRuntimeRoomSnapshot shifted = new CampusRuntimeRoomSnapshot();
                    shifted.RoomName = string.IsNullOrWhiteSpace(marker.RoomName) ? fallbackRoomName : marker.RoomName;
                    shifted.FloorIndex = floor != null ? floor.FloorIndex : 1;
                    shifted.Cell = ToAbsoluteCell(anchorCell, marker.Cell);
                    shifted.HideMarkerVisual = true;
                    shiftedMarkers.Add(shifted);
                }
            }

            if (shiftedMarkers.Count == 0)
            {
                CampusRuntimeRoomSnapshot fallback = new CampusRuntimeRoomSnapshot();
                fallback.RoomName = fallbackRoomName;
                fallback.FloorIndex = floor != null ? floor.FloorIndex : 1;
                fallback.Cell = anchorCell;
                fallback.HideMarkerVisual = true;
                shiftedMarkers.Add(fallback);
            }

            ApplyRooms(floor, shiftedMarkers);
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

            Vector3 position = floor.Grid.GetCellCenterWorld(cell);
            if (IsKeyHeld(KeyCode.LeftAlt) || IsKeyHeld(KeyCode.RightAlt))
            {
                position = mouseWorld;
            }

            position.z = floor.Grid.transform.position.z;
            EraseLightsAtCell(floor, cell);
            GameObject lightObject = new GameObject("运行时光源_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y);
            lightObject.transform.position = position;
            lightObject.transform.rotation = Quaternion.Euler(0f, 0f, rotation90 * 90f);
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = lightBrushType;
            light.blendStyleIndex = 0;
            light.targetSortingLayers = GetAllSortingLayerIds();
            light.color = lightColor;
            light.intensity = lightBrushType == Light2D.LightType.Global ? AmbientLightIntensity : lightIntensity;
            CampusDynamicShadowUtility.ConfigureLightShadows(light, lightBrushType != Light2D.LightType.Global && lightShadowsEnabled, lightShadowIntensity, lightShadowSoftness, lightShadowSoftnessFalloff);
            if (lightBrushType == Light2D.LightType.Point)
            {
                light.pointLightInnerAngle = 360f;
                light.pointLightOuterAngle = 360f;
                light.pointLightInnerRadius = Mathf.Max(0f, lightInnerRadius);
                light.pointLightOuterRadius = Mathf.Max(light.pointLightInnerRadius + 0.1f, lightOuterRadius);
                light.falloffIntensity = 0.18f;
            }

            selectedLight = light;
            ScheduleProjectSync();
            SetStatus("已放置光源。");
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
            EraseRoomMarkersAtCell(floor, cell);
            EraseLightsAtCell(floor, cell);

            if (rebuildWalls)
            {
                RebuildWallVisuals(floor);
                floor.RefreshUsedBounds();
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

        private void EraseRoomMarkersAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.PropsRoot == null)
            {
                return;
            }

            CampusRuntimeRoomMarker[] markers = floor.PropsRoot.GetComponentsInChildren<CampusRuntimeRoomMarker>(true);
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker != null && marker.Cell == cell && marker.FloorIndex == floor.FloorIndex)
                {
                    DestroyRuntimeObject(marker.gameObject);
                }
            }
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
                if (light == null || light.lightType == Light2D.LightType.Global)
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
                if (light == null || light.lightType == Light2D.LightType.Global)
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
                SetStatus("已选中光源：" + selectedLight.gameObject.name);
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
                    SetStatus("已吸取墙体：" + GetDisplayName(wallTile));
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
                    SetStatus("已吸取地面：" + GetDisplayName(floorTile));
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
                        SetStatus("已吸取物体：" + CampusObjectNames.GetDisplayName(placed.ObjectId));
                        return;
                    }
                }
            }

            SetStatus("当前格没有可吸取内容。");
        }

        private void RecordUndo()
        {
            string snapshot = BuildSnapshotJson();
            if (undoSnapshots.Count > 0 && undoSnapshots[undoSnapshots.Count - 1] == snapshot)
            {
                return;
            }

            undoSnapshots.Add(snapshot);
            if (undoSnapshots.Count > MaxUndoSnapshots)
            {
                undoSnapshots.RemoveAt(0);
            }

            redoSnapshots.Clear();
            ScheduleProjectSync();
        }

        private void UndoSnapshot()
        {
            if (undoSnapshots.Count == 0)
            {
                SetStatus("没有可撤销操作。");
                return;
            }

            string current = BuildSnapshotJson();
            string previous = undoSnapshots[undoSnapshots.Count - 1];
            undoSnapshots.RemoveAt(undoSnapshots.Count - 1);
            redoSnapshots.Add(current);
            LoadSnapshotJson(previous);
            ScheduleProjectSync();
            SetStatus("已撤销。");
        }

        private void RedoSnapshot()
        {
            if (redoSnapshots.Count == 0)
            {
                SetStatus("没有可重做操作。");
                return;
            }

            string current = BuildSnapshotJson();
            string next = redoSnapshots[redoSnapshots.Count - 1];
            redoSnapshots.RemoveAt(redoSnapshots.Count - 1);
            undoSnapshots.Add(current);
            LoadSnapshotJson(next);
            ScheduleProjectSync();
            SetStatus("已重做。");
        }

        private string BuildSnapshotJson()
        {
            return JsonUtility.ToJson(BuildSnapshot(), true);
        }

        private CampusRuntimeMapSnapshot BuildSnapshot()
        {
            RefreshSceneReferences();
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
                mapRoot.RebuildFloorReferences();
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

        private void LoadSnapshotJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            CampusRuntimeMapSnapshot snapshot = JsonUtility.FromJson<CampusRuntimeMapSnapshot>(json);
            if (snapshot == null)
            {
                SetStatus("导入失败：JSON 格式无效。");
                return;
            }

            ApplySnapshot(snapshot);
        }

        private void ApplySnapshot(CampusRuntimeMapSnapshot snapshot)
        {
            RefreshSceneReferences();
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
            PrepareRuntimeMapPresentationSafe();
        }

        private void ExportToJson()
        {
            string folder = GetExportFolder();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "CampusMap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(path, BuildSnapshotJson());
            SetStatus("已导出：" + path);
            Debug.Log("[NtingCampusRuntimeMapEditor] Exported map to " + path);
        }

        private void SyncRuntimeContentToProject()
        {
            SyncRuntimeContentToProject(true);
        }

        private void SyncRuntimeContentToProject(bool showStatus)
        {
            if (projectSyncInProgress)
            {
                return;
            }

            try
            {
                projectSyncInProgress = true;
                EnsureImportFolders();
                string syncRoot = GetProjectSyncRootFolder();
                string syncImportFolder = GetProjectSyncImportFolder();
                Directory.CreateDirectory(syncRoot);
                MirrorDirectory(GetImportRootFolder(), syncImportFolder, true);
                File.WriteAllText(GetProjectSyncMapPath(), BuildSnapshotJson(), Encoding.UTF8);
                CampusRuntimeProjectSyncManifest manifest = new CampusRuntimeProjectSyncManifest
                {
                    ExportedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    UnityPersistentDataPath = Application.persistentDataPath,
                    ImportRootFolderName = RuntimeImportFolder,
                    MapFileName = ProjectSyncMapFile
                };
                File.WriteAllText(GetProjectSyncManifestPath(), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
                RefreshAssetDatabaseIfAvailable();
                projectSyncPending = false;
                if (showStatus)
                {
                    SetStatus("\u5df2\u540c\u6b65\u5230\u9879\u76ee\uff1a" + syncRoot);
                }

                Debug.Log("[NtingCampusRuntimeMapEditor] Synced runtime content to project folder: " + syncRoot);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Project sync failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus("\u540c\u6b65\u5230\u9879\u76ee\u5931\u8d25\uff1a" + exception.Message);
                }
            }
            finally
            {
                projectSyncInProgress = false;
            }
        }

        private void RestoreRuntimeContentFromProject()
        {
            RestoreRuntimeContentFromProject(true, true);
        }

        private void RestoreRuntimeContentFromProject(bool recordUndo, bool showStatus)
        {
            string syncRoot = GetProjectSyncRootFolder();
            string syncImportFolder = GetProjectSyncImportFolder();
            string syncMapPath = GetProjectSyncMapPath();
            if (!Directory.Exists(syncImportFolder) && !File.Exists(syncMapPath))
            {
                if (showStatus)
                {
                    SetStatus("\u6ca1\u6709\u627e\u5230\u9879\u76ee\u540c\u6b65\u5305\uff1a" + syncRoot);
                }

                return;
            }

            try
            {
                bool previousSuppress = suppressProjectSyncScheduling;
                suppressProjectSyncScheduling = true;
                if (recordUndo)
                {
                    RecordUndo();
                }

                BackupLocalRuntimeImportFolder();
                if (Directory.Exists(syncImportFolder))
                {
                    MirrorDirectory(syncImportFolder, GetImportRootFolder(), true);
                }

                LoadRuntimeResources();
                if (File.Exists(syncMapPath))
                {
                    LoadSnapshotJson(File.ReadAllText(syncMapPath, Encoding.UTF8));
                }

                suppressProjectSyncScheduling = previousSuppress;
                projectSyncPending = false;
                if (showStatus)
                {
                    SetStatus("\u5df2\u4ece\u9879\u76ee\u540c\u6b65\u5e76\u52a0\u8f7d\u5730\u56fe\u3002");
                }

                Debug.Log("[NtingCampusRuntimeMapEditor] Restored runtime content from project folder: " + syncRoot);
            }
            catch (Exception exception)
            {
                suppressProjectSyncScheduling = false;
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Restore project sync failed: " + exception.Message);
                if (showStatus)
                {
                    SetStatus("\u4ece\u9879\u76ee\u6062\u590d\u5931\u8d25\uff1a" + exception.Message);
                }
            }
        }

        private void TryAutoRestoreRuntimeContentFromProject()
        {
            if (!autoRestoreProjectSyncOnStart)
            {
                return;
            }

            if (!Directory.Exists(GetProjectSyncImportFolder()) && !File.Exists(GetProjectSyncMapPath()))
            {
                return;
            }

            RestoreRuntimeContentFromProject(false, false);
        }

        private void ScheduleProjectSync()
        {
            if (!autoProjectSync || suppressProjectSyncScheduling || projectSyncInProgress)
            {
                return;
            }

            projectSyncPending = true;
            projectSyncDueTime = Time.realtimeSinceStartup + Mathf.Max(0.1f, autoProjectSyncDelay);
        }

        private void ProcessAutoProjectSync()
        {
            if (!autoProjectSync || !projectSyncPending || projectSyncInProgress)
            {
                return;
            }

            if (Time.realtimeSinceStartup < projectSyncDueTime)
            {
                return;
            }

            SyncRuntimeContentToProject(false);
        }

        private void ImportLatestJson()
        {
            string folder = GetExportFolder();
            if (!Directory.Exists(folder))
            {
                SetStatus("没有找到导出目录：" + folder);
                return;
            }

            string[] files = Directory.GetFiles(folder, "CampusMap_*.json");
            if (files.Length == 0)
            {
                SetStatus("没有可导入的地图 JSON。");
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            string path = files[files.Length - 1];
            RecordUndo();
            LoadSnapshotJson(File.ReadAllText(path));
            SetStatus("已导入：" + path);
        }

        private string GetExportFolder()
        {
            return Path.Combine(Application.persistentDataPath, "CampusMapExports");
        }

        private string GetProjectSyncRootFolder()
        {
            return Path.Combine(Application.dataPath, "NtingCampus", ProjectSyncFolder);
        }

        private string GetProjectSyncImportFolder()
        {
            return Path.Combine(GetProjectSyncRootFolder(), RuntimeImportFolder);
        }

        private string GetProjectSyncMapPath()
        {
            return Path.Combine(GetProjectSyncRootFolder(), ProjectSyncMapFile);
        }

        private string GetProjectSyncManifestPath()
        {
            return Path.Combine(GetProjectSyncRootFolder(), ProjectSyncManifestFile);
        }

        private void CaptureTiles(Tilemap tilemap, List<TileBase> palette, List<CampusRuntimeTileSnapshot> output)
        {
            long perf = CampusMapEditorPerformance.Begin();
            try
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
            finally
            {
                CampusMapEditorPerformance.End(perf, "Runtime CaptureTiles");
            }
        }

        private void ApplyTiles(Tilemap tilemap, List<CampusRuntimeTileSnapshot> tiles, List<TileBase> palette)
        {
            long perf = CampusMapEditorPerformance.Begin();
            try
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
            finally
            {
                CampusMapEditorPerformance.End(perf, "Runtime ApplyTiles");
            }
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
            objectSnapshot.DisplayNameOverride = placed.DisplayNameOverride;
            objectSnapshot.PaletteIndex = FindPrefabIndexByName(objectSnapshot.ObjectId);
            objectSnapshot.Position = placed.transform.position;
            objectSnapshot.Cell = placed.Cell;
            objectSnapshot.FootprintSize = placed.NormalizedFootprintSize;
            objectSnapshot.FloorIndex = floor != null ? floor.FloorIndex : placed.FloorIndex;
            objectSnapshot.OverrideFootprintSize = placed.OverrideFootprintSize;
            objectSnapshot.VisualScale = placed.NormalizedVisualScale;
            objectSnapshot.LockVisualScaleAspect = placed.LockVisualScaleAspect;
            objectSnapshot.OverrideAllowRotation = placed.OverrideAllowRotation;
            objectSnapshot.AllowRotation = placed.AllowRotation;
            objectSnapshot.OverrideRotation0Sprite = placed.OverrideRotation0Sprite;
            objectSnapshot.Rotation0SpritePath = placed.Rotation0SpritePath;
            objectSnapshot.OverrideRotation90Sprite = placed.OverrideRotation90Sprite;
            objectSnapshot.Rotation90SpritePath = placed.Rotation90SpritePath;
            objectSnapshot.OverrideRotation180Sprite = placed.OverrideRotation180Sprite;
            objectSnapshot.Rotation180SpritePath = placed.Rotation180SpritePath;
            objectSnapshot.OverrideRotation270Sprite = placed.OverrideRotation270Sprite;
            objectSnapshot.Rotation270SpritePath = placed.Rotation270SpritePath;
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
            clone.DisplayNameOverride = source.DisplayNameOverride;
            clone.PaletteIndex = source.PaletteIndex;
            clone.Position = source.Position;
            clone.Cell = source.Cell;
            clone.FootprintSize = source.FootprintSize;
            clone.FloorIndex = source.FloorIndex;
            clone.OverrideFootprintSize = source.OverrideFootprintSize;
            clone.VisualScale = source.VisualScale;
            clone.LockVisualScaleAspect = source.LockVisualScaleAspect;
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
                placed.DisplayNameOverride = objectSnapshot.DisplayNameOverride;
                placed.FloorIndex = floor.FloorIndex;
                placed.Cell = objectSnapshot.Cell;
                placed.OverrideFootprintSize = objectSnapshot.OverrideFootprintSize;
                placed.FootprintSize = objectSnapshot.FootprintSize;
                placed.VisualScale = CampusPlacedObject.NormalizeVisualScale(objectSnapshot.VisualScale);
                placed.LockVisualScaleAspect = objectSnapshot.LockVisualScaleAspect;
                if (objectSnapshot.OverrideAllowRotation)
                {
                    placed.OverrideAllowRotation = true;
                    placed.AllowRotation = objectSnapshot.AllowRotation;
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
                    ? "\u4ea4\u4e92"
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

                CampusDynamicShadowUtility.EnsureObjectShadowCasters(placed, floor.Grid);
            }
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

                CampusRuntimeRoomSnapshot roomSnapshot = new CampusRuntimeRoomSnapshot();
                roomSnapshot.RoomName = marker.RoomName;
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
                GameObject markerObject = new GameObject("Room_" + roomSnapshot.RoomName + "_F" + floor.FloorIndex + "_" + roomSnapshot.Cell.x + "_" + roomSnapshot.Cell.y);
                markerObject.transform.SetParent(floor.PropsRoot, false);
                if (floor.Grid != null)
                {
                    markerObject.transform.position = floor.Grid.GetCellCenterWorld(roomSnapshot.Cell);
                }

                CampusRuntimeRoomMarker marker = markerObject.AddComponent<CampusRuntimeRoomMarker>();
                marker.RoomName = roomSnapshot.RoomName;
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
                if (light == null)
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
                EnsureDefaultGlobalLight();
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

            GameObject lightObject = new GameObject(string.IsNullOrEmpty(lightSnapshot.Name) ? "运行时光源" : lightSnapshot.Name);
            lightObject.transform.position = lightSnapshot.Position;
            lightObject.transform.rotation = Quaternion.Euler(lightSnapshot.Rotation);
            Light2D light = lightObject.AddComponent<Light2D>();
            Light2D.LightType type;
            if (!Enum.TryParse(lightSnapshot.LightType, out type))
            {
                type = Light2D.LightType.Point;
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
            DestroyChildren(floor.PropsRoot);
            DestroyChildren(floor.StairsRoot);
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
                if (light == null)
                {
                    continue;
                }

                string lightName = light.gameObject.name;
                bool isDayNightLight = CampusObjectNames.MatchesAny(
                    lightName,
                    CampusObjectNames.GlobalLight2D,
                    CampusObjectNames.LegacyGlobalLight2D,
                    CampusObjectNames.SunLight2D,
                    CampusObjectNames.LegacySunLight2D);
                bool isReplacedBySnapshot = snapshotLightNames.Contains(lightName);

                if (isDayNightLight || isReplacedBySnapshot)
                {
                    DestroyRuntimeObject(light.gameObject);
                }
            }

            selectedLight = null;
        }

        private void DrawClosedHint()
        {
            Rect rect = new Rect(18f, 18f, 250f, 34f);
            GUI.Box(rect, "F10 地图编辑器", panelStyle);
        }

        private void ResolveLayoutRects()
        {
            float toolbarY = Mathf.Max(Screen.height - BottomToolbarHeight - PanelMargin, TopMargin + 280f);
            bottomToolbarRect = new Rect(PanelMargin, toolbarY, Mathf.Max(360f, Screen.width - PanelMargin * 2f), BottomToolbarHeight);

            float rightWidth = Mathf.Clamp(Screen.width * 0.18f, 280f, 360f);
            float availableLeftWidth = Screen.width - rightWidth - PanelMargin * 3f;
            float leftWidth = Mathf.Clamp(Screen.width * 0.26f, 300f, 440f);
            leftWidth = Mathf.Min(leftWidth, Mathf.Max(280f, availableLeftWidth));
            float panelHeight = Mathf.Max(280f, toolbarY - TopMargin - PanelMargin);
            leftPanelRect = new Rect(PanelMargin, TopMargin, leftWidth, panelHeight);

            float rightX = Mathf.Max(leftPanelRect.xMax + PanelMargin, Screen.width - rightWidth - PanelMargin);
            rightWidth = Mathf.Min(rightWidth, Screen.width - rightX - PanelMargin);
            if (rightWidth < 240f)
            {
                rightWidth = 240f;
                rightX = Screen.width - rightWidth - PanelMargin;
            }

            float floorHeight = Mathf.Clamp(Screen.height * 0.22f, 176f, 260f);
            floorPanelRect = new Rect(rightX, TopMargin, rightWidth, floorHeight);
            float checklistY = floorPanelRect.yMax + 14f;
            float checklistHeight = Mathf.Max(180f, toolbarY - checklistY - PanelMargin);
            checklistPanelRect = new Rect(rightX, checklistY, rightWidth, checklistHeight);
            settingsPanelRect = new Rect(Mathf.Clamp(Screen.width - 390f - PanelMargin, PanelMargin, Screen.width - 390f), Mathf.Max(TopMargin, toolbarY - 330f), 370f, Mathf.Min(300f, toolbarY - TopMargin - PanelMargin));
            float objectSettingsWidth = Mathf.Clamp(Screen.width * 0.34f, 440f, 620f);
            float objectSettingsHeight = Mathf.Clamp(toolbarY - TopMargin - PanelMargin, 420f, 690f);
            float objectSettingsX = leftPanelRect.xMax + PanelMargin;
            float objectSettingsRightLimit = floorPanelRect.x - PanelMargin;
            if (objectSettingsRightLimit - objectSettingsX < objectSettingsWidth)
            {
                objectSettingsX = Mathf.Clamp((Screen.width - objectSettingsWidth) * 0.5f, PanelMargin, Mathf.Max(PanelMargin, Screen.width - objectSettingsWidth - PanelMargin));
            }

            objectSettingsPanelRect = new Rect(objectSettingsX, TopMargin, objectSettingsWidth, Mathf.Min(objectSettingsHeight, toolbarY - TopMargin - PanelMargin));
            helpPanelRect = new Rect(Mathf.Max(PanelMargin, Screen.width * 0.5f - 285f), TopMargin + 24f, Mathf.Min(570f, Screen.width - PanelMargin * 2f), Mathf.Min(390f, toolbarY - TopMargin - 48f));
        }

        private void DrawLeftPanel()
        {
            GUI.Box(leftPanelRect, GUIContent.none, panelStyle);
            Rect tabRect = new Rect(leftPanelRect.x + 12f, leftPanelRect.y + 10f, leftPanelRect.width - 24f, 46f);
            float tabGap = 6f;
            float tabWidth = (tabRect.width - tabGap * 3f) / 4f;
            DrawTabButton(new Rect(tabRect.x, tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Build, "建筑");
            DrawTabButton(new Rect(tabRect.x + (tabWidth + tabGap), tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Rooms, "房间");
            DrawTabButton(new Rect(tabRect.x + (tabWidth + tabGap) * 2f, tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Objects, "物品");
            DrawTabButton(new Rect(tabRect.x + (tabWidth + tabGap) * 3f, tabRect.y, tabWidth, tabRect.height), CampusRuntimeEditorTab.Lighting, "光源");

            Rect titleRect = new Rect(leftPanelRect.x + 16f, leftPanelRect.y + 66f, leftPanelRect.width - 32f, 42f);
            GUI.Box(titleRect, GetActiveTabTitle(), headerStyle);

            Rect contentRect = new Rect(leftPanelRect.x + 14f, leftPanelRect.y + 116f, leftPanelRect.width - 28f, leftPanelRect.height - 134f);
            switch (activeTab)
            {
                case CampusRuntimeEditorTab.Build:
                    DrawBuildTab(contentRect);
                    break;
                case CampusRuntimeEditorTab.Rooms:
                    DrawRoomTab(contentRect);
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
                case CampusRuntimeEditorTab.Rooms:
                    return "房间";
                case CampusRuntimeEditorTab.Objects:
                    return "物品";
                case CampusRuntimeEditorTab.Lighting:
                    return "光源";
                default:
                    return "建筑";
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
            float height = 34f + 40f * 2f;
            height += 34f + 38f + 42f + (roomPrefabs.Count == 0 ? 66f : roomPrefabs.Count * 42f) + 46f;
            height += 12f + 34f + 38f + 80f + 8f;
            height += roomNames.Count == 0 ? 66f : roomNames.Count * 40f;
            height += 12f + (roomNames.Count > 0 ? 42f : 0f) + 70f;
            return height;
        }

        private float GetObjectContentHeight(float width)
        {
            float height = 34f + 40f * 2f + 8f + 44f;
            height += 46f;
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
            float height = 34f + 40f * 2f + 6f + 44f + 30f + 92f + 30f * 5f + 128f + 130f + 34f + lights.Length * 38f;
            if (selectedLight != null)
            {
                height += selectedLight.lightType == Light2D.LightType.Point ? 370f : 310f;
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

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "工具", headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PaintFloor, CampusRuntimeBrushMode.PaintWall },
                new string[] { "平移", "地板", "墙体" });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.RectangleFloor, CampusRuntimeBrushMode.RectangleWall, CampusRuntimeBrushMode.RectangleErase },
                new string[] { "框地", "框墙", "框擦" });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Erase, CampusRuntimeBrushMode.Pick },
                new string[] { "擦除", "吸取" });

            y += 8f;
            GUI.Label(new Rect(0f, y, 90f, 24f), "刷子尺寸", bodyStyle);
            brushSize = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(90f, y + 8f, viewRect.width - 150f, 20f), brushSize, 1f, 8f));
            GUI.Label(new Rect(viewRect.width - 50f, y, 50f, 24f), brushSize.ToString(), bodyStyle);
            y += 38f;

            DrawImportFolderRow(ref y, viewRect.width, "导入地面", GetFloorImportFolder(), CampusRuntimeImportTarget.Floor);
            DrawImportFolderRow(ref y, viewRect.width, "导入墙体", GetWallImportFolder(), CampusRuntimeImportTarget.Wall);
            y += 8f;

            DrawCustomWallPanel(ref y, viewRect.width);
            y += 8f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "地面", headerStyle);
            y += 34f;
            y = DrawTilePaletteGrid(floorTiles, selectedFloorTileIndex, y, viewRect.width, delegate(int index)
            {
                selectedFloorTileIndex = index;
                brushMode = CampusRuntimeBrushMode.PaintFloor;
            });

            y += 10f;
            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "墙体", headerStyle);
            y += 34f;
            y = DrawTilePaletteGrid(wallTiles, selectedWallTileIndex, y, viewRect.width, delegate(int index)
            {
                selectedWallTileIndex = index;
                brushMode = CampusRuntimeBrushMode.PaintWall;
            });

            if (wallProfiles.Count > 0)
            {
                y += 10f;
                GUI.Label(new Rect(0f, y, viewRect.width, 26f), "墙体视觉", headerStyle);
                y += 34f;
                for (int i = 0; i < wallProfiles.Count; i++)
                {
                    CampusWallRenderProfile profile = wallProfiles[i];
                    string label = profile != null ? CampusObjectNames.GetDisplayName(profile.name) : "默认";
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

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "房间工具", headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.PlaceRoom, CampusRuntimeBrushMode.CreateRoomPrefab, CampusRuntimeBrushMode.PlaceRoomPrefab },
                new string[] { "标记", "框选预制", "放置模块" });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Erase, CampusRuntimeBrushMode.Pick },
                new string[] { "擦除", "吸取" });

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "房间模块", headerStyle);
            y += 34f;
            GUI.Label(new Rect(0f, y, 64f, 30f), "模块名", bodyStyle);
            newRoomPrefabName = DrawTextInput(new Rect(66f, y, viewRect.width - 178f, 30f), newRoomPrefabName, "room_prefab_name");
            if (GUI.Button(new Rect(viewRect.width - 104f, y, 104f, 30f), "框选预制", buttonStyle))
            {
                brushMode = CampusRuntimeBrushMode.CreateRoomPrefab;
                activeTab = CampusRuntimeEditorTab.Rooms;
                SetStatus("拖拽框选区域，松开后预制成房间模块。");
            }

            y += 38f;
            float moduleButtonWidth = Mathf.Max(72f, (viewRect.width - 16f) / 3f);
            if (GUI.Button(new Rect(0f, y, moduleButtonWidth, 30f), "打开目录", buttonStyle))
            {
                OpenImportLocation(GetRoomPrefabFolder());
            }

            if (GUI.Button(new Rect(moduleButtonWidth + 8f, y, moduleButtonWidth, 30f), "刷新", buttonStyle))
            {
                LoadImportedRoomPrefabs();
                ScheduleProjectSync();
                SetStatus("已刷新房间模块。");
            }

            if (roomPrefabs.Count > 0 && GUI.Button(new Rect((moduleButtonWidth + 8f) * 2f, y, moduleButtonWidth, 30f), "删除", buttonStyle))
            {
                DeleteSelectedRoomPrefab();
            }

            y += 42f;
            if (roomPrefabs.Count == 0)
            {
                GUI.Label(new Rect(0f, y, viewRect.width, 58f), "当前没有房间模块。输入模块名后点击“框选预制”，再在地图上拖拽区域。", mutedStyle);
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

            y += 12f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "新增房间类型", headerStyle);
            y += 34f;
            GUI.Label(new Rect(0f, y, 52f, 30f), "名称", bodyStyle);
            newRoomName = GUI.TextField(new Rect(54f, y, viewRect.width - 154f, 30f), newRoomName, buttonStyle);
            newRoomRequiredCount = Mathf.Clamp(ParseIntField(new Rect(viewRect.width - 92f, y, 42f, 30f), newRoomRequiredCount), 0, 99);
            if (GUI.Button(new Rect(viewRect.width - 44f, y, 44f, 30f), "+", buttonStyle))
            {
                RecordUndo();
                AddOrUpdateRoomDefinition(newRoomName, newRoomRequiredCount);
                newRoomName = string.Empty;
                brushMode = CampusRuntimeBrushMode.PlaceRoom;
            }

            y += 38f;
            DrawImportFileRow(ref y, viewRect.width, "导入房间", GetRoomImportFile());
            y += 8f;

            if (roomNames.Count == 0)
            {
                GUI.Label(new Rect(0f, y, viewRect.width, 58f), "当前没有房间预设。请在这里新增房间，或编辑 Rooms.txt 后刷新导入。", mutedStyle);
                y += 66f;
            }

            for (int i = 0; i < roomNames.Count; i++)
            {
                string label = roomNames[i] + "  " + CountRoomMarkers(roomNames[i]) + "/" + roomRequiredCounts[i];
                if (GUI.Button(new Rect(0f, y, viewRect.width, 34f), label, i == selectedRoomIndex ? selectedButtonStyle : buttonStyle))
                {
                    selectedRoomIndex = i;
                    brushMode = CampusRuntimeBrushMode.PlaceRoom;
                }

                y += 40f;
            }

            y += 12f;
            if (roomNames.Count > 0)
            {
                if (GUI.Button(new Rect(0f, y, 118f, 32f), "删除当前", buttonStyle))
                {
                    DeleteSelectedRoomDefinition();
                }

                if (GUI.Button(new Rect(126f, y, 118f, 32f), "清空房间", buttonStyle))
                {
                    ClearRoomDefinitions();
                }

                y += 42f;
            }

            GUI.Label(new Rect(0f, y, viewRect.width, 48f), "房间清单会随运行时标记实时统计，并写入导出的 JSON。", mutedStyle);
            GUI.EndScrollView();
        }

        private void DrawObjectTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetObjectContentHeight(viewWidth)));
            objectScroll = GUI.BeginScrollView(contentRect, objectScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "工具", headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PlaceObject, CampusRuntimeBrushMode.PlaceStair },
                new string[] { "平移", "物品", "楼梯" });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Erase, CampusRuntimeBrushMode.Pick },
                new string[] { "擦除", "吸取" });

            y += 8f;
            GUI.Label(new Rect(0f, y, 70f, 24f), "旋转", bodyStyle);
            if (GUI.Button(new Rect(72f, y, 84f, 30f), rotation90 * 90 + "°", buttonStyle))
            {
                rotation90 = (rotation90 + 1) % 4;
            }

            GUI.Label(new Rect(170f, y, 76f, 24f), "目标楼层", bodyStyle);
            stairTargetFloorIndex = Mathf.Clamp(ParseIntField(new Rect(246f, y, 58f, 30f), stairTargetFloorIndex), 1, 99);
            y += 44f;

            DrawSelectedObjectSettingsLauncher(ref y, viewRect.width);

            DrawImportFolderRow(ref y, viewRect.width, "导入物品", GetObjectImportFolder(), CampusRuntimeImportTarget.Object);
            y += 8f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "物品资源", headerStyle);
            y += 34f;
            y = DrawPrefabPaletteGrid(objectPrefabs, selectedObjectIndex, y, viewRect.width, delegate(int index)
            {
                selectedObjectIndex = index;
                brushMode = CampusRuntimeBrushMode.PlaceObject;
            });

            if (stairPrefab == null)
            {
                y += 12f;
                GUI.Label(new Rect(0f, y, viewRect.width, 52f), "未找到楼梯预制体。把楼梯预制体放进 Resources/NtingCampusRuntime 后，打包内也能放置楼梯。", warningStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawLightingTab(Rect contentRect)
        {
            float viewWidth = contentRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentRect.height + 1f, GetLightingContentHeight(viewWidth)));
            lightScroll = GUI.BeginScrollView(contentRect, lightScroll, viewRect);
            float y = 0f;

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "光源工具", headerStyle);
            y += 34f;
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pan, CampusRuntimeBrushMode.PlaceLight, CampusRuntimeBrushMode.Erase },
                new string[] { "平移", "放置", "擦除" });
            DrawModeButtonRow(ref y, viewRect.width,
                new CampusRuntimeBrushMode[] { CampusRuntimeBrushMode.Pick },
                new string[] { "吸取" });

            y += 6f;
            if (GUI.Button(new Rect(0f, y, 116f, 32f), "点光源", lightBrushType == Light2D.LightType.Point ? selectedButtonStyle : buttonStyle))
            {
                lightBrushType = Light2D.LightType.Point;
                brushMode = CampusRuntimeBrushMode.PlaceLight;
            }

            if (GUI.Button(new Rect(124f, y, 116f, 32f), "全局光", lightBrushType == Light2D.LightType.Global ? selectedButtonStyle : buttonStyle))
            {
                lightBrushType = Light2D.LightType.Global;
                brushMode = CampusRuntimeBrushMode.PlaceLight;
            }

            y += 44f;
            y = DrawSlider(y, viewRect.width, "新光亮度", ref lightIntensity, 0f, 4f);
            DrawColorControls(ref y, viewRect.width, "新光颜色", ref lightColor);
            y = DrawSlider(y, viewRect.width, "内半径", ref lightInnerRadius, 0f, 12f);
            y = DrawSlider(y, viewRect.width, "外半径", ref lightOuterRadius, 0.2f, 24f);
            lightShadowsEnabled = GUI.Toggle(new Rect(0f, y, viewRect.width, 24f), lightShadowsEnabled, "启用阴影", bodyStyle);
            y += 30f;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && lightShadowsEnabled;
            y = DrawSlider(y, viewRect.width, "阴影", ref lightShadowIntensity, 0f, 1f);
            y = DrawSlider(y, viewRect.width, "柔和", ref lightShadowSoftness, 0f, 1f);
            y = DrawSlider(y, viewRect.width, "衰减", ref lightShadowSoftnessFalloff, 0f, 1f);
            GUI.enabled = previousGuiEnabled;
            DrawLightPreviewCard(ref y, viewRect.width);
            DrawDayNightControls(ref y, viewRect.width);

            GUI.Label(new Rect(0f, y, viewRect.width, 26f), "场景光源", headerStyle);
            y += 34f;
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light2D light = lights[i];
                if (light == null)
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
                    ScheduleProjectSync();
                }

                y += 38f;
            }

            if (selectedLight != null)
            {
                bool selectedLightChanged = false;
                y += 8f;
                GUI.Label(new Rect(0f, y, viewRect.width, 26f), "当前光源", headerStyle);
                y += 34f;
                GUI.Label(new Rect(0f, y, 66f, 24f), "亮度", bodyStyle);
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
                DrawColorControls(ref y, viewRect.width, "颜色", ref selectedColor);
                if (selectedLight != null && selectedLight.color != selectedColor)
                {
                    selectedLight.color = selectedColor;
                    lightColor = selectedColor;
                    selectedLightChanged = true;
                }

                bool previousShadowsEnabled = selectedLight.shadowsEnabled;
                bool selectedShadowsEnabled = GUI.Toggle(new Rect(0f, y, viewRect.width, 24f), selectedLight.shadowsEnabled, "启用阴影", bodyStyle);
                y += 30f;
                bool selectedPreviousGuiEnabled = GUI.enabled;
                GUI.enabled = selectedPreviousGuiEnabled && selectedShadowsEnabled;
                float selectedShadowIntensity = selectedLight.shadowIntensity;
                float selectedShadowSoftness = selectedLight.shadowSoftness;
                float selectedShadowSoftnessFalloff = selectedLight.shadowSoftnessFalloffIntensity;
                y = DrawSlider(y, viewRect.width, "阴影", ref selectedShadowIntensity, 0f, 1f);
                y = DrawSlider(y, viewRect.width, "柔和", ref selectedShadowSoftness, 0f, 1f);
                y = DrawSlider(y, viewRect.width, "衰减", ref selectedShadowSoftnessFalloff, 0f, 1f);
                GUI.enabled = selectedPreviousGuiEnabled;
                bool selectedShadowSettingsChanged = previousShadowsEnabled != selectedShadowsEnabled ||
                                                     !Mathf.Approximately(selectedLight.shadowIntensity, selectedShadowIntensity) ||
                                                     !Mathf.Approximately(selectedLight.shadowSoftness, selectedShadowSoftness) ||
                                                     !Mathf.Approximately(selectedLight.shadowSoftnessFalloffIntensity, selectedShadowSoftnessFalloff);
                CampusDynamicShadowUtility.ConfigureLightShadows(selectedLight, selectedLight.lightType != Light2D.LightType.Global && selectedShadowsEnabled, selectedShadowIntensity, selectedShadowSoftness, selectedShadowSoftnessFalloff);
                SyncShadowFieldsFromSelectedLight();
                selectedLightChanged |= selectedShadowSettingsChanged;

                if (selectedLight.lightType == Light2D.LightType.Point)
                {
                    float selectedInnerRadius = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 120f, 20f), selectedLight.pointLightInnerRadius, 0f, 12f);
                    if (!Mathf.Approximately(selectedLight.pointLightInnerRadius, selectedInnerRadius))
                    {
                        selectedLight.pointLightInnerRadius = selectedInnerRadius;
                        selectedLightChanged = true;
                    }

                    GUI.Label(new Rect(0f, y, 66f, 24f), "内半径", bodyStyle);
                    GUI.Label(new Rect(viewRect.width - 44f, y, 44f, 24f), selectedLight.pointLightInnerRadius.ToString("0.0"), smallBodyStyle);
                    y += 30f;
                    float selectedOuterRadius = GUI.HorizontalSlider(new Rect(70f, y + 7f, viewRect.width - 120f, 20f), Mathf.Max(selectedLight.pointLightInnerRadius + 0.1f, selectedLight.pointLightOuterRadius), selectedLight.pointLightInnerRadius + 0.1f, 24f);
                    if (!Mathf.Approximately(selectedLight.pointLightOuterRadius, selectedOuterRadius))
                    {
                        selectedLight.pointLightOuterRadius = selectedOuterRadius;
                        selectedLightChanged = true;
                    }

                    GUI.Label(new Rect(0f, y, 66f, 24f), "外半径", bodyStyle);
                    GUI.Label(new Rect(viewRect.width - 44f, y, 44f, 24f), selectedLight.pointLightOuterRadius.ToString("0.0"), smallBodyStyle);
                    y += 30f;
                }

                if (selectedLightChanged)
                {
                    ScheduleProjectSync();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawFloorPanel()
        {
            GUI.Box(floorPanelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(floorPanelRect.x + 12f, floorPanelRect.y + 12f, floorPanelRect.width - 24f, 40f), "楼层", headerStyle);
            Rect listRect = new Rect(floorPanelRect.x + 12f, floorPanelRect.y + 60f, floorPanelRect.width - 24f, floorPanelRect.height - 110f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(180f, mapRoot != null ? mapRoot.Floors.Count * 40f : 180f));
            floorScroll = GUI.BeginScrollView(listRect, floorScroll, viewRect);
            if (mapRoot != null)
            {
                mapRoot.RebuildFloorReferences();
                for (int i = mapRoot.Floors.Count - 1; i >= 0; i--)
                {
                    CampusFloorRoot floor = mapRoot.Floors[i];
                    if (floor == null)
                    {
                        continue;
                    }

                    float y = (mapRoot.Floors.Count - 1 - i) * 40f;
                    string label = "楼层" + floor.FloorIndex + (floor.IsUnlocked ? string.Empty : "  锁定");
                    if (GUI.Button(new Rect(0f, y, viewRect.width, 34f), label, selectedFloorIndex == floor.FloorIndex ? selectedButtonStyle : buttonStyle))
                    {
                        selectedFloorIndex = floor.FloorIndex;
                        mapRoot.CurrentPreviewFloor = selectedFloorIndex;
                    }
                }
            }

            GUI.EndScrollView();
            float buttonY = floorPanelRect.yMax - 42f;
            if (GUI.Button(new Rect(floorPanelRect.x + 12f, buttonY, 78f, 30f), "新增", buttonStyle))
            {
                RecordUndo();
                selectedFloorIndex = mapRoot.GetHighestFloorIndex() + 1;
                EnsureFloor(selectedFloorIndex);
            }

            if (GUI.Button(new Rect(floorPanelRect.x + 96f, buttonY, 78f, 30f), "锁定", buttonStyle))
            {
                CampusFloorRoot floor = EnsureFloor(selectedFloorIndex);
                if (floor != null)
                {
                    RecordUndo();
                    floor.IsUnlocked = !floor.IsUnlocked;
                }
            }

            if (GUI.Button(new Rect(floorPanelRect.x + 180f, buttonY, 78f, 30f), "删除", buttonStyle))
            {
                DeleteSelectedFloor();
            }
        }

        private void DrawChecklistPanel()
        {
            GUI.Box(checklistPanelRect, GUIContent.none, panelStyle);
            GUI.Box(new Rect(checklistPanelRect.x + 12f, checklistPanelRect.y + 12f, checklistPanelRect.width - 24f, 40f), "清单", headerStyle);
            Rect listRect = new Rect(checklistPanelRect.x + 12f, checklistPanelRect.y + 62f, checklistPanelRect.width - 24f, checklistPanelRect.height - 78f);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, roomNames.Count * 30f));
            checklistScroll = GUI.BeginScrollView(listRect, checklistScroll, viewRect);
            if (roomNames.Count == 0)
            {
                GUI.Label(new Rect(0f, 0f, viewRect.width, 58f), "未添加房间类型。", mutedStyle);
            }

            for (int i = 0; i < roomNames.Count; i++)
            {
                int count = CountRoomMarkers(roomNames[i]);
                int required = roomRequiredCounts[i];
                string label = roomNames[i];
                string value = count + "/" + required;
                GUI.Label(new Rect(0f, i * 30f, viewRect.width - 66f, 28f), label, bodyStyle);
                GUI.Label(new Rect(viewRect.width - 66f, i * 30f, 62f, 28f), value, count >= required ? bodyStyle : warningStyle);
            }

            GUI.EndScrollView();
        }

        private void DrawBottomToolbar()
        {
            GUI.Box(bottomToolbarRect, GUIContent.none, panelStyle);
            float x = bottomToolbarRect.x + 12f;
            float y = bottomToolbarRect.y + 10f;
            DrawToolbarButton(ref x, y, "退出", delegate { isOpen = false; });
            DrawToolbarButton(ref x, y, "帮助", delegate { showHelpOverlay = !showHelpOverlay; });
            DrawToolbarButton(ref x, y, "播放", delegate { isOpen = false; });
            DrawToolbarButton(ref x, y, "导入", ImportLatestJson);
            DrawToolbarButton(ref x, y, "导出", ExportToJson);
            DrawToolbarButton(ref x, y, "撤销", UndoSnapshot, undoSnapshots.Count > 0);
            DrawToolbarButton(ref x, y, "重做", RedoSnapshot, redoSnapshots.Count > 0);

            float rightX = bottomToolbarRect.xMax - 268f;
            if (GUI.Button(new Rect(rightX, y, 78f, 38f), showGridOverlay ? "网格开" : "网格关", showGridOverlay ? selectedButtonStyle : buttonStyle))
            {
                showGridOverlay = !showGridOverlay;
            }

            if (GUI.Button(new Rect(rightX + 88f, y, 78f, 38f), "设置", showSettings ? selectedButtonStyle : buttonStyle))
            {
                showSettings = !showSettings;
            }

            if (GUI.Button(new Rect(rightX + 176f, y, 78f, 38f), "重建", buttonStyle))
            {
                PrepareRuntimeMapPresentationSafe();
                SetStatus("已重建墙体视觉。");
            }
        }

        private void DrawSettingsPanel()
        {
            if (!showSettings)
            {
                return;
            }

            GUI.Box(settingsPanelRect, GUIContent.none, panelStyle);
            settingsScroll = GUI.BeginScrollView(new Rect(settingsPanelRect.x + 12f, settingsPanelRect.y + 12f, settingsPanelRect.width - 24f, settingsPanelRect.height - 24f), settingsScroll, new Rect(0f, 0f, settingsPanelRect.width - 42f, 460f));
            GUI.Label(new Rect(0f, 0f, settingsPanelRect.width - 42f, 26f), "运行时设置", headerStyle);
            GUI.Label(new Rect(0f, 38f, 122f, 24f), "导出目录", bodyStyle);
            GUI.Label(new Rect(0f, 66f, settingsPanelRect.width - 42f, 52f), GetExportFolder(), mutedStyle);
            GUI.Label(new Rect(0f, 122f, 122f, 24f), "导入目录", bodyStyle);
            GUI.Label(new Rect(0f, 150f, settingsPanelRect.width - 42f, 52f), GetImportRootFolder(), mutedStyle);
            if (GUI.Button(new Rect(0f, 210f, 112f, 30f), "打开导入", buttonStyle))
            {
                OpenImportLocation(GetImportRootFolder());
            }

            autoProjectSync = GUI.Toggle(new Rect(122f, 212f, 126f, 24f), autoProjectSync, "\u81ea\u52a8\u540c\u6b65");
            autoRestoreProjectSyncOnStart = GUI.Toggle(new Rect(252f, 212f, 150f, 24f), autoRestoreProjectSyncOnStart, "\u542f\u52a8\u81ea\u52a8\u6062\u590d");

            GUI.Label(new Rect(0f, 252f, settingsPanelRect.width - 42f, 24f), "\u9879\u76ee\u540c\u6b65\u76ee\u5f55", bodyStyle);
            GUI.Label(new Rect(0f, 280f, settingsPanelRect.width - 42f, 52f), GetProjectSyncRootFolder(), mutedStyle);
            if (GUI.Button(new Rect(0f, 340f, 112f, 30f), "\u540c\u6b65\u5230\u9879\u76ee", buttonStyle))
            {
                SyncRuntimeContentToProject();
            }

            if (GUI.Button(new Rect(122f, 340f, 112f, 30f), "\u4ece\u9879\u76ee\u6062\u590d", buttonStyle))
            {
                RestoreRuntimeContentFromProject();
            }

            if (GUI.Button(new Rect(244f, 340f, 112f, 30f), "\u6253\u5f00\u540c\u6b65", buttonStyle))
            {
                OpenImportLocation(GetProjectSyncRootFolder());
            }

            GUI.Label(new Rect(0f, 382f, 122f, 24f), "\u6392\u5e8f\u6b65\u957f", bodyStyle);
            if (mapRoot != null)
            {
                mapRoot.SortingOrderStepPerFloor = Mathf.Clamp(ParseIntField(new Rect(128f, 378f, 80f, 30f), mapRoot.SortingOrderStepPerFloor), 100, 5000);
            }

            GUI.EndScrollView();
        }

        private void DrawHelpPanel()
        {
            if (!showHelpOverlay)
            {
                return;
            }

            GUI.Box(helpPanelRect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(helpPanelRect.x + 18f, helpPanelRect.y + 16f, helpPanelRect.width - 36f, 32f), "运行时地图编辑器", headerStyle);
            string text =
                "F10：打开/关闭编辑器\n" +
                "左键：按当前刷子放置或绘制\n" +
                "鼠标中键拖拽 / Space+左键：平移视图\n" +
                "滚轮：以鼠标位置缩放视图\n" +
                "右键 / Shift+左键：擦除当前格\n" +
                "R：旋转物品/楼梯/光源\n" +
                "[ / ]：调整刷子尺寸\n" +
                "Ctrl+Z / Ctrl+Y：撤销 / 重做\n" +
                "Alt+左键放置光源：不吸附网格\n" +
                "导入：复制图片/文件夹路径后点“粘贴导入”\n" +
                "导出 JSON 会写入 Application.persistentDataPath/CampusMapExports";
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

            if (rectangleDragActive)
            {
                DrawCellRect(floor.Grid, rectangleStartCell, hoverCell, new Color(0.2f, 0.85f, 1f, 0.45f), 2f);
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
            else
            {
                Vector3Int end = new Vector3Int(hoverCell.x + Mathf.Max(1, brushSize) - 1, hoverCell.y + Mathf.Max(1, brushSize) - 1, 0);
                DrawCellRect(floor.Grid, hoverCell, end, new Color(1f, 0.96f, 0.25f, 0.55f), 2f);
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

            Vector3 worldCenter = CampusPlacedObject.GetFootprintWorldCenter(grid, anchor, footprint);
            Vector2 previewScale = placed != null ? placed.NormalizedVisualScale : new Vector2(renderer.transform.localScale.x, renderer.transform.localScale.y);
            Rect rect = BuildWorldPreviewRect(worldCenter, sprite, new Vector3(previewScale.x, previewScale.y, renderer.transform.localScale.z));
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float previewRotation = placed != null && placed.AllowRotation && !usesAuthoredDirectionalSprite ? -effectiveRotation90 * 90f : 0f;
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

        private float DrawTilePaletteGrid(List<TileBase> tiles, int selectedIndex, float y, float width, Action<int> onSelect)
        {
            if (tiles.Count == 0)
            {
                GUI.Label(new Rect(0f, y, width, 46f), "没有可用瓦片。请确认 Resources/NtingCampusRuntime 内有瓦片面板。", warningStyle);
                return y + 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            for (int i = 0; i < tiles.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(column * (PaletteTileSize + 10f), y + row * (PaletteTileSize + 22f), PaletteTileSize, PaletteTileSize + 16f);
                if (GUI.Button(cellRect, GUIContent.none, i == selectedIndex ? selectedButtonStyle : buttonStyle))
                {
                    onSelect(i);
                }

                Rect imageRect = new Rect(cellRect.x + 8f, cellRect.y + 8f, PaletteTileSize - 16f, PaletteTileSize - 16f);
                DrawTilePreview(imageRect, tiles[i]);
                GUI.Label(new Rect(cellRect.x + 4f, cellRect.y + PaletteTileSize - 2f, PaletteTileSize - 8f, 18f), Truncate(GetDisplayName(tiles[i]), 5), smallBodyStyle);
            }

            int rows = Mathf.CeilToInt((float)tiles.Count / columns);
            return y + rows * (PaletteTileSize + 22f);
        }

        private float DrawPrefabPaletteGrid(List<GameObject> prefabs, int selectedIndex, float y, float width, Action<int> onSelect)
        {
            if (prefabs.Count == 0)
            {
                GUI.Label(new Rect(0f, y, width, 46f), "没有可用物品。请确认 Resources/NtingCampusRuntime 内有物体资源面板。", warningStyle);
                return y + 56f;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt(width / (PaletteTileSize + 10f)));
            for (int i = 0; i < prefabs.Count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(column * (PaletteTileSize + 10f), y + row * (PaletteTileSize + 22f), PaletteTileSize, PaletteTileSize + 16f);
                if (GUI.Button(cellRect, GUIContent.none, i == selectedIndex ? selectedButtonStyle : buttonStyle))
                {
                    onSelect(i);
                }

                Rect imageRect = new Rect(cellRect.x + 8f, cellRect.y + 8f, PaletteTileSize - 16f, PaletteTileSize - 16f);
                DrawPrefabPreview(imageRect, prefabs[i]);
                GUI.Label(new Rect(cellRect.x + 4f, cellRect.y + PaletteTileSize - 2f, PaletteTileSize - 8f, 18f), Truncate(GetObjectDisplayName(prefabs[i]), 5), smallBodyStyle);
            }

            int rows = Mathf.CeilToInt((float)prefabs.Count / columns);
            return y + rows * (PaletteTileSize + 22f);
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
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), "光照示意", bodyStyle);
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
            GUI.Label(new Rect(rect.x + 54f, rect.y + 39f, rect.width - 140f, 24f), lightBrushType == Light2D.LightType.Global ? "全局光" : "点光源", bodyStyle);
            GUI.Label(new Rect(rect.x + 54f, rect.y + 66f, rect.width - 140f, 24f), "亮度 " + lightIntensity.ToString("0.0") + " / 外 " + lightOuterRadius.ToString("0.0") + " / 内 " + lightInnerRadius.ToString("0.0"), mutedStyle);
            y += 124f;
        }

        private void DrawDayNightControls(ref float y, float width)
        {
            if (dayNightController == null)
            {
                dayNightController = CampusDayNightController.EnsureSceneController(mapRoot);
            }

            GUI.Label(new Rect(0f, y, width, 26f), "日夜系统", headerStyle);
            y += 34f;

            if (dayNightController == null)
            {
                GUI.Label(new Rect(0f, y, width, 40f), "未找到日夜控制器", warningStyle);
                y += 48f;
                return;
            }

            GUI.Label(new Rect(0f, y, width, 24f), "当前时间 " + FormatGameTime(dayNightController.GameHour), bodyStyle);
            y += 30f;

            float speed = dayNightController.DaySpeedMultiplier;
            float editedSpeed = speed;
            y = DrawSlider(y, width, "日夜速度", ref editedSpeed, 0.1f, 200f);
            if (!Mathf.Approximately(speed, editedSpeed))
            {
                dayNightController.DaySpeedMultiplier = editedSpeed;
            }

            GUI.Label(new Rect(0f, y, width, 24f), "1x = 现实1秒推进游戏分钟；当前一天约 " + dayNightController.RealMinutesPerGameDay.ToString("0.0") + " 分钟", mutedStyle);
            y += 30f;

            float halfWidth = (width - 8f) * 0.5f;
            if (GUI.Button(new Rect(0f, y, halfWidth, 28f), "恢复1x", buttonStyle))
            {
                dayNightController.DaySpeedMultiplier = 1f;
            }

            if (GUI.Button(new Rect(halfWidth + 8f, y, halfWidth, 28f), "快进200x", buttonStyle))
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
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 26f), "新建墙体", headerStyle);

            float rowY = rect.y + 42f;
            DrawWallTexturePicker(rect.x + 12f, rowY, rect.width - 24f, "墙面贴图", customWallFaceTexture, CampusRuntimeImportTarget.WallFace);
            rowY += 56f;
            DrawWallTexturePicker(rect.x + 12f, rowY, rect.width - 24f, "墙顶贴图", customWallCapTexture, CampusRuntimeImportTarget.WallCap);
            rowY += 58f;

            GUI.Label(new Rect(rect.x + 12f, rowY, 78f, 28f), "名称", bodyStyle);
            customWallName = GUI.TextField(new Rect(rect.x + 92f, rowY, rect.width - 104f, 30f), customWallName, buttonStyle);
            rowY += 40f;

            float buttonWidth = (rect.width - 36f) / 2f;
            if (GUI.Button(new Rect(rect.x + 12f, rowY, buttonWidth, 30f), "新建墙体", buttonStyle))
            {
                CreateCustomWallProfile();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, rowY, buttonWidth, 30f), "应用到当前墙体", buttonStyle))
            {
                ApplyCustomTexturesToSelectedWall();
            }

            rowY += 36f;
            if (GUI.Button(new Rect(rect.x + 12f, rowY, buttonWidth, 30f), "重建当前楼层", buttonStyle))
            {
                RebuildWallVisuals(EnsureFloor(selectedFloorIndex));
                ScheduleProjectSync();
            }

            if (GUI.Button(new Rect(rect.x + 24f + buttonWidth, rowY, buttonWidth, 30f), "重建全部墙体", buttonStyle))
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
            if (GUI.Button(new Rect(buttonX, y + 10f, buttonWidth, 30f), "选择", buttonStyle))
            {
                string path = SelectSingleImageFile(label);
                if (!string.IsNullOrEmpty(path))
                {
                    LoadCustomWallTexture(path, target);
                }
            }

            GUIStyle targetStyle = activeImportTarget == target ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect(buttonX + buttonWidth + 8f, y + 10f, buttonWidth, 30f), "拖拽目标", targetStyle))
            {
                SetActiveImportTarget(target, label);
            }
        }

        private void DrawSelectedObjectSettingsLauncher(ref float y, float width)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            string name = prefab != null ? GetObjectDisplayName(prefab) : "\u672a\u9009\u7269\u54c1";

            bool previousEnabled = GUI.enabled;
            GUI.enabled = prefab != null;
            GUI.Box(new Rect(0f, y - 4f, width, 42f), GUIContent.none, objectSettingsHighlightStyle);
            if (GUI.Button(new Rect(8f, y, 156f, 34f), "\u7269\u54c1\u8bbe\u7f6e", showObjectSettings ? selectedButtonStyle : buttonStyle))
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
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 62f, 38f), "\u7269\u54c1\u8bbe\u7f6e", headerStyle);
            if (GUI.Button(new Rect(panelRect.xMax - 46f, panelRect.y + 12f, 32f, 32f), "X", buttonStyle))
            {
                showObjectSettings = false;
                return;
            }

            GameObject prefab = GetSelectedObjectPrefab();
            if (prefab == null)
            {
                GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 62f, panelRect.width - 36f, 40f), "\u8bf7\u5148\u9009\u62e9\u7269\u54c1\u3002", warningStyle);
                return;
            }

            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (placed == null)
            {
                GUI.Label(new Rect(panelRect.x + 18f, panelRect.y + 62f, panelRect.width - 36f, 70f), "\u8be5\u7269\u54c1\u7f3a\u5c11 CampusPlacedObject\uff0c\u8bf7\u5148\u5728\u9879\u76ee\u4e2d\u914d\u7f6e\u540e\u518d\u8bbe\u7f6e\u3002", warningStyle);
                return;
            }

            SyncObjectSettingsSelection(prefab, placed, false);

            float actionY = panelRect.y + 56f;
            if (GUI.Button(new Rect(panelRect.x + 14f, actionY, 132f, 32f), "\u4fdd\u5b58\u5e76\u540c\u6b65", buttonStyle))
            {
                CommitObjectSettingsDraft(prefab, placed);
                SaveSelectedObjectSettings();
            }

            if (GUI.Button(new Rect(panelRect.x + 154f, actionY, 184f, 32f), "\u4e00\u952e\u5e94\u7528\u5230\u573a\u4e0a\u540c\u7c7b", buttonStyle))
            {
                CommitObjectSettingsDraft(prefab, placed);
                ApplySelectedObjectSettingsToPlacedInstances();
            }

            GUI.Label(new Rect(panelRect.x + 348f, actionY + 2f, Mathf.Max(10f, panelRect.width - 408f), 30f), "\u7edf\u4e00\u5df2\u653e\u7f6e\u7269\u54c1", mutedStyle);

            Rect scrollRect = new Rect(panelRect.x + 14f, panelRect.y + 98f, panelRect.width - 28f, panelRect.height - 112f);
            float viewWidth = scrollRect.width - 22f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(scrollRect.height + 1f, 1360f));
            objectSettingsScroll = GUI.BeginScrollView(scrollRect, objectSettingsScroll, viewRect);
            float y = 0f;

            DrawObjectSettingsRenameControls(ref y, viewWidth, prefab, placed);
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
            objectSettingsPreviewRotation90 = 0;
            selectedObjectInteractionAnchorIndex = 0;
            if (placed != null)
            {
                placed.NormalizeCustomInteractionAnchors();
            }

            objectSettingsScroll = Vector2.zero;
            objectSettingsNameDraft = prefab != null ? GetObjectDisplayName(prefab) : string.Empty;
        }

        private static string BuildObjectSettingsInputKey(CampusPlacedObject placed, string suffix)
        {
            string owner = placed != null ? placed.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "none";
            return "object_settings_" + owner + "_" + suffix;
        }

        private void DrawObjectSettingsRenameControls(ref float y, float width, GameObject prefab, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 28f), "\u91cd\u547d\u540d", headerStyle);
            y += 36f;
            GUI.Label(new Rect(0f, y, 72f, 30f), "\u540d\u79f0", bodyStyle);
            objectSettingsNameDraft = DrawTextInput(new Rect(76f, y, width - 168f, 30f), objectSettingsNameDraft, BuildObjectSettingsInputKey(placed, "name"));
            if (GUI.Button(new Rect(width - 84f, y, 84f, 30f), "\u6062\u590d", buttonStyle))
            {
                objectSettingsNameDraft = GetObjectFallbackDisplayName(prefab);
                placed.DisplayNameOverride = string.Empty;
                textInputDrafts.Remove(BuildObjectSettingsInputKey(placed, "name"));
            }

            y += 42f;
        }

        private void DrawObjectSettingsPreviewControls(ref float y, float width, GameObject prefab, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 28f), "\u9884\u89c8", headerStyle);
            y += 34f;

            float gap = 8f;
            float buttonWidth = (width - gap * 3f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                Rect buttonRect = new Rect(i * (buttonWidth + gap), y, buttonWidth, 30f);
                if (GUI.Button(buttonRect, (i * 90).ToString() + "\u00b0", objectSettingsPreviewRotation90 == i ? selectedButtonStyle : buttonStyle))
                {
                    objectSettingsPreviewRotation90 = i;
                }
            }

            y += 38f;
            Rect previewRect = new Rect(0f, y, width, 242f);
            DrawObjectSettingsPreview(previewRect, prefab, placed);
            y += previewRect.height + 12f;
        }

        private void DrawObjectSettingsFootprintControls(ref float y, float width, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 28f), "\u5360\u683c\u8bbe\u7f6e", headerStyle);
            y += 36f;
            GUI.Label(new Rect(0f, y, 72f, 30f), "\u5360\u683c", bodyStyle);
            int nextX = Mathf.Clamp(ParseIntField(new Rect(76f, y, 58f, 30f), selectedObjectFootprintX, BuildObjectSettingsInputKey(placed, "footprint_x")), 1, 32);
            GUI.Label(new Rect(142f, y, 22f, 30f), "x", bodyStyle);
            int nextY = Mathf.Clamp(ParseIntField(new Rect(166f, y, 58f, 30f), selectedObjectFootprintY, BuildObjectSettingsInputKey(placed, "footprint_y")), 1, 32);
            if (nextX != selectedObjectFootprintX || nextY != selectedObjectFootprintY)
            {
                selectedObjectFootprintX = nextX;
                selectedObjectFootprintY = nextY;
                placed.FootprintSize = new Vector2Int(selectedObjectFootprintX, selectedObjectFootprintY);
                placed.OverrideFootprintSize = true;
                placed.ApplyRotationVisualState();
            }

            GUI.Label(new Rect(236f, y + 2f, Mathf.Max(10f, width - 236f), 28f), "\u9884\u89c8\u7f51\u683c\u4f1a\u6309\u5360\u683c\u66f4\u65b0", mutedStyle);
            y += 44f;
        }

        private void DrawObjectSettingsStorageControls(ref float y, float width, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 28f), "\u50a8\u7269\u5bb9\u5668", headerStyle);
            y += 34f;

            placed.NormalizeStorageSettings();
            bool nextEnabled = GUI.Toggle(new Rect(0f, y, 150f, 24f), placed.IsStorageContainer, "\u4f5c\u4e3a\u50a8\u7269\u5bb9\u5668");
            if (nextEnabled != placed.IsStorageContainer)
            {
                placed.IsStorageContainer = nextEnabled;
                if (placed.IsStorageContainer)
                {
                    EnsureStorageInteractionAnchor(placed);
                }

                placed.ApplyInteractionState();
            }

            GUI.Label(new Rect(160f, y, Mathf.Max(10f, width - 160f), 24f), "\u542f\u7528\u540e\u6253\u5f00\u65f6\u4f7f\u7528\u4e0b\u9762\u7684\u50a8\u7269\u7a7a\u95f4", mutedStyle);
            y += 32f;

            if (!placed.IsStorageContainer)
            {
                GUI.Label(new Rect(0f, y, width, 36f), "\u5bf9\u9700\u8981\u5b58\u653e\u7269\u54c1\u7684\u7bb1\u5b50\u3001\u684c\u809a\u3001\u67dc\u5b50\u7b49\u7269\u4f53\u6253\u5f00\u6b64\u9879\u3002", mutedStyle);
                y += 46f;
                return;
            }

            Vector2Int storageSize = placed.NormalizedStorageSize;
            GUI.Label(new Rect(0f, y, 72f, 30f), "\u7a7a\u95f4", bodyStyle);
            int nextColumns = Mathf.Clamp(ParseIntField(new Rect(76f, y, 58f, 30f), storageSize.x, BuildObjectSettingsInputKey(placed, "storage_columns")), 1, 12);
            GUI.Label(new Rect(142f, y, 22f, 30f), "x", bodyStyle);
            int nextRows = Mathf.Clamp(ParseIntField(new Rect(166f, y, 58f, 30f), storageSize.y, BuildObjectSettingsInputKey(placed, "storage_rows")), 1, 12);
            if (nextColumns != storageSize.x || nextRows != storageSize.y)
            {
                placed.StorageSize = new Vector2Int(nextColumns, nextRows);
                placed.NormalizeStorageSettings();
            }

            GUI.Label(new Rect(236f, y + 2f, Mathf.Max(10f, width - 236f), 28f), "\u6253\u5f00\u50a8\u7269\u7a97\u53e3\u65f6\u7684\u5217\u6570 x \u884c\u6570", mutedStyle);
            y += 38f;

            GUI.Label(new Rect(0f, y, 72f, 30f), "\u627f\u91cd", bodyStyle);
            float nextMaxWeight = Mathf.Clamp(ParseFloatField(new Rect(76f, y, 68f, 30f), placed.NormalizedStorageMaxWeight, BuildObjectSettingsInputKey(placed, "storage_weight")), 0f, 999f);
            if (!Mathf.Approximately(nextMaxWeight, placed.StorageMaxWeight))
            {
                placed.StorageMaxWeight = nextMaxWeight;
            }

            GUI.Label(new Rect(154f, y + 2f, Mathf.Max(10f, width - 154f), 28f), "kg\uff0c0 \u8868\u793a\u4e0d\u9650\u5236", mutedStyle);
            y += 44f;
        }

        private void DrawObjectSettingsScaleControls(ref float y, float width, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 28f), "\u8d34\u56fe\u7f29\u653e", headerStyle);
            y += 34f;

            bool nextLock = GUI.Toggle(new Rect(0f, y, width, 24f), placed.LockVisualScaleAspect, "\u7b49\u6bd4\u4f8b\u7f29\u653e");
            if (nextLock != placed.LockVisualScaleAspect)
            {
                placed.LockVisualScaleAspect = nextLock;
                if (placed.LockVisualScaleAspect)
                {
                    float uniform = Mathf.Max(placed.NormalizedVisualScale.x, placed.NormalizedVisualScale.y);
                    placed.VisualScale = new Vector2(uniform, uniform);
                }
            }

            y += 30f;
            Vector2 scale = placed.NormalizedVisualScale;
            if (placed.LockVisualScaleAspect)
            {
                GUI.Label(new Rect(0f, y, 72f, 30f), "\u6bd4\u4f8b", bodyStyle);
                float uniform = Mathf.Clamp(ParseFloatField(new Rect(76f, y, 68f, 30f), scale.x, BuildObjectSettingsInputKey(placed, "scale_uniform")), ObjectSettingsMinScale, ObjectSettingsMaxScale);
                uniform = GUI.HorizontalSlider(new Rect(154f, y + 9f, width - 210f, 18f), uniform, ObjectSettingsMinScale, ObjectSettingsMaxScale);
                GUI.Label(new Rect(width - 48f, y, 48f, 30f), uniform.ToString("0.##"), smallBodyStyle);
                scale = new Vector2(uniform, uniform);
                y += 38f;
            }
            else
            {
                scale.x = DrawObjectScaleAxis(y, width, "X", scale.x, BuildObjectSettingsInputKey(placed, "scale_x"));
                y += 34f;
                scale.y = DrawObjectScaleAxis(y, width, "Y", scale.y, BuildObjectSettingsInputKey(placed, "scale_y"));
                y += 38f;
            }

            Vector2 normalized = CampusPlacedObject.NormalizeVisualScale(scale);
            if (Vector2.Distance(placed.VisualScale, normalized) > 0.0001f)
            {
                placed.VisualScale = normalized;
                placed.ApplyRotationVisualState();
            }
        }

        private float DrawObjectScaleAxis(float y, float width, string label, float value, string key)
        {
            GUI.Label(new Rect(0f, y, 72f, 30f), label, bodyStyle);
            value = Mathf.Clamp(ParseFloatField(new Rect(76f, y, 68f, 30f), value, key), ObjectSettingsMinScale, ObjectSettingsMaxScale);
            value = GUI.HorizontalSlider(new Rect(154f, y + 9f, width - 210f, 18f), value, ObjectSettingsMinScale, ObjectSettingsMaxScale);
            GUI.Label(new Rect(width - 48f, y, 48f, 30f), value.ToString("0.##"), smallBodyStyle);
            return value;
        }

        private void DrawObjectSettingsRotationControls(ref float y, float width, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 28f), "\u56db\u5411\u65cb\u8f6c\u8bbe\u7f6e", headerStyle);
            y += 34f;

            bool nextAllowRotation = GUI.Toggle(new Rect(0f, y, width, 24f), placed.AllowRotation, "\u542f\u7528\u56db\u5411\u65cb\u8f6c");
            if (nextAllowRotation != placed.AllowRotation)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = nextAllowRotation;
                placed.ApplyRotationVisualState();
            }

            y += 32f;
            DrawObjectDirectionSpriteRow(ref y, width, placed, 0);
            DrawObjectDirectionSpriteRow(ref y, width, placed, 1);
            DrawObjectDirectionSpriteRow(ref y, width, placed, 2);
            DrawObjectDirectionSpriteRow(ref y, width, placed, 3);
            y += 8f;
        }

        private void DrawObjectSettingsAnchorControls(ref float y, float width, CampusPlacedObject placed)
        {
            GUI.Label(new Rect(0f, y, width, 28f), "\u4ea4\u4e92\u951a\u70b9", headerStyle);
            y += 34f;

            bool nextUseAnchor = GUI.Toggle(new Rect(0f, y, width, 24f), placed.UseCustomInteractionAnchor, "\u542f\u7528\u4ea4\u4e92\u951a\u70b9");
            if (nextUseAnchor != placed.UseCustomInteractionAnchor)
            {
                placed.UseCustomInteractionAnchor = nextUseAnchor;
                if (placed.UseCustomInteractionAnchor)
                {
                    EnsureSelectedObjectInteractionAnchor(placed);
                }

                placed.ApplyInteractionState();
            }

            y += 30f;
            if (!placed.UseCustomInteractionAnchor)
            {
                GUI.Label(new Rect(0f, y, width, 40f), "\u542f\u7528\u540e\u53ef\u6dfb\u52a0\u591a\u4e2a\u4ea4\u4e92\u951a\u70b9\u3002", mutedStyle);
                y += 48f;
                return;
            }

            EnsureSelectedObjectInteractionAnchor(placed);
            DrawObjectSettingsAnchorList(ref y, width, placed);

            CampusPlacedObjectInteractionAnchor anchor = GetSelectedObjectInteractionAnchor(placed);
            if (anchor == null)
            {
                return;
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = anchor.Enabled;
            string anchorKeyPrefix = "anchor_" + selectedObjectInteractionAnchorIndex + "_";
            GUI.Label(new Rect(0f, y, 72f, 30f), "\u540d\u79f0", bodyStyle);
            anchor.DisplayName = DrawTextInput(new Rect(76f, y, width - 76f, 30f), string.IsNullOrWhiteSpace(anchor.DisplayName) ? string.Empty : anchor.DisplayName, BuildObjectSettingsInputKey(placed, anchorKeyPrefix + "display_name"));
            y += 36f;

            GUI.Label(new Rect(0f, y, 72f, 30f), "\u63d0\u793a", bodyStyle);
            anchor.PromptText = DrawTextInput(new Rect(76f, y, width - 76f, 30f), string.IsNullOrWhiteSpace(anchor.PromptText) ? string.Empty : anchor.PromptText, BuildObjectSettingsInputKey(placed, anchorKeyPrefix + "prompt"));
            y += 36f;

            GUI.Label(new Rect(0f, y, 72f, 30f), "\u529f\u80fdID", bodyStyle);
            anchor.ActionId = DrawTextInput(new Rect(76f, y, width - 76f, 30f), string.IsNullOrWhiteSpace(anchor.ActionId) ? string.Empty : anchor.ActionId, BuildObjectSettingsInputKey(placed, anchorKeyPrefix + "action"));
            y += 36f;

            GUI.Label(new Rect(0f, y, 72f, 30f), "\u53c2\u6570", bodyStyle);
            anchor.Payload = DrawTextInput(new Rect(76f, y, width - 76f, 30f), string.IsNullOrWhiteSpace(anchor.Payload) ? string.Empty : anchor.Payload, BuildObjectSettingsInputKey(placed, anchorKeyPrefix + "payload"));
            y += 36f;

            Vector3 position = anchor.LocalPosition;
            GUI.Label(new Rect(0f, y, 28f, 30f), "X", bodyStyle);
            position.x = ParseFloatField(new Rect(30f, y, 68f, 30f), position.x, BuildObjectSettingsInputKey(placed, anchorKeyPrefix + "x"));
            GUI.Label(new Rect(106f, y, 28f, 30f), "Y", bodyStyle);
            position.y = ParseFloatField(new Rect(136f, y, 68f, 30f), position.y, BuildObjectSettingsInputKey(placed, anchorKeyPrefix + "y"));
            GUI.Label(new Rect(212f, y, 28f, 30f), "R", bodyStyle);
            anchor.Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(ParseFloatField(new Rect(242f, y, 68f, 30f), anchor.Radius, BuildObjectSettingsInputKey(placed, anchorKeyPrefix + "radius")));
            if (Vector3.Distance(anchor.LocalPosition, position) > 0.0001f)
            {
                anchor.LocalPosition = position;
            }

            y += 38f;
            anchor.Enabled = GUI.Toggle(new Rect(0f, y, 98f, 24f), anchor.Enabled, "\u542f\u7528");
            anchor.LogInteraction = GUI.Toggle(new Rect(108f, y, 138f, 24f), anchor.LogInteraction, "\u65e0\u76ee\u6807\u65f6\u8bb0\u5f55");
            anchor.UseTargetDoorStatePrompt = GUI.Toggle(new Rect(256f, y, Mathf.Max(10f, width - 256f), 24f), anchor.UseTargetDoorStatePrompt, "\u4f7f\u7528\u95e8\u72b6\u6001\u63d0\u793a");
            GUI.enabled = previousEnabled;
            y += 34f;
            placed.NormalizeCustomInteractionAnchors();
            placed.ApplyInteractionState();
            GUI.Label(new Rect(0f, y, width, 58f), "\u70b9\u51fb\u4e0a\u65b9\u9884\u89c8\u7f51\u683c\u53ef\u8bbe\u7f6e\u5f53\u524d\u951a\u70b9\u4f4d\u7f6e\u3002\u529f\u80fdID\uff1aopen_storage / toggle_door / interact_target / log\u3002\u7bb1\u5b50\u548c\u95e8\u4e5f\u901a\u8fc7\u8fd9\u91cc\u7684\u529f\u80fdID\u7ed1\u5b9a\u3002", mutedStyle);
            y += 68f;
        }

        private void DrawObjectSettingsAnchorList(ref float y, float width, CampusPlacedObject placed)
        {
            float buttonWidth = Mathf.Max(70f, (width - 16f) / 3f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), "\u65b0\u589e\u951a\u70b9", buttonStyle))
            {
                AddObjectSettingsInteractionAnchor(placed);
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), "\u5220\u9664\u5f53\u524d", buttonStyle))
            {
                RemoveSelectedObjectInteractionAnchor(placed);
            }

            GUI.Label(new Rect((buttonWidth + 8f) * 2f, y + 2f, Mathf.Max(10f, width - (buttonWidth + 8f) * 2f), 28f), "\u591a\u951a\u70b9", mutedStyle);
            y += 38f;

            int count = placed.CustomInteractionAnchors != null ? placed.CustomInteractionAnchors.Count : 0;
            for (int i = 0; i < count; i++)
            {
                CampusPlacedObjectInteractionAnchor anchor = placed.CustomInteractionAnchors[i];
                string label = anchor != null && !string.IsNullOrWhiteSpace(anchor.DisplayName)
                    ? anchor.DisplayName.Trim()
                    : "\u951a\u70b9 " + (i + 1);
                if (GUI.Button(new Rect(0f, y, width, 28f), Truncate(label, 24), i == selectedObjectInteractionAnchorIndex ? selectedButtonStyle : buttonStyle))
                {
                    selectedObjectInteractionAnchorIndex = i;
                }

                y += 32f;
            }

            y += 4f;
        }

        private void EnsureSelectedObjectInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            placed.NormalizeCustomInteractionAnchors();
            if (placed.CustomInteractionAnchors == null)
            {
                placed.CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();
            }

            if (placed.UseCustomInteractionAnchor && placed.CustomInteractionAnchors.Count == 0)
            {
                AddObjectSettingsInteractionAnchor(placed);
            }

            selectedObjectInteractionAnchorIndex = placed.CustomInteractionAnchors.Count > 0
                ? Mathf.Clamp(selectedObjectInteractionAnchorIndex, 0, placed.CustomInteractionAnchors.Count - 1)
                : 0;
        }

        private void EnsureStorageInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            if (placed.CustomInteractionAnchors == null)
            {
                placed.CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();
            }

            placed.IsInteractable = true;
            placed.UseCustomInteractionAnchor = true;
            for (int i = 0; i < placed.CustomInteractionAnchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor existing = placed.CustomInteractionAnchors[i];
                if (existing != null && CampusInteractionActionIds.Equals(existing.ActionId, CampusInteractionActionIds.OpenStorage))
                {
                    selectedObjectInteractionAnchorIndex = i;
                    return;
                }
            }

            Vector2Int footprint = placed.NormalizedFootprintSize;
            placed.CustomInteractionAnchors.Add(new CampusPlacedObjectInteractionAnchor
            {
                AnchorId = "storage",
                DisplayName = "\u50a8\u7269\u5bb9\u5668",
                Enabled = true,
                LocalPosition = new Vector3(0f, Mathf.Max(0.5f, footprint.y * 0.5f), 0f),
                Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(Mathf.Max(0.65f, Mathf.Max(footprint.x, footprint.y) * 0.35f)),
                PromptText = "\u6253\u5f00 " + placed.DisplayName,
                ActionId = CampusInteractionActionIds.OpenStorage,
                Priority = 130,
                LogInteraction = false
            });
            selectedObjectInteractionAnchorIndex = placed.CustomInteractionAnchors.Count - 1;
        }

        private CampusPlacedObjectInteractionAnchor GetSelectedObjectInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return null;
            }

            selectedObjectInteractionAnchorIndex = Mathf.Clamp(selectedObjectInteractionAnchorIndex, 0, placed.CustomInteractionAnchors.Count - 1);
            return placed.CustomInteractionAnchors[selectedObjectInteractionAnchorIndex];
        }

        private void AddObjectSettingsInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            if (placed.CustomInteractionAnchors == null)
            {
                placed.CustomInteractionAnchors = new List<CampusPlacedObjectInteractionAnchor>();
            }

            int nextIndex = placed.CustomInteractionAnchors.Count + 1;
            placed.CustomInteractionAnchors.Add(new CampusPlacedObjectInteractionAnchor
            {
                AnchorId = "custom_" + nextIndex,
                DisplayName = "\u951a\u70b9 " + nextIndex,
                Enabled = true,
                LocalPosition = Vector3.zero,
                Radius = CampusPlacedObject.NormalizeInteractionAnchorRadius(placed.CustomInteractionAnchorRadius),
                PromptText = string.IsNullOrWhiteSpace(placed.CustomInteractionPromptText) ? "\u4ea4\u4e92" : placed.CustomInteractionPromptText,
                Priority = 120,
                LogInteraction = true
            });

            placed.UseCustomInteractionAnchor = true;
            selectedObjectInteractionAnchorIndex = placed.CustomInteractionAnchors.Count - 1;
            placed.ApplyInteractionState();
        }

        private void RemoveSelectedObjectInteractionAnchor(CampusPlacedObject placed)
        {
            if (placed == null || placed.CustomInteractionAnchors == null || placed.CustomInteractionAnchors.Count == 0)
            {
                return;
            }

            selectedObjectInteractionAnchorIndex = Mathf.Clamp(selectedObjectInteractionAnchorIndex, 0, placed.CustomInteractionAnchors.Count - 1);
            placed.CustomInteractionAnchors.RemoveAt(selectedObjectInteractionAnchorIndex);
            selectedObjectInteractionAnchorIndex = Mathf.Clamp(selectedObjectInteractionAnchorIndex, 0, Mathf.Max(0, placed.CustomInteractionAnchors.Count - 1));
            placed.UseCustomInteractionAnchor = placed.CustomInteractionAnchors.Count > 0;
            placed.ApplyInteractionState();
        }

        private void DrawObjectSettingsPreview(Rect rect, GameObject prefab, CampusPlacedObject placed)
        {
            GUI.Box(rect, GUIContent.none, buttonStyle);
            GUI.BeginGroup(rect);
            Rect localRect = new Rect(0f, 0f, rect.width, rect.height);
            int effectiveRotation90 = placed != null ? placed.ResolveAllowedRotation90(objectSettingsPreviewRotation90) : 0;
            Vector2Int footprint = placed != null ? placed.NormalizedFootprintSize : Vector2Int.one;
            Vector2Int previewFootprint = CampusPlacedObject.RotateFootprintSize(footprint, effectiveRotation90);
            Rect gridRect = BuildObjectSettingsGridRect(localRect, previewFootprint, out float cellSize);
            DrawObjectSettingsPreviewGrid(gridRect, previewFootprint, cellSize);

            Sprite sprite = null;
            bool usesAuthoredDirectionalSprite = false;
            if (placed != null)
            {
                sprite = placed.ResolveSpriteForRotation(objectSettingsPreviewRotation90, out usesAuthoredDirectionalSprite, out effectiveRotation90);
            }

            if (sprite == null)
            {
                sprite = GetPrefabSprite(prefab);
            }

            if (sprite != null)
            {
                Rect spriteRect = BuildObjectSettingsSpriteRect(gridRect, cellSize, sprite, placed != null ? placed.NormalizedVisualScale : Vector2.one);
                Matrix4x4 oldMatrix = GUI.matrix;
                float previewRotation = placed != null && placed.AllowRotation && !usesAuthoredDirectionalSprite ? -effectiveRotation90 * 90f : 0f;
                if (!Mathf.Approximately(previewRotation, 0f))
                {
                    GUIUtility.RotateAroundPivot(previewRotation, spriteRect.center);
                }

                DrawSprite(spriteRect, sprite);
                GUI.matrix = oldMatrix;
            }

            if (placed != null && placed.UseCustomInteractionAnchor)
            {
                HandleObjectSettingsAnchorPreviewClick(gridRect, cellSize, placed, effectiveRotation90);
                DrawObjectSettingsAnchorMarkers(gridRect, cellSize, placed, effectiveRotation90);
            }

            GUI.EndGroup();
        }

        private Rect BuildObjectSettingsGridRect(Rect rect, Vector2Int footprint, out float cellSize)
        {
            footprint = CampusPlacedObject.NormalizeFootprintSize(footprint);
            Rect available = new Rect(rect.x + 16f, rect.y + 16f, Mathf.Max(10f, rect.width - 32f), Mathf.Max(10f, rect.height - 32f));
            cellSize = Mathf.Floor(Mathf.Min(available.width / footprint.x, available.height / footprint.y));
            cellSize = Mathf.Max(12f, cellSize);
            float gridWidth = cellSize * footprint.x;
            float gridHeight = cellSize * footprint.y;
            return new Rect(available.center.x - gridWidth * 0.5f, available.center.y - gridHeight * 0.5f, gridWidth, gridHeight);
        }

        private void DrawObjectSettingsPreviewGrid(Rect gridRect, Vector2Int footprint, float cellSize)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.12f, 0.17f, 0.22f, 0.92f);
            GUI.DrawTexture(gridRect, lineTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.24f);
            for (int x = 0; x <= footprint.x; x++)
            {
                float lineX = gridRect.x + x * cellSize;
                GUI.DrawTexture(new Rect(lineX - 0.5f, gridRect.y, 1f, gridRect.height), lineTexture);
            }

            for (int y = 0; y <= footprint.y; y++)
            {
                float lineY = gridRect.y + y * cellSize;
                GUI.DrawTexture(new Rect(gridRect.x, lineY - 0.5f, gridRect.width, 1f), lineTexture);
            }

            GUI.color = new Color(1f, 0.86f, 0.28f, 0.75f);
            GUI.DrawTexture(new Rect(gridRect.x, gridRect.y, gridRect.width, 2f), lineTexture);
            GUI.DrawTexture(new Rect(gridRect.x, gridRect.yMax - 2f, gridRect.width, 2f), lineTexture);
            GUI.DrawTexture(new Rect(gridRect.x, gridRect.y, 2f, gridRect.height), lineTexture);
            GUI.DrawTexture(new Rect(gridRect.xMax - 2f, gridRect.y, 2f, gridRect.height), lineTexture);
            GUI.color = oldColor;
        }

        private Rect BuildObjectSettingsSpriteRect(Rect gridRect, float cellSize, Sprite sprite, Vector2 visualScale)
        {
            if (sprite == null)
            {
                return gridRect;
            }

            visualScale = CampusPlacedObject.NormalizeVisualScale(visualScale);
            Vector2 spriteWorldSize = sprite.bounds.size;
            float width = Mathf.Max(1f, Mathf.Abs(spriteWorldSize.x * visualScale.x * cellSize));
            float height = Mathf.Max(1f, Mathf.Abs(spriteWorldSize.y * visualScale.y * cellSize));
            return new Rect(gridRect.center.x - width * 0.5f, gridRect.center.y - height * 0.5f, width, height);
        }

        private void HandleObjectSettingsAnchorPreviewClick(Rect gridRect, float cellSize, CampusPlacedObject placed, int rotation90Index)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 || !gridRect.Contains(current.mousePosition))
            {
                return;
            }

            EnsureSelectedObjectInteractionAnchor(placed);
            CampusPlacedObjectInteractionAnchor anchor = GetSelectedObjectInteractionAnchor(placed);
            if (anchor == null)
            {
                return;
            }

            Vector2 local = current.mousePosition;
            Vector2 previewLocal = new Vector2((local.x - gridRect.center.x) / cellSize, (gridRect.center.y - local.y) / cellSize);
            anchor.LocalPosition = PreviewLocalToObjectLocal(previewLocal, rotation90Index);
            placed.UseCustomInteractionAnchor = true;
            placed.ApplyInteractionState();
            CommitObjectSettingsDraft(GetSelectedObjectPrefab(), placed);
            SaveSelectedObjectSettings();
            current.Use();
        }

        private void DrawObjectSettingsAnchorMarkers(Rect gridRect, float cellSize, CampusPlacedObject placed, int rotation90Index)
        {
            if (placed == null || placed.CustomInteractionAnchors == null)
            {
                return;
            }

            CampusPlacedObjectInteractionAnchor anchor = GetSelectedObjectInteractionAnchor(placed);
            if (anchor == null || !anchor.Enabled)
            {
                return;
            }

            DrawObjectSettingsAnchorMarker(gridRect, cellSize, anchor.LocalPosition, rotation90Index, true);
        }

        private void DrawObjectSettingsAnchorMarker(Rect gridRect, float cellSize, Vector3 anchor, int rotation90Index, bool selected)
        {
            Vector2 previewLocal = ObjectLocalToPreviewLocal(anchor, rotation90Index);
            Vector2 point = new Vector2(gridRect.center.x + previewLocal.x * cellSize, gridRect.center.y - previewLocal.y * cellSize);
            Color oldColor = GUI.color;
            GUI.color = selected ? new Color(1f, 0.9f, 0.25f, 1f) : new Color(1f, 0.4f, 0.24f, 0.88f);
            float size = selected ? 18f : 14f;
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - 1f, size, 2f), lineTexture);
            GUI.DrawTexture(new Rect(point.x - 1f, point.y - size * 0.5f, 2f, size), lineTexture);
            GUI.DrawTexture(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), lineTexture);
            GUI.color = oldColor;
        }

        private static Vector2 ObjectLocalToPreviewLocal(Vector3 localPosition, int rotation90Index)
        {
            switch (CampusPlacedObject.NormalizeRotation90(rotation90Index))
            {
                case 1:
                    return new Vector2(-localPosition.y, localPosition.x);
                case 2:
                    return new Vector2(-localPosition.x, -localPosition.y);
                case 3:
                    return new Vector2(localPosition.y, -localPosition.x);
                default:
                    return new Vector2(localPosition.x, localPosition.y);
            }
        }

        private static Vector3 PreviewLocalToObjectLocal(Vector2 previewLocal, int rotation90Index)
        {
            switch (CampusPlacedObject.NormalizeRotation90(rotation90Index))
            {
                case 1:
                    return new Vector3(previewLocal.y, -previewLocal.x, 0f);
                case 2:
                    return new Vector3(-previewLocal.x, -previewLocal.y, 0f);
                case 3:
                    return new Vector3(-previewLocal.y, previewLocal.x, 0f);
                default:
                    return new Vector3(previewLocal.x, previewLocal.y, 0f);
            }
        }

        private void CommitObjectSettingsDraft(GameObject prefab, CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            string fallbackName = GetObjectFallbackDisplayName(prefab);
            string trimmedName = string.IsNullOrWhiteSpace(objectSettingsNameDraft) ? string.Empty : objectSettingsNameDraft.Trim();
            placed.DisplayNameOverride = string.IsNullOrEmpty(trimmedName) || trimmedName == fallbackName ? string.Empty : trimmedName;
            placed.FootprintSize = new Vector2Int(Mathf.Clamp(selectedObjectFootprintX, 1, 32), Mathf.Clamp(selectedObjectFootprintY, 1, 32));
            placed.OverrideFootprintSize = true;
            placed.VisualScale = CampusPlacedObject.NormalizeVisualScale(placed.VisualScale);
            if (placed.LockVisualScaleAspect)
            {
                float uniform = Mathf.Max(placed.VisualScale.x, placed.VisualScale.y);
                placed.VisualScale = new Vector2(uniform, uniform);
            }

            placed.NormalizeStorageSettings();
            if (placed.IsStorageContainer)
            {
                EnsureStorageInteractionAnchor(placed);
            }

            placed.NormalizeCustomInteractionAnchors();
        }

        private void DrawSelectedObjectFootprintControls(ref float y, float width)
        {
            SyncSelectedObjectFootprintFields();
            GUI.Label(new Rect(0f, y, 70f, 28f), "占格", bodyStyle);
            selectedObjectFootprintX = Mathf.Clamp(ParseIntField(new Rect(72f, y, 48f, 30f), selectedObjectFootprintX), 1, 32);
            GUI.Label(new Rect(126f, y, 20f, 28f), "x", bodyStyle);
            selectedObjectFootprintY = Mathf.Clamp(ParseIntField(new Rect(148f, y, 48f, 30f), selectedObjectFootprintY), 1, 32);
            if (GUI.Button(new Rect(208f, y, 92f, 30f), "应用占格", buttonStyle))
            {
                ApplySelectedObjectFootprint();
            }

            GameObject prefab = GetSelectedObjectPrefab();
            string name = prefab != null ? CampusObjectNames.GetDisplayName(prefab.name) : "未选物品";
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

            GUI.Label(new Rect(0f, y, width, 26f), "\u7269\u54c1\u8bbe\u7f6e", headerStyle);
            y += 32f;

            bool nextAllowRotation = GUI.Toggle(new Rect(0f, y, width, 24f), placed.AllowRotation, "\u542f\u7528\u56db\u5411\u65cb\u8f6c");
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
            bool nextUseAnchor = GUI.Toggle(new Rect(0f, y, width, 24f), placed.UseCustomInteractionAnchor, "\u542f\u7528\u4ea4\u4e92\u951a\u70b9");
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

            GUI.Label(new Rect(0f, y, 70f, 28f), "\u63d0\u793a", bodyStyle);
            placed.CustomInteractionPromptText = DrawTextInput(new Rect(72f, y, width - 72f, 30f), string.IsNullOrEmpty(placed.CustomInteractionPromptText) ? string.Empty : placed.CustomInteractionPromptText, BuildObjectSettingsInputKey(placed, "legacy_prompt"));
            GUI.enabled = previousEnabled;
            y += 40f;

            if (GUI.Button(new Rect(0f, y, Mathf.Min(150f, width), 30f), "\u4fdd\u5b58\u7269\u54c1\u8bbe\u7f6e", buttonStyle))
            {
                SaveSelectedObjectSettings();
            }

            GUI.Label(new Rect(160f, y, Mathf.Max(10f, width - 160f), 30f), "\u5df2\u653e\u7f6e\u7684\u540c\u540d\u7269\u54c1\u4f1a\u540c\u6b65", mutedStyle);
            y += 42f;
        }

        private void DrawObjectDirectionSpriteRow(ref float y, float width, CampusPlacedObject placed, int rotation90Index)
        {
            int degrees = rotation90Index * 90;
            GUI.Label(new Rect(0f, y, 42f, 28f), degrees.ToString(), bodyStyle);
            Sprite sprite = GetObjectDirectionSprite(placed, rotation90Index);
            string spriteName = sprite != null ? Truncate(sprite.name, 14) : "\u672a\u8bbe\u7f6e";
            GUI.Label(new Rect(46f, y, Mathf.Max(10f, width - 180f), 28f), spriteName, mutedStyle);

            if (GUI.Button(new Rect(width - 122f, y, 56f, 28f), "\u9009\u56fe", buttonStyle))
            {
                SetSelectedObjectDirectionSprite(rotation90Index);
            }

            if (GUI.Button(new Rect(width - 60f, y, 56f, 28f), "\u6e05\u7a7a", buttonStyle))
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
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), "选文件", buttonStyle))
            {
                ImportSelectedFilesIntoFolder(folder, label);
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), "选目录", buttonStyle))
            {
                ImportSelectedFolderIntoFolder(folder, label);
            }

            GUIStyle targetStyle = activeImportTarget == target ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect((buttonWidth + 8f) * 2f, y, buttonWidth, 30f), "拖拽目标", targetStyle))
            {
                SetActiveImportTarget(target, label);
            }

            if (GUI.Button(new Rect((buttonWidth + 8f) * 3f, y, buttonWidth, 30f), "粘贴", buttonStyle))
            {
                ImportClipboardImagesIntoFolder(folder, label);
            }

            y += 36f;
            if (GUI.Button(new Rect(0f, y, buttonWidth, 28f), "打开目录", buttonStyle))
            {
                OpenImportLocation(folder);
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 28f), "刷新", buttonStyle))
            {
                ReloadUserImportsFromUi();
                ScheduleProjectSync();
            }

            GUI.Label(new Rect((buttonWidth + 8f) * 2f, y, width - (buttonWidth + 8f) * 2f, 28f), activeImportTarget == target ? "当前拖拽目标" : Truncate(folder, 22), activeImportTarget == target ? warningStyle : mutedStyle);
            y += 38f;
        }

        private void DrawImportFileRow(ref float y, float width, string label, string filePath)
        {
            GUI.Label(new Rect(0f, y, width, 26f), label, headerStyle);
            y += 32f;
            float buttonWidth = Mathf.Max(68f, (width - 24f) / 4f);
            if (GUI.Button(new Rect(0f, y, buttonWidth, 30f), "选文件", buttonStyle))
            {
                string path = SelectSingleFile("选择房间文件", "Text|*.txt|All|*.*");
                if (!string.IsNullOrEmpty(path))
                {
                    RecordUndo();
                    int count = ImportRoomDefinitionsFromText(File.ReadAllText(path));
                    SetStatus(count > 0 ? "已导入" + count + " 个房间类型。" : "没有找到可导入房间。");
                }
            }

            if (GUI.Button(new Rect(buttonWidth + 8f, y, buttonWidth, 30f), "粘文本", buttonStyle))
            {
                ImportClipboardRooms();
            }

            GUIStyle targetStyle = activeImportTarget == CampusRuntimeImportTarget.Room ? selectedButtonStyle : buttonStyle;
            if (GUI.Button(new Rect((buttonWidth + 8f) * 2f, y, buttonWidth, 30f), "拖拽目标", targetStyle))
            {
                SetActiveImportTarget(CampusRuntimeImportTarget.Room, label);
            }

            if (GUI.Button(new Rect((buttonWidth + 8f) * 3f, y, buttonWidth, 30f), "打开", buttonStyle))
            {
                OpenImportLocation(Path.GetDirectoryName(filePath));
            }

            y += 36f;
            if (GUI.Button(new Rect(0f, y, buttonWidth, 28f), "刷新", buttonStyle))
            {
                LoadImportedRooms();
                ScheduleProjectSync();
                SetStatus("已刷新房间导入文件。");
            }

            GUI.Label(new Rect(buttonWidth + 8f, y, width - buttonWidth - 8f, 28f), activeImportTarget == CampusRuntimeImportTarget.Room ? "当前拖拽目标" : Truncate(filePath, 24), activeImportTarget == CampusRuntimeImportTarget.Room ? warningStyle : mutedStyle);
            y += 38f;
        }

        private void ImportClipboardImagesIntoFolder(string targetFolder, string label)
        {
            List<string> sources = GetClipboardImportSources();
            if (sources.Count == 0)
            {
                SetStatus("先复制图片文件或文件夹路径，再点击“粘贴导入”。");
                return;
            }

            int copied = CopyImportImages(sources, targetFolder);
            if (copied <= 0)
            {
                SetStatus("没有找到可导入图片。支持 png/jpg/jpeg/bmp。");
                return;
            }

            ReloadUserImportsFromUi();
            ScheduleProjectSync();
            SetStatus(label + "已导入" + copied + " 个资源。");
        }

        private void ImportSelectedFilesIntoFolder(string targetFolder, string label)
        {
            List<string> files = SelectImageFiles(label);
            if (files.Count == 0)
            {
                return;
            }

            ImportPathsIntoFolder(files, targetFolder, label);
        }

        private void ImportSelectedFolderIntoFolder(string targetFolder, string label)
        {
            string folder = SelectFolder(label);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            ImportPathsIntoFolder(new List<string> { folder }, targetFolder, label);
        }

        private void ImportDroppedPaths(List<string> paths)
        {
            if (paths == null || paths.Count == 0)
            {
                return;
            }

            switch (activeImportTarget)
            {
                case CampusRuntimeImportTarget.Floor:
                    ImportPathsIntoFolder(paths, GetFloorImportFolder(), activeImportLabel);
                    break;
                case CampusRuntimeImportTarget.Wall:
                    ImportPathsIntoFolder(paths, GetWallImportFolder(), activeImportLabel);
                    break;
                case CampusRuntimeImportTarget.Object:
                    ImportPathsIntoFolder(paths, GetObjectImportFolder(), activeImportLabel);
                    break;
                case CampusRuntimeImportTarget.Room:
                    ImportDroppedRoomPaths(paths);
                    break;
                case CampusRuntimeImportTarget.WallFace:
                case CampusRuntimeImportTarget.WallCap:
                    ImportDroppedWallTexture(paths, activeImportTarget);
                    break;
            }
        }

        private void ImportPathsIntoFolder(List<string> sources, string targetFolder, string label)
        {
            int copied = CopyImportImages(sources, targetFolder);
            if (copied <= 0)
            {
                SetStatus("没有找到可导入图片。支持 png/jpg/jpeg/bmp。");
                return;
            }

            ReloadUserImportsFromUi();
            ScheduleProjectSync();
            SetStatus(label + "已导入" + copied + " 个资源。");
        }

        private void ImportDroppedRoomPaths(List<string> paths)
        {
            int imported = 0;
            RecordUndo();
            for (int i = 0; i < paths.Count; i++)
            {
                string path = NormalizeClipboardPath(paths[i]);
                if (File.Exists(path))
                {
                    imported += ImportRoomDefinitionsFromText(File.ReadAllText(path));
                }
                else if (Directory.Exists(path))
                {
                    string[] files = Directory.GetFiles(path, "*.txt");
                    for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    {
                        imported += ImportRoomDefinitionsFromText(File.ReadAllText(files[fileIndex]));
                    }
                }
            }

            SetStatus(imported > 0 ? "已导入" + imported + " 个房间类型。" : "没有找到可导入房间文本。");
        }

        private void ImportDroppedWallTexture(List<string> paths, CampusRuntimeImportTarget target)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string image = FindFirstImagePath(paths[i]);
                if (string.IsNullOrEmpty(image))
                {
                    continue;
                }

                LoadCustomWallTexture(image, target);
                SetStatus((target == CampusRuntimeImportTarget.WallFace ? "墙面贴图" : "墙顶贴图") + "已载入。");
                return;
            }

            SetStatus("没有找到可用墙体贴图。");
        }

        private string FindFirstImagePath(string source)
        {
            string path = NormalizeClipboardPath(source);
            if (File.Exists(path) && IsSupportedImportImage(path))
            {
                return path;
            }

            if (!Directory.Exists(path))
            {
                return string.Empty;
            }

            string[] files = Directory.GetFiles(path);
            for (int i = 0; i < files.Length; i++)
            {
                if (IsSupportedImportImage(files[i]))
                {
                    return files[i];
                }
            }

            return string.Empty;
        }

        private void ImportClipboardRooms()
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clipboard))
            {
                SetStatus("先复制房间文本，或复制 Rooms.txt 路径。");
                return;
            }

            string path = NormalizeClipboardPath(clipboard);
            string roomText = File.Exists(path) ? File.ReadAllText(path) : clipboard;
            RecordUndo();
            int count = ImportRoomDefinitionsFromText(roomText);
            SetStatus(count > 0 ? "已导入" + count + " 个房间类型。" : "没有找到可导入房间。格式：房间名 或 房间名,数量。");
        }

        private int ImportRoomDefinitionsFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            int imported = 0;
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split(new char[] { ',', '，', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    continue;
                }

                int required = 1;
                if (parts.Length > 1)
                {
                    int.TryParse(parts[1].Trim(), out required);
                }

                AddOrUpdateRoomDefinition(parts[0].Trim(), Mathf.Max(0, required));
                imported++;
            }

            return imported;
        }

        private void SaveRuntimeRoomPrefab(CampusRuntimeRoomPrefab roomPrefab)
        {
            if (roomPrefab == null || string.IsNullOrWhiteSpace(roomPrefab.RoomName))
            {
                return;
            }

            NormalizeRoomPrefab(roomPrefab, roomPrefab.RoomName);
            Directory.CreateDirectory(GetRoomPrefabFolder());
            string fileName = SanitizeFileName(roomPrefab.RoomName) + ".json";
            string path = Path.Combine(GetRoomPrefabFolder(), fileName);
            File.WriteAllText(path, JsonUtility.ToJson(roomPrefab, true), Encoding.UTF8);
            roomPrefab.SourcePath = path;
        }

        private void DeleteSelectedRoomPrefab()
        {
            CampusRuntimeRoomPrefab roomPrefab = GetSelectedRoomPrefab();
            if (roomPrefab == null)
            {
                return;
            }

            string deletedName = roomPrefab.RoomName;
            string path = roomPrefab.SourcePath;
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(GetRoomPrefabFolder(), SanitizeFileName(roomPrefab.RoomName) + ".json");
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            LoadImportedRoomPrefabs();
            ScheduleProjectSync();
            SyncRuntimeContentToProject(false);
            SetStatus("已删除房间模块：" + deletedName);
        }

        private void SelectRoomPrefabByName(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return;
            }

            for (int i = 0; i < roomPrefabs.Count; i++)
            {
                if (roomPrefabs[i] != null && string.Equals(roomPrefabs[i].RoomName, roomName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedRoomPrefabIndex = i;
                    return;
                }
            }
        }

        private List<string> GetClipboardImportSources()
        {
            List<string> sources = new List<string>();
            string clipboard = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clipboard))
            {
                return sources;
            }

            string single = NormalizeClipboardPath(clipboard);
            if (File.Exists(single) || Directory.Exists(single))
            {
                sources.Add(single);
                return sources;
            }

            MatchCollection matches = Regex.Matches(clipboard, "\"([^\"]+)\"|([^\\r\\n;]+)");
            for (int i = 0; i < matches.Count; i++)
            {
                string value = matches[i].Groups[1].Success ? matches[i].Groups[1].Value : matches[i].Groups[2].Value;
                string path = NormalizeClipboardPath(value);
                if ((File.Exists(path) || Directory.Exists(path)) && !sources.Contains(path))
                {
                    sources.Add(path);
                }
            }

            return sources;
        }

        private int CopyImportImages(List<string> sources, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);
            int copied = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                string source = sources[i];
                if (File.Exists(source))
                {
                    copied += CopySingleImportImage(source, targetFolder) ? 1 : 0;
                    continue;
                }

                if (!Directory.Exists(source))
                {
                    continue;
                }

                string[] files = Directory.GetFiles(source);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    copied += CopySingleImportImage(files[fileIndex], targetFolder) ? 1 : 0;
                }
            }

            return copied;
        }

        private bool CopySingleImportImage(string sourcePath, string targetFolder)
        {
            if (!IsSupportedImportImage(sourcePath))
            {
                return false;
            }

            string targetPath = MakeUniqueImportPath(Path.Combine(targetFolder, Path.GetFileName(sourcePath)));
            File.Copy(sourcePath, targetPath, false);
            return true;
        }

        private void BackupLocalRuntimeImportFolder()
        {
            string importRoot = GetImportRootFolder();
            if (!Directory.Exists(importRoot))
            {
                return;
            }

            string backupRoot = Path.Combine(Application.persistentDataPath, RuntimeImportFolder + "_Backups");
            string backupPath = Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backupRoot);
            MirrorDirectory(importRoot, backupPath, false);
        }

        private void MirrorDirectory(string sourceFolder, string destinationFolder, bool clearDestination)
        {
            if (string.IsNullOrEmpty(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                return;
            }

            Directory.CreateDirectory(destinationFolder);
            if (clearDestination)
            {
                ClearDirectoryContents(destinationFolder);
            }

            CopyDirectoryContents(sourceFolder, destinationFolder);
        }

        private void CopyDirectoryContents(string sourceFolder, string destinationFolder)
        {
            Directory.CreateDirectory(destinationFolder);
            string[] files = Directory.GetFiles(sourceFolder);
            for (int i = 0; i < files.Length; i++)
            {
                string source = files[i];
                string destination = Path.Combine(destinationFolder, Path.GetFileName(source));
                File.Copy(source, destination, true);
            }

            string[] directories = Directory.GetDirectories(sourceFolder);
            for (int i = 0; i < directories.Length; i++)
            {
                string sourceDirectory = directories[i];
                string destinationDirectory = Path.Combine(destinationFolder, Path.GetFileName(sourceDirectory));
                CopyDirectoryContents(sourceDirectory, destinationDirectory);
            }
        }

        private void ClearDirectoryContents(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            string normalized = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string projectSyncImport = Path.GetFullPath(GetProjectSyncImportFolder()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string runtimeImport = Path.GetFullPath(GetImportRootFolder()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(normalized, projectSyncImport, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, runtimeImport, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to clear unexpected directory: " + folder);
            }

            string[] files = Directory.GetFiles(folder);
            for (int i = 0; i < files.Length; i++)
            {
                File.Delete(files[i]);
            }

            string[] directories = Directory.GetDirectories(folder);
            for (int i = 0; i < directories.Length; i++)
            {
                Directory.Delete(directories[i], true);
            }
        }

        private void RefreshAssetDatabaseIfAvailable()
        {
            long perf = CampusMapEditorPerformance.Begin();
            try
            {
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            }
            finally
            {
                CampusMapEditorPerformance.End(perf, "Runtime AssetDatabase.Refresh");
            }
        }

        private bool IsSupportedImportImage(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp";
        }

        private string MakeUniqueImportPath(string requestedPath)
        {
            if (!File.Exists(requestedPath))
            {
                return requestedPath;
            }

            string folder = Path.GetDirectoryName(requestedPath);
            string name = Path.GetFileNameWithoutExtension(requestedPath);
            string extension = Path.GetExtension(requestedPath);
            int index = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(folder, name + "_" + index + extension);
                index++;
            }
            while (File.Exists(candidate));

            return candidate;
        }

        private string NormalizeClipboardPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string path = value.Trim().Trim('"');
            if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    path = new Uri(path).LocalPath;
                }
                catch (UriFormatException)
                {
                    path = path.Substring("file:///".Length);
                }
            }

            return path;
        }

        private List<string> SelectImageFiles(string title)
        {
            string output = RunPowerShellDialog(
                "$d=New-Object System.Windows.Forms.OpenFileDialog;" +
                "$d.Title='" + EscapePowerShell(title) + "';" +
                "$d.Filter='Images|*.png;*.jpg;*.jpeg;*.bmp|All Files|*.*';" +
                "$d.Multiselect=$true;" +
                "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){$d.FileNames -join [Environment]::NewLine}");
            return ParseDialogLines(output);
        }

        private string SelectSingleImageFile(string title)
        {
            List<string> files = SelectImageFiles(title);
            return files.Count > 0 ? files[0] : string.Empty;
        }

        private string SelectSingleFile(string title, string filter)
        {
            string safeFilter = string.IsNullOrEmpty(filter) ? "All Files|*.*" : filter;
            string output = RunPowerShellDialog(
                "$d=New-Object System.Windows.Forms.OpenFileDialog;" +
                "$d.Title='" + EscapePowerShell(title) + "';" +
                "$d.Filter='" + EscapePowerShell(safeFilter) + "';" +
                "$d.Multiselect=$false;" +
                "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){$d.FileName}");
            List<string> files = ParseDialogLines(output);
            return files.Count > 0 ? files[0] : string.Empty;
        }

        private string SelectFolder(string title)
        {
            string output = RunPowerShellDialog(
                "$d=New-Object System.Windows.Forms.FolderBrowserDialog;" +
                "$d.Description='" + EscapePowerShell(title) + "';" +
                "if($d.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){$d.SelectedPath}");
            List<string> folders = ParseDialogLines(output);
            return folders.Count > 0 ? folders[0] : string.Empty;
        }

        private string RunPowerShellDialog(string scriptBody)
        {
            try
            {
                string script = "[Console]::OutputEncoding=[Text.UTF8Encoding]::new();" +
                                "Add-Type -AssemblyName System.Windows.Forms;" +
                                scriptBody;
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info))
                {
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] File dialog failed: " + exception.Message);
                SetStatus("当前平台无法打开选择窗口，请使用粘贴导入或拖拽导入。");
                return string.Empty;
            }
        }

        private List<string> ParseDialogLines(string output)
        {
            List<string> paths = new List<string>();
            if (string.IsNullOrWhiteSpace(output))
            {
                return paths;
            }

            string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string path = NormalizeClipboardPath(lines[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        private string EscapePowerShell(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("'", "''");
        }

        private void SetActiveImportTarget(CampusRuntimeImportTarget target, string label)
        {
            activeImportTarget = target;
            activeImportLabel = label;
            SetStatus("拖拽目标：" + label + "。可将文件或文件夹拖到游戏窗口。");
        }

        private void LoadCustomWallTexture(string path, CampusRuntimeImportTarget target)
        {
            if (!IsSupportedImportImage(path))
            {
                SetStatus("请选择 png/jpg/jpeg/bmp 图片。");
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
                SetStatus("请先选择墙面或墙顶贴图。");
                return;
            }

            string cleanName = string.IsNullOrWhiteSpace(customWallName) ? "Custom Wall" : customWallName.Trim();
            Sprite sprite = Sprite.Create(cap != null ? cap : face, new Rect(0f, 0f, (cap != null ? cap.width : face.width), (cap != null ? cap.height : face.height)), new Vector2(0.5f, 0.5f), Mathf.Max(1f, Mathf.Max(cap != null ? cap.width : face.width, cap != null ? cap.height : face.height)));
            sprite.name = cleanName + "_Preview";
            sprite.hideFlags = HideFlags.DontSave;
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = cleanName + "_WallLogic";
            tile.sprite = sprite;
            tile.hideFlags = HideFlags.DontSave;

            CampusWallRenderProfile profile = ScriptableObject.CreateInstance<CampusWallRenderProfile>();
            profile.name = cleanName + " 墙体配置";
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
            ScheduleProjectSync();
            SetStatus("已新建墙体：" + cleanName);
        }

        private void ApplyCustomTexturesToSelectedWall()
        {
            CampusWallRenderProfile profile = selectedWallProfileIndex >= 0 && selectedWallProfileIndex < wallProfiles.Count ? wallProfiles[selectedWallProfileIndex] : fallbackWallProfile;
            if (profile == null)
            {
                SetStatus("当前没有可应用的墙体配置。");
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
            ScheduleProjectSync();
            SetStatus("已应用墙体贴图。");
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
                SetStatus("请先选择物体。");
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
            SetStatus("已设置占格：" + placed.FootprintSize.x + "x" + placed.FootprintSize.y);
        }

        private void SetSelectedObjectDirectionSprite(int rotation90Index)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus("\u8bf7\u5148\u9009\u62e9\u7269\u54c1\u3002");
                return;
            }

            string sourcePath = SelectSingleImageFile("\u9009\u62e9 " + (rotation90Index * 90) + " \u5ea6\u8d34\u56fe");
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            try
            {
                string storedPath = CopyObjectDirectionSprite(prefab.name, rotation90Index, sourcePath);
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = true;
                AssignRuntimeObjectDirectionSprite(placed, rotation90Index, true, storedPath, prefab.name);
                placed.ApplyRotationVisualState();
                SaveSelectedObjectSettings();
                SetStatus("\u5df2\u8bbe\u7f6e\u65cb\u8f6c\u8d34\u56fe\uff1a" + (rotation90Index * 90));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NtingCampusRuntimeMapEditor] Failed to set object direction sprite: " + exception.Message);
                SetStatus("\u65cb\u8f6c\u8d34\u56fe\u8bbe\u7f6e\u5931\u8d25\u3002");
            }
        }

        private void ClearSelectedObjectDirectionSprite(int rotation90Index)
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus("\u8bf7\u5148\u9009\u62e9\u7269\u54c1\u3002");
                return;
            }

            AssignRuntimeObjectDirectionSprite(placed, rotation90Index, true, string.Empty, prefab.name);
            placed.ApplyRotationVisualState();
            SaveSelectedObjectSettings();
            SetStatus("\u5df2\u6e05\u7a7a\u65cb\u8f6c\u8d34\u56fe\uff1a" + (rotation90Index * 90));
        }

        private void SaveSelectedObjectSettings()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus("\u8bf7\u5148\u9009\u62e9\u7269\u54c1\u3002");
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
            ScheduleProjectSync();
            SetStatus("\u5df2\u4fdd\u5b58\u7269\u54c1\u8bbe\u7f6e\uff1a" + GetObjectDisplayName(prefab) + "\uff0c\u5df2\u540c\u6b65 " + appliedCount + " \u4e2a\u573a\u4e0a\u540c\u7c7b\u7269\u54c1\u3002");
        }

        private void ApplySelectedObjectSettingsToPlacedInstances()
        {
            GameObject prefab = GetSelectedObjectPrefab();
            CampusPlacedObject placed = EnsureRuntimePlacedObject(prefab);
            if (prefab == null || placed == null)
            {
                SetStatus("\u8bf7\u5148\u9009\u62e9\u7269\u54c1\u3002");
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
            ScheduleProjectSync();
            SetStatus("\u5df2\u5e94\u7528\u5230\u573a\u4e0a\u540c\u7c7b\u7269\u54c1\uff1a" + appliedCount + " \u4e2a " + GetObjectDisplayName(prefab));
        }

        private int ApplyObjectSettingsToPlacedInstances(GameObject prefab, CampusRuntimeObjectSettings settings, bool recordUndo)
        {
            if (prefab == null || settings == null || mapRoot == null)
            {
                return 0;
            }

            int appliedCount = 0;
            bool undoRecorded = false;
            mapRoot.RebuildFloorReferences();
            for (int floorIndex = 0; floorIndex < mapRoot.Floors.Count; floorIndex++)
            {
                CampusFloorRoot floor = mapRoot.Floors[floorIndex];
                if (floor == null || floor.PropsRoot == null)
                {
                    continue;
                }

                CampusPlacedObject[] objects = floor.PropsRoot.GetComponentsInChildren<CampusPlacedObject>(true);
                for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
                {
                    CampusPlacedObject placed = objects[objectIndex];
                    if (!DoesPlacedObjectMatchPrefab(placed, prefab))
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
                    if (floor.Grid != null)
                    {
                        placed.ApplyCellToTransform(floor.Grid);
                    }

                    appliedCount++;
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

            if (settings.OverrideFootprintSize)
            {
                placed.OverrideFootprintSize = true;
                placed.FootprintSize = CampusPlacedObject.NormalizeFootprintSize(settings.FootprintSize);
            }

            if (settings.OverrideAllowRotation)
            {
                placed.OverrideAllowRotation = true;
                placed.AllowRotation = settings.AllowRotation;
            }

            AssignRuntimeObjectDirectionSprite(placed, 0, settings.OverrideRotation0Sprite, settings.Rotation0SpritePath, target.name);
            AssignRuntimeObjectDirectionSprite(placed, 1, settings.OverrideRotation90Sprite, settings.Rotation90SpritePath, target.name);
            AssignRuntimeObjectDirectionSprite(placed, 2, settings.OverrideRotation180Sprite, settings.Rotation180SpritePath, target.name);
            AssignRuntimeObjectDirectionSprite(placed, 3, settings.OverrideRotation270Sprite, settings.Rotation270SpritePath, target.name);

            placed.UseCustomInteractionAnchor = settings.UseCustomInteractionAnchor;
            placed.CustomInteractionAnchorLocalPosition = settings.CustomInteractionAnchorLocalPosition;
            placed.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(settings.CustomInteractionAnchorRadius);
            placed.CustomInteractionPromptText = string.IsNullOrWhiteSpace(settings.CustomInteractionPromptText)
                ? "\u4ea4\u4e92"
                : settings.CustomInteractionPromptText;
            placed.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(settings.CustomInteractionAnchors);
            placed.IsStorageContainer = settings.IsStorageContainer;
            placed.StorageSize = CampusPlacedObject.NormalizeStorageSize(settings.StorageSize);
            placed.StorageMaxWeight = CampusPlacedObject.NormalizeStorageMaxWeight(settings.StorageMaxWeight);
            if (placed.IsStorageContainer)
            {
                EnsureStorageInteractionAnchor(placed);
            }

            if (placed.UseCustomInteractionAnchor)
            {
                placed.IsInteractable = true;
            }

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
            settings.DisplayNameOverride = string.IsNullOrWhiteSpace(placed.DisplayNameOverride) ? string.Empty : placed.DisplayNameOverride.Trim();
            settings.OverrideFootprintSize = placed.OverrideFootprintSize;
            settings.FootprintSize = placed.NormalizedFootprintSize;
            settings.VisualScale = placed.NormalizedVisualScale;
            settings.LockVisualScaleAspect = placed.LockVisualScaleAspect;
            settings.OverrideAllowRotation = placed.OverrideAllowRotation;
            settings.AllowRotation = placed.AllowRotation;
            settings.OverrideRotation0Sprite = placed.OverrideRotation0Sprite;
            settings.Rotation0SpritePath = placed.Rotation0SpritePath;
            settings.OverrideRotation90Sprite = placed.OverrideRotation90Sprite;
            settings.Rotation90SpritePath = placed.Rotation90SpritePath;
            settings.OverrideRotation180Sprite = placed.OverrideRotation180Sprite;
            settings.Rotation180SpritePath = placed.Rotation180SpritePath;
            settings.OverrideRotation270Sprite = placed.OverrideRotation270Sprite;
            settings.Rotation270SpritePath = placed.Rotation270SpritePath;
            settings.UseCustomInteractionAnchor = placed.UseCustomInteractionAnchor;
            settings.CustomInteractionAnchorLocalPosition = placed.CustomInteractionAnchorLocalPosition;
            settings.CustomInteractionAnchorRadius = CampusPlacedObject.NormalizeInteractionAnchorRadius(placed.CustomInteractionAnchorRadius);
            settings.CustomInteractionPromptText = string.IsNullOrWhiteSpace(placed.CustomInteractionPromptText)
                ? "\u4ea4\u4e92"
                : placed.CustomInteractionPromptText.Trim();
            settings.CustomInteractionAnchors = CampusPlacedObject.CloneInteractionAnchors(placed.CustomInteractionAnchors);
            settings.IsStorageContainer = placed.IsStorageContainer;
            settings.StorageSize = placed.NormalizedStorageSize;
            settings.StorageMaxWeight = placed.NormalizedStorageMaxWeight;
            return settings;
        }

        private void SaveRuntimeObjectSettings(CampusRuntimeObjectSettings settings)
        {
            long perf = CampusMapEditorPerformance.Begin();
            try
            {
            if (settings == null || string.IsNullOrWhiteSpace(settings.ObjectId))
            {
                return;
            }

            string folder = GetObjectSettingsFolder(settings.ObjectId);
            Directory.CreateDirectory(folder);
            File.WriteAllText(GetObjectSettingsPath(settings.ObjectId), JsonUtility.ToJson(settings, true), Encoding.UTF8);
            }
            finally
            {
                CampusMapEditorPerformance.End(perf, "Runtime SaveObjectSettings");
            }
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
            if (placed == null || !hasOverride)
            {
                return;
            }

            Sprite sprite = LoadRuntimeObjectSprite(spritePath, objectName + "_" + (rotation90Index * 90), placed.NormalizedFootprintSize);
            switch (CampusPlacedObject.NormalizeRotation90(rotation90Index))
            {
                case 0:
                    placed.OverrideRotation0Sprite = true;
                    placed.Rotation0SpritePath = spritePath;
                    placed.Rotation0Sprite = sprite;
                    break;
                case 1:
                    placed.OverrideRotation90Sprite = true;
                    placed.Rotation90SpritePath = spritePath;
                    placed.Rotation90Sprite = sprite;
                    break;
                case 2:
                    placed.OverrideRotation180Sprite = true;
                    placed.Rotation180SpritePath = spritePath;
                    placed.Rotation180Sprite = sprite;
                    break;
                case 3:
                    placed.OverrideRotation270Sprite = true;
                    placed.Rotation270SpritePath = spritePath;
                    placed.Rotation270Sprite = sprite;
                    break;
            }
        }

        private Sprite LoadRuntimeObjectSprite(string path, string spriteName, Vector2Int footprint)
        {
            string normalizedPath = NormalizeClipboardPath(path);
            if (string.IsNullOrEmpty(normalizedPath) || !File.Exists(normalizedPath))
            {
                return null;
            }

            Texture2D texture = LoadImportedTexture(normalizedPath);
            return texture != null ? CreateObjectSprite(texture, spriteName, footprint) : null;
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
                return targetPath;
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
            return targetPath;
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
                    SetStatus("\u8be5\u7269\u54c1\u9884\u5236\u4f53\u7f3a\u5c11 CampusPlacedObject\uff0c\u8bf7\u5148\u5728\u9879\u76ee\u4e2d\u914d\u7f6e\u3002");
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

        private bool DoesPlacedObjectMatchPrefab(CampusPlacedObject placed, GameObject prefab)
        {
            if (placed == null || prefab == null)
            {
                return false;
            }

            string prefabName = prefab.name;
            string prefabDisplayName = CampusObjectNames.GetDisplayName(prefabName);
            string objectId = string.IsNullOrWhiteSpace(placed.ObjectId) ? placed.gameObject.name : placed.ObjectId;
            string objectDisplayName = CampusObjectNames.GetDisplayName(objectId);
            return objectId == prefabName ||
                   objectDisplayName == prefabDisplayName ||
                   placed.gameObject.name.StartsWith(prefabDisplayName + "_F", StringComparison.Ordinal);
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

            SetStatus("导入路径已复制：" + path);
        }

        private void DrawToolbarButton(ref float x, float y, string label, Action action)
        {
            DrawToolbarButton(ref x, y, label, action, true);
        }

        private void DrawToolbarButton(ref float x, float y, string label, Action action, bool enabled)
        {
            GUI.enabled = enabled;
            if (GUI.Button(new Rect(x, y, ToolbarButtonWidth, 38f), label, buttonStyle))
            {
                action();
            }

            GUI.enabled = true;
            x += ToolbarButtonWidth + 8f;
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
            roomMarkerTexture = MakeRoomMarkerTexture();

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
            panelStyle.normal.textColor = Color.white;
            panelStyle.border = new RectOffset(4, 4, 4, 4);
            panelStyle.padding = new RectOffset(8, 8, 6, 6);
            panelStyle.alignment = TextAnchor.MiddleCenter;
            panelStyle.fontSize = 18;

            headerStyle = new GUIStyle(GUI.skin.box);
            headerStyle.normal.background = headerTexture;
            headerStyle.normal.textColor = Color.white;
            headerStyle.fontSize = 22;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.padding = new RectOffset(12, 8, 0, 0);

            bodyStyle = new GUIStyle(GUI.skin.label);
            bodyStyle.normal.textColor = Color.white;
            bodyStyle.fontSize = 20;
            bodyStyle.wordWrap = true;

            smallBodyStyle = new GUIStyle(GUI.skin.label);
            smallBodyStyle.normal.textColor = Color.white;
            smallBodyStyle.fontSize = 14;
            smallBodyStyle.alignment = TextAnchor.MiddleCenter;
            smallBodyStyle.clipping = TextClipping.Clip;

            mutedStyle = new GUIStyle(bodyStyle);
            mutedStyle.normal.textColor = new Color(0.76f, 0.84f, 0.93f, 1f);
            mutedStyle.fontSize = 16;

            warningStyle = new GUIStyle(bodyStyle);
            warningStyle.normal.textColor = new Color(1f, 0.86f, 0.58f, 1f);
            warningStyle.fontSize = 18;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = hoverTexture;
            buttonStyle.active.background = selectedTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.fontSize = 18;
            buttonStyle.alignment = TextAnchor.MiddleCenter;

            selectedButtonStyle = new GUIStyle(buttonStyle);
            selectedButtonStyle.normal.background = selectedTexture;

            iconButtonStyle = new GUIStyle(buttonStyle);
            iconButtonStyle.fontSize = 18;

            inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.normal.background = inputTexture;
            inputStyle.focused.background = inputFocusedTexture;
            inputStyle.hover.background = inputFocusedTexture;
            inputStyle.active.background = inputFocusedTexture;
            inputStyle.normal.textColor = Color.white;
            inputStyle.focused.textColor = Color.white;
            inputStyle.hover.textColor = Color.white;
            inputStyle.active.textColor = Color.white;
            inputStyle.fontSize = 18;
            inputStyle.alignment = TextAnchor.MiddleLeft;
            inputStyle.padding = new RectOffset(8, 8, 4, 4);

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

        private Texture2D MakeRoomMarkerTexture()
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = new Color(0.25f, 0.85f, 1f, 0.92f);
            Color edge = new Color(0.06f, 0.12f, 0.17f, 1f);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int dx = x - 8;
                    int dy = y - 8;
                    int d = dx * dx + dy * dy;
                    texture.SetPixel(x, y, d < 50 ? (d > 38 ? edge : fill) : clear);
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
            EnsureDefaultGlobalLight();
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
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic == null)
            {
                return;
            }

            TilemapCollider2D tilemapCollider = wallLogic.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                tilemapCollider = wallLogic.gameObject.AddComponent<TilemapCollider2D>();
            }

            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            Rigidbody2D body = wallLogic.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = wallLogic.gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;
            if (wallLogic.GetComponent<CompositeCollider2D>() == null)
            {
                wallLogic.gameObject.AddComponent<CompositeCollider2D>();
            }
        }

        private void RebuildWallVisuals(CampusFloorRoot floor)
        {
            long perf = CampusMapEditorPerformance.Begin();
            try
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
            CampusWallAutoRenderer.ApplyDebugView(floor, CampusWallDebugView.ShowFinalWallVisuals);
            CampusWallTileUtility.SetTilemapVisible(floor.CollisionDebugTilemap, false);
            }
            finally
            {
                CampusMapEditorPerformance.End(perf, "Runtime RebuildWallVisuals");
            }
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
                SetStatus("至少保留一层。");
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

            SetStatus("已删除楼层。");
        }

        private void EnsureDefaultGlobalLight()
        {
            Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].lightType == Light2D.LightType.Global)
                {
                    return;
                }
            }

            GameObject lightObject = new GameObject(CampusObjectNames.GlobalLight2D);
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.blendStyleIndex = 0;
            light.targetSortingLayers = GetAllSortingLayerIds();
            light.intensity = AmbientLightIntensity;
            light.color = Color.white;
            CampusDynamicShadowUtility.ConfigureLightShadows(light, false, 0.75f, 0.3f, 0.5f);
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
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return;
            }

            string cleanName = roomName.Trim();
            for (int i = 0; i < roomNames.Count; i++)
            {
                if (string.Equals(roomNames[i], cleanName, StringComparison.OrdinalIgnoreCase))
                {
                    roomRequiredCounts[i] = Mathf.Max(0, required);
                    return;
                }
            }

            roomNames.Add(cleanName);
            roomRequiredCounts.Add(Mathf.Max(0, required));
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
            CampusRuntimeRoomMarker[] markers = FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null && markers[i].RoomName == roomName)
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

        private string ResolveNewRoomPrefabName()
        {
            if (!string.IsNullOrWhiteSpace(newRoomPrefabName))
            {
                return newRoomPrefabName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(newRoomName))
            {
                return newRoomName.Trim();
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
                ? (string.IsNullOrWhiteSpace(fallbackName) ? "未命名房间" : fallbackName.Trim())
                : roomPrefab.RoomName.Trim();
            roomPrefab.Size = NormalizeRoomPrefabSize(roomPrefab.Size);
            roomPrefab.FloorTiles = roomPrefab.FloorTiles ?? new List<CampusRuntimeTileSnapshot>();
            roomPrefab.WallTiles = roomPrefab.WallTiles ?? new List<CampusRuntimeTileSnapshot>();
            roomPrefab.Objects = roomPrefab.Objects ?? new List<CampusRuntimeObjectSnapshot>();
            roomPrefab.RoomMarkers = roomPrefab.RoomMarkers ?? new List<CampusRuntimeRoomSnapshot>();
            roomPrefab.Lights = roomPrefab.Lights ?? new List<CampusRuntimeRoomLightSnapshot>();
            EnsureRoomPrefabMarker(roomPrefab);
        }

        private static void EnsureRoomPrefabMarker(CampusRuntimeRoomPrefab roomPrefab)
        {
            if (roomPrefab == null)
            {
                return;
            }

            roomPrefab.RoomMarkers = roomPrefab.RoomMarkers ?? new List<CampusRuntimeRoomSnapshot>();
            for (int i = 0; i < roomPrefab.RoomMarkers.Count; i++)
            {
                CampusRuntimeRoomSnapshot marker = roomPrefab.RoomMarkers[i];
                if (marker != null && string.Equals(marker.RoomName, roomPrefab.RoomName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            Vector2Int size = NormalizeRoomPrefabSize(roomPrefab.Size);
            roomPrefab.RoomMarkers.Add(new CampusRuntimeRoomSnapshot
            {
                RoomName = roomPrefab.RoomName,
                FloorIndex = 0,
                Cell = new Vector3Int(Mathf.Max(0, (size.x - 1) / 2), Mathf.Max(0, (size.y - 1) / 2), 0),
                HideMarkerVisual = true
            });
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
                return "\u672a\u9009\u7269\u54c1";
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
                return "\u672a\u9009\u7269\u54c1";
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
            SpriteRenderer renderer = markerObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = markerObject.AddComponent<SpriteRenderer>();
            }

            if (roomMarkerSprite == null)
            {
                roomMarkerSprite = Sprite.Create(roomMarkerTexture, new Rect(0f, 0f, roomMarkerTexture.width, roomMarkerTexture.height), new Vector2(0.5f, 0.5f), 16f);
                roomMarkerSprite.hideFlags = HideFlags.DontSave;
            }

            renderer.sprite = roomMarkerSprite;
            renderer.sortingOrder = floor != null && mapRoot != null ? floor.FloorIndex * mapRoot.SortingOrderStepPerFloor + CampusRenderSortingUtility.OverlayOffset + 30 : 9999;
            renderer.color = new Color(0.25f, 0.86f, 1f, 0.9f);
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
            return tile == null ? "空" : CampusObjectNames.GetDisplayName(tile.name);
        }

        private string Truncate(string value, int maxCharacters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            {
                return value;
            }

            return value.Substring(0, maxCharacters);
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
    public sealed class CampusRuntimeObjectSnapshot
    {
        public string ObjectId;
        public string DisplayNameOverride;
        public int PaletteIndex = -1;
        public Vector3 Position;
        public Vector3Int Cell;
        public Vector2Int FootprintSize = Vector2Int.one;
        public int FloorIndex = 1;
        public bool OverrideFootprintSize;
        public Vector2 VisualScale = Vector2.one;
        public bool LockVisualScaleAspect = true;
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
        public string DisplayNameOverride;
        public bool OverrideFootprintSize;
        public Vector2Int FootprintSize = Vector2Int.one;
        public Vector2 VisualScale = Vector2.one;
        public bool LockVisualScaleAspect = true;
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

    [Serializable]
    public sealed class CampusRuntimeProjectSyncManifest
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
