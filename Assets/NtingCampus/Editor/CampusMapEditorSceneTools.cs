using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace NtingCampusMapEditor
{
    /// <summary>
    /// SceneView brush handling for the campus map editor window.
    /// </summary>
    public static class CampusMapEditorSceneTools
    {
        private static bool rectangleDragActive;
        private static Vector3Int rectangleStartCell;
        private static Vector3Int rectangleCurrentCell;
        private static bool panDragActive;
        private static Vector2 panStartMouse;
        private static Vector3 panStartPivot;
        private static readonly Dictionary<int, PendingWallVisualRebuild> PendingWallVisualRebuilds = new Dictionary<int, PendingWallVisualRebuild>();
        private static readonly int SceneToolControlHint = "NtingCampusMapEditor.SceneTool".GetHashCode();
        private static bool wallVisualRebuildScheduled;

        private struct PendingWallVisualRebuild
        {
            public CampusFloorRoot Floor;
            public CampusWallRenderProfile Profile;
            public CampusWallDebugView DebugView;
        }

        public static void HandleSceneGUI(CampusMapEditorWindow window, SceneView sceneView)
        {
            if (window == null || !window.EnableEditing)
            {
                return;
            }

            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            EventType eventType = current.type;
            int sceneToolControlId = GUIUtility.GetControlID(SceneToolControlHint, FocusType.Passive);
            if (window.ActiveBrushMode != CampusBrushMode.Pan && !current.alt)
            {
                Tools.current = Tool.None;
            }

            if (HandleTemporaryPanShortcut(window, current))
            {
                return;
            }

            if (window.MapRoot == null)
            {
                DrawSceneMessage(window.Text("No CampusMapRoot. Left-click in Scene view to create one and start editing.", "没有 CampusMapRoot。请在 Scene 视图左键点击以创建并开始编辑。"));
                if (!IsPrimaryMouseDown(current) || current.alt || window.ActiveBrushMode == CampusBrushMode.Pan || !window.EnsureMapRootForEditing())
                {
                    return;
                }
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                rectangleDragActive = false;
                panDragActive = false;
                if (GUIUtility.hotControl == sceneToolControlId)
                {
                    GUIUtility.hotControl = 0;
                }

                window.ClearBrushSelection();
                current.Use();
                return;
            }

            CampusFloorRoot floor = window.GetCurrentFloor();
            if ((floor == null || floor.Grid == null) && IsPrimaryMouseDown(current) && !current.alt && window.ActiveBrushMode != CampusBrushMode.Pan)
            {
                floor = window.EnsureCurrentFloorForEditing();
            }
            if (floor == null || floor.Grid == null)
            {
                DrawSceneMessage(window.Text("No editable floor is available.", "没有可编辑楼层。"));
                return;
            }

            if (!floor.IsUnlocked)
            {
                DrawSceneMessage(window.Text("Current floor is locked. Unlock it before editing.", "当前楼层已锁定，请先解锁。"));
                return;
            }

            bool mouseEvent = current.type == EventType.MouseDown || current.type == EventType.MouseDrag || current.type == EventType.MouseUp;
            bool mutatingEvent = mouseEvent && window.ActiveBrushMode != CampusBrushMode.Pick && window.ActiveBrushMode != CampusBrushMode.Pan;
            if (mutatingEvent)
            {
                CampusMapEditorUtility.EnsureNotPreviewingBeforeEdit(window.MapRoot);
            }

            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(sceneToolControlId);
            }

            if (!TryGetMouseCell(floor, current.mousePosition, out Vector3Int cell, out Vector3 worldPosition))
            {
                return;
            }

            if (HandlePanTool(window, sceneView, current, floor, cell))
            {
                return;
            }

            bool primaryMouseEvent = eventType == EventType.MouseDown ||
                                     eventType == EventType.MouseDrag ||
                                     eventType == EventType.MouseUp;
            if (primaryMouseEvent && (current.button == 0 || current.button == 1) && !current.alt)
            {
                if (eventType == EventType.MouseDown)
                {
                    GUIUtility.hotControl = sceneToolControlId;
                }
                else if (GUIUtility.hotControl != 0 && GUIUtility.hotControl != sceneToolControlId)
                {
                    return;
                }
            }

            if (HandleRectangleTool(window, current, floor, cell))
            {
                if (eventType == EventType.MouseUp && GUIUtility.hotControl == sceneToolControlId)
                {
                    GUIUtility.hotControl = 0;
                }

                return;
            }

            DrawCellPreview(window, floor, cell, worldPosition);

            if (current.alt)
            {
                return;
            }

            if (!mouseEvent || (current.button != 0 && current.button != 1))
            {
                return;
            }

            bool eraseOverride = current.button == 1 || current.shift;
            bool dragAllowed = window.ActiveBrushMode == CampusBrushMode.PaintFloorTile ||
                               window.ActiveBrushMode == CampusBrushMode.PaintWallTile ||
                               window.ActiveBrushMode == CampusBrushMode.Erase ||
                               eraseOverride;

            if (current.type == EventType.MouseDrag && !dragAllowed)
            {
                return;
            }

            if (current.type == EventType.MouseUp && !dragAllowed)
            {
                current.Use();
                return;
            }

            ApplyBrush(window, floor, cell, worldPosition, eraseOverride);
            if (eventType == EventType.MouseUp && GUIUtility.hotControl == sceneToolControlId)
            {
                GUIUtility.hotControl = 0;
            }

            current.Use();
        }

        private static bool HandleTemporaryPanShortcut(CampusMapEditorWindow window, Event current)
        {
            if (window == null || current == null)
            {
                return false;
            }

            if (current.type == EventType.MouseLeaveWindow && window.IsTemporaryPanActive)
            {
                window.SetTemporaryPanOverride(false);
                panDragActive = false;
                rectangleDragActive = false;
                return false;
            }

            if (current.keyCode != KeyCode.Space)
            {
                return false;
            }

            if (current.type == EventType.KeyDown)
            {
                window.SetTemporaryPanOverride(true);
                panDragActive = false;
                rectangleDragActive = false;
                current.Use();
                return true;
            }

            if (current.type == EventType.KeyUp)
            {
                window.SetTemporaryPanOverride(false);
                panDragActive = false;
                current.Use();
                return true;
            }

            return false;
        }

        private static bool IsPrimaryMouseDown(Event current)
        {
            return current != null && current.type == EventType.MouseDown && current.button == 0;
        }

        private static bool HandlePanTool(CampusMapEditorWindow window, SceneView sceneView, Event current, CampusFloorRoot floor, Vector3Int cell)
        {
            if (window.ActiveBrushMode != CampusBrushMode.Pan)
            {
                return false;
            }

            DrawPanCursor(sceneView);
            DrawPanPreview(window, floor, cell);
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                panDragActive = true;
                panStartMouse = current.mousePosition;
                panStartPivot = sceneView.pivot;
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseDrag && panDragActive)
            {
                Vector2 delta = current.mousePosition - panStartMouse;
                float unitsPerPixel = sceneView.size * 2f / Mathf.Max(1f, sceneView.position.height);
                Vector3 right = sceneView.rotation * Vector3.right;
                Vector3 up = sceneView.rotation * Vector3.up;
                sceneView.pivot = panStartPivot - right * (delta.x * unitsPerPixel) + up * (delta.y * unitsPerPixel);
                sceneView.Repaint();
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseUp && panDragActive)
            {
                panDragActive = false;
                current.Use();
                return true;
            }

            return true;
        }

        private static bool HandleRectangleTool(CampusMapEditorWindow window, Event current, CampusFloorRoot floor, Vector3Int cell)
        {
            if (!IsRectangleMode(window.ActiveBrushMode))
            {
                rectangleDragActive = false;
                return false;
            }

            if (current.alt)
            {
                return true;
            }

            if (current.type == EventType.MouseDown && current.button == 1)
            {
                rectangleDragActive = false;
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                rectangleDragActive = true;
                rectangleStartCell = cell;
                rectangleCurrentCell = cell;
                DrawRectanglePreview(window, floor, rectangleStartCell, rectangleCurrentCell);
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseDrag && rectangleDragActive)
            {
                rectangleCurrentCell = cell;
                DrawRectanglePreview(window, floor, rectangleStartCell, rectangleCurrentCell);
                SceneView.RepaintAll();
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseUp && rectangleDragActive)
            {
                rectangleCurrentCell = cell;
                ApplyRectangleBrush(window, floor, rectangleStartCell, rectangleCurrentCell);
                rectangleDragActive = false;
                current.Use();
                return true;
            }

            DrawRectanglePreview(window, floor, rectangleDragActive ? rectangleStartCell : cell, rectangleDragActive ? rectangleCurrentCell : cell);
            return true;
        }

        private static bool IsRectangleMode(CampusBrushMode mode)
        {
            return mode == CampusBrushMode.RectangleFillFloor ||
                   mode == CampusBrushMode.RectangleErase ||
                   mode == CampusBrushMode.RectangleWall;
        }

        private static void ApplyRectangleBrush(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int startCell, Vector3Int endCell)
        {
            switch (window.ActiveBrushMode)
            {
                case CampusBrushMode.RectangleFillFloor:
                    FillFloorRectangle(window, floor, startCell, endCell);
                    break;
                case CampusBrushMode.RectangleErase:
                    EraseRectangle(window, floor, startCell, endCell);
                    break;
                case CampusBrushMode.RectangleWall:
                    PaintWallRectangle(window, floor, startCell, endCell);
                    break;
            }

            CampusMapEditorUtility.MarkSceneDirty();
            window.RefreshAfterSceneEdit();
        }

        private static void FillFloorRectangle(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int startCell, Vector3Int endCell)
        {
            TileBase tile = window.GetCurrentFloorTileOrFallback();
            if (floor.FloorTilemap == null || tile == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] No floor tile selected and no debug fallback is available.");
                return;
            }

            Undo.RecordObject(floor.FloorTilemap, "Fill Campus Floor Rectangle");
            int tileSize = window.FloorTileSizeCells;
            Matrix4x4 transform = BuildTileTransform(window, tileSize);
            foreach (Vector3Int cell in RectangleTileAnchors(startCell, endCell, tileSize))
            {
                ClearFloorTileFootprint(floor.FloorTilemap, cell, tileSize);
                floor.FloorTilemap.SetTile(cell, tile);
                floor.FloorTilemap.SetTileFlags(cell, TileFlags.None);
                floor.FloorTilemap.SetTransformMatrix(cell, transform);
            }

            floor.FloorTilemap.RefreshAllTiles();
            EditorUtility.SetDirty(floor.FloorTilemap);
            floor.RefreshUsedBounds();
        }

        private static void PaintWallRectangle(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int startCell, Vector3Int endCell)
        {
            Tilemap wallLogic = GetWallLogicTilemapForPainting(window, floor);
            TileBase tile = window.EnsureWallTileForPainting();
            if (wallLogic == null || tile == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] No wall tile selected and no debug fallback is available.");
                return;
            }

            Undo.RecordObject(wallLogic, "Paint Campus Wall Rectangle");
            foreach (Vector3Int cell in RectanglePerimeterCells(startCell, endCell))
            {
                wallLogic.SetTile(cell, tile);
                wallLogic.SetTileFlags(cell, TileFlags.None);
                wallLogic.SetTransformMatrix(cell, Matrix4x4.identity);
            }

            RefreshTilemap(wallLogic);
            CampusMapEditorUtility.EnsureWallCollision(floor);
            CampusMapEditorUtility.ProcessWallColliderChanges(floor);
            RequestWallVisualRebuild(floor, window.WallRenderProfileOrFallback, window.WallDebugView);
            floor.RefreshUsedBounds();
        }

        private static void EraseRectangle(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int startCell, Vector3Int endCell)
        {
            Tilemap wallLogic = GetWallLogicTilemapForPainting(window, floor);
            List<Object> undoTargets = new List<Object>();
            AddUndoTarget(undoTargets, floor.FloorTilemap);
            AddUndoTarget(undoTargets, wallLogic);
            AddUndoTarget(undoTargets, floor.OverlayTilemap);
            AddUndoTarget(undoTargets, floor.CollisionDebugTilemap);
            if (undoTargets.Count > 0)
            {
                Undo.RecordObjects(undoTargets.ToArray(), "Erase Campus Rectangle");
            }

            foreach (Vector3Int cell in RectangleCells(startCell, endCell))
            {
                ClearFloorTileAtOrCoveringCell(floor.FloorTilemap, cell);
                ClearWallTileWithoutUndo(wallLogic, cell);
                ClearTileWithoutUndo(floor.OverlayTilemap, cell);
                ClearTileWithoutUndo(floor.CollisionDebugTilemap, cell);
                ErasePlacedObjectsAtCell(floor, cell);
                EraseStairsAtCell(window.MapRoot, floor, cell);
            }

            RefreshTilemap(floor.FloorTilemap);
            RefreshTilemap(wallLogic);
            RefreshTilemap(floor.OverlayTilemap);
            RefreshTilemap(floor.CollisionDebugTilemap);
            CampusMapEditorUtility.ProcessWallColliderChanges(floor);
            RequestWallVisualRebuild(floor, window.WallRenderProfileOrFallback, window.WallDebugView);
            floor.RefreshUsedBounds();
        }

        private static void AddUndoTarget(List<Object> targets, Object target)
        {
            if (target != null && !targets.Contains(target))
            {
                targets.Add(target);
            }
        }

        private static void ClearTileWithoutUndo(Tilemap tilemap, Vector3Int cell)
        {
            if (tilemap == null)
            {
                return;
            }

            tilemap.SetTile(cell, null);
        }

        private static void ClearFloorTileFootprint(Tilemap tilemap, Vector3Int anchorCell, int tileSize)
        {
            if (tilemap == null)
            {
                return;
            }

            foreach (Vector3Int cell in FloorTileFootprintCells(anchorCell, tileSize))
            {
                ClearFloorTileAtOrCoveringCell(tilemap, cell);
            }
        }

        private static void ClearFloorTileAtOrCoveringCell(Tilemap tilemap, Vector3Int cell)
        {
            if (tilemap == null)
            {
                return;
            }

            if (TryFindFloorTileAnchorCoveringCell(tilemap, cell, out Vector3Int anchorCell, out _))
            {
                tilemap.SetTile(anchorCell, null);
                return;
            }

            tilemap.SetTile(cell, null);
        }

        private static bool TryFindFloorTileAnchorCoveringCell(Tilemap tilemap, Vector3Int cell, out Vector3Int anchorCell, out int tileSize)
        {
            anchorCell = cell;
            tileSize = 1;
            if (tilemap == null)
            {
                return false;
            }

            for (int size = 3; size >= 1; size--)
            {
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        Vector3Int candidate = new Vector3Int(cell.x - x, cell.y - y, cell.z);
                        if (!tilemap.HasTile(candidate))
                        {
                            continue;
                        }

                        int candidateSize = GetAuthoredFloorTileSize(tilemap, candidate);
                        if (FloorTileContainsCell(candidate, candidateSize, cell))
                        {
                            anchorCell = candidate;
                            tileSize = candidateSize;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool FloorTileContainsCell(Vector3Int anchorCell, int tileSize, Vector3Int cell)
        {
            int size = Mathf.Clamp(tileSize, 1, 3);
            return cell.x >= anchorCell.x &&
                   cell.x < anchorCell.x + size &&
                   cell.y >= anchorCell.y &&
                   cell.y < anchorCell.y + size &&
                   cell.z == anchorCell.z;
        }

        private static int GetAuthoredFloorTileSize(Tilemap tilemap, Vector3Int anchorCell)
        {
            if (tilemap == null)
            {
                return 1;
            }

            Matrix4x4 matrix = tilemap.GetTransformMatrix(anchorCell);
            Vector2 xColumn = new Vector2(matrix.m00, matrix.m10);
            Vector2 yColumn = new Vector2(matrix.m01, matrix.m11);
            int size = Mathf.RoundToInt(Mathf.Max(xColumn.magnitude, yColumn.magnitude));
            return Mathf.Clamp(size, 1, 3);
        }

        private static void ClearWallTileWithoutUndo(Tilemap wallLogic, Vector3Int baseCell)
        {
            if (wallLogic == null)
            {
                return;
            }

            wallLogic.SetTile(baseCell, null);
        }

        private static void RefreshTilemap(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return;
            }

            tilemap.RefreshAllTiles();
            EditorUtility.SetDirty(tilemap);
        }

        private static IEnumerable<Vector3Int> RectangleCells(Vector3Int startCell, Vector3Int endCell)
        {
            GetRectangleBounds(startCell, endCell, out int minX, out int maxX, out int minY, out int maxY);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    yield return new Vector3Int(x, y, startCell.z);
                }
            }
        }

        private static IEnumerable<Vector3Int> RectangleTileAnchors(Vector3Int startCell, Vector3Int endCell, int tileSize)
        {
            GetRectangleBounds(startCell, endCell, out int minX, out int maxX, out int minY, out int maxY);
            int size = Mathf.Clamp(tileSize, 1, 3);
            for (int x = minX; x <= maxX; x += size)
            {
                for (int y = minY; y <= maxY; y += size)
                {
                    yield return new Vector3Int(x, y, startCell.z);
                }
            }
        }

        private static IEnumerable<Vector3Int> FloorTileFootprintCells(Vector3Int anchorCell, int tileSize)
        {
            int size = Mathf.Clamp(tileSize, 1, 3);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    yield return new Vector3Int(anchorCell.x + x, anchorCell.y + y, anchorCell.z);
                }
            }
        }

        private static IEnumerable<Vector3Int> RectanglePerimeterCells(Vector3Int startCell, Vector3Int endCell)
        {
            GetRectangleBounds(startCell, endCell, out int minX, out int maxX, out int minY, out int maxY);
            for (int x = minX; x <= maxX; x++)
            {
                yield return new Vector3Int(x, minY, startCell.z);
                if (maxY != minY)
                {
                    yield return new Vector3Int(x, maxY, startCell.z);
                }
            }

            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                yield return new Vector3Int(minX, y, startCell.z);
                if (maxX != minX)
                {
                    yield return new Vector3Int(maxX, y, startCell.z);
                }
            }
        }

        private static void GetRectangleBounds(Vector3Int startCell, Vector3Int endCell, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = Mathf.Min(startCell.x, endCell.x);
            maxX = Mathf.Max(startCell.x, endCell.x);
            minY = Mathf.Min(startCell.y, endCell.y);
            maxY = Mathf.Max(startCell.y, endCell.y);
        }

        private static void ApplyBrush(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int anchorCell, Vector3 worldPosition, bool eraseOverride)
        {
            switch (window.ActiveBrushMode)
            {
                case CampusBrushMode.PaintFloorTile:
                    if (!eraseOverride && window.GetCurrentFloorTileOrFallback() == null)
                    {
                        Debug.LogWarning("[NtingCampusMapEditor] No floor tile selected and no debug fallback is available.");
                        return;
                    }

                    PaintFloorTile(floor.FloorTilemap, anchorCell, window.FloorTileSizeCells, eraseOverride ? null : window.GetCurrentFloorTileOrFallback(), window, "Paint Campus Floor Tile");
                    floor.RefreshUsedBounds();
                    break;
                case CampusBrushMode.PaintWallTile:
                    Tilemap wallLogic = GetWallLogicTilemapForPainting(window, floor);
                    if (wallLogic == null)
                    {
                        Debug.LogWarning("[NtingCampusMapEditor] Current floor is missing Tilemap_WallLogic.");
                        return;
                    }

                    TileBase wallTile = eraseOverride ? null : window.EnsureWallTileForPainting();
                    if (!eraseOverride && wallTile == null)
                    {
                        Debug.LogWarning("[NtingCampusMapEditor] No wall tile selected and no debug fallback is available.");
                        return;
                    }

                    PaintWallTiles(wallLogic, anchorCell, window.BrushSize, wallTile, "Paint Campus Wall Logic Tile");
                    CampusMapEditorUtility.EnsureWallCollision(floor);
                    CampusMapEditorUtility.ProcessWallColliderChanges(floor);
                    RequestWallVisualRebuild(floor, window.WallRenderProfileOrFallback, window.WallDebugView);
                    floor.RefreshUsedBounds();
                    break;
                case CampusBrushMode.PlacePrefab:
                    if (eraseOverride)
                    {
                        ErasePlacedObjectsAtCell(floor, anchorCell);
                    }
                    else
                    {
                        PlacePrefab(window, floor, anchorCell);
                    }

                    floor.RefreshUsedBounds();
                    break;
                case CampusBrushMode.PlaceStair:
                    if (eraseOverride)
                    {
                        EraseStairsAtCell(window.MapRoot, floor, anchorCell);
                    }
                    else
                    {
                        PlaceStair(window, floor, anchorCell);
                    }

                    floor.RefreshUsedBounds();
                    break;
                case CampusBrushMode.PlaceLight:
                    if (eraseOverride)
                    {
                        EraseLightsAtCell(floor, anchorCell);
                    }
                    else
                    {
                        PlaceLight(window, floor, anchorCell, worldPosition);
                    }

                    floor.RefreshUsedBounds();
                    break;
                case CampusBrushMode.Erase:
                    EraseAllAtCell(window, floor, anchorCell, window.BrushSize);
                    floor.RefreshUsedBounds();
                    break;
                case CampusBrushMode.Pick:
                    PickAtCell(window, floor, anchorCell);
                    break;
            }

            CampusMapEditorUtility.MarkSceneDirty();
            window.RefreshAfterSceneEdit();
        }

        private static void PaintFloorTile(Tilemap tilemap, Vector3Int cell, int tileSize, TileBase tile, CampusMapEditorWindow window, string undoName)
        {
            if (tilemap == null)
            {
                return;
            }

            Undo.RecordObject(tilemap, undoName);
            int size = Mathf.Clamp(tileSize, 1, 3);
            if (tile == null)
            {
                ClearFloorTileAtOrCoveringCell(tilemap, cell);
                tilemap.RefreshAllTiles();
                EditorUtility.SetDirty(tilemap);
                return;
            }

            Vector3Int anchorCell = NormalizeFloorTileAnchor(cell, size);
            ClearFloorTileFootprint(tilemap, anchorCell, size);
            tilemap.SetTile(anchorCell, tile);
            tilemap.SetTileFlags(anchorCell, TileFlags.None);
            tilemap.SetTransformMatrix(anchorCell, BuildTileTransform(window, size));
            tilemap.RefreshAllTiles();
            EditorUtility.SetDirty(tilemap);
        }

        private static void PaintWallTiles(Tilemap tilemap, Vector3Int baseAnchorCell, int brushSize, TileBase tile, string undoName)
        {
            if (tilemap == null)
            {
                return;
            }

            Undo.RecordObject(tilemap, undoName);
            foreach (Vector3Int cell in BrushCells(baseAnchorCell, brushSize))
            {
                tilemap.SetTile(cell, tile);
                if (tile != null)
                {
                    tilemap.SetTileFlags(cell, TileFlags.None);
                    tilemap.SetTransformMatrix(cell, Matrix4x4.identity);
                }
            }

            tilemap.RefreshAllTiles();
            EditorUtility.SetDirty(tilemap);
        }

        private static void PlacePrefab(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int cell)
        {
            GameObject prefab = window.GetCurrentPrefabOrFallback();
            if (prefab == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Cannot place object: no prefab is selected and no fallback prefab is available.");
                return;
            }

            if (floor.PropsRoot == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Cannot place object: current floor is missing PropsRoot. Run Fix Validation Issues.");
                return;
            }

            if (floor.Grid == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Cannot place object: current floor is missing Grid. Run Fix Validation Issues.");
                return;
            }

            Vector2Int authoredFootprintSize = GetPrefabFootprintSize(prefab);
            int effectiveRotation90 = GetPrefabPlacementRotation90(prefab, window.Rotation90);
            Vector2Int rotatedFootprintSize = CampusPlacedObject.RotateFootprintSize(authoredFootprintSize, effectiveRotation90);
            ErasePlacedObjectsInFootprint(floor, cell, rotatedFootprintSize);

            GameObject instance = CampusMapEditorUtility.InstantiatePrefabInScene(prefab, floor.PropsRoot);
            if (instance == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Cannot place object: failed to instantiate prefab '" + CampusObjectNames.GetDisplayName(prefab.name) + "'.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Campus Prefab");
            string localizedPrefabName = CampusObjectNames.GetDisplayName(prefab.name);
            instance.name = localizedPrefabName + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y;
            instance.transform.rotation = Quaternion.identity;
            ApplyFlip(instance.transform, window.FlipX, window.FlipY);

            CampusPlacedObject placed = instance.GetComponent<CampusPlacedObject>();
            if (placed == null)
            {
                placed = instance.AddComponent<CampusPlacedObject>();
            }

            placed.FloorIndex = floor.FloorIndex;
            placed.ObjectId = localizedPrefabName;
            placed.Cell = cell;
            placed.FootprintSize = authoredFootprintSize;
            placed.ApplyPlacementRotation(effectiveRotation90);
            placed.ApplyCellToTransform(floor.Grid);
            CampusDynamicShadowUtility.EnsureObjectShadowCasters(placed, floor.Grid);

            CampusRenderSortingUtility.ApplyFloorSorting(floor, floor.FloorIndex * window.MapRoot.SortingOrderStepPerFloor);
            EditorUtility.SetDirty(instance);
            Selection.activeGameObject = instance;
            Debug.Log("[NtingCampusMapEditor] Placed object '" + localizedPrefabName + "' at floor " + floor.FloorIndex + ", cell " + cell + ".");
        }

        private static Vector2Int GetPrefabFootprintSize(GameObject prefab)
        {
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            return placed != null ? placed.NormalizedFootprintSize : Vector2Int.one;
        }

        private static Vector2Int GetRotatedPrefabFootprintSize(GameObject prefab, int rotation90)
        {
            return CampusPlacedObject.RotateFootprintSize(GetPrefabFootprintSize(prefab), GetPrefabPlacementRotation90(prefab, rotation90));
        }

        private static int GetPrefabPlacementRotation90(GameObject prefab, int requestedRotation90)
        {
            CampusPlacedObject placed = prefab != null ? prefab.GetComponent<CampusPlacedObject>() : null;
            return placed != null ? placed.ResolveAllowedRotation90(requestedRotation90) : 0;
        }

        private static void PlaceStair(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int cell)
        {
            CampusMapRoot root = window.MapRoot;
            if (root == null || floor.Grid == null)
            {
                return;
            }

            GameObject stairPrefab = window.GetStairPrefabOrFallback();
            if (stairPrefab == null)
            {
                return;
            }

            int targetFloorIndex = floor.FloorIndex + 1;
            CampusFloorRoot targetFloor = CampusMapEditorUtility.GetOrCreateFloor(root, targetFloorIndex, true);
            if (targetFloor == null)
            {
                return;
            }

            Undo.RecordObject(targetFloor, "Unlock Campus Floor");
            targetFloor.IsUnlocked = true;

            Vector3Int secondaryCell = GetStairSecondaryCell(cell, window.Rotation90);
            EraseStairsAtCell(root, floor, cell);
            EraseStairsAtCell(root, floor, secondaryCell);
            string linkId = System.Guid.NewGuid().ToString("N");
            CreateStairInstance(stairPrefab, floor, floor.FloorIndex, targetFloorIndex, cell, secondaryCell, secondaryCell, window.Rotation90, linkId, false, window);

            int returnRotation = (window.Rotation90 + 2) % 4;
            if (!HasMatchingStair(targetFloor, linkId, targetFloorIndex, floor.FloorIndex, secondaryCell, cell))
            {
                CreateStairInstance(stairPrefab, targetFloor, targetFloorIndex, floor.FloorIndex, secondaryCell, cell, cell, returnRotation, linkId, true, window);
            }

            root.RebuildFloorReferences();
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(targetFloor);
        }

        private static void PlaceLight(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int cell, Vector3 worldPosition)
        {
            if (window == null || floor == null || floor.Grid == null)
            {
                return;
            }

            Vector3 position = window.SnapToGrid ? floor.Grid.GetCellCenterWorld(cell) : worldPosition;
            position.z = floor.Grid.transform.position.z;
            string lightName = window.LightBrushName + "_F" + floor.FloorIndex + "_" + cell.x + "_" + cell.y;
            EraseLightsAtCell(floor, cell);
            Light2D light = CampusMapEditorUtility.CreatePlacedSceneLight2D(lightName, window.LightBrushType, position, window.Rotation90, floor.Grid.cellSize);
            if (light == null)
            {
                Debug.LogWarning("[NtingCampusMapEditor] Cannot place light: failed to create Light2D.");
                return;
            }

            window.SetSelectedLight(light);
            Debug.Log("[NtingCampusMapEditor] Placed light '" + light.gameObject.name + "' at floor " + floor.FloorIndex + ", cell " + cell + ".");
        }

        private static void CreateStairInstance(GameObject prefab, CampusFloorRoot floor, int fromFloor, int toFloor, Vector3Int fromCell, Vector3Int secondaryCell, Vector3Int toCell, int rotation90, string linkId, bool isAutoReturnStair, CampusMapEditorWindow window)
        {
            if (prefab == null || floor == null || floor.StairsRoot == null || floor.Grid == null)
            {
                return;
            }

            GameObject instance = CampusMapEditorUtility.InstantiatePrefabInScene(prefab, floor.StairsRoot);
            if (instance == null)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Campus Stair");
            instance.name = CampusObjectNames.GetDisplayName(prefab.name) + "_F" + fromFloor + "_To_F" + toFloor + "_" + fromCell.x + "_" + fromCell.y;
            instance.transform.position = CampusMapEditorUtility.GetStairWorldCenter(floor.Grid, fromCell, secondaryCell);
            instance.transform.rotation = Quaternion.Euler(0f, 0f, rotation90 * 90f);
            ApplyFlip(instance.transform, window.FlipX, window.FlipY);

            CampusStairLink link = instance.GetComponent<CampusStairLink>();
            if (link == null)
            {
                link = instance.AddComponent<CampusStairLink>();
            }

            link.FromFloor = fromFloor;
            link.ToFloor = toFloor;
            link.FromCell = fromCell;
            link.ToCell = toCell;
            link.SecondaryCell = secondaryCell;
            link.Rotation90 = rotation90;
            link.FootprintLength = 2;
            link.LinkId = linkId;
            link.IsAutoReturnStair = isAutoReturnStair;
            link.AutoUnlockTargetFloor = true;
            CampusMapEditorUtility.EnsureTriggerCollider(instance, new Vector2(0.8f, 1.8f));
            CampusDynamicShadowUtility.EnsureRendererShadowCasters(instance);
            CampusRenderSortingUtility.ApplyFloorSorting(floor, fromFloor * window.MapRoot.SortingOrderStepPerFloor);
            EditorUtility.SetDirty(instance);
        }

        private static void EraseAllAtCell(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int anchorCell, int brushSize)
        {
            Tilemap wallLogic = GetWallLogicTilemapForPainting(window, floor);
            foreach (Vector3Int cell in BrushCells(anchorCell, brushSize))
            {
                EraseFloorTile(floor.FloorTilemap, cell, "Erase Campus Floor Tile");
                EraseWallTile(wallLogic, cell, "Erase Campus Wall Logic Tile");
                EraseTile(floor.OverlayTilemap, cell, "Erase Campus Overlay Tile");
                EraseTile(floor.CollisionDebugTilemap, cell, "Erase Campus Collision Debug Tile");
                ErasePlacedObjectsAtCell(floor, cell);
                EraseStairsAtCell(window.MapRoot, floor, cell);
                EraseLightsAtCell(floor, cell);
            }

            CampusMapEditorUtility.ProcessWallColliderChanges(floor);
            RequestWallVisualRebuild(floor, window.WallRenderProfileOrFallback, window.WallDebugView);
        }

        private static Tilemap GetWallLogicTilemapForPainting(CampusMapEditorWindow window, CampusFloorRoot floor)
        {
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic != null)
            {
                return wallLogic;
            }

            CampusMapEditorUtility.EnsureFloorStructure(window != null ? window.MapRoot : null, floor, false);
            return CampusWallTileUtility.GetWallLogicTilemap(floor);
        }

        private static void RequestWallVisualRebuild(CampusFloorRoot floor, CampusWallRenderProfile profile, CampusWallDebugView debugView)
        {
            if (floor == null)
            {
                return;
            }

            PendingWallVisualRebuilds[floor.GetInstanceID()] = new PendingWallVisualRebuild
            {
                Floor = floor,
                Profile = profile,
                DebugView = debugView
            };

            if (wallVisualRebuildScheduled)
            {
                return;
            }

            wallVisualRebuildScheduled = true;
            EditorApplication.delayCall += FlushPendingWallVisualRebuilds;
        }

        private static void FlushPendingWallVisualRebuilds()
        {
            wallVisualRebuildScheduled = false;
            if (PendingWallVisualRebuilds.Count == 0)
            {
                return;
            }

            PendingWallVisualRebuild[] rebuilds = new PendingWallVisualRebuild[PendingWallVisualRebuilds.Count];
            PendingWallVisualRebuilds.Values.CopyTo(rebuilds, 0);
            PendingWallVisualRebuilds.Clear();

            for (int i = 0; i < rebuilds.Length; i++)
            {
                PendingWallVisualRebuild rebuild = rebuilds[i];
                if (rebuild.Floor == null)
                {
                    continue;
                }

                CampusMapEditorUtility.RebuildWallVisuals(rebuild.Floor, rebuild.Profile);
                CampusWallAutoRenderer.ApplyDebugView(rebuild.Floor, rebuild.DebugView);
            }

            SceneView.RepaintAll();
        }

        private static void EraseFloorTile(Tilemap tilemap, Vector3Int cell, string undoName)
        {
            if (tilemap == null)
            {
                return;
            }

            if (!TryFindFloorTileAnchorCoveringCell(tilemap, cell, out Vector3Int anchorCell, out _))
            {
                return;
            }

            Undo.RecordObject(tilemap, undoName);
            tilemap.SetTile(anchorCell, null);
            tilemap.RefreshTile(anchorCell);
            EditorUtility.SetDirty(tilemap);
        }

        private static void EraseTile(Tilemap tilemap, Vector3Int cell, string undoName)
        {
            if (tilemap == null || !tilemap.HasTile(cell))
            {
                return;
            }

            Undo.RecordObject(tilemap, undoName);
            tilemap.SetTile(cell, null);
            tilemap.RefreshTile(cell);
            EditorUtility.SetDirty(tilemap);
        }

        private static void EraseWallTile(Tilemap wallLogic, Vector3Int baseCell, string undoName)
        {
            if (wallLogic == null)
            {
                return;
            }

            EraseTile(wallLogic, baseCell, undoName);
        }

        private static void ErasePlacedObjectsAtCell(CampusFloorRoot floor, Vector3Int cell)
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
                    Undo.DestroyObjectImmediate(placed.gameObject);
                }
            }
        }

        private static void ErasePlacedObjectsInFootprint(CampusFloorRoot floor, Vector3Int anchorCell, Vector2Int footprintSize)
        {
            foreach (Vector3Int cell in PrefabFootprintCells(anchorCell, footprintSize))
            {
                ErasePlacedObjectsAtCell(floor, cell);
            }
        }

        private static IEnumerable<Vector3Int> PrefabFootprintCells(Vector3Int anchorCell, Vector2Int footprintSize)
        {
            Vector2Int size = CampusPlacedObject.NormalizeFootprintSize(footprintSize);
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    yield return new Vector3Int(anchorCell.x + x, anchorCell.y + y, anchorCell.z);
                }
            }
        }

        private static void EraseStairsAtCell(CampusMapRoot root, CampusFloorRoot floor, Vector3Int cell)
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
                    DestroyLinkedStairs(root, stair);
                }
            }

            if (root != null)
            {
                root.RebuildFloorReferences();
                EditorUtility.SetDirty(root);
            }
        }

        private static void EraseLightsAtCell(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            Light2D[] lights = CampusMapEditorUtility.FindSceneLights2D();
            for (int i = lights.Length - 1; i >= 0; i--)
            {
                Light2D light = lights[i];
                if (light == null)
                {
                    continue;
                }

                if (floor.Grid.WorldToCell(light.transform.position) == cell)
                {
                    Undo.DestroyObjectImmediate(light.gameObject);
                }
            }
        }

        private static bool HasMatchingStair(CampusFloorRoot floor, string linkId, int fromFloor, int toFloor, Vector3Int fromCell, Vector3Int toCell)
        {
            if (floor == null || floor.StairsRoot == null)
            {
                return false;
            }

            CampusStairLink[] stairs = floor.StairsRoot.GetComponentsInChildren<CampusStairLink>(true);
            for (int i = 0; i < stairs.Length; i++)
            {
                CampusStairLink stair = stairs[i];
                if (stair == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(linkId) && stair.LinkId == linkId)
                {
                    return true;
                }

                if (stair.FromFloor == fromFloor && stair.ToFloor == toFloor && stair.ContainsCell(fromCell) && stair.ContainsCell(toCell))
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyLinkedStairs(CampusMapRoot root, CampusStairLink selected)
        {
            if (selected == null)
            {
                return;
            }

            string linkId = selected.LinkId;
            if (root != null && !string.IsNullOrEmpty(linkId))
            {
                root.RebuildFloorReferences();
                for (int floorIndex = 0; floorIndex < root.Floors.Count; floorIndex++)
                {
                    CampusFloorRoot floor = root.Floors[floorIndex];
                    if (floor == null || floor.StairsRoot == null)
                    {
                        continue;
                    }

                    CampusStairLink[] stairs = floor.StairsRoot.GetComponentsInChildren<CampusStairLink>(true);
                    for (int i = stairs.Length - 1; i >= 0; i--)
                    {
                        if (stairs[i] != null && stairs[i].LinkId == linkId)
                        {
                            Undo.DestroyObjectImmediate(stairs[i].gameObject);
                        }
                    }
                }

                return;
            }

            Undo.DestroyObjectImmediate(selected.gameObject);
        }

        private static void PickAtCell(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int cell)
        {
            Tilemap wallLogic = CampusWallTileUtility.GetWallLogicTilemap(floor);
            if (wallLogic != null)
            {
                TileBase wall = wallLogic.GetTile(cell);

                if (wall != null)
                {
                    window.SetCurrentWallTile(wall);
                    return;
                }
            }

            if (floor.FloorTilemap != null)
            {
                TileBase floorTile = null;
                int floorTileSize = 1;
                if (TryFindFloorTileAnchorCoveringCell(floor.FloorTilemap, cell, out Vector3Int floorAnchor, out floorTileSize))
                {
                    floorTile = floor.FloorTilemap.GetTile(floorAnchor);
                }

                if (floorTile != null)
                {
                    window.SetCurrentFloorTile(floorTile);
                    window.SetFloorTileSizeCells(floorTileSize);
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

                    GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(placed.gameObject);
                    window.SetCurrentPrefab(prefab != null ? prefab : placed.gameObject);
                    return;
                }
            }
        }

        private static bool TryGetMouseCell(CampusFloorRoot floor, Vector2 mousePosition, out Vector3Int cell, out Vector3 worldPosition)
        {
            cell = Vector3Int.zero;
            worldPosition = Vector3.zero;

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.forward, floor.Grid.transform.position);
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            cell = floor.Grid.WorldToCell(worldPosition);
            return true;
        }

        private static void DrawSceneMessage(string message)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 420f, 48f), EditorStyles.helpBox);
            GUILayout.Label(message, EditorStyles.boldLabel);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void DrawCellPreview(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int cell, Vector3 worldPosition)
        {
            Vector3 cellSize = floor.Grid.cellSize;
            Vector3 center = window.SnapToGrid ? floor.Grid.GetCellCenterWorld(cell) : worldPosition;
            CampusBrushMode activeMode = window.ActiveBrushMode;
            int brushSize = activeMode == CampusBrushMode.PaintFloorTile ? window.FloorTileSizeCells : window.BrushSize;
            Vector3 previewCenter;
            Vector3 previewSize;

            if (activeMode == CampusBrushMode.PlaceStair)
            {
                Vector3Int secondaryCell = GetStairSecondaryCell(cell, window.Rotation90);
                previewCenter = CampusMapEditorUtility.GetStairWorldCenter(floor.Grid, cell, secondaryCell);
                Vector3Int direction = secondaryCell - cell;
                previewSize = new Vector3(
                    cellSize.x * (Mathf.Abs(direction.x) + 1),
                    cellSize.y * (Mathf.Abs(direction.y) + 1),
                    0f);
            }
            else if (activeMode == CampusBrushMode.PaintFloorTile)
            {
                Vector3Int anchorCell = NormalizeFloorTileAnchor(cell, brushSize);
                Vector3 anchorCenter = floor.Grid.GetCellCenterWorld(anchorCell);
                previewCenter = anchorCenter + new Vector3((brushSize - 1) * cellSize.x * 0.5f, (brushSize - 1) * cellSize.y * 0.5f, 0f);
                previewSize = new Vector3(cellSize.x * brushSize, cellSize.y * brushSize, 0f);
            }
            else if (activeMode == CampusBrushMode.PlacePrefab)
            {
                Vector2Int footprint = GetRotatedPrefabFootprintSize(window.GetCurrentPrefabOrFallback(), window.Rotation90);
                previewCenter = CampusPlacedObject.GetFootprintWorldCenter(floor.Grid, cell, footprint);
                previewSize = new Vector3(cellSize.x * footprint.x, cellSize.y * footprint.y, 0f);
            }
            else if (activeMode == CampusBrushMode.PlaceLight)
            {
                previewCenter = center;
                previewSize = new Vector3(cellSize.x, cellSize.y, 0f);
            }
            else
            {
                previewCenter = center + new Vector3((brushSize - 1) * cellSize.x * 0.5f, (brushSize - 1) * cellSize.y * 0.5f, 0f);
                previewSize = new Vector3(cellSize.x * brushSize, cellSize.y * brushSize, 0f);
            }

            Handles.color = PreviewColor(activeMode);
            Handles.DrawWireCube(previewCenter, previewSize);
            if (activeMode == CampusBrushMode.PlacePrefab)
            {
                DrawPrefabPlacementTexturePreview(window, floor, cell, previewCenter);
            }

            if (activeMode == CampusBrushMode.PlaceLight && window.LightBrushType != Light2D.LightType.Global)
            {
                float radius = Mathf.Max(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)) * 4f;
                Handles.DrawWireDisc(previewCenter, Vector3.forward, radius);
            }

            string label = BuildPreviewLabel(window, cell);
            Handles.Label(previewCenter + Vector3.up * (previewSize.y * 0.6f), label, EditorStyles.helpBox);
        }

        private static void DrawPrefabPlacementTexturePreview(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int cell, Vector3 previewCenter)
        {
            GameObject prefab = window != null ? window.GetCurrentPrefabOrFallback() : null;
            if (prefab == null || floor == null || floor.Grid == null)
            {
                return;
            }

            CampusPlacedObject placed = prefab.GetComponent<CampusPlacedObject>();
            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                return;
            }

            Sprite sprite = ResolvePrefabPreviewSprite(prefab, placed, window.Rotation90, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90);
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            Rect rect = BuildSceneGuiPreviewRect(previewCenter, sprite, renderer.transform.localScale);
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            float previewRotation = placed != null && placed.AllowRotation && !usesAuthoredDirectionalSprite ? -effectiveRotation90 * 90f : 0f;
            Handles.BeginGUI();
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
            Handles.EndGUI();
        }

        private static Sprite ResolvePrefabPreviewSprite(GameObject prefab, CampusPlacedObject placed, int requestedRotation90, out bool usesAuthoredDirectionalSprite, out int effectiveRotation90)
        {
            usesAuthoredDirectionalSprite = false;
            effectiveRotation90 = 0;
            if (prefab == null)
            {
                return null;
            }

            if (placed != null)
            {
                return placed.ResolveSpriteForRotation(requestedRotation90, out usesAuthoredDirectionalSprite, out effectiveRotation90);
            }

            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private static Rect BuildSceneGuiPreviewRect(Vector3 worldCenter, Sprite sprite, Vector3 visualScale)
        {
            if (sprite == null)
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

            Vector2 center = HandleUtility.WorldToGUIPoint(worldCenter);
            float guiWidth = Mathf.Abs(HandleUtility.WorldToGUIPoint(worldCenter + Vector3.right * worldWidth).x - center.x);
            float guiHeight = Mathf.Abs(HandleUtility.WorldToGUIPoint(worldCenter + Vector3.up * worldHeight).y - center.y);
            return new Rect(center.x - guiWidth * 0.5f, center.y - guiHeight * 0.5f, guiWidth, guiHeight);
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
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

        private static void DrawPanPreview(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int cell)
        {
            Vector3 cellSize = floor.Grid.cellSize;
            Vector3 center = floor.Grid.GetCellCenterWorld(cell);
            Handles.color = PreviewColor(CampusBrushMode.Pan);
            Handles.DrawWireCube(center, new Vector3(cellSize.x, cellSize.y, 0f));
            Handles.Label(center + Vector3.up * (cellSize.y * 0.6f), window.Text("Drag to pan Scene view", "拖拽平移 Scene 视图"), EditorStyles.helpBox);
        }

        private static void DrawPanCursor(SceneView sceneView)
        {
            if (sceneView == null)
            {
                return;
            }

            Handles.BeginGUI();
            EditorGUIUtility.AddCursorRect(new Rect(0f, 0f, sceneView.position.width, sceneView.position.height), MouseCursor.Pan);
            Handles.EndGUI();
        }

        private static void DrawRectanglePreview(CampusMapEditorWindow window, CampusFloorRoot floor, Vector3Int startCell, Vector3Int endCell)
        {
            if (floor == null || floor.Grid == null)
            {
                return;
            }

            GetRectangleBounds(startCell, endCell, out int minX, out int maxX, out int minY, out int maxY);
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int floorTileSize = window.FloorTileSizeCells;
            CampusBrushMode activeMode = window.ActiveBrushMode;
            int count = activeMode == CampusBrushMode.RectangleWall
                ? RectanglePerimeterCount(width, height)
                : (activeMode == CampusBrushMode.RectangleFillFloor
                    ? RectangleTileAnchorCount(width, height, floorTileSize)
                    : width * height);
            int z = startCell.z;

            Vector3 bottomLeft = floor.Grid.CellToWorld(new Vector3Int(minX, minY, z));
            Vector3 bottomRight = floor.Grid.CellToWorld(new Vector3Int(maxX + 1, minY, z));
            Vector3 topRight = floor.Grid.CellToWorld(new Vector3Int(maxX + 1, maxY + 1, z));
            Vector3 topLeft = floor.Grid.CellToWorld(new Vector3Int(minX, maxY + 1, z));
            Color outline = PreviewColor(activeMode);
            Color fill = outline;
            fill.a = activeMode == CampusBrushMode.RectangleErase ? 0.12f : 0.10f;

            Handles.DrawSolidRectangleWithOutline(
                new[] { bottomLeft, bottomRight, topRight, topLeft },
                fill,
                outline);

            Vector3 center = (bottomLeft + topRight) * 0.5f;
            Vector3 cellSize = floor.Grid.cellSize;
            string action = window.ActiveBrushMode == CampusBrushMode.RectangleErase
                ? window.Text("Rectangle Erase", "矩形删除")
                : (window.ActiveBrushMode == CampusBrushMode.RectangleWall
                    ? window.Text("Rectangle Wall", "矩形墙体")
                    : window.Text("Rectangle Floor", "矩形铺地"));
            string unit = window.ActiveBrushMode == CampusBrushMode.RectangleFillFloor ? window.Text("tiles", "块") : window.Text("cells", "格");
            string sizeLabel = window.ActiveBrushMode == CampusBrushMode.RectangleFillFloor ? "\n" + window.Text("Size: ", "尺寸: ") + window.FloorTileSizeLabel() : string.Empty;
            string label = action + "\n" + width + " x " + height + " / " + count + " " + unit + sizeLabel;
            Handles.Label(center + Vector3.up * (cellSize.y * 0.6f), label, EditorStyles.helpBox);
        }

        private static int RectangleTileAnchorCount(int width, int height, int tileSize)
        {
            int size = Mathf.Clamp(tileSize, 1, 3);
            return Mathf.CeilToInt(width / (float)size) * Mathf.CeilToInt(height / (float)size);
        }

        private static int RectanglePerimeterCount(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return 0;
            }

            if (width == 1)
            {
                return height;
            }

            if (height == 1)
            {
                return width;
            }

            return width * 2 + height * 2 - 4;
        }

        private static string BuildPreviewLabel(CampusMapEditorWindow window, Vector3Int cell)
        {
            switch (window.ActiveBrushMode)
            {
                case CampusBrushMode.PaintFloorTile:
                    TileBase floorTile = window.GetCurrentFloorTileOrFallback();
                    Vector3Int floorAnchor = NormalizeFloorTileAnchor(cell, window.FloorTileSizeCells);
                    return window.Text("Cell ", "格子 ") + floorAnchor + "\n" +
                           window.Text("Floor: ", "地面: ") + (floorTile != null ? CampusObjectNames.GetDisplayName(floorTile.name) : window.Text("None", "无")) + "\n" +
                           window.Text("Size: ", "尺寸: ") + window.FloorTileSizeLabel();
                case CampusBrushMode.PaintWallTile:
                    TileBase wallTile = window.GetCurrentWallTileOrFallback();
                    return window.Text("Wall ", "墙体 ") + cell + "\n" + window.Text("Tile: ", "瓦片: ") + (wallTile != null ? CampusObjectNames.GetDisplayName(wallTile.name) : window.Text("None", "无"));
                case CampusBrushMode.PlacePrefab:
                    GameObject prefab = window.GetCurrentPrefabOrFallback();
                    Vector2Int footprint = GetRotatedPrefabFootprintSize(prefab, window.Rotation90);
                    return window.Text("Cell ", "格子 ") + cell + "\n" +
                           window.Text("Object: ", "物体: ") + (prefab != null ? CampusObjectNames.GetDisplayName(prefab.name) : window.Text("None", "无")) + "\n" +
                           "Footprint: " + footprint.x + " x " + footprint.y;
                case CampusBrushMode.PlaceStair:
                    GameObject stair = window.GetStairPrefabOrFallback();
                    Vector3Int secondaryCell = GetStairSecondaryCell(cell, window.Rotation90);
                    return window.Text("Cell ", "格子 ") + cell + " -> " + secondaryCell + "\n" + window.Text("Stair: ", "楼梯: ") + (stair != null ? CampusObjectNames.GetDisplayName(stair.name) : window.Text("None", "无"));
                case CampusBrushMode.PlaceLight:
                    return window.Text("Cell ", "格子 ") + cell + "\n" +
                           window.Text("Light: ", "光源: ") + window.LightBrushName + "\n" +
                           window.Text("Type: ", "类型: ") + window.LightBrushType;
                case CampusBrushMode.Erase:
                    return window.Text("Cell ", "格子 ") + cell + "\n" + window.Text("Erase", "擦除");
                case CampusBrushMode.Pick:
                    return window.Text("Cell ", "格子 ") + cell + "\n" + window.Text("Pick", "拾取");
                case CampusBrushMode.RectangleFillFloor:
                    return window.Text("Rectangle Floor", "矩形铺地");
                case CampusBrushMode.RectangleErase:
                    return window.Text("Rectangle Erase", "矩形删除");
                case CampusBrushMode.RectangleWall:
                    return window.Text("Rectangle Wall", "矩形墙体");
                case CampusBrushMode.Pan:
                    return window.Text("Pan View", "拖拽平移");
                default:
                    return window.Text("Cell ", "格子 ") + cell;
            }
        }

        private static Color PreviewColor(CampusBrushMode mode)
        {
            switch (mode)
            {
                case CampusBrushMode.PaintFloorTile:
                    return new Color(0.1f, 0.8f, 0.45f, 0.95f);
                case CampusBrushMode.PaintWallTile:
                    return new Color(0.95f, 0.55f, 0.12f, 0.95f);
                case CampusBrushMode.PlacePrefab:
                    return new Color(0.25f, 0.55f, 1f, 0.95f);
                case CampusBrushMode.PlaceStair:
                    return new Color(1f, 0.86f, 0.2f, 0.95f);
                case CampusBrushMode.PlaceLight:
                    return new Color(1f, 0.96f, 0.45f, 0.95f);
                case CampusBrushMode.Erase:
                    return new Color(1f, 0.18f, 0.18f, 0.95f);
                case CampusBrushMode.Pick:
                    return new Color(0.7f, 0.45f, 1f, 0.95f);
                case CampusBrushMode.RectangleFillFloor:
                    return new Color(0.15f, 0.75f, 1f, 0.95f);
                case CampusBrushMode.RectangleErase:
                    return new Color(1f, 0.12f, 0.12f, 0.95f);
                case CampusBrushMode.RectangleWall:
                    return new Color(1f, 0.60f, 0.10f, 0.95f);
                case CampusBrushMode.Pan:
                    return new Color(0.95f, 0.95f, 0.95f, 0.95f);
                default:
                    return Color.white;
            }
        }

        private static Matrix4x4 BuildTileTransform(CampusMapEditorWindow window, int tileSize)
        {
            Vector3 scale = new Vector3(window.FlipX ? -1f : 1f, window.FlipY ? -1f : 1f, 1f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, window.Rotation90 * 90f);
            return Matrix4x4.TRS(Vector3.zero, rotation, scale);
        }

        private static Matrix4x4 BuildTileTransform(int tileSize, int rotation90, bool flipX, bool flipY)
        {
            Vector3 scale = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(rotation90, 0, 3) * 90f);
            return Matrix4x4.TRS(Vector3.zero, rotation, scale);
        }

        private static Vector3Int NormalizeFloorTileAnchor(Vector3Int cell, int tileSize)
        {
            int size = Mathf.Clamp(tileSize, 1, 3);
            if (size <= 1)
            {
                return cell;
            }

            return new Vector3Int(FloorToMultiple(cell.x, size), FloorToMultiple(cell.y, size), cell.z);
        }

        private static int FloorToMultiple(int value, int size)
        {
            return Mathf.FloorToInt((float)value / size) * size;
        }

        private static void ApplyFlip(Transform transform, bool flipX, bool flipY)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (flipX ? -1f : 1f);
            scale.y = Mathf.Abs(scale.y) * (flipY ? -1f : 1f);
            transform.localScale = scale;
        }

        private static void ApplySpriteSorting(Transform root, int sortingOrder)
        {
            if (root == null)
            {
                return;
            }

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = sortingOrder + i;
                }
            }
        }

        private static Vector3Int GetStairSecondaryCell(Vector3Int primaryCell, int rotation90)
        {
            return primaryCell + CampusStairLink.DirectionFromRotation(rotation90);
        }

        private static System.Collections.Generic.IEnumerable<Vector3Int> BrushCells(Vector3Int anchorCell, int brushSize)
        {
            int size = Mathf.Max(1, brushSize);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    yield return new Vector3Int(anchorCell.x + x, anchorCell.y + y, anchorCell.z);
                }
            }
        }
    }
}
