using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampusMapEditor
{
    public enum CampusInteractionTextId
    {
        Interact = 0,
        InteractWith = 1,
        OpenDoor = 2,
        CloseDoor = 3,
        OpenObject = 4,
        PickupItem = 5,
        UnknownActor = 6,
        InteractedWithLog = 7,
        DroppedItem = 8,
        SitDown = 9,
        SitDownObjectLog = 10
    }

    public static class CampusInteractionTextCatalog
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

        private static readonly Dictionary<CampusInteractionTextId, Entry> Entries =
            new Dictionary<CampusInteractionTextId, Entry>
            {
                { CampusInteractionTextId.Interact, new Entry("交互", "Interact") },
                { CampusInteractionTextId.InteractWith, new Entry("交互 {0}", "Interact {0}") },
                { CampusInteractionTextId.OpenDoor, new Entry("开门", "Open Door") },
                { CampusInteractionTextId.CloseDoor, new Entry("关门", "Close Door") },
                { CampusInteractionTextId.OpenObject, new Entry("打开 {0}", "Open {0}") },
                { CampusInteractionTextId.PickupItem, new Entry("拾取 {0}", "Pick up {0}") },
                { CampusInteractionTextId.UnknownActor, new Entry("未知角色", "Unknown Actor") },
                { CampusInteractionTextId.InteractedWithLog, new Entry("{0} 与 {1} 交互。", "{0} interacted with {1}.") },
                { CampusInteractionTextId.DroppedItem, new Entry("掉落物", "Dropped Item") },
                { CampusInteractionTextId.SitDown, new Entry("坐下", "Sit") },
                { CampusInteractionTextId.SitDownObjectLog, new Entry("坐下 {0}", "Sat at {0}") }
            };

        public static string Get(CampusInteractionTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusInteractionTextId id)
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

        public static string Format(CampusInteractionTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}
