namespace Nting.Storage
{
    public readonly struct StorageItemPosition
    {
        public StorageItemPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }

    public static class StorageTransferService
    {
        public static bool TryMove(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            out StorageItemPosition previousPosition,
            out string errorMessage)
        {
            previousPosition = item != null ? new StorageItemPosition(item.X, item.Y) : default;
            errorMessage = string.Empty;

            if (item == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.MissingItem);
                return false;
            }

            if (target == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.MissingTargetContainer);
                return false;
            }

            source = source ?? item.CurrentContainer;
            if (source == target)
            {
                if (target.PlaceItem(item, x, y))
                {
                    return true;
                }

                errorMessage = StorageTextCatalog.Get(StorageTextId.TargetBlocked);
                return false;
            }

            if (!target.CanPlace(item, x, y))
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.TargetBlocked);
                return false;
            }

            if (source != null && !source.RemoveItem(item))
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.CouldNotRemoveFromSource);
                return false;
            }

            if (target.PlaceItem(item, x, y))
            {
                return true;
            }

            Restore(item, source, previousPosition);
            errorMessage = StorageTextCatalog.Get(StorageTextId.TargetBlocked);
            return false;
        }

        public static bool TryRemove(
            StorageItemModel item,
            StorageContainerModel source,
            out StorageItemPosition previousPosition,
            out string errorMessage)
        {
            previousPosition = item != null ? new StorageItemPosition(item.X, item.Y) : default;
            errorMessage = string.Empty;
            source = source ?? item?.CurrentContainer;
            if (item == null || source == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.MissingItemOrSource);
                return false;
            }

            if (source.RemoveItem(item))
            {
                return true;
            }

            errorMessage = StorageTextCatalog.Get(StorageTextId.CouldNotRemoveFromSource);
            return false;
        }

        public static void Restore(
            StorageItemModel item,
            StorageContainerModel source,
            StorageItemPosition previousPosition)
        {
            if (item != null && source != null)
            {
                source.PlaceItem(item, previousPosition.X, previousPosition.Y);
            }
        }
    }
}
