using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusStorageTransferHandler : IStorageTransferHandler
    {
        public static readonly CampusStorageTransferHandler Instance = new CampusStorageTransferHandler();

        private CampusStorageTransferHandler()
        {
        }

        public bool TryMoveItem(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel target,
            int x,
            int y,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryTransferItem(
                ResolveActorRuntime(context),
                item,
                source,
                target,
                x,
                y,
                context,
                out result);
        }

        public bool TryMoveToFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryTransferItemToFirstFit(
                ResolveActorRuntime(context),
                item,
                source,
                targets,
                context,
                out result);
        }

        public bool TryDropItemToGround(
            GameObject dropContext,
            StorageItemModel item,
            StorageContainerModel source,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            return CampusInventoryActionExecutor.TryDropItemToGround(
                ResolveActorRuntime(context),
                item,
                source,
                dropContext,
                context,
                out result);
        }

        private static CampusCharacterRuntime ResolveActorRuntime(StorageTransferContext context)
        {
            return context != null && context.Actor != null
                ? context.Actor.GetComponentInParent<CampusCharacterRuntime>()
                : null;
        }
    }
}
