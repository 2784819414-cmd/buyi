using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [Serializable]
    public sealed class CampusGameplayServiceStationSlotBinding
    {
        [SerializeField] private string roleId = string.Empty;
        [SerializeField] private List<string> facilityIds = new List<string>();

        public string RoleId => roleId;
        public IReadOnlyList<string> FacilityIds => facilityIds;

        public void Configure(string targetRoleId, IEnumerable<string> targetFacilityIds)
        {
            roleId = NormalizeId(targetRoleId);
            facilityIds = facilityIds ?? new List<string>();
            facilityIds.Clear();
            if (targetFacilityIds == null)
            {
                return;
            }

            foreach (string facilityId in targetFacilityIds)
            {
                string normalized = NormalizeId(facilityId);
                if (!string.IsNullOrEmpty(normalized))
                {
                    facilityIds.Add(normalized);
                }
            }
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [DisallowMultipleComponent]
    public sealed class CampusGameplayServiceStationMarker : MonoBehaviour
    {
        [SerializeField] private string stationId = string.Empty;
        [SerializeField] private string stationTypeId = string.Empty;
        [SerializeField] private string roomId = string.Empty;
        [SerializeField] private string ownerFacilityId = string.Empty;
        [SerializeField] private List<CampusGameplayServiceStationSlotBinding> slots =
            new List<CampusGameplayServiceStationSlotBinding>();

        public string StationId => stationId;
        public string StationTypeId => stationTypeId;
        public string RoomId => roomId;
        public string OwnerFacilityId => ownerFacilityId;
        public IReadOnlyList<CampusGameplayServiceStationSlotBinding> Slots => slots;

        public void Configure(
            string targetStationId,
            string targetStationTypeId,
            string targetRoomId,
            string targetOwnerFacilityId,
            IReadOnlyList<CampusGameplayServiceStationSlotBinding> targetSlots)
        {
            stationId = NormalizeId(targetStationId);
            stationTypeId = NormalizeId(targetStationTypeId);
            roomId = NormalizeId(targetRoomId);
            ownerFacilityId = NormalizeId(targetOwnerFacilityId);
            slots = slots ?? new List<CampusGameplayServiceStationSlotBinding>();
            slots.Clear();
            if (targetSlots == null)
            {
                return;
            }

            for (int i = 0; i < targetSlots.Count; i++)
            {
                CampusGameplayServiceStationSlotBinding source = targetSlots[i];
                if (source == null || string.IsNullOrWhiteSpace(source.RoleId))
                {
                    continue;
                }

                CampusGameplayServiceStationSlotBinding clone = new CampusGameplayServiceStationSlotBinding();
                clone.Configure(source.RoleId, source.FacilityIds);
                slots.Add(clone);
            }
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
