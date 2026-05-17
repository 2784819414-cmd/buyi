using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Schedule
{
    [DisallowMultipleComponent]
    public sealed class CampusClassroomLoopService : MonoBehaviour
    {
        private const float ThinkIntervalSeconds = 0.65f;
        private const int SleepyheadDozeThreshold = 55;
        private const int OrdinaryDozeThreshold = 75;

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

        [SerializeField] private string currentPrompt = "课堂闭环等待上课。";
        [SerializeField] private string activeClassroomId = string.Empty;
        [SerializeField] private string distractedTeacherId = string.Empty;
        [SerializeField] private string distractionSourceStudentId = string.Empty;
        [SerializeField] private string expectedPlayerClassroomId = string.Empty;
        [SerializeField] private float distractedUntilTime = -1f;
        [SerializeField] private int dailyDozeEventCount;
        [SerializeField] private int dailySneakOutCount;
        [SerializeField] private int dailyCaughtSkippingCount;
        [SerializeField] private bool playerLeaveHandledThisSegment;

        private readonly HashSet<string> dozedStudentIdsThisSegment =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private float nextThinkTime = -999f;

        public string CurrentPrompt => currentPrompt;
        public string ActiveClassroomId => activeClassroomId;
        public string DistractedTeacherId => distractedTeacherId;
        public string DistractionSourceStudentId => distractionSourceStudentId;
        public int DailyDozeEventCount => dailyDozeEventCount;
        public int DailySneakOutCount => dailySneakOutCount;
        public int DailyCaughtSkippingCount => dailyCaughtSkippingCount;
        public bool HasActiveDistraction => Time.time < distractedUntilTime && !string.IsNullOrWhiteSpace(activeClassroomId);
        public float DistractionRemainingSeconds => HasActiveDistraction ? Mathf.Max(0f, distractedUntilTime - Time.time) : 0f;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            timeController = bootstrap != null ? bootstrap.TimeController : null;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;
            scheduleService = bootstrap != null ? bootstrap.ScheduleService : null;
            gameplayEventHub = bootstrap != null ? bootstrap.GameplayEventHub : null;

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
            return HasActiveDistraction &&
                   !string.IsNullOrWhiteSpace(roomId) &&
                   string.Equals(activeClassroomId, roomId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public bool TryForceSleepyDistraction()
        {
            ResolveReferences();
            return TryTriggerSleepyStudentDistraction(true);
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
            if (!IsClassSessionNow())
            {
                ClearDistraction();
                RefreshPrompt();
                return;
            }

            if (!HasActiveDistraction)
            {
                TryTriggerSleepyStudentDistraction(false);
            }

            DetectPlayerLeavingClassroom();
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

        private void ResetSegmentState()
        {
            dozedStudentIdsThisSegment.Clear();
            playerLeaveHandledThisSegment = false;
            expectedPlayerClassroomId = ResolveExpectedPlayerClassroomId();
            ClearDistraction();
        }

        private void ClearDistraction()
        {
            activeClassroomId = string.Empty;
            distractedTeacherId = string.Empty;
            distractionSourceStudentId = string.Empty;
            distractedUntilTime = -1f;
        }

        private bool IsClassSessionNow()
        {
            return scheduleService != null && scheduleService.IsClassSessionNow();
        }

        private bool TryTriggerSleepyStudentDistraction(bool force)
        {
            if (rosterService == null || worldService == null)
            {
                return false;
            }

            CampusCharacterRuntime sleepyStudent = FindSleepyStudentCandidate(force);
            if (sleepyStudent == null || sleepyStudent.Data == null)
            {
                return false;
            }

            CampusGameplayRoom classroom = ResolveRuntimeRoom(sleepyStudent);
            if (classroom == null || classroom.RoomType != CampusRoomType.Classroom)
            {
                return false;
            }

            CampusCharacterRuntime teacher = FindTeacherForRoom(classroom.RoomId);
            sleepyStudent.Data.SetState(CampusCharacterState.Sleeping);
            sleepyStudent.Data.AddMemory(CampusCharacterMemoryId.DozedOffInClass);
            dozedStudentIdsThisSegment.Add(sleepyStudent.CharacterId);

            activeClassroomId = classroom.RoomId;
            distractionSourceStudentId = sleepyStudent.CharacterId;
            distractedTeacherId = teacher != null ? teacher.CharacterId : string.Empty;
            distractedUntilTime = Time.time + UnityEngine.Random.Range(
                Mathf.Min(minDistractionSeconds, maxDistractionSeconds),
                Mathf.Max(minDistractionSeconds, maxDistractionSeconds));
            dailyDozeEventCount++;

            if (teacher != null && teacher.Data != null)
            {
                teacher.Data.SetState(CampusCharacterState.Nervous);
                teacher.Data.AddMemory(CampusCharacterMemoryId.NoticedClassroomDozing);
            }

            gameplayEventHub?.PublishStudentDozedOff(new CampusStudentDozedOffEvent(
                sleepyStudent.CharacterId,
                classroom.RoomId,
                sleepyStudent.Data.Sleepiness));
            gameplayEventHub?.PublishTeacherDistracted(new CampusTeacherDistractedEvent(
                distractedTeacherId,
                sleepyStudent.CharacterId,
                classroom.RoomId,
                DistractionRemainingSeconds));

            WriteLog("[课堂] " + sleepyStudent.Data.DisplayName + "撑不住睡着了，老师注意力被吸引。");
            return true;
        }

        private CampusCharacterRuntime FindSleepyStudentCandidate(bool force)
        {
            CampusCharacterRuntime best = null;
            int bestSleepiness = int.MinValue;
            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Student))
            {
                if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                if (dozedStudentIdsThisSegment.Contains(runtime.CharacterId))
                {
                    continue;
                }

                CampusGameplayRoom room = ResolveRuntimeRoom(runtime);
                if (room == null || room.RoomType != CampusRoomType.Classroom)
                {
                    continue;
                }

                int threshold = runtime.Data.HasTrait(CampusCharacterTrait.Sleepyhead)
                    ? SleepyheadDozeThreshold
                    : OrdinaryDozeThreshold;
                if (!force && runtime.Data.Sleepiness < threshold)
                {
                    continue;
                }

                if (best == null || runtime.Data.Sleepiness > bestSleepiness)
                {
                    best = runtime;
                    bestSleepiness = runtime.Data.Sleepiness;
                }
            }

            return best;
        }

        private void DetectPlayerLeavingClassroom()
        {
            if (playerLeaveHandledThisSegment || rosterService == null || rosterService.PlayerRuntime == null)
            {
                return;
            }

            CampusCharacterRuntime player = rosterService.PlayerRuntime;
            CampusGameplayRoom currentRoom = ResolveRuntimeRoom(player);
            if (string.IsNullOrWhiteSpace(expectedPlayerClassroomId))
            {
                expectedPlayerClassroomId = ResolveExpectedPlayerClassroomId();
            }

            if (string.IsNullOrWhiteSpace(expectedPlayerClassroomId))
            {
                return;
            }

            if (currentRoom != null &&
                string.Equals(currentRoom.RoomId, expectedPlayerClassroomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            playerLeaveHandledThisSegment = true;
            dailySneakOutCount++;

            string toRoomId = currentRoom != null ? currentRoom.RoomId : string.Empty;
            bool usedDistraction = IsTeacherDistractedInRoom(expectedPlayerClassroomId);
            CampusCharacterRuntime teacher = FindTeacherForRoom(expectedPlayerClassroomId);
            bool detected = RollSkipDetection(usedDistraction, teacher != null);
            if (detected)
            {
                dailyCaughtSkippingCount++;
                player.Data.AddMemory(CampusCharacterMemoryId.CaughtSkippingClass);
                WriteLog("[课堂] 玩家离开教室时被老师发现。");
            }
            else
            {
                player.Data.AddMemory(CampusCharacterMemoryId.SneakedOutDuringClass);
                WriteLog(usedDistraction
                    ? "[课堂] 玩家趁老师分神离开教室。"
                    : "[课堂] 玩家偷偷离开教室，暂时没人追出来。");
            }

            gameplayEventHub?.PublishPlayerSkipClass(new CampusPlayerSkipClassEvent(
                player.CharacterId,
                expectedPlayerClassroomId,
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

        private string ResolveExpectedPlayerClassroomId()
        {
            if (!IsClassSessionNow() || rosterService == null || worldService == null)
            {
                return string.Empty;
            }

            CampusCharacterRuntime player = rosterService.PlayerRuntime;
            CampusGameplayRoom playerRoom = ResolveRuntimeRoom(player);
            if (playerRoom != null && playerRoom.RoomType == CampusRoomType.Classroom)
            {
                return playerRoom.RoomId;
            }

            CampusGameplayRoom firstClassroom = worldService.FindFirstUsableRoom(CampusRoomType.Classroom) ??
                                                worldService.FindFirstRoom(CampusRoomType.Classroom);
            return firstClassroom != null ? firstClassroom.RoomId : string.Empty;
        }

        private CampusCharacterRuntime FindTeacherForRoom(string roomId)
        {
            if (rosterService == null || string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.EnumerateByRole(CampusCharacterRole.Teacher))
            {
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                CampusGameplayRoom room = ResolveRuntimeRoom(runtime);
                if (room != null && string.Equals(room.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    return runtime;
                }
            }

            return null;
        }

        private CampusGameplayRoom ResolveRuntimeRoom(CampusCharacterRuntime runtime)
        {
            if (runtime == null || worldService == null)
            {
                return null;
            }

            CampusGameplayRoom room = worldService.FindRoomForRuntime(runtime);
            if (room != null)
            {
                return room;
            }

            return runtime.Data != null ? worldService.FindRoomById(runtime.Data.CurrentRoomId) : null;
        }

        private void RefreshPrompt()
        {
            if (!IsClassSessionNow())
            {
                currentPrompt = "课堂闭环等待上课。";
                return;
            }

            if (HasActiveDistraction)
            {
                currentPrompt = "老师分心窗口开启：" + DistractionRemainingSeconds.ToString("0.0") + " 秒，可传纸条或离开教室。";
                return;
            }

            currentPrompt = "上课中：高困倦学生可能睡着，玩家离开教室会触发逃课判定。";
        }

        private void WriteLog(string message)
        {
            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(message);
            }
        }
    }
}
