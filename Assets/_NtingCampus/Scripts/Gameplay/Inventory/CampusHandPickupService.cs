using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusHandPickupService
    {
        private readonly CampusInventoryTransferService transferService;

        public CampusHandPickupService(CampusInventoryTransferService transferService)
        {
            this.transferService = transferService;
        }

        public bool TryPickUpIntoHands(
            StorageMemory memory,
            StorageItemModel item,
            StorageTransferContext context,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (memory == null || item == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItem));
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(
                ResolveActorRuntime(context),
                false);
            StorageContainerModel[] hands = inventory.Hands;
            for (int i = 0; i < hands.Length; i++)
            {
                if (transferService.TryPlaceFirstFit(item, null, hands[i], context, out result))
                {
                    RefreshHeldItemVisual(context);
                    return true;
                }
            }

            result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.TargetBlocked));
            return false;
        }

        private static CampusCharacterRuntime ResolveActorRuntime(StorageTransferContext context)
        {
            if (context != null && context.Actor != null)
            {
                CampusCharacterRuntime runtime = context.Actor.GetComponentInParent<CampusCharacterRuntime>();
                if (runtime != null)
                {
                    return runtime;
                }
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : Object.FindFirstObjectByType<CampusCharacterRuntime>();
        }

        private static void RefreshHeldItemVisual(StorageTransferContext context)
        {
            CampusCharacterRuntime runtime = ResolveActorRuntime(context);
            CampusHeldItemVisual heldItemVisual = runtime != null ? runtime.GetComponent<CampusHeldItemVisual>() : null;
            if (heldItemVisual != null)
            {
                heldItemVisual.RefreshImmediate();
            }
        }
    }
}
