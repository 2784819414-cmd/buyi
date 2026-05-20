using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Core
{
    public enum CampusEcologyOverviewTextId
    {
        WindowTitle = 0,
        WorldState = 1,
        OrderChaos = 2,
        TeacherAlertPlayerSuspicion = 3,
        DailyWarnings = 4,
        PlayerRisk = 5,
        Room = 6,
        CarriedContraband = 7,
        ConfiscatedEvidence = 8,
        HighestNearbyVigilance = 9,
        InspectionEcology = 10,
        Inspection = 11,
        TodayQuestionSearchFoundConfiscated = 12,
        ReportsProactivePatrols = 13,
        HighestRiskArea = 14,
        Current = 15,
        NpcEcology = 16,
        GossipEventsToday = 17,
        MostSuspiciousNpc = 18,
        TheftLoops = 19,
        PrankService = 20,
        PassNotesCanteenDelivery = 21,
        Delivery = 22,
        None = 23,
        SearchShort = 24,
        QuestionShort = 25,
        InContainer = 26
    }

    public static class CampusEcologyOverviewTextCatalog
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

        private static readonly Dictionary<CampusEcologyOverviewTextId, Entry> Entries =
            new Dictionary<CampusEcologyOverviewTextId, Entry>
            {
                { CampusEcologyOverviewTextId.WindowTitle, new Entry("生态总览", "Ecology Overview") },
                { CampusEcologyOverviewTextId.WorldState, new Entry("世界状态", "World State") },
                { CampusEcologyOverviewTextId.OrderChaos, new Entry("秩序 / 混乱", "Order / Chaos") },
                { CampusEcologyOverviewTextId.TeacherAlertPlayerSuspicion, new Entry("教师警觉 / 玩家嫌疑", "Teacher Alert / Player Suspicion") },
                { CampusEcologyOverviewTextId.DailyWarnings, new Entry("每日警告", "Daily Warnings") },
                { CampusEcologyOverviewTextId.PlayerRisk, new Entry("玩家风险", "Player Risk") },
                { CampusEcologyOverviewTextId.Room, new Entry("区域", "Room") },
                { CampusEcologyOverviewTextId.CarriedContraband, new Entry("随身违禁品", "Carried Contraband") },
                { CampusEcologyOverviewTextId.ConfiscatedEvidence, new Entry("已没收证物", "Confiscated Evidence") },
                { CampusEcologyOverviewTextId.HighestNearbyVigilance, new Entry("附近最高警觉", "Highest Nearby Vigilance") },
                { CampusEcologyOverviewTextId.InspectionEcology, new Entry("巡查生态", "Inspection Ecology") },
                { CampusEcologyOverviewTextId.Inspection, new Entry("巡查", "Inspection") },
                { CampusEcologyOverviewTextId.TodayQuestionSearchFoundConfiscated, new Entry("今日盘问/搜查/发现/没收", "Today Q/S/Found/Confiscated") },
                { CampusEcologyOverviewTextId.ReportsProactivePatrols, new Entry("举报 / 主动巡查", "Reports / Proactive Patrols") },
                { CampusEcologyOverviewTextId.HighestRiskArea, new Entry("最高风险区域", "Highest Risk Area") },
                { CampusEcologyOverviewTextId.Current, new Entry("当前", "Current") },
                { CampusEcologyOverviewTextId.NpcEcology, new Entry("NPC 生态", "NPC Ecology") },
                { CampusEcologyOverviewTextId.GossipEventsToday, new Entry("今日传闻 / 生态事件", "Gossip / Events Today") },
                { CampusEcologyOverviewTextId.MostSuspiciousNpc, new Entry("最可疑 NPC", "Most Suspicious NPC") },
                { CampusEcologyOverviewTextId.TheftLoops, new Entry("偷窃闭环", "Theft Loops") },
                { CampusEcologyOverviewTextId.PrankService, new Entry("恶作剧服务", "Prank Service") },
                { CampusEcologyOverviewTextId.PassNotesCanteenDelivery, new Entry("传纸条 / 食堂偷取 / 外卖偷取", "Pass Notes / Canteen Theft / Delivery Theft") },
                { CampusEcologyOverviewTextId.Delivery, new Entry("外卖", "Delivery") },
                { CampusEcologyOverviewTextId.None, new Entry("无", "none") },
                { CampusEcologyOverviewTextId.SearchShort, new Entry("搜查", "S") },
                { CampusEcologyOverviewTextId.QuestionShort, new Entry("盘问", "Q") },
                { CampusEcologyOverviewTextId.InContainer, new Entry("{0} 位于 {1}", "{0} in {1}") }
            };

        public static string Get(CampusEcologyOverviewTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusEcologyOverviewTextId id)
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

        public static string Format(CampusEcologyOverviewTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string FormatLine(CampusEcologyOverviewTextId label, string value)
        {
            return Get(label) + ": " + (string.IsNullOrWhiteSpace(value) ? "-" : value);
        }
    }
}
