using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Canteen;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.Schedule;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Pranks
{
    public enum CampusPrankType
    {
        Unknown = 0,
        PassNote = 1,
        CanteenFoodTheft = 2,
        DeliveryTheft = 3
    }

    public enum CampusCanteenClerkState
    {
        Unknown = 0,
        WatchingCounter = 1,
        PreparingMalatang = 2,
        CookingNoodles = 3,
        FetchingIngredients = 4,
        BrieflyAway = 5
    }

    public enum CampusDeliveryOrderState
    {
        None = 0,
        WaitingPickup = 1,
        Stolen = 2,
        Reported = 3,
        Served = 4,
        Searching = 5
    }

    [DisallowMultipleComponent]
    public sealed class CampusPrankService : MonoBehaviour
    {
        public const string PassNotePayload = CampusPrankPayloadIds.PassNote;

        private const float WorldSyncIntervalSeconds = 0.75f;
        private const int SuspicionWarningThreshold = 70;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusClassroomLoopService classroomLoopService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField] private bool logValidationIssues = true;
        [SerializeField, Min(0.1f)] private float prankCooldownSeconds = 1.25f;
        [SerializeField, Min(1)] private int basePassNoteReward = 5;

        [SerializeField] private string currentPrompt = string.Empty;
        [SerializeField] private int dailyPassNoteCount;
        [SerializeField] private int dailyCanteenTheftCount;
        [SerializeField] private int dailyDeliveryTheftCount;
        [SerializeField] private string activeDeliveryOwnerId = string.Empty;
        [SerializeField] private string activeDeliveryItemName = string.Empty;
        [SerializeField] private CampusDeliveryOrderState activeDeliveryOrderState = CampusDeliveryOrderState.None;
        [SerializeField] private float activeDeliveryCreatedTime = -999f;
        [SerializeField] private float activeDeliveryStolenTime = -999f;
        [SerializeField] private float activeDeliveryResolvedUntilTime = -999f;
        [SerializeField] private bool activeDeliveryMissingNoticed;
        [SerializeField] private bool activeDeliverySearchDecisionMade;
        [SerializeField] private bool activeDeliveryReportedByOwner;
        [SerializeField] private float lastPrankTime = -999f;

        private float nextWorldSyncTime = -999f;
        private float nextDeliveryRefreshTime = -999f;
        private readonly CampusPrankFacts facts = new CampusPrankFacts();
        private readonly List<CampusEcologyValidator.ValidationIssue> validationIssues =
            new List<CampusEcologyValidator.ValidationIssue>();
        private CampusPrankActions actions;
        private bool hasValidatedSetup;

        public string CurrentPrompt => currentPrompt;
        public int DailyPassNoteCount => dailyPassNoteCount;
        public int DailyCanteenTheftCount => dailyCanteenTheftCount;
        public int DailyDeliveryTheftCount => dailyDeliveryTheftCount;
        public CampusCanteenClerkState CurrentCanteenClerkState => ResolveCanteenClerkState(null, null);
        public string ActiveDeliveryOwnerId => activeDeliveryOwnerId;
        public string ActiveDeliveryItemName => activeDeliveryItemName;
        public CampusDeliveryOrderState ActiveDeliveryOrderState => activeDeliveryOrderState;
        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> ValidationIssues => validationIssues;

        internal CampusGameBootstrap Bootstrap => bootstrap;
        internal CampusWorldService WorldService => worldService;
        internal CampusRosterService RosterService => rosterService;
        internal CampusScheduleService ScheduleService => scheduleService;
        internal CampusClassroomLoopService ClassroomLoopService => classroomLoopService;
        internal CampusGameplayEventHub GameplayEventHub => gameplayEventHub;
        internal float PrankCooldownSeconds => prankCooldownSeconds;
        internal float LastPrankTime => lastPrankTime;
        internal int BasePassNoteReward => basePassNoteReward;

        private CampusPrankActions Actions
        {
            get
            {
                if (actions == null)
                {
                    actions = new CampusPrankActions(this);
                }

                return actions;
            }
        }

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            classroomLoopService = bootstrap != null ? bootstrap.ClassroomLoopService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;

            if (bootstrap != null && bootstrap.TimeController != null)
            {
                bootstrap.TimeController.DailySettlementStarted -= HandleDailySettlementStarted;
                bootstrap.TimeController.DailySettlementStarted += HandleDailySettlementStarted;
                bootstrap.TimeController.SegmentChanged -= HandleSegmentChanged;
                bootstrap.TimeController.SegmentChanged += HandleSegmentChanged;
            }

            SyncPlacedPrankObjects(forceImmediate: true);
            if (!hasValidatedSetup)
            {
                ValidateSetup(logValidationIssues);
            }

            EnsureDeliveryOrder(false);
            RefreshPrompt();
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.TimeController != null)
            {
                bootstrap.TimeController.DailySettlementStarted -= HandleDailySettlementStarted;
                bootstrap.TimeController.SegmentChanged -= HandleSegmentChanged;
            }
        }

        private void Update()
        {
            SyncPlacedPrankObjects(forceImmediate: false);
            EnsureDeliveryOrder(false);
            UpdateDeliveryOwnerProgress();
            RefreshPrompt();
        }

        private void HandleSegmentChanged(CampusTimeSegment _, CampusTimeSegment __)
        {
            EnsureDeliveryOrder(true);
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            dailyPassNoteCount = 0;
            dailyCanteenTheftCount = 0;
            dailyDeliveryTheftCount = 0;
            activeDeliveryOwnerId = string.Empty;
            activeDeliveryItemName = string.Empty;
            activeDeliveryOrderState = CampusDeliveryOrderState.None;
            activeDeliveryCreatedTime = -999f;
            activeDeliveryStolenTime = -999f;
            activeDeliveryResolvedUntilTime = -999f;
            activeDeliveryMissingNoticed = false;
            activeDeliverySearchDecisionMade = false;
            activeDeliveryReportedByOwner = false;

            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.SetPlayerSuspicion(Mathf.Max(0, bootstrap.GameState.PlayerSuspicion - 35));
            }
        }

        public bool SupportsPayload(string payload)
        {
            return CampusPrankCatalog.TryGetByPayload(payload, out _);
        }

        public IReadOnlyList<CampusEcologyValidator.ValidationIssue> ValidateSetup(bool logIssues)
        {
            SyncPlacedPrankObjects(true);
            validationIssues.Clear();
            List<CampusEcologyValidator.ValidationIssue> issues = CampusPrankValidator.Validate(facts.Targets);
            for (int i = 0; i < issues.Count; i++)
            {
                validationIssues.Add(issues[i]);
            }

            if (logIssues)
            {
                CampusPrankValidator.LogIssues(validationIssues);
            }

            hasValidatedSetup = true;
            return validationIssues;
        }

        internal bool TryFindNpcPrankTarget(
            CampusCharacterRuntime actor,
            Predicate<string> payloadFilter,
            out CampusPrankTarget target)
        {
            SyncPlacedPrankObjects(false);
            return facts.TryFindTarget(actor, payloadFilter, out target);
        }

        public bool CanExecutePayload(string payload, GameObject actor, out string unavailableReason)
        {
            return Actions.CanExecutePayload(payload, actor, out unavailableReason);
        }

        public bool TryExecutePayload(string payload, GameObject actor)
        {
            return Actions.TryExecutePayload(payload, actor);
        }

        private bool IsEveningStudyOrNightErrand()
        {
            CampusTimeController time = bootstrap != null ? bootstrap.TimeController : null;
            if (time == null)
            {
                return false;
            }

            switch (time.CurrentSegment)
            {
                case CampusTimeSegment.EveningStudy1:
                case CampusTimeSegment.EveningStudy2:
                case CampusTimeSegment.EveningStudy3:
                case CampusTimeSegment.EveningBreak1:
                case CampusTimeSegment.EveningBreak2:
                case CampusTimeSegment.NightFree:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsDeliveryWindowNow()
        {
            CampusTimeController time = bootstrap != null ? bootstrap.TimeController : null;
            if (time == null)
            {
                return false;
            }

            switch (time.CurrentSegment)
            {
                case CampusTimeSegment.DinnerBreak:
                case CampusTimeSegment.EveningStudy1:
                case CampusTimeSegment.EveningBreak1:
                case CampusTimeSegment.EveningStudy2:
                case CampusTimeSegment.EveningBreak2:
                case CampusTimeSegment.EveningStudy3:
                    return true;
                default:
                    return false;
            }
        }

        internal void EnsureDeliveryOrder(bool force)
        {
            if (!force && Time.time < nextDeliveryRefreshTime)
            {
                return;
            }

            nextDeliveryRefreshTime = Time.time + 5f;
            if (activeDeliveryOrderState == CampusDeliveryOrderState.WaitingPickup ||
                activeDeliveryOrderState == CampusDeliveryOrderState.Stolen ||
                activeDeliveryOrderState == CampusDeliveryOrderState.Searching ||
                activeDeliveryOrderState == CampusDeliveryOrderState.Reported)
            {
                return;
            }

            if (activeDeliveryOrderState == CampusDeliveryOrderState.Served &&
                Time.time < activeDeliveryResolvedUntilTime)
            {
                return;
            }

            if (activeDeliveryOrderState == CampusDeliveryOrderState.Served &&
                Time.time >= activeDeliveryResolvedUntilTime)
            {
                ClearActiveDeliveryOrder();
            }

            if (!IsDeliveryWindowNow() || !HasAnyDeliveryDropPoint())
            {
                return;
            }

            CampusCharacterRuntime owner = FindDeliveryOwnerCandidate();
            if (owner == null || owner.Data == null)
            {
                return;
            }

            activeDeliveryOwnerId = owner.CharacterId;
            activeDeliveryItemName = ResolveDeliveryItemName(owner);
            activeDeliveryOrderState = CampusDeliveryOrderState.WaitingPickup;
            activeDeliveryCreatedTime = Time.time;
            activeDeliveryStolenTime = -999f;
            activeDeliveryResolvedUntilTime = -999f;
            activeDeliveryMissingNoticed = false;
            activeDeliverySearchDecisionMade = false;
            activeDeliveryReportedByOwner = false;
            owner.Data.AddMemory(CampusCharacterMemoryId.OrderedSecretDelivery);
            WriteLog(CampusPrankTextCatalog.Format(
                CampusPrankTextId.SecretDeliveryWaiting,
                owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage),
                activeDeliveryItemName));
        }

        private void UpdateDeliveryOwnerProgress()
        {
            if (activeDeliveryOrderState == CampusDeliveryOrderState.None ||
                string.IsNullOrWhiteSpace(activeDeliveryOwnerId) ||
                rosterService == null)
            {
                return;
            }

            CampusCharacterRuntime owner = rosterService.FindRuntime(activeDeliveryOwnerId);
            if (owner == null || owner.Data == null)
            {
                return;
            }

            switch (activeDeliveryOrderState)
            {
                case CampusDeliveryOrderState.WaitingPickup:
                    if (IsRuntimeAtDeliveryPoint(owner))
                    {
                        owner.Data.AddMemory(CampusCharacterMemoryId.PickedUpDelivery);
                        activeDeliveryOrderState = CampusDeliveryOrderState.Served;
                        activeDeliveryResolvedUntilTime = Time.time + 35f;
                        WriteLog(CampusPrankTextCatalog.Format(
                            CampusPrankTextId.DeliveryPickedUp,
                            owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage),
                            activeDeliveryItemName));
                    }

                    break;
                case CampusDeliveryOrderState.Stolen:
                    if (IsRuntimeAtDeliveryPoint(owner))
                    {
                        activeDeliveryOrderState = CampusDeliveryOrderState.Searching;
                        activeDeliveryMissingNoticed = true;
                        activeDeliveryStolenTime = activeDeliveryStolenTime > 0f ? activeDeliveryStolenTime : Time.time;
                        owner.Data.AddMemory(CampusCharacterMemoryId.LostDelivery);
                        owner.Data.SetState(CampusCharacterState.Nervous);
                        AddDeliveryAlert(2);
                        WriteLog(CampusPrankTextCatalog.Format(
                            CampusPrankTextId.DeliveryMissingFound,
                            owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)));
                    }

                    break;
                case CampusDeliveryOrderState.Searching:
                    ResolveDeliveryOwnerSearch(owner);
                    break;
                case CampusDeliveryOrderState.Reported:
                    if (IsRuntimeInRoomType(owner, CampusRoomType.Office))
                    {
                        owner.Data.SetState(CampusCharacterState.Normal);
                        activeDeliveryOrderState = CampusDeliveryOrderState.Served;
                        activeDeliveryResolvedUntilTime = Time.time + 40f;
                    }

                    break;
            }
        }

        private void ResolveDeliveryOwnerSearch(CampusCharacterRuntime owner)
        {
            if (owner == null || owner.Data == null)
            {
                return;
            }

            float elapsed = activeDeliveryStolenTime > 0f ? Time.time - activeDeliveryStolenTime : 0f;
            if (!activeDeliverySearchDecisionMade && elapsed >= 8f)
            {
                activeDeliverySearchDecisionMade = true;
                if (RollDeliveryOwnerReport(owner))
                {
                    activeDeliveryOrderState = CampusDeliveryOrderState.Reported;
                    activeDeliveryReportedByOwner = true;
                    owner.Data.AddMemory(CampusCharacterMemoryId.ReportedLostDelivery);
                    AddSuspicion(12, CampusPrankTextCatalog.Get(CampusPrankTextId.DeliveryOwnerReportedMissingReason));
                    AddDeliveryAlert(5);
                    if (bootstrap != null && bootstrap.GameState != null)
                    {
                        bootstrap.GameState.AddCampusChaos(4);
                        bootstrap.GameState.AddCampusOrder(-2);
                    }

                    WriteLog(CampusPrankTextCatalog.Format(
                        CampusPrankTextId.DeliveryReportedMissing,
                        owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)));
                }
            }

            if (activeDeliverySearchDecisionMade &&
                !activeDeliveryReportedByOwner &&
                elapsed >= 20f)
            {
                owner.Data.SetState(CampusCharacterState.Normal);
                activeDeliveryOrderState = CampusDeliveryOrderState.Served;
                activeDeliveryResolvedUntilTime = Time.time + 25f;
                WriteLog(CampusPrankTextCatalog.Format(
                    CampusPrankTextId.DeliveryGaveUpSearching,
                    owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage),
                    activeDeliveryItemName));
            }
        }

        private CampusCharacterRuntime FindDeliveryOwnerCandidate()
        {
            if (rosterService == null)
            {
                return null;
            }

            List<CampusCharacterRuntime> preferred = new List<CampusCharacterRuntime>();
            List<CampusCharacterRuntime> fallback = new List<CampusCharacterRuntime>();
            CampusCharacterRuntime player = rosterService.PlayerRuntime;
            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Student))
            {
                if (runtime == null || runtime == player || runtime.Data == null)
                {
                    continue;
                }

                if (runtime.Data.HasTrait(CampusCharacterTrait.SecretDeliveryBuyer))
                {
                    preferred.Add(runtime);
                }
                else if (runtime.Data.Mischief >= 50)
                {
                    fallback.Add(runtime);
                }
            }

            List<CampusCharacterRuntime> source = preferred.Count > 0 ? preferred : fallback;
            if (source.Count == 0)
            {
                return null;
            }

            int day = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.Day : 1;
            int index = Mathf.Abs(StableHash("delivery|" + day + "|" + source.Count)) % source.Count;
            return source[index];
        }

        private string ResolveDeliveryItemName(CampusCharacterRuntime owner)
        {
            CampusPrankTextId[] names =
            {
                CampusPrankTextId.DeliveryFriedChickenRice,
                CampusPrankTextId.DeliveryMilkTea,
                CampusPrankTextId.DeliverySpicyNoodles,
                CampusPrankTextId.DeliveryBurgerSet,
                CampusPrankTextId.DeliveryOdenCup
            };
            int seed = StableHash((owner != null ? owner.CharacterId : "delivery") + "|" + (bootstrap != null ? bootstrap.GameState.Day : 1));
            return CampusPrankTextCatalog.Get(names[Mathf.Abs(seed) % names.Length]);
        }

        internal CampusCanteenClerkState ResolveCanteenClerkState(
            CampusGameplayRoom canteen,
            CampusCharacterRuntime actor)
        {
            if (canteen == null)
            {
                CampusCharacterRuntime anyClerk = FindCanteenClerk(string.Empty);
                canteen = anyClerk != null && worldService != null
                    ? worldService.FindRoomForRuntime(anyClerk)
                    : null;
            }

            if (canteen == null || canteen.RoomType != CampusRoomType.Canteen)
            {
                return CampusCanteenClerkState.Unknown;
            }

            CampusCanteenService canteenService = CampusCanteenService.Resolve(false);
            if (canteenService != null && canteenService.Stations != null)
            {
                IReadOnlyList<CampusCanteenStation> stations = canteenService.Stations;
                for (int i = 0; i < stations.Count; i++)
                {
                    CampusCanteenStation station = stations[i];
                    if (station == null ||
                        !string.Equals(station.RoomId, canteen.RoomId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (canteenService.HasFoodAtStation(station))
                    {
                        return CampusCanteenClerkState.WatchingCounter;
                    }
                }
            }

            return FindCanteenClerk(canteen.RoomId) != null
                ? CampusCanteenClerkState.WatchingCounter
                : CampusCanteenClerkState.Unknown;
        }

        internal bool RollDeliverySuspicion(CampusCharacterRuntime owner)
        {
            int alert = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.DeliveryAlertLevel : 0;
            float chance = 0.12f + alert / 100f * 0.4f;
            if (owner != null && owner.Data != null && owner.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                chance += 0.18f;
            }

            if (FindDeliveryWatcher() != null)
            {
                chance += 0.12f;
            }

            return UnityEngine.Random.value < Mathf.Clamp01(chance);
        }

        private bool RollDeliveryOwnerReport(CampusCharacterRuntime owner)
        {
            int alert = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.DeliveryAlertLevel : 0;
            float chance = 0.28f + alert / 100f * 0.35f;
            if (owner != null && owner.Data != null && owner.Data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                chance += 0.24f;
            }

            if (FindDeliveryWatcher() != null)
            {
                chance += 0.12f;
            }

            return UnityEngine.Random.value < Mathf.Clamp01(chance);
        }

        private bool IsRuntimeAtDeliveryPoint(CampusCharacterRuntime runtime)
        {
            if (runtime == null || worldService == null)
            {
                return false;
            }

            CampusGameplayRoom room = worldService.FindRoomForRuntime(runtime);
            return room != null &&
                   room.RoomType == CampusRoomType.Outdoor &&
                   HasDeliveryDropPoint(room);
        }

        private bool IsRuntimeInRoomType(CampusCharacterRuntime runtime, CampusRoomType roomType)
        {
            if (runtime == null || worldService == null)
            {
                return false;
            }

            CampusGameplayRoom room = worldService.FindRoomForRuntime(runtime);
            return room != null && room.RoomType == roomType;
        }

        private void ClearActiveDeliveryOrder()
        {
            activeDeliveryOwnerId = string.Empty;
            activeDeliveryItemName = string.Empty;
            activeDeliveryOrderState = CampusDeliveryOrderState.None;
            activeDeliveryCreatedTime = -999f;
            activeDeliveryStolenTime = -999f;
            activeDeliveryResolvedUntilTime = -999f;
            activeDeliveryMissingNoticed = false;
            activeDeliverySearchDecisionMade = false;
            activeDeliveryReportedByOwner = false;
        }

        internal CampusCharacterRuntime FindCanteenClerk(string roomId)
        {
            if (rosterService == null)
            {
                return null;
            }

            CampusCharacterRuntime fallback = null;
            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Staff))
            {
                if (runtime == null || runtime.Data == null ||
                    (runtime.Data.StaffDuty & CampusStaffDuty.CanteenClerk) == 0)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = runtime;
                }

                if (string.IsNullOrWhiteSpace(roomId))
                {
                    continue;
                }

                CampusGameplayRoom room = worldService != null ? worldService.FindRoomForRuntime(runtime) : null;
                if (room != null && string.Equals(room.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return fallback;
        }

        private CampusCharacterRuntime FindDeliveryWatcher()
        {
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Staff))
            {
                if (runtime != null &&
                    runtime.Data != null &&
                    (runtime.Data.StaffDuty & CampusStaffDuty.DeliveryWatcher) != 0)
                {
                    return runtime;
                }
            }

            return null;
        }

        private bool HasAnyDeliveryDropPoint()
        {
            if (worldService == null)
            {
                return false;
            }

            List<CampusGameplayRoom> outdoorRooms = worldService.GetRoomsByType(CampusRoomType.Outdoor, false);
            for (int i = 0; i < outdoorRooms.Count; i++)
            {
                if (HasDeliveryDropPoint(outdoorRooms[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasDeliveryDropPoint(CampusGameplayRoom room)
        {
            return room != null && room.GetFacilityCount(CampusFacilityType.DeliveryDropPoint) > 0;
        }

        internal void AddSuspicion(int amount, string reason)
        {
            if (bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            bootstrap.GameState.AddPlayerSuspicion(amount);
            WriteLog(CampusPrankTextCatalog.Format(
                CampusPrankTextId.SuspicionIncrease,
                amount,
                reason,
                bootstrap.GameState.PlayerSuspicion));
            if (bootstrap.GameState.PlayerSuspicion < SuspicionWarningThreshold)
            {
                return;
            }

            bootstrap.GameState.AddDailyWarningCount(1);
            bootstrap.GameState.AddTeacherAlertness(5);
            bootstrap.GameState.SetPlayerSuspicion(45);
            WriteLog(CampusPrankTextCatalog.Format(
                CampusPrankTextId.SuspicionThresholdReached,
                bootstrap.GameState.DailyWarningCount));
        }

        internal void AddDeliveryAlert(int amount)
        {
            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.AddDeliveryAlertLevel(amount);
            }
        }

        internal void MarkPassNoteExecuted()
        {
            dailyPassNoteCount++;
            lastPrankTime = Time.time;
            RefreshPrompt();
        }

        internal void MarkCanteenFoodTheftExecuted()
        {
            dailyCanteenTheftCount++;
            lastPrankTime = Time.time;
        }

        internal void MarkDeliveryTheftExecuted(bool detected)
        {
            activeDeliveryOrderState = detected ? CampusDeliveryOrderState.Reported : CampusDeliveryOrderState.Stolen;
            activeDeliveryStolenTime = Time.time;
            activeDeliveryMissingNoticed = detected;
            activeDeliverySearchDecisionMade = detected;
            activeDeliveryReportedByOwner = detected;
            dailyDeliveryTheftCount++;
            lastPrankTime = Time.time;
        }

        private void RefreshPrompt()
        {
            if (activeDeliveryOrderState == CampusDeliveryOrderState.WaitingPickup)
            {
                currentPrompt = CampusPrankTextCatalog.Format(
                    CampusPrankTextId.DeliveryWaitingPrompt,
                    activeDeliveryItemName,
                    activeDeliveryOwnerId);
                return;
            }

            if (activeDeliveryOrderState == CampusDeliveryOrderState.Stolen ||
                activeDeliveryOrderState == CampusDeliveryOrderState.Searching)
            {
                currentPrompt = CampusPrankTextCatalog.Format(
                    CampusPrankTextId.DeliveryOwnerLookingPrompt,
                    activeDeliveryItemName);
                return;
            }

            if (activeDeliveryOrderState == CampusDeliveryOrderState.Reported)
            {
                currentPrompt = CampusPrankTextCatalog.Get(CampusPrankTextId.DeliveryOwnerReportingPrompt);
                return;
            }

            if (FindCanteenClerk(string.Empty) != null)
            {
                currentPrompt = CampusPrankTextCatalog.Format(
                    CampusPrankTextId.CanteenClerkStatePrompt,
                    CurrentCanteenClerkState);
                return;
            }

            currentPrompt = scheduleService == null || !scheduleService.IsClassSessionNow()
                ? CampusPrankTextCatalog.Get(CampusPrankTextId.DefaultPromptNoClass)
                : CampusPrankTextCatalog.Get(CampusPrankTextId.DefaultPromptClass);
        }

        private void SyncPlacedPrankObjects(bool forceImmediate)
        {
            if (!forceImmediate && Time.time < nextWorldSyncTime)
            {
                return;
            }

            nextWorldSyncTime = Time.time + WorldSyncIntervalSeconds;
            BindPlacedPrankObjects();
            CleanupStandaloneScenePrankSpots();
            facts.Refresh(worldService);
        }

        private void BindPlacedPrankObjects()
        {
            CampusPlacedObject[] placedObjects = FindObjectsByType<CampusPlacedObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < placedObjects.Length; index++)
            {
                CampusPlacedObject placedObject = placedObjects[index];
                if (placedObject == null ||
                    !CampusPrankCatalog.TryGetByObjectId(placedObject.ObjectId, out CampusPrankDefinition definition))
                {
                    continue;
                }

                CampusPrankPlacedObject prankObject = placedObject.GetComponent<CampusPrankPlacedObject>();
                if (prankObject == null)
                {
                    prankObject = placedObject.gameObject.AddComponent<CampusPrankPlacedObject>();
                }

                prankObject.Configure(definition);
                if (string.IsNullOrWhiteSpace(placedObject.DisplayNameOverride))
                {
                    placedObject.DisplayNameOverride = definition.DisplayName;
                    placedObject.LocalizedDisplayNameOverride = definition.LocalizedDisplayName;
                }

                placedObject.ApplyInteractionState();
                RebindAnchorTargets(placedObject, prankObject, definition);
            }
        }

        private static void RebindAnchorTargets(
            CampusPlacedObject placedObject,
            CampusPrankPlacedObject prankObject,
            CampusPrankDefinition definition)
        {
            CampusInteractionAnchor[] anchors = placedObject.GetComponentsInChildren<CampusInteractionAnchor>(true);
            for (int index = 0; index < anchors.Length; index++)
            {
                CampusInteractionAnchor anchor = anchors[index];
                if (anchor == null || !CampusInteractionActionIds.Equals(anchor.ActionId, CampusInteractionActionIds.PrankExecute))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(anchor.Payload) &&
                    !string.Equals(anchor.Payload, definition.Payload, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                anchor.InteractionTarget = prankObject;
                anchor.ActionId = CampusInteractionActionIds.PrankExecute;
                anchor.Payload = definition.Payload;
                if (string.IsNullOrWhiteSpace(anchor.PromptText))
                {
                    anchor.PromptText = definition.DisplayName;
                    anchor.LocalizedPromptText = definition.LocalizedDisplayName;
                }
            }
        }

        private static void CleanupStandaloneScenePrankSpots()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            CampusPrankInteractionSpot[] spots = FindObjectsByType<CampusPrankInteractionSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < spots.Length; index++)
            {
                CampusPrankInteractionSpot spot = spots[index];
                if (spot == null ||
                    spot.GetComponentInParent<CampusPlacedObject>() != null ||
                    spot.GetComponentInParent<CampusRuntimeGameplayOverlayEntity>() != null)
                {
                    continue;
                }

                Destroy(spot.gameObject);
            }
        }

        internal CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            if (actor != null)
            {
                CampusCharacterRuntime directRuntime = actor.GetComponent<CampusCharacterRuntime>();
                if (directRuntime != null)
                {
                    return directRuntime;
                }

                CampusPlayerCharacter playerCharacter = actor.GetComponent<CampusPlayerCharacter>();
                if (playerCharacter != null && playerCharacter.IsCurrentPlayer)
                {
                    return playerCharacter.CharacterRuntime;
                }
            }

            return rosterService != null ? rosterService.PlayerRuntime : null;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }

                return hash == int.MinValue ? int.MaxValue : Mathf.Abs(hash);
            }
        }

        internal void WriteLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }
        }
    }
}
