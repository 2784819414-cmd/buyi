using System;
using NtingCampus.Gameplay.Inventory;
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

            if (item.ConsumeOnUse && !TryRemoveItem(item, sourceGrid, out string removeError))
            {
                statusMessage = string.IsNullOrWhiteSpace(removeError) ? "Could not consume " + itemName + "." : removeError;
                return false;
            }

            Debug.Log("[Storage] " + statusMessage);
            return true;
        }

        private static bool TryRemoveItem(StorageItemModel item, StorageGridUI sourceGrid, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (item == null)
            {
                return false;
            }

            CampusInventoryTransferService service = CampusInventoryTransferService.Resolve();
            StorageContainerModel source = sourceGrid != null ? sourceGrid.Container : item.CurrentContainer;
            StorageTransferContext context = new StorageTransferContext
            {
                Reason = StorageTransferReason.UseItem
            };
            if (service.TryConsumeItem(item, source, context, out StorageTransferResult result))
            {
                return true;
            }

            errorMessage = result.Message;
            return false;
        }
    }
}
