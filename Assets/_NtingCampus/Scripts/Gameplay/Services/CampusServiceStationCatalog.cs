using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Services
{
    internal readonly struct CampusServiceStation
    {
        public CampusServiceStation(
            CampusGameplayRoom room,
            string stationId,
            string stationTypeId,
            string interactionActionId,
            string availabilityRuleId,
            CampusServiceStationAvailabilityDefinition availability,
            CampusGameplayRoom.FacilityRecord interactionFacility,
            CampusGameplayRoom.FacilityRecord operatorSlot,
            CampusGameplayRoom.FacilityRecord customerSlot,
            CampusGameplayRoom.FacilityRecord outputSlot,
            List<CampusGameplayRoom.FacilityRecord> queueSlots,
            bool hasRequiredSlots)
        {
            Room = room;
            StationId = stationId ?? string.Empty;
            StationTypeId = stationTypeId ?? string.Empty;
            InteractionActionId = interactionActionId ?? string.Empty;
            AvailabilityRuleId = availabilityRuleId ?? string.Empty;
            Availability = availability ?? CampusServiceStationAvailabilityDefinition.Always;
            InteractionFacility = interactionFacility;
            OperatorSlot = operatorSlot;
            CustomerSlot = customerSlot;
            OutputSlot = outputSlot;
            QueueSlots = queueSlots ?? new List<CampusGameplayRoom.FacilityRecord>();
            HasRequiredSlots = hasRequiredSlots;
        }

        public CampusGameplayRoom Room { get; }
        public string StationId { get; }
        public string StationTypeId { get; }
        public string InteractionActionId { get; }
        public string AvailabilityRuleId { get; }
        public CampusServiceStationAvailabilityDefinition Availability { get; }
        public CampusGameplayRoom.FacilityRecord InteractionFacility { get; }
        public CampusGameplayRoom.FacilityRecord OperatorSlot { get; }
        public CampusGameplayRoom.FacilityRecord CustomerSlot { get; }
        public CampusGameplayRoom.FacilityRecord OutputSlot { get; }
        public IReadOnlyList<CampusGameplayRoom.FacilityRecord> QueueSlots { get; }
        public bool HasRequiredSlots { get; }

        public bool HasInteractionFacility => InteractionFacility != null;
        public bool HasOperatorSlot => OperatorSlot != null;
        public bool HasCustomerSlot => CustomerSlot != null;
        public bool HasOutputSlot => OutputSlot != null;
        public bool IsOperational => HasInteractionFacility && HasRequiredSlots;

        public Vector3 CustomerTargetPosition =>
            CustomerSlot != null
                ? PositionOf(CustomerSlot)
                : PositionOf(InteractionFacility);

        public static Vector3 PositionOf(CampusGameplayRoom.FacilityRecord record)
        {
            if (record == null)
            {
                return Vector3.zero;
            }

            return new Vector3(record.Cell.x + 0.5f, record.Cell.y + 0.5f, 0f);
        }
    }

    internal static class CampusServiceStationCatalog
    {
        public static List<CampusServiceStation> Collect(
            CampusGameplayRoom room,
            string interactionActionId = "",
            IReadOnlyList<string> stationTypeIds = null)
        {
            List<CampusServiceStation> stations = new List<CampusServiceStation>();
            if (room == null || room.ServiceStations == null)
            {
                return stations;
            }

            string normalizedActionId = CampusInteractionActionIds.Normalize(interactionActionId);
            for (int i = 0; i < room.ServiceStations.Count; i++)
            {
                CampusGameplayServiceStationRecord record = room.ServiceStations[i];
                if (!TryBuild(room, record, out CampusServiceStation station))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(normalizedActionId) &&
                    !CampusInteractionActionIds.Equals(station.InteractionActionId, normalizedActionId))
                {
                    continue;
                }

                if (!MatchesStationType(station.StationTypeId, stationTypeIds))
                {
                    continue;
                }

                stations.Add(station);
            }

            stations.Sort(CompareStations);
            return stations;
        }

        private static bool TryBuild(
            CampusGameplayRoom room,
            CampusGameplayServiceStationRecord record,
            out CampusServiceStation station)
        {
            station = default;
            if (room == null || record == null ||
                !CampusServiceStationPresetCatalog.TryResolve(
                    record.StationTypeId,
                    out CampusServiceStationTypeDefinition definition))
            {
                return false;
            }

            CampusGameplayRoom.FacilityRecord owner = FindFacility(room, record.OwnerFacilityId);
            if (owner == null)
            {
                return false;
            }

            List<CampusGameplayRoom.FacilityRecord> queueSlots =
                new List<CampusGameplayRoom.FacilityRecord>();
            Dictionary<string, List<CampusGameplayRoom.FacilityRecord>> slotsByRole =
                ResolveSlots(room, record);
            bool hasRequiredSlots = HasRequiredSlots(definition, slotsByRole);
            CampusGameplayRoom.FacilityRecord operatorSlot = FirstSlot(slotsByRole, CampusServiceStationSlotRoleIds.Operator);
            CampusGameplayRoom.FacilityRecord customerSlot = FirstSlot(slotsByRole, CampusServiceStationSlotRoleIds.Customer);
            CampusGameplayRoom.FacilityRecord outputSlot = FirstSlot(slotsByRole, CampusServiceStationSlotRoleIds.Output);
            AddSlots(slotsByRole, CampusServiceStationSlotRoleIds.Queue, queueSlots);

            station = new CampusServiceStation(
                room,
                record.StationId,
                record.StationTypeId,
                definition.InteractionActionId,
                definition.AvailabilityRuleId,
                definition.Availability,
                owner,
                operatorSlot,
                customerSlot,
                outputSlot,
                queueSlots,
                hasRequiredSlots);
            return true;
        }

        private static Dictionary<string, List<CampusGameplayRoom.FacilityRecord>> ResolveSlots(
            CampusGameplayRoom room,
            CampusGameplayServiceStationRecord record)
        {
            Dictionary<string, List<CampusGameplayRoom.FacilityRecord>> slotsByRole =
                new Dictionary<string, List<CampusGameplayRoom.FacilityRecord>>(StringComparer.OrdinalIgnoreCase);
            if (record == null || record.Slots == null)
            {
                return slotsByRole;
            }

            for (int i = 0; i < record.Slots.Count; i++)
            {
                CampusGameplayServiceStationSlotBinding binding = record.Slots[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.RoleId) || binding.FacilityIds == null)
                {
                    continue;
                }

                List<CampusGameplayRoom.FacilityRecord> slots = GetOrCreateSlots(slotsByRole, binding.RoleId);
                for (int facilityIndex = 0; facilityIndex < binding.FacilityIds.Count; facilityIndex++)
                {
                    CampusGameplayRoom.FacilityRecord facility = FindFacility(
                        room,
                        binding.FacilityIds[facilityIndex]);
                    if (facility != null && !ContainsFacility(slots, facility))
                    {
                        slots.Add(facility);
                    }
                }

                slots.Sort(CompareFacilities);
            }

            return slotsByRole;
        }

        private static bool HasRequiredSlots(
            CampusServiceStationTypeDefinition definition,
            Dictionary<string, List<CampusGameplayRoom.FacilityRecord>> slotsByRole)
        {
            if (definition == null || definition.Slots == null)
            {
                return true;
            }

            for (int i = 0; i < definition.Slots.Count; i++)
            {
                CampusServiceStationSlotDefinition slot = definition.Slots[i];
                if (slot == null || slot.MinCount <= 0)
                {
                    continue;
                }

                int count = slotsByRole != null &&
                            slotsByRole.TryGetValue(slot.RoleId, out List<CampusGameplayRoom.FacilityRecord> records) &&
                            records != null
                    ? records.Count
                    : 0;
                if (count < slot.MinCount)
                {
                    return false;
                }
            }

            return true;
        }

        private static CampusGameplayRoom.FacilityRecord FindFacility(
            CampusGameplayRoom room,
            string facilityId)
        {
            string normalized = NormalizeId(facilityId);
            if (room == null || string.IsNullOrEmpty(normalized) || room.Facilities == null)
            {
                return null;
            }

            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = room.Facilities[i];
                if (facility != null &&
                    string.Equals(facility.FacilityId, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return facility;
                }
            }

            return null;
        }

        private static List<CampusGameplayRoom.FacilityRecord> GetOrCreateSlots(
            Dictionary<string, List<CampusGameplayRoom.FacilityRecord>> slotsByRole,
            string roleId)
        {
            string normalized = NormalizeId(roleId);
            if (!slotsByRole.TryGetValue(normalized, out List<CampusGameplayRoom.FacilityRecord> slots))
            {
                slots = new List<CampusGameplayRoom.FacilityRecord>();
                slotsByRole[normalized] = slots;
            }

            return slots;
        }

        private static CampusGameplayRoom.FacilityRecord FirstSlot(
            Dictionary<string, List<CampusGameplayRoom.FacilityRecord>> slotsByRole,
            string roleId)
        {
            return slotsByRole != null &&
                   slotsByRole.TryGetValue(roleId, out List<CampusGameplayRoom.FacilityRecord> slots) &&
                   slots != null &&
                   slots.Count > 0
                ? slots[0]
                : null;
        }

        private static void AddSlots(
            Dictionary<string, List<CampusGameplayRoom.FacilityRecord>> slotsByRole,
            string roleId,
            List<CampusGameplayRoom.FacilityRecord> output)
        {
            if (slotsByRole == null ||
                output == null ||
                !slotsByRole.TryGetValue(roleId, out List<CampusGameplayRoom.FacilityRecord> slots) ||
                slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                output.Add(slots[i]);
            }
        }

        private static bool MatchesStationType(
            string stationTypeId,
            IReadOnlyList<string> allowedStationTypeIds)
        {
            if (allowedStationTypeIds == null || allowedStationTypeIds.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < allowedStationTypeIds.Count; i++)
            {
                if (string.Equals(
                        stationTypeId,
                        allowedStationTypeIds[i],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsFacility(
            IReadOnlyList<CampusGameplayRoom.FacilityRecord> records,
            CampusGameplayRoom.FacilityRecord target)
        {
            if (records == null || target == null)
            {
                return false;
            }

            for (int i = 0; i < records.Count; i++)
            {
                if (SameFacility(records[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SameFacility(
            CampusGameplayRoom.FacilityRecord left,
            CampusGameplayRoom.FacilityRecord right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.FacilityId, right.FacilityId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareStations(CampusServiceStation left, CampusServiceStation right)
        {
            int compare = CompareFacilities(left.InteractionFacility, right.InteractionFacility);
            return compare != 0
                ? compare
                : string.Compare(left.StationId, right.StationId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareFacilities(
            CampusGameplayRoom.FacilityRecord left,
            CampusGameplayRoom.FacilityRecord right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int xCompare = left.Cell.x.CompareTo(right.Cell.x);
            if (xCompare != 0)
            {
                return xCompare;
            }

            int yCompare = left.Cell.y.CompareTo(right.Cell.y);
            return yCompare != 0
                ? yCompare
                : left.FacilityType.CompareTo(right.FacilityType);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
