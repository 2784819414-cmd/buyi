using UnityEngine;

namespace Nting.Storage
{
    public static class StorageItemUseUtility
    {
        public const string ConsumeFoodActionId = "campus.item.consume_food";

        public static bool CanUse(StorageItemModel item)
        {
            return item != null &&
                   item.IsUsable &&
                   StorageItemUseActionRegistry.TryGet(item.UseActionId, out _);
        }

        public static bool TryUse(StorageItemModel item, StorageGridUI sourceGrid, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (item == null || !item.IsUsable)
            {
                statusMessage = StorageTextCatalog.Get(StorageTextId.ItemCannotBeUsed);
                return false;
            }

            if (!StorageItemUseActionRegistry.TryGet(item.UseActionId, out IStorageItemUseAction action))
            {
                statusMessage = StorageTextCatalog.Format(StorageTextId.UnsupportedUseAction, item.UseActionId);
                return false;
            }

            bool used = action.TryUse(item, sourceGrid, out statusMessage);
            if (used && !string.IsNullOrWhiteSpace(statusMessage))
            {
                Debug.Log(StorageTextCatalog.Format(StorageTextId.StatusLog, statusMessage));
            }

            return used;
        }
    }
}
