using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Delivery
{
    public readonly struct CampusDeliveryTarget
    {
        public CampusDeliveryTarget(string roomId, Vector3 position, float stopDistance)
        {
            RoomId = roomId ?? string.Empty;
            Position = position;
            StopDistance = Mathf.Max(0.02f, stopDistance);
        }

        public string RoomId { get; }
        public Vector3 Position { get; }
        public float StopDistance { get; }
    }

    public static class CampusDeliveryFacts
    {
        public const float OrderHoldSeconds = 4.5f;
        public const float DeliveryLeadSeconds = 28f;
        public const float OrderCooldownSeconds = 480f;
        public const float PickupCooldownSeconds = 600f;

        public static bool CanStartStudentOrder(CampusNpcOpportunityContext npc)
        {
            if (!npc.IsValid ||
                !npc.HasMindState ||
                npc.Data.Role != CampusCharacterRole.Student ||
                npc.Data.IsPlayerControlled ||
                npc.Data.State == CampusCharacterState.Punished ||
                npc.Data.State == CampusCharacterState.Sleeping ||
                npc.DeliveryState == CampusNpcDeliveryState.Ordering ||
                npc.DeliveryState == CampusNpcDeliveryState.Waiting ||
                npc.DeliveryState == CampusNpcDeliveryState.ReadyForPickup ||
                npc.Time < npc.NextDeliveryOrderAllowedAt ||
                !CampusNpcScheduleFacts.IsStudentDeliveryOrderWindow(npc.Segment))
            {
                return false;
            }

            int threshold = npc.Data.HasTrait(CampusCharacterTrait.SecretDeliveryBuyer) ? 82 : 18;
            threshold += Mathf.Clamp(npc.Data.Mischief / 8, 0, 12);
            int roll = CampusNpcStableIds.PositiveModulo(
                CampusNpcStableIds.Hash(npc.Data.Id + ":" + npc.Segment),
                100);
            return roll < threshold;
        }

        public static bool CanPickupStudentDelivery(CampusNpcOpportunityContext npc)
        {
            return npc.IsValid &&
                   npc.HasMindState &&
                   npc.Data.Role == CampusCharacterRole.Student &&
                   npc.DeliveryState == CampusNpcDeliveryState.ReadyForPickup &&
                   TryResolvePickupTarget(npc, out _);
        }

        public static bool TryResolvePickupTarget(
            CampusNpcOpportunityContext npc,
            out CampusDeliveryTarget target)
        {
            target = default;
            if (!npc.IsValid)
            {
                return false;
            }

            CampusNpcPersonalProfile profile = npc.Profile;
            if (profile != null && profile.HasDeliveryPoint)
            {
                target = new CampusDeliveryTarget(
                    profile.DeliveryRoomId,
                    profile.DeliveryPointPosition,
                    0.18f);
                return true;
            }

            Vector3 fallback = npc.RuntimeState != null
                ? npc.RuntimeState.RoomTarget(CampusRoomType.Outdoor, 0.25f)
                : npc.Position;
            target = new CampusDeliveryTarget(string.Empty, fallback, 0.18f);
            return true;
        }
    }
}
