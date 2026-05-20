using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Economy
{
    internal sealed class CampusStoreNpcOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public static CampusStoreNpcOpportunityProvider Instance { get; } = new CampusStoreNpcOpportunityProvider();

        public string ProviderId => "store";

        private CampusStoreNpcOpportunityProvider()
        {
        }

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            if (!npc.IsValid)
            {
                return false;
            }

            switch (query.Purpose)
            {
                case CampusNpcOpportunityPurpose.Required:
                    return true;
                case CampusNpcOpportunityPurpose.FreeMovement:
                    return npc.Data.Role == CampusCharacterRole.Student;
                default:
                    return false;
            }
        }

        public void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            if (results == null || !npc.IsValid)
            {
                return;
            }

            CampusCommerceService commerce = ResolveCommerce(npc);
            if (commerce == null || !commerce.IsStoreOpenNow())
            {
                return;
            }

            switch (query.Purpose)
            {
                case CampusNpcOpportunityPurpose.Required:
                    CollectCheckoutOpportunity(npc, commerce, results);
                    break;
                case CampusNpcOpportunityPurpose.FreeMovement:
                    if (ShouldSeekStorePurchase(npc, commerce))
                    {
                        CollectShelfBrowseOpportunity(npc, commerce, SelectPreferredStoreCategory(npc), results);
                    }

                    break;
            }
        }

        private static CampusCommerceService ResolveCommerce(CampusNpcOpportunityContext npc)
        {
            return CampusCommerceService.Resolve(false);
        }

        private static void CollectCheckoutOpportunity(
            CampusNpcOpportunityContext npc,
            CampusCommerceService commerce,
            List<CampusNpcActionOpportunity> results)
        {
            if (npc.Runtime == null || !commerce.ActorHasUnpaidStoreItems(npc.Runtime))
            {
                return;
            }

            if (!commerce.TryFindCheckoutTarget(
                    npc.Runtime,
                    out CampusPlacedObject checkout,
                    out string roomId,
                    out Vector3 targetPosition))
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "store_checkout",
                CampusCharacterAction.RunCommand(StoreOpportunityCommand.Checkout(checkout)),
                targetPosition,
                roomId,
                0.18f,
                90f,
                CampusNpcIntentKind.PayStoreCheckout,
                "StoreCheckout",
                actor => actor != null && commerce.ActorHasUnpaidStoreItems(actor) && commerce.IsStoreOpenNow()));
        }

        private static void CollectShelfBrowseOpportunity(
            CampusNpcOpportunityContext npc,
            CampusCommerceService commerce,
            string preferredCategoryId,
            List<CampusNpcActionOpportunity> results)
        {
            if (!commerce.TryFindShelfBrowseTarget(
                    npc.Runtime,
                    preferredCategoryId,
                    out CampusPlacedObject shelf,
                    out string roomId,
                    out Vector3 targetPosition))
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "store_shelf",
                CampusCharacterAction.RunCommand(StoreOpportunityCommand.TakeShelfItem(shelf)),
                targetPosition,
                roomId,
                0.2f,
                55f,
                CampusNpcIntentKind.BrowseStoreShelf,
                "StoreShelf",
                actor => actor != null && !commerce.ActorHasUnpaidStoreItems(actor) && commerce.IsStoreOpenNow()));
        }

        private static bool ShouldSeekStorePurchase(CampusNpcOpportunityContext npc, CampusCommerceService commerce)
        {
            if (!npc.IsValid || !npc.HasMindState)
            {
                return false;
            }

            if (npc.Data.Role != CampusCharacterRole.Student)
            {
                return false;
            }

            var state = npc.Bootstrap != null ? npc.Bootstrap.GameState : null;
            if (state == null)
            {
                return false;
            }

            if (npc.Data.IsPlayerControlled ||
                npc.Data.State == CampusCharacterState.Punished ||
                npc.Data.State == CampusCharacterState.Sleeping ||
                npc.Time < npc.NextStoreVisitAllowedAt ||
                !CampusNpcScheduleFacts.IsStudentFreeMovementWindow(npc.Segment) ||
                commerce.HasCheckedOutStoreToday(npc.Runtime) ||
                commerce.ActorHasUnpaidStoreItems(npc.Runtime))
            {
                return false;
            }

            int threshold = 14;
            if (npc.Data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                threshold += 12;
            }

            if (npc.Data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                threshold -= 4;
            }

            threshold += Mathf.Clamp(npc.Data.Mischief / 10, 0, 10);
            int roll = CampusNpcStableIds.PositiveModulo(
                CampusNpcStableIds.Hash(npc.Data.Id + ":store:" + state.Day + ":" + npc.Segment),
                100);
            return roll < threshold;
        }

        private static string SelectPreferredStoreCategory(CampusNpcOpportunityContext npc)
        {
            if (npc.Data == null)
            {
                return string.Empty;
            }

            if (npc.Data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return CampusNpcStableIds.PositiveModulo(npc.PersonalSeed, 2) == 0 ? "book" : "stationery";
            }

            if (npc.Data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                return CampusNpcStableIds.PositiveModulo(npc.PersonalSeed, 2) == 0 ? "snack" : "electronics";
            }

            switch (CampusNpcStableIds.PositiveModulo(npc.PersonalSeed, 5))
            {
                case 0:
                    return "snack";
                case 1:
                    return "stationery";
                case 2:
                    return "book";
                case 3:
                    return "electronics";
                default:
                    return "general";
            }
        }

        private enum StoreOpportunityCommandKind
        {
            Checkout,
            TakeShelfItem
        }

        private sealed class StoreOpportunityCommand : ICampusCharacterActionCommand
        {
            private readonly StoreOpportunityCommandKind kind;
            private readonly CampusPlacedObject target;

            private StoreOpportunityCommand(StoreOpportunityCommandKind kind, CampusPlacedObject target)
            {
                this.kind = kind;
                this.target = target;
            }

            public static StoreOpportunityCommand Checkout(CampusPlacedObject checkout)
            {
                return new StoreOpportunityCommand(StoreOpportunityCommandKind.Checkout, checkout);
            }

            public static StoreOpportunityCommand TakeShelfItem(CampusPlacedObject shelf)
            {
                return new StoreOpportunityCommand(StoreOpportunityCommandKind.TakeShelfItem, shelf);
            }

            public bool TryExecute(CampusCharacterRuntime actor, out StorageTransferResult result)
            {
                result = StorageTransferResult.Fail(string.Empty);
                CampusCommerceService commerce = CampusCommerceService.Resolve(false);
                if (actor == null || target == null || commerce == null)
                {
                    return false;
                }

                bool succeeded = kind == StoreOpportunityCommandKind.Checkout
                    ? commerce.TryCheckout(actor, target, out string message)
                    : commerce.TryTakeOneItemFromShelf(actor, target, out message);
                result = new StorageTransferResult(succeeded, false, false, message, string.Empty);
                return succeeded;
            }
        }
    }
}
