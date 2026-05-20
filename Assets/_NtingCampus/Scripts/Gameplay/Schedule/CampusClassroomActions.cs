using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Schedule
{
    internal sealed class CampusClassroomActions
    {
        private readonly CampusClassroomLoopService service;
        private readonly CampusClassroomFacts facts;

        public CampusClassroomActions(
            CampusClassroomLoopService service,
            CampusClassroomFacts facts)
        {
            this.service = service;
            this.facts = facts;
        }

        public bool CanStudentDozeOff(CampusCharacterRuntime student, bool force)
        {
            if (service == null || facts == null)
            {
                return false;
            }

            if (!force && service.HasActiveDistraction)
            {
                return false;
            }

            return facts.CanStudentDozeOff(student, service.HasStudentDozedThisSegment, force, out _);
        }

        public bool TryStudentDozeOff(CampusCharacterRuntime student, bool force)
        {
            if (!CanStudentDozeOff(student, force) ||
                !facts.CanStudentDozeOff(student, service.HasStudentDozedThisSegment, force, out CampusGameplayRoom classroom))
            {
                return false;
            }

            CampusCharacterRuntime teacher = facts.FindTeacherForRoom(classroom.RoomId);
            student.Data.SetState(CampusCharacterState.Sleeping);
            student.Data.AddMemory(CampusCharacterMemoryId.DozedOffInClass);
            service.MarkStudentDozedThisSegment(student.CharacterId);

            float seconds = service.BeginTeacherDistraction(classroom, student, teacher);
            service.GameplayEventHub?.PublishStudentDozedOff(new CampusStudentDozedOffEvent(
                student.CharacterId,
                classroom.RoomId,
                student.Data.Sleepiness));
            service.GameplayEventHub?.PublishTeacherDistracted(new CampusTeacherDistractedEvent(
                teacher != null ? teacher.CharacterId : string.Empty,
                student.CharacterId,
                classroom.RoomId,
                seconds));

            service.WriteClassroomLog(CampusClassroomTextCatalog.Format(
                CampusClassroomTextId.StudentDozedLog,
                student.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)));
            return true;
        }
    }

    internal sealed class CampusClassroomDozeActionCommand : ICampusCharacterActionCommand
    {
        private readonly CampusClassroomLoopService classroomLoopService;

        public CampusClassroomDozeActionCommand(CampusClassroomLoopService classroomLoopService)
        {
            this.classroomLoopService = classroomLoopService;
        }

        public bool TryExecute(CampusCharacterRuntime actor, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (classroomLoopService == null || actor == null)
            {
                return false;
            }

            bool succeeded = classroomLoopService.TryStudentDozeOff(actor, false);
            result = succeeded
                ? new StorageTransferResult(true, false, false, string.Empty, string.Empty)
                : StorageTransferResult.Fail(string.Empty);
            return succeeded;
        }
    }
}
