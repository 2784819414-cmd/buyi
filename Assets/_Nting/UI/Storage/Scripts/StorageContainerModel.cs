using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    [Serializable]
    public sealed class StorageContainerModel
    {
        public string Id;
        public string DisplayName;
        public int Columns = 1;
        public int Rows = 1;
        public float MaxWeight = 10f;
        public List<StorageItemModel> Items = new List<StorageItemModel>();
        public StorageContainerAccessPolicy AccessPolicy = StorageContainerAccessPolicy.Open;
        public string OwnerId;
        public string OwnerRole;
        public string RoomId;
        public bool AllowTakingContents = true;
        public bool IsPlayerCarried;
        public int SuspicionRisk;

        public float CurrentWeight
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i] != null)
                    {
                        total += Items[i].Weight;
                    }
                }

                return total;
            }
        }

        public bool Contains(StorageItemModel item)
        {
            return item != null && Items.Contains(item);
        }

        public bool CanPlace(StorageItemModel item, int x, int y, StorageItemModel ignoreItem = null)
        {
            if (item == null)
            {
                return false;
            }

            if (!IsAreaInside(x, y, item.CurrentWidth, item.CurrentHeight))
            {
                return false;
            }

            if (HasOverlap(item, x, y, ignoreItem))
            {
                return false;
            }

            if (!CanAcceptWeight(item, ignoreItem))
            {
                return false;
            }

            return true;
        }

        public bool PlaceItem(StorageItemModel item, int x, int y)
        {
            StorageItemModel ignoreItem = Contains(item) ? item : null;
            if (!CanPlace(item, x, y, ignoreItem))
            {
                return false;
            }

            if (!Items.Contains(item))
            {
                Items.Add(item);
            }

            item.X = x;
            item.Y = y;
            item.CurrentContainer = this;
            return true;
        }

        public bool RemoveItem(StorageItemModel item)
        {
            if (item == null || !Items.Remove(item))
            {
                return false;
            }

            if (item.CurrentContainer == this)
            {
                item.CurrentContainer = null;
            }

            return true;
        }

        public bool FindFirstFit(StorageItemModel item, out Vector2Int position)
        {
            position = default;
            if (item == null)
            {
                return false;
            }

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    if (CanPlace(item, x, y, Contains(item) ? item : null))
                    {
                        position = new Vector2Int(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsCellOccupied(int x, int y, StorageItemModel ignoreItem = null)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                StorageItemModel item = Items[i];
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

        private bool IsAreaInside(int x, int y, int width, int height)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x + width <= Columns &&
                   y + height <= Rows;
        }

        private bool HasOverlap(StorageItemModel item, int x, int y, StorageItemModel ignoreItem)
        {
            RectInt candidate = new RectInt(x, y, item.CurrentWidth, item.CurrentHeight);
            for (int i = 0; i < Items.Count; i++)
            {
                StorageItemModel other = Items[i];
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

        private bool CanAcceptWeight(StorageItemModel item, StorageItemModel ignoreItem)
        {
            float currentWeight = CurrentWeight;
            if (ignoreItem != null && Items.Contains(ignoreItem))
            {
                currentWeight -= ignoreItem.Weight;
            }

            return MaxWeight <= 0f || currentWeight + item.Weight <= MaxWeight + 0.0001f;
        }
    }
}
