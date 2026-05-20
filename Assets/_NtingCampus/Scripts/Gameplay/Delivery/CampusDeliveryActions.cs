using Nting.Storage;
using NtingCampus.Gameplay.Characters;

namespace NtingCampus.Gameplay.Delivery
{
    internal static class CampusDeliveryActions
    {
        public static void RefreshPersonalDelivery(CampusNpcAiRuntime npc)
        {
            CampusNpcMindState mind = npc != null ? npc.Mind : null;
            if (npc == null ||
                mind == null ||
                mind.DeliveryState != CampusNpcDeliveryState.Waiting ||
                mind.DeliveryReadyAt <= 0f ||
                npc.Time < mind.DeliveryReadyAt)
            {
                return;
            }

            mind.DeliveryState = CampusNpcDeliveryState.ReadyForPickup;
            npc.Speak(CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.DeliveryIsHere), 1.8f, false);
            npc.RequestDecisionSoon();
        }

        public static bool TryPlaceStudentOrder(
            CampusNpcAiRuntime npc,
            CampusCharacterRuntime actor,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            CampusNpcMindState mind = npc != null ? npc.Mind : null;
            if (npc == null || actor == null || actor != npc.Runtime || mind == null)
            {
                return false;
            }

            mind.DeliveryState = CampusNpcDeliveryState.Waiting;
            mind.DeliveryReadyAt =
                npc.Time +
                CampusDeliveryFacts.DeliveryLeadSeconds +
                CampusNpcStableIds.PositiveModulo(npc.PersonalSeed, 7);
            mind.NextDeliveryOrderAllowedAt = npc.Time + CampusDeliveryFacts.OrderCooldownSeconds;
            mind.IntentHoldUntil = -1f;
            npc.Speak(CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.DeliveryOrderPlaced), 1.8f, false);
            npc.RequestDecisionSoon();

            result = new StorageTransferResult(true, false, false, string.Empty, string.Empty);
            return true;
        }

        public static bool TryPickupStudentDelivery(
            CampusNpcAiRuntime npc,
            CampusCharacterRuntime actor,
            out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            CampusNpcMindState mind = npc != null ? npc.Mind : null;
            if (npc == null ||
                actor == null ||
                actor != npc.Runtime ||
                mind == null ||
                mind.DeliveryState != CampusNpcDeliveryState.ReadyForPickup)
            {
                return false;
            }

            mind.DeliveryState = CampusNpcDeliveryState.PickedUp;
            mind.DeliveryReadyAt = -1f;
            mind.NextDeliveryOrderAllowedAt = npc.Time + CampusDeliveryFacts.PickupCooldownSeconds;
            mind.IntentHoldUntil = -1f;
            npc.Speak(CampusNpcSpeechTextCatalog.Get(CampusNpcSpeechTextId.DeliveryPickedUp), 1.8f, false);
            npc.RequestDecisionSoon();

            result = new StorageTransferResult(true, false, false, string.Empty, string.Empty);
            return true;
        }
    }
}
