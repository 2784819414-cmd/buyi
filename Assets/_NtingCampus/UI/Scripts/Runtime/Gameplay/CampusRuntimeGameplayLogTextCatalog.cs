using System.Collections.Generic;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.UI.Runtime.Gameplay
{
    internal enum CampusRuntimeGameplayLogTextId
    {
        OverlayLoaderMessage = 0,
        LaunchSelectionApplyFailed = 1
    }

    internal static class CampusRuntimeGameplayLogTextCatalog
    {
        private static readonly Dictionary<CampusRuntimeGameplayLogTextId, Entry> Entries = new()
        {
            { CampusRuntimeGameplayLogTextId.OverlayLoaderMessage, new Entry("[CampusRuntimeGameplayOverlayLoader] {0}", "[CampusRuntimeGameplayOverlayLoader] {0}") },
            { CampusRuntimeGameplayLogTextId.LaunchSelectionApplyFailed, new Entry("[CampusLaunchSelectionApplier] {0}", "[CampusLaunchSelectionApplier] {0}") }
        };

        public static void Log(CampusRuntimeGameplayLogTextId id, params object[] args)
        {
            Debug.Log(Format(id, args));
        }

        public static void Warning(CampusRuntimeGameplayLogTextId id, params object[] args)
        {
            Debug.LogWarning(Format(id, args));
        }

        private static string Format(CampusRuntimeGameplayLogTextId id, params object[] args)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return string.Format(entry.Get(CampusLanguageState.CurrentLanguage), args);
        }
    }
}
