using System;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Schedule
{
    internal sealed class CampusClassroomFacts
    {
        private const int SleepyheadDozeThreshold = 55;
        private const int OrdinaryDozeThreshold = 75;

        private readonly CampusScheduleService scheduleService;
        private readonly CampusWorldService worldService;
        private readonly CampusRosterService rosterService;

        public CampusClassroomFacts(
            CampusScheduleService scheduleService,
            CampusWorldService worldService,
            CampusRosterService rosterService)
        {
            this.scheduleService = scheduleService;
            this.worldService = worldService;
            this.rosterService = rosterService;
        }

        public bool IsClassSessionNow()
        {
            return scheduleService != null && scheduleService.IsClassSessionNow();
        }

        public bool IsTeacherDistractedInRoom(
            string activeClassroomId,
            float distractedUntilTime,
            string roomId,
            float now)
        {
            return now < distractedUntilTime &&
                   !string.IsNullOrWhiteSpace(activeClassroomId) &&
                   !string.IsNullOrWhiteSpace(roomId) &&
                   string.Equals(activeClassroomId.Trim(), roomId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public bool CanStudentDozeOff(
            CampusCharacterRuntime student,
            Func<string, bool> hasDozedThisSegment,
            bool force,
            out CampusGameplayRoom classroom)
        {
            classroom = null;
            if (student == null ||
                student.Data == null ||
                student.Data.Role != CampusCharacterRole.Student ||
                student.Data.State == CampusCharacterState.Punished ||
                student.Data.State == CampusCharacterState.Sleeping ||
                !IsClassSessionNow())
            {
                return false;
            }

            if (hasDozedThisSegment != null && hasDozedThisSegment(student.CharacterId))
            {
                return false;
            }

            classroom = ResolveRuntimeRoom(student);
            if (classroom == null || classroom.RoomType != CampusRoomType.Classroom)
            {
                return false;
            }

            if (force)
            {
                return true;
            }

            int threshold = student.Data.HasTrait(CampusCharacterTrait.Sleepyhead)
                ? SleepyheadDozeThreshold
                : OrdinaryDozeThreshold;
            return student.Data.Sleepiness >= threshold;
        }

        public string ResolveExpectedControlledActorClassroomId()
        {
            if (!IsClassSessionNow() || rosterService == null || worldService == null)
            {
                return string.Empty;
            }

            CampusCharacterRuntime controlledActor = rosterService.PlayerRuntime;
            CampusGameplayRoom controlledActorRoom = ResolveRuntimeRoom(controlledActor);
            if (controlledActorRoom != null && controlledActorRoom.RoomType == CampusRoomType.Classroom)
            {
                return controlledActorRoom.RoomId;
            }

            CampusGameplayRoom firstClassroom = worldService.FindFirstUsableRoom(CampusRoomType.Classroom) ??
                                                worldService.FindFirstRoom(CampusRoomType.Classroom);
            return firstClassroom != null ? firstClassroom.RoomId : string.Empty;
        }

        public CampusCharacterRuntime FindTeacherForRoom(string roomId)
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

        public CampusGameplayRoom ResolveRuntimeRoom(CampusCharacterRuntime runtime)
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
    }
}
