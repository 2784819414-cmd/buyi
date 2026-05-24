using UnityEngine;

namespace Nting.Storage
{
    public interface IStorageTransferHandler
    {
        bool TryMoveItem(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            StorageTransferContext context,
            out StorageTransferResult result);

        bool TryMoveToFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result);

        bool TryDropItemToGround(
            GameObject dropContext,
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result);
    }

    public sealed class StorageDefaultTransferHandler : IStorageTransferHandler
    {
        public static readonly StorageDefaultTransferHandler Instance = new StorageDefaultTransferHandler();

        private StorageDefaultTransferHandler()
        {
        }

        public bool TryMoveItem(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (StorageTransferService.TryMove(
                    item,
                    source,
                    target,
                    x,
                    y,
                    out _,
                    out string errorMessage))
            {
                result = new StorageTransferResult(
                    true,
                    false,
                    false,
                    StorageTextCatalog.Format(StorageTextId.MovedItem, ResolveItemName(item)),
                    string.Empty);
                return true;
            }

            result = StorageTransferResult.Fail(errorMessage);
            return false;
        }

        public bool TryMoveToFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (item == null || targets == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.TargetBlocked));
                return false;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                StorageContainerModel target = targets[i];
                if (target != null &&
                    target.FindFirstFit(item, out Vector2Int position) &&
                    TryMoveItem(item, source, target, position.x, position.y, context, out result))
                {
                    return true;
                }
            }

            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.TargetBlocked));
            return false;
        }

        public bool TryDropItemToGround(
            GameObject dropContext,
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.GroundDropNotConfigured));
            return false;
        }

        private static string ResolveItemName(StorageItemModel item)
        {
            return item != null ? item.GetDisplayName() : StorageTextCatalog.Get(StorageTextId.ItemFallback);
        }
    }
}
