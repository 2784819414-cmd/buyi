using System;
using System.Collections.Generic;
using System.IO;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CampusRuntimeGameplayOverlayLoader : MonoBehaviour
    {
        private const string OverlayRootName = "RuntimeGameplayOverlay";

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusMapRoot mapRoot;
        [SerializeField] private Transform generatedOverlayRoot;
        [SerializeField] private bool useRuntimeOverlayOnly;

        private readonly List<CampusRuntimeGameplayOverlayEntity> managedExternalEntities =
            new List<CampusRuntimeGameplayOverlayEntity>();

        public static CampusRuntimeGameplayOverlayLoader Instance { get; private set; }
        public bool UseRuntimeOverlayOnly => useRuntimeOverlayOnly;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }

            Instance = this;
            bootstrap = bootstrap != null ? bootstrap : GetComponent<CampusGameBootstrap>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ApplyLaunchSelection(string mapPath, CampusRuntimeMapLoadSource mapSource)
        {
            bootstrap = bootstrap != null ? bootstrap : CampusGameBootstrap.Instance;
            mapRoot = ResolveMapRoot();

            ClearGeneratedOverlay();
            useRuntimeOverlayOnly = mapSource != CampusRuntimeMapLoadSource.Scene && !string.IsNullOrWhiteSpace(mapPath);
            if (!useRuntimeOverlayOnly)
            {
                return;
            }

            PurgeSceneAuthoredActors();

            string overlayPath;
            CampusRuntimeGameplayOverlaySnapshot snapshot;
            string readError;
            if (!CampusRuntimeGameplayOverlayWorkflow.TryLoadExistingSnapshot(
                    mapPath,
                    out overlayPath,
                    out snapshot,
                    out readError))
            {
                if (!string.IsNullOrWhiteSpace(readError))
                {
                    throw new InvalidOperationException(readError);
                }

                WriteLog(CampusPlayerUiTextCatalog.Format(
                    CampusPlayerUiTextId.GameplayOverlayMissing,
                    Path.GetFileName(mapPath)));
                return;
            }

            HideRuntimeRoomMarkerVisuals();
            Transform host = EnsureGeneratedOverlayRoot();
            SpawnRooms(snapshot, host);
            SpawnFacilities(snapshot, host);
            SpawnServiceStations(snapshot, host);
            SpawnActors(snapshot, host);
            int actorCount = snapshot.Actors != null ? snapshot.Actors.Count : 0;
            int facilityCount = snapshot.Facilities != null ? snapshot.Facilities.Count : 0;
            WriteLog(CampusPlayerUiTextCatalog.Format(
                CampusPlayerUiTextId.GameplayOverlayApplied,
                Path.GetFileName(overlayPath),
                actorCount,
                facilityCount));
        }

        public bool ShouldIncludeActorRuntime(CampusCharacterRuntime runtime)
        {
            if (!useRuntimeOverlayOnly)
            {
                return true;
            }

            return runtime != null && runtime.GetComponent<CampusRuntimeGameplayOverlayEntity>() != null;
        }

        public bool ShouldIncludeExplicitMarker(Component marker)
        {
            if (!useRuntimeOverlayOnly)
            {
                return true;
            }

            return marker != null && marker.GetComponent<CampusRuntimeGameplayOverlayEntity>() != null;
        }

        public static bool TryGetManagedEntity(Component component, out CampusRuntimeGameplayOverlayEntity entity)
        {
            entity = component != null ? component.GetComponent<CampusRuntimeGameplayOverlayEntity>() : null;
            return entity != null;
        }

        private void SpawnRooms(CampusRuntimeGameplayOverlaySnapshot snapshot, Transform host)
        {
            if (snapshot == null || snapshot.Rooms == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Rooms.Count; i++)
            {
                CampusRuntimeGameplayRoomSnapshot room = snapshot.Rooms[i];
                if (room == null)
                {
                    continue;
                }

                CampusFloorRoot floor = mapRoot != null ? mapRoot.GetFloor(Mathf.Max(1, room.FloorIndex)) : null;
                Vector3 worldPosition = ResolveWorldPosition(floor, room.AnchorCell);
                string primaryDisplayName = room.GetPrimaryDisplayName();
                GameObject roomObject = new GameObject(string.IsNullOrWhiteSpace(primaryDisplayName)
                    ? "RuntimeGameplayRoom"
                    : primaryDisplayName);
                roomObject.transform.SetParent(host, false);
                roomObject.transform.position = worldPosition;

                CampusRuntimeGameplayOverlayEntity entity =
                    roomObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
                entity.Configure(false, room.FloorIndex, room.AnchorCell);

                CampusGameplayRoomMarker marker = roomObject.AddComponent<CampusGameplayRoomMarker>();
                marker.Configure(
                    room.Id,
                    room.DisplayName,
                    room.LocalizedDisplayName,
                    room.RoomType,
                    room.FloorIndex,
                    room.AnchorCell,
                    room.Size,
                    room.UsableForGameplay);
            }
        }

        private void SpawnFacilities(CampusRuntimeGameplayOverlaySnapshot snapshot, Transform host)
        {
            if (snapshot == null || snapshot.Facilities == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Facilities.Count; i++)
            {
                CampusRuntimeGameplayFacilitySnapshot facility = snapshot.Facilities[i];
                if (facility == null)
                {
                    continue;
                }

                facility.Normalize();
                CampusFloorRoot floor = mapRoot != null ? mapRoot.GetFloor(Mathf.Max(1, facility.FloorIndex)) : null;
                Vector3 worldPosition = ResolveWorldPosition(floor, facility.Cell);
                string primaryDisplayName = facility.GetPrimaryDisplayName();
                GameObject facilityObject = new GameObject(string.IsNullOrWhiteSpace(primaryDisplayName)
                    ? "RuntimeFacility"
                    : primaryDisplayName);
                facilityObject.transform.SetParent(host, false);
                facilityObject.transform.position = worldPosition;

                CampusRuntimeGameplayOverlayEntity entity =
                    facilityObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
                entity.Configure(false, facility.FloorIndex, facility.Cell);

                CampusGameplayFacilityMarker marker = facilityObject.AddComponent<CampusGameplayFacilityMarker>();
                marker.Configure(
                    facility.Id,
                    facility.DisplayName,
                    facility.LocalizedDisplayName,
                    facility.FacilityType,
                    facility.FloorIndex,
                    facility.Cell,
                    facility.CountsAsCoreFacility,
                    null);
            }
        }

        private void SpawnServiceStations(CampusRuntimeGameplayOverlaySnapshot snapshot, Transform host)
        {
            if (snapshot == null || snapshot.ServiceStations == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.ServiceStations.Count; i++)
            {
                CampusRuntimeGameplayServiceStationSnapshot serviceStation = snapshot.ServiceStations[i];
                if (serviceStation == null)
                {
                    continue;
                }

                serviceStation.Normalize();
                GameObject stationObject = new GameObject(string.IsNullOrWhiteSpace(serviceStation.Id)
                    ? "RuntimeServiceStation"
                    : serviceStation.Id);
                stationObject.transform.SetParent(host, false);

                CampusRuntimeGameplayOverlayEntity entity =
                    stationObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
                entity.Configure(false, 1, Vector3Int.zero);

                CampusGameplayServiceStationMarker marker =
                    stationObject.AddComponent<CampusGameplayServiceStationMarker>();
                marker.Configure(
                    serviceStation.Id,
                    serviceStation.StationTypeId,
                    serviceStation.RoomId,
                    serviceStation.OwnerFacilityId,
                    BuildSlotBindings(serviceStation.Slots));
            }
        }

        private void SpawnActors(CampusRuntimeGameplayOverlaySnapshot snapshot, Transform host)
        {
            if (snapshot == null || snapshot.Actors == null)
            {
                return;
            }

            int playerActorIndex = ResolvePlayerActorIndex(snapshot.Actors);
            for (int i = 0; i < snapshot.Actors.Count; i++)
            {
                CampusRuntimeGameplayActorSnapshot actor = snapshot.Actors[i];
                if (actor == null)
                {
                    continue;
                }

                bool isPlayerActor = i == playerActorIndex;
                GameObject actorObject = isPlayerActor ? ResolvePlayerHost(actor) : CreateNpcHost(actor, host);
                if (actorObject == null)
                {
                    continue;
                }

                CampusRuntimeGameplayOverlayEntity entity =
                    actorObject.GetComponent<CampusRuntimeGameplayOverlayEntity>();
                if (entity == null)
                {
                    entity = actorObject.AddComponent<CampusRuntimeGameplayOverlayEntity>();
                }

                entity.Configure(true, actor.FloorIndex, actor.Cell);
                if (!managedExternalEntities.Contains(entity) && isPlayerActor)
                {
                    managedExternalEntities.Add(entity);
                }

                BindActor(actorObject, actor, isPlayerActor);
                PositionActor(actorObject, actor);
            }
        }

        private static int ResolvePlayerActorIndex(IReadOnlyList<CampusRuntimeGameplayActorSnapshot> actors)
        {
            if (actors == null || actors.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < actors.Count; i++)
            {
                CampusRuntimeGameplayActorSnapshot actor = actors[i];
                if (actor != null && actor.IsPlayerControlled)
                {
                    return i;
                }
            }

            for (int i = 0; i < actors.Count; i++)
            {
                CampusRuntimeGameplayActorSnapshot actor = actors[i];
                if (actor == null || string.IsNullOrWhiteSpace(actor.Id))
                {
                    continue;
                }

                if (actor.Id.StartsWith("player_", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<CampusGameplayServiceStationSlotBinding> BuildSlotBindings(
            IReadOnlyList<CampusRuntimeGameplayServiceStationSlotSnapshot> sourceSlots)
        {
            List<CampusGameplayServiceStationSlotBinding> bindings =
                new List<CampusGameplayServiceStationSlotBinding>();
            if (sourceSlots == null)
            {
                return bindings;
            }

            for (int i = 0; i < sourceSlots.Count; i++)
            {
                CampusRuntimeGameplayServiceStationSlotSnapshot source = sourceSlots[i];
                if (source == null)
                {
                    continue;
                }

                source.Normalize();
                CampusGameplayServiceStationSlotBinding binding = new CampusGameplayServiceStationSlotBinding();
                binding.Configure(source.RoleId, source.FacilityIds);
                bindings.Add(binding);
            }

            return bindings;
        }

        private GameObject ResolvePlayerHost(CampusRuntimeGameplayActorSnapshot actor)
        {
            CampusPlayerCharacter existingPlayer = CampusPlayerCharacter.FindCurrent();
            if (existingPlayer != null)
            {
                return existingPlayer.gameObject;
            }

            CampusTestPlayerController testController =
                FindFirstObjectByType<CampusTestPlayerController>(FindObjectsInactive.Include);
            if (testController != null)
            {
                return testController.gameObject;
            }

            GameObject playerObject = new GameObject(string.IsNullOrWhiteSpace(actor.DisplayName) ? "RuntimePlayer" : actor.DisplayName);
            if (generatedOverlayRoot != null)
            {
                playerObject.transform.SetParent(generatedOverlayRoot, false);
            }

            return playerObject;
        }

        private static GameObject CreateNpcHost(CampusRuntimeGameplayActorSnapshot actor, Transform host)
        {
            GameObject actorObject = new GameObject(string.IsNullOrWhiteSpace(actor.DisplayName) ? "RuntimeNpc" : actor.DisplayName);
            actorObject.transform.SetParent(host, false);
            return actorObject;
        }

        private void BindActor(GameObject actorObject, CampusRuntimeGameplayActorSnapshot actor, bool isPlayerActor)
        {
            if (actorObject == null || actor == null)
            {
                return;
            }

            CampusCharacterData data = new CampusCharacterData();
            data.Configure(
                actor.Id,
                actor.DisplayName,
                actor.LocalizedDisplayName,
                actor.Role,
                actor.TeacherDuty,
                actor.ClassId,
                actor.InitialState,
                isPlayerActor,
                actor.Sleepiness,
                actor.Mischief,
                actor.Traits,
                actor.StaffDuty,
                actor.InitialMoney);
            data.SetAssignments(actor.Assignments);

            CampusCharacterRuntime runtime = actorObject.GetComponent<CampusCharacterRuntime>();
            if (runtime == null)
            {
                runtime = actorObject.AddComponent<CampusCharacterRuntime>();
            }

            runtime.Bind(data, renameGameObject: true);
            if (isPlayerActor)
            {
                CampusCharacterBodyController bodyController = actorObject.GetComponent<CampusCharacterBodyController>();
                if (bodyController == null)
                {
                    bodyController = actorObject.AddComponent<CampusCharacterBodyController>();
                }

                bodyController.EnsureSetup();
                CampusPlayerCharacter playerCharacter = actorObject.GetComponent<CampusPlayerCharacter>();
                if (playerCharacter == null)
                {
                    playerCharacter = actorObject.AddComponent<CampusPlayerCharacter>();
                }

                CampusNpcActor npcActor = actorObject.GetComponent<CampusNpcActor>();
                if (npcActor != null)
                {
                    Destroy(npcActor);
                }

                CampusTestPlayerController testController = actorObject.GetComponent<CampusTestPlayerController>();
                if (testController == null)
                {
                    testController = actorObject.AddComponent<CampusTestPlayerController>();
                }

                testController.enabled = true;
                testController.FloorIndex = Mathf.Max(1, actor.FloorIndex);
                testController.SetGameplayInputEnabled(true);
                playerCharacter.Bind(runtime);
                return;
            }

            CampusNpcActor actorComponent = actorObject.GetComponent<CampusNpcActor>();
            if (actorComponent == null)
            {
                actorComponent = actorObject.AddComponent<CampusNpcActor>();
            }

            actorComponent.Initialize(runtime, bootstrap, bootstrap != null ? bootstrap.WorldService : null);
        }

        private void PositionActor(GameObject actorObject, CampusRuntimeGameplayActorSnapshot actor)
        {
            CampusFloorRoot floor = mapRoot != null ? mapRoot.GetFloor(Mathf.Max(1, actor.FloorIndex)) : null;
            Vector3 worldPosition = ResolveWorldPosition(floor, actor.Cell);

            CampusCharacterBodyController bodyController = actorObject.GetComponent<CampusCharacterBodyController>();
            if (bodyController != null)
            {
                bodyController.FloorIndex = Mathf.Max(1, actor.FloorIndex);
                bodyController.EnsureSetup();
                bodyController.Teleport(worldPosition);
            }
            else
            {
                actorObject.transform.position = worldPosition;
            }

            CampusTestPlayerController testController = actorObject.GetComponent<CampusTestPlayerController>();
            if (testController != null)
            {
                testController.FloorIndex = Mathf.Max(1, actor.FloorIndex);
            }
        }

        private Transform EnsureGeneratedOverlayRoot()
        {
            if (generatedOverlayRoot != null)
            {
                return generatedOverlayRoot;
            }

            GameObject host = new GameObject(OverlayRootName);
            if (mapRoot != null)
            {
                host.transform.SetParent(mapRoot.transform, false);
            }

            generatedOverlayRoot = host.transform;
            return generatedOverlayRoot;
        }

        private void PurgeSceneAuthoredActors()
        {
            CampusSceneCharacterDefinition[] sceneDefinitions = FindObjectsByType<CampusSceneCharacterDefinition>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < sceneDefinitions.Length; i++)
            {
                CampusSceneCharacterDefinition definition = sceneDefinitions[i];
                if (definition == null)
                {
                    continue;
                }

                if (definition.GetComponent<CampusPlayerCharacter>() != null ||
                    definition.GetComponent<CampusTestPlayerController>() != null)
                {
                    continue;
                }

                Destroy(definition.gameObject);
            }

            CampusCharacterRuntime[] runtimes =
                FindObjectsByType<CampusCharacterRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < runtimes.Length; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null)
                {
                    continue;
                }

                if (runtime.GetComponent<CampusRuntimeGameplayOverlayEntity>() != null)
                {
                    continue;
                }

                if (runtime.GetComponent<CampusPlayerCharacter>() != null ||
                    runtime.GetComponent<CampusTestPlayerController>() != null)
                {
                    continue;
                }

                Destroy(runtime.gameObject);
            }
        }

        private void HideRuntimeRoomMarkerVisuals()
        {
            CampusRuntimeRoomMarker[] markers =
                FindObjectsByType<CampusRuntimeRoomMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                CampusRuntimeRoomMarker marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                SpriteRenderer markerRenderer = marker.GetComponent<SpriteRenderer>();
                if (markerRenderer != null)
                {
                    markerRenderer.enabled = false;
                }
            }
        }

        private void ClearGeneratedOverlay()
        {
            if (generatedOverlayRoot != null)
            {
                Destroy(generatedOverlayRoot.gameObject);
                generatedOverlayRoot = null;
            }

            for (int i = 0; i < managedExternalEntities.Count; i++)
            {
                CampusRuntimeGameplayOverlayEntity entity = managedExternalEntities[i];
                if (entity != null)
                {
                    Destroy(entity);
                }
            }

            managedExternalEntities.Clear();
        }

        private CampusMapRoot ResolveMapRoot()
        {
            if (mapRoot != null)
            {
                mapRoot.RebuildFloorReferences();
                return mapRoot;
            }

            mapRoot = FindFirstObjectByType<CampusMapRoot>(FindObjectsInactive.Include);
            if (mapRoot != null)
            {
                mapRoot.RebuildFloorReferences();
            }

            return mapRoot;
        }

        private static Vector3 ResolveWorldPosition(CampusFloorRoot floor, Vector3Int cell)
        {
            if (floor != null && floor.Grid != null)
            {
                Vector3 world = floor.Grid.GetCellCenterWorld(cell);
                world.z = 0f;
                return world;
            }

            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        }

        private void WriteLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }

            CampusRuntimeGameplayLogTextCatalog.Log(
                CampusRuntimeGameplayLogTextId.OverlayLoaderMessage,
                message);
        }
    }

}

