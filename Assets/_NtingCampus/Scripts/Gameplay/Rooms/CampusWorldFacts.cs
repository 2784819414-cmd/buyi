using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public sealed class CampusWorldFacts
    {
        public sealed class RoomFact
        {
            public string RoomId = string.Empty;
            public string DisplayName = string.Empty;
            public CampusLocalizedText LocalizedDisplayName = default;
            public CampusRoomType RoomType = CampusRoomType.Unknown;
            public CampusRoomTypeSource RoomTypeSource = CampusRoomTypeSource.Unknown;
            public int FloorIndex = 1;
            public BoundsInt Bounds;
            public Vector3 WorldCenter;
            public bool IsUsableForGameplay;
            public bool HasExplicitRoomType;

            public string GetDisplayName(CampusDisplayLanguage language)
            {
                if (LocalizedDisplayName.HasAnyText)
                {
                    return LocalizedDisplayName.Get(language, DisplayName);
                }

                return string.IsNullOrWhiteSpace(DisplayName)
                    ? CampusRoomTextCatalog.Get(language, RoomType)
                    : DisplayName.Trim();
            }
        }

        public sealed class FacilityFact
        {
            public string FacilityId = string.Empty;
            public string DisplayName = string.Empty;
            public CampusFacilityType FacilityType = CampusFacilityType.Unknown;
            public CampusFacilityTypeSource FacilityTypeSource = CampusFacilityTypeSource.Unknown;
            public string FacilityTypeDiagnostic = string.Empty;
            public string RoomId = string.Empty;
            public CampusRoomType RoomType = CampusRoomType.Unknown;
            public int FloorIndex = 1;
            public Vector3Int Cell;
            public bool HasPlacedObject;
            public bool HasExplicitFacilityType;
        }

        public sealed class ServiceStationSlotFact
        {
            public string RoleId = string.Empty;
            public List<string> FacilityIds = new List<string>();
        }

        public sealed class ServiceStationFact
        {
            public string StationId = string.Empty;
            public string StationTypeId = string.Empty;
            public string RoomId = string.Empty;
            public string OwnerFacilityId = string.Empty;
            public List<ServiceStationSlotFact> Slots = new List<ServiceStationSlotFact>();
        }

        public sealed class ActorFact
        {
            public string ActorId = string.Empty;
            public string DisplayName = string.Empty;
            public CampusCharacterRole Role = CampusCharacterRole.Student;
            public CampusTeacherDuty TeacherDuty = CampusTeacherDuty.None;
            public CampusStaffDuty StaffDuty = CampusStaffDuty.None;
            public string ClassId = string.Empty;
            public string CurrentRoomId = string.Empty;
            public bool IsPlayerControlled;
            public CampusCharacterAssignmentData Assignments = new CampusCharacterAssignmentData();
        }

        private readonly List<RoomFact> rooms = new List<RoomFact>();
        private readonly List<FacilityFact> facilities = new List<FacilityFact>();
        private readonly List<ServiceStationFact> serviceStations = new List<ServiceStationFact>();
        private readonly List<ActorFact> actors = new List<ActorFact>();
        private readonly Dictionary<string, RoomFact> roomsById =
            new Dictionary<string, RoomFact>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FacilityFact> facilitiesById =
            new Dictionary<string, FacilityFact>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ServiceStationFact> serviceStationsById =
            new Dictionary<string, ServiceStationFact>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ActorFact> actorsById =
            new Dictionary<string, ActorFact>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<RoomFact> Rooms => rooms;
        public IReadOnlyList<FacilityFact> Facilities => facilities;
        public IReadOnlyList<ServiceStationFact> ServiceStations => serviceStations;
        public IReadOnlyList<ActorFact> Actors => actors;

        public static CampusWorldFacts Build(CampusWorldService worldService, CampusRosterService rosterService)
        {
            CampusWorldFacts facts = new CampusWorldFacts();
            CampusRoomRegistry registry = worldService != null ? worldService.RoomRegistry : null;
            if (registry != null && registry.Rooms != null)
            {
                for (int i = 0; i < registry.Rooms.Count; i++)
                {
                    facts.AddRoom(registry.Rooms[i]);
                }
            }

            if (rosterService != null && rosterService.Runtimes != null)
            {
                for (int i = 0; i < rosterService.Runtimes.Count; i++)
                {
                    facts.AddActor(rosterService.Runtimes[i]);
                }
            }

            return facts;
        }

        public bool TryGetRoom(string roomId, out RoomFact room)
        {
            return roomsById.TryGetValue(NormalizeId(roomId), out room);
        }

        public bool TryGetFacility(string facilityId, out FacilityFact facility)
        {
            return facilitiesById.TryGetValue(NormalizeId(facilityId), out facility);
        }

        public bool TryGetActor(string actorId, out ActorFact actor)
        {
            return actorsById.TryGetValue(NormalizeId(actorId), out actor);
        }

        public bool TryGetServiceStation(string stationId, out ServiceStationFact station)
        {
            return serviceStationsById.TryGetValue(NormalizeId(stationId), out station);
        }

        public int CountRooms(CampusRoomType roomType)
        {
            int count = 0;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] != null && rooms[i].RoomType == roomType)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountActors(CampusCharacterRole role)
        {
            int count = 0;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null && actors[i].Role == role)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountFacilities(CampusFacilityType facilityType)
        {
            int count = 0;
            for (int i = 0; i < facilities.Count; i++)
            {
                if (facilities[i] != null && facilities[i].FacilityType == facilityType)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountFacilitiesInRoom(string roomId, params CampusFacilityType[] facilityTypes)
        {
            string normalizedRoomId = NormalizeId(roomId);
            if (string.IsNullOrEmpty(normalizedRoomId) || facilityTypes == null || facilityTypes.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < facilities.Count; i++)
            {
                FacilityFact facility = facilities[i];
                if (facility == null ||
                    !string.Equals(facility.RoomId, normalizedRoomId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (int typeIndex = 0; typeIndex < facilityTypes.Length; typeIndex++)
                {
                    if (facility.FacilityType == facilityTypes[typeIndex])
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void AddRoom(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return;
            }

            RoomFact fact = new RoomFact
            {
                RoomId = NormalizeId(room.RoomId),
                DisplayName = room.GetPrimaryDisplayName(),
                LocalizedDisplayName = room.LocalizedDisplayName,
                RoomType = room.RoomType,
                RoomTypeSource = room.RoomTypeSource,
                FloorIndex = Mathf.Max(1, room.FloorIndex),
                Bounds = room.MarkerBounds,
                WorldCenter = room.WorldCenter,
                IsUsableForGameplay = room.IsUsableForGameplay,
                HasExplicitRoomType = room.HasExplicitRoomType
            };

            rooms.Add(fact);
            if (!string.IsNullOrEmpty(fact.RoomId) && !roomsById.ContainsKey(fact.RoomId))
            {
                roomsById.Add(fact.RoomId, fact);
            }

            IReadOnlyList<CampusGameplayRoom.FacilityRecord> sourceFacilities = room.Facilities;
            if (sourceFacilities == null)
            {
                return;
            }

            for (int i = 0; i < sourceFacilities.Count; i++)
            {
                AddFacility(room, sourceFacilities[i]);
            }

            IReadOnlyList<CampusGameplayServiceStationRecord> sourceStations = room.ServiceStations;
            if (sourceStations == null)
            {
                return;
            }

            for (int i = 0; i < sourceStations.Count; i++)
            {
                AddServiceStation(room, sourceStations[i]);
            }
        }

        private void AddFacility(CampusGameplayRoom room, CampusGameplayRoom.FacilityRecord facility)
        {
            if (room == null || facility == null)
            {
                return;
            }

            FacilityFact fact = new FacilityFact
            {
                FacilityId = NormalizeId(facility.FacilityId),
                DisplayName = string.IsNullOrWhiteSpace(facility.DisplayName)
                    ? facility.FacilityType.ToString()
                    : facility.DisplayName.Trim(),
                FacilityType = facility.FacilityType,
                FacilityTypeSource = facility.FacilityTypeSource,
                FacilityTypeDiagnostic = facility.FacilityTypeDiagnostic,
                RoomId = NormalizeId(room.RoomId),
                RoomType = room.RoomType,
                FloorIndex = Mathf.Max(1, room.FloorIndex),
                Cell = facility.Cell,
                HasPlacedObject = facility.PlacedObject != null,
                HasExplicitFacilityType = facility.HasExplicitFacilityType
            };

            facilities.Add(fact);
            if (!string.IsNullOrEmpty(fact.FacilityId) && !facilitiesById.ContainsKey(fact.FacilityId))
            {
                facilitiesById.Add(fact.FacilityId, fact);
            }
        }

        private void AddServiceStation(
            CampusGameplayRoom room,
            CampusGameplayServiceStationRecord station)
        {
            if (room == null || station == null)
            {
                return;
            }

            ServiceStationFact fact = new ServiceStationFact
            {
                StationId = NormalizeId(station.StationId),
                StationTypeId = NormalizeId(station.StationTypeId),
                RoomId = NormalizeId(room.RoomId),
                OwnerFacilityId = NormalizeId(station.OwnerFacilityId),
                Slots = BuildSlotFacts(station.Slots)
            };

            serviceStations.Add(fact);
            if (!string.IsNullOrEmpty(fact.StationId) && !serviceStationsById.ContainsKey(fact.StationId))
            {
                serviceStationsById.Add(fact.StationId, fact);
            }
        }

        private static List<ServiceStationSlotFact> BuildSlotFacts(
            IReadOnlyList<CampusGameplayServiceStationSlotBinding> slots)
        {
            List<ServiceStationSlotFact> facts = new List<ServiceStationSlotFact>();
            if (slots == null)
            {
                return facts;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                CampusGameplayServiceStationSlotBinding slot = slots[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.RoleId))
                {
                    continue;
                }

                ServiceStationSlotFact fact = new ServiceStationSlotFact
                {
                    RoleId = NormalizeId(slot.RoleId),
                    FacilityIds = new List<string>()
                };
                if (slot.FacilityIds != null)
                {
                    for (int facilityIndex = 0; facilityIndex < slot.FacilityIds.Count; facilityIndex++)
                    {
                        string facilityId = NormalizeId(slot.FacilityIds[facilityIndex]);
                        if (!string.IsNullOrEmpty(facilityId))
                        {
                            fact.FacilityIds.Add(facilityId);
                        }
                    }
                }

                facts.Add(fact);
            }

            return facts;
        }

        private void AddActor(CampusCharacterRuntime runtime)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            if (data == null)
            {
                return;
            }

            ActorFact fact = new ActorFact
            {
                ActorId = NormalizeId(data.Id),
                DisplayName = data.DisplayName,
                Role = data.Role,
                TeacherDuty = data.TeacherDuty,
                StaffDuty = data.StaffDuty,
                ClassId = NormalizeId(data.ClassId),
                CurrentRoomId = NormalizeId(data.CurrentRoomId),
                IsPlayerControlled = data.IsPlayerControlled,
                Assignments = data.Assignments != null ? data.Assignments.Clone() : new CampusCharacterAssignmentData()
            };

            actors.Add(fact);
            if (!string.IsNullOrEmpty(fact.ActorId) && !actorsById.ContainsKey(fact.ActorId))
            {
                actorsById.Add(fact.ActorId, fact);
            }
        }
    }

    internal static class CampusFacilityActivationFacts
    {
        public static bool HasNearbyQualifiedNpc(
            CampusPlacedObject sourceObject,
            float activationRadius,
            Func<CampusCharacterRuntime, bool> qualifiesNpc)
        {
            if (sourceObject == null || qualifiesNpc == null)
            {
                return false;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            CampusRosterService rosterService = bootstrap != null ? bootstrap.RosterService : null;
            IReadOnlyList<CampusCharacterRuntime> runtimes = rosterService != null
                ? rosterService.Runtimes
                : Array.Empty<CampusCharacterRuntime>();
            float radius = Mathf.Max(0.05f, activationRadius);
            float radiusSqr = radius * radius;
            int targetFloorIndex = Mathf.Max(1, sourceObject.FloorIndex);
            Vector3 targetPosition = sourceObject.transform.position;

            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (!MatchesQualifiedNpc(worldService, runtime, targetFloorIndex, qualifiesNpc))
                {
                    continue;
                }

                if (Vector2.SqrMagnitude((Vector2)(runtime.transform.position - targetPosition)) <= radiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesQualifiedNpc(
            CampusWorldService worldService,
            CampusCharacterRuntime runtime,
            int targetFloorIndex,
            Func<CampusCharacterRuntime, bool> qualifiesNpc)
        {
            if (runtime == null ||
                runtime.Data == null ||
                runtime.Data.IsPlayerControlled ||
                !qualifiesNpc(runtime))
            {
                return false;
            }

            CampusGameplayRoom currentRoom = worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
            if (currentRoom != null)
            {
                return currentRoom.FloorIndex == targetFloorIndex;
            }

            CampusSceneCharacterDefinition sceneCharacter = runtime.GetComponent<CampusSceneCharacterDefinition>();
            return sceneCharacter == null || Mathf.Max(1, sceneCharacter.FloorIndex) == targetFloorIndex;
        }
    }
}
