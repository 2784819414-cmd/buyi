using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// Shared editor helpers for creating map hierarchy, debug assets, collision setup, and map snapshots.
    /// </summary>
    public static class CampusMapEditorUtility
    {
        public const string RootPath = "Assets/NtingCampus";
        public const string RuntimePath = RootPath + "/Runtime";
        public const string EditorPath = RootPath + "/Editor";
        public const string ScriptableObjectsPath = RootPath + "/ScriptableObjects";
        public const string DebugTilesPath = RootPath + "/Tiles/Debug";
        public const string FloorTilesPath = RootPath + "/Tiles/Floor";
        public const string ImportedFloorTilesPath = FloorTilesPath + "/Imported";
        public const string WallTilesPath = RootPath + "/Tiles/Walls";
        public const string ImportedWallTilesPath = WallTilesPath + "/Imported";
        public const string PrototypeWallTilesPath = WallTilesPath + "/Prototype";
        public const string ImportedSourceTexturePath = RootPath + "/Tiles/Source";
        public const string ImportedFloorSourceTexturePath = ImportedSourceTexturePath + "/Floor";
        public const string ImportedWallSourceTexturePath = ImportedSourceTexturePath + "/Walls";
        public const string PrototypeWallSourceTexturePath = ImportedSourceTexturePath + "/WallPrototype";
        public const string StairPrefabsPath = RootPath + "/Prefabs/Stairs";
        public const string PropPrefabsPath = RootPath + "/Prefabs/Props";
        public const string MapPrefabsPath = RootPath + "/Prefabs/Map";
        public const string WallsPrefabsPath = RootPath + "/Prefabs/Walls";
        public const string MaterialsPath = RootPath + "/Materials";
        public const string WallMaterialsPath = MaterialsPath + "/Walls";
        public const string DefaultMapDataPath = ScriptableObjectsPath + "/CampusMapData.asset";
        public const string DefaultFloorPalettePath = ScriptableObjectsPath + "/地面瓦片面板.asset";
        public const string DefaultWallPalettePath = ScriptableObjectsPath + "/墙体瓦片面板.asset";
        public const string DefaultPrefabPalettePath = ScriptableObjectsPath + "/物体资源面板.asset";
        public const string WallProfilesPath = ScriptableObjectsPath + "/WallProfiles";
        public const string DefaultWallRenderProfilePath = WallProfilesPath + "/默认原型墙体配置.asset";
        public const string BrickWallRenderProfilePath = WallProfilesPath + "/砖墙原型墙体配置.asset";
        public const string DefaultWallVisualCatalogPath = ScriptableObjectsPath + "/墙体视觉目录.asset";
        public const string ExternalTileFolderName = "Assets/瓦片";
        public const string ExternalFloorFolderName = "地面";
        public const string ExternalWallFolderName = "墙";
        private const string WallLitShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default";
        private const string WallMeshLitShaderName = "Universal Render Pipeline/2D/Mesh2D-Lit-Default";
        private const float AmbientLightIntensity = 0.28f;
        private const float SunLightIntensity = 1.15f;
        private const float SunNormalMapDistance = 4f;
        private static readonly Vector2 SunDirectionToLight = new Vector2(-0.52f, 0.85f).normalized;

        public struct DebugAssetSet
        {
            public TileBase FloorTile;
            public TileBase WallTile;
            public TileBase HorizontalWallTile;
            public TileBase VerticalWallTile;
            public TileBase CornerWallTile;
            public TileBase HighWallTile;
            public GameObject StairPrefab;
            public GameObject PropBoxPrefab;
        }

        public static void EnsureDirectories()
        {
            string[] directories =
            {
                RootPath,
                RuntimePath,
                EditorPath,
                ScriptableObjectsPath,
                MapPrefabsPath,
                WallsPrefabsPath,
                StairPrefabsPath,
                PropPrefabsPath,
                FloorTilesPath,
                WallTilesPath,
                ImportedFloorTilesPath,
                ImportedWallTilesPath,
                PrototypeWallTilesPath,
                ImportedSourceTexturePath,
                ImportedFloorSourceTexturePath,
                ImportedWallSourceTexturePath,
                PrototypeWallSourceTexturePath,
                WallProfilesPath,
                DebugTilesPath,
                MaterialsPath,
                WallMaterialsPath
            };

            for (int i = 0; i < directories.Length; i++)
            {
                EnsureAssetFolder(directories[i]);
            }
        }

        public static CampusMapRoot FindCampusMapRoot()
        {
            return Object.FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
        }

        public static CampusMapRoot FindOrCreateCampusMapRoot()
        {
            CampusMapRoot root = FindCampusMapRoot();
            if (root != null)
            {
                root.RebuildFloorReferences();
                return root;
            }

            GameObject rootObject = new GameObject(CampusObjectNames.MapRoot);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create CampusMapRoot");
            root = rootObject.AddComponent<CampusMapRoot>();
            CampusFloorVisibilityController controller = rootObject.AddComponent<CampusFloorVisibilityController>();
            controller.MapRoot = root;

            GetOrCreateChild(rootObject.transform, CampusObjectNames.FloorsRoot, CampusObjectNames.LegacyFloorsRoot);
            GetOrCreateChild(rootObject.transform, CampusObjectNames.EditorDataRoot, CampusObjectNames.LegacyEditorDataRoot);
            EnsureMapLighting(root);
            CreateFloor(root, 1, true);
            root.CurrentPreviewFloor = 1;
            MarkSceneDirty();
            return root;
        }

        public static CampusFloorRoot GetOrCreateFloor(CampusMapRoot root, int floorIndex, bool unlocked)
        {
            if (root == null)
            {
                return null;
            }

            root.RebuildFloorReferences();
            CampusFloorRoot existing = root.GetFloor(floorIndex);
            if (existing != null)
            {
                existing.IsUnlocked |= unlocked;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            return CreateFloor(root, floorIndex, unlocked);
        }

        public static CampusFloorRoot CreateFloor(CampusMapRoot root, int floorIndex, bool unlocked)
        {
            if (root == null)
            {
                return null;
            }

            EnsureDirectories();
            Transform floorsRoot = GetOrCreateChild(root.transform, CampusObjectNames.FloorsRoot, CampusObjectNames.LegacyFloorsRoot);

            GameObject floorObject = new GameObject(CampusObjectNames.GetFloorName(floorIndex));
            floorObject.transform.SetParent(floorsRoot, false);

            CampusFloorRoot floor = floorObject.AddComponent<CampusFloorRoot>();
            floor.FloorIndex = floorIndex;
            floor.IsUnlocked = unlocked;

            GameObject gridObject = new GameObject(CampusObjectNames.Grid);
            gridObject.transform.SetParent(floorObject.transform, false);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            floor.Grid = grid;

            int sortingBase = floorIndex * root.SortingOrderStepPerFloor;
            floor.FloorTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.FloorTilemap, sortingBase + CampusRenderSortingUtility.FloorOffset);
            floor.WallLogicTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.WallLogicTilemap, sortingBase + CampusRenderSortingUtility.WallLogicOffset);
            floor.WallTilemap = floor.WallLogicTilemap;
            floor.WallFaceTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.WallFaceTilemap, sortingBase + CampusRenderSortingUtility.WallFaceOffset);
            floor.WallSideTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.WallSideTilemap, sortingBase + CampusRenderSortingUtility.WallSideOffset);
            floor.WallCapTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.WallCapTilemap, sortingBase + CampusRenderSortingUtility.WallCapOffset);
            floor.WallOverlayTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.WallOverlayTilemap, sortingBase + CampusRenderSortingUtility.WallVisualOverlayOffset);
            floor.OverlayTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.OverlayTilemap, sortingBase + CampusRenderSortingUtility.OverlayOffset);
            floor.CollisionDebugTilemap = CreateTilemap(gridObject.transform, CampusObjectNames.CollisionDebugTilemap, sortingBase + CampusRenderSortingUtility.CollisionDebugOffset);
            floor.WallMeshRoot = GetOrCreateChild(gridObject.transform, CampusObjectNames.WallMeshRoot, CampusObjectNames.LegacyWallMeshRoot);

            floor.PropsRoot = GetOrCreateChild(floorObject.transform, CampusObjectNames.PropsRoot, CampusObjectNames.LegacyPropsRoot);
            floor.StairsRoot = GetOrCreateChild(floorObject.transform, CampusObjectNames.StairsRoot, CampusObjectNames.LegacyStairsRoot);
            EnsureWallCollision(floor);
            CampusWallAutoRenderer.ApplyDebugView(floor, CampusWallDebugView.ShowFinalWallVisuals);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);

            Undo.RegisterCreatedObjectUndo(floorObject, "Create Campus Floor");
            root.RebuildFloorReferences();
            EditorUtility.SetDirty(root);
            MarkSceneDirty();
            return floor;
        }

        public static void RebuildFloorReferences(CampusMapRoot root)
        {
            if (root == null)
            {
                return;
            }

            GetOrCreateChild(root.transform, CampusObjectNames.FloorsRoot, CampusObjectNames.LegacyFloorsRoot);
            GetOrCreateChild(root.transform, CampusObjectNames.EditorDataRoot, CampusObjectNames.LegacyEditorDataRoot);
            CampusFloorRoot[] floors = root.GetComponentsInChildren<CampusFloorRoot>(true);
            for (int i = 0; i < floors.Length; i++)
            {
                EnsureFloorStructure(root, floors[i], false);
                EnsureWallCollision(floors[i]);
            }

            CampusFloorVisibilityController controller = root.GetComponent<CampusFloorVisibilityController>();
            if (controller == null)
            {
                controller = root.gameObject.AddComponent<CampusFloorVisibilityController>();
            }

            controller.MapRoot = root;
            root.RebuildFloorReferences();
            EditorUtility.SetDirty(root);
            MarkSceneDirty();
        }

        public static void EnsureNotPreviewingBeforeEdit(CampusMapRoot root)
        {
            if (root == null)
            {
                return;
            }

            CampusFloorVisibilityController controller = root.GetComponent<CampusFloorVisibilityController>();
            if (controller != null && controller.IsPreviewActive)
            {
                controller.ResetVisibility();
                SceneView.RepaintAll();
            }
        }

        public static void AutoWireFloor(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            floor.Grid = floor.Grid != null ? floor.Grid : floor.GetComponentInChildren<Grid>(true);
            if (floor.Grid != null)
            {
                Tilemap[] tilemaps = floor.Grid.GetComponentsInChildren<Tilemap>(true);
                for (int i = 0; i < tilemaps.Length; i++)
                {
                    Tilemap tilemap = tilemaps[i];
                    if (tilemap == null)
                    {
                        continue;
                    }

                    string tilemapName = CampusObjectNames.LocalizeLegacyHierarchyName(tilemap.name);
                    if (tilemap.name != tilemapName)
                    {
                        tilemap.name = tilemapName;
                    }

                    switch (tilemapName)
                    {
                        case CampusObjectNames.FloorTilemap:
                            floor.FloorTilemap = tilemap;
                            break;
                        case CampusObjectNames.WallLogicTilemap:
                            floor.WallLogicTilemap = tilemap;
                            floor.WallTilemap = tilemap;
                            break;
                        case CampusObjectNames.WallCapTilemap:
                            floor.WallCapTilemap = tilemap;
                            break;
                        case CampusObjectNames.WallFaceTilemap:
                            floor.WallFaceTilemap = tilemap;
                            break;
                        case CampusObjectNames.WallSideTilemap:
                            floor.WallSideTilemap = tilemap;
                            break;
                        case CampusObjectNames.WallOverlayTilemap:
                            floor.WallOverlayTilemap = tilemap;
                            break;
                        case CampusObjectNames.OverlayTilemap:
                            floor.OverlayTilemap = tilemap;
                            break;
                        case CampusObjectNames.CollisionDebugTilemap:
                            floor.CollisionDebugTilemap = tilemap;
                            break;
                    }
                }
            }

            floor.gameObject.name = CampusObjectNames.GetFloorName(floor.FloorIndex);
            CampusDynamicShadowUtility.RemoveFixedWallShadowTilemaps(floor);
            floor.PropsRoot = floor.PropsRoot != null ? floor.PropsRoot : FindChildRecursive(floor.transform, CampusObjectNames.PropsRoot, CampusObjectNames.LegacyPropsRoot);
            floor.StairsRoot = floor.StairsRoot != null ? floor.StairsRoot : FindChildRecursive(floor.transform, CampusObjectNames.StairsRoot, CampusObjectNames.LegacyStairsRoot);
            floor.WallMeshRoot = floor.WallMeshRoot != null ? floor.WallMeshRoot : FindChildRecursive(floor.transform, CampusObjectNames.WallMeshRoot, CampusObjectNames.LegacyWallMeshRoot);
            if (floor.WallMeshRoot == null && floor.Grid != null)
            {
                floor.WallMeshRoot = GetOrCreateChild(floor.Grid.transform, CampusObjectNames.WallMeshRoot, CampusObjectNames.LegacyWallMeshRoot);
            }

            if (floor.PropsRoot == null)
            {
                floor.PropsRoot = GetOrCreateChild(floor.transform, CampusObjectNames.PropsRoot, CampusObjectNames.LegacyPropsRoot);
            }

            if (floor.StairsRoot == null)
            {
                floor.StairsRoot = GetOrCreateChild(floor.transform, CampusObjectNames.StairsRoot, CampusObjectNames.LegacyStairsRoot);
            }

            floor.RefreshUsedBounds();
            EditorUtility.SetDirty(floor);
        }

        public static void EnsureFloorStructure(CampusMapRoot root, CampusFloorRoot floor, bool forceCaptureOriginals)
        {
            if (floor == null)
            {
                return;
            }

            int sortingBase = floor.FloorIndex * (root != null ? root.SortingOrderStepPerFloor : 1000);
            if (floor.Grid == null)
            {
                Transform gridTransform = FindChildRecursive(floor.transform, CampusObjectNames.Grid, CampusObjectNames.LegacyGrid);
                if (gridTransform == null)
                {
                    gridTransform = GetOrCreateChild(floor.transform, CampusObjectNames.Grid, CampusObjectNames.LegacyGrid);
                }

                floor.Grid = gridTransform.GetComponent<Grid>();
                if (floor.Grid == null)
                {
                    floor.Grid = gridTransform.gameObject.AddComponent<Grid>();
                }

                floor.Grid.cellSize = Vector3.one;
                floor.Grid.cellLayout = GridLayout.CellLayout.Rectangle;
            }

            floor.gameObject.name = CampusObjectNames.GetFloorName(floor.FloorIndex);
            floor.Grid.gameObject.name = CampusObjectNames.Grid;
            EnsureFloorGridAlignedToSceneGrid(floor);

            if (floor.FloorTilemap == null)
            {
                floor.FloorTilemap = FindTilemapByName(floor.Grid.transform, CampusObjectNames.FloorTilemap, CampusObjectNames.LegacyFloorTilemap);
                if (floor.FloorTilemap == null)
                {
                    floor.FloorTilemap = CreateTilemap(floor.Grid.transform, CampusObjectNames.FloorTilemap, sortingBase);
                }
            }

            if (floor.WallLogicTilemap == null)
            {
                if (floor.WallTilemap != null)
                {
                    floor.WallLogicTilemap = floor.WallTilemap;
                    floor.WallLogicTilemap.name = CampusObjectNames.WallLogicTilemap;
                }
                else
                {
                    floor.WallLogicTilemap = FindTilemapByName(floor.Grid.transform, CampusObjectNames.WallLogicTilemap, CampusObjectNames.LegacyWallLogicTilemap, CampusObjectNames.LegacyWallsTilemap);
                    if (floor.WallLogicTilemap == null)
                    {
                        Tilemap legacyWallTilemap = FindTilemapByName(floor.Grid.transform, CampusObjectNames.WallLogicTilemap, CampusObjectNames.LegacyWallsTilemap);
                        if (legacyWallTilemap != null)
                        {
                            legacyWallTilemap.name = CampusObjectNames.WallLogicTilemap;
                            floor.WallLogicTilemap = legacyWallTilemap;
                        }
                        else
                        {
                            floor.WallLogicTilemap = CreateTilemap(floor.Grid.transform, CampusObjectNames.WallLogicTilemap, sortingBase + 90);
                        }
                    }
                }
            }

            floor.WallTilemap = floor.WallLogicTilemap;
            CampusDynamicShadowUtility.RemoveFixedWallShadowTilemaps(floor);
            EnsureWallVisualTilemap(ref floor.WallFaceTilemap, floor.Grid.transform, CampusObjectNames.WallFaceTilemap, sortingBase + CampusRenderSortingUtility.WallFaceOffset, CampusObjectNames.LegacyWallFaceTilemap);
            EnsureWallVisualTilemap(ref floor.WallSideTilemap, floor.Grid.transform, CampusObjectNames.WallSideTilemap, sortingBase + CampusRenderSortingUtility.WallSideOffset, CampusObjectNames.LegacyWallSideTilemap);
            EnsureWallVisualTilemap(ref floor.WallCapTilemap, floor.Grid.transform, CampusObjectNames.WallCapTilemap, sortingBase + CampusRenderSortingUtility.WallCapOffset, CampusObjectNames.LegacyWallCapTilemap);
            EnsureWallVisualTilemap(ref floor.WallOverlayTilemap, floor.Grid.transform, CampusObjectNames.WallOverlayTilemap, sortingBase + CampusRenderSortingUtility.WallVisualOverlayOffset, CampusObjectNames.LegacyWallOverlayTilemap);
            floor.WallMeshRoot = floor.WallMeshRoot != null ? floor.WallMeshRoot : FindChildRecursive(floor.Grid.transform, CampusObjectNames.WallMeshRoot, CampusObjectNames.LegacyWallMeshRoot);
            if (floor.WallMeshRoot == null)
            {
                floor.WallMeshRoot = GetOrCreateChild(floor.Grid.transform, CampusObjectNames.WallMeshRoot, CampusObjectNames.LegacyWallMeshRoot);
            }

            if (floor.OverlayTilemap == null)
            {
                floor.OverlayTilemap = FindTilemapByName(floor.Grid.transform, CampusObjectNames.OverlayTilemap, CampusObjectNames.LegacyOverlayTilemap);
                if (floor.OverlayTilemap == null)
                {
                    floor.OverlayTilemap = CreateTilemap(floor.Grid.transform, CampusObjectNames.OverlayTilemap, sortingBase + CampusRenderSortingUtility.OverlayOffset);
                }
            }

            if (floor.CollisionDebugTilemap == null)
            {
                floor.CollisionDebugTilemap = FindTilemapByName(floor.Grid.transform, CampusObjectNames.CollisionDebugTilemap, CampusObjectNames.LegacyCollisionDebugTilemap);
                if (floor.CollisionDebugTilemap == null)
                {
                    floor.CollisionDebugTilemap = CreateTilemap(floor.Grid.transform, CampusObjectNames.CollisionDebugTilemap, sortingBase + CampusRenderSortingUtility.CollisionDebugOffset);
                }
            }

            floor.PropsRoot = floor.PropsRoot != null ? floor.PropsRoot : FindChildRecursive(floor.transform, CampusObjectNames.PropsRoot, CampusObjectNames.LegacyPropsRoot);
            if (floor.PropsRoot == null)
            {
                floor.PropsRoot = GetOrCreateChild(floor.transform, CampusObjectNames.PropsRoot, CampusObjectNames.LegacyPropsRoot);
            }

            floor.StairsRoot = floor.StairsRoot != null ? floor.StairsRoot : FindChildRecursive(floor.transform, CampusObjectNames.StairsRoot, CampusObjectNames.LegacyStairsRoot);
            if (floor.StairsRoot == null)
            {
                floor.StairsRoot = GetOrCreateChild(floor.transform, CampusObjectNames.StairsRoot, CampusObjectNames.LegacyStairsRoot);
            }

            CampusRenderSortingUtility.ApplyFloorSorting(floor, sortingBase);
            floor.CaptureOriginalRenderState(forceCaptureOriginals);
            floor.RefreshUsedBounds();
            EditorUtility.SetDirty(floor);
        }

        public static void EnsureFloorGridAlignedToSceneGrid(CampusFloorRoot floor)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            Transform floorTransform = floor.transform;
            Transform gridTransform = floor.Grid.transform;
            Vector3 targetGridWorld = gridTransform.position;
            targetGridWorld.x = Mathf.Round(targetGridWorld.x);
            targetGridWorld.y = Mathf.Round(targetGridWorld.y);

            bool needsAlignment =
                !Approximately(gridTransform.position, targetGridWorld) ||
                !Approximately(floorTransform.localScale, Vector3.one) ||
                Quaternion.Angle(floorTransform.localRotation, Quaternion.identity) > 0.01f ||
                !Approximately(gridTransform.localPosition, Vector3.zero) ||
                !Approximately(gridTransform.localScale, Vector3.one) ||
                Quaternion.Angle(gridTransform.localRotation, Quaternion.identity) > 0.01f ||
                !Approximately(floor.Grid.cellSize, Vector3.one) ||
                floor.Grid.cellLayout != GridLayout.CellLayout.Rectangle;

            if (!needsAlignment)
            {
                return;
            }

            Undo.RecordObjects(new Object[] { floorTransform, gridTransform, floor.Grid }, "Align Campus Floor Grid");
            floorTransform.localRotation = Quaternion.identity;
            floorTransform.localScale = Vector3.one;
            gridTransform.localPosition = Vector3.zero;
            gridTransform.localRotation = Quaternion.identity;
            gridTransform.localScale = Vector3.one;
            floor.Grid.cellSize = Vector3.one;
            floor.Grid.cellLayout = GridLayout.CellLayout.Rectangle;

            Vector3 gridDelta = targetGridWorld - gridTransform.position;
            if (gridDelta.sqrMagnitude > 0.00000001f)
            {
                floorTransform.position += gridDelta;
            }

            EditorUtility.SetDirty(floorTransform);
            EditorUtility.SetDirty(gridTransform);
            EditorUtility.SetDirty(floor.Grid);
            MarkSceneDirty();
        }

        private static bool Approximately(Vector3 lhs, Vector3 rhs)
        {
            return (lhs - rhs).sqrMagnitude <= 0.00000001f;
        }

        public static void EnsureWallCollision(CampusFloorRoot floor)
        {
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (floor == null || wallLogic == null)
            {
                return;
            }

            floor.WallLogicTilemap = wallLogic;
            floor.WallTilemap = wallLogic;
            GameObject wallObject = wallLogic.gameObject;
            TilemapCollider2D tilemapCollider = wallObject.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                tilemapCollider = Undo.AddComponent<TilemapCollider2D>(wallObject);
            }

            Rigidbody2D body = wallObject.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody2D>(wallObject);
            }

            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            CompositeCollider2D composite = wallObject.GetComponent<CompositeCollider2D>();
            if (composite == null)
            {
                composite = Undo.AddComponent<CompositeCollider2D>(wallObject);
            }

            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            composite.isTrigger = false;
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            tilemapCollider.extrusionFactor = 0.01f;
            tilemapCollider.maximumTileChangeCount = 1024;
            tilemapCollider.ProcessTilemapChanges();
            EditorUtility.SetDirty(wallLogic);
            EditorUtility.SetDirty(tilemapCollider);
            EditorUtility.SetDirty(composite);
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(wallObject);
        }

        public static void ProcessWallColliderChanges(CampusFloorRoot floor)
        {
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (floor == null || wallLogic == null)
            {
                return;
            }

            TilemapCollider2D wallCollider = wallLogic.GetComponent<TilemapCollider2D>();
            if (wallCollider == null)
            {
                EnsureWallCollision(floor);
                wallCollider = wallLogic.GetComponent<TilemapCollider2D>();
            }

            if (wallCollider != null)
            {
                wallCollider.ProcessTilemapChanges();
                EditorUtility.SetDirty(wallLogic);
                EditorUtility.SetDirty(wallCollider);
            }
        }

        public static DebugAssetSet EnsureDebugAssets()
        {
            EnsureDirectories();

            Sprite floorSprite = EnsureSprite(DebugTilesPath + "/Debug_FloorTile.png", new Color(0.34f, 0.56f, 0.46f, 1f), DebugSpritePattern.Floor);
            Sprite wallSprite = EnsureSprite(DebugTilesPath + "/Debug_WallTile.png", new Color(0.36f, 0.37f, 0.42f, 1f), DebugSpritePattern.Wall);
            Sprite horizontalWallSprite = EnsureSprite(DebugTilesPath + "/Wall_Horizontal_Debug.png", new Color(0.44f, 0.44f, 0.5f, 1f), DebugSpritePattern.HorizontalWall);
            Sprite verticalWallSprite = EnsureSprite(DebugTilesPath + "/Wall_Vertical_Debug.png", new Color(0.39f, 0.4f, 0.48f, 1f), DebugSpritePattern.VerticalWall);
            Sprite cornerWallSprite = EnsureSprite(DebugTilesPath + "/Wall_Corner_Debug.png", new Color(0.46f, 0.42f, 0.36f, 1f), DebugSpritePattern.CornerWall);
            Sprite highWallSprite = EnsureSprite(DebugTilesPath + "/Wall_High_Debug.png", new Color(0.36f, 0.38f, 0.48f, 1f), DebugSpritePattern.HighWall, false, 32, 64, new Vector2(0.5f, 0f));
            Sprite stairSprite = EnsureSprite(DebugTilesPath + "/Debug_Stair.png", new Color(0.76f, 0.64f, 0.31f, 1f), DebugSpritePattern.Stair, false, 32, 64, new Vector2(0.5f, 0.5f));
            Sprite propSprite = EnsureSprite(DebugTilesPath + "/Debug_Prop_Box.png", new Color(0.44f, 0.28f, 0.17f, 1f), DebugSpritePattern.Box);

            DebugAssetSet set = new DebugAssetSet
            {
                FloorTile = EnsureTile(DebugTilesPath + "/调试地面.asset", floorSprite, Tile.ColliderType.None),
                WallTile = EnsureTile(DebugTilesPath + "/调试墙体.asset", wallSprite, Tile.ColliderType.Grid),
                HorizontalWallTile = EnsureTile(DebugTilesPath + "/Wall_Horizontal_Debug.asset", horizontalWallSprite, Tile.ColliderType.Grid),
                VerticalWallTile = EnsureTile(DebugTilesPath + "/Wall_Vertical_Debug.asset", verticalWallSprite, Tile.ColliderType.Grid),
                CornerWallTile = EnsureTile(DebugTilesPath + "/Wall_Corner_Debug.asset", cornerWallSprite, Tile.ColliderType.Grid),
                HighWallTile = EnsureTile(DebugTilesPath + "/Wall_High_Debug.asset", highWallSprite, Tile.ColliderType.Grid),
                StairPrefab = EnsureStairPrefab(StairPrefabsPath + "/测试楼梯.prefab", stairSprite),
                PropBoxPrefab = EnsurePropPrefab(PropPrefabsPath + "/测试箱.prefab", propSprite)
            };

            EnsureDefaultPalettes(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return set;
        }

        public static DebugAssetSet LoadDebugAssets()
        {
            return new DebugAssetSet
            {
                FloorTile = AssetDatabase.LoadAssetAtPath<TileBase>(DebugTilesPath + "/调试地面.asset"),
                WallTile = AssetDatabase.LoadAssetAtPath<TileBase>(DebugTilesPath + "/调试墙体.asset"),
                HorizontalWallTile = AssetDatabase.LoadAssetAtPath<TileBase>(DebugTilesPath + "/Wall_Horizontal_Debug.asset"),
                VerticalWallTile = AssetDatabase.LoadAssetAtPath<TileBase>(DebugTilesPath + "/Wall_Vertical_Debug.asset"),
                CornerWallTile = AssetDatabase.LoadAssetAtPath<TileBase>(DebugTilesPath + "/Wall_Corner_Debug.asset"),
                HighWallTile = AssetDatabase.LoadAssetAtPath<TileBase>(DebugTilesPath + "/Wall_High_Debug.asset"),
                StairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StairPrefabsPath + "/测试楼梯.prefab"),
                PropBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PropPrefabsPath + "/测试箱.prefab")
            };
        }

        [MenuItem("Tools/Nting Campus/Generate Debug Assets")]
        public static void GenerateDebugAssetsFromMenu()
        {
            EnsureDebugAssets();
        }

        [MenuItem("Tools/Nting Campus/Ensure Map Light And Max Shadows")]
        public static void EnsureMapLightingFromMenu()
        {
            EnsureEditableSceneLoadedForBatchMode();
            EnsureMapLighting(FindCampusMapRoot());
            if (Application.isBatchMode)
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }

        [MenuItem("Tools/Nting Campus/Generate Wedge Wall Test Assets")]
        public static void GenerateWedgeWallTestAssetsFromMenu()
        {
            GenerateWedgeWallTestAssets();
        }

        [MenuItem("Tools/Nting Campus/Rebuild 3D Wall Visuals")]
        public static void RebuildWallMeshVisualsFromMenu()
        {
            EnsureEditableSceneLoadedForBatchMode();
            CampusMapRoot root = FindCampusMapRoot();
            if (root == null)
            {
                return;
            }

            RebuildAllWallVisuals(root, LoadDefaultWallRenderProfile());
            ApplyWallDebugView(root, CampusWallDebugView.ShowFinalWallVisuals);
            if (Application.isBatchMode)
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }

        public static Light2D EnsureMapLighting(CampusMapRoot root)
        {
            EnsureCurrentQualityUsesMaxShadows();
            RemoveGenerated3DMapKeyLight();
            Light2D ambientLight = EnsureSceneGlobalLight2D();
            Light2D sunLight = EnsureSceneSunLight2D(root, ambientLight);
            CampusDayNightController.EnsureSceneController(root);
            MarkSceneDirty();
            return sunLight != null ? sunLight : ambientLight;
        }

        public static Light2D[] FindSceneLights2D()
        {
            return Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(light => light != null)
                .OrderBy(light => light.gameObject.name)
                .ToArray();
        }

        public static Light2D CreateSceneLight2D(CampusMapRoot root, string requestedName, Light2D.LightType lightType)
        {
            EnsureCurrentQualityUsesMaxShadows();
            string baseName = string.IsNullOrWhiteSpace(requestedName) ? "新光源" : requestedName.Trim();
            GameObject lightObject = new GameObject(MakeUniqueSceneObjectName(baseName));
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Campus Light 2D");

            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = lightType;
            light.blendStyleIndex = 0;
            light.targetSortingLayers = GetAllSortingLayerIds();
            ApplyCreatedLightDefaults(root, light);
            MarkSceneDirty();
            return light;
        }

        public static Light2D CreatePlacedSceneLight2D(string requestedName, Light2D.LightType lightType, Vector3 worldPosition, int rotation90, Vector3 cellSize)
        {
            EnsureCurrentQualityUsesMaxShadows();
            string baseName = string.IsNullOrWhiteSpace(requestedName) ? "新光源" : requestedName.Trim();
            GameObject lightObject = new GameObject(MakeUniqueSceneObjectName(baseName));
            Undo.RegisterCreatedObjectUndo(lightObject, "Place Campus Light 2D");

            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = lightType;
            light.blendStyleIndex = 0;
            light.targetSortingLayers = GetAllSortingLayerIds();
            ApplyPlacedLightDefaults(light, worldPosition, rotation90, cellSize);
            MarkSceneDirty();
            return light;
        }

        public static void FitLight2DToMap(CampusMapRoot root, Light2D light)
        {
            if (light == null)
            {
                return;
            }

            Undo.RecordObjects(new Object[] { light, light.transform }, "Fit Campus Light 2D To Map");
            light.targetSortingLayers = GetAllSortingLayerIds();
            Bounds mapBounds = CalculateMapWorldBounds(root);
            float diagonal = Mathf.Max(8f, new Vector2(mapBounds.size.x, mapBounds.size.y).magnitude);

            if (light.lightType == Light2D.LightType.Global)
            {
                light.transform.position = Vector3.zero;
                light.transform.rotation = Quaternion.identity;
                light.intensity = Mathf.Clamp(light.intensity <= 0f ? AmbientLightIntensity : light.intensity, 0f, 8f);
            }
            else
            {
                light.transform.position = mapBounds.center;
                light.pointLightInnerAngle = 360f;
                light.pointLightOuterAngle = 360f;
                light.pointLightInnerRadius = Mathf.Max(3f, diagonal * 0.55f);
                light.pointLightOuterRadius = Mathf.Max(light.pointLightInnerRadius + 1f, diagonal * 0.75f);
            }

            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.transform);
            MarkSceneDirty();
        }

        private static void ApplyCreatedLightDefaults(CampusMapRoot root, Light2D light)
        {
            if (light == null)
            {
                return;
            }

            Bounds mapBounds = CalculateMapWorldBounds(root);
            float diagonal = Mathf.Max(8f, new Vector2(mapBounds.size.x, mapBounds.size.y).magnitude);
            light.transform.localScale = Vector3.one;
            if (light.lightType == Light2D.LightType.Global)
            {
                light.transform.position = Vector3.zero;
                light.transform.rotation = Quaternion.identity;
                light.color = Color.white;
                light.intensity = AmbientLightIntensity;
                CampusDynamicShadowUtility.ConfigureLightShadows(light, false, 0.75f, 0.3f, 0.5f);
            }
            else
            {
                light.transform.position = mapBounds.center;
                light.transform.rotation = Quaternion.identity;
                light.color = new Color(1f, 0.96f, 0.88f, 1f);
                light.intensity = SunLightIntensity;
                light.pointLightInnerAngle = 360f;
                light.pointLightOuterAngle = 360f;
                light.pointLightInnerRadius = Mathf.Max(3f, diagonal * 0.55f);
                light.pointLightOuterRadius = Mathf.Max(light.pointLightInnerRadius + 1f, diagonal * 0.75f);
                light.falloffIntensity = 0.08f;
                CampusDynamicShadowUtility.ConfigureLightShadows(light, true, 0.45f, 0.75f, 0.85f);
                ConfigureLight2DNormalMap(light, Light2D.NormalMapQuality.Accurate, SunNormalMapDistance);
            }

            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.gameObject);
            EditorUtility.SetDirty(light.transform);
        }

        private static void ApplyPlacedLightDefaults(Light2D light, Vector3 worldPosition, int rotation90, Vector3 cellSize)
        {
            if (light == null)
            {
                return;
            }

            float cellExtent = Mathf.Max(0.5f, Mathf.Max(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)));
            light.transform.position = worldPosition;
            light.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(rotation90, 0, 3) * 90f);
            light.transform.localScale = Vector3.one;
            if (light.lightType == Light2D.LightType.Global)
            {
                light.color = Color.white;
                light.intensity = AmbientLightIntensity;
                CampusDynamicShadowUtility.ConfigureLightShadows(light, false, 0.75f, 0.3f, 0.5f);
            }
            else
            {
                light.color = new Color(1f, 0.96f, 0.88f, 1f);
                light.intensity = SunLightIntensity;
                light.pointLightInnerAngle = 360f;
                light.pointLightOuterAngle = 360f;
                light.pointLightInnerRadius = cellExtent * 1.5f;
                light.pointLightOuterRadius = cellExtent * 4f;
                light.falloffIntensity = 0.18f;
                CampusDynamicShadowUtility.ConfigureLightShadows(light, true, 0.45f, 0.75f, 0.85f);
                ConfigureLight2DNormalMap(light, Light2D.NormalMapQuality.Accurate, SunNormalMapDistance);
            }

            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.gameObject);
            EditorUtility.SetDirty(light.transform);
        }

        private static string MakeUniqueSceneObjectName(string baseName)
        {
            string cleanBaseName = string.IsNullOrWhiteSpace(baseName) ? "Light 2D" : baseName.Trim();
            if (!SceneObjectNameExists(cleanBaseName))
            {
                return cleanBaseName;
            }

            int index = 2;
            string candidate;
            do
            {
                candidate = cleanBaseName + " " + index;
                index++;
            }
            while (SceneObjectNameExists(candidate));

            return candidate;
        }

        private static bool SceneObjectNameExists(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].gameObject.name == objectName)
                {
                    return true;
                }
            }

            return false;
        }

        private static Light2D EnsureSceneGlobalLight2D()
        {
            Transform lightTransform = null;
            Light2D[] sceneLights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneLights.Length; i++)
            {
                Light2D sceneLight = sceneLights[i];
                if (sceneLight == null)
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(sceneLight.gameObject.name, CampusObjectNames.GlobalLight2D, CampusObjectNames.LegacyGlobalLight2D))
                {
                    lightTransform = sceneLight.transform;
                    break;
                }
            }

            if (lightTransform == null)
            {
                for (int i = 0; i < sceneLights.Length; i++)
                {
                    Light2D sceneLight = sceneLights[i];
                    if (sceneLight != null && sceneLight.lightType == Light2D.LightType.Global)
                    {
                        lightTransform = sceneLight.transform;
                        break;
                    }
                }
            }

            bool created = false;
            if (lightTransform == null)
            {
                GameObject lightObject = new GameObject(CampusObjectNames.GlobalLight2D);
                Undo.RegisterCreatedObjectUndo(lightObject, "Create Global Light 2D");
                lightTransform = lightObject.transform;
                created = true;
            }

            lightTransform.gameObject.name = CampusObjectNames.GlobalLight2D;
            lightTransform.SetParent(null, false);
            lightTransform.position = Vector3.zero;
            lightTransform.localScale = Vector3.one;
            if (created)
            {
                lightTransform.rotation = Quaternion.Euler(0f, 0f, 225f);
            }

            Light2D light = lightTransform.GetComponent<Light2D>();
            if (light == null)
            {
                light = lightTransform.gameObject.AddComponent<Light2D>();
                created = true;
            }

            ConfigureGlobalLight2D(light, created);
            return light;
        }

        private static void ConfigureGlobalLight2D(Light2D light, bool created)
        {
            if (light == null)
            {
                return;
            }

            light.lightType = Light2D.LightType.Global;
            light.intensity = created ? AmbientLightIntensity : Mathf.Min(light.intensity, AmbientLightIntensity);
            light.targetSortingLayers = GetAllSortingLayerIds();
            CampusDynamicShadowUtility.ConfigureLightShadows(light, false, 0.75f, 0.3f, 0.5f);
            if (created)
            {
                light.color = Color.white;
                light.blendStyleIndex = 0;
            }

            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.gameObject);
        }

        private static Light2D EnsureSceneSunLight2D(CampusMapRoot root, Light2D ambientLight)
        {
            Transform lightTransform = null;
            Light2D[] sceneLights = Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneLights.Length; i++)
            {
                Light2D sceneLight = sceneLights[i];
                if (sceneLight == null)
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(sceneLight.gameObject.name, CampusObjectNames.SunLight2D, CampusObjectNames.LegacySunLight2D))
                {
                    lightTransform = sceneLight.transform;
                    break;
                }
            }

            bool created = false;
            if (lightTransform == null)
            {
                GameObject lightObject = new GameObject(CampusObjectNames.SunLight2D);
                Undo.RegisterCreatedObjectUndo(lightObject, "Create Sun Light 2D");
                lightTransform = lightObject.transform;
                created = true;
            }

            lightTransform.gameObject.name = CampusObjectNames.SunLight2D;
            lightTransform.SetParent(null, false);
            lightTransform.localScale = Vector3.one;

            Light2D light = lightTransform.GetComponent<Light2D>();
            if (light == null)
            {
                light = lightTransform.gameObject.AddComponent<Light2D>();
                created = true;
            }

            ConfigureSunLight2D(root, light, lightTransform, ambientLight, created);
            return light;
        }

        private static void ConfigureSunLight2D(CampusMapRoot root, Light2D light, Transform lightTransform, Light2D ambientLight, bool created)
        {
            if (light == null || lightTransform == null)
            {
                return;
            }

            Bounds mapBounds = CalculateMapWorldBounds(root);
            float diagonal = Mathf.Max(8f, new Vector2(mapBounds.size.x, mapBounds.size.y).magnitude);
            float sunDistance = Mathf.Clamp(diagonal * 5f, 48f, 640f);
            float fullBrightRadius = sunDistance + diagonal * 0.75f;
            float outerRadius = fullBrightRadius + Mathf.Max(12f, diagonal * 0.12f);
            Vector3 sunPosition = mapBounds.center + new Vector3(SunDirectionToLight.x, SunDirectionToLight.y, 0f) * sunDistance;

            lightTransform.position = sunPosition;
            lightTransform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(-SunDirectionToLight.x, -SunDirectionToLight.y, 0f));

            light.lightType = Light2D.LightType.Point;
            light.color = created || ambientLight == null ? new Color(1f, 0.96f, 0.88f, 1f) : Color.Lerp(ambientLight.color, new Color(1f, 0.96f, 0.88f, 1f), 0.45f);
            light.intensity = created ? SunLightIntensity : Mathf.Max(light.intensity, SunLightIntensity);
            light.blendStyleIndex = ambientLight != null ? ambientLight.blendStyleIndex : 0;
            light.pointLightInnerAngle = 360f;
            light.pointLightOuterAngle = 360f;
            light.pointLightInnerRadius = fullBrightRadius;
            light.pointLightOuterRadius = outerRadius;
            light.falloffIntensity = 0.05f;
            light.targetSortingLayers = GetAllSortingLayerIds();
            CampusDynamicShadowUtility.ConfigureLightShadows(light, true, 0.45f, 0.75f, 0.85f);

            ConfigureLight2DNormalMap(light, Light2D.NormalMapQuality.Accurate, SunNormalMapDistance);
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.gameObject);
        }

        private static Bounds CalculateMapWorldBounds(CampusMapRoot root)
        {
            if (root == null)
            {
                return new Bounds(Vector3.zero, new Vector3(16f, 16f, 1f));
            }

            root.RebuildFloorReferences();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds || bounds.size.sqrMagnitude <= 0.0001f)
            {
                bounds = new Bounds(root.transform.position, new Vector3(16f, 16f, 1f));
            }

            return bounds;
        }

        private static int[] GetAllSortingLayerIds()
        {
            SortingLayer[] layers = SortingLayer.layers;
            if (layers == null || layers.Length == 0)
            {
                return new[] { 0 };
            }

            int[] ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                ids[i] = layers[i].id;
            }

            return ids;
        }

        private static void ConfigureLight2DNormalMap(Light2D light, Light2D.NormalMapQuality quality, float distance)
        {
            if (light == null)
            {
                return;
            }

            SerializedObject serializedLight = new SerializedObject(light);
            SerializedProperty normalMapQuality = serializedLight.FindProperty("m_NormalMapQuality");
            if (normalMapQuality != null)
            {
                normalMapQuality.intValue = (int)quality;
            }

            SerializedProperty normalMapDistance = serializedLight.FindProperty("m_NormalMapDistance");
            if (normalMapDistance != null)
            {
                normalMapDistance.floatValue = Mathf.Max(0f, distance);
            }

            SerializedProperty useNormalMap = serializedLight.FindProperty("m_UseNormalMap");
            if (useNormalMap != null)
            {
                useNormalMap.boolValue = quality != Light2D.NormalMapQuality.Disabled;
            }

            serializedLight.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveGenerated3DMapKeyLight()
        {
            Light[] sceneLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = sceneLights.Length - 1; i >= 0; i--)
            {
                Light sceneLight = sceneLights[i];
                if (sceneLight == null || !CampusObjectNames.MatchesAny(sceneLight.gameObject.name, CampusObjectNames.MapKeyLight, CampusObjectNames.LegacyMapKeyLight))
                {
                    continue;
                }

                if (RenderSettings.sun == sceneLight)
                {
                    RenderSettings.sun = null;
                }

                Undo.DestroyObjectImmediate(sceneLight.gameObject);
            }
        }

        private static void EnsureCurrentQualityUsesMaxShadows()
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames != null && qualityNames.Length > 0 && QualitySettings.GetQualityLevel() != qualityNames.Length - 1)
            {
                QualitySettings.SetQualityLevel(qualityNames.Length - 1, true);
            }

            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 150f);
            QualitySettings.shadowNearPlaneOffset = Mathf.Min(QualitySettings.shadowNearPlaneOffset, 1f);
        }

        private static void EnsureEditableSceneLoadedForBatchMode()
        {
            if (!Application.isBatchMode)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            {
                return;
            }

            const string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            if (File.Exists(sampleScenePath))
            {
                EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
            }
        }

        public static CampusWallRenderProfile GenerateWedgeWallTestAssets()
        {
            EnsureDirectories();
            DebugAssetSet debugSet = EnsureDebugAssets();
            string faceTexturePath = FindExternalWallTextureByFileName("混凝土砖墙.jpg");
            if (string.IsNullOrEmpty(faceTexturePath))
            {
                faceTexturePath = FindPreferredExternalWallTexture(false);
            }

            string capTexturePath = FindExternalWallTextureByFileName("灰墙.jpg");
            if (string.IsNullOrEmpty(capTexturePath))
            {
                capTexturePath = FindPreferredExternalCapTexture();
            }

            const string profileName = "测试楔形墙体_混凝土砖墙";
            PrototypeWallTiles tiles = EnsurePrototypeWallTiles(profileName, false, debugSet.WallTile, faceTexturePath, capTexturePath);
            string profilePath = WallProfilesPath + "/" + profileName + ".asset";
            CampusWallRenderProfile profile = EnsureWallRenderProfile(profilePath, "WedgeWallTestConcrete", tiles);
            AssignWallProfileSourceTextures(profile, faceTexturePath, capTexturePath);
            RegisterWallProfileBinding(tiles.LogicTile, profile, true);
            AddWallTileToDefaultPalette(tiles.LogicTile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }

        [MenuItem("Tools/Nting Campus/Import Tile Textures")]
        public static void ImportTileTexturesFromMenu()
        {
            DebugAssetSet debugSet = EnsureDebugAssets();
            EnsureDefaultPalettes(debugSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Nting Campus/Run Validation")]
        public static void RunValidationFromMenu()
        {
            RunValidation(FindCampusMapRoot(), LoadDefaultFloorPalette(), LoadDefaultWallPalette(), LoadDefaultPrefabPalette());
        }

        [MenuItem("Tools/Nting Campus/Fix Validation Issues")]
        public static void FixValidationIssuesFromMenu()
        {
            FixValidationIssues(FindCampusMapRoot(), LoadDefaultFloorPalette(), LoadDefaultWallPalette(), LoadDefaultPrefabPalette());
        }

        public static CampusTilePalette LoadDefaultFloorPalette()
        {
            return AssetDatabase.LoadAssetAtPath<CampusTilePalette>(DefaultFloorPalettePath);
        }

        public static CampusWallPalette LoadDefaultWallPalette()
        {
            return AssetDatabase.LoadAssetAtPath<CampusWallPalette>(DefaultWallPalettePath);
        }

        public static CampusPrefabPalette LoadDefaultPrefabPalette()
        {
            return AssetDatabase.LoadAssetAtPath<CampusPrefabPalette>(DefaultPrefabPalettePath);
        }

        public static CampusWallRenderProfile LoadDefaultWallRenderProfile()
        {
            return AssetDatabase.LoadAssetAtPath<CampusWallRenderProfile>(DefaultWallRenderProfilePath);
        }

        public static CampusWallRenderProfile LoadBrickWallRenderProfile()
        {
            return AssetDatabase.LoadAssetAtPath<CampusWallRenderProfile>(BrickWallRenderProfilePath);
        }

        public static CampusWallVisualCatalog LoadWallVisualCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<CampusWallVisualCatalog>(DefaultWallVisualCatalogPath);
        }

        public static CampusWallRenderProfile GetWallRenderProfileForLogicTile(TileBase logicTile, CampusWallRenderProfile fallback)
        {
            CampusWallVisualCatalog catalog = LoadWallVisualCatalog();
            return catalog != null ? catalog.GetProfileForLogicTile(logicTile, fallback) : fallback;
        }

        public static bool ApplyWallTextureSelection(CampusWallRenderProfile profile, Texture2D faceTexture, Texture2D capTexture)
        {
            return ApplyWallTextureSelection(profile != null ? profile.GetLogicTile() : null, profile, faceTexture, capTexture) != null;
        }

        public static CampusWallRenderProfile ApplyWallTextureSelection(TileBase logicTile, CampusWallRenderProfile fallbackProfile, Texture2D faceTexture, Texture2D capTexture)
        {
            if (logicTile == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Select a wall logic tile before applying wall textures.");
                return null;
            }

            EnsureDirectories();
            string faceTexturePath = GetTextureAssetPath(faceTexture);
            string capTexturePath = GetTextureAssetPath(capTexture);
            bool brickFace = IsBrickTextureName(Path.GetFileNameWithoutExtension(faceTexturePath));
            string profileName = MakeSafeAssetFileName(logicTile.name);
            if (string.IsNullOrEmpty(profileName))
            {
                profileName = "SelectedWall";
            }

            string profileAssetPath = GetWallProfileAssetPath(logicTile, fallbackProfile);
            string profileId = "WallVisual_" + profileName;
            PrototypeWallTiles tiles = EnsurePrototypeWallTiles(profileName, brickFace, logicTile, faceTexturePath, capTexturePath);
            CampusWallRenderProfile updatedProfile = EnsureWallRenderProfile(profileAssetPath, profileId, tiles);
            updatedProfile.LogicTile = logicTile;
            updatedProfile.FaceSourceTexture = faceTexture;
            updatedProfile.CapSourceTexture = capTexture;
            AssignWallProfileMaterials(updatedProfile);
            EditorUtility.SetDirty(updatedProfile);
            RegisterWallProfileBinding(logicTile, updatedProfile, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return updatedProfile;
        }

        public static CampusWallRenderProfile CreateWallTextureProfile(string requestedName, Texture2D faceTexture, Texture2D capTexture)
        {
            EnsureDirectories();
            string profileName = MakeWallProfileName(requestedName, faceTexture, capTexture);
            string faceTexturePath = GetTextureAssetPath(faceTexture);
            string capTexturePath = GetTextureAssetPath(capTexture);
            string textureName = !string.IsNullOrEmpty(faceTexturePath) ? Path.GetFileNameWithoutExtension(faceTexturePath) : string.Empty;
            bool brickFace = IsBrickTextureName(textureName);

            PrototypeWallTiles tiles = EnsurePrototypeWallTiles(profileName, brickFace, null, faceTexturePath, capTexturePath);
            if (tiles.LogicTile is Tile logicTile)
            {
                logicTile.name = profileName;
                logicTile.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(logicTile);
            }

            string profilePath = WallProfilesPath + "/" + profileName + " Wall Profile.asset";
            CampusWallRenderProfile profile = EnsureWallRenderProfile(profilePath, "WallVisual_" + profileName, tiles);
            profile.LogicTile = tiles.LogicTile;
            profile.FaceSourceTexture = faceTexture;
            profile.CapSourceTexture = capTexture;
            AssignWallProfileMaterials(profile);
            EditorUtility.SetDirty(profile);

            RegisterWallProfileBinding(tiles.LogicTile, profile, true);
            AddWallTileToDefaultPalette(tiles.LogicTile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }

        public static CampusWallRenderProfile EnsureWallRenderProfiles(DebugAssetSet debugSet)
        {
            EnsureDirectories();
            string defaultFaceTexturePath = FindPreferredExternalWallTexture(false);
            string brickFaceTexturePath = FindPreferredExternalWallTexture(true);
            string defaultCapTexturePath = FindPreferredExternalCapTexture();
            PrototypeWallTiles defaultTiles = EnsurePrototypeWallTiles("Default", false, debugSet.WallTile, defaultFaceTexturePath, defaultCapTexturePath);
            PrototypeWallTiles brickTiles = EnsurePrototypeWallTiles("Brick", true, debugSet.WallTile, brickFaceTexturePath, defaultCapTexturePath);
            CampusWallRenderProfile defaultProfile = EnsureWallRenderProfile(DefaultWallRenderProfilePath, "DefaultPrototypeWall", defaultTiles);
            CampusWallRenderProfile brickProfile = EnsureWallRenderProfile(BrickWallRenderProfilePath, "BrickPrototypeWall", brickTiles);
            AssignWallProfileSourceTextures(defaultProfile, defaultFaceTexturePath, defaultCapTexturePath);
            AssignWallProfileSourceTextures(brickProfile, brickFaceTexturePath, defaultCapTexturePath);

            CampusWallVisualCatalog catalog = LoadWallVisualCatalog();
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CampusWallVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, DefaultWallVisualCatalogPath);
            }

            catalog.DefaultProfile = defaultProfile;
            EnsureWallCatalogLists(catalog);
            AddWallProfileIfValid(catalog, defaultProfile);
            AddWallProfileIfValid(catalog, brickProfile);
            RegisterWallProfileBinding(catalog, defaultProfile != null ? defaultProfile.GetLogicTile() : null, defaultProfile);
            RegisterWallProfileBinding(catalog, brickProfile != null ? brickProfile.GetLogicTile() : null, brickProfile);

            EditorUtility.SetDirty(catalog);
            return defaultProfile;
        }

        private static void AssignWallProfileSourceTextures(CampusWallRenderProfile profile, string faceTexturePath, string capTexturePath)
        {
            if (profile == null)
            {
                return;
            }

            profile.FaceSourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(faceTexturePath);
            profile.CapSourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(capTexturePath);
            AssignWallProfileMaterials(profile);
            EditorUtility.SetDirty(profile);
        }

        private static void AssignWallProfileMaterials(CampusWallRenderProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            string profileName = MakeSafeAssetFileName(!string.IsNullOrEmpty(profile.ProfileId) ? profile.ProfileId : profile.name);
            if (string.IsNullOrEmpty(profileName))
            {
                profileName = "Wall";
            }

            profile.FaceMaterial = EnsureWallMeshMaterial(profile.FaceMaterial, profileName, "Wall", new Color(0.74f, 0.74f, 0.70f, 1f), profile.FaceSourceTexture);
            profile.CapMaterial = EnsureWallMeshMaterial(profile.CapMaterial, profileName, "Cap", new Color(0.88f, 0.86f, 0.78f, 1f), profile.CapSourceTexture != null ? profile.CapSourceTexture : profile.FaceSourceTexture);
            profile.EdgeMaterial = EnsureWallMeshMaterial(profile.EdgeMaterial, profileName, "Edge", new Color(0.045f, 0.040f, 0.035f, 1f), null);
        }

        private static Material EnsureWallMeshMaterial(Material current, string profileName, string suffix, Color color, Texture texture)
        {
            string assetPath = WallMaterialsPath + "/" + profileName + "_" + suffix + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (current != null)
            {
                string currentPath = AssetDatabase.GetAssetPath(current);
                if (!string.IsNullOrEmpty(currentPath) && !currentPath.StartsWith(WallMaterialsPath + "/", System.StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
            }

            if (material == null)
            {
                material = new Material(ResolveWallMeshShader(suffix == "Shadow"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            ConfigureWallMeshMaterial(material, suffix, color, texture);
            return material;
        }

        private static Shader ResolveWallMeshShader(bool shadow)
        {
            Shader shader = null;
            if (!shadow)
            {
                shader = Shader.Find(WallLitShaderName);
                if (shader == null)
                {
                    shader = Shader.Find(WallMeshLitShaderName);
                }
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return shader;
        }

        private static void ConfigureWallMeshMaterial(Material material, string suffix, Color color, Texture texture)
        {
            if (material == null)
            {
                return;
            }

            bool shadow = suffix == "Shadow";
            Shader shader = ResolveWallMeshShader(shadow);
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            string assetPath = AssetDatabase.GetAssetPath(material);
            material.name = "WallMesh_" + suffix;
            color.a = shadow ? color.a : 1f;
            material.color = color;
            material.mainTexture = texture != null ? texture : Texture2D.whiteTexture;
            material.renderQueue = shadow ? 3000 : -1;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_White"))
            {
                material.SetColor("_White", color);
            }

            ConfigureWallMeshTextureProperty(material, "_BaseMap", texture);
            ConfigureWallMeshTextureProperty(material, "_MainTex", texture);
            ConfigureWallMeshTextureProperty(material, "_MaskTex", Texture2D.whiteTexture);

            ConfigureWallTextureImport(texture);

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", 0);
            }

            ConfigureWallMeshBlendState(material, shadow);
            EditorUtility.SetDirty(material);
        }

        private static void ConfigureWallMeshTextureProperty(Material material, string propertyName, Texture texture)
        {
            if (material == null || !material.HasProperty(propertyName))
            {
                return;
            }

            material.SetTexture(propertyName, texture != null ? texture : Texture2D.whiteTexture);
            material.SetTextureScale(propertyName, Vector2.one);
        }

        private static void ConfigureWallTextureImport(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool dirty = false;
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureWallMeshBlendState(Material material, bool shadow)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", shadow ? 1f : 0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", shadow ? (int)UnityEngine.Rendering.BlendMode.SrcAlpha : (int)UnityEngine.Rendering.BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", shadow ? (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : (int)UnityEngine.Rendering.BlendMode.Zero);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", shadow ? 0 : 1);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            if (!shadow)
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
            }
        }

        private static string GetWallProfileAssetPath(TileBase logicTile, CampusWallRenderProfile fallbackProfile)
        {
            if (logicTile != null && fallbackProfile != null && fallbackProfile.GetLogicTile() == logicTile)
            {
                string fallbackPath = AssetDatabase.GetAssetPath(fallbackProfile);
                if (!string.IsNullOrEmpty(fallbackPath))
                {
                    return fallbackPath;
                }
            }

            string profileName = logicTile != null ? MakeSafeAssetFileName(logicTile.name) : "墙体";
            if (string.IsNullOrEmpty(profileName))
            {
                profileName = "墙体";
            }

            return WallProfilesPath + "/" + profileName + " 墙体配置.asset";
        }

        private static string MakeWallProfileName(string requestedName, Texture2D faceTexture, Texture2D capTexture)
        {
            string rawName = !string.IsNullOrWhiteSpace(requestedName) ? requestedName.Trim() : string.Empty;
            if (string.IsNullOrEmpty(rawName))
            {
                string faceName = faceTexture != null ? faceTexture.name : "墙面";
                string capName = capTexture != null ? capTexture.name : "墙顶";
                rawName = faceName + "_" + capName;
            }

            string safeName = MakeSafeAssetFileName(rawName);
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = "自定义墙体";
            }

            if (!safeName.StartsWith("墙体_", System.StringComparison.Ordinal))
            {
                safeName = "墙体_" + safeName;
            }

            return safeName;
        }

        private static bool IsBrickTextureName(string textureName)
        {
            return !string.IsNullOrEmpty(textureName) &&
                   (textureName.Contains("红砖") ||
                    textureName.IndexOf("brick", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AddWallTileToDefaultPalette(TileBase wallTile)
        {
            if (wallTile == null)
            {
                return;
            }

            CampusWallPalette wallPalette = LoadDefaultWallPalette();
            if (wallPalette == null)
            {
                wallPalette = ScriptableObject.CreateInstance<CampusWallPalette>();
                AssetDatabase.CreateAsset(wallPalette, DefaultWallPalettePath);
            }

            wallPalette.RemoveInvalidEntries();
            if (wallPalette.WallTiles == null)
            {
                wallPalette.WallTiles = new List<TileBase>();
            }

            AddTileIfValid(wallPalette.WallTiles, wallTile);
            if (wallPalette.HighWall == null)
            {
                wallPalette.HighWall = wallTile;
            }

            EditorUtility.SetDirty(wallPalette);
        }

        private static void EnsureWallProfilesForLogicTiles(List<TileBase> logicTiles, CampusWallRenderProfile fallbackProfile, string capTexturePath)
        {
            if (logicTiles == null)
            {
                return;
            }

            for (int i = 0; i < logicTiles.Count; i++)
            {
                TileBase logicTile = logicTiles[i];
                if (logicTile == null)
                {
                    continue;
                }

                CampusWallRenderProfile existing = GetWallRenderProfileForLogicTile(logicTile, fallbackProfile);
                if (existing != null && existing.GetLogicTile() == logicTile)
                {
                    RegisterWallProfileBinding(logicTile, existing, true);
                    continue;
                }

                string faceTexturePath = GetTileSpriteTexturePath(logicTile);
                if (string.IsNullOrEmpty(faceTexturePath))
                {
                    continue;
                }

                string profileName = MakeSafeAssetFileName(logicTile.name);
                string textureName = Path.GetFileNameWithoutExtension(faceTexturePath);
                bool brickFace = IsBrickTextureName(textureName);
                PrototypeWallTiles tiles = EnsurePrototypeWallTiles(profileName, brickFace, logicTile, faceTexturePath, capTexturePath);
                string profilePath = GetWallProfileAssetPath(logicTile, fallbackProfile);
                CampusWallRenderProfile profile = EnsureWallRenderProfile(profilePath, "WallVisual_" + profileName, tiles);
                profile.LogicTile = logicTile;
                profile.FaceSourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(faceTexturePath);
                profile.CapSourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(capTexturePath);
                EditorUtility.SetDirty(profile);
                RegisterWallProfileBinding(logicTile, profile, true);
            }
        }

        private static string GetTileSpriteTexturePath(TileBase tileBase)
        {
            Tile tile = tileBase as Tile;
            if (tile == null || tile.sprite == null || tile.sprite.texture == null)
            {
                return string.Empty;
            }

            return AssetDatabase.GetAssetPath(tile.sprite.texture);
        }

        private static void RegisterWallProfileBinding(TileBase logicTile, CampusWallRenderProfile profile, bool markDirty)
        {
            CampusWallVisualCatalog catalog = LoadWallVisualCatalog();
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CampusWallVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, DefaultWallVisualCatalogPath);
            }

            RegisterWallProfileBinding(catalog, logicTile, profile);
            if (markDirty)
            {
                EditorUtility.SetDirty(catalog);
            }
        }

        private static void RegisterWallProfileBinding(CampusWallVisualCatalog catalog, TileBase logicTile, CampusWallRenderProfile profile)
        {
            if (catalog == null || logicTile == null || profile == null)
            {
                return;
            }

            EnsureWallCatalogLists(catalog);
            AddWallProfileIfValid(catalog, profile);
            for (int i = 0; i < catalog.ProfileBindings.Count; i++)
            {
                CampusWallVisualProfileBinding binding = catalog.ProfileBindings[i];
                if (binding != null && binding.LogicTile == logicTile)
                {
                    binding.Profile = profile;
                    return;
                }
            }

            catalog.ProfileBindings.Add(new CampusWallVisualProfileBinding
            {
                LogicTile = logicTile,
                Profile = profile
            });
        }

        private static void EnsureWallCatalogLists(CampusWallVisualCatalog catalog)
        {
            if (catalog.Profiles == null)
            {
                catalog.Profiles = new List<CampusWallRenderProfile>();
            }

            if (catalog.ProfileBindings == null)
            {
                catalog.ProfileBindings = new List<CampusWallVisualProfileBinding>();
            }
        }

        private static void AddWallProfileIfValid(CampusWallVisualCatalog catalog, CampusWallRenderProfile profile)
        {
            if (catalog != null && profile != null && !catalog.Profiles.Contains(profile))
            {
                catalog.Profiles.Add(profile);
            }
        }

        public static void RebuildWallVisuals(CampusFloorRoot floor, CampusWallRenderProfile profile)
        {
            if (floor == null)
            {
                return;
            }

            EnsureFloorStructure(FindCampusMapRoot(), floor, false);
            CampusWallVisualCatalog catalog = LoadWallVisualCatalog();
            EnsureWallMeshMaterialsForCatalog(catalog, profile);
            CampusWallAutoRenderer.RebuildFloor(floor, catalog, profile);
            MarkWallTilemapsDirty(floor);
            EditorUtility.SetDirty(floor);
            MarkSceneDirty();
        }

        private static void EnsureWallMeshMaterialsForCatalog(CampusWallVisualCatalog catalog, CampusWallRenderProfile fallbackProfile)
        {
            AssignWallProfileMaterials(fallbackProfile);
            if (catalog == null)
            {
                return;
            }

            AssignWallProfileMaterials(catalog.DefaultProfile);
            if (catalog.Profiles != null)
            {
                for (int i = 0; i < catalog.Profiles.Count; i++)
                {
                    AssignWallProfileMaterials(catalog.Profiles[i]);
                }
            }

            if (catalog.ProfileBindings != null)
            {
                for (int i = 0; i < catalog.ProfileBindings.Count; i++)
                {
                    CampusWallVisualProfileBinding binding = catalog.ProfileBindings[i];
                    if (binding != null)
                    {
                        AssignWallProfileMaterials(binding.Profile);
                    }
                }
            }
        }

        public static void RebuildAllWallVisuals(CampusMapRoot root, CampusWallRenderProfile profile)
        {
            if (root == null)
            {
                return;
            }

            EnsureNotPreviewingBeforeEdit(root);
            RebuildFloorReferences(root);
            root.RebuildFloorReferences();
            for (int i = 0; i < root.Floors.Count; i++)
            {
                RebuildWallVisuals(root.Floors[i], profile);
                CampusDynamicShadowUtility.EnsureObjectShadowCasters(root.Floors[i]);
            }

            root.CaptureFloorOriginalStates(true);
            EditorUtility.SetDirty(root);
            MarkSceneDirty();
        }

        public static void ApplyWallDebugView(CampusMapRoot root, CampusWallDebugView view)
        {
            if (root == null)
            {
                return;
            }

            root.RebuildFloorReferences();
            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusWallAutoRenderer.ApplyDebugView(root.Floors[i], view);
            }

            SceneView.RepaintAll();
        }

        private static void MarkWallTilemapsDirty(CampusFloorRoot floor)
        {
            if (floor == null)
            {
                return;
            }

            SetDirtyIfNotNull(CampusWallTileUtility.GetWallLogicTilemap(floor));
            SetDirtyIfNotNull(floor.WallCapTilemap);
            SetDirtyIfNotNull(floor.WallFaceTilemap);
            SetDirtyIfNotNull(floor.WallSideTilemap);
            SetDirtyIfNotNull(floor.WallOverlayTilemap);
            SetDirtyIfNotNull(floor.WallMeshRoot);
        }

        private static void SetDirtyIfNotNull(Object target)
        {
            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }
        }

        public static void EnsureDefaultPalettes(DebugAssetSet debugSet)
        {
            CampusWallRenderProfile defaultWallProfile = EnsureWallRenderProfiles(debugSet);
            List<TileBase> importedTiles = ImportExternalTextureTiles(
                Path.Combine(GetProjectRootPath(), ExternalTileFolderName, ExternalFloorFolderName),
                ImportedFloorSourceTexturePath,
                ImportedFloorTilesPath,
                Tile.ColliderType.None,
                "地面_");
            CampusTilePalette tilePalette = LoadDefaultFloorPalette();
            if (tilePalette == null)
            {
                tilePalette = ScriptableObject.CreateInstance<CampusTilePalette>();
                AssetDatabase.CreateAsset(tilePalette, DefaultFloorPalettePath);
            }

            tilePalette.FloorTiles.Clear();
            for (int i = 0; i < importedTiles.Count; i++)
            {
                if (importedTiles[i] != null && !tilePalette.FloorTiles.Contains(importedTiles[i]))
                {
                    tilePalette.FloorTiles.Add(importedTiles[i]);
                }
            }

            if (debugSet.FloorTile != null && !tilePalette.FloorTiles.Contains(debugSet.FloorTile))
            {
                tilePalette.FloorTiles.Add(debugSet.FloorTile);
            }

            EditorUtility.SetDirty(tilePalette);

            CampusWallPalette wallPalette = LoadDefaultWallPalette();
            if (wallPalette == null)
            {
                wallPalette = ScriptableObject.CreateInstance<CampusWallPalette>();
                AssetDatabase.CreateAsset(wallPalette, DefaultWallPalettePath);
            }

            string wallSourceFolder = Path.Combine(GetProjectRootPath(), ExternalTileFolderName, ExternalWallFolderName);
            List<TileBase> importedWallLogicTiles = ImportExternalTextureTiles(
                wallSourceFolder,
                ImportedWallSourceTexturePath,
                ImportedWallTilesPath,
                Tile.ColliderType.Grid,
                "墙体_");
            EnsureWallProfilesForLogicTiles(importedWallLogicTiles, defaultWallProfile, FindPreferredExternalCapTexture());
            TileBase logicWall = defaultWallProfile != null && defaultWallProfile.GetLogicTile() != null
                ? defaultWallProfile.GetLogicTile()
                : debugSet.WallTile;
            wallPalette.HorizontalWall = logicWall;
            wallPalette.VerticalWall = logicWall;
            wallPalette.CornerWall = logicWall;
            wallPalette.HighWall = logicWall;
            wallPalette.WallTiles.Clear();
            AddTileIfValid(wallPalette.WallTiles, logicWall);
            for (int i = 0; i < importedWallLogicTiles.Count; i++)
            {
                AddTileIfValid(wallPalette.WallTiles, importedWallLogicTiles[i]);
            }

            AddTileIfValid(wallPalette.WallTiles, debugSet.WallTile);
            EditorUtility.SetDirty(wallPalette);

            CampusPrefabPalette prefabPalette = LoadDefaultPrefabPalette();
            if (prefabPalette == null)
            {
                prefabPalette = ScriptableObject.CreateInstance<CampusPrefabPalette>();
                AssetDatabase.CreateAsset(prefabPalette, DefaultPrefabPalettePath);
            }

            prefabPalette.Prefabs.Clear();
            AddPrefabIfValid(prefabPalette.Prefabs, debugSet.PropBoxPrefab);
            AddPrefabAssetsFromFolder(prefabPalette.Prefabs, PropPrefabsPath);

            EditorUtility.SetDirty(prefabPalette);
        }

        private static PrototypeWallTiles EnsurePrototypeWallTiles(string profileName, bool brickFace, TileBase fallbackLogicTile, string sourceWallTexturePath, string sourceCapTexturePath)
        {
            PrototypeWallTiles tiles = new PrototypeWallTiles();
            string prefix = profileName + "_";
            Sprite logicSprite = EnsurePrototypeWallSprite(prefix + "WallLogic", WallPrototypeSpriteKind.Logic, 0, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.LogicTile = EnsureTile(PrototypeWallTilesPath + "/" + prefix + "WallLogic.asset", logicSprite, Tile.ColliderType.Grid);
            if (tiles.LogicTile == null)
            {
                tiles.LogicTile = fallbackLogicTile;
            }

            tiles.CapTiles = new TileBase[16];
            for (int mask = 0; mask < tiles.CapTiles.Length; mask++)
            {
                Sprite capSprite = EnsurePrototypeWallSprite(prefix + "WallCap_" + mask, WallPrototypeSpriteKind.Cap, mask, brickFace, sourceWallTexturePath, sourceCapTexturePath);
                tiles.CapTiles[mask] = EnsureTile(PrototypeWallTilesPath + "/" + prefix + "WallCap_" + mask + ".asset", capSprite, Tile.ColliderType.None);
            }

            tiles.DefaultCapTile = tiles.CapTiles[0];
            Sprite faceSprite = EnsurePrototypeWallSprite(prefix + "Generated_FaceSouth_32", WallPrototypeSpriteKind.FaceSouth, 0, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            Sprite westSideSprite = EnsurePrototypeWallSprite(prefix + "WallSideWest", WallPrototypeSpriteKind.SideWest, 0, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            Sprite eastSideSprite = EnsurePrototypeWallSprite(prefix + "WallSideEast", WallPrototypeSpriteKind.SideEast, 0, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            Sprite bothSideSprite = EnsurePrototypeWallSprite(prefix + "WallSideBoth", WallPrototypeSpriteKind.SideBoth, 0, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.SouthFaceTile = EnsureTile(PrototypeWallTilesPath + "/" + prefix + "Generated_FaceSouth_32.asset", faceSprite, Tile.ColliderType.None);
            tiles.WestSideTile = EnsureTile(PrototypeWallTilesPath + "/" + prefix + "WallSideWest.asset", westSideSprite, Tile.ColliderType.None);
            tiles.EastSideTile = EnsureTile(PrototypeWallTilesPath + "/" + prefix + "WallSideEast.asset", eastSideSprite, Tile.ColliderType.None);
            tiles.BothSideTile = EnsureTile(PrototypeWallTilesPath + "/" + prefix + "WallSideBoth.asset", bothSideSprite, Tile.ColliderType.None);
            tiles.EndNorth = EnsurePrototypeOverlayTile(prefix, "EndNorth", WallPrototypeSpriteKind.EndNorth, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.EndEast = EnsurePrototypeOverlayTile(prefix, "EndEast", WallPrototypeSpriteKind.EndEast, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.EndSouth = EnsurePrototypeOverlayTile(prefix, "EndSouth", WallPrototypeSpriteKind.EndSouth, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.EndWest = EnsurePrototypeOverlayTile(prefix, "EndWest", WallPrototypeSpriteKind.EndWest, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.OuterCornerNE = EnsurePrototypeOverlayTile(prefix, "OuterCornerNE", WallPrototypeSpriteKind.OuterCornerNE, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.OuterCornerNW = EnsurePrototypeOverlayTile(prefix, "OuterCornerNW", WallPrototypeSpriteKind.OuterCornerNW, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.OuterCornerSE = EnsurePrototypeOverlayTile(prefix, "OuterCornerSE", WallPrototypeSpriteKind.OuterCornerSE, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.OuterCornerSW = EnsurePrototypeOverlayTile(prefix, "OuterCornerSW", WallPrototypeSpriteKind.OuterCornerSW, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.InnerCornerNE = EnsurePrototypeOverlayTile(prefix, "InnerCornerNE", WallPrototypeSpriteKind.InnerCornerNE, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.InnerCornerNW = EnsurePrototypeOverlayTile(prefix, "InnerCornerNW", WallPrototypeSpriteKind.InnerCornerNW, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.InnerCornerSE = EnsurePrototypeOverlayTile(prefix, "InnerCornerSE", WallPrototypeSpriteKind.InnerCornerSE, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.InnerCornerSW = EnsurePrototypeOverlayTile(prefix, "InnerCornerSW", WallPrototypeSpriteKind.InnerCornerSW, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.TJunctionNorth = EnsurePrototypeOverlayTile(prefix, "TJunctionNorth", WallPrototypeSpriteKind.TJunctionNorth, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.TJunctionEast = EnsurePrototypeOverlayTile(prefix, "TJunctionEast", WallPrototypeSpriteKind.TJunctionEast, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.TJunctionSouth = EnsurePrototypeOverlayTile(prefix, "TJunctionSouth", WallPrototypeSpriteKind.TJunctionSouth, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.TJunctionWest = EnsurePrototypeOverlayTile(prefix, "TJunctionWest", WallPrototypeSpriteKind.TJunctionWest, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            tiles.Cross = EnsurePrototypeOverlayTile(prefix, "Cross", WallPrototypeSpriteKind.Cross, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            return tiles;
        }

        private static TileBase EnsurePrototypeOverlayTile(string prefix, string tileName, WallPrototypeSpriteKind kind, bool brickFace, string sourceWallTexturePath, string sourceCapTexturePath)
        {
            string assetName = prefix + "WallOverlay_" + tileName;
            Sprite sprite = EnsurePrototypeWallSprite(assetName, kind, 0, brickFace, sourceWallTexturePath, sourceCapTexturePath);
            return EnsureTile(PrototypeWallTilesPath + "/" + assetName + ".asset", sprite, Tile.ColliderType.None);
        }

        private static CampusWallRenderProfile EnsureWallRenderProfile(string assetPath, string profileId, PrototypeWallTiles tiles)
        {
            CampusWallRenderProfile profile = AssetDatabase.LoadAssetAtPath<CampusWallRenderProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CampusWallRenderProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.ProfileId = profileId;
            profile.LogicTile = tiles.LogicTile;
            profile.DefaultCapTile = tiles.DefaultCapTile;
            if (profile.CapTilesByExposedMask == null || profile.CapTilesByExposedMask.Length != 16)
            {
                profile.CapTilesByExposedMask = new TileBase[16];
            }

            for (int i = 0; i < profile.CapTilesByExposedMask.Length; i++)
            {
                profile.CapTilesByExposedMask[i] = tiles.CapTiles != null && i < tiles.CapTiles.Length && tiles.CapTiles[i] != null
                    ? tiles.CapTiles[i]
                    : tiles.DefaultCapTile;
            }

            profile.SouthFaceTile = tiles.SouthFaceTile;
            profile.WestSideTile = tiles.WestSideTile;
            profile.EastSideTile = tiles.EastSideTile;
            profile.BothSideTile = tiles.BothSideTile;
            profile.EndNorth = tiles.EndNorth;
            profile.EndEast = tiles.EndEast;
            profile.EndSouth = tiles.EndSouth;
            profile.EndWest = tiles.EndWest;
            profile.OuterCornerNE = tiles.OuterCornerNE;
            profile.OuterCornerNW = tiles.OuterCornerNW;
            profile.OuterCornerSE = tiles.OuterCornerSE;
            profile.OuterCornerSW = tiles.OuterCornerSW;
            profile.InnerCornerNE = tiles.InnerCornerNE;
            profile.InnerCornerNW = tiles.InnerCornerNW;
            profile.InnerCornerSE = tiles.InnerCornerSE;
            profile.InnerCornerSW = tiles.InnerCornerSW;
            profile.TJunctionNorth = tiles.TJunctionNorth;
            profile.TJunctionEast = tiles.TJunctionEast;
            profile.TJunctionSouth = tiles.TJunctionSouth;
            profile.TJunctionWest = tiles.TJunctionWest;
            profile.Cross = tiles.Cross;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Sprite EnsurePrototypeWallSprite(string spriteName, WallPrototypeSpriteKind kind, int mask, bool brickFace, string sourceWallTexturePath, string sourceCapTexturePath)
        {
            string assetPath = PrototypeWallSourceTexturePath + "/" + spriteName + ".png";
            string fullPath = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            const int size = 128;
            Texture2D faceTexture = LoadTextureFromAssetPath(sourceWallTexturePath);
            Texture2D capTexture = LoadTextureFromAssetPath(sourceCapTexturePath);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, GetPrototypeWallPixel(x, y, size, kind, mask, brickFace, faceTexture, capTexture));
                }
            }

            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            if (faceTexture != null)
            {
                Object.DestroyImmediate(faceTexture);
            }

            if (capTexture != null)
            {
                Object.DestroyImmediate(capTexture);
            }

            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            EnsureSpriteImporter(assetPath, size, new Vector2(0.5f, 0.5f));
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Color GetPrototypeWallPixel(int x, int y, int size, WallPrototypeSpriteKind kind, int mask, bool brickFace, Texture2D sourceTexture, Texture2D capTexture)
        {
            float u = (float)x / (size - 1);
            float v = (float)y / (size - 1);
            float noise = (((x * 17 + y * 31) % 11) - 5) / 255f;
            Color sampledCap = SampleSourceWall(capTexture, u, v);
            Color cap = sampledCap.a > 0f ? Color.Lerp(sampledCap, Color.white, 0.10f) : (brickFace ? new Color(0.74f, 0.70f, 0.62f, 1f) : new Color(0.72f, 0.73f, 0.70f, 1f));
            Color sampledFace = SampleSourceWallFace(sourceTexture, u, v, brickFace);
            Color face = sampledFace.a > 0f ? sampledFace : (brickFace ? new Color(0.56f, 0.24f, 0.16f, 1f) : new Color(0.52f, 0.58f, 0.61f, 1f));
            Color mortar = brickFace ? new Color(0.28f, 0.21f, 0.19f, 1f) : new Color(0.42f, 0.47f, 0.49f, 1f);

            switch (kind)
            {
                case WallPrototypeSpriteKind.Logic:
                    return new Color(0.95f, 0.45f, 0.12f, 0.38f);
                case WallPrototypeSpriteKind.Cap:
                    return GetWedgeCapPixel(x, y, size, mask, cap, noise);
                case WallPrototypeSpriteKind.FaceSouth:
                    return GetWedgeFacePixel(x, y, size, face, mortar, brickFace, sourceTexture);
                case WallPrototypeSpriteKind.SideWest:
                    return GetPrototypeSidePixel(x, y, size, face, mortar, brickFace, sourceTexture, true, false);
                case WallPrototypeSpriteKind.SideEast:
                    return GetPrototypeSidePixel(x, y, size, face, mortar, brickFace, sourceTexture, false, false);
                case WallPrototypeSpriteKind.SideBoth:
                    return GetPrototypeBothSidePixel(x, y, size, face, mortar, brickFace, sourceTexture);
                case WallPrototypeSpriteKind.EndNorth:
                case WallPrototypeSpriteKind.EndEast:
                case WallPrototypeSpriteKind.EndSouth:
                case WallPrototypeSpriteKind.EndWest:
                case WallPrototypeSpriteKind.OuterCornerNE:
                case WallPrototypeSpriteKind.OuterCornerNW:
                case WallPrototypeSpriteKind.OuterCornerSE:
                case WallPrototypeSpriteKind.OuterCornerSW:
                case WallPrototypeSpriteKind.InnerCornerNE:
                case WallPrototypeSpriteKind.InnerCornerNW:
                case WallPrototypeSpriteKind.InnerCornerSE:
                case WallPrototypeSpriteKind.InnerCornerSW:
                case WallPrototypeSpriteKind.TJunctionNorth:
                case WallPrototypeSpriteKind.TJunctionEast:
                case WallPrototypeSpriteKind.TJunctionSouth:
                case WallPrototypeSpriteKind.TJunctionWest:
                case WallPrototypeSpriteKind.Cross:
                    return GetPrototypeOverlayPixel(x, y, size, kind, brickFace);
                default:
                    return Color.clear;
            }
        }

        private static Color GetPrototypeOverlayPixel(int x, int y, int size, WallPrototypeSpriteKind kind, bool brickFace)
        {
            float u = (float)x / (size - 1);
            float v = (float)y / (size - 1);
            Vector2 p = new Vector2(u, v);
            int connectionMask = GetOverlayConnectionMask(kind);
            if (connectionMask < 0)
            {
                return Color.clear;
            }

            const float center = 0.5f;
            const float lineHalfWidth = 0.020f;
            const float coreHalfWidth = 0.070f;
            const float end = 0.93f;
            bool path = Mathf.Abs(p.x - center) <= lineHalfWidth && (
                ((connectionMask & CampusWallTileUtility.NorthMask) != 0 && p.y >= center && p.y <= end) ||
                ((connectionMask & CampusWallTileUtility.SouthMask) != 0 && p.y <= center && p.y >= 1f - end));
            path |= Mathf.Abs(p.y - center) <= lineHalfWidth && (
                ((connectionMask & CampusWallTileUtility.EastMask) != 0 && p.x >= center && p.x <= end) ||
                ((connectionMask & CampusWallTileUtility.WestMask) != 0 && p.x <= center && p.x >= 1f - end));

            bool core = Mathf.Abs(p.x - center) <= coreHalfWidth && Mathf.Abs(p.y - center) <= coreHalfWidth;
            if (!path && !core)
            {
                return Color.clear;
            }

            Color mark = brickFace ? new Color(0.08f, 0.055f, 0.045f, 0.26f) : new Color(0.06f, 0.065f, 0.06f, 0.22f);
            if (core)
            {
                mark = Color.Lerp(mark, Color.white, 0.18f);
            }
            else if ((x + y) % 11 == 0)
            {
                mark = Color.Lerp(mark, Color.white, 0.12f);
            }

            return mark;
        }

        private static Color GetWedgeCapPixel(int x, int y, int size, int connectionMask, Color cap, float noise)
        {
            float u = (float)x / (size - 1);
            float v = (float)y / (size - 1);
            Color outline = new Color(0.045f, 0.04f, 0.035f, 1f);
            bool north = (connectionMask & CampusWallTileUtility.NorthMask) != 0;
            bool east = (connectionMask & CampusWallTileUtility.EastMask) != 0;
            bool south = (connectionMask & CampusWallTileUtility.SouthMask) != 0;
            bool west = (connectionMask & CampusWallTileUtility.WestMask) != 0;
            bool hasHorizontal = east || west;
            bool hasVertical = north || south;
            if (!hasHorizontal && !hasVertical)
            {
                hasHorizontal = true;
                east = true;
                west = true;
            }

            const float capBottom = 0.75f;
            const float capTop = 0.97f;
            const float stripLeft = 0.39f;
            const float stripRight = 0.61f;
            const float edgeWidth = 0.030f;

            bool inHorizontal = false;
            bool horizontalEdge = false;
            if (hasHorizontal)
            {
                float minX = hasVertical && !west ? stripLeft : 0f;
                float maxX = hasVertical && !east ? stripRight : 1f;
                inHorizontal = u >= minX && u <= maxX && v >= capBottom && v <= capTop;
                horizontalEdge = inHorizontal &&
                    (v <= capBottom + edgeWidth || v >= capTop - edgeWidth ||
                     (minX > 0f && u <= minX + edgeWidth) ||
                     (maxX < 1f && u >= maxX - edgeWidth));
            }

            bool inVertical = false;
            bool verticalEdge = false;
            if (hasVertical)
            {
                float minY = hasHorizontal && !south ? capBottom : 0f;
                float maxY = hasHorizontal && !north ? capTop : 1f;
                inVertical = u >= stripLeft && u <= stripRight && v >= minY && v <= maxY;
                verticalEdge = inVertical &&
                    (u <= stripLeft + edgeWidth || u >= stripRight - edgeWidth ||
                     (minY > 0f && v <= minY + edgeWidth) ||
                     (maxY < 1f && v >= maxY - edgeWidth));
            }

            if (!inHorizontal && !inVertical)
            {
                return Color.clear;
            }

            if (horizontalEdge || verticalEdge)
            {
                return outline;
            }

            Color capColor = cap * (1.02f + noise);
            if (inHorizontal)
            {
                float capT = Mathf.InverseLerp(capBottom, capTop, v);
                capColor = Color.Lerp(capColor, Color.white, 0.18f + capT * 0.24f);
                capColor = Color.Lerp(capColor, Color.black, (1f - capT) * 0.10f);
            }

            if (inVertical)
            {
                float centerFalloff = Mathf.Abs(u - 0.5f) / 0.11f;
                capColor = Color.Lerp(capColor, Color.white, Mathf.Clamp01(1f - centerFalloff) * 0.16f);
                capColor = Color.Lerp(capColor, Color.black, Mathf.Clamp01(centerFalloff) * 0.12f);
            }

            if ((x + y * 3) % 23 == 0)
            {
                capColor = Color.Lerp(capColor, Color.black, 0.08f);
            }

            capColor.a = 1f;
            return capColor;
        }

        private static Color GetWedgeFacePixel(int x, int y, int size, Color face, Color mortar, bool brickFace, Texture2D sourceTexture)
        {
            float v = (float)y / (size - 1);
            Color outline = new Color(0.045f, 0.035f, 0.03f, 1f);
            if (y <= 3 || y >= size - 4)
            {
                return outline;
            }

            Color faceColor = face * Mathf.Lerp(0.58f, 0.88f, v);
            if (sourceTexture == null && brickFace)
            {
                int row = y / 12;
                int offset = row % 2 == 0 ? 0 : 12;
                bool mortarLine = y % 12 <= 1 || (x + offset) % 24 <= 1;
                faceColor = mortarLine ? Color.Lerp(faceColor, mortar, 0.70f) : faceColor;
            }
            else if (sourceTexture == null && y % 13 <= 1)
            {
                faceColor = Color.Lerp(faceColor, mortar, 0.38f);
            }

            if (x < 5 || x > size - 6)
            {
                faceColor = Color.Lerp(faceColor, outline, 0.30f);
            }

            if ((x * 5 + y * 7) % 37 == 0)
            {
                faceColor = Color.Lerp(faceColor, Color.white, 0.10f);
            }

            faceColor.a = 1f;
            return faceColor;
        }

        private static Color GetPrototypeSidePixel(int x, int y, int size, Color face, Color mortar, bool brickFace, Texture2D sourceTexture, bool westSide, bool transparentOutside)
        {
            float u = (float)x / (size - 1);
            float v = (float)y / (size - 1);
            if (transparentOutside && !IsInSideStrip(u, westSide))
            {
                return Color.clear;
            }

            Color outline = new Color(0.045f, 0.035f, 0.03f, 1f);
            float outerEdge = westSide ? u : 1f - u;
            if (outerEdge <= 0.035f || y <= 3 || y >= size - 4)
            {
                return outline;
            }

            Color side = face * (westSide ? 0.76f : 0.60f) * Mathf.Lerp(0.70f, 1.03f, v);
            if (sourceTexture == null && brickFace)
            {
                bool mortarLine = y % 12 <= 1 || x % 22 <= 1;
                side = mortarLine ? Color.Lerp(side, mortar, 0.55f) : side;
            }
            else if (sourceTexture == null && y % 13 <= 1)
            {
                side = Color.Lerp(side, mortar, 0.28f);
            }

            side = Color.Lerp(side, Color.white, Mathf.Clamp01((0.13f - outerEdge) / 0.13f) * 0.16f);
            side = Color.Lerp(side, Color.black, Mathf.Clamp01((outerEdge - 0.68f) / 0.30f) * 0.22f);
            side.a = 1f;
            return side;
        }

        private static Color GetPrototypeBothSidePixel(int x, int y, int size, Color face, Color mortar, bool brickFace, Texture2D sourceTexture)
        {
            float u = (float)x / (size - 1);
            if (IsInSideStrip(u, true))
            {
                return GetPrototypeSidePixel(x, y, size, face, mortar, brickFace, sourceTexture, true, false);
            }

            if (IsInSideStrip(u, false))
            {
                return GetPrototypeSidePixel(x, y, size, face, mortar, brickFace, sourceTexture, false, false);
            }

            return Color.clear;
        }

        private static bool IsInSideStrip(float u, bool westSide)
        {
            return westSide ? u <= 0.18f : u >= 0.82f;
        }

        private static int GetOverlayConnectionMask(WallPrototypeSpriteKind kind)
        {
            switch (kind)
            {
                case WallPrototypeSpriteKind.EndNorth:
                    return CampusWallTileUtility.NorthMask;
                case WallPrototypeSpriteKind.EndEast:
                    return CampusWallTileUtility.EastMask;
                case WallPrototypeSpriteKind.EndSouth:
                    return CampusWallTileUtility.SouthMask;
                case WallPrototypeSpriteKind.EndWest:
                    return CampusWallTileUtility.WestMask;
                case WallPrototypeSpriteKind.OuterCornerNE:
                case WallPrototypeSpriteKind.InnerCornerNE:
                    return CampusWallTileUtility.NorthMask | CampusWallTileUtility.EastMask;
                case WallPrototypeSpriteKind.OuterCornerNW:
                case WallPrototypeSpriteKind.InnerCornerNW:
                    return CampusWallTileUtility.NorthMask | CampusWallTileUtility.WestMask;
                case WallPrototypeSpriteKind.OuterCornerSE:
                case WallPrototypeSpriteKind.InnerCornerSE:
                    return CampusWallTileUtility.SouthMask | CampusWallTileUtility.EastMask;
                case WallPrototypeSpriteKind.OuterCornerSW:
                case WallPrototypeSpriteKind.InnerCornerSW:
                    return CampusWallTileUtility.SouthMask | CampusWallTileUtility.WestMask;
                case WallPrototypeSpriteKind.TJunctionNorth:
                    return CampusWallTileUtility.EastMask | CampusWallTileUtility.SouthMask | CampusWallTileUtility.WestMask;
                case WallPrototypeSpriteKind.TJunctionEast:
                    return CampusWallTileUtility.NorthMask | CampusWallTileUtility.SouthMask | CampusWallTileUtility.WestMask;
                case WallPrototypeSpriteKind.TJunctionSouth:
                    return CampusWallTileUtility.NorthMask | CampusWallTileUtility.EastMask | CampusWallTileUtility.WestMask;
                case WallPrototypeSpriteKind.TJunctionWest:
                    return CampusWallTileUtility.NorthMask | CampusWallTileUtility.EastMask | CampusWallTileUtility.SouthMask;
                case WallPrototypeSpriteKind.Cross:
                    return CampusWallTileUtility.NorthMask | CampusWallTileUtility.EastMask | CampusWallTileUtility.SouthMask | CampusWallTileUtility.WestMask;
                default:
                    return -1;
            }
        }

        private static string FindPreferredExternalWallTexture(bool brickFace)
        {
            string sourceFolder = Path.Combine(GetProjectRootPath(), ExternalTileFolderName, ExternalWallFolderName);
            if (!Directory.Exists(sourceFolder))
            {
                return string.Empty;
            }

            string[] texturePaths = Directory.GetFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => IsSupportedTextureFile(path))
                .OrderBy(path => Path.GetFileNameWithoutExtension(path))
                .ToArray();
            if (texturePaths.Length == 0)
            {
                return string.Empty;
            }

            string preferred = string.Empty;
            for (int i = 0; i < texturePaths.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(texturePaths[i]);
                if (brickFace && fileName.Contains("红砖"))
                {
                    preferred = texturePaths[i];
                    break;
                }

                if (!brickFace && (fileName.Contains("灰墙") || fileName.Contains("混凝土")))
                {
                    preferred = texturePaths[i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(preferred))
            {
                preferred = texturePaths[0];
            }

            return CopyTextureIntoAssets(preferred, ImportedWallSourceTexturePath);
        }

        private static string FindExternalWallTextureByFileName(string fileName)
        {
            string sourcePath = Path.Combine(GetProjectRootPath(), ExternalTileFolderName, ExternalWallFolderName, fileName);
            if (File.Exists(sourcePath))
            {
                return CopyTextureIntoAssets(sourcePath, ImportedWallSourceTexturePath);
            }

            string fallbackSourcePath = Path.Combine(GetProjectRootPath(), "瓦片", ExternalWallFolderName, fileName);
            if (File.Exists(fallbackSourcePath))
            {
                return CopyTextureIntoAssets(fallbackSourcePath, ImportedWallSourceTexturePath);
            }

            return string.Empty;
        }

        private static string FindPreferredExternalCapTexture()
        {
            string preferredWallTopPath = Path.Combine(GetProjectRootPath(), ExternalTileFolderName, ExternalWallFolderName, "灰墙.jpg");
            if (File.Exists(preferredWallTopPath))
            {
                return CopyTextureIntoAssets(preferredWallTopPath, ImportedWallSourceTexturePath);
            }

            string sourceFolder = Path.Combine(GetProjectRootPath(), ExternalTileFolderName, ExternalFloorFolderName);
            if (!Directory.Exists(sourceFolder))
            {
                return string.Empty;
            }

            string[] texturePaths = Directory.GetFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => IsSupportedTextureFile(path))
                .OrderBy(path => Path.GetFileNameWithoutExtension(path))
                .ToArray();
            if (texturePaths.Length == 0)
            {
                return string.Empty;
            }

            string preferred = string.Empty;
            for (int i = 0; i < texturePaths.Length; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(texturePaths[i]);
                if (fileName.Contains("白色瓷砖") || fileName.Contains("内部瓷砖") || fileName.Contains("地板瓷砖"))
                {
                    preferred = texturePaths[i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(preferred))
            {
                preferred = texturePaths[0];
            }

            return CopyTextureIntoAssets(preferred, ImportedFloorSourceTexturePath);
        }

        private static string GetTextureAssetPath(Texture2D texture)
        {
            if (texture == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            return string.IsNullOrEmpty(path) ? string.Empty : path;
        }

        private static Texture2D LoadTextureFromAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        private static Color SampleSourceWall(Texture2D sourceTexture, float u, float v)
        {
            if (sourceTexture == null)
            {
                return Color.clear;
            }

            Color color = sourceTexture.GetPixelBilinear(u, v);
            color.a = 1f;
            return color;
        }

        private static Color SampleSourceWallFace(Texture2D sourceTexture, float u, float v, bool brickFace)
        {
            if (sourceTexture == null)
            {
                return Color.clear;
            }

            Color raw = SampleSourceWall(sourceTexture, u, v);
            if (brickFace)
            {
                return raw;
            }

            Color neighborhood = AverageSourceWall(sourceTexture, u, v, 6, 4);
            float rawBrightness = raw.grayscale;
            float neighborhoodBrightness = neighborhood.grayscale;
            float darkSeam = Mathf.Clamp01((neighborhoodBrightness - rawBrightness - 0.02f) * 12f);
            float blend = Mathf.Lerp(0.55f, 0.96f, darkSeam);
            Color softened = Color.Lerp(raw, neighborhood, blend);
            softened = Color.Lerp(softened, Color.white, 0.03f);
            softened.a = 1f;
            return softened;
        }

        private static Color AverageSourceWall(Texture2D sourceTexture, float u, float v, int radiusX, int radiusY)
        {
            Color total = Color.clear;
            int count = 0;
            float stepU = 1f / Mathf.Max(1, sourceTexture.width - 1);
            float stepV = 1f / Mathf.Max(1, sourceTexture.height - 1);
            for (int oy = -radiusY; oy <= radiusY; oy++)
            {
                for (int ox = -radiusX; ox <= radiusX; ox++)
                {
                    float sampleU = Mathf.Clamp01(u + ox * stepU * 6f);
                    float sampleV = Mathf.Clamp01(v + oy * stepV * 6f);
                    total += sourceTexture.GetPixelBilinear(sampleU, sampleV);
                    count++;
                }
            }

            if (count == 0)
            {
                return Color.clear;
            }

            Color average = total / count;
            average.a = 1f;
            return average;
        }

        private static List<TileBase> ImportExternalTextureTiles(string sourceFolder, string importedTextureFolder, string tileAssetFolder, Tile.ColliderType colliderType, string tilePrefix)
        {
            EnsureAssetFolder(importedTextureFolder);
            EnsureAssetFolder(tileAssetFolder);
            List<TileBase> result = new List<TileBase>();
            if (!Directory.Exists(sourceFolder))
            {
                Debug.LogWarning("[NtingCampusMapEditor] Texture source folder is missing: " + sourceFolder);
                return result;
            }

            string[] texturePaths = Directory.GetFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => IsSupportedTextureFile(path))
                .OrderBy(path => Path.GetFileNameWithoutExtension(path))
                .ToArray();

            for (int i = 0; i < texturePaths.Length; i++)
            {
                string copiedTexturePath = CopyTextureIntoAssets(texturePaths[i], importedTextureFolder);
                if (string.IsNullOrEmpty(copiedTexturePath))
                {
                    continue;
                }

                Sprite sprite = EnsureTextureSpriteImport(copiedTexturePath);
                if (sprite == null)
                {
                    continue;
                }

                string tileName = tilePrefix + Path.GetFileNameWithoutExtension(copiedTexturePath);
                string tilePath = tileAssetFolder + "/" + tileName + ".asset";
                TileBase tile = EnsureTile(tilePath, sprite, colliderType);
                if (tile != null)
                {
                    result.Add(tile);
                }
            }

            result = result
                .Where(tile => tile != null)
                .OrderBy(tile => tile.name)
                .ToList();
            return result;
        }

        private static string CopyTextureIntoAssets(string sourcePath, string importedTextureFolder)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                return string.Empty;
            }

            string safeFileName = MakeSafeAssetFileName(Path.GetFileName(sourcePath));
            string assetPath = importedTextureFolder + "/" + safeFileName;
            string destinationPath = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            FileInfo sourceInfo = new FileInfo(sourcePath);
            FileInfo destinationInfo = new FileInfo(destinationPath);
            if (!destinationInfo.Exists ||
                destinationInfo.Length != sourceInfo.Length ||
                destinationInfo.LastWriteTimeUtc < sourceInfo.LastWriteTimeUtc)
            {
                File.Copy(sourcePath, destinationPath, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            return assetPath;
        }

        private static bool IsSupportedTextureFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".png", System.StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string MakeSafeAssetFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string safe = fileName;
            for (int i = 0; i < invalidChars.Length; i++)
            {
                safe = safe.Replace(invalidChars[i], '_');
            }

            return safe;
        }

        private static string GetProjectRootPath()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static Sprite EnsureTextureSpriteImport(string texturePath)
        {
            return EnsureTextureSpriteImport(texturePath, null, new Vector2(0.5f, 0.5f));
        }

        private static Sprite EnsureTextureSpriteImport(string texturePath, int? pixelsPerUnitOverride, Vector2 pivot)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            }

            int width = 1024;
            int height = 1024;
            importer.GetSourceTextureWidthAndHeight(out width, out height);
            float pixelsPerUnit = pixelsPerUnitOverride.HasValue
                ? Mathf.Max(1, pixelsPerUnitOverride.Value)
                : Mathf.Max(1, Mathf.Max(width, height));

            bool changed = pixelsPerUnitOverride.HasValue ||
                           importer.textureType != TextureImporterType.Sprite ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                           importer.mipmapEnabled ||
                           importer.filterMode != FilterMode.Point;

            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

        private static void AddTileIfValid(List<TileBase> list, TileBase tile)
        {
            if (list != null && tile != null && !list.Contains(tile))
            {
                list.Add(tile);
            }
        }

        private static void AddPrefabIfValid(List<GameObject> list, GameObject prefab)
        {
            if (list != null && prefab != null && !list.Contains(prefab))
            {
                list.Add(prefab);
            }
        }

        private static void AddPrefabAssetsFromFolder(List<GameObject> list, string folderPath)
        {
            if (list == null || string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                AddPrefabIfValid(list, prefab);
            }
        }

        public static CampusMapData SaveMapData(CampusMapRoot root, CampusMapData targetData)
        {
            if (root == null)
            {
                return targetData;
            }

            EnsureNotPreviewingBeforeEdit(root);
            EnsureDirectories();
            CampusMapData data = targetData;
            if (data == null)
            {
                data = AssetDatabase.LoadAssetAtPath<CampusMapData>(DefaultMapDataPath);
            }

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CampusMapData>();
                data.MapId = SceneManager.GetActiveScene().name + "_CampusMap";
                AssetDatabase.CreateAsset(data, DefaultMapDataPath);
            }

            CaptureMapData(root, data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        public static void LoadMapData(CampusMapData data, CampusMapRoot root)
        {
            if (data == null || root == null)
            {
                return;
            }

            EnsureNotPreviewingBeforeEdit(root);
            DebugAssetSet debugAssets = EnsureDebugAssets();
            CampusWallRenderProfile wallProfile = LoadDefaultWallRenderProfile();
            root.RebuildFloorReferences();

            for (int i = 0; i < data.Floors.Count; i++)
            {
                CampusFloorData floorData = data.Floors[i];
                CampusFloorRoot floor = GetOrCreateFloor(root, floorData.FloorIndex, floorData.IsUnlocked);
                if (floor == null)
                {
                    continue;
                }

                floor.IsUnlocked = floorData.IsUnlocked;
                ClearFloorAuthoredContent(floor);

                ApplyTiles(floor.FloorTilemap, floorData.FloorTiles, debugAssets.FloorTile);
                ApplyTiles(CampusWallTileUtility.GetWallLogicTilemap(floor), floorData.WallTiles, wallProfile != null ? wallProfile.GetLogicTile() : debugAssets.WallTile);

                for (int objectIndex = 0; objectIndex < floorData.Objects.Count; objectIndex++)
                {
                    CampusPlacedObjectData objectData = floorData.Objects[objectIndex];
                    GameObject prefab = FindPrefabByGuid(objectData.ObjectGuid);
                    if (prefab == null)
                    {
                        prefab = FindPrefabByName(objectData.ObjectId);
                    }

                    if (prefab == null)
                    {
                        prefab = debugAssets.PropBoxPrefab;
                    }

                    GameObject instance = InstantiatePrefabInScene(prefab, floor.PropsRoot);
                    if (instance == null)
                    {
                        continue;
                    }

                    CampusPlacedObject placed = instance.GetComponent<CampusPlacedObject>();
                    if (placed == null)
                    {
                        placed = instance.AddComponent<CampusPlacedObject>();
                    }

                    placed.ObjectId = objectData.ObjectId;
                    placed.FloorIndex = objectData.FloorIndex;
                    placed.Cell = objectData.Cell;
                    placed.FootprintSize = ResolveObjectFootprintSize(prefab, objectData.FootprintSize);
                    placed.Rotation90 = objectData.Rotation90;
                    if (placed.AllowRotation)
                    {
                        placed.ApplyRotationVisualState();
                    }
                    else
                    {
                        instance.transform.rotation = Quaternion.Euler(0f, 0f, objectData.Rotation90 * 90f);
                    }
                    if (!HasSerializedCell(placed.Cell, objectData.Position))
                    {
                        instance.transform.position = objectData.Position;
                        placed.RefreshCellFromTransform(floor.Grid);
                    }
                    else
                    {
                        placed.ApplyCellToTransform(floor.Grid);
                    }

                    placed.BlocksMovement = objectData.BlocksMovement;
                    placed.BlocksSight = objectData.BlocksSight;
                    placed.IsInteractable = objectData.IsInteractable;
                }

                for (int stairIndex = 0; stairIndex < floorData.Stairs.Count; stairIndex++)
                {
                    CampusStairData stairData = floorData.Stairs[stairIndex];
                    GameObject instance = InstantiatePrefabInScene(debugAssets.StairPrefab, floor.StairsRoot);
                    if (instance == null)
                    {
                        continue;
                    }

                    Vector3Int secondaryCell = stairData.SecondaryCell != Vector3Int.zero
                        ? stairData.SecondaryCell
                        : stairData.FromCell + CampusStairLink.DirectionFromRotation(stairData.Rotation90);
                    instance.transform.position = GetStairWorldCenter(floor.Grid, stairData.FromCell, secondaryCell);
                    instance.transform.rotation = Quaternion.Euler(0f, 0f, stairData.Rotation90 * 90f);
                    CampusStairLink link = instance.GetComponent<CampusStairLink>();
                    if (link == null)
                    {
                        link = instance.AddComponent<CampusStairLink>();
                    }

                    link.FromFloor = stairData.FromFloor;
                    link.ToFloor = stairData.ToFloor;
                    link.FromCell = stairData.FromCell;
                    link.ToCell = stairData.ToCell;
                    link.SecondaryCell = secondaryCell;
                    link.Rotation90 = stairData.Rotation90;
                    link.FootprintLength = 2;
                    link.LinkId = stairData.LinkId;
                    link.IsAutoReturnStair = stairData.IsAutoReturnStair;
                    EnsureTriggerCollider(instance, new Vector2(0.8f, 1.8f));
                }

                EnsureWallCollision(floor);
                CampusWallAutoRenderer.RebuildFloor(floor, LoadWallVisualCatalog(), wallProfile);
                floor.RefreshUsedBounds();
                EditorUtility.SetDirty(floor);
            }

            root.RebuildFloorReferences();
            MarkSceneDirty();
        }

        public static GameObject InstantiatePrefabInScene(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
                instance.name = prefab.name;
            }

            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            return instance;
        }

        public static void EnsureTriggerCollider(GameObject target)
        {
            EnsureTriggerCollider(target, null);
        }

        public static void EnsureTriggerCollider(GameObject target, Vector2? size)
        {
            if (target == null)
            {
                return;
            }

            Collider2D collider = target.GetComponent<Collider2D>();
            if (collider == null)
            {
                collider = target.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            if (size.HasValue && collider is BoxCollider2D box)
            {
                box.size = size.Value;
            }
        }

        public static Vector3 GetStairWorldCenter(Grid grid, Vector3Int primaryCell, Vector3Int secondaryCell)
        {
            if (grid == null)
            {
                return Vector3.zero;
            }

            return (grid.GetCellCenterWorld(primaryCell) + grid.GetCellCenterWorld(secondaryCell)) * 0.5f;
        }

        public static Vector3Int ResolvePlacedObjectCell(Grid grid, CampusPlacedObject placed)
        {
            if (grid == null || placed == null)
            {
                return Vector3Int.zero;
            }

            Vector3Int transformCell = placed.ResolveCellFromTransform(grid);
            if (placed.Cell != Vector3Int.zero)
            {
                Vector3 markerCenter = CampusPlacedObject.GetFootprintWorldCenter(grid, placed.Cell, placed.RotatedFootprintSize);
                Vector3 transformCenter = CampusPlacedObject.GetFootprintWorldCenter(grid, transformCell, placed.RotatedFootprintSize);
                float maxDistance = Mathf.Max(grid.cellSize.x, grid.cellSize.y) * 0.6f;
                if (Vector3.Distance(markerCenter, placed.transform.position) <= maxDistance ||
                    Vector3.Distance(markerCenter, transformCenter) <= 0.01f)
                {
                    return placed.Cell;
                }
            }

            return transformCell;
        }

        private static Vector2Int ResolveObjectFootprintSize(GameObject prefab, Vector2Int serializedFootprintSize)
        {
            if (serializedFootprintSize.x > 0 && serializedFootprintSize.y > 0)
            {
                return CampusPlacedObject.NormalizeFootprintSize(serializedFootprintSize);
            }

            CampusPlacedObject prefabObject = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            return prefabObject != null ? prefabObject.NormalizedFootprintSize : Vector2Int.one;
        }

        private static bool HasSerializedCell(Vector3Int cell, Vector3 fallbackPosition)
        {
            return cell != Vector3Int.zero || fallbackPosition == Vector3.zero;
        }

        public static void MarkSceneDirty()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        public static void RunValidation(CampusMapRoot root, CampusTilePalette tilePalette, CampusWallPalette wallPalette, CampusPrefabPalette prefabPalette)
        {
            Debug.Log("[NtingCampusMapEditor] Validation started.");
            if (root == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation failed: CampusMapRoot is missing.");
                return;
            }

            root.RebuildFloorReferences();
            if (root.GetFloor(1) == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation failed: Floor_1 is missing.");
            }

            for (int i = 0; i < root.Floors.Count; i++)
            {
                ValidateFloor(root, root.Floors[i]);
            }

            ValidatePalette(tilePalette, "Floor Tile Palette");
            ValidateWallPalette(wallPalette);
            ValidatePalette(prefabPalette, "Prefab Palette");
            ValidateStairLinks(root);
            Debug.Log("[NtingCampusMapEditor] Validation finished.");
        }

        public static void FixValidationIssues(CampusMapRoot root, CampusTilePalette tilePalette, CampusWallPalette wallPalette, CampusPrefabPalette prefabPalette)
        {
            DebugAssetSet debugSet = EnsureDebugAssets();
            if (root == null)
            {
                root = FindOrCreateCampusMapRoot();
            }

            EnsureNotPreviewingBeforeEdit(root);
            RebuildFloorReferences(root);
            root.RebuildFloorReferences();

            if (root.GetFloor(1) == null)
            {
                GetOrCreateFloor(root, 1, true);
            }

            root.RebuildFloorReferences();
            CampusWallRenderProfile wallProfile = LoadDefaultWallRenderProfile();
            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusFloorRoot floor = root.Floors[i];
                EnsureFloorStructure(root, floor, true);
                EnsureWallCollision(floor);
                CampusWallAutoRenderer.RebuildFloor(floor, LoadWallVisualCatalog(), wallProfile);
                CampusWallAutoRenderer.ApplyDebugView(floor, CampusWallDebugView.ShowFinalWallVisuals);
                RefreshPlacedObjectsOnFloor(floor);
            }

            CleanPalettes(tilePalette, wallPalette, prefabPalette);
            EnsureDefaultPalettes(debugSet);
            root.CaptureFloorOriginalStates(true);
            EditorUtility.SetDirty(root);
            MarkSceneDirty();
            Debug.Log("[NtingCampusMapEditor] Fix Validation Issues completed. Orphan stairs are reported by validation but not deleted automatically.");
        }

        public static void CleanPalettes(CampusTilePalette tilePalette, CampusWallPalette wallPalette, CampusPrefabPalette prefabPalette)
        {
            if (tilePalette != null)
            {
                tilePalette.RemoveInvalidEntries();
                EditorUtility.SetDirty(tilePalette);
            }

            if (wallPalette != null)
            {
                wallPalette.RemoveInvalidEntries();
                EditorUtility.SetDirty(wallPalette);
            }

            if (prefabPalette != null)
            {
                prefabPalette.RemoveInvalidEntries();
                EditorUtility.SetDirty(prefabPalette);
            }
        }

        public static void RefreshPlacedObjectsOnFloor(CampusFloorRoot floor)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
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

                Undo.RecordObject(placed, "Refresh Campus Placed Object Cell");
                placed.FloorIndex = floor.FloorIndex;
                placed.RefreshCellFromTransform(floor.Grid);
                EditorUtility.SetDirty(placed);
            }
        }

        private static void ValidateFloor(CampusMapRoot root, CampusFloorRoot floor)
        {
            if (floor == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: A floor reference is null.");
                return;
            }

            if (floor.Grid == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing Grid.");
            }

            if (floor.FloorTilemap == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing Tilemap_Floor.");
            }

            if (CampusWallTileUtility.GetWallLogicTilemap(floor) == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing Tilemap_WallLogic.");
            }

            if (floor.WallCapTilemap == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing Tilemap_WallCap.");
            }

            if (floor.WallFaceTilemap == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing Tilemap_WallFace.");
            }

            if (floor.OverlayTilemap == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing Tilemap_Overlay.");
            }

            if (floor.PropsRoot == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing PropsRoot.");
            }

            if (floor.StairsRoot == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " is missing StairsRoot.");
            }

            ValidateWallCollider(floor);
            ValidateLockedFloorContent(floor);
            ValidatePlacedObjects(floor);
        }

        private static void ValidateWallCollider(CampusFloorRoot floor)
        {
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (floor == null || wallLogic == null)
            {
                return;
            }

            GameObject wallObject = wallLogic.gameObject;
            TilemapCollider2D tilemapCollider = wallObject.GetComponent<TilemapCollider2D>();
            Rigidbody2D body = wallObject.GetComponent<Rigidbody2D>();
            CompositeCollider2D composite = wallObject.GetComponent<CompositeCollider2D>();
            if (tilemapCollider == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " Tilemap_WallLogic is missing TilemapCollider2D.");
            }
            else if (tilemapCollider.compositeOperation != Collider2D.CompositeOperation.Merge)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " Tilemap_WallLogic compositeOperation is not Merge.");
            }

            if (body == null || body.bodyType != RigidbodyType2D.Static || !body.simulated)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " Tilemap_WallLogic Rigidbody2D must be Static and simulated.");
            }

            if (composite == null)
            {
                Debug.LogError("[NtingCampusMapEditor] Validation: " + floor.name + " Tilemap_WallLogic is missing CompositeCollider2D.");
            }
        }

        private static void ValidatePalette(CampusTilePalette palette, string name)
        {
            if (palette == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: " + name + " is not assigned.");
                return;
            }

            for (int i = 0; i < palette.FloorTiles.Count; i++)
            {
                if (!CampusTilePalette.IsUsableTile(palette.FloorTiles[i]))
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Validation: " + name + " has a null or sprite-less tile at index " + i + ".");
                }
            }
        }

        private static void ValidatePalette(CampusPrefabPalette palette, string name)
        {
            if (palette == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: " + name + " is not assigned.");
                return;
            }

            for (int i = 0; i < palette.Prefabs.Count; i++)
            {
                if (palette.Prefabs[i] == null)
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Validation: " + name + " has a null prefab at index " + i + ".");
                }
            }
        }

        private static void ValidateWallPalette(CampusWallPalette palette)
        {
            if (palette == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: Wall Palette is not assigned.");
                return;
            }

            if (!CampusTilePalette.IsUsableTile(palette.HorizontalWall))
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: Wall Palette HorizontalWall is missing or invalid.");
            }

            if (!CampusTilePalette.IsUsableTile(palette.VerticalWall))
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: Wall Palette VerticalWall is missing or invalid.");
            }

            if (!CampusTilePalette.IsUsableTile(palette.CornerWall))
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: Wall Palette CornerWall is missing or invalid.");
            }

            if (!CampusTilePalette.IsUsableTile(palette.HighWall))
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: Wall Palette HighWall is missing or invalid.");
            }

            for (int i = 0; i < palette.WallTiles.Count; i++)
            {
                if (!CampusTilePalette.IsUsableTile(palette.WallTiles[i]))
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Validation: Wall Palette has a null or sprite-less tile at index " + i + ".");
                }
            }
        }

        private static void ValidateStairLinks(CampusMapRoot root)
        {
            Dictionary<string, int> linkCounts = new Dictionary<string, int>();
            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusFloorRoot floor = root.Floors[i];
                if (floor == null || floor.StairsRoot == null)
                {
                    continue;
                }

                CampusStairLink[] stairs = floor.StairsRoot.GetComponentsInChildren<CampusStairLink>(true);
                for (int stairIndex = 0; stairIndex < stairs.Length; stairIndex++)
                {
                    CampusStairLink stair = stairs[stairIndex];
                    if (stair == null || string.IsNullOrEmpty(stair.LinkId))
                    {
                        continue;
                    }

                    linkCounts.TryGetValue(stair.LinkId, out int count);
                    linkCounts[stair.LinkId] = count + 1;
                }
            }

            foreach (KeyValuePair<string, int> pair in linkCounts)
            {
                if (pair.Value == 1)
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Validation: Orphan stair LinkId " + pair.Key + " has only one stair.");
                }
            }
        }

        private static void ValidateLockedFloorContent(CampusFloorRoot floor)
        {
            if (floor == null || floor.IsUnlocked)
            {
                return;
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            bool hasContent = (floor.FloorTilemap != null && floor.FloorTilemap.GetUsedTilesCount() > 0) ||
                              (wallLogic != null && wallLogic.GetUsedTilesCount() > 0) ||
                              (floor.PropsRoot != null && floor.PropsRoot.childCount > 0) ||
                              (floor.StairsRoot != null && floor.StairsRoot.childCount > 0);
            if (hasContent)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Validation: Locked floor " + floor.name + " contains authored content.");
            }
        }

        private static void ValidatePlacedObjects(CampusFloorRoot floor)
        {
            if (floor == null || floor.Grid == null || floor.PropsRoot == null)
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

                if (placed.FloorIndex != floor.FloorIndex)
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Validation: " + placed.name + " FloorIndex does not match parent " + floor.name + ".");
                }

                Vector3 expected = CampusPlacedObject.GetFootprintWorldCenter(floor.Grid, placed.Cell, placed.RotatedFootprintSize);
                if (Vector3.Distance(expected, placed.transform.position) > 0.05f)
                {
                    Debug.LogWarning("[NtingCampusMapEditor] Validation: " + placed.name + " Cell does not match transform position.");
                }
            }
        }

        private static void CaptureMapData(CampusMapRoot root, CampusMapData data)
        {
            data.Floors.Clear();
            root.RebuildFloorReferences();

            for (int i = 0; i < root.Floors.Count; i++)
            {
                CampusFloorRoot floor = root.Floors[i];
                if (floor == null)
                {
                    continue;
                }

                CampusFloorData floorData = new CampusFloorData
                {
                    FloorIndex = floor.FloorIndex,
                    IsUnlocked = floor.IsUnlocked
                };

                CaptureTiles(floor.FloorTilemap, floorData.FloorTiles);
                CaptureTiles(CampusWallTileUtility.GetWallLogicTilemap(floor), floorData.WallTiles);
                CaptureObjects(floor, floorData.Objects);
                CaptureStairs(floor.StairsRoot, floorData.Stairs);
                data.Floors.Add(floorData);
            }
        }

        private static void CaptureTiles(Tilemap tilemap, List<CampusTileCellData> output)
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

                Matrix4x4 transform = tilemap.GetTransformMatrix(cell);
                output.Add(new CampusTileCellData
                {
                    Cell = cell,
                    TileId = tile.name,
                    TileGuid = GetAssetGuid(tile),
                    Size = GetAuthoredTileSize(transform),
                    Rotation90 = GetAuthoredTileRotation90(transform),
                    FlipX = IsTileFlippedX(transform),
                    FlipY = IsTileFlippedY(transform),
                    Transform = transform
                });
            }
        }

        private static void CaptureObjects(CampusFloorRoot floor, List<CampusPlacedObjectData> output)
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

                Vector3Int cell = ResolvePlacedObjectCell(floor.Grid, placed);
                placed.Cell = cell;
                placed.FloorIndex = floor.FloorIndex;
                GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(placed.gameObject);
                Vector3 cellCenter = CampusPlacedObject.GetFootprintWorldCenter(floor.Grid, cell, placed.RotatedFootprintSize);
                output.Add(new CampusPlacedObjectData
                {
                    ObjectId = placed.ObjectId,
                    ObjectGuid = GetAssetGuid(prefabSource),
                    Position = cellCenter,
                    Cell = cell,
                    FootprintSize = placed.NormalizedFootprintSize,
                    FloorIndex = floor.FloorIndex,
                    Rotation90 = placed.Rotation90,
                    BlocksMovement = placed.BlocksMovement,
                    BlocksSight = placed.BlocksSight,
                    IsInteractable = placed.IsInteractable
                });
            }
        }

        private static void CaptureStairs(Transform stairsRoot, List<CampusStairData> output)
        {
            output.Clear();
            if (stairsRoot == null)
            {
                return;
            }

            CampusStairLink[] stairs = stairsRoot.GetComponentsInChildren<CampusStairLink>(true);
            for (int i = 0; i < stairs.Length; i++)
            {
                CampusStairLink stair = stairs[i];
                if (stair == null)
                {
                    continue;
                }

                output.Add(new CampusStairData
                {
                    FromFloor = stair.FromFloor,
                    ToFloor = stair.ToFloor,
                    FromCell = stair.FromCell,
                    ToCell = stair.ToCell,
                    SecondaryCell = stair.GetSecondaryCell(),
                    Rotation90 = stair.Rotation90,
                    LinkId = stair.LinkId,
                    IsAutoReturnStair = stair.IsAutoReturnStair
                });
            }
        }

        private static void ClearFloorAuthoredContent(CampusFloorRoot floor)
        {
            if (floor.FloorTilemap != null)
            {
                Undo.RecordObject(floor.FloorTilemap, "Load Campus Map Data");
                floor.FloorTilemap.ClearAllTiles();
            }

            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic != null)
            {
                Undo.RecordObject(wallLogic, "Load Campus Map Data");
                wallLogic.ClearAllTiles();
            }

            CampusWallAutoRenderer.ClearVisualLayers(floor);

            DestroyChildrenWithUndo(floor.PropsRoot);
            DestroyChildrenWithUndo(floor.StairsRoot);
        }

        private static void DestroyChildrenWithUndo(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
            }
        }

        private static Matrix4x4 ResolveTileDataTransform(CampusTileCellData tileData)
        {
            if (HasSerializedTransform(tileData.Transform))
            {
                return tileData.Transform;
            }

            return BuildTileDataTransform(tileData.Size, tileData.Rotation90, tileData.FlipX, tileData.FlipY);
        }

        private static bool HasSerializedTransform(Matrix4x4 transform)
        {
            return !Mathf.Approximately(transform.m00, 0f) ||
                   !Mathf.Approximately(transform.m01, 0f) ||
                   !Mathf.Approximately(transform.m10, 0f) ||
                   !Mathf.Approximately(transform.m11, 0f) ||
                   !Mathf.Approximately(transform.m33, 0f);
        }

        private static Matrix4x4 BuildTileDataTransform(int tileSize, int rotation90, bool flipX, bool flipY)
        {
            int size = Mathf.Clamp(tileSize <= 0 ? 1 : tileSize, 1, 3);
            Vector3 offset = new Vector3((size - 1) * 0.5f, (size - 1) * 0.5f, 0f);
            Vector3 scale = new Vector3((flipX ? -1f : 1f) * size, (flipY ? -1f : 1f) * size, 1f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(rotation90, 0, 3) * 90f);
            return Matrix4x4.TRS(offset, rotation, scale);
        }

        private static int GetAuthoredTileSize(Matrix4x4 transform)
        {
            Vector2 xColumn = new Vector2(transform.m00, transform.m10);
            Vector2 yColumn = new Vector2(transform.m01, transform.m11);
            int size = Mathf.RoundToInt(Mathf.Max(xColumn.magnitude, yColumn.magnitude));
            return Mathf.Clamp(size <= 0 ? 1 : size, 1, 3);
        }

        private static int GetAuthoredTileRotation90(Matrix4x4 transform)
        {
            float angle = Mathf.Atan2(transform.m10, transform.m00) * Mathf.Rad2Deg;
            int rotation = Mathf.RoundToInt(angle / 90f) % 4;
            return rotation < 0 ? rotation + 4 : rotation;
        }

        private static bool IsTileFlippedX(Matrix4x4 transform)
        {
            return transform.determinant < 0f;
        }

        private static bool IsTileFlippedY(Matrix4x4 transform)
        {
            return false;
        }

        private static void ApplyTiles(Tilemap tilemap, List<CampusTileCellData> tiles, TileBase fallbackTile)
        {
            if (tilemap == null || tiles == null)
            {
                return;
            }

            Undo.RecordObject(tilemap, "Load Campus Map Data");
            for (int i = 0; i < tiles.Count; i++)
            {
                CampusTileCellData tileData = tiles[i];
                TileBase tile = FindTileByGuid(tileData.TileGuid);
                if (tile == null)
                {
                    tile = FindTileByName(tileData.TileId);
                }

                TileBase resolvedTile = tile != null ? tile : fallbackTile;
                tilemap.SetTile(tileData.Cell, resolvedTile);
                if (resolvedTile != null)
                {
                    tilemap.SetTileFlags(tileData.Cell, TileFlags.None);
                    tilemap.SetTransformMatrix(tileData.Cell, ResolveTileDataTransform(tileData));
                }
            }

            tilemap.RefreshAllTiles();
            EditorUtility.SetDirty(tilemap);
        }

        private static string GetAssetGuid(Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static TileBase FindTileByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TileBase>(path);
        }

        private static GameObject FindPrefabByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static TileBase FindTileByName(string tileName)
        {
            if (string.IsNullOrEmpty(tileName))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets(tileName + " t:TileBase");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null && tile.name == tileName)
                {
                    return tile;
                }
            }

            guids = AssetDatabase.FindAssets(tileName + " t:Tile");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null && tile.name == tileName)
                {
                    return tile;
                }
            }

            return null;
        }

        private static GameObject FindPrefabByName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return null;
            }

            string localizedPrefabName = LocalizeLegacyObjectName(prefabName);
            HashSet<string> prefabGuids = new HashSet<string>(AssetDatabase.FindAssets(localizedPrefabName + " t:Prefab"));
            string[] legacyGuids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
            for (int i = 0; i < legacyGuids.Length; i++)
            {
                prefabGuids.Add(legacyGuids[i]);
            }

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && (prefab.name == localizedPrefabName || prefab.name == prefabName))
                {
                    return prefab;
                }
            }

            string[] searchFolders =
            {
                PropPrefabsPath,
                StairPrefabsPath,
                MapPrefabsPath,
                WallsPrefabsPath,
                RootPath + "/Prefabs/Player"
            };

            string[] fallbackGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
            for (int i = 0; i < fallbackGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(fallbackGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                CampusPlacedObject placedObject = prefab.GetComponent<CampusPlacedObject>();
                string assetName = Path.GetFileNameWithoutExtension(path);
                if (prefab.name == prefabName ||
                    prefab.name == localizedPrefabName ||
                    assetName == prefabName ||
                    assetName == localizedPrefabName ||
                    (placedObject != null && (placedObject.ObjectId == prefabName || placedObject.ObjectId == localizedPrefabName)))
                {
                    return prefab;
                }
            }

            return null;
        }

        private static string LocalizeLegacyObjectName(string objectName)
        {
            return CampusObjectNames.LocalizeLegacyHierarchyName(objectName);
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        private static void EnsureWallVisualTilemap(ref Tilemap tilemap, Transform parent, string name, int sortingOrder, params string[] legacyNames)
        {
            if (tilemap == null)
            {
                tilemap = FindTilemapByName(parent, name, legacyNames);
                if (tilemap == null)
                {
                    tilemap = CreateTilemap(parent, name, sortingOrder);
                }
            }

            EnsureTilemapRendererOrder(tilemap, sortingOrder);
            RemoveWallVisualCollider(tilemap);
        }

        private static void EnsureTilemapRendererOrder(Tilemap tilemap, int sortingOrder)
        {
            if (tilemap == null)
            {
                return;
            }

            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
            }

            renderer.sortingOrder = sortingOrder;
        }

        private static void RemoveWallVisualCollider(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return;
            }

            Collider2D[] colliders = tilemap.GetComponents<Collider2D>();
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(colliders[i]);
            }

            Rigidbody2D body = tilemap.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                Object.DestroyImmediate(body);
            }
        }

        private static Tilemap FindTilemapByName(Transform root, string name, params string[] legacyNames)
        {
            if (root == null)
            {
                return null;
            }

            Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i] == null)
                {
                    continue;
                }

                if (CampusObjectNames.MatchesAny(tilemaps[i].name, name) || CampusObjectNames.MatchesAny(tilemaps[i].name, legacyNames))
                {
                    tilemaps[i].name = name;
                    return tilemaps[i];
                }
            }

            return null;
        }

        private static Transform GetOrCreateChild(Transform parent, string name, params string[] legacyNames)
        {
            Transform child = CampusObjectNames.FindDirectChild(parent, name);
            if (child == null && legacyNames != null)
            {
                for (int i = 0; i < legacyNames.Length; i++)
                {
                    child = CampusObjectNames.FindDirectChild(parent, legacyNames[i]);
                    if (child != null)
                    {
                        break;
                    }
                }
            }

            if (child != null)
            {
                child.name = name;
                return child;
            }

            GameObject childObject = new GameObject(name);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static Transform FindChildRecursive(Transform root, string name, params string[] legacyNames)
        {
            if (root == null)
            {
                return null;
            }

            Transform direct = CampusObjectNames.FindDirectChild(root, name);
            if (direct == null && legacyNames != null)
            {
                for (int i = 0; i < legacyNames.Length; i++)
                {
                    direct = CampusObjectNames.FindDirectChild(root, legacyNames[i]);
                    if (direct != null)
                    {
                        break;
                    }
                }
            }

            if (direct != null)
            {
                direct.name = name;
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name, legacyNames);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static Sprite EnsureSprite(string assetPath, Color baseColor, DebugSpritePattern pattern, bool overwriteExisting = false, int width = 32, int height = 32, Vector2? pivot = null)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (existing != null && !overwriteExisting)
            {
                EnsureSpriteImporter(assetPath, width, pivot ?? new Vector2(0.5f, 0.5f));
                return existing;
            }

            string fullPath = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, GetDebugPixelColor(x, y, width, height, baseColor, pattern));
                }
            }

            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            EnsureSpriteImporter(assetPath, width, pivot ?? new Vector2(0.5f, 0.5f));
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void EnsureSpriteImporter(string assetPath, int pixelsPerUnit, Vector2 pivot)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Mathf.Max(1, pixelsPerUnit);
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static TileBase EnsureTile(string assetPath, Sprite sprite, Tile.ColliderType colliderType)
        {
            TileBase existing = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);
            if (existing != null)
            {
                Tile existingTile = existing as Tile;
                if (existingTile != null)
                {
                    existingTile.sprite = sprite;
                    existingTile.colliderType = colliderType;
                    EditorUtility.SetDirty(existingTile);
                }

                return existing;
            }

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = Path.GetFileNameWithoutExtension(assetPath);
            tile.sprite = sprite;
            tile.colliderType = colliderType;
            AssetDatabase.CreateAsset(tile, assetPath);
            return tile;
        }

        private static GameObject EnsurePropPrefab(string assetPath, Sprite sprite)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject prop = new GameObject(CampusObjectNames.TestPropBox);
            SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 300;
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            CampusPlacedObject placed = prop.AddComponent<CampusPlacedObject>();
            placed.ObjectId = CampusObjectNames.TestPropBox;
            placed.BlocksMovement = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prop, assetPath);
            Object.DestroyImmediate(prop);
            return prefab;
        }

        private static GameObject EnsureStairPrefab(string assetPath, Sprite sprite)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existing != null)
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                ConfigureStairPrefab(prefabRoot, sprite);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }

            GameObject stair = new GameObject(CampusObjectNames.TestStair);
            ConfigureStairPrefab(stair, sprite);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(stair, assetPath);
            Object.DestroyImmediate(stair);
            return prefab;
        }

        private static void ConfigureStairPrefab(GameObject stair, Sprite sprite)
        {
            if (stair == null)
            {
                return;
            }

            stair.name = CampusObjectNames.TestStair;
            SpriteRenderer renderer = stair.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = stair.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.sortingOrder = 250;

            BoxCollider2D collider = stair.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = stair.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 1.8f);
            collider.offset = Vector2.zero;

            CampusStairLink link = stair.GetComponent<CampusStairLink>();
            if (link == null)
            {
                link = stair.AddComponent<CampusStairLink>();
            }

            link.FootprintLength = 2;
        }

        private static Color GetDebugPixelColor(int x, int y, int width, int height, Color baseColor, DebugSpritePattern pattern)
        {
            bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
            bool diagonal = Mathf.Abs((float)x / Mathf.Max(1, width - 1) - (float)y / Mathf.Max(1, height - 1)) < 0.04f ||
                            Mathf.Abs((float)x / Mathf.Max(1, width - 1) - (1f - (float)y / Mathf.Max(1, height - 1))) < 0.04f;
            bool gridLine = x % 8 == 0 || y % 8 == 0;
            float normalizedY = (float)y / Mathf.Max(1, height - 1);
            float normalizedX = (float)x / Mathf.Max(1, width - 1);

            switch (pattern)
            {
                case DebugSpritePattern.Floor:
                    return border ? baseColor * 0.65f : (gridLine ? baseColor * 0.9f : baseColor);
                case DebugSpritePattern.Wall:
                    return normalizedY < 0.28f ? baseColor * 0.55f : (border ? baseColor * 0.75f : baseColor);
                case DebugSpritePattern.HorizontalWall:
                    return normalizedY > 0.58f ? baseColor * 1.15f : (normalizedY < 0.25f ? baseColor * 0.5f : baseColor);
                case DebugSpritePattern.VerticalWall:
                    return normalizedX < 0.25f ? baseColor * 0.5f : (normalizedX > 0.7f ? baseColor * 1.1f : baseColor);
                case DebugSpritePattern.CornerWall:
                    return normalizedX < 0.34f || normalizedY < 0.34f ? baseColor * 0.55f : (border ? baseColor * 0.75f : baseColor);
                case DebugSpritePattern.HighWall:
                    if (normalizedY < 0.16f)
                    {
                        return baseColor * 0.45f;
                    }

                    if (normalizedY > 0.74f)
                    {
                        return baseColor * 1.18f;
                    }

                    return border || x % 10 == 0 ? baseColor * 0.72f : baseColor;
                case DebugSpritePattern.Stair:
                    return border ? baseColor * 0.55f : (y % 8 < 3 ? baseColor * 1.15f : baseColor);
                case DebugSpritePattern.Box:
                    return border || diagonal ? baseColor * 0.55f : baseColor;
                default:
                    return baseColor;
            }
        }

        private enum DebugSpritePattern
        {
            Floor,
            Wall,
            HorizontalWall,
            VerticalWall,
            CornerWall,
            HighWall,
            Stair,
            Box
        }

        private struct PrototypeWallTiles
        {
            public TileBase LogicTile;
            public TileBase DefaultCapTile;
            public TileBase[] CapTiles;
            public TileBase SouthFaceTile;
            public TileBase WestSideTile;
            public TileBase EastSideTile;
            public TileBase BothSideTile;
            public TileBase EndNorth;
            public TileBase EndEast;
            public TileBase EndSouth;
            public TileBase EndWest;
            public TileBase OuterCornerNE;
            public TileBase OuterCornerNW;
            public TileBase OuterCornerSE;
            public TileBase OuterCornerSW;
            public TileBase InnerCornerNE;
            public TileBase InnerCornerNW;
            public TileBase InnerCornerSE;
            public TileBase InnerCornerSW;
            public TileBase TJunctionNorth;
            public TileBase TJunctionEast;
            public TileBase TJunctionSouth;
            public TileBase TJunctionWest;
            public TileBase Cross;
        }

        private enum WallPrototypeSpriteKind
        {
            Logic,
            Cap,
            FaceSouth,
            SideWest,
            SideEast,
            SideBoth,
            EndNorth,
            EndEast,
            EndSouth,
            EndWest,
            OuterCornerNE,
            OuterCornerNW,
            OuterCornerSE,
            OuterCornerSW,
            InnerCornerNE,
            InnerCornerNW,
            InnerCornerSE,
            InnerCornerSW,
            TJunctionNorth,
            TJunctionEast,
            TJunctionSouth,
            TJunctionWest,
            Cross
        }
    }
}


