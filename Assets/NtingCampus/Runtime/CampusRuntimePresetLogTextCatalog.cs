using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampusMapEditor
{
    internal enum CampusRuntimePresetLogTextId
    {
        FailedToLoadAreaPresets = 0,
        FailedToLoadGameplayActorPresets = 1,
        FailedToLoadGameplayMarkerPresets = 2,
        FailedToReadPresetFile = 3
    }

    internal static class CampusRuntimePresetLogTextCatalog
    {
        private static readonly Dictionary<CampusRuntimePresetLogTextId, Entry> Entries = new()
        {
            { CampusRuntimePresetLogTextId.FailedToLoadAreaPresets, new Entry("[CampusRuntimeAreaPresetCatalog] 加载 AreaPresets.json 失败：{0}", "[CampusRuntimeAreaPresetCatalog] Failed to load AreaPresets.json: {0}") },
            { CampusRuntimePresetLogTextId.FailedToLoadGameplayActorPresets, new Entry("[CampusRuntimeGameplayActorPresetCatalog] 加载 GameplayActorPresets.json 失败：{0}", "[CampusRuntimeGameplayActorPresetCatalog] Failed to load GameplayActorPresets.json: {0}") },
            { CampusRuntimePresetLogTextId.FailedToLoadGameplayMarkerPresets, new Entry("[CampusRuntimeGameplayMarkerPresetCatalog] 加载 GameplayMarkerPresets.json 失败：{0}", "[CampusRuntimeGameplayMarkerPresetCatalog] Failed to load GameplayMarkerPresets.json: {0}") },
            { CampusRuntimePresetLogTextId.FailedToReadPresetFile, new Entry("[NtingCampusRuntimeModPresetStore] 读取预设文件 {0} 失败：{1}", "[NtingCampusRuntimeModPresetStore] Failed to read preset file '{0}': {1}") }
        };

        public static string Format(CampusRuntimePresetLogTextId id, params object[] args)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return string.Format(entry.Get(CampusLanguageState.CurrentLanguage), args);
        }

        public static void Warning(CampusRuntimePresetLogTextId id, params object[] args)
        {
            Debug.LogWarning(Format(id, args));
        }
    }
}
