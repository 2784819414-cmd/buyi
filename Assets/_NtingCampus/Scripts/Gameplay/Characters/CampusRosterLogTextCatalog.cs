using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.Gameplay.Characters
{
    internal enum CampusRosterLogTextId
    {
        MissingPlayerControlledRuntime = 0,
        MultiplePlayerControlledRuntimes = 1
    }

    internal static class CampusRosterLogTextCatalog
    {
        private const string Prefix = "[CampusRosterService] ";

        private static readonly Dictionary<CampusRosterLogTextId, Entry> Entries = new()
        {
            { CampusRosterLogTextId.MissingPlayerControlledRuntime, new Entry("没有找到玩家控制的场景角色。", "No player-controlled scene character was found.") },
            { CampusRosterLogTextId.MultiplePlayerControlledRuntimes, new Entry("发现多个玩家控制的角色运行时。保留 {0}，忽略 {1}。", "Multiple player-controlled runtimes were found. Keeping {0} and ignoring {1}.") }
        };

        public static string Format(CampusRosterLogTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Get(CampusRosterLogTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }

        public static void Warning(CampusRosterLogTextId id, params object[] args)
        {
            Debug.LogWarning(Prefix + Format(id, args));
        }
    }
}
