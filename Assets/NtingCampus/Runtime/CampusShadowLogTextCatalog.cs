using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampusMapEditor
{
    internal enum CampusShadowLogTextId
    {
        WallGroundCasterSortingLayerUnavailable = 0,
        ConfigureShadowCasterFailed = 1,
        AssignSortingLayersFailed = 2,
        AssignShapeFailed = 3
    }

    internal static class CampusShadowLogTextCatalog
    {
        private static readonly Dictionary<CampusShadowLogTextId, Entry> Entries = new()
        {
            { CampusShadowLogTextId.WallGroundCasterSortingLayerUnavailable, new Entry("[NtingCampus] 已禁用 {0} 的 wall ground ShadowCaster2D，因为无法配置 URP sorting-layer 字段。", "[NtingCampus] Disabled wall ground ShadowCaster2D on '{0}' because the URP sorting-layer field could not be configured.") },
            { CampusShadowLogTextId.ConfigureShadowCasterFailed, new Entry("[NtingCampus] 配置 {0} 的 ShadowCaster2D 失败：{1}", "[NtingCampus] Failed to configure ShadowCaster2D on '{0}': {1}") },
            { CampusShadowLogTextId.AssignSortingLayersFailed, new Entry("[NtingCampus] 设置 {0} 的 ShadowCaster2D sorting layers 失败：{1}", "[NtingCampus] Failed to assign ShadowCaster2D sorting layers on '{0}': {1}") },
            { CampusShadowLogTextId.AssignShapeFailed, new Entry("[NtingCampus] 设置 {0} 的 ShadowCaster2D shape 失败：{1}", "[NtingCampus] Failed to assign ShadowCaster2D shape on '{0}': {1}") }
        };

        public static void Warning(CampusShadowLogTextId id, params object[] args)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            Debug.LogWarning(string.Format(entry.Get(CampusLanguageState.CurrentLanguage), args));
        }
    }
}
