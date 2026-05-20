using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcEcologyTextId
    {
        WaitingForEvents = 0,
        SegmentPulse = 1,
        DailyRecovery = 2,
        PrankAttemptNoticed = 3,
        DetectedPrankRisk = 4,
        UndetectedPrankShift = 5,
        SanctionReacted = 6,
        ClassroomDozingReacted = 7,
        TeacherDistractionChangedMood = 8,
        DetectedClassSkipping = 9,
        EscapedClassSkipping = 10,
        ProtectedItemMoveFact = 11,
        WitnessedTheftEscalated = 12,
        ContrabandQuestioning = 13,
        BagQuestioning = 14,
        ContrabandFoundEscalated = 15,
        EventLogLine = 16,
        VisibleStolenItemLog = 17,
        ItemFallback = 18
    }

    public static class CampusNpcEcologyTextCatalog
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

        private static readonly Dictionary<CampusNpcEcologyTextId, Entry> Entries =
            new Dictionary<CampusNpcEcologyTextId, Entry>
            {
                { CampusNpcEcologyTextId.WaitingForEvents, new Entry("NPC 生态正在等待事件。", "NPC ecology waiting for events.") },
                { CampusNpcEcologyTextId.SegmentPulse, new Entry("时段生态已更新：{0}。", "Segment ecology pulse: {0}.") },
                { CampusNpcEcologyTextId.DailyRecovery, new Entry("每日 NPC 生态恢复已应用。", "Daily ecology recovery applied.") },
                { CampusNpcEcologyTextId.PrankAttemptNoticed, new Entry("NPC 生态注意到角色尝试恶作剧。", "NPC ecology noticed an actor prank attempt.") },
                { CampusNpcEcologyTextId.DetectedPrankRisk, new Entry("NPC 生态在恶作剧被发现后将角色标记为高风险。", "NPC ecology marked the actor as risky after a detected prank.") },
                { CampusNpcEcologyTextId.UndetectedPrankShift, new Entry("NPC 生态因未被发现的恶作剧发生变化。", "NPC ecology shifted after an undetected actor prank.") },
                { CampusNpcEcologyTextId.SanctionReacted, new Entry("NPC 生态响应了角色处分。", "NPC ecology reacted to an actor sanction.") },
                { CampusNpcEcologyTextId.ClassroomDozingReacted, new Entry("NPC 生态响应了课堂打瞌睡。", "NPC ecology reacted to classroom dozing.") },
                { CampusNpcEcologyTextId.TeacherDistractionChangedMood, new Entry("老师分心改变了教室气氛。", "Teacher distraction changed classroom mood.") },
                { CampusNpcEcologyTextId.DetectedClassSkipping, new Entry("NPC 生态响应了被发现的逃课。", "NPC ecology reacted to detected class skipping.") },
                { CampusNpcEcologyTextId.EscapedClassSkipping, new Entry("NPC 生态响应了成功逃课。", "NPC ecology reacted to escaped class skipping.") },
                { CampusNpcEcologyTextId.ProtectedItemMoveFact, new Entry("NPC 生态记录了受保护物品移动事实。", "NPC ecology recorded a protected item move fact.") },
                { CampusNpcEcologyTextId.WitnessedTheftEscalated, new Entry("NPC 生态因目击物品偷窃而升级。", "NPC ecology escalated after a witnessed item theft.") },
                { CampusNpcEcologyTextId.ContrabandQuestioning, new Entry("NPC 生态响应了违禁品盘问。", "NPC ecology reacted to contraband questioning.") },
                { CampusNpcEcologyTextId.BagQuestioning, new Entry("NPC 生态注意到一次查包盘问。", "NPC ecology noticed a bag questioning.") },
                { CampusNpcEcologyTextId.ContrabandFoundEscalated, new Entry("NPC 生态因发现违禁品而升级。", "NPC ecology escalated after contraband was found.") },
                { CampusNpcEcologyTextId.EventLogLine, new Entry("[NPC] {0} 传闻热度={1}。", "[NPC] {0} Gossip={1}.") },
                { CampusNpcEcologyTextId.VisibleStolenItemLog, new Entry("[物品] {0} 注意到可见赃物：{1}。", "[Inventory] {0} noticed visible stolen item: {1}.") },
                { CampusNpcEcologyTextId.ItemFallback, new Entry("物品", "item") }
            };

        public static string Get(CampusNpcEcologyTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusNpcEcologyTextId id)
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

        public static string Format(CampusNpcEcologyTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}
