using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampusMapEditor
{
    internal enum CampusRuntimeMapEditorLogTextId
    {
        FailedToRebuildGameplayRooms = 0,
        FailedToSetWallMountedSprite = 1,
        FailedToSetObjectDirectionSprite = 2,
        WarningMessage = 3,
        FailedToOpenFolder = 4,
        NativeFileDialogFailed = 5,
        ActiveMapSource = 6,
        FailedToCreateObjectImage = 7,
        FailedToImportImage = 8,
        MapPresentationSetupFailed = 9,
        ReadGameplayOverlayFailed = 10,
        ExportMapJsonFailed = 11,
        ExportedMap = 12,
        SavedPlayerMap = 13,
        SavePlayerMapFailed = 14,
        SaveMapFailed = 15,
        LoadedPlayerMap = 16,
        LoadPlayerMapFailed = 17,
        ExportedAuthoringPackage = 18,
        ExportAuthoringPackageFailed = 19,
        RestoredAuthoringPackage = 20,
        RestoreAuthoringPackageFailed = 21,
        ImportLatestJsonFailed = 22,
        SaveGameplayOverlayFailed = 23,
        LoadGameplayOverlayFailed = 24,
        MigratedRuntimeImportLibrary = 25,
        FailedToDeleteResource = 26,
        FailedToDeleteObjectResource = 27,
        BakedRuntimeGeneratedContent = 28,
        NativeFileDropFailed = 29
    }

    internal static class CampusRuntimeMapEditorLogTextCatalog
    {
        private static readonly Dictionary<CampusRuntimeMapEditorLogTextId, Entry> Entries = new()
        {
            { CampusRuntimeMapEditorLogTextId.FailedToRebuildGameplayRooms, new Entry("[NtingCampusRuntimeMapEditor] 重建 gameplay rooms 失败：{0}", "[NtingCampusRuntimeMapEditor] Failed to rebuild gameplay rooms: {0}") },
            { CampusRuntimeMapEditorLogTextId.FailedToSetWallMountedSprite, new Entry("[NtingCampusRuntimeMapEditor] 设置壁挂 sprite 失败：{0}", "[NtingCampusRuntimeMapEditor] Failed to set wall-mounted sprite: {0}") },
            { CampusRuntimeMapEditorLogTextId.FailedToSetObjectDirectionSprite, new Entry("[NtingCampusRuntimeMapEditor] 设置物体方向 sprite 失败：{0}", "[NtingCampusRuntimeMapEditor] Failed to set object direction sprite: {0}") },
            { CampusRuntimeMapEditorLogTextId.WarningMessage, new Entry("[NtingCampusRuntimeMapEditor] {0}", "[NtingCampusRuntimeMapEditor] {0}") },
            { CampusRuntimeMapEditorLogTextId.FailedToOpenFolder, new Entry("[NtingCampusRuntimeMapEditor] 打开文件夹 {0} 失败：{1}", "[NtingCampusRuntimeMapEditor] Failed to open folder '{0}': {1}") },
            { CampusRuntimeMapEditorLogTextId.NativeFileDialogFailed, new Entry("[NtingCampusRuntimeMapEditor] 原生文件对话框失败：{0}", "[NtingCampusRuntimeMapEditor] Native file dialog failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.ActiveMapSource, new Entry("[NtingCampusRuntimeMapEditor] 当前地图来源：{0}", "[NtingCampusRuntimeMapEditor] Active map source: {0}") },
            { CampusRuntimeMapEditorLogTextId.FailedToCreateObjectImage, new Entry("[NtingCampusRuntimeMapEditor] 创建物体图片 {0} 失败：{1}", "[NtingCampusRuntimeMapEditor] Failed to create object image '{0}': {1}") },
            { CampusRuntimeMapEditorLogTextId.FailedToImportImage, new Entry("[NtingCampusRuntimeMapEditor] 导入图片 {0} 失败：{1}", "[NtingCampusRuntimeMapEditor] Failed to import image '{0}': {1}") },
            { CampusRuntimeMapEditorLogTextId.MapPresentationSetupFailed, new Entry("[NtingCampusRuntimeMapEditor] 地图表现设置失败，保留运行时编辑器 UI：{0}", "[NtingCampusRuntimeMapEditor] Map presentation setup failed, keeping runtime editor UI alive: {0}") },
            { CampusRuntimeMapEditorLogTextId.ReadGameplayOverlayFailed, new Entry("[NtingCampusRuntimeMapEditor] 读取 gameplay overlay 失败：{0}", "[NtingCampusRuntimeMapEditor] Read gameplay overlay failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.ExportMapJsonFailed, new Entry("[NtingCampusRuntimeMapEditor] 导出地图 JSON 失败：{0}", "[NtingCampusRuntimeMapEditor] Export map JSON failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.ExportedMap, new Entry("[NtingCampusRuntimeMapEditor] 已导出地图到 {0}", "[NtingCampusRuntimeMapEditor] Exported map to {0}") },
            { CampusRuntimeMapEditorLogTextId.SavedPlayerMap, new Entry("[NtingCampusRuntimeMapEditor] 已保存玩家地图到 {0}", "[NtingCampusRuntimeMapEditor] Saved player map to {0}") },
            { CampusRuntimeMapEditorLogTextId.SavePlayerMapFailed, new Entry("[NtingCampusRuntimeMapEditor] 保存玩家地图失败：{0}", "[NtingCampusRuntimeMapEditor] Save player map failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.SaveMapFailed, new Entry("[NtingCampusRuntimeMapEditor] 保存地图失败：{0}", "[NtingCampusRuntimeMapEditor] Save map failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.LoadedPlayerMap, new Entry("[NtingCampusRuntimeMapEditor] 已从 {0} 加载玩家地图", "[NtingCampusRuntimeMapEditor] Loaded player map from {0}") },
            { CampusRuntimeMapEditorLogTextId.LoadPlayerMapFailed, new Entry("[NtingCampusRuntimeMapEditor] 加载玩家地图失败：{0}", "[NtingCampusRuntimeMapEditor] Load player map failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.ExportedAuthoringPackage, new Entry("[NtingCampusRuntimeMapEditor] 已导出 authoring map package 到 {0}", "[NtingCampusRuntimeMapEditor] Exported authoring map package to {0}") },
            { CampusRuntimeMapEditorLogTextId.ExportAuthoringPackageFailed, new Entry("[NtingCampusRuntimeMapEditor] 导出 authoring package 失败：{0}", "[NtingCampusRuntimeMapEditor] Export authoring package failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.RestoredAuthoringPackage, new Entry("[NtingCampusRuntimeMapEditor] 已从 {0} 恢复 authoring map package", "[NtingCampusRuntimeMapEditor] Restored authoring map package from {0}") },
            { CampusRuntimeMapEditorLogTextId.RestoreAuthoringPackageFailed, new Entry("[NtingCampusRuntimeMapEditor] 恢复 authoring package 失败：{0}", "[NtingCampusRuntimeMapEditor] Restore authoring package failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.ImportLatestJsonFailed, new Entry("[NtingCampusRuntimeMapEditor] 导入最新 JSON 失败：{0}", "[NtingCampusRuntimeMapEditor] Import latest JSON failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.SaveGameplayOverlayFailed, new Entry("[NtingCampusRuntimeMapEditor] 保存 gameplay overlay 失败：{0}", "[NtingCampusRuntimeMapEditor] Save gameplay overlay failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.LoadGameplayOverlayFailed, new Entry("[NtingCampusRuntimeMapEditor] 加载 gameplay overlay 失败：{0}", "[NtingCampusRuntimeMapEditor] Load gameplay overlay failed: {0}") },
            { CampusRuntimeMapEditorLogTextId.MigratedRuntimeImportLibrary, new Entry("[NtingCampusRuntimeMapEditor] 已将 runtime import library 迁移到项目文件夹：{0}", "[NtingCampusRuntimeMapEditor] Migrated runtime import library into project folder: {0}") },
            { CampusRuntimeMapEditorLogTextId.FailedToDeleteResource, new Entry("[NtingCampusRuntimeMapEditor] 删除 {0} 资源 {1} 失败：{2}", "[NtingCampusRuntimeMapEditor] Failed to delete {0} resource '{1}': {2}") },
            { CampusRuntimeMapEditorLogTextId.FailedToDeleteObjectResource, new Entry("[NtingCampusRuntimeMapEditor] 删除物体资源 {0} 失败：{1}", "[NtingCampusRuntimeMapEditor] Failed to delete object resource '{0}': {1}") },
            { CampusRuntimeMapEditorLogTextId.BakedRuntimeGeneratedContent, new Entry("[NtingCampusRuntimeMapEditor] 已将运行时生成内容烘焙进 {0}", "[NtingCampusRuntimeMapEditor] Baked runtime generated content into {0}") },
            { CampusRuntimeMapEditorLogTextId.NativeFileDropFailed, new Entry("[NtingCampusRuntimeMapEditor] 原生文件拖放失败：{0}", "[NtingCampusRuntimeMapEditor] Native file drop failed: {0}") }
        };

        public static string Format(CampusRuntimeMapEditorLogTextId id, params object[] args)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return string.Format(entry.Get(CampusLanguageState.CurrentLanguage), args);
        }

        public static void Log(CampusRuntimeMapEditorLogTextId id, params object[] args)
        {
            Debug.Log(Format(id, args));
        }

        public static void Warning(CampusRuntimeMapEditorLogTextId id, params object[] args)
        {
            Debug.LogWarning(Format(id, args));
        }
    }
}
