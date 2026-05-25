using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public static class CampusBackpackEquipmentService
    {
        public static bool IsBackpackDefinitionId(string definitionId)
        {
            return !string.IsNullOrWhiteSpace(definitionId) &&
                   string.Equals(
                       definitionId.Trim(),
                       StoragePlayerInventoryUtility.BackpackItemDefinitionId,
                       System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBackpackItem(StorageItemModel item)
        {
            return item != null && IsBackpackDefinitionId(item.DefinitionId);
        }

        public static bool TryPickUpBackpack(
            CampusCharacterRuntime actor,
            StorageItemModel backpackItem,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (actor == null || backpackItem == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            if (!IsBackpackItem(backpackItem))
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItem));
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, true);
            StorageContainerModel equipmentSlot = inventory != null ? inventory.BackpackEquipmentSlot : null;
            if (equipmentSlot == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingTargetContainer));
                return false;
            }

            if (CampusBackpackInventoryUtility.ResolveEquippedBackpack(equipmentSlot) != null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.BackpackAlreadyEquipped));
                return false;
            }

            context = EnsureActorContext(actor, context, StorageTransferReason.Pickup);
            bool moved = CampusInventoryTransferService.Resolve()
                .TryMoveItem(backpackItem, source, equipmentSlot, 0, 0, context, out result);
            if (moved)
            {
                result = new StorageTransferResult(
                    true,
                    result.Illegal,
                    result.Observed,
                    StorageTextCatalog.Get(StorageTextId.BackpackEquipped),
                    result.WitnessId);
            }

            return moved;
        }

        public static bool TryDropEquippedBackpack(
            CampusCharacterRuntime actor,
            GameObject dropContext,
            out StorageTransferResult result)
        {
            if (actor == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, false);
            StorageContainerModel equipmentSlot = inventory != null ? inventory.BackpackEquipmentSlot : null;
            StorageItemModel backpackItem = CampusBackpackInventoryUtility.ResolveEquippedBackpack(equipmentSlot);
            if (equipmentSlot == null || backpackItem == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.NoBackpack));
                return false;
            }

            StorageTransferContext context = StorageTransferContext.ForActor(actor.gameObject, StorageTransferReason.DropToGround);
            bool dropped = CampusInventoryTransferService.Resolve().TryDropItemToGround(
                dropContext != null ? dropContext : actor.gameObject,
                backpackItem,
                equipmentSlot,
                context,
                out result);
            if (dropped)
            {
                result = new StorageTransferResult(
                    true,
                    result.Illegal,
                    result.Observed,
                    StorageTextCatalog.Get(StorageTextId.BackpackDropped),
                    result.WitnessId);
            }

            return dropped;
        }

        private static StorageTransferContext EnsureActorContext(
            CampusCharacterRuntime actor,
            StorageTransferContext context,
            StorageTransferReason reason)
        {
            context ??= StorageTransferContext.ForActor(actor.gameObject, reason);
            if (context.Actor == null)
            {
                context.Actor = actor.gameObject;
            }

            return context;
        }
    }
}
