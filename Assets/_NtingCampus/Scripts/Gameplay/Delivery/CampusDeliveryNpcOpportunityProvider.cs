using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;

namespace NtingCampus.Gameplay.Delivery
{
    internal sealed class CampusDeliveryNpcOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public static CampusDeliveryNpcOpportunityProvider Instance { get; } =
            new CampusDeliveryNpcOpportunityProvider();

        public string ProviderId => "delivery";

        private CampusDeliveryNpcOpportunityProvider()
        {
        }

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            if (!npc.IsValid || npc.Data.Role != CampusCharacterRole.Student)
            {
                return false;
            }

            return query.Purpose == CampusNpcOpportunityPurpose.Required ||
                   query.Purpose == CampusNpcOpportunityPurpose.FreeMovement;
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

            CampusDeliveryActions.RefreshPersonalDelivery(npc.RuntimeState);

            switch (query.Purpose)
            {
                case CampusNpcOpportunityPurpose.Required:
                    CollectPickup(npc, results);
                    break;
                case CampusNpcOpportunityPurpose.FreeMovement:
                    CollectOrder(npc, results);
                    break;
            }
        }

        private static void CollectPickup(
            CampusNpcOpportunityContext npc,
            List<CampusNpcActionOpportunity> results)
        {
            if (!CampusDeliveryFacts.CanPickupStudentDelivery(npc) ||
                !CampusDeliveryFacts.TryResolvePickupTarget(npc, out CampusDeliveryTarget target))
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "delivery_pickup",
                CampusCharacterAction.RunCommand(DeliveryCommand.Pickup(npc.RuntimeState)),
                target.Position,
                target.RoomId,
                target.StopDistance,
                92f,
                CampusNpcIntentKind.PickupDelivery,
                "PickupDelivery",
                actor => actor != null &&
                         actor == npc.Runtime &&
                         npc.DeliveryState == CampusNpcDeliveryState.ReadyForPickup));
        }

        private static void CollectOrder(
            CampusNpcOpportunityContext npc,
            List<CampusNpcActionOpportunity> results)
        {
            if (!CampusDeliveryFacts.CanStartStudentOrder(npc))
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                "delivery_order",
                CampusCharacterAction.RunCommand(DeliveryCommand.Order(npc.RuntimeState)),
                npc.Position,
                npc.Data.CurrentRoomId,
                0.05f,
                64f,
                CampusNpcIntentKind.UsePhoneForDelivery,
                "UsePhoneDelivery",
                false,
                CampusDeliveryFacts.OrderHoldSeconds,
                actor => actor != null &&
                         actor == npc.Runtime &&
                         CampusDeliveryFacts.CanStartStudentOrder(npc)));
        }

        private enum DeliveryCommandKind
        {
            Order,
            Pickup
        }

        private sealed class DeliveryCommand : ICampusCharacterActionCommand
        {
            private readonly DeliveryCommandKind kind;
            private readonly CampusNpcAiRuntime npc;

            private DeliveryCommand(DeliveryCommandKind kind, CampusNpcAiRuntime npc)
            {
                this.kind = kind;
                this.npc = npc;
            }

            public static DeliveryCommand Order(CampusNpcAiRuntime npc)
            {
                return new DeliveryCommand(DeliveryCommandKind.Order, npc);
            }

            public static DeliveryCommand Pickup(CampusNpcAiRuntime npc)
            {
                return new DeliveryCommand(DeliveryCommandKind.Pickup, npc);
            }

            public bool TryExecute(CampusCharacterRuntime actor, out StorageTransferResult result)
            {
                return kind == DeliveryCommandKind.Order
                    ? CampusDeliveryActions.TryPlaceStudentOrder(npc, actor, out result)
                    : CampusDeliveryActions.TryPickupStudentDelivery(npc, actor, out result);
            }
        }
    }
}
