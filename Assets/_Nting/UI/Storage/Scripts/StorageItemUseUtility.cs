using System;
using UnityEngine;

namespace Nting.Storage
{
    public static class StorageItemUseUtility
    {
        public const string ConsumeFoodActionId = "campus.item.consume_food";

        public static bool CanUse(StorageItemModel item)
        {
            return item != null && item.IsUsable && !string.IsNullOrWhiteSpace(item.UseActionId);
        }

        public static bool TryUse(StorageItemModel item, StorageGridUI sourceGrid, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (!CanUse(item))
            {
                statusMessage = "This item cannot be used.";
                return false;
            }

            if (!StoragePlayerInventoryUtility.IsHandContainerId(item.CurrentContainerId))
            {
                statusMessage = "Move it into a hand slot first.";
                return false;
            }

            if (!string.Equals(item.UseActionId, ConsumeFoodActionId, StringComparison.OrdinalIgnoreCase))
            {
                statusMessage = "Unsupported item use action: " + item.UseActionId + ".";
                return false;
            }

            string itemName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.DefinitionId : item.DisplayName;
            statusMessage = string.IsNullOrWhiteSpace(item.UseText)
                ? "Used " + itemName + "."
                : item.UseText;

            if (item.ConsumeOnUse && !TryRemoveItem(item, sourceGrid))
            {
                statusMessage = "Could not consume " + itemName + ".";
                return false;
            }

            Debug.Log("[Storage] " + statusMessage);
            return true;
        }

        private static bool TryRemoveItem(StorageItemModel item, StorageGridUI sourceGrid)
        {
            if (item == null)
            {
                return false;
            }

            if (sourceGrid != null && sourceGrid.RemoveItem(item))
            {
                return true;
            }

            StorageContainerModel container = item.CurrentContainer;
            return container != null && container.RemoveItem(item);
        }
    }
}
