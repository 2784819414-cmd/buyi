using System.Collections.Generic;

namespace NtingCampus.Gameplay.Rooms
{
    public sealed class CampusGameplayServiceStationRecord
    {
        private readonly List<CampusGameplayServiceStationSlotBinding> slots =
            new List<CampusGameplayServiceStationSlotBinding>();

        public string StationId { get; private set; } = string.Empty;
        public string StationTypeId { get; private set; } = string.Empty;
        public string RoomId { get; private set; } = string.Empty;
        public string OwnerFacilityId { get; private set; } = string.Empty;
        public IReadOnlyList<CampusGameplayServiceStationSlotBinding> Slots => slots;

        public void Bind(
            string stationId,
            string stationTypeId,
            string roomId,
            string ownerFacilityId,
            IReadOnlyList<CampusGameplayServiceStationSlotBinding> sourceSlots)
        {
            StationId = CampusGameplayServiceStationMarker.NormalizeId(stationId);
            StationTypeId = CampusGameplayServiceStationMarker.NormalizeId(stationTypeId);
            RoomId = CampusGameplayServiceStationMarker.NormalizeId(roomId);
            OwnerFacilityId = CampusGameplayServiceStationMarker.NormalizeId(ownerFacilityId);
            slots.Clear();
            if (sourceSlots == null)
            {
                return;
            }

            for (int i = 0; i < sourceSlots.Count; i++)
            {
                CampusGameplayServiceStationSlotBinding source = sourceSlots[i];
                if (source == null || string.IsNullOrWhiteSpace(source.RoleId))
                {
                    continue;
                }

                CampusGameplayServiceStationSlotBinding clone = new CampusGameplayServiceStationSlotBinding();
                clone.Configure(source.RoleId, source.FacilityIds);
                slots.Add(clone);
            }
        }
    }
}
