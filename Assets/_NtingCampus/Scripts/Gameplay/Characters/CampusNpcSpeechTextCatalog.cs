using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

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

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }
    }
}
