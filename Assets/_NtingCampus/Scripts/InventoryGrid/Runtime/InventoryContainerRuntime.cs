using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.InventoryGrid
{
    [Serializable]
    public class InventoryContainerRuntime
    {
        public InventoryContainerDefinition definition;
        public List<PlacedItem> items = new List<PlacedItem>();

        public InventoryContainerRuntime()
        {
        }

        public InventoryContainerRuntime(InventoryContainerDefinition definition)
        {
            this.definition = definition;
        }

        public bool CanPlaceItem(ItemInstance item, int x, int y)
        {
            return ValidatePlacement(item, x, y, null, true, true);
        }

        public bool TryPlaceItem(ItemInstance item, int x, int y)
        {
            EnsureItems();
            if (ContainsItemInstance(item))
            {
                Debug.LogWarning("Inventory placement failed: item instance is already in container '" + GetContainerName() + "'.");
                return false;
            }

            if (!CanPlaceItem(item, x, y))
            {
                return false;
            }

            item.NormalizeStackCount();
            items.Add(new PlacedItem(item, x, y));
            return true;
        }

        public bool TryMoveItem(PlacedItem placedItem, int newX, int newY)
        {
            EnsureItems();
            if (!ContainsPlacedItem(placedItem))
            {
                Debug.LogWarning("Inventory move failed: placed item is not in container '" + GetContainerName() + "'.");
                return false;
            }

            if (!ValidatePlacement(placedItem.item, newX, newY, placedItem, false, true))
            {
                return false;
            }

            placedItem.x = newX;
            placedItem.y = newY;
            return true;
        }

        public bool TryRotateItem(PlacedItem placedItem)
        {
            EnsureItems();
            if (!ContainsPlacedItem(placedItem))
            {
                Debug.LogWarning("Inventory rotate failed: placed item is not in container '" + GetContainerName() + "'.");
                return false;
            }

            if (placedItem.item == null || !placedItem.item.CanRotate())
            {
                Debug.LogWarning("Inventory rotate failed: item cannot rotate.");
                return false;
            }

            bool originalRotated = placedItem.item.rotated;
            placedItem.item.Rotate();
            if (ValidatePlacement(placedItem.item, placedItem.x, placedItem.y, placedItem, false, true))
            {
                return true;
            }

            placedItem.item.rotated = originalRotated;
            return false;
        }

        public bool RemoveItem(PlacedItem placedItem)
        {
            EnsureItems();
            if (placedItem == null)
            {
                Debug.LogWarning("Inventory remove failed: placed item is null.");
                return false;
            }

            if (!items.Remove(placedItem))
            {
                Debug.LogWarning("Inventory remove failed: placed item is not in container '" + GetContainerName() + "'.");
                return false;
            }

            return true;
        }

        public PlacedItem GetItemAt(int x, int y)
        {
            EnsureItems();
            for (int i = 0; i < items.Count; i++)
            {
                PlacedItem placedItem = items[i];
                if (ContainsCell(placedItem, x, y))
                {
                    return placedItem;
                }
            }

            return null;
        }

        public bool IsCellOccupied(int x, int y)
        {
            return GetItemAt(x, y) != null;
        }

        public bool IsAreaFree(ItemInstance item, int x, int y, PlacedItem ignoreItem = null)
        {
            return ValidatePlacement(item, x, y, ignoreItem, false, false);
        }

        public float GetCurrentWeight()
        {
            EnsureItems();
            float total = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                PlacedItem placedItem = items[i];
                if (placedItem != null && placedItem.item != null)
                {
                    total += placedItem.item.TotalWeight;
                }
            }

            return total;
        }

        public bool CanAcceptWeight(ItemInstance item)
        {
            return CanAcceptWeight(item, null, true);
        }

        public bool CanAcceptWeight(ItemInstance item, PlacedItem ignoreItem)
        {
            return CanAcceptWeight(item, ignoreItem, false);
        }

        public PlacedItem FindFirstFit(ItemInstance item)
        {
            if (!HasValidContainer(true) || !HasValidItem(item, true))
            {
                return null;
            }

            for (int y = 0; y < definition.height; y++)
            {
                for (int x = 0; x < definition.width; x++)
                {
                    if (ValidatePlacement(item, x, y, null, true, false))
                    {
                        return new PlacedItem(item, x, y);
                    }
                }
            }

            return null;
        }

        public bool TryAutoPlace(ItemInstance item)
        {
            if (!CanAcceptWeight(item))
            {
                return false;
            }

            PlacedItem firstFit = FindFirstFit(item);
            if (firstFit == null)
            {
                Debug.LogWarning("Inventory auto-place failed: no valid space in container '" + GetContainerName() + "'.");
                return false;
            }

            return TryPlaceItem(item, firstFit.x, firstFit.y);
        }

        public bool ContainsPlacedItem(PlacedItem placedItem)
        {
            EnsureItems();
            return placedItem != null && items.Contains(placedItem);
        }

        public PlacedItem FindPlacedItem(ItemInstance item)
        {
            EnsureItems();
            if (item == null)
            {
                return null;
            }

            for (int i = 0; i < items.Count; i++)
            {
                PlacedItem placedItem = items[i];
                if (placedItem != null && ReferenceEquals(placedItem.item, item))
                {
                    return placedItem;
                }
            }

            return null;
        }

        public InventoryContainerSaveData ToSaveData()
        {
            EnsureItems();
            InventoryContainerSaveData saveData = new InventoryContainerSaveData
            {
                containerId = definition != null ? definition.containerId : string.Empty
            };

            for (int i = 0; i < items.Count; i++)
            {
                PlacedItem placedItem = items[i];
                if (placedItem == null || placedItem.item == null || placedItem.item.definition == null)
                {
                    continue;
                }

                saveData.items.Add(new PlacedItemSaveData
                {
                    itemDefinitionId = placedItem.item.definition.itemId,
                    instanceId = placedItem.item.instanceId,
                    x = placedItem.x,
                    y = placedItem.y,
                    rotated = placedItem.item.rotated,
                    stackCount = placedItem.item.stackCount
                });
            }

            return saveData;
        }

        public bool LoadFromSaveData(InventoryContainerSaveData saveData, InventoryItemRegistry itemRegistry)
        {
            EnsureItems();
            if (saveData == null)
            {
                Debug.LogWarning("Inventory load failed: save data is null.");
                return false;
            }

            if (itemRegistry == null)
            {
                Debug.LogWarning("Inventory load failed: item registry is null.");
                return false;
            }

            items.Clear();

            if (definition != null &&
                !string.IsNullOrWhiteSpace(saveData.containerId) &&
                !string.Equals(definition.containerId, saveData.containerId, StringComparison.Ordinal))
            {
                Debug.LogWarning("Inventory load warning: save containerId '" + saveData.containerId +
                                 "' does not match runtime container '" + definition.containerId + "'.");
            }

            bool loadedAll = true;
            if (saveData.items == null)
            {
                return true;
            }

            for (int i = 0; i < saveData.items.Count; i++)
            {
                PlacedItemSaveData placedSave = saveData.items[i];
                if (placedSave == null)
                {
                    loadedAll = false;
                    continue;
                }

                ItemDefinition itemDefinition = itemRegistry.FindItemDefinition(placedSave.itemDefinitionId);
                if (itemDefinition == null)
                {
                    loadedAll = false;
                    continue;
                }

                ItemInstance instance = new ItemInstance(itemDefinition, placedSave.instanceId, placedSave.stackCount)
                {
                    rotated = placedSave.rotated
                };
                instance.NormalizeStackCount();

                if (!TryPlaceItem(instance, placedSave.x, placedSave.y))
                {
                    loadedAll = false;
                }
            }

            return loadedAll;
        }

        private bool ValidatePlacement(
            ItemInstance item,
            int x,
            int y,
            PlacedItem ignoreItem,
            bool includeWeight,
            bool logWarnings)
        {
            if (!HasValidContainer(logWarnings) || !HasValidItem(item, logWarnings))
            {
                return false;
            }

            if (!IsInsideBounds(item, x, y))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Inventory placement failed: item '" + GetItemName(item) +
                                     "' is out of bounds in container '" + GetContainerName() + "'.");
                }

                return false;
            }

            PlacedItem overlap = FindOverlappingItem(item, x, y, ignoreItem);
            if (overlap != null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Inventory placement failed: item '" + GetItemName(item) +
                                     "' overlaps '" + GetItemName(overlap.item) +
                                     "' in container '" + GetContainerName() + "'.");
                }

                return false;
            }

            if (includeWeight && !CanAcceptWeight(item, ignoreItem, logWarnings))
            {
                return false;
            }

            return true;
        }

        private bool CanAcceptWeight(ItemInstance item, PlacedItem ignoreItem, bool logWarnings)
        {
            if (!HasValidContainer(logWarnings) || !HasValidItem(item, logWarnings))
            {
                return false;
            }

            float currentWeight = GetCurrentWeight();
            if (ignoreItem != null && ContainsPlacedItem(ignoreItem) && ignoreItem.item != null)
            {
                currentWeight -= ignoreItem.item.TotalWeight;
            }

            float projectedWeight = currentWeight + item.TotalWeight;
            if (projectedWeight <= definition.maxWeight + 0.0001f)
            {
                return true;
            }

            if (logWarnings)
            {
                Debug.LogWarning("Inventory placement failed: container '" + GetContainerName() +
                                 "' would exceed max weight. Current=" + currentWeight.ToString("0.##") +
                                 ", Item=" + item.TotalWeight.ToString("0.##") +
                                 ", Max=" + definition.maxWeight.ToString("0.##") + ".");
            }

            return false;
        }

        private bool HasValidContainer(bool logWarnings)
        {
            if (definition != null && definition.width > 0 && definition.height > 0)
            {
                return true;
            }

            if (logWarnings)
            {
                Debug.LogWarning("Inventory operation failed: container definition is not bound or has invalid size.");
            }

            return false;
        }

        private static bool HasValidItem(ItemInstance item, bool logWarnings)
        {
            if (item != null && item.definition != null && item.CurrentWidth > 0 && item.CurrentHeight > 0)
            {
                item.NormalizeStackCount();
                return true;
            }

            if (logWarnings)
            {
                Debug.LogWarning("Inventory operation failed: item instance or definition is invalid.");
            }

            return false;
        }

        private bool IsInsideBounds(ItemInstance item, int x, int y)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x + item.CurrentWidth <= definition.width &&
                   y + item.CurrentHeight <= definition.height;
        }

        private PlacedItem FindOverlappingItem(ItemInstance item, int x, int y, PlacedItem ignoreItem)
        {
            EnsureItems();
            for (int i = 0; i < items.Count; i++)
            {
                PlacedItem other = items[i];
                if (other == null || ReferenceEquals(other, ignoreItem) || other.item == null)
                {
                    continue;
                }

                if (RectanglesOverlap(x, y, item.CurrentWidth, item.CurrentHeight,
                        other.x, other.y, other.item.CurrentWidth, other.item.CurrentHeight))
                {
                    return other;
                }
            }

            return null;
        }

        private static bool RectanglesOverlap(
            int leftA,
            int topA,
            int widthA,
            int heightA,
            int leftB,
            int topB,
            int widthB,
            int heightB)
        {
            return leftA < leftB + widthB &&
                   leftA + widthA > leftB &&
                   topA < topB + heightB &&
                   topA + heightA > topB;
        }

        private static bool ContainsCell(PlacedItem placedItem, int x, int y)
        {
            if (placedItem == null || placedItem.item == null)
            {
                return false;
            }

            return x >= placedItem.x &&
                   x < placedItem.x + placedItem.item.CurrentWidth &&
                   y >= placedItem.y &&
                   y < placedItem.y + placedItem.item.CurrentHeight;
        }

        private bool ContainsItemInstance(ItemInstance item)
        {
            return FindPlacedItem(item) != null;
        }

        private void EnsureItems()
        {
            if (items == null)
            {
                items = new List<PlacedItem>();
            }
        }

        private string GetContainerName()
        {
            if (definition == null)
            {
                return "<unbound>";
            }

            return string.IsNullOrWhiteSpace(definition.displayName) ? definition.containerId : definition.displayName;
        }

        private static string GetItemName(ItemInstance item)
        {
            if (item == null || item.definition == null)
            {
                return "<invalid item>";
            }

            return string.IsNullOrWhiteSpace(item.definition.displayName)
                ? item.definition.itemId
                : item.definition.displayName;
        }
    }
}
