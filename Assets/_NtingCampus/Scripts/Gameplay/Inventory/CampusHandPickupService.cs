using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public sealed class CampusHandPickupService
    {
        private const float HandsFullSpeechDurationSeconds = 2f;

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

            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(context);
            if (actorRuntime == null)
            {
                result = StorageTransferResult.Fail(StorageTextCatalog.Get(StorageTextId.MissingItemOrSource));
                return false;
            }

            CampusCharacterInventory inventory = CampusCharacterInventoryService.GetOrCreateInventory(
                actorRuntime,
                false);
            StorageContainerModel[] hands = inventory.Hands;
            if (!TryFindFirstOpenHand(hands, item, out StorageContainerModel targetHand, out Vector2Int targetPosition))
            {
                return FailBecauseHandsAreFull(actorRuntime, out result);
            }

            StorageContainerModel source = item.CurrentContainer;
            if (!transferService.TryMoveItem(item, source, targetHand, targetPosition.x, targetPosition.y, context, out result))
            {
                return false;
            }

            RefreshHeldItemVisual(actorRuntime);
            return true;
        }

        private static CampusCharacterRuntime ResolveActorRuntime(StorageTransferContext context)
        {
            return context != null
                ? CampusCharacterActionUtility.ResolveActorRuntime(context.Actor)
                : null;
        }

        private static void RefreshHeldItemVisual(CampusCharacterRuntime runtime)
        {
            CampusHeldItemVisual heldItemVisual = runtime != null ? runtime.GetComponent<CampusHeldItemVisual>() : null;
            if (heldItemVisual != null)
            {
                heldItemVisual.RefreshImmediate();
            }
        }

        private static bool TryFindFirstOpenHand(
            StorageContainerModel[] hands,
            StorageItemModel item,
            out StorageContainerModel targetHand,
            out Vector2Int targetPosition)
        {
            targetHand = null;
            targetPosition = default;
            if (hands == null || item == null)
            {
                return false;
            }

            for (int i = 0; i < hands.Length; i++)
            {
                StorageContainerModel hand = hands[i];
                if (hand != null && hand.FindFirstFit(item, out targetPosition))
                {
                    targetHand = hand;
                    return true;
                }
            }

            return false;
        }

        private static bool FailBecauseHandsAreFull(
            CampusCharacterRuntime actorRuntime,
            out StorageTransferResult result)
        {
            string message = StorageTextCatalog.Get(StorageTextId.HandsFullPickup);
            CampusCharacterSpeechUtility.Speak(actorRuntime, message, HandsFullSpeechDurationSeconds);
            result = StorageTransferResult.Fail(message);
            return false;
        }
    }
}
