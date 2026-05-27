using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
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

        public static bool TryUseFirstHeldItem(
            CampusCharacterRuntime actor,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.ItemCannotBeUsed));
            if (actor == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            StorageContainerModel[] hands = CampusHandInventoryUtility.ResolveHands(actor);
            if (TryUseHeldItem(actor, hands, 1, out result) ||
                TryUseHeldItem(actor, hands, 0, out result))
            {
                WriteUseLog(result.Message);
                return true;
            }

            return false;
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

        private static bool TryUseHeldItem(
            CampusCharacterRuntime actor,
            StorageContainerModel[] hands,
            int handIndex,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.ItemCannotBeUsed));
            StorageItemModel item = CampusHandInventoryUtility.ResolveHeldItem(hands, handIndex);
            if (!StorageItemUseUtility.CanUse(item))
            {
                return false;
            }

            StorageItemUseContext useContext = StorageItemUseContext.ForActor(actor.gameObject, StorageTransferReason.UseItem);
            if (!StorageItemUseUtility.TryUse(item, null, useContext, out string message))
            {
                result = StorageTransferResult.Fail(message);
                return false;
            }

            result = new StorageTransferResult(true, false, false, message, string.Empty);
            return true;
        }

        private static void WriteUseLog(string message)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(message);
            }
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
