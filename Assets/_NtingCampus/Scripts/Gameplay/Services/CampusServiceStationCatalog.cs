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
            string ownerFacilityId,
            string stationId,
            string interactionActionId,
            CampusGameplayRoom.FacilityRecord interactionFacility,
            CampusGameplayRoom.FacilityRecord operatorSlot,
            CampusGameplayRoom.FacilityRecord customerSlot,
            List<CampusGameplayRoom.FacilityRecord> queueSlots)
        {
            Room = room;
            OwnerFacilityId = ownerFacilityId ?? string.Empty;
            StationId = stationId ?? string.Empty;
            InteractionActionId = interactionActionId ?? string.Empty;
            InteractionFacility = interactionFacility;
            OperatorSlot = operatorSlot;
            CustomerSlot = customerSlot;
            QueueSlots = queueSlots ?? new List<CampusGameplayRoom.FacilityRecord>();
        }

        public CampusGameplayRoom Room { get; }
        public string OwnerFacilityId { get; }
        public string StationId { get; }
        public string InteractionActionId { get; }
        public CampusGameplayRoom.FacilityRecord InteractionFacility { get; }
        public CampusGameplayRoom.FacilityRecord OperatorSlot { get; }
        public CampusGameplayRoom.FacilityRecord CustomerSlot { get; }
        public IReadOnlyList<CampusGameplayRoom.FacilityRecord> QueueSlots { get; }

        public bool HasInteractionFacility => InteractionFacility != null;
        public bool HasOperatorSlot => OperatorSlot != null;
        public bool HasCustomerSlot => CustomerSlot != null;
        public bool IsOperational => HasInteractionFacility && HasOperatorSlot;

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
            string interactionActionId = "")
        {
            List<CampusServiceStation> stations = new List<CampusServiceStation>();
            if (room == null)
            {
                return stations;
            }

            string normalizedActionId = CampusInteractionActionIds.Normalize(interactionActionId);
            Dictionary<string, StationBuilder> buildersByOwnerId =
                new Dictionary<string, StationBuilder>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = room.Facilities;

            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = facilities[i];
                if (!TryResolveInteractionActionId(facility, out string actionId) ||
                    (!string.IsNullOrEmpty(normalizedActionId) &&
                     !CampusInteractionActionIds.Equals(actionId, normalizedActionId)))
                {
                    continue;
                }

                string ownerFacilityId = ResolveFacilityKey(room, facility);
                if (string.IsNullOrEmpty(ownerFacilityId))
                {
                    continue;
                }

                StationBuilder builder = GetOrCreateBuilder(buildersByOwnerId, room, ownerFacilityId);
                builder.SetInteractionFacility(facility, actionId);
                buildersByOwnerId[ownerFacilityId] = builder;
            }

            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = facilities[i];
                string ownerFacilityId = NormalizeOwnerFacilityId(
                    facility != null ? facility.OwnerFacilityId : string.Empty);
                if (string.IsNullOrEmpty(ownerFacilityId) ||
                    !buildersByOwnerId.TryGetValue(ownerFacilityId, out StationBuilder builder))
                {
                    continue;
                }

                builder.AddSupportFacility(facility);
                buildersByOwnerId[ownerFacilityId] = builder;
            }

            foreach (StationBuilder builder in buildersByOwnerId.Values)
            {
                if (!builder.HasInteractionFacility)
                {
                    continue;
                }

                stations.Add(builder.Build());
            }

            stations.Sort(CompareStations);
            return stations;
        }

        public static bool TryResolveByFacility(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord facility,
            out CampusServiceStation station)
        {
            station = default;
            if (room == null || facility == null)
            {
                return false;
            }

            string ownerFacilityId = ResolveOwnerFacilityId(room, facility);
            if (string.IsNullOrEmpty(ownerFacilityId))
            {
                return false;
            }

            List<CampusServiceStation> stations = Collect(room);
            for (int i = 0; i < stations.Count; i++)
            {
                CampusServiceStation candidate = stations[i];
                if (string.Equals(
                        candidate.OwnerFacilityId,
                        ownerFacilityId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    station = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveByPlacedObject(
            CampusWorldService worldService,
            CampusPlacedObject placedObject,
            out CampusServiceStation station)
        {
            station = default;
            if (worldService == null || placedObject == null)
            {
                return false;
            }

            CampusGameplayRoom room = worldService.FindRoomForPosition(
                placedObject.FloorIndex,
                placedObject.transform.position);
            if (room == null)
            {
                return false;
            }

            IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = room.Facilities;
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = facilities[i];
                if (facility == null || facility.PlacedObject != placedObject)
                {
                    continue;
                }

                return TryResolveByFacility(room, facility, out station);
            }

            return false;
        }

        public static bool TryResolveInteractionActionId(
            CampusGameplayRoom.FacilityRecord facility,
            out string actionId)
        {
            actionId = string.Empty;
            if (facility == null)
            {
                return false;
            }

            CampusPlacedObject placedObject = facility.PlacedObject;
            if (placedObject != null &&
                placedObject.TryGetComponent(out CampusSimpleInteractable handler) &&
                !string.IsNullOrWhiteSpace(handler.DefaultActionId))
            {
                actionId = CampusInteractionActionIds.Normalize(handler.DefaultActionId);
                return !string.IsNullOrEmpty(actionId);
            }

            if (placedObject != null)
            {
                CampusInteractionAnchor anchor = placedObject.GetComponent<CampusInteractionAnchor>();
                if (anchor == null)
                {
                    anchor = placedObject.GetComponentInChildren<CampusInteractionAnchor>(true);
                }

                if (anchor != null && !string.IsNullOrWhiteSpace(anchor.ActionId))
                {
                    actionId = CampusInteractionActionIds.Normalize(anchor.ActionId);
                    return !string.IsNullOrEmpty(actionId);
                }
            }

            actionId = ResolveFallbackInteractionActionId(facility.FacilityType);
            return !string.IsNullOrEmpty(actionId);
        }

        private static string ResolveOwnerFacilityId(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord facility)
        {
            if (facility == null)
            {
                return string.Empty;
            }

            string ownerFacilityId = NormalizeOwnerFacilityId(facility.OwnerFacilityId);
            if (!string.IsNullOrEmpty(ownerFacilityId))
            {
                return ownerFacilityId;
            }

            return TryResolveInteractionActionId(facility, out _)
                ? ResolveFacilityKey(room, facility)
                : string.Empty;
        }

        private static string ResolveFacilityKey(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord facility)
        {
            if (room == null || facility == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(facility.FacilityId))
            {
                return facility.FacilityId.Trim();
            }

            return CampusGameplayFacilityMarker.BuildStableFacilityId(
                room.FloorIndex,
                facility.FacilityType,
                facility.Cell);
        }

        private static string ResolveFallbackInteractionActionId(CampusFacilityType facilityType)
        {
            switch (facilityType)
            {
                case CampusFacilityType.ServiceWindow:
                    return CampusInteractionActionIds.ServiceWindowUse;
                default:
                    return string.Empty;
            }
        }

        private static string NormalizeOwnerFacilityId(string value)
        {
            return CampusGameplayFacilityMarker.NormalizeOwnerFacilityId(value);
        }

        private static StationBuilder GetOrCreateBuilder(
            Dictionary<string, StationBuilder> buildersByOwnerId,
            CampusGameplayRoom room,
            string ownerFacilityId)
        {
            if (!buildersByOwnerId.TryGetValue(ownerFacilityId, out StationBuilder builder))
            {
                builder = new StationBuilder(room, ownerFacilityId);
            }

            return builder;
        }

        private static int CompareStations(CampusServiceStation left, CampusServiceStation right)
        {
            int compare = CompareFacilities(left.InteractionFacility, right.InteractionFacility);
            return compare != 0
                ? compare
                : string.Compare(
                    left.OwnerFacilityId,
                    right.OwnerFacilityId,
                    StringComparison.OrdinalIgnoreCase);
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

        private struct StationBuilder
        {
            private readonly CampusGameplayRoom room;
            private readonly string ownerFacilityId;
            private string interactionActionId;
            private CampusGameplayRoom.FacilityRecord interactionFacility;
            private CampusGameplayRoom.FacilityRecord operatorSlot;
            private CampusGameplayRoom.FacilityRecord customerSlot;
            private List<CampusGameplayRoom.FacilityRecord> queueSlots;

            public StationBuilder(CampusGameplayRoom room, string ownerFacilityId)
            {
                this.room = room;
                this.ownerFacilityId = ownerFacilityId ?? string.Empty;
                interactionActionId = string.Empty;
                interactionFacility = null;
                operatorSlot = null;
                customerSlot = null;
                queueSlots = new List<CampusGameplayRoom.FacilityRecord>();
            }

            public bool HasInteractionFacility => interactionFacility != null;

            public void SetInteractionFacility(
                CampusGameplayRoom.FacilityRecord facility,
                string actionId)
            {
                interactionFacility = facility;
                interactionActionId = actionId ?? string.Empty;
            }

            public void AddSupportFacility(CampusGameplayRoom.FacilityRecord facility)
            {
                if (facility == null)
                {
                    return;
                }

                switch (facility.FacilityType)
                {
                    case CampusFacilityType.WorkerStandPoint:
                        operatorSlot = facility;
                        break;
                    case CampusFacilityType.PickupPoint:
                        customerSlot = facility;
                        break;
                    case CampusFacilityType.WaitingPoint:
                        queueSlots.Add(facility);
                        queueSlots.Sort(CompareFacilities);
                        break;
                }
            }

            public CampusServiceStation Build()
            {
                string stationId = !string.IsNullOrWhiteSpace(interactionFacility != null ? interactionFacility.LegacyServiceStationId : string.Empty)
                    ? interactionFacility.LegacyServiceStationId.Trim()
                    : ownerFacilityId;
                return new CampusServiceStation(
                    room,
                    ownerFacilityId,
                    stationId,
                    interactionActionId,
                    interactionFacility,
                    operatorSlot,
                    customerSlot,
                    queueSlots);
            }
        }
    }
}
