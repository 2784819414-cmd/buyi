using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcSpeechTextId
    {
        GenericScheduledBusy = 0,
        GenericHeadingToTask = 1,
        GenericHeadingOut = 2
    }

    public static class CampusNpcSpeechTextCatalog
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

        private static readonly Dictionary<CampusNpcSpeechTextId, Entry> Entries = new()
        {
            { CampusNpcSpeechTextId.GenericScheduledBusy, new Entry("我现在有安排。", "I have something scheduled right now.") },
            { CampusNpcSpeechTextId.GenericHeadingToTask, new Entry("我先去做当前的事。", "I am heading to the current task.") },
            { CampusNpcSpeechTextId.GenericHeadingOut, new Entry("我先过去。", "I am heading over.") }
        };

        public static string Get(CampusNpcSpeechTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return Resolve(CampusLanguageState.CurrentLanguage, entry.Chinese, entry.English);
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
