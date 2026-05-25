using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.Gameplay.Rooms
{
    public enum CampusFacilityValidationTextId
    {
        LegacyInference = 0,
        StorageFallback = 1,
        MissingTypeId = 2,
        UnknownTypeId = 3,
        UnknownTypeSource = 4
    }

    public static class CampusFacilityValidationTextCatalog
    {
        private static readonly Dictionary<CampusFacilityValidationTextId, Entry> Entries = new()
        {
            { CampusFacilityValidationTextId.LegacyInference, new Entry("设施类型 {0} 来自旧版物体名或显示名推断。请给源物体设置显式 TypeId，或添加 CampusGameplayFacilityMarker。{1}", "Facility type {0} came from legacy object or display-name inference. Set an explicit TypeId on the source object or add a CampusGameplayFacilityMarker. {1}") },
            { CampusFacilityValidationTextId.StorageFallback, new Entry("设施类型 Storage 来自储物容器 fallback。正常玩法数据应设置显式 TypeId=Storage。{0}", "Facility type Storage came from the storage-container fallback. Normal gameplay data should set explicit TypeId=Storage. {0}") },
            { CampusFacilityValidationTextId.MissingTypeId, new Entry("设施缺少显式 TypeId，当前不能作为明确玩法设施。{0}", "Facility is missing an explicit TypeId and cannot act as a clear gameplay facility. {0}") },
            { CampusFacilityValidationTextId.UnknownTypeId, new Entry("设施 TypeId 无法解析。请使用 CampusFacilityType 枚举值或 FacilityRules TypeIds。{0}", "Facility TypeId could not be resolved. Use a CampusFacilityType enum value or a FacilityRules TypeIds entry. {0}") },
            { CampusFacilityValidationTextId.UnknownTypeSource, new Entry("设施类型来源未知。请设置显式 TypeId 或 CampusGameplayFacilityMarker。", "Facility type source is unknown. Set an explicit TypeId or CampusGameplayFacilityMarker.") }
        };

        public static string Format(CampusFacilityValidationTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Get(CampusFacilityValidationTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }
    }
}
