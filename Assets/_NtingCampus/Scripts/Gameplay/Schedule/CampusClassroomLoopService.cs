using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Schedule
{
    [DisallowMultipleComponent]
    public sealed class CampusClassroomLoopService : MonoBehaviour
    {
        private const float ThinkIntervalSeconds = 0.65f;

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;
        [SerializeField] private CampusScheduleService scheduleService;
        [SerializeField] private CampusGameplayEventHub gameplayEventHub;
        [SerializeField, Min(1f)] private float minDistractionSeconds = 7f;
        [SerializeField, Min(1f)] private float maxDistractionSeconds = 12f;
        [SerializeField, Range(0f, 1f)] private float baseSkipDetectionChance = 0.42f;
        [SerializeField, Range(0f, 1f)] private float distractedSkipDetectionMultiplier = 0.35f;
        [SerializeField, Range(0f, 1f)] private float noTeacherSkipDetectionMultiplier = 0.5f;

        [SerializeField] private string currentPrompt = string.Empty;
        [SerializeField] private string activeClassroomId = string.Empty;
        [SerializeField] private string distractedTeacherId = string.Empty;
        [SerializeField] private string distractionSourceStudentId = string.Empty;
        [SerializeField] private string expectedControlledActorClassroomId = string.Empty;
        [SerializeField] private float distractedUntilTime = -1f;
        [SerializeField] private int dailyDozeEventCount;
        [SerializeField] private int dailySneakOutCount;
        [SerializeField] private int dailyCaughtSkippingCount;
        [SerializeField] private bool controlledActorLeaveHandledThisSegment;

        private readonly HashSet<string> dozedStudentIdsThisSegment =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private CampusClassroomFacts classroomFacts;
        private CampusClassroomActions classroomActions;
        private CampusScheduleService factsScheduleService;
        private CampusWorldService factsWorldService;
        private CampusRosterService factsRosterService;
        private float nextThinkTime = -999f;

        public string CurrentPrompt => currentPrompt;
        public string ActiveClassroomId => HasActiveDistraction ? activeClassroomId : string.Empty;
        public string DistractedTeacherId => HasActiveDistraction ? distractedTeacherId : string.Empty;
        public string DistractionSourceStudentId => HasActiveDistraction ? distractionSourceStudentId : string.Empty;
        public int DailyDozeEventCount => dailyDozeEventCount;
        public int DailySneakOutCount => dailySneakOutCount;
        public int DailyCaughtSkippingCount => dailyCaughtSkippingCount;
        public bool HasActiveDistraction => Time.time < distractedUntilTime && !string.IsNullOrWhiteSpace(activeClassroomId);
        public float DistractionRemainingSeconds => HasActiveDistraction ? Mathf.Max(0f, distractedUntilTime - Time.time) : 0f;

        internal CampusGameBootstrap Bootstrap => bootstrap;
        internal CampusGameplayEventHub GameplayEventHub => gameplayEventHub;
        internal CampusClassroomFacts Facts
        {
            get
            {
                EnsureDomainOwners();
                return classroomFacts;
            }
        }

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            timeController = bootstrap != null ? bootstrap.TimeController : null;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;
            EnsureDomainOwners();

            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.SegmentChanged += HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
                timeController.DailySettlementStarted += HandleDailySettlementStarted;
            }

            ResetSegmentState();
            RefreshPrompt();
        }

        public bool IsTeacherDistractedInRoom(string roomId)
        {
            EnsureDomainOwners();
            return classroomFacts != null &&
                   classroomFacts.IsTeacherDistractedInRoom(
                       activeClassroomId,
                       distractedUntilTime,
                       roomId,
                       Time.time);
        }

        public bool TryForceSleepyDistraction()
        {
            ResolveReferences();
            EnsureDomainOwners();
            return classroomActions != null &&
                   classroomActions.TryStudentDozeOff(rosterService != null ? rosterService.PlayerRuntime : null, true);
        }

        internal bool CanStudentDozeOff(CampusCharacterRuntime student, bool force)
        {
            ResolveReferences();
            EnsureDomainOwners();
            return classroomActions != null && classroomActions.CanStudentDozeOff(student, force);
        }

        internal bool TryStudentDozeOff(CampusCharacterRuntime student, bool force)
        {
            ResolveReferences();
            EnsureDomainOwners();
            return classroomActions != null && classroomActions.TryStudentDozeOff(student, force);
        }

        internal bool HasStudentDozedThisSegment(string studentId)
        {
            return !string.IsNullOrWhiteSpace(studentId) && dozedStudentIdsThisSegment.Contains(studentId);
        }

        internal void MarkStudentDozedThisSegment(string studentId)
        {
            if (!string.IsNullOrWhiteSpace(studentId))
            {
                dozedStudentIdsThisSegment.Add(studentId);
            }
        }

        internal float BeginTeacherDistraction(
            CampusGameplayRoom classroom,
            CampusCharacterRuntime sourceStudent,
            CampusCharacterRuntime teacher)
        {
            activeClassroomId = classroom != null ? classroom.RoomId : string.Empty;
            distractionSourceStudentId = sourceStudent != null ? sourceStudent.CharacterId : string.Empty;
            distractedTeacherId = teacher != null ? teacher.CharacterId : string.Empty;
            distractedUntilTime = Time.time + UnityEngine.Random.Range(
                Mathf.Min(minDistractionSeconds, maxDistractionSeconds),
                Mathf.Max(minDistractionSeconds, maxDistractionSeconds));
            dailyDozeEventCount++;
            return DistractionRemainingSeconds;
        }

        internal void WriteClassroomLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }
        }

        private void OnDestroy()
        {
            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || Time.time < nextThinkTime)
            {
                return;
            }

            nextThinkTime = Time.time + ThinkIntervalSeconds;
            ResolveReferences();
            EnsureDomainOwners();
            ExpireDistractionIfNeeded();

            if (classroomFacts == null || !classroomFacts.IsClassSessionNow())
            {
                ClearDistraction();
                RefreshPrompt();
                return;
            }

            DetectControlledActorLeavingClassroom();
            RefreshPrompt();
        }

        private void HandleSegmentChanged(CampusTimeSegment previousSegment, CampusTimeSegment currentSegment)
        {
            ResetSegmentState();
            RefreshPrompt();
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            dailyDozeEventCount = 0;
            dailySneakOutCount = 0;
            dailyCaughtSkippingCount = 0;
        }

        private void ResolveReferences()
        {
            if (bootstrap == null)
            {
                bootstrap = CampusGameBootstrap.Instance;
            }

            if (timeController == null && bootstrap != null)
            {
                timeController = bootstrap.TimeController;
            }

            if (worldService == null && bootstrap != null)
            {
                worldService = bootstrap.WorldService;
            }

            if (rosterService == null && bootstrap != null)
            {
                rosterService = bootstrap.RosterService;
            }

            if (scheduleService == null && bootstrap != null)
            {
                scheduleService = bootstrap.ScheduleService;
            }

            if (gameplayEventHub == null && bootstrap != null)
            {
                gameplayEventHub = bootstrap.GameplayEventHub;
            }
        }

        private void EnsureDomainOwners()
        {
            if (classroomFacts != null &&
                factsScheduleService == scheduleService &&
                factsWorldService == worldService &&
                factsRosterService == rosterService)
            {
                return;
            }

            factsScheduleService = scheduleService;
            factsWorldService = worldService;
            factsRosterService = rosterService;
            classroomFacts = new CampusClassroomFacts(scheduleService, worldService, rosterService);
            classroomActions = new CampusClassroomActions(this, classroomFacts);
        }

        private void ResetSegmentState()
        {
            EnsureDomainOwners();
            dozedStudentIdsThisSegment.Clear();
            controlledActorLeaveHandledThisSegment = false;
            expectedControlledActorClassroomId = classroomFacts != null
                ? classroomFacts.ResolveExpectedControlledActorClassroomId()
                : string.Empty;
            ClearDistraction();
        }

        private void ExpireDistractionIfNeeded()
        {
            if (!string.IsNullOrWhiteSpace(activeClassroomId) && Time.time >= distractedUntilTime)
            {
                ClearDistraction();
            }
        }

        private void ClearDistraction()
        {
            activeClassroomId = string.Empty;
            distractedTeacherId = string.Empty;
            distractionSourceStudentId = string.Empty;
            distractedUntilTime = -1f;
        }

        private void DetectControlledActorLeavingClassroom()
        {
            if (controlledActorLeaveHandledThisSegment || rosterService == null || rosterService.PlayerRuntime == null)
            {
                return;
            }

            CampusCharacterRuntime controlledActor = rosterService.PlayerRuntime;
            CampusGameplayRoom currentRoom = classroomFacts.ResolveRuntimeRoom(controlledActor);
            if (string.IsNullOrWhiteSpace(expectedControlledActorClassroomId))
            {
                expectedControlledActorClassroomId = classroomFacts.ResolveExpectedControlledActorClassroomId();
            }

            if (string.IsNullOrWhiteSpace(expectedControlledActorClassroomId))
            {
                return;
            }

            if (currentRoom != null &&
                string.Equals(currentRoom.RoomId, expectedControlledActorClassroomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            controlledActorLeaveHandledThisSegment = true;
            dailySneakOutCount++;

            string toRoomId = currentRoom != null ? currentRoom.RoomId : string.Empty;
            bool usedDistraction = IsTeacherDistractedInRoom(expectedControlledActorClassroomId);
            CampusCharacterRuntime teacher = classroomFacts.FindTeacherForRoom(expectedControlledActorClassroomId);
            bool detected = RollSkipDetection(usedDistraction, teacher != null);
            if (detected)
            {
                dailyCaughtSkippingCount++;
                controlledActor.Data.AddMemory(CampusCharacterMemoryId.CaughtSkippingClass);
                WriteClassroomLog(CampusClassroomTextCatalog.Format(
                    CampusClassroomTextId.ActorCaughtLeavingLog,
                    controlledActor.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)));
            }
            else
            {
                controlledActor.Data.AddMemory(CampusCharacterMemoryId.SneakedOutDuringClass);
                WriteClassroomLog(CampusClassroomTextCatalog.Format(
                    usedDistraction
                        ? CampusClassroomTextId.ActorLeftDuringDistractionLog
                        : CampusClassroomTextId.ActorSneakedOutLog,
                    controlledActor.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)));
            }

            gameplayEventHub?.PublishActorSkipClass(new CampusActorSkipClassEvent(
                controlledActor.CharacterId,
                expectedControlledActorClassroomId,
                toRoomId,
                teacher != null ? teacher.CharacterId : string.Empty,
                detected,
                usedDistraction,
                usedDistraction ? "teacher_distracted" : "sneak_out"));
        }

        private bool RollSkipDetection(bool usedDistraction, bool hasTeacher)
        {
            int alertness = bootstrap != null && bootstrap.GameState != null ? bootstrap.GameState.TeacherAlertness : 0;
            float chance = Mathf.Clamp01(baseSkipDetectionChance + alertness / 100f * 0.38f);
            if (usedDistraction)
            {
                chance *= distractedSkipDetectionMultiplier;
            }

            if (!hasTeacher)
            {
                chance *= noTeacherSkipDetectionMultiplier;
            }

            return UnityEngine.Random.value < Mathf.Clamp01(chance);
        }

        private void RefreshPrompt()
        {
            EnsureDomainOwners();
            if (classroomFacts == null || !classroomFacts.IsClassSessionNow())
            {
                currentPrompt = CampusClassroomTextCatalog.Get(CampusClassroomTextId.WaitingForClass);
                return;
            }

            if (HasActiveDistraction)
            {
                currentPrompt = CampusClassroomTextCatalog.Format(
                    CampusClassroomTextId.DistractionActive,
                    DistractionRemainingSeconds.ToString("0.0"));
                return;
            }

            currentPrompt = CampusClassroomTextCatalog.Get(CampusClassroomTextId.ClassInSession);
        }
    }
}
