using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public static class CampusInventoryActionExecutor
    {
        public static bool TryTransferItem(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            out StorageTransferResult result)
        {
            return TryTransferItem(actor, item, source, target, x, y, null, out result);
        }

        public static bool TryTransferItem(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (actor == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            context = BuildActorTransferContext(actor, context, StorageTransferReason.Move);
            return CampusInventoryTransferService.Resolve().TryMoveItem(item, source, target, x, y, context, out result);
        }

        public static bool TryTransferItemToFirstFit(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            out StorageTransferResult result)
        {
            return TryTransferItemToFirstFit(actor, item, source, targets, null, out result);
        }

        public static bool TryTransferItemToFirstFit(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (actor == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            context = BuildActorTransferContext(actor, context, StorageTransferReason.QuickTransfer);
            return CampusInventoryTransferService.Resolve().TryMoveToFirstFit(item, source, targets, context, out result);
        }

        public static bool TryDropItemToGround(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            GameObject groundDropContext,
            out StorageTransferResult result)
        {
            return TryDropItemToGround(actor, item, source, groundDropContext, null, out result);
        }

        public static bool TryDropItemToGround(
            CampusCharacterRuntime actor,
            StorageItemModel item,
            StorageContainerModel source,
            GameObject groundDropContext,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            if (actor == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            GameObject dropContext = groundDropContext != null ? groundDropContext : actor.gameObject;
            context = BuildActorTransferContext(actor, context, StorageTransferReason.DropToGround);
            return CampusInventoryTransferService.Resolve().TryDropItemToGround(dropContext, item, source, context, out result);
        }

        public static bool TryOpenInventoryView(
            CampusCharacterRuntime actor,
            StorageContainerModel externalContainer,
            GameObject groundDropContext,
            bool includeBackpack,
            out string message)
        {
            message = string.Empty;
            if (actor == null)
            {
                message = StorageTextCatalog.Get(StorageTextId.MissingItemOrSource);
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(actor, true);
            if (inventory == null)
            {
                message = StorageTextCatalog.Get(StorageTextId.MissingItemOrSource);
                return false;
            }

            StorageWindowUI window = Object.FindFirstObjectByType<StorageWindowUI>(FindObjectsInactive.Include);
            if (window == null)
            {
                GameObject windowObject = new GameObject("Canvas_Storage", typeof(RectTransform), typeof(StorageWindowUI));
                window = windowObject.GetComponent<StorageWindowUI>();
            }

            GameObject dropContext = groundDropContext != null ? groundDropContext : actor.gameObject;
            window.SetGroundDropContext(dropContext);
            window.SetActorContext(actor.gameObject);
            window.SetTransferHandler(CampusStorageTransferHandler.Instance);
            window.OpenPlayerStorage(
                inventory.Hands,
                inventory.Pockets,
                inventory.Backpack,
                includeBackpack && inventory.HasBackpack,
                externalContainer,
                includeBackpack);
            return true;
        }

        private static StorageTransferContext BuildActorTransferContext(
            CampusCharacterRuntime actor,
            StorageTransferContext context,
            StorageTransferReason fallbackReason)
        {
            if (context == null)
            {
                return StorageTransferContext.ForActor(actor.gameObject, fallbackReason);
            }

            if (context.Actor == null)
            {
                context.Actor = actor.gameObject;
            }

            return context;
        }
    }
}
