using System.Collections.Generic;

namespace Nting.Storage
{
    public interface IStorageItemUseAction
    {
        string ActionId { get; }

        bool CanUse(StorageItemModel item, StorageGridUI sourceGrid, out string reason);

        bool TryUse(StorageItemModel item, StorageGridUI sourceGrid, out string statusMessage);
    }

    public static class StorageItemUseActionRegistry
    {
        private static readonly Dictionary<string, IStorageItemUseAction> Actions =
            new Dictionary<string, IStorageItemUseAction>(System.StringComparer.OrdinalIgnoreCase);

        public static void Register(IStorageItemUseAction action)
        {
            if (action != null && !string.IsNullOrWhiteSpace(action.ActionId))
            {
                Actions[action.ActionId.Trim()] = action;
            }
        }

        public static bool TryGet(string actionId, out IStorageItemUseAction action)
        {
            action = null;
            return !string.IsNullOrWhiteSpace(actionId) &&
                   Actions.TryGetValue(actionId.Trim(), out action) &&
                   action != null;
        }
    }
}
