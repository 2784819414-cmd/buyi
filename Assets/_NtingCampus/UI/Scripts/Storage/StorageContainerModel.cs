using System;
using System.Collections.Generic;
using UnityEngine;
using NtingCampus.UI.Runtime.Gameplay;

namespace Nting.Storage
{
    [Serializable]
    public sealed class StorageContainerModel
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;

        [Header("Grid")]
        public int Columns = 1;
        public int Rows = 1;
        public float MaxWeight = 10f;
        public List<StorageItemModel> Items = new List<StorageItemModel>();

        [Header("Ownership")]
        public StorageContainerAccessPolicy AccessPolicy = StorageContainerAccessPolicy.Open;
        public string OwnerId;
        public string OwnerRole;
        public string RoomId;
        public bool AllowTakingContents = true;
        public bool IsPlayerCarried;
        public bool IsSingleItemSlot;
        public int SuspicionRisk;

        public bool IsCarriedInventory =>
            IsPlayerCarried ||
            AccessPolicy == StorageContainerAccessPolicy.PlayerCarried;

        public void ApplyShape(int columns, int rows, bool isSingleItemSlot)
        {
            IsSingleItemSlot = isSingleItemSlot;
            if (IsSingleItemSlot)
            {
                Columns = 1;
                Rows = 1;
                NormalizeSingleItemSlotItems();
                return;
            }

            Columns = Mathf.Max(1, columns);
            Rows = Mathf.Max(1, rows);
        }

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
            return StoragePlacementService.CanPlace(this, item, x, y, ignoreItem);
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
                StorageItemStackingService.PrepareItemForPlacement(this, item, x, y, ignoreItem);
                Items.Add(item);
            }
            else
            {
                StorageItemStackingService.PrepareItemForPlacement(this, item, x, y, ignoreItem);
            }

            item.X = IsSingleItemSlot ? 0 : x;
            item.Y = IsSingleItemSlot ? 0 : y;
            if (!string.IsNullOrWhiteSpace(item.StackId))
            {
                StorageItemStackingService.MoveStackMembers(this, item, item.X, item.Y);
            }

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

            StorageItemStackingService.NormalizeItemAfterRemoval(this, item);
            return true;
        }

        public bool FindFirstFit(StorageItemModel item, out Vector2Int position)
        {
            return StoragePlacementService.FindFirstFit(this, item, out position);
        }

        public bool IsCellOccupied(int x, int y, StorageItemModel ignoreItem = null)
        {
            return StoragePlacementService.IsCellOccupied(this, x, y, ignoreItem);
        }

        public string GetDisplayName(CampusDisplayLanguage language)
        {
            return LocalizedDisplayName.Get(language, DisplayName, Id);
        }

        public string GetDisplayName()
        {
            return GetDisplayName(CampusLanguageState.CurrentLanguage);
        }

        private void NormalizeSingleItemSlotItems()
        {
            if (Items == null)
            {
                return;
            }

            for (int i = 0; i < Items.Count; i++)
            {
                StorageItemModel item = Items[i];
                if (item == null)
                {
                    continue;
                }

                item.X = 0;
                item.Y = 0;
            }
        }
    }
}

