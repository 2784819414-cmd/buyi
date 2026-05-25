using System;
using System.Collections.Generic;

namespace NtingCampusMapEditor
{
    public static class CampusDroppedStorageItemRegistry
    {
        private static readonly List<CampusDroppedStorageItem> activeItems =
            new List<CampusDroppedStorageItem>(64);

        public static IReadOnlyList<CampusDroppedStorageItem> ActiveItems
        {
            get
            {
                RemoveInvalidEntries();
                return activeItems;
            }
        }

        public static void Register(CampusDroppedStorageItem item)
        {
            if (item == null)
            {
                return;
            }

            RemoveInvalidEntries();
            if (!activeItems.Contains(item))
            {
                activeItems.Add(item);
            }
        }

        public static void Unregister(CampusDroppedStorageItem item)
        {
            if (item == null)
            {
                return;
            }

            activeItems.Remove(item);
        }

        public static bool Contains(Predicate<CampusDroppedStorageItem> predicate)
        {
            if (predicate == null)
            {
                return false;
            }

            IReadOnlyList<CampusDroppedStorageItem> items = ActiveItems;
            for (int i = 0; i < items.Count; i++)
            {
                CampusDroppedStorageItem item = items[i];
                if (item != null && predicate(item))
                {
                    return true;
                }
            }

            return false;
        }

        public static int CountBySourceContainer(string sourceContainerId)
        {
            if (string.IsNullOrWhiteSpace(sourceContainerId))
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<CampusDroppedStorageItem> items = ActiveItems;
            for (int i = 0; i < items.Count; i++)
            {
                CampusDroppedStorageItem item = items[i];
                if (item != null &&
                    string.Equals(item.SourceContainerId, sourceContainerId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        public static void CollectBySourceContainer(
            string sourceContainerId,
            List<CampusDroppedStorageItem> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            if (string.IsNullOrWhiteSpace(sourceContainerId))
            {
                return;
            }

            IReadOnlyList<CampusDroppedStorageItem> items = ActiveItems;
            for (int i = 0; i < items.Count; i++)
            {
                CampusDroppedStorageItem item = items[i];
                if (item != null &&
                    string.Equals(item.SourceContainerId, sourceContainerId, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(item);
                }
            }
        }

        private static void RemoveInvalidEntries()
        {
            for (int i = activeItems.Count - 1; i >= 0; i--)
            {
                CampusDroppedStorageItem item = activeItems[i];
                if (item == null || !item.isActiveAndEnabled)
                {
                    activeItems.RemoveAt(i);
                }
            }
        }
    }
}
