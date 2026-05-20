using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcCommonProfileResolver
    {
        public static void Build(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            int peerIndex = CampusNpcRosterIndexer.RoleIndex(data, rosterService);
            CampusGameplayRoom dorm = CampusNpcRoomSelector.Choose(
                CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Dormitory),
                data != null ? data.Id : string.Empty,
                peerIndex);
            profile.SetDorm(dorm, CampusNpcRoomSelector.PointNearCenter(dorm, peerIndex, 0.35f));

            List<CampusGameplayRoom> commonRooms =
                CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.CommonActivityZone);
            if (commonRooms.Count == 0)
            {
                commonRooms = CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Corridor);
            }

            CampusGameplayRoom common = CampusNpcRoomSelector.Choose(
                commonRooms,
                data != null ? data.Id : string.Empty,
                peerIndex);
            profile.SetCommonRoom(common, CampusNpcRoomSelector.PointNearCenter(common, peerIndex, 0.55f));

            CampusGameplayRoom deliveryRoom = CampusNpcRoomSelector.Choose(
                CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Outdoor),
                data != null ? data.Id : string.Empty,
                peerIndex);
            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    data != null ? data.Assignments.DeliveryPointId : string.Empty,
                    CampusNpcFacilityGroups.DeliveryPoints,
                    out CampusGameplayRoom assignedDeliveryRoom,
                    out CampusGameplayRoom.FacilityRecord assignedDeliveryPoint))
            {
                profile.SetDeliveryPoint(
                    assignedDeliveryRoom,
                    CampusNpcFacilitySelector.KeyFor(assignedDeliveryRoom, assignedDeliveryPoint),
                    CampusNpcFacilitySelector.PositionOf(assignedDeliveryPoint));
            }
            else if (CampusNpcFacilitySelector.TryChoose(
                         deliveryRoom,
                         CampusNpcFacilityGroups.DeliveryPoints,
                         peerIndex,
                         out CampusGameplayRoom.FacilityRecord deliveryPoint))
            {
                profile.SetDeliveryPoint(
                    deliveryRoom,
                    CampusNpcFacilitySelector.KeyFor(deliveryRoom, deliveryPoint),
                    CampusNpcFacilitySelector.PositionOf(deliveryPoint));
            }
            else
            {
                profile.SetDeliveryPoint(
                    deliveryRoom,
                    string.Empty,
                    CampusNpcRoomSelector.PointNearCenter(deliveryRoom, peerIndex, 0.25f));
            }
        }
    }
}
