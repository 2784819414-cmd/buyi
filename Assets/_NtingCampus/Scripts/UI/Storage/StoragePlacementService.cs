using UnityEngine;

namespace Nting.Storage
{
    public static class StoragePlacementService
    {
        public static bool CanPlace(
            StorageContainerModel container,
            StorageItemModel item,
            int x,
            int y,
            StorageItemModel ignoreItem = null)
        {
            if (container == null || item == null)
            {
                return false;
            }

            if (container.IsSingleItemSlot)
            {
                return CanPlaceInSingleItemSlot(container, item, x, y, ignoreItem);
            }

            return IsInside(container, item, x, y) &&
                   !HasOverlap(container, item, x, y, ignoreItem) &&
                   CanAcceptWeight(container, item, ignoreItem);
        }

        public static bool FindFirstFit(
            StorageContainerModel container,
            StorageItemModel item,
            out Vector2Int position)
        {
            position = default;
            if (container == null || item == null)
            {
                return false;
            }

            if (container.IsSingleItemSlot)
            {
                if (CanPlaceInSingleItemSlot(container, item, 0, 0, container.Contains(item) ? item : null))
                {
                    position = Vector2Int.zero;
                    return true;
                }

                return false;
            }

            StorageItemModel ignoreItem = container.Contains(item) ? item : null;
            for (int y = 0; y < container.Rows; y++)
            {
                for (int x = 0; x < container.Columns; x++)
                {
                    if (CanPlace(container, item, x, y, ignoreItem))
                    {
                        position = new Vector2Int(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsCellOccupied(
            StorageContainerModel container,
            int x,
            int y,
            StorageItemModel ignoreItem = null)
        {
            if (container == null || container.Items == null)
            {
                return false;
            }

            if (container.IsSingleItemSlot)
            {
                return x == 0 && y == 0 && HasAnyOccupant(container, ignoreItem);
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null || item == ignoreItem)
                {
                    continue;
                }

                if (x >= item.X && x < item.X + item.CurrentWidth &&
                    y >= item.Y && y < item.Y + item.CurrentHeight)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInside(StorageContainerModel container, StorageItemModel item, int x, int y)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x + item.CurrentWidth <= container.Columns &&
                   y + item.CurrentHeight <= container.Rows;
        }

        private static bool HasOverlap(
            StorageContainerModel container,
            StorageItemModel item,
            int x,
            int y,
            StorageItemModel ignoreItem)
        {
            RectInt candidate = new RectInt(x, y, item.CurrentWidth, item.CurrentHeight);
            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel other = container.Items[i];
                if (other == null || other == ignoreItem)
                {
                    continue;
                }

                RectInt occupied = new RectInt(other.X, other.Y, other.CurrentWidth, other.CurrentHeight);
                if (candidate.Overlaps(occupied))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanAcceptWeight(
            StorageContainerModel container,
            StorageItemModel item,
            StorageItemModel ignoreItem)
        {
            float currentWeight = container.CurrentWeight;
            if (ignoreItem != null && container.Contains(ignoreItem))
            {
                currentWeight -= ignoreItem.Weight;
            }

            return container.MaxWeight <= 0f || currentWeight + item.Weight <= container.MaxWeight + 0.0001f;
        }

        private static bool CanPlaceInSingleItemSlot(
            StorageContainerModel container,
            StorageItemModel item,
            int x,
            int y,
            StorageItemModel ignoreItem)
        {
            return x == 0 &&
                   y == 0 &&
                   !HasAnyOccupant(container, ignoreItem) &&
                   CanAcceptWeight(container, item, ignoreItem);
        }

        private static bool HasAnyOccupant(StorageContainerModel container, StorageItemModel ignoreItem)
        {
            if (container == null || container.Items == null)
            {
                return false;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null && item != ignoreItem)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
