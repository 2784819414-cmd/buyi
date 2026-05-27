using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampusMapEditor
{
    internal enum CampusAiMapAuthoringTextId
    {
        ImportMenuTitle,
        ImportSuccessTitle,
        ImportFailedTitle,
        SelectJsonTitle,
        JsonExtension,
        DialogOk,
        GenerateCatalogSummary,
        ValidationErrorPrefix,
        ValidationNoMap,
        ValidationMapIdMissing,
        ValidationNoFloors,
        ValidationFloorIndexInvalid,
        ValidationRoomIdMissing,
        ValidationRoomSizeInvalid,
        ValidationTileIdMissing,
        ValidationTileIdUnknown,
        ValidationObjectIdMissing,
        ValidationObjectIdUnknown,
        ValidationStairTargetInvalid,
        ValidationStairCellMissing,
        AppliedSummary
    }

    internal static class CampusAiMapAuthoringTextCatalog
    {
        private static readonly Dictionary<CampusAiMapAuthoringTextId, Entry> Entries = new()
        {
            { CampusAiMapAuthoringTextId.ImportMenuTitle, new Entry("导入 AI 地图 JSON", "Import AI Map JSON") },
            { CampusAiMapAuthoringTextId.ImportSuccessTitle, new Entry("AI 地图导入完成", "AI map import completed") },
            { CampusAiMapAuthoringTextId.ImportFailedTitle, new Entry("AI 地图导入失败", "AI map import failed") },
            { CampusAiMapAuthoringTextId.SelectJsonTitle, new Entry("选择 AI 地图 JSON", "Select AI map JSON") },
            { CampusAiMapAuthoringTextId.JsonExtension, new Entry("json", "json") },
            { CampusAiMapAuthoringTextId.DialogOk, new Entry("确定", "OK") },
            { CampusAiMapAuthoringTextId.GenerateCatalogSummary, new Entry("AI 地图资源目录已生成：地面 {0}，墙体 {1}，物体 {2}。", "AI map asset catalog generated: {0} floor tile(s), {1} wall tile(s), {2} object(s).") },
            { CampusAiMapAuthoringTextId.ValidationErrorPrefix, new Entry("AI 地图校验错误：", "AI map validation error: ") },
            { CampusAiMapAuthoringTextId.ValidationNoMap, new Entry("地图文档为空。", "Map document is empty.") },
            { CampusAiMapAuthoringTextId.ValidationMapIdMissing, new Entry("MapId 不能为空。", "MapId is required.") },
            { CampusAiMapAuthoringTextId.ValidationNoFloors, new Entry("至少需要一个楼层。", "At least one floor is required.") },
            { CampusAiMapAuthoringTextId.ValidationFloorIndexInvalid, new Entry("楼层索引必须大于 0：{0}", "Floor index must be greater than 0: {0}") },
            { CampusAiMapAuthoringTextId.ValidationRoomIdMissing, new Entry("房间 ID 不能为空：楼层 {0}", "Room id is required on floor {0}.") },
            { CampusAiMapAuthoringTextId.ValidationRoomSizeInvalid, new Entry("房间尺寸必须大于 0：楼层 {0}，房间 {1}", "Room size must be greater than 0 on floor {0}, room {1}.") },
            { CampusAiMapAuthoringTextId.ValidationTileIdMissing, new Entry("瓦片 ID 不能为空：楼层 {0}，{1}", "Tile id is required on floor {0}, {1}.") },
            { CampusAiMapAuthoringTextId.ValidationTileIdUnknown, new Entry("未知瓦片 ID：楼层 {0}，{1}，{2}", "Unknown tile id on floor {0}, {1}: {2}") },
            { CampusAiMapAuthoringTextId.ValidationObjectIdMissing, new Entry("物体 ID 不能为空：楼层 {0}", "Object id is required on floor {0}.") },
            { CampusAiMapAuthoringTextId.ValidationObjectIdUnknown, new Entry("未知物体 ID：楼层 {0}，{1}", "Unknown object id on floor {0}: {1}") },
            { CampusAiMapAuthoringTextId.ValidationStairTargetInvalid, new Entry("楼梯目标楼层必须大于 0 且不能等于当前楼层：楼层 {0}", "Stair target floor must be greater than 0 and differ from the current floor: {0}.") },
            { CampusAiMapAuthoringTextId.ValidationStairCellMissing, new Entry("楼梯起点格子不能为空：楼层 {0}", "Stair source cell is required on floor {0}.") },
            { CampusAiMapAuthoringTextId.AppliedSummary, new Entry("已导入地图 {0}，楼层 {1} 个。", "Imported map {0} with {1} floor(s).") }
        };

        public static string Get(CampusAiMapAuthoringTextId id)
        {
            return Entries.TryGetValue(id, out Entry entry) ? entry.Get() : id.ToString();
        }

        public static string Format(CampusAiMapAuthoringTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        private readonly struct Entry
        {
            private readonly string zh;
            private readonly string en;

            public Entry(string zh, string en)
            {
                this.zh = zh;
                this.en = en;
            }

            public string Get()
            {
                return CampusLanguageState.CurrentLanguage == CampusDisplayLanguage.English ? en : zh;
            }
        }
    }
}
