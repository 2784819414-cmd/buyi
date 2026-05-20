using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Inventory
{
    public enum CampusInspectionTextId
    {
        WaitingForActivity = 0,
        NoAuthorityInspectorInRange = 1,
        SearchReadyNoQuestioningNpc = 2,
        ForcedQuestioning = 3,
        DebugInspector = 4,
        ForcedSearchFound = 5,
        ForcedSearchNoContraband = 6,
        StorageMemoryUnavailable = 7,
        DebugContrabandSeedSource = 8,
        SeededCarriedContraband = 9,
        InspectionDebugLogLine = 10,
        OpenYourBagLine = 11,
        ShowCarryingLine = 12,
        TattletaleContrabandLine = 13,
        TattletaleSuspiciousLine = 14,
        SearchFoundNothing = 15,
        InspectionLogLine = 16,
        ConfiscationFailed = 17,
        QuestioningSummary = 18,
        ContrabandFoundSummary = 19,
        UnknownRoom = 20,
        MissingItem = 21,
        MissingSourceContainer = 22,
        CouldNotCreateConfiscatedContainer = 23,
        NoEvidenceStorageSpace = 24,
        ConfiscatedToContainer = 25,
        ReportedForContraband = 26,
        ReportedAsSuspicious = 27,
        DebugContrabandDisplayName = 28,
        DebugContrabandDescription = 29,
        ServicesNotInitialized = 30,
        TargetRuntimeUnavailable = 31,
        TargetNotInRoom = 32,
        Ready = 33,
        ItemFallback = 34,
        DebugPanelTitle = 35,
        ServiceMissing = 36,
        Actions = 37,
        SeedContraband = 38,
        ForceQuestioning = 39,
        ForceSearch = 40,
        LastAction = 41,
        Status = 42,
        Room = 43,
        AreaPressure = 44,
        SearchInspector = 45,
        Questioner = 46,
        HighestVigilanceNpc = 47,
        FinalChance = 48,
        Cooldown = 49,
        CarriedContraband = 50,
        ConfiscatedEvidence = 51,
        None = 52,
        MissingAction = 53,
        ActionOk = 54,
        ActionFailed = 55,
        Search = 56,
        Question = 57,
        InContainer = 58,
        InitialDebugHint = 59,
        DailyCountersReset = 60
    }

    public static class CampusInspectionTextCatalog
    {
        private readonly struct Entry
        {
            public Entry(string chinese, string english)
            {
                Chinese = chinese;
                English = english;
            }

            public string Chinese { get; }
            public string English { get; }
        }

        private static readonly Dictionary<CampusInspectionTextId, Entry> Entries =
            new Dictionary<CampusInspectionTextId, Entry>
            {
                { CampusInspectionTextId.WaitingForActivity, new Entry("巡查系统等待事件。", "Inspection system waiting for activity.") },
                { CampusInspectionTextId.NoAuthorityInspectorInRange, new Entry("附近没有有权搜查的巡查者。", "No authority inspector in range.") },
                { CampusInspectionTextId.SearchReadyNoQuestioningNpc, new Entry("可搜查，但附近没有可盘问的 NPC。", "Search ready, no questioning NPC in range.") },
                { CampusInspectionTextId.ForcedQuestioning, new Entry("强制盘问由 {0} 执行。压力={1}。", "Forced questioning by {0}. Pressure={1}.") },
                { CampusInspectionTextId.DebugInspector, new Entry("调试巡查者", "debug inspector") },
                { CampusInspectionTextId.ForcedSearchFound, new Entry("强制搜查发现并没收了 {0}。", "Forced search found and confiscated {0}.") },
                { CampusInspectionTextId.ForcedSearchNoContraband, new Entry("强制搜查没有发现违禁品。", "Forced search found no contraband.") },
                { CampusInspectionTextId.StorageMemoryUnavailable, new Entry("储物内存不可用。", "Storage memory is unavailable.") },
                { CampusInspectionTextId.DebugContrabandSeedSource, new Entry("调试违禁品投放", "Debug contraband seed") },
                { CampusInspectionTextId.SeededCarriedContraband, new Entry("已投放随身违禁品：{0}。", "Seeded carried contraband: {0}.") },
                { CampusInspectionTextId.InspectionDebugLogLine, new Entry("[巡查调试] {0}", "[InspectionDebug] {0}") },
                { CampusInspectionTextId.OpenYourBagLine, new Entry("把包打开。", "Open your bag.") },
                { CampusInspectionTextId.ShowCarryingLine, new Entry("让我看看你带了什么。", "Show me what you are carrying.") },
                { CampusInspectionTextId.TattletaleContrabandLine, new Entry("老师，查一下这个包。", "Teacher, check the bag.") },
                { CampusInspectionTextId.TattletaleSuspiciousLine, new Entry("老师，这里有点不对劲。", "Teacher, something is off.") },
                { CampusInspectionTextId.SearchFoundNothing, new Entry("搜查没有发现物品。压力={0}。", "Search found nothing. Pressure={0}.") },
                { CampusInspectionTextId.InspectionLogLine, new Entry("[巡查] {0}", "[Inspection] {0}") },
                { CampusInspectionTextId.ConfiscationFailed, new Entry("违禁品没收失败：{0}", "Contraband confiscation failed: {0}") },
                { CampusInspectionTextId.QuestioningSummary, new Entry("{0} 盘问了 {1}。压力={2}。发现={3}。", "{0} questioned {1}. Pressure={2}. Found={3}.") },
                { CampusInspectionTextId.ContrabandFoundSummary, new Entry("发现违禁品：{0}，地点={1}。压力={2}。", "Contraband found: {0} in {1}. Pressure={2}.") },
                { CampusInspectionTextId.UnknownRoom, new Entry("未知区域", "unknown") },
                { CampusInspectionTextId.MissingItem, new Entry("缺少物品。", "Missing item.") },
                { CampusInspectionTextId.MissingSourceContainer, new Entry("缺少来源容器。", "Missing source container.") },
                { CampusInspectionTextId.CouldNotCreateConfiscatedContainer, new Entry("无法创建没收物品容器。", "Could not create confiscated item container.") },
                { CampusInspectionTextId.NoEvidenceStorageSpace, new Entry("证物储物空间不足。", "No evidence storage space.") },
                { CampusInspectionTextId.ConfiscatedToContainer, new Entry("已没收 {0} 到 {1}。", "Confiscated {0} to {1}.") },
                { CampusInspectionTextId.ReportedForContraband, new Entry("{0} 举报 {1} 疑似携带违禁品。区域={2}。", "{0} reported {1} for suspected contraband. Room={2}.") },
                { CampusInspectionTextId.ReportedAsSuspicious, new Entry("{0} 举报 {1} 行迹可疑。区域={2}。", "{0} reported {1} as suspicious. Room={2}.") },
                { CampusInspectionTextId.DebugContrabandDisplayName, new Entry("调试违禁品", "Debug Contraband") },
                { CampusInspectionTextId.DebugContrabandDescription, new Entry("运行时投放的巡查证物。", "Runtime seeded inspection evidence.") },
                { CampusInspectionTextId.ServicesNotInitialized, new Entry("巡查服务尚未初始化。", "Inspection services are not initialized.") },
                { CampusInspectionTextId.TargetRuntimeUnavailable, new Entry("巡查目标运行时不可用。", "Inspection target runtime is unavailable.") },
                { CampusInspectionTextId.TargetNotInRoom, new Entry("巡查目标不在玩法区域内。", "Inspection target is not inside a gameplay room.") },
                { CampusInspectionTextId.Ready, new Entry("就绪。", "Ready.") },
                { CampusInspectionTextId.ItemFallback, new Entry("物品", "item") },
                { CampusInspectionTextId.DebugPanelTitle, new Entry("巡查调试", "Inspection Debug") },
                { CampusInspectionTextId.ServiceMissing, new Entry("巡查服务：无", "InspectionService: none") },
                { CampusInspectionTextId.Actions, new Entry("动作", "Actions") },
                { CampusInspectionTextId.SeedContraband, new Entry("投放违禁品", "Seed Contraband") },
                { CampusInspectionTextId.ForceQuestioning, new Entry("强制盘问", "Force Questioning") },
                { CampusInspectionTextId.ForceSearch, new Entry("强制搜查", "Force Search") },
                { CampusInspectionTextId.LastAction, new Entry("上次动作", "Last Action") },
                { CampusInspectionTextId.Status, new Entry("状态", "Status") },
                { CampusInspectionTextId.Room, new Entry("区域", "Room") },
                { CampusInspectionTextId.AreaPressure, new Entry("区域压力", "Area Pressure") },
                { CampusInspectionTextId.SearchInspector, new Entry("搜查者", "Search Inspector") },
                { CampusInspectionTextId.Questioner, new Entry("盘问者", "Questioner") },
                { CampusInspectionTextId.HighestVigilanceNpc, new Entry("最高警觉 NPC", "Highest Vigilance NPC") },
                { CampusInspectionTextId.FinalChance, new Entry("最终概率", "Final Chance") },
                { CampusInspectionTextId.Cooldown, new Entry("冷却", "Cooldown") },
                { CampusInspectionTextId.CarriedContraband, new Entry("随身违禁品", "Carried Contraband") },
                { CampusInspectionTextId.ConfiscatedEvidence, new Entry("已没收证物", "Confiscated Evidence") },
                { CampusInspectionTextId.None, new Entry("无", "none") },
                { CampusInspectionTextId.MissingAction, new Entry("缺少动作。", "Missing action.") },
                { CampusInspectionTextId.ActionOk, new Entry("成功：{0}", "OK: {0}") },
                { CampusInspectionTextId.ActionFailed, new Entry("失败：{0}", "Failed: {0}") },
                { CampusInspectionTextId.Search, new Entry("搜查", "Search") },
                { CampusInspectionTextId.Question, new Entry("盘问", "Question") },
                { CampusInspectionTextId.InContainer, new Entry("{0} 位于 {1}", "{0} in {1}") },
                { CampusInspectionTextId.InitialDebugHint, new Entry("按 F11 切换巡查调试。", "Press F11 to toggle inspection debug.") },
                { CampusInspectionTextId.DailyCountersReset, new Entry("新的一天已重置巡查计数。", "Inspection counters reset for the new day.") }
            };

        public static string Get(CampusInspectionTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusInspectionTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return language switch
            {
                CampusDisplayLanguage.English => entry.English,
                CampusDisplayLanguage.Bilingual => entry.Chinese + " / " + entry.English,
                _ => entry.Chinese
            };
        }

        public static string Format(CampusInspectionTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static CampusLocalizedText Localized(CampusInspectionTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return new CampusLocalizedText(entry.Chinese, entry.English);
        }

        public static string FormatLine(CampusInspectionTextId label, string value)
        {
            return Get(label) + ": " + (string.IsNullOrWhiteSpace(value) ? "-" : value);
        }
    }
}
