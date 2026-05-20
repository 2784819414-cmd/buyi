using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Inventory;
using UnityEngine;

namespace NtingCampus.Gameplay.Canteen
{
    public sealed class CampusCanteenFoodBoxTransferHandler : IStorageTransferHandler
    {
        private readonly StorageContainerModel foodBox;
        private readonly CampusCanteenStation station;

        public CampusCanteenFoodBoxTransferHandler(StorageContainerModel foodBox, CampusCanteenStation station)
        {
            this.foodBox = foodBox;
            this.station = station;
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
                BuildContext(source, target, context),
                out result);
        }

        public bool TryMoveToFirstFit(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel[] targets,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            StorageContainerModel target = ResolveFirstAvailableTarget(targets);
            return CampusInventoryActionExecutor.TryTransferItemToFirstFit(
                ResolveActorRuntime(context),
                item,
                source,
                targets,
                BuildContext(source, target, context),
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
                BuildContext(source, null, context),
                out result);
        }

        private StorageTransferContext BuildContext(
            StorageContainerModel source,
            StorageContainerModel target,
            StorageTransferContext context)
        {
            context = context ?? new StorageTransferContext();
            if (IsTakingFromFoodBox(source, target))
            {
                context.ForceIllegal = true;
                context.OwnerId = station != null ? station.StationId : context.OwnerId;
                context.SourceLocation = station != null ? station.DisplayName : context.SourceLocation;
                if (context.SuspicionRiskOverride < 0)
                {
                    context.SuspicionRiskOverride = foodBox != null ? foodBox.SuspicionRisk : 18;
                }
            }

            return context;
        }

        private bool IsTakingFromFoodBox(StorageContainerModel source, StorageContainerModel target)
        {
            if (source == null || !ReferenceEquals(source, foodBox))
            {
                return false;
            }

            if (ReferenceEquals(source, target))
            {
                return false;
            }

            return target == null ||
                   target.IsPlayerCarried ||
                   target.AccessPolicy == StorageContainerAccessPolicy.PlayerCarried ||
                   target.AccessPolicy == StorageContainerAccessPolicy.Ground;
        }

        private static StorageContainerModel ResolveFirstAvailableTarget(StorageContainerModel[] targets)
        {
            if (targets == null)
            {
                return null;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    return targets[i];
                }
            }

            return null;
        }

        private static CampusCharacterRuntime ResolveActorRuntime(StorageTransferContext context)
        {
            return context != null && context.Actor != null
                ? context.Actor.GetComponentInParent<CampusCharacterRuntime>()
                : null;
        }
    }
}
