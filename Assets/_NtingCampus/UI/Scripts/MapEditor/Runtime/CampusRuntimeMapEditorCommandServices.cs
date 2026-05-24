using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NtingCampus.Gameplay.Retail;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public enum CampusRuntimeMapEditorFloorDeleteOutcome
    {
        None = 0,
        MissingMapRoot = 1,
        KeepAtLeastOneFloor = 2,
        MissingFloor = 3,
        Deleted = 4
    }

    public readonly struct CampusRuntimeMapEditorFloorDeleteResult
    {
        public CampusRuntimeMapEditorFloorDeleteResult(
            CampusRuntimeMapEditorFloorDeleteOutcome outcome,
            int nextSelectedFloorIndex)
        {
            Outcome = outcome;
            NextSelectedFloorIndex = nextSelectedFloorIndex;
        }

        public CampusRuntimeMapEditorFloorDeleteOutcome Outcome { get; }
        public int NextSelectedFloorIndex { get; }
    }

    public static class CampusRuntimeMapEditorFloorCommandService
    {
        public static int SelectFloor(CampusMapRoot mapRoot, int floorIndex)
        {
            int nextFloorIndex = Mathf.Max(1, floorIndex);
            if (mapRoot != null)
            {
                mapRoot.CurrentPreviewFloor = nextFloorIndex;
            }

            return nextFloorIndex;
        }

        public static int AddFloor(
            CampusMapRoot mapRoot,
            Action recordUndo,
            Func<int, CampusFloorRoot> ensureFloor)
        {
            if (mapRoot == null)
            {
                return 0;
            }

            recordUndo?.Invoke();
            int nextFloorIndex = mapRoot.GetHighestFloorIndex() + 1;
            ensureFloor?.Invoke(nextFloorIndex);
            mapRoot.CurrentPreviewFloor = nextFloorIndex;
            return nextFloorIndex;
        }

        public static bool ToggleFloorLock(CampusFloorRoot floor, Action recordUndo)
        {
            if (floor == null)
            {
                return false;
            }

            recordUndo?.Invoke();
            floor.IsUnlocked = !floor.IsUnlocked;
            return true;
        }

        public static CampusRuntimeMapEditorFloorDeleteResult DeleteSelectedFloor(
            CampusMapRoot mapRoot,
            int selectedFloorIndex,
            Action recordUndo,
            Action<GameObject> destroyFloorObject)
        {
            if (mapRoot == null)
            {
                return new CampusRuntimeMapEditorFloorDeleteResult(
                    CampusRuntimeMapEditorFloorDeleteOutcome.MissingMapRoot,
                    selectedFloorIndex);
            }

            mapRoot.RebuildFloorReferences();
            if (mapRoot.Floors.Count <= 1)
            {
                return new CampusRuntimeMapEditorFloorDeleteResult(
                    CampusRuntimeMapEditorFloorDeleteOutcome.KeepAtLeastOneFloor,
                    selectedFloorIndex);
            }

            CampusFloorRoot floor = mapRoot.GetFloor(selectedFloorIndex);
            if (floor == null)
            {
                return new CampusRuntimeMapEditorFloorDeleteResult(
                    CampusRuntimeMapEditorFloorDeleteOutcome.MissingFloor,
                    selectedFloorIndex);
            }

            recordUndo?.Invoke();
            destroyFloorObject?.Invoke(floor.gameObject);

            int nextSelectedFloorIndex = 1;
            mapRoot.RebuildFloorReferences();
            if (mapRoot.Floors.Count > 0 && mapRoot.GetFloor(nextSelectedFloorIndex) == null)
            {
                nextSelectedFloorIndex = mapRoot.Floors[0].FloorIndex;
            }

            mapRoot.CurrentPreviewFloor = nextSelectedFloorIndex;
            return new CampusRuntimeMapEditorFloorDeleteResult(
                CampusRuntimeMapEditorFloorDeleteOutcome.Deleted,
                nextSelectedFloorIndex);
        }
    }

    public sealed class CampusRuntimeMapEditorToolbarCommandMap
    {
        public Action Close { get; set; }
        public Action ToggleHelp { get; set; }
        public Action Import { get; set; }
        public Action Export { get; set; }
        public Action Undo { get; set; }
        public Action Redo { get; set; }
        public Action ToggleGrid { get; set; }
        public Action ToggleSettings { get; set; }
        public Action Rebuild { get; set; }
    }

    public static class CampusRuntimeMapEditorToolbarCommandService
    {
        public static void Execute(
            CampusRuntimeMapEditorToolbarInteraction interaction,
            CampusRuntimeMapEditorToolbarCommandMap commandMap)
        {
            if (commandMap == null)
            {
                return;
            }

            if (interaction.CloseRequested)
            {
                commandMap.Close?.Invoke();
            }

            if (interaction.ToggleHelpRequested)
            {
                commandMap.ToggleHelp?.Invoke();
            }

            if (interaction.ImportRequested)
            {
                commandMap.Import?.Invoke();
            }

            if (interaction.ExportRequested)
            {
                commandMap.Export?.Invoke();
            }

            if (interaction.UndoRequested)
            {
                commandMap.Undo?.Invoke();
            }

            if (interaction.RedoRequested)
            {
                commandMap.Redo?.Invoke();
            }

            if (interaction.ToggleGridRequested)
            {
                commandMap.ToggleGrid?.Invoke();
            }

            if (interaction.ToggleSettingsRequested)
            {
                commandMap.ToggleSettings?.Invoke();
            }

            if (interaction.RebuildRequested)
            {
                commandMap.Rebuild?.Invoke();
            }
        }
    }

    public enum CampusRuntimeMapEditorHistoryOutcome
    {
        NoSnapshotAvailable = 0,
        Applied = 1
    }

    public readonly struct CampusRuntimeMapEditorHistoryResult
    {
        public CampusRuntimeMapEditorHistoryResult(
            CampusRuntimeMapEditorHistoryOutcome outcome,
            bool usedWallStrokeEntry)
        {
            Outcome = outcome;
            UsedWallStrokeEntry = usedWallStrokeEntry;
        }

        public CampusRuntimeMapEditorHistoryOutcome Outcome { get; }
        public bool UsedWallStrokeEntry { get; }
    }

    public static class CampusRuntimeMapEditorHistoryCommandService
    {
        public static CampusRuntimeMapEditorHistoryResult Undo(
            List<string> undoSnapshots,
            List<string> redoSnapshots,
            Func<string, bool> tryApplyWallStrokeUndoEntry,
            Func<string> buildCurrentSnapshotJson,
            Action<string> loadSnapshotJson)
        {
            if (undoSnapshots == null || redoSnapshots == null || undoSnapshots.Count == 0)
            {
                return new CampusRuntimeMapEditorHistoryResult(
                    CampusRuntimeMapEditorHistoryOutcome.NoSnapshotAvailable,
                    false);
            }

            string previous = undoSnapshots[undoSnapshots.Count - 1];
            undoSnapshots.RemoveAt(undoSnapshots.Count - 1);

            if (tryApplyWallStrokeUndoEntry != null && tryApplyWallStrokeUndoEntry(previous))
            {
                redoSnapshots.Add(previous);
                return new CampusRuntimeMapEditorHistoryResult(
                    CampusRuntimeMapEditorHistoryOutcome.Applied,
                    true);
            }

            if (buildCurrentSnapshotJson == null || loadSnapshotJson == null)
            {
                return new CampusRuntimeMapEditorHistoryResult(
                    CampusRuntimeMapEditorHistoryOutcome.NoSnapshotAvailable,
                    false);
            }

            string current = buildCurrentSnapshotJson();
            redoSnapshots.Add(current);
            loadSnapshotJson(previous);
            return new CampusRuntimeMapEditorHistoryResult(
                CampusRuntimeMapEditorHistoryOutcome.Applied,
                false);
        }

        public static CampusRuntimeMapEditorHistoryResult Redo(
            List<string> undoSnapshots,
            List<string> redoSnapshots,
            Func<string, bool> tryApplyWallStrokeUndoEntry,
            Func<string> buildCurrentSnapshotJson,
            Action<string> loadSnapshotJson)
        {
            if (undoSnapshots == null || redoSnapshots == null || redoSnapshots.Count == 0)
            {
                return new CampusRuntimeMapEditorHistoryResult(
                    CampusRuntimeMapEditorHistoryOutcome.NoSnapshotAvailable,
                    false);
            }

            string next = redoSnapshots[redoSnapshots.Count - 1];
            redoSnapshots.RemoveAt(redoSnapshots.Count - 1);

            if (tryApplyWallStrokeUndoEntry != null && tryApplyWallStrokeUndoEntry(next))
            {
                undoSnapshots.Add(next);
                return new CampusRuntimeMapEditorHistoryResult(
                    CampusRuntimeMapEditorHistoryOutcome.Applied,
                    true);
            }

            if (buildCurrentSnapshotJson == null || loadSnapshotJson == null)
            {
                return new CampusRuntimeMapEditorHistoryResult(
                    CampusRuntimeMapEditorHistoryOutcome.NoSnapshotAvailable,
                    false);
            }

            string current = buildCurrentSnapshotJson();
            undoSnapshots.Add(current);
            loadSnapshotJson(next);
            return new CampusRuntimeMapEditorHistoryResult(
                CampusRuntimeMapEditorHistoryOutcome.Applied,
                false);
        }
    }

    public enum CampusRuntimeMapEditorPersistenceOutcome
    {
        Succeeded = 0,
        Busy = 1,
        UnsupportedSource = 2,
        MissingPath = 3,
        MissingPlayerSave = 4,
        MissingAuthoringPackage = 5,
        MissingExportFolder = 6,
        MissingExportJson = 7,
        Failed = 8
    }

    public readonly struct CampusRuntimeMapEditorPersistenceResult
    {
        public CampusRuntimeMapEditorPersistenceResult(
            CampusRuntimeMapEditorPersistenceOutcome outcome,
            string path,
            string rootPath,
            string errorMessage)
        {
            Outcome = outcome;
            Path = path ?? string.Empty;
            RootPath = rootPath ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public CampusRuntimeMapEditorPersistenceOutcome Outcome { get; }
        public string Path { get; }
        public string RootPath { get; }
        public string ErrorMessage { get; }
        public bool Succeeded => Outcome == CampusRuntimeMapEditorPersistenceOutcome.Succeeded;
    }

    public static class CampusRuntimeMapEditorPersistenceCommandService
    {
        public static CampusRuntimeMapEditorPersistenceResult ExportSnapshotJson(
            CampusRuntimeMapSnapshot snapshot,
            Action<string, bool> saveGameplayOverlayForMapPath)
        {
            try
            {
                string path = CampusRuntimeMapSnapshotStore.ExportMap(snapshot);
                saveGameplayOverlayForMapPath?.Invoke(path, false);
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Succeeded,
                    path,
                    CampusRuntimeMapSnapshotStore.GetExportFolder(),
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Failed,
                    string.Empty,
                    CampusRuntimeMapSnapshotStore.GetExportFolder(),
                    exception.Message);
            }
        }

        public static CampusRuntimeMapEditorPersistenceResult SavePlayerMap(
            CampusRuntimeMapSnapshot snapshot,
            Action ensureImportFolders,
            Action<string, bool> saveGameplayOverlayForMapPath)
        {
            try
            {
                ensureImportFolders?.Invoke();
                string mapPath = CampusRuntimeMapSnapshotStore.WritePlayerSave(snapshot);
                string saveRoot = CampusRuntimeMapSnapshotStore.GetPlayerSaveRootFolder();
                saveGameplayOverlayForMapPath?.Invoke(mapPath, false);
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Succeeded,
                    mapPath,
                    saveRoot,
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Failed,
                    string.Empty,
                    CampusRuntimeMapSnapshotStore.GetPlayerSaveRootFolder(),
                    exception.Message);
            }
        }

        public static CampusRuntimeMapEditorPersistenceResult SaveCurrentMapSource(
            CampusRuntimeMapLoadSource lastMapLoadSource,
            string lastMapLoadPath,
            Func<CampusRuntimeMapEditorPersistenceResult> savePlayerMap,
            Func<string, CampusRuntimeMapLoadSource, CampusRuntimeMapEditorPersistenceResult> saveMapToPath)
        {
            switch (lastMapLoadSource)
            {
                case CampusRuntimeMapLoadSource.AuthoringPackage:
                    if (string.IsNullOrWhiteSpace(lastMapLoadPath))
                    {
                        return new CampusRuntimeMapEditorPersistenceResult(
                            CampusRuntimeMapEditorPersistenceOutcome.MissingPath,
                            string.Empty,
                            string.Empty,
                            string.Empty);
                    }

                    return saveMapToPath != null
                        ? saveMapToPath(lastMapLoadPath, CampusRuntimeMapLoadSource.AuthoringPackage)
                        : new CampusRuntimeMapEditorPersistenceResult(
                            CampusRuntimeMapEditorPersistenceOutcome.Failed,
                            lastMapLoadPath,
                            string.Empty,
                            string.Empty);
                case CampusRuntimeMapLoadSource.PlayerSave:
                    return savePlayerMap != null
                        ? savePlayerMap()
                        : new CampusRuntimeMapEditorPersistenceResult(
                            CampusRuntimeMapEditorPersistenceOutcome.Failed,
                            string.Empty,
                            CampusRuntimeMapSnapshotStore.GetPlayerSaveRootFolder(),
                            string.Empty);
                default:
                    return new CampusRuntimeMapEditorPersistenceResult(
                        CampusRuntimeMapEditorPersistenceOutcome.UnsupportedSource,
                        string.Empty,
                        string.Empty,
                        string.Empty);
            }
        }

        public static CampusRuntimeMapEditorPersistenceResult SaveMapToPath(
            string path,
            CampusRuntimeMapLoadSource source,
            CampusRuntimeMapSnapshot snapshot,
            Action ensureImportFolders,
            Action<string, bool> saveGameplayOverlayForMapPath,
            Action<CampusRuntimeMapLoadSource, string> rememberMapLoadSource,
            Action refreshAssetDatabaseIfAvailable)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.MissingPath,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }

            try
            {
                ensureImportFolders?.Invoke();
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                CampusRuntimeMapSnapshotStore.WriteMap(path, snapshot, true);
                saveGameplayOverlayForMapPath?.Invoke(path, false);
                rememberMapLoadSource?.Invoke(source, path);
                refreshAssetDatabaseIfAvailable?.Invoke();
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Succeeded,
                    path,
                    folder,
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Failed,
                    path,
                    Path.GetDirectoryName(path),
                    exception.Message);
            }
        }

        public static CampusRuntimeMapEditorPersistenceResult LoadPlayerMap(
            string savePath,
            bool recordUndo,
            Action recordUndoAction,
            Action ensureImportFolders,
            Action loadRuntimeResources,
            Action<CampusRuntimeMapSnapshot> applySnapshot,
            Action<CampusRuntimeMapLoadSource, string> rememberMapLoadSource,
            Action<string, bool> applyGameplayOverlayFromPath)
        {
            if (!File.Exists(savePath))
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.MissingPlayerSave,
                    savePath,
                    CampusRuntimeMapSnapshotStore.GetPlayerSaveRootFolder(),
                    string.Empty);
            }

            try
            {
                if (recordUndo)
                {
                    recordUndoAction?.Invoke();
                }

                ensureImportFolders?.Invoke();
                loadRuntimeResources?.Invoke();
                applySnapshot?.Invoke(CampusRuntimeMapSnapshotStore.ReadMap(savePath));
                rememberMapLoadSource?.Invoke(CampusRuntimeMapLoadSource.PlayerSave, savePath);
                applyGameplayOverlayFromPath?.Invoke(savePath, false);
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Succeeded,
                    savePath,
                    CampusRuntimeMapSnapshotStore.GetPlayerSaveRootFolder(),
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Failed,
                    savePath,
                    CampusRuntimeMapSnapshotStore.GetPlayerSaveRootFolder(),
                    exception.Message);
            }
        }

        public static CampusRuntimeMapEditorPersistenceResult ExportAuthoringPackage(
            Action savePlayerMapBeforeExport,
            CampusRuntimeMapSnapshot snapshot,
            string importRootFolder,
            Action ensureImportFolders,
            Action<string, bool> saveGameplayOverlayForMapPath,
            Action refreshAssetDatabaseIfAvailable)
        {
            try
            {
                savePlayerMapBeforeExport?.Invoke();
                ensureImportFolders?.Invoke();
                string packageRoot = CampusRuntimeMapSnapshotStore.GetAuthoringPackageRootFolder();
                string mapPath = CampusRuntimeMapSnapshotStore.WriteAuthoringPackage(snapshot, importRootFolder);
                saveGameplayOverlayForMapPath?.Invoke(mapPath, false);
                refreshAssetDatabaseIfAvailable?.Invoke();
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Succeeded,
                    mapPath,
                    packageRoot,
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Failed,
                    string.Empty,
                    CampusRuntimeMapSnapshotStore.GetAuthoringPackageRootFolder(),
                    exception.Message);
            }
        }

        public static CampusRuntimeMapEditorPersistenceResult RestoreAuthoringPackage(
            string packageRoot,
            string packageImportFolder,
            string packageMapPath,
            string importRootFolder,
            bool recordUndo,
            Action recordUndoAction,
            Action loadRuntimeResources,
            Action<string> loadSnapshotJson,
            Action<CampusRuntimeMapLoadSource, string> rememberMapLoadSource,
            Action<string, bool> applyGameplayOverlayFromPath)
        {
            if (!Directory.Exists(packageImportFolder) && !File.Exists(packageMapPath))
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.MissingAuthoringPackage,
                    packageMapPath,
                    packageRoot,
                    string.Empty);
            }

            try
            {
                if (recordUndo)
                {
                    recordUndoAction?.Invoke();
                }

                if (!CampusRuntimeImportLibrary.AreSamePath(packageImportFolder, importRootFolder))
                {
                    CampusRuntimeImportLibrary.BackupImportRoot(importRootFolder);
                    if (Directory.Exists(packageImportFolder))
                    {
                        CampusRuntimeImportLibrary.MirrorDirectory(packageImportFolder, importRootFolder, true);
                    }
                }

                loadRuntimeResources?.Invoke();
                if (File.Exists(packageMapPath))
                {
                    string json = File.ReadAllText(packageMapPath, Encoding.UTF8);
                    loadSnapshotJson?.Invoke(json);
                    rememberMapLoadSource?.Invoke(CampusRuntimeMapLoadSource.AuthoringPackage, packageMapPath);
                    applyGameplayOverlayFromPath?.Invoke(packageMapPath, false);
                }

                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Succeeded,
                    packageMapPath,
                    packageRoot,
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Failed,
                    packageMapPath,
                    packageRoot,
                    exception.Message);
            }
        }

        public static CampusRuntimeMapEditorPersistenceResult ImportLatestJson(
            string exportFolder,
            bool recordUndo,
            Action recordUndoAction,
            Action<string> loadSnapshotJson,
            Action<string, bool> applyGameplayOverlayFromPath)
        {
            if (!Directory.Exists(exportFolder))
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.MissingExportFolder,
                    string.Empty,
                    exportFolder,
                    string.Empty);
            }

            string[] files = Directory.GetFiles(exportFolder, "CampusMap_*.json");
            if (files.Length == 0)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.MissingExportJson,
                    string.Empty,
                    exportFolder,
                    string.Empty);
            }

            try
            {
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                string path = files[files.Length - 1];
                if (recordUndo)
                {
                    recordUndoAction?.Invoke();
                }

                loadSnapshotJson?.Invoke(File.ReadAllText(path, Encoding.UTF8));
                applyGameplayOverlayFromPath?.Invoke(path, false);
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Succeeded,
                    path,
                    exportFolder,
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new CampusRuntimeMapEditorPersistenceResult(
                    CampusRuntimeMapEditorPersistenceOutcome.Failed,
                    string.Empty,
                    exportFolder,
                    exception.Message);
            }
        }
    }

    public enum CampusRuntimeMapEditorObjectSettingsOutcome
    {
        MissingSelection = 0,
        MissingMapRoot = 1,
        MissingObjectIdentity = 2,
        Applied = 3
    }

    public readonly struct CampusRuntimeMapEditorObjectSettingsResult
    {
        public CampusRuntimeMapEditorObjectSettingsResult(
            CampusRuntimeMapEditorObjectSettingsOutcome outcome,
            int appliedCount,
            string objectId)
        {
            Outcome = outcome;
            AppliedCount = appliedCount;
            ObjectId = objectId ?? string.Empty;
        }

        public CampusRuntimeMapEditorObjectSettingsOutcome Outcome { get; }
        public int AppliedCount { get; }
        public string ObjectId { get; }
    }

    public static class CampusRuntimeMapEditorObjectSettingsCommandService
    {
        public static void CommitDraft(CampusPlacedObject placed)
        {
            if (placed == null)
            {
                return;
            }

            CampusRetailShelf retailShelf = placed.GetComponent<CampusRetailShelf>();
            if (retailShelf != null)
            {
                retailShelf.ItemDefinitionId = string.IsNullOrWhiteSpace(retailShelf.ItemDefinitionId)
                    ? string.Empty
                    : retailShelf.ItemDefinitionId.Trim();
                retailShelf.StockCount = Mathf.Max(1, retailShelf.StockCount);
                retailShelf.DisplaySlotCount = Mathf.Max(1, retailShelf.DisplaySlotCount);
                placed.IsStorageContainer = retailShelf.ShelfMode == CampusRetailShelfMode.Container;
            }

            placed.NormalizeStorageSettings();
            placed.NormalizeCustomInteractionAnchors();
            placed.TypeId = string.IsNullOrWhiteSpace(placed.TypeId) ? string.Empty : placed.TypeId.Trim();
            placed.ApplyVisualScaleState();
            placed.ApplyRotationVisualState();
            placed.ApplyInteractionState();
        }

        public static CampusRuntimeMapEditorObjectSettingsResult ApplySettingsToMatchingPlacedObjects(
            CampusMapRoot mapRoot,
            GameObject prefab,
            CampusRuntimeObjectSettings settings,
            bool recordUndo,
            Action recordUndoAction,
            Func<GameObject, CampusRuntimeObjectSettings, CampusPlacedObject> applyRuntimeObjectSettings,
            Func<CampusPlacedObject, CampusFloorRoot> resolveFloorForPlacedObject,
            Action<CampusPlacedObject> refreshPlacedRetailShelf)
        {
            if (prefab == null || settings == null)
            {
                return new CampusRuntimeMapEditorObjectSettingsResult(
                    CampusRuntimeMapEditorObjectSettingsOutcome.MissingSelection,
                    0,
                    string.Empty);
            }

            if (mapRoot == null)
            {
                return new CampusRuntimeMapEditorObjectSettingsResult(
                    CampusRuntimeMapEditorObjectSettingsOutcome.MissingMapRoot,
                    0,
                    string.Empty);
            }

            string targetObjectId = CampusRuntimeObjectAuthoring.ResolveSyncId(settings, prefab);
            if (string.IsNullOrEmpty(targetObjectId))
            {
                return new CampusRuntimeMapEditorObjectSettingsResult(
                    CampusRuntimeMapEditorObjectSettingsOutcome.MissingObjectIdentity,
                    0,
                    string.Empty);
            }

            int appliedCount = 0;
            bool undoRecorded = false;
            HashSet<CampusFloorRoot> affectedFloors = new HashSet<CampusFloorRoot>();
            mapRoot.RebuildFloorReferences();
            CampusPlacedObject[] objects = mapRoot.GetComponentsInChildren<CampusPlacedObject>(true);
            for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
            {
                CampusPlacedObject placed = objects[objectIndex];
                if (!CampusRuntimeObjectAuthoring.DoesPlacedObjectMatchIdentity(placed, targetObjectId, prefab.name))
                {
                    continue;
                }

                if (recordUndo && !undoRecorded)
                {
                    recordUndoAction?.Invoke();
                    undoRecorded = true;
                }

                int preservedRotation = placed.Rotation90;
                Vector3Int preservedCell = placed.Cell;
                int preservedFloor = placed.FloorIndex;
                applyRuntimeObjectSettings?.Invoke(placed.gameObject, settings);
                placed.Rotation90 = preservedRotation;
                placed.Cell = preservedCell;
                placed.FloorIndex = preservedFloor;
                placed.ApplyRotationVisualState();
                placed.ApplyInteractionState();

                CampusFloorRoot floor = resolveFloorForPlacedObject != null
                    ? resolveFloorForPlacedObject(placed)
                    : null;
                if (floor != null)
                {
                    affectedFloors.Add(floor);
                    if (floor.Grid != null)
                    {
                        placed.ApplyCellToTransform(floor.Grid);
                    }
                }

                refreshPlacedRetailShelf?.Invoke(placed);
                appliedCount++;
            }

            foreach (CampusFloorRoot floor in affectedFloors)
            {
                if (floor == null)
                {
                    continue;
                }

                CampusRenderSortingUtility.ApplyFloorSorting(
                    floor,
                    floor.FloorIndex * mapRoot.SortingOrderStepPerFloor);
            }

            return new CampusRuntimeMapEditorObjectSettingsResult(
                CampusRuntimeMapEditorObjectSettingsOutcome.Applied,
                appliedCount,
                targetObjectId);
        }
    }
}
