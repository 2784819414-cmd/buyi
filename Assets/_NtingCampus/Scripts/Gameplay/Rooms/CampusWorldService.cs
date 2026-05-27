using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Services;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    public sealed class CampusWorldService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusRoomRegistry roomRegistry;
        [SerializeField] private bool rebuildRegistryOnInitialize = true;

        private readonly List<CampusEcologyValidator.ValidationIssue> ecologyValidationIssues =
            new List<CampusEcologyValidator.ValidationIssue>();
        private readonly CampusServiceStationRegistry serviceStations =
            new CampusServiceStationRegistry();
        private bool serviceStationsBuilt;
        private CampusWorldFacts cachedFacts;
        private CampusRosterService cachedFactsRoster;
        private int cachedFactsFrame = -1;

        public CampusRoomRegistry RoomRegistry => roomRegistry;
        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> EcologyValidationIssues => ecologyValidationIssues;
        internal CampusServiceStationRegistry ServiceStations
        {
            get
            {
                EnsureServiceStationRegistry();
                return serviceStations;
            }
        }

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            ResolveRoomRegistry();
            if (rebuildRegistryOnInitialize && roomRegistry != null)
            {
                RebuildRegistries();
            }
        }

        public void RebuildRegistries()
        {
            ResolveRoomRegistry();
            if (roomRegistry == null)
            {
                serviceStations.Rebuild(null);
                serviceStationsBuilt = true;
                InvalidateFacts();
                return;
            }

            roomRegistry.RebuildRegistry();
            serviceStations.Rebuild(roomRegistry.Rooms);
            serviceStationsBuilt = true;
            InvalidateFacts();
        }

        public CampusGameplayRoom FindFirstUsableRoom(CampusRoomType roomType)
        {
            return FindFirstRoom(roomType, true);
        }

        public List<CampusGameplayRoom> GetRoomsByType(CampusRoomType roomType, bool requireUsableForGameplay = false)
        {
            List<CampusGameplayRoom> matches = new List<CampusGameplayRoom>();
            if (roomRegistry == null || roomRegistry.Rooms == null)
            {
                return matches;
            }

            for (int i = 0; i < roomRegistry.Rooms.Count; i++)
            {
                CampusGameplayRoom room = roomRegistry.Rooms[i];
                if (room == null || room.RoomType != roomType)
                {
                    continue;
                }

                if (!requireUsableForGameplay || room.IsUsableForGameplay)
                {
                    matches.Add(room);
                }
            }

            return matches;
        }

        public CampusGameplayRoom FindFirstRoom(CampusRoomType roomType, bool requireUsableForGameplay = false)
        {
            if (roomRegistry == null || roomRegistry.Rooms == null)
            {
                return null;
            }

            for (int i = 0; i < roomRegistry.Rooms.Count; i++)
            {
                CampusGameplayRoom room = roomRegistry.Rooms[i];
                if (room == null || room.RoomType != roomType)
                {
                    continue;
                }

                if (!requireUsableForGameplay || room.IsUsableForGameplay)
                {
                    return room;
                }
            }

            return null;
        }

        public CampusGameplayRoom FindRoomById(string roomId)
        {
            if (roomRegistry == null || string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            return roomRegistry.TryGetRoom(roomId, out CampusGameplayRoom room) ? room : null;
        }

        public CampusGameplayRoom FindRoomForPosition(int floorIndex, Vector3 worldPosition)
        {
            if (roomRegistry == null)
            {
                return null;
            }

            Vector3Int cell = new Vector3Int(
                Mathf.FloorToInt(worldPosition.x),
                Mathf.FloorToInt(worldPosition.y),
                0);
            return roomRegistry.FindRoomByCell(floorIndex, cell);
        }

        public CampusGameplayRoom FindRoomForPlacedObject(CampusPlacedObject placedObject)
        {
            ResolveRoomRegistry();
            return roomRegistry != null &&
                   roomRegistry.TryFindRoomForPlacedObject(placedObject, out CampusGameplayRoom room)
                ? room
                : null;
        }

        public CampusGameplayRoom FindRoomForRuntime(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null || string.IsNullOrWhiteSpace(runtime.Data.CurrentRoomId))
            {
                return null;
            }

            return FindRoomById(runtime.Data.CurrentRoomId);
        }

        public CampusWorldFacts BuildFacts(CampusRosterService rosterService)
        {
            ResolveRoomRegistry();
            EnsureServiceStationRegistry();
            int frame = Time.frameCount;
            if (cachedFacts != null && cachedFactsRoster == rosterService && cachedFactsFrame == frame)
            {
                return cachedFacts;
            }

            cachedFacts = CampusWorldFacts.Build(this, rosterService);
            cachedFactsRoster = rosterService;
            cachedFactsFrame = frame;
            return cachedFacts;
        }

        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> ValidateEcology(
            CampusRosterService rosterService,
            bool logIssues)
        {
            ecologyValidationIssues.Clear();
            CampusWorldFacts facts = BuildFacts(rosterService);
            List<CampusEcologyValidator.ValidationIssue> issues = CampusEcologyValidator.Validate(facts);
            for (int i = 0; i < issues.Count; i++)
            {
                ecologyValidationIssues.Add(issues[i]);
            }

            if (logIssues)
            {
                CampusEcologyValidator.LogIssues(ecologyValidationIssues);
            }

            return ecologyValidationIssues;
        }

        private int ResolveFloorIndex(CampusCharacterRuntime runtime)
        {
            if (CampusRuntimeGameplayOverlayLoader.TryGetManagedEntity(runtime, out CampusRuntimeGameplayOverlayEntity entity))
            {
                return entity.FloorIndex;
            }

            CampusSceneCharacterDefinition sceneCharacter = runtime.GetComponent<CampusSceneCharacterDefinition>();
            if (sceneCharacter != null)
            {
                return sceneCharacter.FloorIndex;
            }

            if (runtime.Data != null && !string.IsNullOrWhiteSpace(runtime.Data.CurrentRoomId))
            {
                CampusGameplayRoom room = FindRoomById(runtime.Data.CurrentRoomId);
                if (room != null)
                {
                    return room.FloorIndex;
                }
            }

            return 1;
        }

        private void ResolveRoomRegistry()
        {
            if (roomRegistry != null)
            {
                return;
            }

            roomRegistry = GetComponent<CampusRoomRegistry>();
            if (roomRegistry == null)
            {
                roomRegistry = FindFirstObjectByType<CampusRoomRegistry>(FindObjectsInactive.Include);
            }

            if (roomRegistry == null)
            {
                roomRegistry = gameObject.AddComponent<CampusRoomRegistry>();
            }
        }

        private void EnsureServiceStationRegistry()
        {
            ResolveRoomRegistry();
            if (serviceStationsBuilt)
            {
                return;
            }

            serviceStations.Rebuild(roomRegistry != null ? roomRegistry.Rooms : null);
            serviceStationsBuilt = true;
        }

        private void InvalidateFacts()
        {
            cachedFacts = null;
            cachedFactsRoster = null;
            cachedFactsFrame = -1;
        }
    }
}

