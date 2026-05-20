using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Core
{
    public enum CampusCoreTextId
    {
        GameplayBootstrapInitialized = 0,
        EmptyEvent = 1,
        SwitchedStudentBodyMode = 2,
        SwitchedGodViewMode = 3,
        EventRecorded = 4
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

        private static readonly Dictionary<CampusCoreTextId, Entry> Entries = new Dictionary<CampusCoreTextId, Entry>
        {
            { CampusCoreTextId.GameplayBootstrapInitialized, new Entry("[系统] {0} 玩法启动完成。金钱={1}，神力={2}，天数={3}，秩序={4}，混乱={5}。", "[System] {0} gameplay bootstrap initialized. Money={1}, DivinePower={2}, Day={3}, Order={4}, Chaos={5}.") },
            { CampusCoreTextId.EmptyEvent, new Entry("（空事件）", "(empty event)") },
            { CampusCoreTextId.SwitchedStudentBodyMode, new Entry("[系统] 已切换到学生身体模式。", "[System] Switched to student body mode.") },
            { CampusCoreTextId.SwitchedGodViewMode, new Entry("[系统] 已切换到神视角模式。", "[System] Switched to god view mode.") },
            { CampusCoreTextId.EventRecorded, new Entry("[事件] {0}", "[Event] {0}") }
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

            return language switch
            {
                CampusDisplayLanguage.English => entry.English,
                CampusDisplayLanguage.Bilingual => entry.Chinese + " / " + entry.English,
                _ => entry.Chinese
            };
        }

        public static string Format(CampusCoreTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}
