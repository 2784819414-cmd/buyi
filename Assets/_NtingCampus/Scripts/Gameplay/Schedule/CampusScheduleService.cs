using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Schedule
{
    [DisallowMultipleComponent]
    public sealed class CampusScheduleService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusTimeController timeController;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusRosterService rosterService;

        public CampusTimeController TimeController => timeController;
        public bool IsNightActionWindow => timeController != null && timeController.AllowsNightFreeAction;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            timeController = bootstrap != null ? bootstrap.TimeController : null;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            rosterService = bootstrap != null ? bootstrap.RosterService : null;

            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.SegmentChanged += HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
                timeController.DailySettlementStarted += HandleDailySettlementStarted;
            }

            ApplyCurrentSegment();
        }

        public bool IsClassSession(CampusTimeSegment segment)
        {
            switch (segment)
            {
                case CampusTimeSegment.MorningReading:
                case CampusTimeSegment.MorningClass1:
                case CampusTimeSegment.MorningClass2:
                case CampusTimeSegment.MorningClass3:
                case CampusTimeSegment.MorningClass4:
                case CampusTimeSegment.AfternoonClass1:
                case CampusTimeSegment.AfternoonClass2:
                case CampusTimeSegment.AfternoonClass3:
                case CampusTimeSegment.AfternoonClass4:
                case CampusTimeSegment.EveningStudy1:
                case CampusTimeSegment.EveningStudy2:
                case CampusTimeSegment.EveningStudy3:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsClassSessionNow()
        {
            return timeController != null && IsClassSession(timeController.CurrentSegment);
        }

        public CampusRoomType GetScheduledRoomType(CampusCharacterData data)
        {
            return timeController == null ? CampusRoomType.Unknown : ResolveScheduledRoomType(data, timeController.CurrentSegment);
        }

        public CampusCharacterTaskDirective BuildDirective(CampusCharacterRuntime runtime)
        {
            CampusCharacterData data = runtime != null ? runtime.Data : null;
            CampusTimeSegment segment = timeController != null ? timeController.CurrentSegment : CampusTimeSegment.WakeUp;
            CampusCharacterTaskDirective directive = new CampusCharacterTaskDirective();

            if (data == null)
            {
                directive.TaskType = CampusCharacterTaskType.Idle;
                directive.DebugLabel = "NoData";
                return directive;
            }

            if (data.State == CampusCharacterState.Punished)
            {
                directive.TaskType = CampusCharacterTaskType.UseOfficeDesk;
                directive.TargetRoomType = CampusRoomType.Office;
                directive.PreferredFacilityType = CampusFacilityType.OfficeDesk;
                directive.DebugLabel = "Punished";
                return directive;
            }

            if (data.Role == CampusCharacterRole.Teacher)
            {
                return BuildTeacherDirective(data, segment);
            }

            return BuildStudentDirective(data, segment);
        }

        public CampusGameplayRoom ResolveBestRoom(CampusCharacterRuntime runtime, CampusCharacterTaskDirective directive)
        {
            if (directive == null || worldService == null)
            {
                return null;
            }

            CampusGameplayRoom currentRoom = worldService.FindRoomForRuntime(runtime);
            if (currentRoom != null &&
                directive.TargetRoomType != CampusRoomType.Unknown &&
                currentRoom.RoomType == directive.TargetRoomType)
            {
                return currentRoom;
            }

            CampusGameplayRoom scheduledRoom = worldService.FindFirstUsableRoom(directive.TargetRoomType);
            if (scheduledRoom == null)
            {
                scheduledRoom = worldService.FindFirstRoom(directive.TargetRoomType);
            }

            if (scheduledRoom != null)
            {
                return scheduledRoom;
            }

            return currentRoom;
        }

        private void OnDestroy()
        {
            if (timeController != null)
            {
                timeController.SegmentChanged -= HandleSegmentChanged;
                timeController.DailySettlementStarted -= HandleDailySettlementStarted;
            }
        }

        private void HandleSegmentChanged(CampusTimeSegment _, CampusTimeSegment __)
        {
            ApplyCurrentSegment();
        }

        private void HandleDailySettlementStarted(CampusGameDate _)
        {
            if (rosterService == null)
            {
                return;
            }

            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                runtime.Data.SetState(CampusCharacterState.Normal);
                runtime.Data.SetSleepiness(Mathf.Clamp(runtime.Data.Sleepiness + 15, 0, 100));
            }
        }

        private void ApplyCurrentSegment()
        {
            if (timeController == null || worldService == null || rosterService == null)
            {
                return;
            }

            CampusTimeSegment currentSegment = timeController.CurrentSegment;
            bool classSession = IsClassSession(currentSegment);

            foreach (CampusCharacterRuntime runtime in rosterService.Runtimes)
            {
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                SyncRuntimeRoomBinding(runtime);

                if (runtime.Data.Role == CampusCharacterRole.Student && classSession)
                {
                    runtime.Data.SetState(CampusCharacterState.Normal);
                }
            }
        }

        private void SyncRuntimeRoomBinding(CampusCharacterRuntime runtime)
        {
            int floorIndex = ResolveFloorIndex(runtime);
            CampusGameplayRoom actualRoom = worldService.FindRoomForPosition(floorIndex, runtime.transform.position);
            runtime.Data.SetCurrentRoom(actualRoom != null ? actualRoom.RoomId : string.Empty);
        }

        private static CampusCharacterTaskDirective BuildTeacherDirective(CampusCharacterData data, CampusTimeSegment segment)
        {
            CampusCharacterTaskDirective directive = new CampusCharacterTaskDirective();
            bool instructional = IsInstructionalSegment(segment);
            bool patrolTeacher = (data.TeacherDuty & CampusTeacherDuty.PatrolDirector) != 0;

            if (instructional)
            {
                directive.TaskType = CampusCharacterTaskType.TeachClass;
                directive.TargetRoomType = CampusRoomType.Classroom;
                directive.PreferredFacilityType = CampusFacilityType.Podium;
                directive.HoldRadius = 0.12f;
                directive.RequiresFacingFront = true;
                directive.DebugLabel = "TeachClass";
                return directive;
            }

            if (segment == CampusTimeSegment.DormCheck || segment == CampusTimeSegment.NightFree)
            {
                directive.TaskType = patrolTeacher ? CampusCharacterTaskType.PatrolHallway : CampusCharacterTaskType.UseOfficeDesk;
                directive.TargetRoomType = patrolTeacher ? CampusRoomType.CommonActivityZone : CampusRoomType.Office;
                directive.PreferredFacilityType = patrolTeacher ? CampusFacilityType.Door : CampusFacilityType.OfficeDesk;
                directive.HoldRadius = 0.2f;
                directive.DebugLabel = patrolTeacher ? "NightPatrol" : "OfficeDuty";
                return directive;
            }

            directive.TaskType = patrolTeacher ? CampusCharacterTaskType.PatrolHallway : CampusCharacterTaskType.UseOfficeDesk;
            directive.TargetRoomType = patrolTeacher ? CampusRoomType.CommonActivityZone : CampusRoomType.Office;
            directive.PreferredFacilityType = patrolTeacher ? CampusFacilityType.Door : CampusFacilityType.OfficeDesk;
            directive.HoldRadius = patrolTeacher ? 0.35f : 0.16f;
            directive.DebugLabel = patrolTeacher ? "Patrol" : "Office";
            return directive;
        }

        private static CampusCharacterTaskDirective BuildStudentDirective(CampusCharacterData data, CampusTimeSegment segment)
        {
            CampusCharacterTaskDirective directive = new CampusCharacterTaskDirective();
            bool instructional = IsInstructionalSegment(segment);

            if (segment == CampusTimeSegment.WakeUp ||
                segment == CampusTimeSegment.DormReturn ||
                segment == CampusTimeSegment.DormCheck ||
                segment == CampusTimeSegment.LightsOut ||
                segment == CampusTimeSegment.NightFree)
            {
                directive.TaskType = CampusCharacterTaskType.RestInDorm;
                directive.TargetRoomType = CampusRoomType.Dormitory;
                directive.PreferredFacilityType = CampusFacilityType.Bed;
                directive.HoldRadius = 0.18f;
                directive.DebugLabel = "Dorm";
                return directive;
            }

            if (instructional)
            {
                directive.TargetRoomType = CampusRoomType.Classroom;
                directive.PreferredFacilityType = CampusFacilityType.StudentDesk;
                directive.RequiresSeat = true;
                directive.RequiresFacingFront = true;
                directive.HoldRadius = 0.12f;

                if (data.Sleepiness >= 55 && data.HasTrait(CampusCharacterTrait.Sleepyhead))
                {
                    directive.TaskType = CampusCharacterTaskType.DozeAtDesk;
                    directive.DebugLabel = "DozeAtDesk";
                    return directive;
                }

                directive.TaskType = CampusCharacterTaskType.AttendClass;
                directive.DebugLabel = "AttendClass";
                return directive;
            }

            if (segment == CampusTimeSegment.MorningBreak1 ||
                segment == CampusTimeSegment.MorningExerciseBreak ||
                segment == CampusTimeSegment.MorningBreak2 ||
                segment == CampusTimeSegment.AfternoonBreak1 ||
                segment == CampusTimeSegment.AfternoonBreak2 ||
                segment == CampusTimeSegment.AfternoonBreak3 ||
                segment == CampusTimeSegment.EveningBreak1 ||
                segment == CampusTimeSegment.EveningBreak2)
            {
                if (data.HasTrait(CampusCharacterTrait.Tattletale))
                {
                    directive.TaskType = CampusCharacterTaskType.CheckBulletinBoard;
                    directive.TargetRoomType = CampusRoomType.CommonActivityZone;
                    directive.PreferredFacilityType = CampusFacilityType.BulletinBoard;
                    directive.HoldRadius = 0.18f;
                    directive.DebugLabel = "CheckBoard";
                    return directive;
                }

                if (data.HasTrait(CampusCharacterTrait.Troublemaker))
                {
                    directive.TaskType = CampusCharacterTaskType.Socialize;
                    directive.TargetRoomType = CampusRoomType.CommonActivityZone;
                    directive.PreferredFacilityType = CampusFacilityType.Door;
                    directive.HoldRadius = 0.28f;
                    directive.DebugLabel = "Socialize";
                    return directive;
                }

                directive.TaskType = CampusCharacterTaskType.WanderCommonArea;
                directive.TargetRoomType = CampusRoomType.CommonActivityZone;
                directive.PreferredFacilityType = CampusFacilityType.Door;
                directive.HoldRadius = 0.35f;
                directive.DebugLabel = "Break";
                return directive;
            }

            directive.TaskType = CampusCharacterTaskType.WanderCommonArea;
            directive.TargetRoomType = CampusRoomType.CommonActivityZone;
            directive.PreferredFacilityType = CampusFacilityType.Door;
            directive.HoldRadius = 0.3f;
            directive.DebugLabel = "Free";
            return directive;
        }

        private static CampusRoomType ResolveScheduledRoomType(CampusCharacterData data, CampusTimeSegment currentSegment)
        {
            CampusCharacterTaskDirective directive = data == null
                ? null
                : data.Role == CampusCharacterRole.Teacher
                    ? BuildTeacherDirective(data, currentSegment)
                    : BuildStudentDirective(data, currentSegment);
            return directive != null ? directive.TargetRoomType : CampusRoomType.Unknown;
        }

        private static bool IsInstructionalSegment(CampusTimeSegment currentSegment)
        {
            switch (currentSegment)
            {
                case CampusTimeSegment.MorningReading:
                case CampusTimeSegment.MorningClass1:
                case CampusTimeSegment.MorningClass2:
                case CampusTimeSegment.MorningClass3:
                case CampusTimeSegment.MorningClass4:
                case CampusTimeSegment.AfternoonClass1:
                case CampusTimeSegment.AfternoonClass2:
                case CampusTimeSegment.AfternoonClass3:
                case CampusTimeSegment.AfternoonClass4:
                case CampusTimeSegment.EveningStudy1:
                case CampusTimeSegment.EveningStudy2:
                case CampusTimeSegment.EveningStudy3:
                    return true;
                default:
                    return false;
            }
        }

        private int ResolveFloorIndex(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return 1;
            }

            if (CampusRuntimeGameplayOverlayLoader.TryGetManagedEntity(runtime, out CampusRuntimeGameplayOverlayEntity overlayEntity))
            {
                return overlayEntity.FloorIndex;
            }

            CampusSceneCharacterDefinition sceneCharacter = runtime.GetComponent<CampusSceneCharacterDefinition>();
            if (sceneCharacter != null)
            {
                return sceneCharacter.FloorIndex;
            }

            if (runtime.Data != null && !string.IsNullOrWhiteSpace(runtime.Data.CurrentRoomId))
            {
                CampusGameplayRoom room = worldService.FindRoomById(runtime.Data.CurrentRoomId);
                if (room != null)
                {
                    return room.FloorIndex;
                }
            }

            return 1;
        }
    }
}
