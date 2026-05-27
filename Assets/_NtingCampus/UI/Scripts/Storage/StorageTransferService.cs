namespace Nting.Storage
{
    using System.Collections.Generic;

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
            if (ShouldMoveWholeStack(source, item))
            {
                return TryMoveStack(
                    item,
                    source,
                    target,
                    x,
                    y,
                    out previousPosition,
                    out errorMessage);
            }

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

        private static bool ShouldMoveWholeStack(StorageContainerModel source, StorageItemModel item)
        {
            return source != null &&
                   item != null &&
                   StorageItemStackingService.IsRepresentative(source, item) &&
                   StorageItemStackingService.GetStackCount(source, item) > 1;
        }

        private static bool TryMoveStack(
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
            if (item == null || source == null || target == null)
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.MissingItemOrSource);
                return false;
            }

            List<StorageItemModel> members = StorageItemStackingService.GetStackMembers(source, item);
            if (members.Count <= 1)
            {
                return false;
            }

            if (source == target)
            {
                if (!CanPlaceStackAt(target, members, item, x, y, source))
                {
                    errorMessage = StorageTextCatalog.Get(StorageTextId.TargetBlocked);
                    return false;
                }

                for (int i = 0; i < members.Count; i++)
                {
                    members[i].X = x;
                    members[i].Y = y;
                }

                return true;
            }

            if (!CanPlaceStackAt(target, members, item, x, y, null))
            {
                errorMessage = StorageTextCatalog.Get(StorageTextId.TargetBlocked);
                return false;
            }

            string sourceStackId = item.StackId;
            string targetStackId = ResolveTargetStackId(target, item, x, y);
            if (string.IsNullOrWhiteSpace(targetStackId))
            {
                targetStackId = sourceStackId;
            }

            for (int i = 0; i < members.Count; i++)
            {
                source.Items.Remove(members[i]);
            }

            for (int i = 0; i < members.Count; i++)
            {
                StorageItemModel member = members[i];
                member.StackId = targetStackId;
                if (!target.PlaceItem(member, x, y))
                {
                    RestoreStack(members, source, sourceStackId, previousPosition);
                    errorMessage = StorageTextCatalog.Get(StorageTextId.TargetBlocked);
                    return false;
                }
            }

            return true;
        }

        private static bool CanPlaceStackAt(
            StorageContainerModel target,
            IReadOnlyList<StorageItemModel> members,
            StorageItemModel representative,
            int x,
            int y,
            StorageContainerModel source)
        {
            if (target == null || members == null || representative == null)
            {
                return false;
            }

            if (source == target)
            {
                return target.CanPlace(representative, x, y, representative);
            }

            if (StorageItemStackingService.TryFindCompatibleStackAt(
                    target,
                    representative,
                    x,
                    y,
                    null,
                    out StorageItemModel targetStack))
            {
                int combinedCount =
                    StorageItemStackingService.GetStackCount(target, targetStack) + members.Count;
                return combinedCount <= StorageItemStackingService.ResolveMaxStackSize(representative) &&
                       CanAcceptStackWeight(target, members);
            }

            return target.CanPlace(representative, x, y) &&
                   CanAcceptStackWeight(target, members);
        }

        private static bool CanAcceptStackWeight(
            StorageContainerModel target,
            IReadOnlyList<StorageItemModel> members)
        {
            if (target == null)
            {
                return false;
            }

            float incomingWeight = StorageItemStackingService.GetStackWeight(members);
            return target.MaxWeight <= 0f ||
                   target.CurrentWeight + incomingWeight <= target.MaxWeight + 0.0001f;
        }

        private static string ResolveTargetStackId(StorageContainerModel target, StorageItemModel item, int x, int y)
        {
            return StorageItemStackingService.TryFindCompatibleStackAt(target, item, x, y, null, out StorageItemModel targetStack)
                ? targetStack.StackId
                : string.Empty;
        }

        private static void RestoreStack(
            IReadOnlyList<StorageItemModel> members,
            StorageContainerModel source,
            string stackId,
            StorageItemPosition previousPosition)
        {
            if (members == null || source == null)
            {
                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                StorageItemModel member = members[i];
                if (member == null)
                {
                    continue;
                }

                if (member.CurrentContainer != null)
                {
                    member.CurrentContainer.Items.Remove(member);
                }

                member.StackId = stackId;
                source.PlaceItem(member, previousPosition.X, previousPosition.Y);
            }
        }
    }
}
