using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    public readonly struct CampusInspectionDebugSnapshot
    {
        public CampusInspectionDebugSnapshot(
            bool isAvailable,
            string status,
            string roomId,
            CampusRoomType roomType,
            string searchInspectorId,
            string searchInspectorName,
            string questionerId,
            string questionerName,
            string highestVigilanceNpcId,
            string highestVigilanceNpcName,
            int highestVigilancePressure,
            int areaSearchPressure,
            int areaQuestioningPressure,
            int searchPressure,
            int questioningPressure,
            float searchCooldownRemaining,
            float questioningCooldownRemaining,
            bool hasContraband,
            string contrabandItemName,
            string contrabandContainerId,
            int confiscatedItemCount)
        {
            IsAvailable = isAvailable;
            Status = status ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            RoomType = roomType;
            SearchInspectorId = searchInspectorId ?? string.Empty;
            SearchInspectorName = searchInspectorName ?? string.Empty;
            QuestionerId = questionerId ?? string.Empty;
            QuestionerName = questionerName ?? string.Empty;
            HighestVigilanceNpcId = highestVigilanceNpcId ?? string.Empty;
            HighestVigilanceNpcName = highestVigilanceNpcName ?? string.Empty;
            HighestVigilancePressure = Mathf.Clamp(highestVigilancePressure, 0, 100);
            AreaSearchPressure = Mathf.Clamp(areaSearchPressure, 0, 100);
            AreaQuestioningPressure = Mathf.Clamp(areaQuestioningPressure, 0, 100);
            SearchPressure = Mathf.Clamp(searchPressure, 0, 100);
            QuestioningPressure = Mathf.Clamp(questioningPressure, 0, 100);
            SearchCooldownRemaining = Mathf.Max(0f, searchCooldownRemaining);
            QuestioningCooldownRemaining = Mathf.Max(0f, questioningCooldownRemaining);
            HasContraband = hasContraband;
            ContrabandItemName = contrabandItemName ?? string.Empty;
            ContrabandContainerId = contrabandContainerId ?? string.Empty;
            ConfiscatedItemCount = Mathf.Max(0, confiscatedItemCount);
        }

        public bool IsAvailable { get; }
        public string Status { get; }
        public string RoomId { get; }
        public CampusRoomType RoomType { get; }
        public string SearchInspectorId { get; }
        public string SearchInspectorName { get; }
        public string QuestionerId { get; }
        public string QuestionerName { get; }
        public string HighestVigilanceNpcId { get; }
        public string HighestVigilanceNpcName { get; }
        public int HighestVigilancePressure { get; }
        public int AreaSearchPressure { get; }
        public int AreaQuestioningPressure { get; }
        public int SearchPressure { get; }
        public int QuestioningPressure { get; }
        public float SearchCooldownRemaining { get; }
        public float QuestioningCooldownRemaining { get; }
        public bool HasContraband { get; }
        public string ContrabandItemName { get; }
        public string ContrabandContainerId { get; }
        public int ConfiscatedItemCount { get; }
    }

    [DisallowMultipleComponent]
    public sealed class CampusInspectionService : MonoBehaviour
    {
        private const float MinimumEvaluationIntervalSeconds = 0.5f;
        public const string ConfiscatedContainerId = "campus_confiscated_items";

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField, Min(MinimumEvaluationIntervalSeconds)] private float evaluationIntervalSeconds = 4f;
        [SerializeField, Min(0f)] private float searchCooldownSeconds = 18f;
        [SerializeField, Min(0f)] private float questioningCooldownSeconds = 12f;
        [SerializeField, Min(0.5f)] private float maxInspectionDistance = 5.5f;
        [SerializeField] private CampusInspectionPressure globalSearchPressure = CampusInspectionPressure.Of(4);
        [SerializeField] private CampusInspectionPressure globalQuestioningPressure = CampusInspectionPressure.Of(7);
        [SerializeField] private List<CampusAreaInspectionPressureRule> areaPressureRules =
            new List<CampusAreaInspectionPressureRule>();
        [SerializeField] private List<CampusNpcInspectionPressureRule> npcPressureRules =
            new List<CampusNpcInspectionPressureRule>();
        [SerializeField, Min(0)] private int dailyQuestioningCount;
        [SerializeField, Min(0)] private int dailySearchCount;
        [SerializeField, Min(0)] private int dailyContrabandFoundCount;
        [SerializeField, Min(0)] private int dailyConfiscatedItemCount;
        [SerializeField, Min(0)] private int dailyTattletaleReportCount;
        [SerializeField, Min(0)] private int dailyProactiveInspectionCount;
        [SerializeField] private string currentInspectionSummary = "Inspection system waiting for activity.";

        private float nextEvaluationTime;
        private float nextSearchAllowedTime;
        private float nextQuestioningAllowedTime;
        private bool subscribedToTime;
        private readonly Dictionary<string, float> proactiveActionCooldownByNpc =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CampusAreaInspectionPressureRule> AreaPressureRules => areaPressureRules;
        public IReadOnlyList<CampusNpcInspectionPressureRule> NpcPressureRules => npcPressureRules;
        public float SearchCooldownRemaining => Mathf.Max(0f, nextSearchAllowedTime - Time.time);
        public float QuestioningCooldownRemaining => Mathf.Max(0f, nextQuestioningAllowedTime - Time.time);
        public int DailyQuestioningCount => dailyQuestioningCount;
        public int DailySearchCount => dailySearchCount;
        public int DailyContrabandFoundCount => dailyContrabandFoundCount;
        public int DailyConfiscatedItemCount => dailyConfiscatedItemCount;
        public int DailyTattletaleReportCount => dailyTattletaleReportCount;
        public int DailyProactiveInspectionCount => dailyProactiveInspectionCount;
        public string CurrentInspectionSummary => currentInspectionSummary;

        private void Reset()
        {
            EnsureDefaultPressureRules();
        }

        private void OnValidate()
        {
            evaluationIntervalSeconds = Mathf.Max(MinimumEvaluationIntervalSeconds, evaluationIntervalSeconds);
            searchCooldownSeconds = Mathf.Max(0f, searchCooldownSeconds);
            questioningCooldownSeconds = Mathf.Max(0f, questioningCooldownSeconds);
            maxInspectionDistance = Mathf.Max(0.5f, maxInspectionDistance);
            EnsureDefaultPressureRules();
        }

        public static CampusInspectionService Resolve()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.InspectionService != null)
            {
                return bootstrap.InspectionService;
            }

            CampusInspectionService existing =
                FindFirstObjectByType<CampusInspectionService>(FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.Initialize(bootstrap);
                return existing;
            }

            GameObject host = bootstrap != null ? bootstrap.gameObject : new GameObject("CampusInspectionService");
            CampusInspectionService service = host.AddComponent<CampusInspectionService>();
            service.Initialize(bootstrap);
            return service;
        }

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            ReleaseTimeSubscription();
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            ResolveReferences();
            EnsureDefaultPressureRules();
            SubscribeTime();
            nextEvaluationTime = Time.time + UnityEngine.Random.Range(0.2f, 1.2f);
        }

        private void OnDestroy()
        {
            ReleaseTimeSubscription();
        }

        public void SetAreaPressure(string roomId, CampusRoomType roomType, int searchPressure, int questioningPressure)
        {
            areaPressureRules = areaPressureRules ?? new List<CampusAreaInspectionPressureRule>();
            CampusAreaInspectionPressureRule rule = FindAreaRule(roomId, roomType);
            if (rule == null)
            {
                rule = new CampusAreaInspectionPressureRule();
                areaPressureRules.Add(rule);
            }

            rule.RoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            rule.RoomType = roomType;
            rule.SearchPressure = CampusInspectionPressure.Of(searchPressure);
            rule.QuestioningPressure = CampusInspectionPressure.Of(questioningPressure);
        }

        public void SetNpcVigilance(string characterId, int vigilancePressure)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            npcPressureRules = npcPressureRules ?? new List<CampusNpcInspectionPressureRule>();
            string normalizedId = characterId.Trim();
            for (int i = 0; i < npcPressureRules.Count; i++)
            {
                CampusNpcInspectionPressureRule existing = npcPressureRules[i];
                if (existing != null && existing.Matches(normalizedId))
                {
                    existing.VigilancePressure = CampusInspectionPressure.Of(vigilancePressure);
                    return;
                }
            }

            npcPressureRules.Add(new CampusNpcInspectionPressureRule
            {
                CharacterId = normalizedId,
                VigilancePressure = CampusInspectionPressure.Of(vigilancePressure)
            });
        }

        public CampusInspectionDebugSnapshot BuildDebugSnapshot()
        {
            ResolveReferences();
            if (!TryResolveInspectionContext(out CampusCharacterRuntime playerRuntime, out CampusGameplayRoom room, out string status))
            {
                return new CampusInspectionDebugSnapshot(
                    false,
                    status,
                    string.Empty,
                    CampusRoomType.Unknown,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    0,
                    SearchCooldownRemaining,
                    QuestioningCooldownRemaining,
                    false,
                    string.Empty,
                    string.Empty,
                    CountConfiscatedItems());
            }

            bool hasContraband = TryFindCarriedContraband(
                out StorageItemModel contrabandItem,
                out StorageContainerModel contrabandContainer);
            TryFindBestInspector(room, playerRuntime, true, out CampusCharacterRuntime searchInspector);
            TryFindBestInspector(room, playerRuntime, false, out CampusCharacterRuntime questioner);
            TryFindHighestVigilanceNpc(room, playerRuntime, out CampusCharacterRuntime highestNpc);

            int searchPressure = searchInspector != null
                ? ResolveSearchPressure(room, searchInspector, contrabandItem)
                : 0;
            int questioningPressure = questioner != null
                ? ResolveQuestioningPressure(room, questioner, hasContraband)
                : 0;

            string snapshotStatus = status;
            if (searchInspector == null)
            {
                snapshotStatus = "No authority inspector in range.";
            }
            else if (questioner == null)
            {
                snapshotStatus = "Search ready, no questioning NPC in range.";
            }

            return new CampusInspectionDebugSnapshot(
                true,
                snapshotStatus,
                room.RoomId,
                room.RoomType,
                ResolveRuntimeId(searchInspector),
                ResolveRuntimeName(searchInspector),
                ResolveRuntimeId(questioner),
                ResolveRuntimeName(questioner),
                ResolveRuntimeId(highestNpc),
                ResolveRuntimeName(highestNpc),
                ResolveNpcVigilancePressure(highestNpc).Value,
                ResolveAreaSearchPressure(room).Value,
                ResolveAreaQuestioningPressure(room).Value,
                searchPressure,
                questioningPressure,
                SearchCooldownRemaining,
                QuestioningCooldownRemaining,
                hasContraband,
                ResolveItemName(contrabandItem),
                contrabandContainer != null ? contrabandContainer.Id : string.Empty,
                CountConfiscatedItems());
        }

        public bool TryForceQuestioning(out string message)
        {
            ResolveReferences();
            if (!TryResolveInspectionContext(out CampusCharacterRuntime playerRuntime, out CampusGameplayRoom room, out message))
            {
                return false;
            }

            bool hasContraband = TryFindCarriedContraband(out _, out _);
            TryFindBestInspector(room, playerRuntime, false, out CampusCharacterRuntime questioner);
            int pressure = ResolveQuestioningPressure(room, questioner, hasContraband);
            nextQuestioningAllowedTime = Time.time + questioningCooldownSeconds;
            HandleQuestioning(playerRuntime, questioner, room, pressure, false);
            message = "Forced questioning by " +
                      (questioner != null ? ResolveRuntimeName(questioner) : "debug inspector") +
                      ". Pressure=" + pressure + ".";
            return true;
        }

        public bool TryForceSearch(out string message)
        {
            ResolveReferences();
            if (!TryResolveInspectionContext(out CampusCharacterRuntime playerRuntime, out CampusGameplayRoom room, out message))
            {
                return false;
            }

            bool hasContraband = TryFindCarriedContraband(
                out StorageItemModel contrabandItem,
                out StorageContainerModel contrabandContainer);
            TryFindBestInspector(room, playerRuntime, true, out CampusCharacterRuntime inspector);
            int pressure = ResolveSearchPressure(room, inspector, contrabandItem);
            nextSearchAllowedTime = Time.time + searchCooldownSeconds;
            nextQuestioningAllowedTime = Mathf.Max(nextQuestioningAllowedTime, Time.time + 5f);
            HandleSearch(playerRuntime, inspector, room, contrabandItem, contrabandContainer, pressure);
            message = hasContraband
                ? "Forced search found and confiscated " + ResolveItemName(contrabandItem) + "."
                : "Forced search found no contraband.";
            return true;
        }

        public bool TrySeedDebugContraband(out string message)
        {
            ResolveReferences();
            if (!TryResolveInspectionContext(out CampusCharacterRuntime playerRuntime, out CampusGameplayRoom room, out message))
            {
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                message = "Storage memory is unavailable.";
                return false;
            }

            StoragePlayerInventoryUtility.EnsureRegistry(memory);
            StorageItemModel item = CreateDebugContraband(memory, room);
            StorageTransferContext context = StorageTransferContext.ForActor(playerRuntime.gameObject, StorageTransferReason.SystemSeed);
            context.ActorId = ResolveRuntimeId(playerRuntime);
            context.RoomId = room.RoomId;
            context.AllowProtectedTake = true;
            context.SuppressNpcDetection = true;
            context.SuppressSuspicion = true;
            context.SourceLocation = "Debug contraband seed";
            context.OwnerId = item.OwnerId;

            CampusInventoryTransferService transferService = CampusInventoryTransferService.Resolve();
            if (!transferService.TryPlaceInCarriedStorage(memory, item, context, out StorageTransferResult result))
            {
                message = result.Message;
                return false;
            }

            message = "Seeded carried contraband: " + ResolveItemName(item) + ".";
            WriteInspectionLog("[InspectionDebug] " + message);
            return true;
        }

        public bool TryNpcProactiveInspection(CampusCharacterRuntime npcRuntime, out string line)
        {
            line = string.Empty;
            ResolveReferences();
            if (!TryResolveInspectionContext(out CampusCharacterRuntime playerRuntime, out CampusGameplayRoom room, out _) ||
                npcRuntime == null ||
                npcRuntime.Data == null ||
                npcRuntime.Data.IsPlayerControlled ||
                string.Equals(npcRuntime.CharacterId, playerRuntime.CharacterId, StringComparison.OrdinalIgnoreCase) ||
                !IsRuntimeInRoom(npcRuntime, room))
            {
                return false;
            }

            string npcId = ResolveRuntimeId(npcRuntime);
            if (!string.IsNullOrWhiteSpace(npcId) &&
                proactiveActionCooldownByNpc.TryGetValue(npcId, out float cooldownUntil) &&
                Time.time < cooldownUntil)
            {
                return false;
            }

            float distance = Vector2.Distance(npcRuntime.transform.position, playerRuntime.transform.position);
            if (distance > Mathf.Min(maxInspectionDistance, 1.8f))
            {
                return false;
            }

            bool hasContraband = TryFindCarriedContraband(
                out StorageItemModel contrabandItem,
                out StorageContainerModel contrabandContainer);
            bool authority = IsAuthority(npcRuntime);
            bool tattletale = npcRuntime.Data.HasTrait(CampusCharacterTrait.Tattletale);

            if (authority)
            {
                int searchPressure = ResolveSearchPressure(room, npcRuntime, contrabandItem);
                if (hasContraband && searchPressure >= 45 && Roll(searchPressure))
                {
                    proactiveActionCooldownByNpc[npcId] = Time.time + Mathf.Max(12f, searchCooldownSeconds);
                    nextSearchAllowedTime = Mathf.Max(nextSearchAllowedTime, Time.time + Mathf.Min(6f, searchCooldownSeconds));
                    HandleSearch(playerRuntime, npcRuntime, room, contrabandItem, contrabandContainer, searchPressure);
                    line = "Open your bag.";
                    return true;
                }

                int questioningPressure = ResolveQuestioningPressure(room, npcRuntime, hasContraband);
                if (questioningPressure >= 34 && Roll(questioningPressure))
                {
                    proactiveActionCooldownByNpc[npcId] = Time.time + Mathf.Max(10f, questioningCooldownSeconds);
                    nextQuestioningAllowedTime = Mathf.Max(nextQuestioningAllowedTime, Time.time + Mathf.Min(5f, questioningCooldownSeconds));
                    HandleQuestioning(playerRuntime, npcRuntime, room, questioningPressure, false);
                    line = "Show me what you are carrying.";
                    return true;
                }
            }

            if (tattletale &&
                ResolveNpcProactivePressure(npcRuntime, room, hasContraband) >= 42 &&
                Roll(hasContraband ? 72 : 42))
            {
                proactiveActionCooldownByNpc[npcId] = Time.time + 24f;
                HandleTattletaleReport(npcRuntime, playerRuntime, room, hasContraband);
                line = hasContraband ? "Teacher, check the bag." : "Teacher, something is off.";
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Time.time < nextEvaluationTime)
            {
                return;
            }

            nextEvaluationTime = Time.time + Mathf.Max(MinimumEvaluationIntervalSeconds, evaluationIntervalSeconds);
            ResolveReferences();
            EvaluatePlayerInspection();
        }

        private void EvaluatePlayerInspection()
        {
            if (bootstrap == null || rosterService == null || worldService == null)
            {
                return;
            }

            CampusCharacterRuntime playerRuntime = rosterService.PlayerRuntime;
            if (playerRuntime == null || playerRuntime.Data == null)
            {
                return;
            }

            CampusGameplayRoom room = worldService.FindRoomForRuntime(playerRuntime);
            if (room == null)
            {
                return;
            }

            bool hasContraband = TryFindCarriedContraband(out StorageItemModel contrabandItem, out StorageContainerModel contrabandContainer);
            if (Time.time >= nextSearchAllowedTime &&
                TryFindBestInspector(room, playerRuntime, true, out CampusCharacterRuntime searchInspector))
            {
                int searchPressure = ResolveSearchPressure(room, searchInspector, contrabandItem);
                if (Roll(searchPressure))
                {
                    nextSearchAllowedTime = Time.time + searchCooldownSeconds;
                    nextQuestioningAllowedTime = Mathf.Max(nextQuestioningAllowedTime, Time.time + 5f);
                    HandleSearch(playerRuntime, searchInspector, room, contrabandItem, contrabandContainer, searchPressure);
                    if (hasContraband)
                    {
                        return;
                    }
                }
            }

            if (Time.time >= nextQuestioningAllowedTime &&
                TryFindBestInspector(room, playerRuntime, false, out CampusCharacterRuntime questioner))
            {
                int questioningPressure = ResolveQuestioningPressure(room, questioner, hasContraband);
                if (Roll(questioningPressure))
                {
                    nextQuestioningAllowedTime = Time.time + questioningCooldownSeconds;
                    HandleQuestioning(playerRuntime, questioner, room, questioningPressure, false);
                }
            }
        }

        private void HandleSearch(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            CampusGameplayRoom room,
            StorageItemModel contrabandItem,
            StorageContainerModel contrabandContainer,
            int pressure)
        {
            dailySearchCount++;
            bool foundContraband = contrabandItem != null;
            HandleQuestioning(actorRuntime, inspectorRuntime, room, pressure, foundContraband);
            if (!foundContraband)
            {
                currentInspectionSummary = "Search found nothing. Pressure=" + pressure + ".";
                WriteInspectionLog("[Inspection] Search found nothing. Pressure=" + pressure + ".");
                return;
            }

            ApplyContrabandConsequences(actorRuntime, inspectorRuntime, room, contrabandItem, pressure);
            bool confiscated = TryConfiscateContraband(
                contrabandItem,
                contrabandContainer,
                room,
                actorRuntime,
                inspectorRuntime,
                out string confiscationMessage);
            if (!confiscated)
            {
                WriteInspectionLog("[Inspection] Contraband confiscation failed: " + confiscationMessage);
            }

            gameplayEventHub?.PublishContrabandFound(new CampusContrabandFoundEvent(
                ResolveRuntimeId(actorRuntime),
                ResolveRuntimeId(inspectorRuntime),
                room != null ? room.RoomId : string.Empty,
                ResolveInstanceId(contrabandItem),
                contrabandItem.DefinitionId,
                ResolveItemName(contrabandItem),
                contrabandContainer != null ? contrabandContainer.Id : string.Empty,
                pressure,
                IsAuthority(inspectorRuntime)));
        }

        private void HandleQuestioning(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            CampusGameplayRoom room,
            int pressure,
            bool foundContraband)
        {
            dailyQuestioningCount++;
            string actorId = ResolveRuntimeId(actorRuntime);
            string inspectorId = ResolveRuntimeId(inspectorRuntime);
            gameplayEventHub?.PublishInventoryQuestioned(new CampusInventoryQuestionedEvent(
                actorId,
                inspectorId,
                room != null ? room.RoomId : string.Empty,
                pressure,
                foundContraband));

            if (actorRuntime != null && actorRuntime.Data != null)
            {
                actorRuntime.Data.SetState(CampusCharacterState.Nervous);
            }

            if (inspectorRuntime != null && inspectorRuntime.Data != null && !string.IsNullOrWhiteSpace(actorId))
            {
                inspectorRuntime.Data.AddRelationshipSuspicion(actorId, foundContraband ? 8 : 2);
                inspectorRuntime.Data.AddMood(foundContraband ? -2 : -1);
            }

            if (!foundContraband && bootstrap != null && bootstrap.GameState != null)
            {
                int suspicionNudge = Mathf.Clamp(pressure / 45, 0, 2);
                if (suspicionNudge > 0)
                {
                    bootstrap.GameState.AddPlayerSuspicion(suspicionNudge);
                }
            }

            currentInspectionSummary = (string.IsNullOrWhiteSpace(inspectorId) ? "Debug inspector" : inspectorId) +
                                       " questioned " + actorId +
                                       ". Pressure=" + pressure +
                                       ". Found=" + foundContraband + ".";
            WriteInspectionLog("[Inspection] " + currentInspectionSummary);
        }

        private void ApplyContrabandConsequences(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            CampusGameplayRoom room,
            StorageItemModel contrabandItem,
            int pressure)
        {
            if (bootstrap != null && bootstrap.GameState != null)
            {
                int suspicion = Mathf.Clamp(10 + pressure / 5 + Mathf.Max(0, contrabandItem.SuspicionRisk), 8, 40);
                bootstrap.GameState.AddPlayerSuspicion(suspicion);
                bootstrap.GameState.AddTeacherAlertness(Mathf.Clamp(3 + pressure / 25, 3, 7));
                bootstrap.GameState.AddCampusChaos(3);
                bootstrap.GameState.AddCampusOrder(-4);
            }

            if (actorRuntime != null && actorRuntime.Data != null)
            {
                AddMemoryIfMissing(actorRuntime.Data, CampusCharacterMemoryId.FoundContraband);
            }

            if (inspectorRuntime != null && inspectorRuntime.Data != null)
            {
                AddMemoryIfMissing(inspectorRuntime.Data, CampusCharacterMemoryId.FoundContraband);
            }

            dailyContrabandFoundCount++;
            currentInspectionSummary = "Contraband found: " + ResolveItemName(contrabandItem) +
                                       " in " + (room != null ? room.RoomId : "unknown") +
                                       ". Pressure=" + pressure + ".";
            WriteInspectionLog("[Inspection] Contraband found: " + ResolveItemName(contrabandItem) +
                               " in " + (room != null ? room.RoomId : "unknown") +
                               ". Pressure=" + pressure + ".");
        }

        private bool TryConfiscateContraband(
            StorageItemModel item,
            StorageContainerModel source,
            CampusGameplayRoom room,
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime,
            out string message)
        {
            message = string.Empty;
            if (item == null)
            {
                message = "Missing item.";
                return false;
            }

            if (source == null)
            {
                message = "Missing source container.";
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                message = "Storage memory is unavailable.";
                return false;
            }

            StorageContainerModel evidenceContainer = GetOrCreateConfiscatedContainer(memory, room);
            if (evidenceContainer == null)
            {
                message = "Could not create confiscated item container.";
                return false;
            }

            if (!TryFindConfiscationFit(evidenceContainer, item, out Vector2Int targetPosition))
            {
                message = "No evidence storage space.";
                return false;
            }

            int previousX = item.X;
            int previousY = item.Y;
            if (!source.RemoveItem(item))
            {
                message = "Could not remove item from player storage.";
                return false;
            }

            item.AllowTaking = false;
            if (item.LegalState == StorageItemLegalState.Unknown || item.LegalState == StorageItemLegalState.Personal)
            {
                item.LegalState = StorageItemLegalState.Suspicious;
            }

            if (!evidenceContainer.PlaceItem(item, targetPosition.x, targetPosition.y))
            {
                source.PlaceItem(item, previousX, previousY);
                message = "Could not place item in evidence storage.";
                return false;
            }

            PublishConfiscationTransfer(item, source, evidenceContainer, room, actorRuntime, inspectorRuntime);
            message = "Confiscated " + ResolveItemName(item) + " to " + evidenceContainer.Id + ".";
            dailyConfiscatedItemCount++;
            currentInspectionSummary = message;
            WriteInspectionLog("[Inspection] " + message);
            return true;
        }

        private StorageContainerModel GetOrCreateConfiscatedContainer(StorageMemory memory, CampusGameplayRoom room)
        {
            if (memory == null)
            {
                return null;
            }

            StorageContainerModel container = memory.GetOrCreateContainer(
                ConfiscatedContainerId,
                "Confiscated Evidence",
                8,
                12,
                200f);
            if (container == null)
            {
                return null;
            }

            container.AccessPolicy = StorageContainerAccessPolicy.StaffOnly;
            container.OwnerId = "campus_office";
            container.OwnerRole = "Staff";
            container.RoomId = room != null ? room.RoomId : string.Empty;
            container.AllowTakingContents = false;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = 0;
            return container;
        }

        private static bool TryFindConfiscationFit(
            StorageContainerModel container,
            StorageItemModel item,
            out Vector2Int targetPosition)
        {
            targetPosition = default;
            if (container == null || item == null)
            {
                return false;
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (container.FindFirstFit(item, out targetPosition))
                {
                    return true;
                }

                container.Rows += Mathf.Max(2, item.CurrentHeight);
            }

            return false;
        }

        private void PublishConfiscationTransfer(
            StorageItemModel item,
            StorageContainerModel source,
            StorageContainerModel evidenceContainer,
            CampusGameplayRoom room,
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime inspectorRuntime)
        {
            if (gameplayEventHub == null || item == null || evidenceContainer == null)
            {
                return;
            }

            gameplayEventHub.PublishItemTransferred(new CampusItemTransferredEvent(
                ResolveRuntimeId(actorRuntime),
                ResolveInstanceId(item),
                item.DefinitionId,
                ResolveItemName(item),
                source != null ? source.Id : string.Empty,
                evidenceContainer.Id,
                room != null ? room.RoomId : string.Empty,
                StorageTransferReason.InspectionConfiscation,
                false,
                inspectorRuntime != null));
        }

        private bool TryFindCarriedContraband(out StorageItemModel item, out StorageContainerModel container)
        {
            item = null;
            container = null;

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                return false;
            }

            StorageContainerModel[] hands = StoragePlayerInventoryUtility.GetOrCreateHandContainers(memory);
            if (TryFindContrabandInContainers(hands, out item, out container))
            {
                return true;
            }

            StorageContainerModel[] pockets = StoragePlayerInventoryUtility.GetOrCreatePocketContainers(memory);
            if (TryFindContrabandInContainers(pockets, out item, out container))
            {
                return true;
            }

            StorageContainerModel backpack = StoragePlayerInventoryUtility.GetOrCreateBackpack(memory);
            return TryFindContrabandInContainer(backpack, out item, out container);
        }

        private int ResolveNpcProactivePressure(
            CampusCharacterRuntime npcRuntime,
            CampusGameplayRoom playerRoom,
            bool hasContraband)
        {
            if (npcRuntime == null || npcRuntime.Data == null)
            {
                return 0;
            }

            int pressure = ResolveNpcVigilancePressure(npcRuntime).Value;
            pressure += Mathf.RoundToInt((ResolveAreaSearchPressure(playerRoom).Value +
                                          ResolveAreaQuestioningPressure(playerRoom).Value) * 0.35f);
            if (bootstrap != null && bootstrap.GameState != null)
            {
                pressure += Mathf.RoundToInt(bootstrap.GameState.PlayerSuspicion * 0.45f);
                pressure += Mathf.RoundToInt(bootstrap.GameState.TeacherAlertness * 0.25f);
            }

            CampusCharacterRuntime playerRuntime = rosterService != null ? rosterService.PlayerRuntime : null;
            string playerId = ResolveRuntimeId(playerRuntime);
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                pressure += Mathf.RoundToInt(npcRuntime.Data.GetRelationshipSuspicion(playerId) * 0.35f);
            }

            if (hasContraband)
            {
                pressure += 22;
            }

            if (npcRuntime.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                pressure += 8;
            }

            return Mathf.Clamp(pressure, 0, 100);
        }

        private void HandleTattletaleReport(
            CampusCharacterRuntime reporterRuntime,
            CampusCharacterRuntime playerRuntime,
            CampusGameplayRoom room,
            bool hasContraband)
        {
            string reporterId = ResolveRuntimeId(reporterRuntime);
            string playerId = ResolveRuntimeId(playerRuntime);
            dailyTattletaleReportCount++;
            if (reporterRuntime != null && reporterRuntime.Data != null && !string.IsNullOrWhiteSpace(playerId))
            {
                reporterRuntime.Data.AddRelationshipSuspicion(playerId, hasContraband ? 12 : 7);
                reporterRuntime.Data.AddRelationshipTrust(playerId, -3);
                AddMemoryIfMissing(reporterRuntime.Data, CampusCharacterMemoryId.WarnedAboutActor);
            }

            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.AddTeacherAlertness(hasContraband ? 5 : 3);
                bootstrap.GameState.AddPlayerSuspicion(hasContraband ? 4 : 2);
                bootstrap.GameState.AddCampusChaos(1);
            }

            currentInspectionSummary = reporterId + " reported " + playerId +
                                       (hasContraband ? " for suspected contraband." : " as suspicious.") +
                                       " Room=" + (room != null ? room.RoomId : "-") + ".";
            WriteInspectionLog("[Inspection] " + currentInspectionSummary);
        }

        private StorageItemModel CreateDebugContraband(StorageMemory memory, CampusGameplayRoom room)
        {
            string instanceId = "debug_contraband_" + Guid.NewGuid().ToString("N");
            StorageItemModel item = memory != null && memory.ItemRegistry != null
                ? memory.ItemRegistry.CreateItem("note", instanceId)
                : null;

            if (item == null)
            {
                item = new StorageItemModel
                {
                    Id = instanceId,
                    InstanceId = instanceId,
                    DefinitionId = "debug_contraband"
                };
            }

            item.DefinitionId = string.IsNullOrWhiteSpace(item.DefinitionId) ? "debug_contraband" : item.DefinitionId;
            item.DisplayName = "Debug Contraband";
            item.Width = 1;
            item.Height = 1;
            item.Weight = Mathf.Max(0.01f, item.Weight);
            item.Description = "Runtime seeded inspection evidence.";
            item.ThemeColor = new Color(0.72f, 0.22f, 0.18f, 1f);
            item.LegalState = StorageItemLegalState.Stolen;
            item.OwnerId = "campus_debug_owner";
            item.SourceContainerId = "debug_locked_storage";
            item.SourceRoomId = room != null ? room.RoomId : string.Empty;
            item.SourceLocation = "Debug contraband seed";
            item.AllowTaking = false;
            item.StolenDuringSession = true;
            item.SuspicionRisk = 22;
            return item;
        }

        private static bool TryFindContrabandInContainers(
            StorageContainerModel[] containers,
            out StorageItemModel item,
            out StorageContainerModel container)
        {
            item = null;
            container = null;
            if (containers == null)
            {
                return false;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                if (TryFindContrabandInContainer(containers[i], out item, out container))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindContrabandInContainer(
            StorageContainerModel candidate,
            out StorageItemModel item,
            out StorageContainerModel container)
        {
            item = null;
            container = null;
            if (candidate == null || candidate.Items == null)
            {
                return false;
            }

            for (int i = 0; i < candidate.Items.Count; i++)
            {
                StorageItemModel candidateItem = candidate.Items[i];
                if (candidateItem != null && candidateItem.IsStolenEvidence)
                {
                    item = candidateItem;
                    container = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveInspectionContext(
            out CampusCharacterRuntime playerRuntime,
            out CampusGameplayRoom room,
            out string message)
        {
            playerRuntime = null;
            room = null;
            message = string.Empty;
            if (bootstrap == null || rosterService == null || worldService == null)
            {
                message = "Inspection services are not initialized.";
                return false;
            }

            playerRuntime = rosterService.PlayerRuntime;
            if (playerRuntime == null || playerRuntime.Data == null)
            {
                message = "Player runtime is unavailable.";
                return false;
            }

            room = worldService.FindRoomForRuntime(playerRuntime);
            if (room == null)
            {
                message = "Player is not inside a gameplay room.";
                return false;
            }

            message = "Ready.";
            return true;
        }

        private bool TryFindBestInspector(
            CampusGameplayRoom room,
            CampusCharacterRuntime actorRuntime,
            bool requireAuthority,
            out CampusCharacterRuntime inspector)
        {
            inspector = null;
            if (room == null || rosterService == null)
            {
                return false;
            }

            float bestScore = float.MinValue;
            for (int i = 0; i < rosterService.Runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = rosterService.Runtimes[i];
                if (!CanInspect(runtime, actorRuntime, room, requireAuthority))
                {
                    continue;
                }

                float distance = actorRuntime != null
                    ? Vector2.Distance(runtime.transform.position, actorRuntime.transform.position)
                    : 0f;
                if (distance > maxInspectionDistance)
                {
                    continue;
                }

                int vigilance = ResolveNpcVigilancePressure(runtime).Value;
                float score = vigilance + Mathf.Max(0f, maxInspectionDistance - distance) * 8f;
                if (score > bestScore)
                {
                    bestScore = score;
                    inspector = runtime;
                }
            }

            return inspector != null;
        }

        private bool TryFindHighestVigilanceNpc(
            CampusGameplayRoom room,
            CampusCharacterRuntime actorRuntime,
            out CampusCharacterRuntime highestNpc)
        {
            highestNpc = null;
            if (room == null || rosterService == null)
            {
                return false;
            }

            int highestPressure = -1;
            for (int i = 0; i < rosterService.Runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = rosterService.Runtimes[i];
                if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                if (actorRuntime != null &&
                    string.Equals(runtime.CharacterId, actorRuntime.CharacterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsRuntimeInRoom(runtime, room))
                {
                    continue;
                }

                if (actorRuntime != null &&
                    Vector2.Distance(runtime.transform.position, actorRuntime.transform.position) > maxInspectionDistance)
                {
                    continue;
                }

                int pressure = ResolveNpcVigilancePressure(runtime).Value;
                if (pressure > highestPressure)
                {
                    highestPressure = pressure;
                    highestNpc = runtime;
                }
            }

            return highestNpc != null;
        }

        private bool CanInspect(
            CampusCharacterRuntime runtime,
            CampusCharacterRuntime actorRuntime,
            CampusGameplayRoom room,
            bool requireAuthority)
        {
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return false;
            }

            if (actorRuntime != null &&
                string.Equals(runtime.CharacterId, actorRuntime.CharacterId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsRuntimeInRoom(runtime, room))
            {
                return false;
            }

            if (requireAuthority)
            {
                return IsAuthority(runtime);
            }

            return IsAuthority(runtime) ||
                   runtime.Data.HasTrait(CampusCharacterTrait.Tattletale) ||
                   runtime.Data.HasTrait(CampusCharacterTrait.GoodStudent) ||
                   HasNpcSpecificVigilance(runtime);
        }

        private bool IsRuntimeInRoom(CampusCharacterRuntime runtime, CampusGameplayRoom room)
        {
            if (runtime == null || runtime.Data == null || room == null)
            {
                return false;
            }

            CampusGameplayRoom currentRoom = worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
            string currentRoomId = currentRoom != null ? currentRoom.RoomId : runtime.Data.CurrentRoomId;
            return string.Equals(currentRoomId, room.RoomId, StringComparison.OrdinalIgnoreCase);
        }

        private int ResolveSearchPressure(CampusGameplayRoom room, CampusCharacterRuntime inspector, StorageItemModel contrabandItem)
        {
            int pressure = globalSearchPressure.Value;
            pressure += ResolveAreaSearchPressure(room).Value;
            pressure += Mathf.RoundToInt(ResolveNpcVigilancePressure(inspector).Value * 0.45f);
            if (bootstrap != null && bootstrap.GameState != null)
            {
                pressure += Mathf.RoundToInt(bootstrap.GameState.TeacherAlertness * 0.25f);
                pressure += Mathf.RoundToInt(bootstrap.GameState.PlayerSuspicion * 0.35f);
            }

            if (contrabandItem != null)
            {
                pressure += Mathf.Clamp(10 + Mathf.Max(0, contrabandItem.SuspicionRisk) / 2, 10, 24);
            }

            return Mathf.Clamp(pressure, 0, 100);
        }

        private int ResolveQuestioningPressure(CampusGameplayRoom room, CampusCharacterRuntime inspector, bool hasContraband)
        {
            int pressure = globalQuestioningPressure.Value;
            pressure += ResolveAreaQuestioningPressure(room).Value;
            pressure += Mathf.RoundToInt(ResolveNpcVigilancePressure(inspector).Value * 0.35f);
            if (bootstrap != null && bootstrap.GameState != null)
            {
                pressure += Mathf.RoundToInt(bootstrap.GameState.TeacherAlertness * 0.15f);
                pressure += Mathf.RoundToInt(bootstrap.GameState.PlayerSuspicion * 0.25f);
            }

            if (hasContraband)
            {
                pressure += 8;
            }

            return Mathf.Clamp(pressure, 0, 100);
        }

        private CampusInspectionPressure ResolveAreaSearchPressure(CampusGameplayRoom room)
        {
            CampusAreaInspectionPressureRule rule = FindMatchingAreaRule(room);
            return rule != null ? rule.SearchPressure : CampusInspectionPressure.Of(0);
        }

        private CampusInspectionPressure ResolveAreaQuestioningPressure(CampusGameplayRoom room)
        {
            CampusAreaInspectionPressureRule rule = FindMatchingAreaRule(room);
            return rule != null ? rule.QuestioningPressure : CampusInspectionPressure.Of(0);
        }

        private CampusInspectionPressure ResolveNpcVigilancePressure(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return CampusInspectionPressure.Of(0);
            }

            if (TryResolveSpecificNpcVigilance(runtime.CharacterId, out CampusInspectionPressure specificPressure))
            {
                return specificPressure;
            }

            CampusCharacterData data = runtime.Data;
            if (data.Role == CampusCharacterRole.Teacher)
            {
                return CampusInspectionPressure.Of(48);
            }

            if (data.Role == CampusCharacterRole.Staff)
            {
                return CampusInspectionPressure.Of(42);
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                return CampusInspectionPressure.Of(28);
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                return CampusInspectionPressure.Of(18);
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                return CampusInspectionPressure.Of(5);
            }

            return CampusInspectionPressure.Of(12);
        }

        private bool HasNpcSpecificVigilance(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   TryResolveSpecificNpcVigilance(runtime.CharacterId, out CampusInspectionPressure pressure) &&
                   pressure.Value > 0;
        }

        private bool TryResolveSpecificNpcVigilance(string characterId, out CampusInspectionPressure pressure)
        {
            pressure = CampusInspectionPressure.Of(0);
            if (npcPressureRules == null || string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            for (int i = 0; i < npcPressureRules.Count; i++)
            {
                CampusNpcInspectionPressureRule rule = npcPressureRules[i];
                if (rule != null && rule.Matches(characterId))
                {
                    pressure = rule.VigilancePressure;
                    return true;
                }
            }

            return false;
        }

        private CampusAreaInspectionPressureRule FindMatchingAreaRule(CampusGameplayRoom room)
        {
            if (room == null || areaPressureRules == null)
            {
                return null;
            }

            CampusAreaInspectionPressureRule fallback = null;
            for (int i = 0; i < areaPressureRules.Count; i++)
            {
                CampusAreaInspectionPressureRule rule = areaPressureRules[i];
                if (rule == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(rule.RoomId) && rule.Matches(room))
                {
                    return rule;
                }

                if (fallback == null && rule.Matches(room))
                {
                    fallback = rule;
                }
            }

            return fallback;
        }

        private CampusAreaInspectionPressureRule FindAreaRule(string roomId, CampusRoomType roomType)
        {
            if (areaPressureRules == null)
            {
                return null;
            }

            string normalizedRoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            for (int i = 0; i < areaPressureRules.Count; i++)
            {
                CampusAreaInspectionPressureRule rule = areaPressureRules[i];
                if (rule == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(normalizedRoomId) &&
                    string.Equals(rule.RoomId, normalizedRoomId, StringComparison.OrdinalIgnoreCase))
                {
                    return rule;
                }

                if (string.IsNullOrWhiteSpace(normalizedRoomId) &&
                    roomType != CampusRoomType.Unknown &&
                    rule.RoomType == roomType)
                {
                    return rule;
                }
            }

            return null;
        }

        private void EnsureDefaultPressureRules()
        {
            areaPressureRules = areaPressureRules ?? new List<CampusAreaInspectionPressureRule>();
            if (areaPressureRules.Count > 0)
            {
                return;
            }

            areaPressureRules.Add(CreateAreaRule(CampusRoomType.Office, 32, 26));
            areaPressureRules.Add(CreateAreaRule(CampusRoomType.Canteen, 22, 18));
            areaPressureRules.Add(CreateAreaRule(CampusRoomType.Store, 24, 19));
            areaPressureRules.Add(CreateAreaRule(CampusRoomType.Classroom, 14, 15));
            areaPressureRules.Add(CreateAreaRule(CampusRoomType.Corridor, 8, 12));
            areaPressureRules.Add(CreateAreaRule(CampusRoomType.Outdoor, 10, 9));
            areaPressureRules.Add(CreateAreaRule(CampusRoomType.Dormitory, 6, 8));
        }

        private static CampusAreaInspectionPressureRule CreateAreaRule(
            CampusRoomType roomType,
            int searchPressure,
            int questioningPressure)
        {
            return new CampusAreaInspectionPressureRule
            {
                RoomType = roomType,
                SearchPressure = CampusInspectionPressure.Of(searchPressure),
                QuestioningPressure = CampusInspectionPressure.Of(questioningPressure)
            };
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (worldService == null && bootstrap != null)
            {
                worldService = bootstrap.WorldService;
            }

            if (rosterService == null && bootstrap != null)
            {
                rosterService = bootstrap.RosterService;
            }

            if (gameplayEventHub == null && bootstrap != null)
            {
                gameplayEventHub = bootstrap.GameplayEventHub;
            }

            if (timeController == null && bootstrap != null)
            {
                timeController = bootstrap.TimeController;
            }
        }

        private static bool Roll(int pressure)
        {
            return pressure > 0 && UnityEngine.Random.value <= Mathf.Clamp01(pressure / 100f);
        }

        private void SubscribeTime()
        {
            if (subscribedToTime || timeController == null)
            {
                return;
            }

            timeController.DailySettlementStarted += HandleDailySettlementStarted;
            subscribedToTime = true;
        }

        private void ReleaseTimeSubscription()
        {
            if (!subscribedToTime || timeController == null)
            {
                subscribedToTime = false;
                return;
            }

            timeController.DailySettlementStarted -= HandleDailySettlementStarted;
            subscribedToTime = false;
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            dailyQuestioningCount = 0;
            dailySearchCount = 0;
            dailyContrabandFoundCount = 0;
            dailyConfiscatedItemCount = 0;
            dailyTattletaleReportCount = 0;
            dailyProactiveInspectionCount = 0;
            proactiveActionCooldownByNpc.Clear();
            currentInspectionSummary = "Inspection counters reset for the new day.";
        }

        private static bool IsAuthority(CampusCharacterRuntime runtime)
        {
            return runtime != null &&
                   runtime.Data != null &&
                   (runtime.Data.Role == CampusCharacterRole.Teacher ||
                    runtime.Data.Role == CampusCharacterRole.Staff);
        }

        private static string ResolveRuntimeId(CampusCharacterRuntime runtime)
        {
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.CharacterId
                : string.Empty;
        }

        private static string ResolveRuntimeName(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return string.Empty;
            }

            string displayName = runtime.Data.DisplayName;
            return string.IsNullOrWhiteSpace(displayName) ? runtime.CharacterId : displayName;
        }

        private static string ResolveInstanceId(StorageItemModel item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(item.InstanceId) ? item.InstanceId : item.Id;
        }

        private static string ResolveItemName(StorageItemModel item)
        {
            if (item == null)
            {
                return "item";
            }

            if (!string.IsNullOrWhiteSpace(item.DisplayName))
            {
                return item.DisplayName;
            }

            return !string.IsNullOrWhiteSpace(item.DefinitionId) ? item.DefinitionId : "item";
        }

        private static float PseudoRandom01(string key, int salt)
        {
            unchecked
            {
                int value = 23;
                if (!string.IsNullOrEmpty(key))
                {
                    for (int i = 0; i < key.Length; i++)
                    {
                        value = value * 31 + key[i];
                    }
                }

                value ^= salt * 374761393;
                value = (value << 13) ^ value;
                int mixed = value * (value * value * 15731 + 789221) + 1376312589;
                return (mixed & 0x7fffffff) / 2147483647f;
            }
        }

        private static int CountConfiscatedItems()
        {
            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null ||
                !memory.TryGetContainer(ConfiscatedContainerId, out StorageContainerModel container) ||
                container.Items == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < container.Items.Count; i++)
            {
                if (container.Items[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void WriteInspectionLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(message);
            }
        }

        private static void AddMemoryIfMissing(CampusCharacterData data, CampusCharacterMemoryId memory)
        {
            if (data == null || memory == CampusCharacterMemoryId.None || data.HasMemory(memory))
            {
                return;
            }

            data.AddMemory(memory);
        }
    }
}
