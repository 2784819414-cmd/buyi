using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Economy;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Sanctions;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.TheftConsequences
{
    [DisallowMultipleComponent]
    public sealed class CampusTheftConsequenceService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusSanctionService sanctionService;
        [SerializeField] private CampusEconomyService economyService;
        [SerializeField] private CampusInventoryTransferService inventoryTransferService;

        private CampusTheftIncidentRecord lastIncident;
        private CampusTheftConsequenceResult lastResult;

        public CampusTheftIncidentRecord LastIncident => lastIncident;
        public CampusTheftConsequenceResult LastResult => lastResult;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            sanctionService = bootstrap != null ? bootstrap.SanctionService : null;
            economyService = bootstrap != null ? bootstrap.EconomyService : null;
            inventoryTransferService = bootstrap != null ? bootstrap.InventoryTransferService : null;

            if (gameplayEventHub != null)
            {
                gameplayEventHub.ItemTheftObserved -= HandleItemTheftObserved;
                gameplayEventHub.ItemTheftObserved += HandleItemTheftObserved;
                gameplayEventHub.ContrabandFound -= HandleContrabandFound;
                gameplayEventHub.ContrabandFound += HandleContrabandFound;
                gameplayEventHub.ConfiguredAction -= HandleConfiguredAction;
                gameplayEventHub.ConfiguredAction += HandleConfiguredAction;
            }
        }

        private void OnDestroy()
        {
            if (gameplayEventHub != null)
            {
                gameplayEventHub.ItemTheftObserved -= HandleItemTheftObserved;
                gameplayEventHub.ContrabandFound -= HandleContrabandFound;
                gameplayEventHub.ConfiguredAction -= HandleConfiguredAction;
            }
        }

        private void HandleItemTheftObserved(CampusItemTheftObservedEvent eventData)
        {
            CampusCharacterRuntime actorRuntime = FindRuntime(eventData.ActorId);
            CampusCharacterRuntime witnessRuntime = FindRuntime(eventData.WitnessId);
            CampusTheftIncidentRecord incident = BuildObservedIncident(eventData, actorRuntime, witnessRuntime);
            ApplyIncident(actorRuntime, incident);
        }

        private void HandleContrabandFound(CampusContrabandFoundEvent eventData)
        {
            CampusCharacterRuntime actorRuntime = FindRuntime(eventData.ActorId);
            CampusCharacterRuntime inspectorRuntime = FindRuntime(eventData.InspectorId);
            CampusTheftIncidentRecord incident = BuildContrabandIncident(eventData, actorRuntime, inspectorRuntime);
            ApplyIncident(actorRuntime, incident);
        }

        private void HandleConfiguredAction(CampusConfiguredActionEvent eventData)
        {
            if (!eventData.Succeeded || string.IsNullOrWhiteSpace(eventData.ItemDefinitionId))
            {
                return;
            }

            CampusCharacterRuntime actorRuntime = FindRuntime(eventData.ActorId);
            CampusTheftIncidentRecord incident = BuildConfiguredActionIncident(eventData, actorRuntime);
            ApplyIncident(actorRuntime, incident);
        }

        private CampusTheftIncidentRecord BuildObservedIncident(
            CampusItemTheftObservedEvent eventData,
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime witnessRuntime)
        {
            CampusGameplayRoom room = ResolveRoom(eventData.RoomId, actorRuntime);
            CampusCharacterData witnessData = witnessRuntime != null ? witnessRuntime.Data : null;
            bool ownerWitness = IsSameId(eventData.WitnessId, eventData.OwnerId);
            int itemValue = CampusTheftConsequencePresetCatalog.ResolveItemValue(
                eventData.ItemDefinitionId,
                eventData.SuspicionAmount);

            return new CampusTheftIncidentRecord
            {
                Kind = CampusTheftIncidentKind.ObservedProtectedItemMove,
                ActorId = Clean(eventData.ActorId),
                WitnessId = Clean(eventData.WitnessId),
                OwnerId = Clean(eventData.OwnerId),
                ItemInstanceId = Clean(eventData.ItemInstanceId),
                ItemDefinitionId = Clean(eventData.ItemDefinitionId),
                ItemDisplayName = Clean(eventData.ItemDisplayName),
                SourceContainerId = Clean(eventData.SourceContainerId),
                TargetContainerId = Clean(eventData.TargetContainerId),
                RoomId = ResolveRoomId(eventData.RoomId, room, actorRuntime),
                Day = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0,
                BaseItemValue = itemValue,
                EvidenceValue = Mathf.Max(1, eventData.SuspicionAmount),
                WitnessWeight = CampusTheftConsequencePresetCatalog.ResolveWitnessWeight(witnessData, ownerWitness),
                RoomSensitivity = CampusTheftConsequencePresetCatalog.ResolveRoomSensitivity(room, eventData.RoomId),
                OfficialWitness = eventData.ShouldIssueSanction,
                FoundOnActor = false
            };
        }

        private CampusTheftIncidentRecord BuildContrabandIncident(
            CampusContrabandFoundEvent eventData,
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime)
        {
            CampusGameplayRoom room = ResolveRoom(eventData.RoomId, actorRuntime);
            CampusCharacterData inspectorData = inspectorRuntime != null ? inspectorRuntime.Data : null;
            int itemValue = CampusTheftConsequencePresetCatalog.ResolveItemValue(
                eventData.ItemDefinitionId,
                eventData.InspectionPressure);

            return new CampusTheftIncidentRecord
            {
                Kind = CampusTheftIncidentKind.ContrabandFound,
                ActorId = Clean(eventData.ActorId),
                WitnessId = Clean(eventData.InspectorId),
                OwnerId = string.Empty,
                ItemInstanceId = Clean(eventData.ItemInstanceId),
                ItemDefinitionId = Clean(eventData.ItemDefinitionId),
                ItemDisplayName = Clean(eventData.ItemDisplayName),
                SourceContainerId = string.Empty,
                TargetContainerId = Clean(eventData.ContainerId),
                RoomId = ResolveRoomId(eventData.RoomId, room, actorRuntime),
                Day = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0,
                BaseItemValue = itemValue,
                EvidenceValue = Mathf.Max(1, eventData.InspectionPressure),
                WitnessWeight = CampusTheftConsequencePresetCatalog.ResolveWitnessWeight(inspectorData, false),
                RoomSensitivity = CampusTheftConsequencePresetCatalog.ResolveRoomSensitivity(room, eventData.RoomId),
                OfficialWitness = eventData.ShouldIssueSanction,
                FoundOnActor = true
            };
        }

        private CampusTheftIncidentRecord BuildConfiguredActionIncident(
            CampusConfiguredActionEvent eventData,
            CampusCharacterRuntime actorRuntime)
        {
            CampusGameplayRoom room = ResolveRoom(eventData.RoomId, actorRuntime);
            int eventRisk = eventData.EvidenceDelta +
                            eventData.SuspicionDelta +
                            eventData.RumorDelta +
                            eventData.CrackdownDelta +
                            eventData.AreaAlertDelta;
            int itemValue = CampusTheftConsequencePresetCatalog.ResolveItemValue(
                eventData.ItemDefinitionId,
                eventRisk);

            return new CampusTheftIncidentRecord
            {
                Kind = CampusTheftIncidentKind.ConfiguredActionTheft,
                ActorId = Clean(eventData.ActorId),
                WitnessId = string.Empty,
                OwnerId = Clean(eventData.OwnerId),
                ItemInstanceId = Clean(eventData.ItemInstanceId),
                ItemDefinitionId = Clean(eventData.ItemDefinitionId),
                ItemDisplayName = Clean(eventData.ItemDisplayName),
                SourceContainerId = Clean(eventData.SourceLocation),
                TargetContainerId = Clean(eventData.TargetId),
                RoomId = ResolveRoomId(eventData.RoomId, room, actorRuntime),
                Day = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 0,
                BaseItemValue = itemValue,
                EvidenceValue = Mathf.Max(1, eventRisk),
                WitnessWeight = 0,
                RoomSensitivity = CampusTheftConsequencePresetCatalog.ResolveRoomSensitivity(room, eventData.RoomId),
                OfficialWitness = false,
                FoundOnActor = false
            };
        }

        private void ApplyIncident(CampusCharacterRuntime actorRuntime, CampusTheftIncidentRecord incident)
        {
            if (incident == null || bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            CampusTheftConsequenceResult result = CampusTheftConsequenceEvaluator.Evaluate(incident, bootstrap.GameState);
            lastIncident = incident;
            lastResult = result;

            ApplyGameState(result, actorRuntime);
            ApplyActorState(actorRuntime);
            ApplyCompensation(actorRuntime, result);
            ApplyConfiscation(actorRuntime, result);
            ApplySanction(actorRuntime, incident, result);
            WriteLogs(incident, result);
        }

        private void ApplyGameState(CampusTheftConsequenceResult result, CampusCharacterRuntime actorRuntime)
        {
            if (bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            if (IsPlayerRuntime(actorRuntime))
            {
                bootstrap.GameState.AddPlayerSuspicion(result.SuspicionDelta);
                bootstrap.GameState.AddPlayerTheftEvidence(result.EvidenceDelta);
                bootstrap.GameState.AddPlayerTheftRecord(result.RecordDelta);
            }

            bootstrap.GameState.AddCampusRumor(result.RumorDelta);
            bootstrap.GameState.AddCampusCrackdown(result.CrackdownDelta);
            bootstrap.GameState.AddTeacherAlertness(result.TeacherAlertnessDelta);
            bootstrap.GameState.AddCampusOrder(result.CampusOrderDelta);
            bootstrap.GameState.AddCampusChaos(result.CampusChaosDelta);
        }

        private static void ApplyActorState(CampusCharacterRuntime actorRuntime)
        {
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return;
            }

            actorRuntime.Data.AddMemory(CampusCharacterMemoryId.SuspectedProtectedTheft);
        }

        private void ApplyCompensation(CampusCharacterRuntime actorRuntime, CampusTheftConsequenceResult result)
        {
            if (result.CompensationAmount <= 0 || economyService == null || actorRuntime == null)
            {
                return;
            }

            bool paid = economyService.TrySpendMoney(actorRuntime, result.CompensationAmount);
            bootstrap.EventLog?.AddLog(CampusTheftConsequenceTextCatalog.FormatCompensation(
                CampusLanguageState.CurrentLanguage,
                result.CompensationAmount,
                paid));
        }

        private void ApplyConfiscation(CampusCharacterRuntime actorRuntime, CampusTheftConsequenceResult result)
        {
            if (!result.ConfiscateEvidence || actorRuntime == null)
            {
                return;
            }

            if (!CampusContrabandService.TryFindCarriedContraband(
                    actorRuntime,
                    out StorageItemModel item,
                    out StorageContainerModel source))
            {
                return;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            CampusGameplayRoom room = ResolveRoom(actorRuntime.Data != null ? actorRuntime.Data.CurrentRoomId : string.Empty, actorRuntime);
            StorageContainerModel target = CampusContrabandService.GetOrCreateConfiscatedContainer(memory, room);
            if (target == null ||
                !CampusContrabandService.TryFindConfiscationSpace(target, item, out Vector2Int targetPosition))
            {
                return;
            }

            StorageTransferContext context = StorageTransferContext.ForActor(
                actorRuntime.gameObject,
                StorageTransferReason.InspectionConfiscation);
            context.SuppressNpcDetection = true;
            context.SuppressSuspicion = true;
            context.AllowProtectedTake = true;
            context.RoomId = room != null ? room.RoomId : string.Empty;

            CampusInventoryTransferService transferService = inventoryTransferService != null
                ? inventoryTransferService
                : CampusInventoryTransferService.Resolve();
            if (transferService.TryMoveItem(item, source, target, targetPosition.x, targetPosition.y, context, out _))
            {
                bootstrap.EventLog?.AddLog(CampusTheftConsequenceTextCatalog.FormatConfiscated(
                    CampusLanguageState.CurrentLanguage,
                    item.GetDisplayName()));
            }
        }

        private void ApplySanction(
            CampusCharacterRuntime actorRuntime,
            CampusTheftIncidentRecord incident,
            CampusTheftConsequenceResult result)
        {
            if (actorRuntime == null || result.SanctionLevel == CampusSanctionLevel.None)
            {
                return;
            }

            CampusSanctionService service = sanctionService != null
                ? sanctionService
                : bootstrap != null ? bootstrap.SanctionService : null;
            if (service == null)
            {
                return;
            }

            CampusSanctionReasonId reason = incident.Kind == CampusTheftIncidentKind.ContrabandFound
                ? CampusSanctionReasonId.ContrabandFound
                : CampusSanctionReasonId.ProtectedPropertyObserved;
            service.IssueSanction(new CampusSanctionRequest(
                actorRuntime,
                incident.RoomId,
                result.SanctionLevel,
                CampusCharacterTextCatalog.FormatSanctionReason(CampusLanguageState.CurrentLanguage, reason)));
        }

        private void WriteLogs(CampusTheftIncidentRecord incident, CampusTheftConsequenceResult result)
        {
            if (bootstrap == null || bootstrap.EventLog == null)
            {
                return;
            }

            bootstrap.EventLog.AddLog(CampusTheftConsequenceTextCatalog.FormatIncidentRecorded(
                CampusLanguageState.CurrentLanguage,
                incident,
                result));
            bootstrap.EventLog.AddLog(CampusTheftConsequenceTextCatalog.FormatConsequenceApplied(
                CampusLanguageState.CurrentLanguage,
                result));
        }

        private CampusCharacterRuntime FindRuntime(string actorId)
        {
            return rosterService != null ? rosterService.FindRuntime(actorId) : null;
        }

        private CampusGameplayRoom ResolveRoom(string roomId, CampusCharacterRuntime actorRuntime)
        {
            if (worldService != null && !string.IsNullOrWhiteSpace(roomId))
            {
                CampusGameplayRoom room = worldService.FindRoomById(roomId);
                if (room != null)
                {
                    return room;
                }
            }

            return worldService != null && actorRuntime != null
                ? worldService.FindRoomForRuntime(actorRuntime)
                : null;
        }

        private static string ResolveRoomId(string eventRoomId, CampusGameplayRoom room, CampusCharacterRuntime actorRuntime)
        {
            if (!string.IsNullOrWhiteSpace(eventRoomId))
            {
                return eventRoomId.Trim();
            }

            if (room != null && !string.IsNullOrWhiteSpace(room.RoomId))
            {
                return room.RoomId;
            }

            return actorRuntime != null && actorRuntime.Data != null
                ? Clean(actorRuntime.Data.CurrentRoomId)
                : string.Empty;
        }

        private bool IsPlayerRuntime(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   rosterService != null &&
                   rosterService.PlayerRuntime != null &&
                   IsSameId(runtime.CharacterId, rosterService.PlayerRuntime.CharacterId);
        }

        private static bool IsSameId(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
