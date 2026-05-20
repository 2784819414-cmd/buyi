using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.Gameplay.UI;
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
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private CampusTimeController timeController;
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
        [SerializeField] private string currentInspectionSummary =
            CampusInspectionTextCatalog.Get(CampusInspectionTextId.WaitingForActivity);

        private float nextSearchAllowedTime;
        private float nextQuestioningAllowedTime;
        private bool subscribedToTime;
        private readonly Dictionary<string, float> proactiveActionCooldownByNpc =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly CampusInspectionFacts facts = new CampusInspectionFacts();
        private CampusInspectionActions actions;

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

        internal CampusGameBootstrap Bootstrap => bootstrap;
        internal CampusGameplayEventHub GameplayEventHub => gameplayEventHub;
        internal float SearchCooldownSeconds => searchCooldownSeconds;
        internal float QuestioningCooldownSeconds => questioningCooldownSeconds;
        internal float MaxInspectionDistance => maxInspectionDistance;

        internal CampusInspectionFacts Facts
        {
            get
            {
                ConfigureFacts();
                return facts;
            }
        }

        private CampusInspectionActions Actions
        {
            get
            {
                if (actions == null)
                {
                    actions = new CampusInspectionActions(this);
                }

                return actions;
            }
        }

        private void Reset()
        {
            EnsureDefaultPressureRules();
        }

        private void OnValidate()
        {
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
            return BuildDebugSnapshot(ResolveDefaultInspectionTarget());
        }

        public CampusInspectionDebugSnapshot BuildDebugSnapshot(CampusCharacterRuntime requestedTarget)
        {
            ResolveReferences();
            if (!Facts.TryResolveInspectionTarget(requestedTarget, out CampusCharacterRuntime targetRuntime, out CampusGameplayRoom room, out string status))
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
                    CampusContrabandService.CountConfiscatedItems());
            }

            bool hasContraband = CampusContrabandService.TryFindCarriedContraband(
                targetRuntime,
                out StorageItemModel contrabandItem,
                out StorageContainerModel contrabandContainer);
            Facts.TryFindBestInspector(room, targetRuntime, true, out CampusCharacterRuntime searchInspector);
            Facts.TryFindBestInspector(room, targetRuntime, false, out CampusCharacterRuntime questioner);
            Facts.TryFindHighestVigilanceNpc(room, targetRuntime, out CampusCharacterRuntime highestNpc);

            int searchPressure = searchInspector != null
                ? Facts.ResolveSearchPressure(room, searchInspector, targetRuntime, contrabandItem)
                : 0;
            int questioningPressure = questioner != null
                ? Facts.ResolveQuestioningPressure(room, questioner, targetRuntime, hasContraband)
                : 0;

            string snapshotStatus = status;
            if (searchInspector == null)
            {
                snapshotStatus = CampusInspectionTextCatalog.Get(CampusInspectionTextId.NoAuthorityInspectorInRange);
            }
            else if (questioner == null)
            {
                snapshotStatus = CampusInspectionTextCatalog.Get(CampusInspectionTextId.SearchReadyNoQuestioningNpc);
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
                Facts.ResolveNpcVigilancePressure(highestNpc).Value,
                Facts.ResolveAreaSearchPressure(room).Value,
                Facts.ResolveAreaQuestioningPressure(room).Value,
                searchPressure,
                questioningPressure,
                SearchCooldownRemaining,
                QuestioningCooldownRemaining,
                hasContraband,
                ResolveItemName(contrabandItem),
                contrabandContainer != null ? contrabandContainer.Id : string.Empty,
                CampusContrabandService.CountConfiscatedItems());
        }

        public bool TryForceQuestioning(out string message)
        {
            ResolveReferences();
            return TryForceQuestioning(ResolveDefaultInspectionTarget(), out message);
        }

        public bool TryForceQuestioning(CampusCharacterRuntime requestedTarget, out string message)
        {
            ResolveReferences();
            return Actions.TryForceQuestioning(requestedTarget, out message);
        }

        public bool TryForceSearch(out string message)
        {
            ResolveReferences();
            return TryForceSearch(ResolveDefaultInspectionTarget(), out message);
        }

        public bool TryForceSearch(CampusCharacterRuntime requestedTarget, out string message)
        {
            ResolveReferences();
            return Actions.TryForceSearch(requestedTarget, out message);
        }

        public bool TrySeedDebugContraband(out string message)
        {
            ResolveReferences();
            return TrySeedDebugContraband(ResolveDefaultInspectionTarget(), out message);
        }

        public bool TrySeedDebugContraband(CampusCharacterRuntime requestedTarget, out string message)
        {
            ResolveReferences();
            if (!Facts.TryResolveInspectionTarget(requestedTarget, out CampusCharacterRuntime targetRuntime, out CampusGameplayRoom room, out message))
            {
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            if (memory == null)
            {
                message = CampusInspectionTextCatalog.Get(CampusInspectionTextId.StorageMemoryUnavailable);
                return false;
            }

            CampusCharacterInventoryService.GetOrCreateInventory(targetRuntime, false);
            StorageItemModel item = CreateDebugContraband(memory, room);
            StorageTransferContext context = StorageTransferContext.ForActor(targetRuntime.gameObject, StorageTransferReason.SystemSeed);
            context.ActorId = ResolveRuntimeId(targetRuntime);
            context.RoomId = room.RoomId;
            context.AllowProtectedTake = true;
            context.SuppressNpcDetection = true;
            context.SuppressSuspicion = true;
            context.SourceLocation = CampusInspectionTextCatalog.Get(CampusInspectionTextId.DebugContrabandSeedSource);
            context.OwnerId = item.OwnerId;

            CampusInventoryTransferService transferService = CampusInventoryTransferService.Resolve();
            if (!transferService.TryPickUpIntoHands(memory, item, context, out StorageTransferResult result))
            {
                message = result.Message;
                return false;
            }

            message = CampusInspectionTextCatalog.Format(CampusInspectionTextId.SeededCarriedContraband, ResolveItemName(item));
            WriteInspectionLog(CampusInspectionTextCatalog.Format(CampusInspectionTextId.InspectionDebugLogLine, message));
            return true;
        }

        public bool TryNpcProactiveInspection(CampusCharacterRuntime npcRuntime, out string line)
        {
            ResolveReferences();
            return TryNpcProactiveInspection(npcRuntime, ResolveDefaultInspectionTarget(), out line);
        }

        internal bool TryBuildNpcProactiveOpportunity(
            CampusCharacterRuntime npcRuntime,
            CampusCharacterRuntime requestedTarget,
            out CampusInspectionNpcOpportunity opportunity)
        {
            ResolveReferences();
            return Actions.TryBuildNpcProactiveOpportunity(npcRuntime, requestedTarget, out opportunity);
        }

        public bool TryNpcProactiveInspection(
            CampusCharacterRuntime npcRuntime,
            CampusCharacterRuntime requestedTarget,
            out string line)
        {
            ResolveReferences();
            return Actions.TryNpcProactiveInspection(npcRuntime, requestedTarget, out line);
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
            item.DisplayName = CampusInspectionTextCatalog.Get(CampusDisplayLanguage.Chinese, CampusInspectionTextId.DebugContrabandDisplayName);
            item.LocalizedDisplayName = CampusInspectionTextCatalog.Localized(CampusInspectionTextId.DebugContrabandDisplayName);
            item.Width = 1;
            item.Height = 1;
            item.Weight = Mathf.Max(0.01f, item.Weight);
            item.Description = CampusInspectionTextCatalog.Get(CampusDisplayLanguage.Chinese, CampusInspectionTextId.DebugContrabandDescription);
            item.LocalizedDescription = CampusInspectionTextCatalog.Localized(CampusInspectionTextId.DebugContrabandDescription);
            item.ThemeColor = new Color(0.72f, 0.22f, 0.18f, 1f);
            item.LegalState = StorageItemLegalState.Stolen;
            item.OwnerId = "campus_debug_owner";
            item.SourceContainerId = "debug_locked_storage";
            item.SourceRoomId = room != null ? room.RoomId : string.Empty;
            item.SourceLocation = CampusInspectionTextCatalog.Get(CampusInspectionTextId.DebugContrabandSeedSource);
            item.AllowTaking = false;
            item.StolenDuringSession = true;
            item.SuspicionRisk = 22;
            return item;
        }

        private CampusCharacterRuntime ResolveDefaultInspectionTarget()
        {
            return rosterService != null ? rosterService.PlayerRuntime : null;
        }

        internal bool IsNpcProactiveCooldownReady(string npcId)
        {
            return string.IsNullOrWhiteSpace(npcId) ||
                   !proactiveActionCooldownByNpc.TryGetValue(npcId, out float cooldownUntil) ||
                   Time.time >= cooldownUntil;
        }

        internal void SetNpcProactiveCooldown(string npcId, float seconds)
        {
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                proactiveActionCooldownByNpc[npcId] = Time.time + Mathf.Max(0f, seconds);
            }
        }

        internal void SetSearchCooldown(float seconds)
        {
            nextSearchAllowedTime = Time.time + Mathf.Max(0f, seconds);
        }

        internal void SetQuestioningCooldown(float seconds)
        {
            nextQuestioningAllowedTime = Time.time + Mathf.Max(0f, seconds);
        }

        internal void HoldSearchCooldown(float seconds)
        {
            nextSearchAllowedTime = Mathf.Max(nextSearchAllowedTime, Time.time + Mathf.Max(0f, seconds));
        }

        internal void HoldQuestioningCooldown(float seconds)
        {
            nextQuestioningAllowedTime = Mathf.Max(nextQuestioningAllowedTime, Time.time + Mathf.Max(0f, seconds));
        }

        internal void MarkQuestioning()
        {
            dailyQuestioningCount++;
        }

        internal void MarkSearch()
        {
            dailySearchCount++;
        }

        internal void MarkContrabandFound()
        {
            dailyContrabandFoundCount++;
        }

        internal void MarkConfiscatedItem()
        {
            dailyConfiscatedItemCount++;
        }

        internal void MarkTattletaleReport()
        {
            dailyTattletaleReportCount++;
        }

        internal void MarkProactiveInspection()
        {
            dailyProactiveInspectionCount++;
        }

        internal void SetInspectionSummary(string summary)
        {
            currentInspectionSummary = summary ?? string.Empty;
        }

        internal void AddPlayerSuspicionIfTargetIsPlayer(CampusCharacterRuntime targetRuntime, int amount)
        {
            if (amount <= 0 ||
                targetRuntime == null ||
                targetRuntime.Data == null ||
                !targetRuntime.Data.IsPlayerControlled ||
                bootstrap == null ||
                bootstrap.GameState == null)
            {
                return;
            }

            bootstrap.GameState.AddPlayerSuspicion(amount);
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

            ConfigureFacts();
        }

        private void ConfigureFacts()
        {
            facts.Configure(
                bootstrap,
                worldService,
                rosterService,
                maxInspectionDistance,
                globalSearchPressure,
                globalQuestioningPressure,
                areaPressureRules,
                npcPressureRules);
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
            currentInspectionSummary = CampusInspectionTextCatalog.Get(CampusInspectionTextId.DailyCountersReset);
        }

        internal static string ResolveRuntimeId(CampusCharacterRuntime runtime)
        {
            return runtime != null && !string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.CharacterId
                : string.Empty;
        }

        internal static string ResolveRuntimeName(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return string.Empty;
            }

            string displayName = runtime.Data.DisplayName;
            return string.IsNullOrWhiteSpace(displayName) ? runtime.CharacterId : displayName;
        }

        internal static string ResolveInstanceId(StorageItemModel item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(item.InstanceId) ? item.InstanceId : item.Id;
        }

        internal static string ResolveItemName(StorageItemModel item)
        {
            if (item == null)
            {
                return CampusInspectionTextCatalog.Get(CampusInspectionTextId.ItemFallback);
            }

            return item.GetDisplayName();
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

        internal void WriteInspectionLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null && !string.IsNullOrWhiteSpace(message))
            {
                bootstrap.EventLog.AddLog(message);
            }
        }

        internal static void AddMemoryIfMissing(CampusCharacterData data, CampusCharacterMemoryId memory)
        {
            if (data == null || memory == CampusCharacterMemoryId.None || data.HasMemory(memory))
            {
                return;
            }

            data.AddMemory(memory);
        }
    }
}
