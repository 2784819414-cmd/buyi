using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampusMapEditor
{
    internal enum CampusMapEditorLogTextId
    {
        InstallRefreshStoredSceneTools = 0,
        CreateTestPlayerInScene = 1,
        NoFloorTileSelected = 2,
        NoWallTileSelected = 3,
        CurrentFloorMissingWallLogic = 4,
        CannotPlaceObjectNoPrefab = 5,
        CannotPlaceObjectMissingPropsRoot = 6,
        CannotPlaceObjectMissingGrid = 7,
        CannotPlaceObjectInstantiateFailed = 8,
        PlacedObject = 9,
        CannotPlaceLightCreateLight2D = 10,
        PlacedLight = 11,
        SelectWallLogicTile = 12,
        TextureSourceFolderMissing = 13,
        ValidationStarted = 14,
        ValidationFailedCampusMapRootMissing = 15,
        ValidationFailedFloorOneMissing = 16,
        ValidationFinished = 17,
        FixValidationIssuesCompleted = 18,
        ValidationNullFloorReference = 19,
        ValidationFloorMissingGrid = 20,
        ValidationFloorMissingTilemapFloor = 21,
        ValidationFloorMissingTilemapWallLogic = 22,
        ValidationFloorMissingTilemapWallCap = 23,
        ValidationFloorMissingTilemapWallFace = 24,
        ValidationFloorMissingTilemapOverlay = 25,
        ValidationFloorMissingPropsRoot = 26,
        ValidationFloorMissingStairsRoot = 27,
        ValidationWallLogicMissingTilemapCollider = 28,
        ValidationWallLogicCompositeNotMerge = 29,
        ValidationWallLogicRigidbodyInvalid = 30,
        ValidationWallLogicMissingCompositeCollider = 31,
        ValidationAssetNotAssigned = 32,
        ValidationNullOrSpriteLessTile = 33,
        ValidationNullPrefab = 34,
        ValidationWallPaletteMissing = 35,
        ValidationWallPaletteHorizontalInvalid = 36,
        ValidationWallPaletteVerticalInvalid = 37,
        ValidationWallPaletteCornerInvalid = 38,
        ValidationWallPaletteHighInvalid = 39,
        ValidationWallPaletteNullSprite = 40,
        ValidationOrphanStair = 41,
        ValidationLockedFloorHasContent = 42,
        ValidationPlacedFloorMismatch = 43,
        ValidationPlacedCellMismatch = 44,
        CouldNotConfigureSimpleInteractable = 45,
        ExitPlayModeBeforeBake = 46,
        CannotCreateTestPlayerPrefabMissing = 47
    }

    internal static class CampusMapEditorLogTextCatalog
    {
        private static readonly Dictionary<CampusMapEditorLogTextId, Entry> Entries = new()
        {
            { CampusMapEditorLogTextId.InstallRefreshStoredSceneTools, new Entry("安装 / 刷新场景工具", "Install / Refresh Stored Scene Tools") },
            { CampusMapEditorLogTextId.CreateTestPlayerInScene, new Entry("在场景中创建测试玩家", "Create Test Player In Scene") },
            { CampusMapEditorLogTextId.NoFloorTileSelected, new Entry("[NtingCampusMapEditor] 未选择地板瓦片，也没有可用的调试兜底。", "[NtingCampusMapEditor] No floor tile selected and no debug fallback is available.") },
            { CampusMapEditorLogTextId.NoWallTileSelected, new Entry("[NtingCampusMapEditor] 未选择墙体瓦片，也没有可用的调试兜底。", "[NtingCampusMapEditor] No wall tile selected and no debug fallback is available.") },
            { CampusMapEditorLogTextId.CurrentFloorMissingWallLogic, new Entry("[NtingCampusMapEditor] 当前楼层缺少 Tilemap_WallLogic。", "[NtingCampusMapEditor] Current floor is missing Tilemap_WallLogic.") },
            { CampusMapEditorLogTextId.CannotPlaceObjectNoPrefab, new Entry("[NtingCampusMapEditor] 无法放置物体：未选择 prefab，也没有可用兜底 prefab。", "[NtingCampusMapEditor] Cannot place object: no prefab is selected and no fallback prefab is available.") },
            { CampusMapEditorLogTextId.CannotPlaceObjectMissingPropsRoot, new Entry("[NtingCampusMapEditor] 无法放置物体：当前楼层缺少 PropsRoot。请运行 Fix Validation Issues。", "[NtingCampusMapEditor] Cannot place object: current floor is missing PropsRoot. Run Fix Validation Issues.") },
            { CampusMapEditorLogTextId.CannotPlaceObjectMissingGrid, new Entry("[NtingCampusMapEditor] 无法放置物体：当前楼层缺少 Grid。请运行 Fix Validation Issues。", "[NtingCampusMapEditor] Cannot place object: current floor is missing Grid. Run Fix Validation Issues.") },
            { CampusMapEditorLogTextId.CannotPlaceObjectInstantiateFailed, new Entry("[NtingCampusMapEditor] 无法放置物体：实例化 prefab 失败：{0}。", "[NtingCampusMapEditor] Cannot place object: failed to instantiate prefab '{0}'.") },
            { CampusMapEditorLogTextId.PlacedObject, new Entry("[NtingCampusMapEditor] 已放置物体 {0}，楼层 {1}，格子 {2}。", "[NtingCampusMapEditor] Placed object '{0}' at floor {1}, cell {2}.") },
            { CampusMapEditorLogTextId.CannotPlaceLightCreateLight2D, new Entry("[NtingCampusMapEditor] 无法放置灯光：创建 Light2D 失败。", "[NtingCampusMapEditor] Cannot place light: failed to create Light2D.") },
            { CampusMapEditorLogTextId.PlacedLight, new Entry("[NtingCampusMapEditor] 已放置灯光 {0}，楼层 {1}，格子 {2}。", "[NtingCampusMapEditor] Placed light '{0}' at floor {1}, cell {2}.") },
            { CampusMapEditorLogTextId.SelectWallLogicTile, new Entry("[NtingCampusMapEditor] 应用墙体贴图前请先选择墙体逻辑瓦片。", "[NtingCampusMapEditor] Select a wall logic tile before applying wall textures.") },
            { CampusMapEditorLogTextId.TextureSourceFolderMissing, new Entry("[NtingCampusMapEditor] 贴图源文件夹不存在：{0}", "[NtingCampusMapEditor] Texture source folder is missing: {0}") },
            { CampusMapEditorLogTextId.ValidationStarted, new Entry("[NtingCampusMapEditor] 校验开始。", "[NtingCampusMapEditor] Validation started.") },
            { CampusMapEditorLogTextId.ValidationFailedCampusMapRootMissing, new Entry("[NtingCampusMapEditor] 校验失败：缺少 CampusMapRoot。", "[NtingCampusMapEditor] Validation failed: CampusMapRoot is missing.") },
            { CampusMapEditorLogTextId.ValidationFailedFloorOneMissing, new Entry("[NtingCampusMapEditor] 校验失败：缺少 Floor_1。", "[NtingCampusMapEditor] Validation failed: Floor_1 is missing.") },
            { CampusMapEditorLogTextId.ValidationFinished, new Entry("[NtingCampusMapEditor] 校验结束。", "[NtingCampusMapEditor] Validation finished.") },
            { CampusMapEditorLogTextId.FixValidationIssuesCompleted, new Entry("[NtingCampusMapEditor] Fix Validation Issues 已完成。孤立楼梯只报告，不自动删除。", "[NtingCampusMapEditor] Fix Validation Issues completed. Orphan stairs are reported by validation but not deleted automatically.") },
            { CampusMapEditorLogTextId.ValidationNullFloorReference, new Entry("[NtingCampusMapEditor] 校验：楼层引用为空。", "[NtingCampusMapEditor] Validation: A floor reference is null.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingGrid, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 Grid。", "[NtingCampusMapEditor] Validation: {0} is missing Grid.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingTilemapFloor, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 Tilemap_Floor。", "[NtingCampusMapEditor] Validation: {0} is missing Tilemap_Floor.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingTilemapWallLogic, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 Tilemap_WallLogic。", "[NtingCampusMapEditor] Validation: {0} is missing Tilemap_WallLogic.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingTilemapWallCap, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 Tilemap_WallCap。", "[NtingCampusMapEditor] Validation: {0} is missing Tilemap_WallCap.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingTilemapWallFace, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 Tilemap_WallFace。", "[NtingCampusMapEditor] Validation: {0} is missing Tilemap_WallFace.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingTilemapOverlay, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 Tilemap_Overlay。", "[NtingCampusMapEditor] Validation: {0} is missing Tilemap_Overlay.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingPropsRoot, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 PropsRoot。", "[NtingCampusMapEditor] Validation: {0} is missing PropsRoot.") },
            { CampusMapEditorLogTextId.ValidationFloorMissingStairsRoot, new Entry("[NtingCampusMapEditor] 校验：{0} 缺少 StairsRoot。", "[NtingCampusMapEditor] Validation: {0} is missing StairsRoot.") },
            { CampusMapEditorLogTextId.ValidationWallLogicMissingTilemapCollider, new Entry("[NtingCampusMapEditor] 校验：{0} Tilemap_WallLogic 缺少 TilemapCollider2D。", "[NtingCampusMapEditor] Validation: {0} Tilemap_WallLogic is missing TilemapCollider2D.") },
            { CampusMapEditorLogTextId.ValidationWallLogicCompositeNotMerge, new Entry("[NtingCampusMapEditor] 校验：{0} Tilemap_WallLogic compositeOperation 不是 Merge。", "[NtingCampusMapEditor] Validation: {0} Tilemap_WallLogic compositeOperation is not Merge.") },
            { CampusMapEditorLogTextId.ValidationWallLogicRigidbodyInvalid, new Entry("[NtingCampusMapEditor] 校验：{0} Tilemap_WallLogic Rigidbody2D 必须为 Static 且 simulated。", "[NtingCampusMapEditor] Validation: {0} Tilemap_WallLogic Rigidbody2D must be Static and simulated.") },
            { CampusMapEditorLogTextId.ValidationWallLogicMissingCompositeCollider, new Entry("[NtingCampusMapEditor] 校验：{0} Tilemap_WallLogic 缺少 CompositeCollider2D。", "[NtingCampusMapEditor] Validation: {0} Tilemap_WallLogic is missing CompositeCollider2D.") },
            { CampusMapEditorLogTextId.ValidationAssetNotAssigned, new Entry("[NtingCampusMapEditor] 校验：{0} 未分配。", "[NtingCampusMapEditor] Validation: {0} is not assigned.") },
            { CampusMapEditorLogTextId.ValidationNullOrSpriteLessTile, new Entry("[NtingCampusMapEditor] 校验：{0} 第 {1} 项为空或没有 sprite。", "[NtingCampusMapEditor] Validation: {0} has a null or sprite-less tile at index {1}.") },
            { CampusMapEditorLogTextId.ValidationNullPrefab, new Entry("[NtingCampusMapEditor] 校验：{0} 第 {1} 项为空 prefab。", "[NtingCampusMapEditor] Validation: {0} has a null prefab at index {1}.") },
            { CampusMapEditorLogTextId.ValidationWallPaletteMissing, new Entry("[NtingCampusMapEditor] 校验：未分配 Wall Palette。", "[NtingCampusMapEditor] Validation: Wall Palette is not assigned.") },
            { CampusMapEditorLogTextId.ValidationWallPaletteHorizontalInvalid, new Entry("[NtingCampusMapEditor] 校验：Wall Palette HorizontalWall 缺失或无效。", "[NtingCampusMapEditor] Validation: Wall Palette HorizontalWall is missing or invalid.") },
            { CampusMapEditorLogTextId.ValidationWallPaletteVerticalInvalid, new Entry("[NtingCampusMapEditor] 校验：Wall Palette VerticalWall 缺失或无效。", "[NtingCampusMapEditor] Validation: Wall Palette VerticalWall is missing or invalid.") },
            { CampusMapEditorLogTextId.ValidationWallPaletteCornerInvalid, new Entry("[NtingCampusMapEditor] 校验：Wall Palette CornerWall 缺失或无效。", "[NtingCampusMapEditor] Validation: Wall Palette CornerWall is missing or invalid.") },
            { CampusMapEditorLogTextId.ValidationWallPaletteHighInvalid, new Entry("[NtingCampusMapEditor] 校验：Wall Palette HighWall 缺失或无效。", "[NtingCampusMapEditor] Validation: Wall Palette HighWall is missing or invalid.") },
            { CampusMapEditorLogTextId.ValidationWallPaletteNullSprite, new Entry("[NtingCampusMapEditor] 校验：Wall Palette 第 {0} 项为空或没有 sprite。", "[NtingCampusMapEditor] Validation: Wall Palette has a null or sprite-less tile at index {0}.") },
            { CampusMapEditorLogTextId.ValidationOrphanStair, new Entry("[NtingCampusMapEditor] 校验：孤立楼梯 LinkId {0} 只有一个楼梯。", "[NtingCampusMapEditor] Validation: Orphan stair LinkId {0} has only one stair.") },
            { CampusMapEditorLogTextId.ValidationLockedFloorHasContent, new Entry("[NtingCampusMapEditor] 校验：锁定楼层 {0} 包含已创作内容。", "[NtingCampusMapEditor] Validation: Locked floor {0} contains authored content.") },
            { CampusMapEditorLogTextId.ValidationPlacedFloorMismatch, new Entry("[NtingCampusMapEditor] 校验：{0} FloorIndex 与父级 {1} 不匹配。", "[NtingCampusMapEditor] Validation: {0} FloorIndex does not match parent {1}.") },
            { CampusMapEditorLogTextId.ValidationPlacedCellMismatch, new Entry("[NtingCampusMapEditor] 校验：{0} Cell 与 transform 位置不匹配。", "[NtingCampusMapEditor] Validation: {0} Cell does not match transform position.") },
            { CampusMapEditorLogTextId.CouldNotConfigureSimpleInteractable, new Entry("[NtingCampusMapEditor] CampusSimpleInteractable 尚不可用，无法配置 simple interactable。", "Could not configure simple interactable because CampusSimpleInteractable is not available yet.") },
            { CampusMapEditorLogTextId.ExitPlayModeBeforeBake, new Entry("[NtingCampusRuntimeMapEditor] 烘焙 authoring map 到场景前请先退出 Play Mode。", "[NtingCampusRuntimeMapEditor] Exit Play Mode before baking the authoring map into the scene.") },
            { CampusMapEditorLogTextId.CannotCreateTestPlayerPrefabMissing, new Entry("[NtingCampusMapEditor] 无法创建测试玩家，prefab 缺失：{0}", "[NtingCampusMapEditor] Cannot create test player because the prefab is missing: {0}") }
        };

        public static string Get(CampusMapEditorLogTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }

        public static string Format(CampusMapEditorLogTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static void Log(CampusMapEditorLogTextId id, params object[] args)
        {
            Debug.Log(Format(id, args));
        }

        public static void Warning(CampusMapEditorLogTextId id, params object[] args)
        {
            Debug.LogWarning(Format(id, args));
        }

        public static void Error(CampusMapEditorLogTextId id, params object[] args)
        {
            Debug.LogError(Format(id, args));
        }
    }
}
