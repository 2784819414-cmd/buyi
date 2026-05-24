using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.Core
{
    public enum CampusCoreTextId
    {
        GameplayBootstrapInitialized = 0,
        EmptyEvent = 1,
        SwitchedStudentBodyMode = 2,
        SwitchedGodViewMode = 3,
        EventRecorded = 4,
        TestTimeAdjusted = 5,
        TestTimePaused = 6,
        TestTimeResumed = 7
    }

    public static class CampusCoreTextCatalog
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

        private static readonly Dictionary<CampusCoreTextId, Entry> Entries = new()
        {
            { CampusCoreTextId.GameplayBootstrapInitialized, new Entry("[系统] {0} 玩法启动完成。神力={1}，天数={2}，秩序={3}，混乱={4}。", "[System] {0} gameplay bootstrap initialized. DivinePower={1}, Day={2}, Order={3}, Chaos={4}.") },
            { CampusCoreTextId.EmptyEvent, new Entry("（空事件）", "(empty event)") },
            { CampusCoreTextId.SwitchedStudentBodyMode, new Entry("[系统] 已切换到学生身体模式。", "[System] Switched to student body mode.") },
            { CampusCoreTextId.SwitchedGodViewMode, new Entry("[系统] 已切换到神视角模式。", "[System] Switched to god view mode.") },
            { CampusCoreTextId.EventRecorded, new Entry("[事件] {0}", "[Event] {0}") },
            { CampusCoreTextId.TestTimeAdjusted, new Entry("[测试] 时间已调整为 {0} {1}（{2}）。", "[Test] Time adjusted to {0} {1} ({2}).") },
            { CampusCoreTextId.TestTimePaused, new Entry("[测试] 时间已暂停。", "[Test] Time paused.") },
            { CampusCoreTextId.TestTimeResumed, new Entry("[测试] 时间已恢复为 {0}。", "[Test] Time resumed as {0}.") }
        };

        public static string Get(CampusCoreTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusCoreTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return Resolve(language, entry.Chinese, entry.English);
        }

        public static string Format(CampusCoreTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        private static string Resolve(CampusDisplayLanguage language, string chinese, string english)
        {
            return language switch
            {
                CampusDisplayLanguage.English => english,
                CampusDisplayLanguage.Bilingual => chinese + " / " + english,
                _ => chinese
            };
        }
    }
}
