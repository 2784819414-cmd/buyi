using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using System.Collections.Generic;
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

            if (data.Role == CampusCharacterRole.Staff)
            {
                return BuildStaffDirective(data, segment);
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

            List<CampusGameplayRoom> candidates = worldService.GetRoomsByType(directive.TargetRoomType, true);
            if (candidates.Count == 0)
            {
                candidates = worldService.GetRoomsByType(directive.TargetRoomType, false);
            }

            CampusGameplayRoom scheduledRoom = ResolveDistributedRoom(runtime, currentRoom, directive, candidates);
            if (scheduledRoom != null)
            {
                return scheduledRoom;
            }

            CampusGameplayRoom fallbackRoom = ResolveFallbackRoom(directive.TargetRoomType);
            if (fallbackRoom != null)
            {
                return fallbackRoom;
            }

            return currentRoom;
        }

        private CampusGameplayRoom ResolveDistributedRoom(
            CampusCharacterRuntime runtime,
            CampusGameplayRoom currentRoom,
            CampusCharacterTaskDirective directive,
            List<CampusGameplayRoom> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            string runtimeKey = ResolveRuntimeDistributionKey(runtime, directive);
            int runtimeSeed = StableHash(runtimeKey);
            float distanceWeight = ResolveDistanceWeight(directive != null ? directive.TargetRoomType : CampusRoomType.Unknown);

            CampusGameplayRoom bestRoom = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                CampusGameplayRoom room = candidates[i];
                if (room == null)
                {
                    continue;
                }

                float score = ScoreRoomCandidate(runtime, currentRoom, directive, room, runtimeSeed, distanceWeight);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRoom = room;
                }
            }

            return bestRoom;
        }

        private float ScoreRoomCandidate(
            CampusCharacterRuntime runtime,
            CampusGameplayRoom currentRoom,
            CampusCharacterTaskDirective directive,
            CampusGameplayRoom candidate,
            int runtimeSeed,
            float distanceWeight)
        {
            float score = PseudoRandom01(runtimeSeed, StableHash(candidate.RoomId) + (int)candidate.RoomType * 97) * 5f;

            if (runtime != null)
            {
                score += Vector2.SqrMagnitude((Vector2)(candidate.WorldCenter - runtime.transform.position)) * distanceWeight;
            }

            if (currentRoom != null && candidate.FloorIndex == currentRoom.FloorIndex)
            {
                score -= 0.8f;
            }

            if (directive != null && directive.PreferredFacilityType != CampusFacilityType.Unknown)
            {
                score += candidate.GetFacilityCount(directive.PreferredFacilityType) > 0 ? -4f : 8f;
            }

            if (directive != null && directive.TargetRoomType == CampusRoomType.Classroom && runtime != null && runtime.Data != null)
            {
                score += ClassroomScatterBias(runtime.Data.ClassId, candidate.RoomId);
            }

            return score;
        }

        private static float ResolveDistanceWeight(CampusRoomType roomType)
        {
            switch (roomType)
            {
                case CampusRoomType.Classroom:
                case CampusRoomType.Office:
                case CampusRoomType.Dormitory:
                    return 0.22f;
                case CampusRoomType.Corridor:
                case CampusRoomType.CommonActivityZone:
                    return 0.035f;
                default:
                    return 0.08f;
            }
        }

        private static float ClassroomScatterBias(string classId, string roomId)
        {
            if (string.IsNullOrWhiteSpace(classId) || string.IsNullOrWhiteSpace(roomId))
            {
                return 0f;
            }

            int classSeed = StableHash(classId);
            int roomSeed = StableHash(roomId);
            return PseudoRandom01(classSeed, roomSeed) * 1.2f;
        }

        private static string ResolveRuntimeDistributionKey(CampusCharacterRuntime runtime, CampusCharacterTaskDirective directive)
        {
            if (runtime == null)
            {
                return "npc|" + (directive != null ? directive.TaskType.ToString() : "None");
            }

            CampusCharacterData data = runtime.Data;
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.ClassId) &&
                    directive != null &&
                    directive.TargetRoomType == CampusRoomType.Classroom)
                {
                    return data.ClassId + "|" + runtime.CharacterId;
                }

                return data.Id + "|" + data.Role + "|" + data.TeacherDuty;
            }

            return !string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.CharacterId
                : runtime.GetInstanceID().ToString();
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

        private static float PseudoRandom01(int seed, int salt)
        {
            unchecked
            {
                int value = seed;
                value ^= salt * 374761393;
                value = (value << 13) ^ value;
                int mixed = value * (value * value * 15731 + 789221) + 1376312589;
                return (mixed & 0x7fffffff) / 2147483647f;
            }
        }

        private CampusGameplayRoom ResolveFallbackRoom(CampusRoomType targetRoomType)
        {
            switch (targetRoomType)
            {
                case CampusRoomType.Office:
                case CampusRoomType.CommonActivityZone:
                    return worldService.FindFirstRoom(CampusRoomType.Corridor) ??
                           worldService.FindFirstRoom(CampusRoomType.Classroom);
                case CampusRoomType.Corridor:
                    return worldService.FindFirstRoom(CampusRoomType.CommonActivityZone) ??
                           worldService.FindFirstRoom(CampusRoomType.Classroom);
                default:
                    return null;
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
            bool homeroomTeacher = (data.TeacherDuty & CampusTeacherDuty.HomeroomTeacher) != 0;

            if (instructional && patrolTeacher)
            {
                directive.TaskType = CampusCharacterTaskType.PatrolHallway;
                directive.TargetRoomType = CampusRoomType.Corridor;
                directive.PreferredFacilityType = CampusFacilityType.Door;
                directive.HoldRadius = 0.3f;
                directive.DebugLabel = "ClassPatrol";
                return directive;
            }

            if (instructional && homeroomTeacher)
            {
                directive.TaskType = CampusCharacterTaskType.TeachClass;
                directive.TargetRoomType = CampusRoomType.Classroom;
                directive.PreferredFacilityType = CampusFacilityType.Podium;
                directive.HoldRadius = 0.12f;
                directive.RequiresFacingFront = true;
                directive.DebugLabel = "TeachClass";
                return directive;
            }

            if (instructional)
            {
                directive.TaskType = CampusCharacterTaskType.PatrolHallway;
                directive.TargetRoomType = CampusRoomType.Corridor;
                directive.PreferredFacilityType = CampusFacilityType.Door;
                directive.HoldRadius = 0.3f;
                directive.DebugLabel = "ClassSupportPatrol";
                return directive;
            }

            if (segment == CampusTimeSegment.DormCheck || segment == CampusTimeSegment.NightFree)
            {
                directive.TaskType = patrolTeacher ? CampusCharacterTaskType.PatrolHallway : CampusCharacterTaskType.UseOfficeDesk;
                directive.TargetRoomType = patrolTeacher ? CampusRoomType.Corridor : CampusRoomType.Office;
                directive.PreferredFacilityType = patrolTeacher ? CampusFacilityType.Door : CampusFacilityType.OfficeDesk;
                directive.HoldRadius = 0.2f;
                directive.DebugLabel = patrolTeacher ? "NightPatrol" : "OfficeDuty";
                return directive;
            }

            directive.TaskType = patrolTeacher ? CampusCharacterTaskType.PatrolHallway : CampusCharacterTaskType.UseOfficeDesk;
            directive.TargetRoomType = patrolTeacher ? CampusRoomType.Corridor : CampusRoomType.Office;
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

        private static CampusCharacterTaskDirective BuildStaffDirective(CampusCharacterData data, CampusTimeSegment segment)
        {
            CampusCharacterTaskDirective directive = new CampusCharacterTaskDirective();

            if ((data.StaffDuty & CampusStaffDuty.CanteenClerk) != 0)
            {
                directive.TaskType = CampusCharacterTaskType.WorkCanteenCounter;
                directive.TargetRoomType = CampusRoomType.Canteen;
                directive.PreferredFacilityType = CampusFacilityType.CanteenCounter;
                directive.HoldRadius = 0.18f;
                directive.DebugLabel = "CanteenClerk";
                return directive;
            }

            if ((data.StaffDuty & CampusStaffDuty.DeliveryWatcher) != 0)
            {
                directive.TaskType = CampusCharacterTaskType.WatchDeliveryPoint;
                directive.TargetRoomType = CampusRoomType.Outdoor;
                directive.PreferredFacilityType = CampusFacilityType.DeliveryDropPoint;
                directive.HoldRadius = 0.2f;
                directive.DebugLabel = "DeliveryWatch";
                return directive;
            }

            directive.TaskType = CampusCharacterTaskType.WanderCommonArea;
            directive.TargetRoomType = CampusRoomType.CommonActivityZone;
            directive.PreferredFacilityType = CampusFacilityType.Door;
            directive.HoldRadius = 0.3f;
            directive.DebugLabel = "StaffIdle";
            return directive;
        }

        private static CampusRoomType ResolveScheduledRoomType(CampusCharacterData data, CampusTimeSegment currentSegment)
        {
            CampusCharacterTaskDirective directive = data == null
                ? null
                : data.Role == CampusCharacterRole.Teacher
                    ? BuildTeacherDirective(data, currentSegment)
                    : data.Role == CampusCharacterRole.Staff
                        ? BuildStaffDirective(data, currentSegment)
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
