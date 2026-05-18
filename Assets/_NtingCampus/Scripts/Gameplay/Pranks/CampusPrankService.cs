using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Inventory;
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
        PickedUp = 4,
        Searching = 5
    }

    [DisallowMultipleComponent]
    public sealed class CampusPrankService : MonoBehaviour, ICampusInteractionActionHandler
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
        [SerializeField, Min(0.1f)] private float prankCooldownSeconds = 1.25f;
        [SerializeField, Min(1)] private int basePassNoteReward = 5;
        [SerializeField, Min(1f)] private float canteenClerkStateSeconds = 6f;

        [SerializeField] private string currentPrompt = "Move into a classroom during class and press E to pass a note.";
        [SerializeField] private int dailyPassNoteCount;
        [SerializeField] private int dailyCanteenTheftCount;
        [SerializeField] private int dailyDeliveryTheftCount;
        [SerializeField] private CampusCanteenClerkState currentCanteenClerkState = CampusCanteenClerkState.Unknown;
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
        private float nextCanteenClerkStateTime = -999f;
        private float nextDeliveryRefreshTime = -999f;

        public string CurrentPrompt => currentPrompt;
        public int DailyPassNoteCount => dailyPassNoteCount;
        public int DailyCanteenTheftCount => dailyCanteenTheftCount;
        public int DailyDeliveryTheftCount => dailyDeliveryTheftCount;
        public CampusCanteenClerkState CurrentCanteenClerkState => currentCanteenClerkState;
        public string ActiveDeliveryOwnerId => activeDeliveryOwnerId;
        public string ActiveDeliveryItemName => activeDeliveryItemName;
        public CampusDeliveryOrderState ActiveDeliveryOrderState => activeDeliveryOrderState;

        public bool TryBuildDeliveryOwnerDirective(CampusCharacterRuntime runtime, out CampusCharacterTaskDirective directive)
        {
            directive = null;
            if (runtime == null ||
                string.IsNullOrWhiteSpace(activeDeliveryOwnerId) ||
                !string.Equals(runtime.CharacterId, activeDeliveryOwnerId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            switch (activeDeliveryOrderState)
            {
                case CampusDeliveryOrderState.WaitingPickup:
                    directive = BuildDeliveryPointDirective(CampusCharacterTaskType.PickupDelivery, "PickupDelivery");
                    return true;
                case CampusDeliveryOrderState.Stolen:
                case CampusDeliveryOrderState.Searching:
                    directive = BuildDeliveryPointDirective(CampusCharacterTaskType.SearchMissingDelivery, "SearchDelivery");
                    return true;
                case CampusDeliveryOrderState.Reported:
                    directive = new CampusCharacterTaskDirective
                    {
                        TaskType = CampusCharacterTaskType.ReportMissingDelivery,
                        TargetRoomType = CampusRoomType.Office,
                        PreferredFacilityType = CampusFacilityType.OfficeDesk,
                        HoldRadius = 0.2f,
                        DebugLabel = "ReportDelivery"
                    };
                    return true;
                default:
                    return false;
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
            UpdateCanteenClerkState(true);
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
            UpdateCanteenClerkState(false);
            EnsureDeliveryOrder(false);
            UpdateDeliveryOwnerProgress();
            RefreshPrompt();
        }

        private void HandleSegmentChanged(CampusTimeSegment _, CampusTimeSegment __)
        {
            EnsureDeliveryOrder(true);
            UpdateCanteenClerkState(true);
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

        public bool CanExecutePayload(string payload, GameObject actor, out string unavailableReason)
        {
            if (!CampusPrankCatalog.TryGetByPayload(payload, out CampusPrankDefinition definition))
            {
                unavailableReason = "Unknown formal prank payload.";
                return false;
            }

            if (IsPayload(payload, PassNotePayload))
            {
                return CanExecuteKnownPayload(ResolvePassNoteUnavailableReason(actor), out unavailableReason);
            }

            if (TryResolveCanteenFood(payload, out _))
            {
                return CanExecuteKnownPayload(ResolveCanteenFoodUnavailableReason(actor), out unavailableReason);
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealDelivery))
            {
                return CanExecuteKnownPayload(ResolveDeliveryUnavailableReason(actor), out unavailableReason);
            }

            unavailableReason = definition.UnsupportedReason;
            return false;
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string normalizedActionId = CampusInteractionActionIds.Normalize(actionId);
            if (!CampusInteractionActionIds.Equals(normalizedActionId, CampusInteractionActionIds.PrankExecute))
            {
                return false;
            }

            return TryExecutePayload(payload, actor);
        }

        public bool TryExecutePayload(string payload, GameObject actor)
        {
            if (!CampusPrankCatalog.TryGetByPayload(payload, out CampusPrankDefinition definition))
            {
                WriteLog("[Prank] Unknown prank payload: " + payload + ".");
                return false;
            }

            if (IsPayload(payload, PassNotePayload))
            {
                return TryExecutePassNote(actor);
            }

            if (TryResolveCanteenFood(payload, out CampusTheftItemSpec foodSpec))
            {
                return TryExecuteCanteenFoodTheft(actor, foodSpec);
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealDelivery))
            {
                return TryExecuteDeliveryTheft(actor);
            }

            WriteLog("[Prank] " + definition.DisplayName + " is not wired into the formal gameplay loop yet.");
            return false;
        }

        private bool TryExecutePassNote(GameObject actor)
        {
            string unavailableReason = ResolvePassNoteUnavailableReason(actor);
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                WriteLog("[Prank] " + unavailableReason);
                return false;
            }

            if (!PassesCooldown("Pass note"))
            {
                return false;
            }

            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            CampusGameplayRoom classroom = worldService != null && actorRuntime != null ? worldService.FindRoomForRuntime(actorRuntime) : null;

            CampusCharacterRuntime targetStudent = FindTargetStudent(actorRuntime, classroom.RoomId);
            if (targetStudent == null || targetStudent.Data == null)
            {
                WriteLog("[Prank] No nearby student is available to receive the note.");
                return false;
            }

            CampusCharacterRuntime teacherRuntime = FindTeacherInRoom(classroom.RoomId);
            gameplayEventHub?.PublishPrankAttempted(new CampusPrankAttemptedEvent(
                CampusPrankType.PassNote,
                actorRuntime.CharacterId,
                targetStudent.CharacterId,
                classroom.RoomId,
                true));

            int reward = ResolvePassNoteReward();
            bool teacherDistracted = classroomLoopService != null && classroomLoopService.IsTeacherDistractedInRoom(classroom.RoomId);
            bool detected = teacherRuntime != null && RollTeacherDetection(classroom.RoomId);
            bool succeeded = !detected;

            actorRuntime.Data.AddMemory(CampusCharacterMemoryId.PassedNoteToday);
            targetStudent.Data.AddMemory(CampusCharacterMemoryId.ReceivedNoteFromActor);
            if (teacherRuntime != null && teacherRuntime.Data != null)
            {
                teacherRuntime.Data.AddMemory(detected
                    ? CampusCharacterMemoryId.CaughtNotePassing
                    : CampusCharacterMemoryId.SawRestlessClassroom);
            }

            if (succeeded)
            {
                bootstrap.ResourceState.AddDivinePower(reward);
                bootstrap.GameState.AddCampusChaos(4);
                bootstrap.GameState.AddDivineInterest(5);
                bootstrap.GameState.AddTeacherAlertness(1);
                bootstrap.GameState.UnlockShrineRoom();
                string actorName = actorRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage);
                WriteLog(teacherDistracted
                    ? "[Prank] The teacher is distracted. " + actorName + " passed the note cleanly. Divine Power +" + reward + "."
                    : "[Prank] " + actorName + " passed the note cleanly. Divine Power +" + reward + ".");
            }
            else
            {
                reward = 0;
                bootstrap.GameState.AddCampusChaos(6);
                bootstrap.GameState.AddTeacherAlertness(6);
                bootstrap.GameState.AddDivineInterest(2);
                WriteLog("[Prank] The teacher noticed the note passing.");
            }

            gameplayEventHub?.PublishPrankResolved(new CampusPrankResolvedEvent(
                CampusPrankType.PassNote,
                actorRuntime.CharacterId,
                targetStudent.CharacterId,
                classroom.RoomId,
                succeeded,
                detected,
                reward));

            dailyPassNoteCount++;
            lastPrankTime = Time.time;
            RefreshPrompt();
            return true;
        }

        private bool TryExecuteCanteenFoodTheft(GameObject actor, CampusTheftItemSpec foodSpec)
        {
            string unavailableReason = ResolveCanteenFoodUnavailableReason(actor);
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                WriteLog("[Canteen] " + unavailableReason);
                return false;
            }

            if (!PassesCooldown(foodSpec.DisplayName))
            {
                return false;
            }

            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            CampusGameplayRoom canteen = worldService.FindRoomForRuntime(actorRuntime);
            CampusCharacterRuntime clerk = FindCanteenClerk(canteen.RoomId);
            bool clerkDistracted = IsCanteenClerkDistracted(currentCanteenClerkState);
            bool detected = clerk != null && RollCanteenDetection(clerkDistracted);

            gameplayEventHub?.PublishPrankAttempted(new CampusPrankAttemptedEvent(
                CampusPrankType.CanteenFoodTheft,
                actorRuntime.CharacterId,
                clerk != null ? clerk.CharacterId : string.Empty,
                canteen.RoomId,
                scheduleService != null && scheduleService.IsClassSessionNow()));

            if (detected)
            {
                actorRuntime.Data.SetState(CampusCharacterState.Nervous);
                actorRuntime.Data.AddMemory(CampusCharacterMemoryId.CanteenTheftSuspected);
                if (clerk != null && clerk.Data != null)
                {
                    clerk.Data.SetState(CampusCharacterState.Nervous);
                }

                AddSuspicion(foodSpec.SuspicionRisk + 18, "canteen clerk saw the attempt");
                AddCanteenAlert(7);
                bootstrap.GameState.AddCampusChaos(3);
                WriteLog("[Canteen] The clerk turns back too early. " + FormatActorName(actorRuntime) + " does not get the " + foodSpec.DisplayName + ".");
                PublishCanteenResolved(actorRuntime, clerk, canteen, false, foodSpec);
                lastPrankTime = Time.time;
                return false;
            }

            if (!TryGiveStolenItem(actor, foodSpec, out string itemError))
            {
                WriteLog("[Canteen] " + itemError);
                return false;
            }

            actorRuntime.Data.AddMemory(CampusCharacterMemoryId.StoleCanteenFood);
            dailyCanteenTheftCount++;
            AddSuspicion(foodSpec.SuspicionRisk, "stolen canteen food");
            AddCanteenAlert(3);
            bootstrap.GameState.AddCampusChaos(6);
            bootstrap.GameState.AddCampusOrder(-2);
            bootstrap.GameState.AddDivineInterest(4);
            WriteLog("[Canteen] Clerk state=" + currentCanteenClerkState + ". " + FormatActorName(actorRuntime) + " stole " + foodSpec.DisplayName + ".");
            PublishCanteenResolved(actorRuntime, clerk, canteen, true, foodSpec);
            lastPrankTime = Time.time;
            return true;
        }

        private bool TryExecuteDeliveryTheft(GameObject actor)
        {
            EnsureDeliveryOrder(true);
            string unavailableReason = ResolveDeliveryUnavailableReason(actor);
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                WriteLog("[Delivery] " + unavailableReason);
                return false;
            }

            if (!PassesCooldown("delivery"))
            {
                return false;
            }

            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            CampusGameplayRoom outdoorRoom = worldService.FindRoomForRuntime(actorRuntime);
            CampusCharacterRuntime owner = rosterService.FindRuntime(activeDeliveryOwnerId);
            CampusTheftItemSpec deliverySpec = CampusTheftItemSpec.CreateDelivery(activeDeliveryItemName, activeDeliveryOwnerId);
            bool detected = RollDeliverySuspicion(owner);

            if (!TryGiveStolenItem(actor, deliverySpec, out string itemError))
            {
                WriteLog("[Delivery] " + itemError);
                return false;
            }

            actorRuntime.Data.AddMemory(CampusCharacterMemoryId.DeliveryStolen);
            if (owner != null && owner.Data != null)
            {
                if (detected)
                {
                    owner.Data.AddMemory(CampusCharacterMemoryId.LostDelivery);
                    owner.Data.AddMemory(CampusCharacterMemoryId.ReportedLostDelivery);
                    owner.Data.SetState(CampusCharacterState.Nervous);
                }
            }

            activeDeliveryOrderState = detected ? CampusDeliveryOrderState.Reported : CampusDeliveryOrderState.Stolen;
            activeDeliveryStolenTime = Time.time;
            activeDeliveryMissingNoticed = detected;
            activeDeliverySearchDecisionMade = detected;
            activeDeliveryReportedByOwner = detected;
            dailyDeliveryTheftCount++;
            AddSuspicion(deliverySpec.SuspicionRisk + (detected ? 16 : 5), detected ? "delivery owner reports the loss" : "stolen delivery");
            AddDeliveryAlert(detected ? 8 : 4);
            bootstrap.GameState.AddCampusChaos(detected ? 8 : 5);
            bootstrap.GameState.AddCampusOrder(detected ? -4 : -2);
            bootstrap.GameState.AddDivineInterest(5);

            gameplayEventHub?.PublishPrankAttempted(new CampusPrankAttemptedEvent(
                CampusPrankType.DeliveryTheft,
                actorRuntime.CharacterId,
                owner != null ? owner.CharacterId : activeDeliveryOwnerId,
                outdoorRoom != null ? outdoorRoom.RoomId : string.Empty,
                scheduleService != null && scheduleService.IsClassSessionNow()));
            gameplayEventHub?.PublishPrankResolved(new CampusPrankResolvedEvent(
                CampusPrankType.DeliveryTheft,
                actorRuntime.CharacterId,
                owner != null ? owner.CharacterId : activeDeliveryOwnerId,
                outdoorRoom != null ? outdoorRoom.RoomId : string.Empty,
                true,
                false,
                0));

            WriteLog(detected
                ? "[Delivery] " + FormatActorName(actorRuntime) + " took " + activeDeliveryItemName + ". The owner is likely to report it."
                : "[Delivery] " + FormatActorName(actorRuntime) + " took " + activeDeliveryItemName + " before the owner arrived.");
            lastPrankTime = Time.time;
            return true;
        }

        private string ResolvePassNoteUnavailableReason(GameObject actor)
        {
            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return "No formal actor runtime is available.";
            }

            if (scheduleService == null || !scheduleService.IsClassSessionNow())
            {
                return "Passing notes only counts during class sessions.";
            }

            CampusGameplayRoom classroom = worldService != null ? worldService.FindRoomForRuntime(actorRuntime) : null;
            if (classroom == null || classroom.RoomType != CampusRoomType.Classroom)
            {
                return "Actor needs to be inside a formal classroom.";
            }

            return string.Empty;
        }

        private string ResolveCanteenFoodUnavailableReason(GameObject actor)
        {
            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return "No formal actor runtime is available.";
            }

            if (!IsEveningStudyOrNightErrand())
            {
                return "Canteen theft is tuned for evening study or night errand windows.";
            }

            CampusGameplayRoom canteen = worldService != null ? worldService.FindRoomForRuntime(actorRuntime) : null;
            if (canteen == null || canteen.RoomType != CampusRoomType.Canteen)
            {
                return "Actor needs to be in a declared Canteen room.";
            }

            if (FindCanteenClerk(canteen.RoomId) == null)
            {
                return "No Staff actor with CanteenClerk duty is present for this canteen.";
            }

            return string.Empty;
        }

        private string ResolveDeliveryUnavailableReason(GameObject actor)
        {
            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return "No formal actor runtime is available.";
            }

            EnsureDeliveryOrder(false);
            if (activeDeliveryOrderState != CampusDeliveryOrderState.WaitingPickup ||
                string.IsNullOrWhiteSpace(activeDeliveryOwnerId))
            {
                return "No student delivery order is waiting at a declared outdoor delivery point.";
            }

            CampusGameplayRoom room = worldService != null ? worldService.FindRoomForRuntime(actorRuntime) : null;
            if (room == null || room.RoomType != CampusRoomType.Outdoor)
            {
                return "Actor needs to be in a declared Outdoor delivery area.";
            }

            if (!HasDeliveryDropPoint(room))
            {
                return "This outdoor area needs a DeliveryDropPoint facility.";
            }

            return string.Empty;
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

        private void EnsureDeliveryOrder(bool force)
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

            if (activeDeliveryOrderState == CampusDeliveryOrderState.PickedUp &&
                Time.time < activeDeliveryResolvedUntilTime)
            {
                return;
            }

            if (activeDeliveryOrderState == CampusDeliveryOrderState.PickedUp &&
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
            WriteLog("[Delivery] " + owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage) + " has a secret delivery waiting: " + activeDeliveryItemName + ".");
        }

        private static CampusCharacterTaskDirective BuildDeliveryPointDirective(CampusCharacterTaskType taskType, string debugLabel)
        {
            return new CampusCharacterTaskDirective
            {
                TaskType = taskType,
                TargetRoomType = CampusRoomType.Outdoor,
                PreferredFacilityType = CampusFacilityType.DeliveryDropPoint,
                HoldRadius = 0.18f,
                DebugLabel = debugLabel
            };
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
                        activeDeliveryOrderState = CampusDeliveryOrderState.PickedUp;
                        activeDeliveryResolvedUntilTime = Time.time + 35f;
                        WriteLog("[Delivery] " + owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage) + " picked up " + activeDeliveryItemName + ".");
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
                        WriteLog("[Delivery] " + owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage) + " reached the pickup point and found the delivery missing.");
                    }

                    break;
                case CampusDeliveryOrderState.Searching:
                    ResolveDeliveryOwnerSearch(owner);
                    break;
                case CampusDeliveryOrderState.Reported:
                    if (IsRuntimeInRoomType(owner, CampusRoomType.Office))
                    {
                        owner.Data.SetState(CampusCharacterState.Normal);
                        activeDeliveryOrderState = CampusDeliveryOrderState.PickedUp;
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
                    AddSuspicion(12, "delivery owner reported a missing delivery");
                    AddDeliveryAlert(5);
                    if (bootstrap != null && bootstrap.GameState != null)
                    {
                        bootstrap.GameState.AddCampusChaos(4);
                        bootstrap.GameState.AddCampusOrder(-2);
                    }

                    WriteLog("[Delivery] " + owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage) + " reported the missing delivery.");
                }
            }

            if (activeDeliverySearchDecisionMade &&
                !activeDeliveryReportedByOwner &&
                elapsed >= 20f)
            {
                owner.Data.SetState(CampusCharacterState.Normal);
                activeDeliveryOrderState = CampusDeliveryOrderState.PickedUp;
                activeDeliveryResolvedUntilTime = Time.time + 25f;
                WriteLog("[Delivery] " + owner.Data.GetDisplayName(CampusLanguageState.CurrentLanguage) + " gave up searching for " + activeDeliveryItemName + ".");
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
            string[] names = { "fried chicken rice", "milk tea", "spicy noodles", "burger set", "oden cup" };
            int seed = StableHash((owner != null ? owner.CharacterId : "delivery") + "|" + (bootstrap != null ? bootstrap.GameState.Day : 1));
            return names[Mathf.Abs(seed) % names.Length];
        }

        private void UpdateCanteenClerkState(bool force)
        {
            if (!force && Time.time < nextCanteenClerkStateTime)
            {
                return;
            }

            nextCanteenClerkStateTime = Time.time + Mathf.Max(1f, canteenClerkStateSeconds);
            if (FindCanteenClerk(string.Empty) == null)
            {
                currentCanteenClerkState = CampusCanteenClerkState.Unknown;
                return;
            }

            CampusCanteenClerkState[] cycle =
            {
                CampusCanteenClerkState.WatchingCounter,
                CampusCanteenClerkState.PreparingMalatang,
                CampusCanteenClerkState.CookingNoodles,
                CampusCanteenClerkState.FetchingIngredients,
                CampusCanteenClerkState.BrieflyAway
            };
            int step = Mathf.Abs(Mathf.FloorToInt(Time.time / Mathf.Max(1f, canteenClerkStateSeconds)));
            currentCanteenClerkState = cycle[step % cycle.Length];
        }

        private bool IsCanteenClerkDistracted(CampusCanteenClerkState state)
        {
            return state == CampusCanteenClerkState.PreparingMalatang ||
                   state == CampusCanteenClerkState.CookingNoodles ||
                   state == CampusCanteenClerkState.FetchingIngredients ||
                   state == CampusCanteenClerkState.BrieflyAway;
        }

        private bool RollCanteenDetection(bool clerkDistracted)
        {
            int alert = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.CanteenAlertLevel : 0;
            float chance = currentCanteenClerkState == CampusCanteenClerkState.WatchingCounter ? 0.72f : 0.18f;
            if (currentCanteenClerkState == CampusCanteenClerkState.BrieflyAway)
            {
                chance = 0.06f;
            }

            if (clerkDistracted)
            {
                chance *= 0.65f;
            }

            chance += alert / 100f * 0.35f;
            return UnityEngine.Random.value < Mathf.Clamp01(chance);
        }

        private bool RollDeliverySuspicion(CampusCharacterRuntime owner)
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

        private CampusCharacterRuntime FindCanteenClerk(string roomId)
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

        private static bool HasDeliveryDropPoint(CampusGameplayRoom room)
        {
            return room != null && room.GetFacilityCount(CampusFacilityType.DeliveryDropPoint) > 0;
        }

        private void PublishCanteenResolved(
            CampusCharacterRuntime actorRuntime,
            CampusCharacterRuntime clerk,
            CampusGameplayRoom canteen,
            bool succeeded,
            CampusTheftItemSpec foodSpec)
        {
            gameplayEventHub?.PublishPrankResolved(new CampusPrankResolvedEvent(
                CampusPrankType.CanteenFoodTheft,
                actorRuntime != null ? actorRuntime.CharacterId : string.Empty,
                clerk != null ? clerk.CharacterId : string.Empty,
                canteen != null ? canteen.RoomId : string.Empty,
                succeeded,
                false,
                succeeded ? foodSpec.DivinePowerReward : 0));
        }

        private bool TryGiveStolenItem(GameObject actor, CampusTheftItemSpec spec, out string errorMessage)
        {
            errorMessage = string.Empty;
            StorageMemory memory = StorageMemory.GetOrCreate();
            StorageItemRegistry registry = StoragePlayerInventoryUtility.EnsureRegistry(memory);
            EnsureRuntimeItemDefinition(registry, spec);

            StorageItemModel item = registry.CreateItem(spec.DefinitionId, spec.DefinitionId + "_" + Guid.NewGuid().ToString("N"));
            if (item == null)
            {
                errorMessage = "Failed to create stolen item definition: " + spec.DefinitionId + ".";
                return false;
            }

            item.DisplayName = spec.DisplayName;
            item.Description = "Stolen from " + spec.SourceLocation + ". Smell=" + spec.SmellLevel + ", suspicion risk=" + spec.SuspicionRisk + ".";
            item.IsUsable = true;
            item.UseActionId = StorageItemUseUtility.ConsumeFoodActionId;
            item.ConsumeOnUse = true;
            item.UseText = "Ate " + spec.DisplayName + ".";
            item.OwnerId = ResolveTheftOwnerId(spec);
            item.SourceLocation = spec.SourceLocation;
            item.SuspicionRisk = spec.SuspicionRisk;
            item.AllowTaking = false;

            StorageTransferContext context = StorageTransferContext.ForActor(actor, StorageTransferReason.PrankTheft);
            context.ForceIllegal = true;
            context.SuppressNpcDetection = true;
            context.SuppressSuspicion = true;
            context.SourceLocation = spec.SourceLocation;
            context.OwnerId = item.OwnerId;
            context.SuspicionRiskOverride = spec.SuspicionRisk;
            CampusInventoryTransferService service = CampusInventoryTransferService.Resolve();
            if (service.TryPlaceInCarriedStorage(memory, item, context, out StorageTransferResult result))
            {
                return true;
            }

            errorMessage = result.Message;
            return false;
        }

        private static string ResolveTheftOwnerId(CampusTheftItemSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(spec.SourceLocation) &&
                spec.SourceLocation.StartsWith("delivery:", StringComparison.OrdinalIgnoreCase))
            {
                return spec.SourceLocation.Substring("delivery:".Length).Trim();
            }

            return string.IsNullOrWhiteSpace(spec.SourceLocation) ? "campus" : spec.SourceLocation.Trim();
        }

        private static void EnsureRuntimeItemDefinition(StorageItemRegistry registry, CampusTheftItemSpec spec)
        {
            if (registry == null || registry.TryGetDefinition(spec.DefinitionId, out _))
            {
                return;
            }

            StorageItemDefinition definition = ScriptableObject.CreateInstance<StorageItemDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Id = spec.DefinitionId;
            definition.DisplayName = spec.DisplayName;
            definition.Width = spec.Width;
            definition.Height = spec.Height;
            definition.Weight = spec.Weight;
            definition.Description = "Runtime stolen item. Source=" + spec.SourceLocation + ".";
            definition.ThemeColor = spec.ThemeColor;
            definition.IsUsable = true;
            definition.UseActionId = StorageItemUseUtility.ConsumeFoodActionId;
            definition.ConsumeOnUse = true;
            definition.UseText = "Ate " + spec.DisplayName + ".";
            registry.RegisterRuntimeDefinition(definition);
        }

        private void AddSuspicion(int amount, string reason)
        {
            if (bootstrap == null || bootstrap.GameState == null)
            {
                return;
            }

            bootstrap.GameState.AddPlayerSuspicion(amount);
            WriteLog("[Suspicion] +" + amount + " (" + reason + "). Total=" + bootstrap.GameState.PlayerSuspicion + ".");
            if (bootstrap.GameState.PlayerSuspicion < SuspicionWarningThreshold)
            {
                return;
            }

            bootstrap.GameState.AddDailyWarningCount(1);
            bootstrap.GameState.AddTeacherAlertness(5);
            bootstrap.GameState.SetPlayerSuspicion(45);
            WriteLog("[Suspicion] Threshold reached. A warning/check-bag consequence is queued. Daily warnings=" + bootstrap.GameState.DailyWarningCount + ".");
        }

        private void AddCanteenAlert(int amount)
        {
            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.AddCanteenAlertLevel(amount);
            }
        }

        private void AddDeliveryAlert(int amount)
        {
            if (bootstrap != null && bootstrap.GameState != null)
            {
                bootstrap.GameState.AddDeliveryAlertLevel(amount);
            }
        }

        private bool PassesCooldown(string actionName)
        {
            if (IsPrankCooldownReady())
            {
                return true;
            }

            WriteLog("[Prank] " + actionName + " is still cooling down.");
            return false;
        }

        private bool CanExecuteKnownPayload(string resolvedUnavailableReason, out string unavailableReason)
        {
            unavailableReason = resolvedUnavailableReason;
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                return false;
            }

            if (!IsPrankCooldownReady())
            {
                unavailableReason = "Prank action is cooling down.";
                return false;
            }

            return true;
        }

        private bool IsPrankCooldownReady()
        {
            return Time.time - lastPrankTime >= prankCooldownSeconds;
        }

        private CampusCharacterRuntime FindTargetStudent(CampusCharacterRuntime actorRuntime, string roomId)
        {
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Student))
            {
                if (runtime == null || runtime == actorRuntime || runtime.Data == null)
                {
                    continue;
                }

                if (string.Equals(runtime.Data.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private CampusCharacterRuntime FindTeacherInRoom(string roomId)
        {
            if (rosterService == null)
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Teacher))
            {
                if (runtime != null &&
                    runtime.Data != null &&
                    string.Equals(runtime.Data.CurrentRoomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private int ResolvePassNoteReward()
        {
            switch (dailyPassNoteCount)
            {
                case 0:
                    return basePassNoteReward;
                case 1:
                    return Mathf.Max(1, Mathf.RoundToInt(basePassNoteReward * 0.7f));
                default:
                    return Mathf.Max(1, Mathf.RoundToInt(basePassNoteReward * 0.4f));
            }
        }

        private bool RollTeacherDetection(string roomId)
        {
            int alertness = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.TeacherAlertness : 0;
            float detectionChance = Mathf.Clamp01(0.15f + alertness / 100f * 0.55f);
            if (classroomLoopService != null && classroomLoopService.IsTeacherDistractedInRoom(roomId))
            {
                detectionChance *= 0.35f;
            }

            return UnityEngine.Random.value < detectionChance;
        }

        private void RefreshPrompt()
        {
            if (activeDeliveryOrderState == CampusDeliveryOrderState.WaitingPickup)
            {
                currentPrompt = "Delivery waiting: " + activeDeliveryItemName + " for " + activeDeliveryOwnerId + ".";
                return;
            }

            if (activeDeliveryOrderState == CampusDeliveryOrderState.Stolen ||
                activeDeliveryOrderState == CampusDeliveryOrderState.Searching)
            {
                currentPrompt = "Delivery owner is looking for: " + activeDeliveryItemName + ".";
                return;
            }

            if (activeDeliveryOrderState == CampusDeliveryOrderState.Reported)
            {
                currentPrompt = "Delivery owner is reporting a missing order.";
                return;
            }

            if (FindCanteenClerk(string.Empty) != null)
            {
                currentPrompt = "Canteen clerk state: " + currentCanteenClerkState + ".";
                return;
            }

            currentPrompt = scheduleService == null || !scheduleService.IsClassSessionNow()
                ? "Place declared prank spots for canteen food or delivery theft."
                : "Pass Note available during class, or sneak out to declared canteen/delivery spots.";
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
                if (string.IsNullOrWhiteSpace(anchor.PromptText) || anchor.PromptText == "浜や簰")
                {
                    anchor.PromptText = definition.DisplayName;
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

        private CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            if (actor != null)
            {
                CampusCharacterRuntime directRuntime = actor.GetComponent<CampusCharacterRuntime>();
                if (directRuntime != null)
                {
                    return directRuntime;
                }

                CampusPlayerCharacter playerCharacter = actor.GetComponent<CampusPlayerCharacter>();
                if (playerCharacter != null && playerCharacter.CharacterRuntime != null)
                {
                    return playerCharacter.CharacterRuntime;
                }
            }

            return rosterService != null ? rosterService.PlayerRuntime : null;
        }

        private static string FormatActorName(CampusCharacterRuntime actorRuntime)
        {
            if (actorRuntime == null || actorRuntime.Data == null)
            {
                return "Actor";
            }

            return actorRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage);
        }

        private static bool IsPayload(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveCanteenFood(string payload, out CampusTheftItemSpec spec)
        {
            if (IsPayload(payload, CampusPrankPayloadIds.StealFriedChicken))
            {
                spec = new CampusTheftItemSpec(
                    "stolen_fried_chicken",
                    "stolen fried chicken",
                    "canteen",
                    2,
                    2,
                    0.45f,
                    28,
                    35,
                    2,
                    new Color(0.78f, 0.46f, 0.24f, 1f));
                return true;
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealBurger))
            {
                spec = new CampusTheftItemSpec(
                    "stolen_burger",
                    "stolen burger",
                    "canteen",
                    2,
                    2,
                    0.38f,
                    22,
                    26,
                    2,
                    new Color(0.67f, 0.52f, 0.28f, 1f));
                return true;
            }

            if (IsPayload(payload, CampusPrankPayloadIds.StealOden))
            {
                spec = new CampusTheftItemSpec(
                    "stolen_oden",
                    "stolen oden cup",
                    "canteen",
                    2,
                    2,
                    0.5f,
                    30,
                    42,
                    2,
                    new Color(0.66f, 0.62f, 0.42f, 1f));
                return true;
            }

            spec = default;
            return false;
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

        private void WriteLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }
        }

        private readonly struct CampusTheftItemSpec
        {
            public CampusTheftItemSpec(
                string definitionId,
                string displayName,
                string sourceLocation,
                int width,
                int height,
                float weight,
                int suspicionRisk,
                int smellLevel,
                int divinePowerReward,
                Color themeColor)
            {
                DefinitionId = definitionId;
                DisplayName = displayName;
                SourceLocation = sourceLocation;
                Width = Mathf.Max(1, width);
                Height = Mathf.Max(1, height);
                Weight = Mathf.Max(0f, weight);
                SuspicionRisk = Mathf.Max(0, suspicionRisk);
                SmellLevel = Mathf.Max(0, smellLevel);
                DivinePowerReward = Mathf.Max(0, divinePowerReward);
                ThemeColor = themeColor;
            }

            public string DefinitionId { get; }
            public string DisplayName { get; }
            public string SourceLocation { get; }
            public int Width { get; }
            public int Height { get; }
            public float Weight { get; }
            public int SuspicionRisk { get; }
            public int SmellLevel { get; }
            public int DivinePowerReward { get; }
            public Color ThemeColor { get; }

            public static CampusTheftItemSpec CreateDelivery(string itemName, string ownerId)
            {
                string normalizedItemName = string.IsNullOrWhiteSpace(itemName) ? "delivery" : itemName.Trim();
                return new CampusTheftItemSpec(
                    "stolen_delivery_" + StableId(normalizedItemName),
                    "stolen " + normalizedItemName,
                    "delivery:" + ownerId,
                    2,
                    2,
                    0.6f,
                    24,
                    normalizedItemName.Contains("chicken", StringComparison.OrdinalIgnoreCase) ? 38 : 18,
                    2,
                    new Color(0.54f, 0.42f, 0.30f, 1f));
            }

            private static string StableId(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return "item";
                }

                char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (!char.IsLetterOrDigit(chars[i]))
                    {
                        chars[i] = '_';
                    }
                }

                return new string(chars).Trim('_');
            }
        }
    }
}
