using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusStaffProfileResolver
    {
        public static void Build(
            CampusNpcPersonalProfile profile,
            CampusCharacterRuntime runtime,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            int staffIndex = CampusNpcRosterIndexer.PeerIndex(
                runtime,
                rosterService,
                CampusNpcRosterIndexer.IsStaff);
            CampusStaffDuty duty = data != null ? data.StaffDuty : CampusStaffDuty.None;

            if ((duty & CampusStaffDuty.StoreOwner) != 0 || (duty & CampusStaffDuty.BookstoreOwner) != 0)
            {
                BuildStoreStaffProfile(profile, data, worldService, staffIndex);
                return;
            }

            if ((duty & CampusStaffDuty.DeliveryWatcher) != 0)
            {
                BuildDeliveryStaffProfile(profile, data, worldService, staffIndex);
                return;
            }

            BuildCanteenStaffProfile(profile, data, worldService, staffIndex);
        }

        private static void BuildCanteenStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            int staffIndex)
        {
            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    data != null ? data.Assignments.PrimaryWorkstationId : string.Empty,
                    CampusNpcFacilityGroups.CanteenWorkstations,
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedCounter))
            {
                profile.SetPrimaryWorkstation(
                    assignedRoom,
                    CampusNpcFacilitySelector.KeyFor(assignedRoom, assignedCounter),
                    CampusNpcFacilitySelector.PositionOf(assignedCounter));
                CampusNpcFacilitySelector.AddPositions(
                    assignedRoom,
                    CampusNpcFacilityGroups.CanteenWorkstations,
                    profile.SecondaryWorkstationPositions);
                return;
            }

            CampusGameplayRoom canteen = CampusNpcRoomSelector.Choose(
                CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Canteen),
                data != null ? data.Id : string.Empty,
                staffIndex);
            if (CampusNpcFacilitySelector.TryChoose(
                    canteen,
                    CampusNpcFacilityGroups.CanteenWorkstations,
                    staffIndex,
                    out CampusGameplayRoom.FacilityRecord counter))
            {
                profile.SetPrimaryWorkstation(
                    canteen,
                    CampusNpcFacilitySelector.KeyFor(canteen, counter),
                    CampusNpcFacilitySelector.PositionOf(counter));
            }
            else
            {
                profile.SetPrimaryWorkstation(
                    canteen,
                    string.Empty,
                    CampusNpcRoomSelector.PointNearCenter(canteen, staffIndex, 0.25f));
            }

            CampusNpcFacilitySelector.AddPositions(
                canteen,
                CampusNpcFacilityGroups.CanteenWorkstations,
                profile.SecondaryWorkstationPositions);
        }

        private static void BuildStoreStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            int staffIndex)
        {
            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    data != null ? data.Assignments.PrimaryWorkstationId : string.Empty,
                    CampusNpcFacilityGroups.StoreWorkstations,
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedCheckout))
            {
                profile.SetPrimaryWorkstation(
                    assignedRoom,
                    CampusNpcFacilitySelector.KeyFor(assignedRoom, assignedCheckout),
                    CampusNpcFacilitySelector.PositionOf(assignedCheckout));
                CampusNpcFacilitySelector.AddPositions(
                    assignedRoom,
                    CampusNpcFacilityGroups.StoreWorkstations,
                    profile.SecondaryWorkstationPositions);
                CampusNpcFacilitySelector.AddPositions(
                    assignedRoom,
                    CampusNpcFacilityGroups.Shelves,
                    profile.ShelfPositions);
                return;
            }

            CampusGameplayRoom store = CampusNpcRoomSelector.Choose(
                CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Store),
                data != null ? data.Id : string.Empty,
                staffIndex);
            if (CampusNpcFacilitySelector.TryChoose(
                    store,
                    CampusNpcFacilityGroups.StoreWorkstations,
                    staffIndex,
                    out CampusGameplayRoom.FacilityRecord checkout))
            {
                profile.SetPrimaryWorkstation(
                    store,
                    CampusNpcFacilitySelector.KeyFor(store, checkout),
                    CampusNpcFacilitySelector.PositionOf(checkout));
            }
            else
            {
                profile.SetPrimaryWorkstation(
                    store,
                    string.Empty,
                    CampusNpcRoomSelector.PointNearCenter(store, staffIndex, 0.25f));
            }

            CampusNpcFacilitySelector.AddPositions(
                store,
                CampusNpcFacilityGroups.StoreWorkstations,
                profile.SecondaryWorkstationPositions);
            CampusNpcFacilitySelector.AddPositions(
                store,
                CampusNpcFacilityGroups.Shelves,
                profile.ShelfPositions);
        }

        private static void BuildDeliveryStaffProfile(
            CampusNpcPersonalProfile profile,
            CampusCharacterData data,
            CampusWorldService worldService,
            int staffIndex)
        {
            string assignedPointId = data != null && !string.IsNullOrWhiteSpace(data.Assignments.DeliveryPointId)
                ? data.Assignments.DeliveryPointId
                : data != null ? data.Assignments.PrimaryWorkstationId : string.Empty;
            if (CampusNpcFacilitySelector.FindAssigned(
                    worldService,
                    assignedPointId,
                    CampusNpcFacilityGroups.DeliveryPoints,
                    out CampusGameplayRoom assignedRoom,
                    out CampusGameplayRoom.FacilityRecord assignedPoint))
            {
                UnityEngine.Vector3 assignedPosition = CampusNpcFacilitySelector.PositionOf(assignedPoint);
                string key = CampusNpcFacilitySelector.KeyFor(assignedRoom, assignedPoint);
                profile.SetPrimaryWorkstation(assignedRoom, key, assignedPosition);
                profile.SetDeliveryPoint(assignedRoom, key, assignedPosition);
                return;
            }

            CampusGameplayRoom outdoor = CampusNpcRoomSelector.Choose(
                CampusNpcRoomSelector.GetRooms(worldService, CampusRoomType.Outdoor),
                data != null ? data.Id : string.Empty,
                staffIndex);
            if (CampusNpcFacilitySelector.TryChoose(
                    outdoor,
                    CampusNpcFacilityGroups.DeliveryPoints,
                    staffIndex,
                    out CampusGameplayRoom.FacilityRecord point))
            {
                profile.SetPrimaryWorkstation(
                    outdoor,
                    CampusNpcFacilitySelector.KeyFor(outdoor, point),
                    CampusNpcFacilitySelector.PositionOf(point));
                profile.SetDeliveryPoint(
                    outdoor,
                    CampusNpcFacilitySelector.KeyFor(outdoor, point),
                    CampusNpcFacilitySelector.PositionOf(point));
            }
            else
            {
                UnityEngine.Vector3 fallback = CampusNpcRoomSelector.PointNearCenter(outdoor, staffIndex, 0.25f);
                profile.SetPrimaryWorkstation(outdoor, string.Empty, fallback);
                profile.SetDeliveryPoint(outdoor, string.Empty, fallback);
            }
        }
    }
}
