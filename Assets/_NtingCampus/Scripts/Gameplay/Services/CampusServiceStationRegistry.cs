using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Services
{
    internal sealed class CampusServiceStationRegistry
    {
        private readonly List<CampusServiceStation> stations = new List<CampusServiceStation>();
        private readonly Dictionary<string, CampusServiceStation> stationsById =
            new Dictionary<string, CampusServiceStation>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<CampusServiceStation>> stationsByFacilityId =
            new Dictionary<string, List<CampusServiceStation>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<CampusCharacterRuntime>> presentOperatorsByStationId =
            new Dictionary<string, List<CampusCharacterRuntime>>(StringComparer.OrdinalIgnoreCase);

        private int operatorPresenceFrame = -1;
        private CampusRosterService operatorPresenceRoster;

        public IReadOnlyList<CampusServiceStation> Stations => stations;

        public void Rebuild(IReadOnlyList<CampusGameplayRoom> rooms)
        {
            stations.Clear();
            stationsById.Clear();
            stationsByFacilityId.Clear();
            ClearOperatorPresence();

            if (rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                List<CampusServiceStation> roomStations = CampusServiceStationCatalog.Collect(room);
                for (int stationIndex = 0; stationIndex < roomStations.Count; stationIndex++)
                {
                    Register(roomStations[stationIndex]);
                }
            }

            stations.Sort(CompareStations);
        }

        public List<CampusServiceStation> Find(
            string interactionActionId,
            IReadOnlyList<string> stationTypeIds,
            CampusRoomType roomType)
        {
            List<CampusServiceStation> matches = new List<CampusServiceStation>();
            string normalizedActionId = CampusInteractionActionIds.Normalize(interactionActionId);

            for (int i = 0; i < stations.Count; i++)
            {
                CampusServiceStation station = stations[i];
                if (roomType != CampusRoomType.Unknown &&
                    (station.Room == null || station.Room.RoomType != roomType))
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

                matches.Add(station);
            }

            return matches;
        }

        public List<CampusServiceStation> FindInRoom(
            CampusGameplayRoom room,
            string interactionActionId,
            IReadOnlyList<string> stationTypeIds)
        {
            List<CampusServiceStation> matches = new List<CampusServiceStation>();
            if (room == null)
            {
                return matches;
            }

            string roomId = NormalizeId(room.RoomId);
            string normalizedActionId = CampusInteractionActionIds.Normalize(interactionActionId);
            for (int i = 0; i < stations.Count; i++)
            {
                CampusServiceStation station = stations[i];
                if (station.Room == null ||
                    !string.Equals(station.Room.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
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

                matches.Add(station);
            }

            return matches;
        }

        public bool TryResolveById(string stationId, out CampusServiceStation station)
        {
            return stationsById.TryGetValue(NormalizeId(stationId), out station);
        }

        public bool TryResolveByFacility(
            CampusGameplayRoom.FacilityRecord facility,
            out CampusServiceStation station)
        {
            station = default;
            if (facility == null ||
                !stationsByFacilityId.TryGetValue(
                    NormalizeId(facility.FacilityId),
                    out List<CampusServiceStation> matches) ||
                matches == null ||
                matches.Count == 0)
            {
                return false;
            }

            station = matches[0];
            return true;
        }

        public bool TryResolveByPlacedObject(
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
            if (room == null || room.Facilities == null)
            {
                return false;
            }

            for (int i = 0; i < room.Facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = room.Facilities[i];
                if (facility != null && facility.PlacedObject == placedObject)
                {
                    return TryResolveByFacility(facility, out station);
                }
            }

            return false;
        }

        public bool TryResolvePresentOperator(
            CampusServiceStation station,
            CampusRosterService rosterService,
            out CampusCharacterRuntime runtime)
        {
            runtime = null;
            RefreshOperatorPresence(rosterService);
            if (string.IsNullOrWhiteSpace(station.StationId) ||
                !presentOperatorsByStationId.TryGetValue(station.StationId, out List<CampusCharacterRuntime> operators) ||
                operators == null ||
                operators.Count == 0)
            {
                return false;
            }

            runtime = operators[0];
            return runtime != null;
        }

        private void Register(CampusServiceStation station)
        {
            if (!station.IsOperational || string.IsNullOrWhiteSpace(station.StationId))
            {
                return;
            }

            if (stationsById.ContainsKey(station.StationId))
            {
                return;
            }

            stations.Add(station);
            stationsById.Add(station.StationId, station);
            IndexFacility(station.InteractionFacility, station);
            IndexFacility(station.OperatorSlot, station);
            IndexFacility(station.CustomerSlot, station);
            IndexFacility(station.OutputSlot, station);
            for (int i = 0; i < station.QueueSlots.Count; i++)
            {
                IndexFacility(station.QueueSlots[i], station);
            }
        }

        private void IndexFacility(
            CampusGameplayRoom.FacilityRecord facility,
            CampusServiceStation station)
        {
            string facilityId = NormalizeId(facility != null ? facility.FacilityId : string.Empty);
            if (string.IsNullOrEmpty(facilityId))
            {
                return;
            }

            if (!stationsByFacilityId.TryGetValue(facilityId, out List<CampusServiceStation> matches))
            {
                matches = new List<CampusServiceStation>();
                stationsByFacilityId.Add(facilityId, matches);
            }

            matches.Add(station);
        }

        private void RefreshOperatorPresence(CampusRosterService rosterService)
        {
            int frame = Time.frameCount;
            if (operatorPresenceFrame == frame && operatorPresenceRoster == rosterService)
            {
                return;
            }

            ClearOperatorPresence();
            operatorPresenceFrame = frame;
            operatorPresenceRoster = rosterService;

            IReadOnlyList<CampusCharacterRuntime> runtimes = rosterService != null
                ? rosterService.Index.Runtimes
                : Array.Empty<CampusCharacterRuntime>();
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                CampusCharacterAssignmentData assignments =
                    runtime != null && runtime.Data != null ? runtime.Data.Assignments : null;
                if (assignments == null ||
                    !TryResolveById(assignments.ServiceStationId, out CampusServiceStation station) ||
                    !IsAssignedOperator(assignments, station) ||
                    !IsRuntimeAtOperatorTarget(runtime, station))
                {
                    continue;
                }

                List<CampusCharacterRuntime> operators =
                    GetOrCreateOperatorList(station.StationId);
                operators.Add(runtime);
            }
        }

        private void ClearOperatorPresence()
        {
            presentOperatorsByStationId.Clear();
            operatorPresenceFrame = -1;
            operatorPresenceRoster = null;
        }

        private static bool IsAssignedOperator(
            CampusCharacterAssignmentData assignments,
            CampusServiceStation station)
        {
            if (assignments == null || station.Availability == null)
            {
                return false;
            }

            string requiredRoleId = NormalizeId(station.Availability.OperatorSlotRoleId);
            if (string.IsNullOrEmpty(requiredRoleId))
            {
                requiredRoleId = CampusServiceStationSlotRoleIds.Operator;
            }

            return string.Equals(
                NormalizeId(assignments.ServiceStationSlotRoleId),
                requiredRoleId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRuntimeAtOperatorTarget(
            CampusCharacterRuntime runtime,
            CampusServiceStation station)
        {
            if (runtime == null || station.Availability == null)
            {
                return false;
            }

            CampusGameplayRoom.FacilityRecord target = station.HasOperatorSlot
                ? station.OperatorSlot
                : station.InteractionFacility;
            if (target == null)
            {
                return false;
            }

            float radius = Mathf.Max(0.05f, station.Availability.OperatorActivationRadius);
            Vector2 runtimePosition = runtime.transform.position;
            Vector2 targetPosition = CampusServiceStation.PositionOf(target);
            return Vector2.SqrMagnitude(runtimePosition - targetPosition) <= radius * radius;
        }

        private List<CampusCharacterRuntime> GetOrCreateOperatorList(string stationId)
        {
            string normalized = NormalizeId(stationId);
            if (!presentOperatorsByStationId.TryGetValue(
                    normalized,
                    out List<CampusCharacterRuntime> operators))
            {
                operators = new List<CampusCharacterRuntime>();
                presentOperatorsByStationId.Add(normalized, operators);
            }

            return operators;
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

        private static int CompareStations(CampusServiceStation left, CampusServiceStation right)
        {
            return string.Compare(left.StationId, right.StationId, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
