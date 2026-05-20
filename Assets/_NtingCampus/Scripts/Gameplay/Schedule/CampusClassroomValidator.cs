using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Schedule
{
    public static class CampusClassroomValidator
    {
        public static void Validate(
            CampusWorldFacts facts,
            List<CampusEcologyValidator.ValidationIssue> issues)
        {
            if (facts == null || issues == null)
            {
                return;
            }

            ValidateStudentAssignments(facts, issues);
            ValidateTeacherAssignments(facts, issues);
        }

        private static void ValidateStudentAssignments(
            CampusWorldFacts facts,
            List<CampusEcologyValidator.ValidationIssue> issues)
        {
            int studentCount = 0;
            Dictionary<string, List<string>> deskOwners =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < facts.Actors.Count; i++)
            {
                CampusWorldFacts.ActorFact actor = facts.Actors[i];
                if (actor == null || actor.Role != CampusCharacterRole.Student)
                {
                    continue;
                }

                studentCount++;
                CampusCharacterAssignmentData assignments = actor.Assignments;
                string classroomId = assignments != null ? assignments.StudentClassroomId : string.Empty;
                string deskId = assignments != null ? assignments.StudentDeskId : string.Empty;

                if (string.IsNullOrWhiteSpace(classroomId))
                {
                    issues.Add(Issue(
                        CampusEcologyValidator.Severity.Warning,
                        actor.ActorId,
                        CampusClassroomValidationTextCatalog.Get(
                            CampusClassroomValidationTextId.StudentMissingClassroomId)));
                }
                else
                {
                    ValidateRoomReference(
                        facts,
                        issues,
                        actor.ActorId,
                        "StudentClassroomId",
                        classroomId,
                        CampusRoomType.Classroom);
                }

                if (string.IsNullOrWhiteSpace(deskId))
                {
                    issues.Add(Issue(
                        CampusEcologyValidator.Severity.Warning,
                        actor.ActorId,
                        CampusClassroomValidationTextCatalog.Get(
                            CampusClassroomValidationTextId.StudentMissingDeskId)));
                    continue;
                }

                AddOwner(deskOwners, deskId, actor.ActorId);
                if (ValidateFacilityType(
                        facts,
                        issues,
                        actor.ActorId,
                        deskId,
                        "StudentDeskId",
                        CampusFacilityType.StudentDesk))
                {
                    ValidateFacilityInAssignedRoom(
                        facts,
                        issues,
                        actor.ActorId,
                        deskId,
                        "StudentDeskId",
                        classroomId,
                        "StudentClassroomId");
                }
            }

            int studentDeskCount = facts.CountFacilities(CampusFacilityType.StudentDesk);
            if (studentCount > 0 && studentDeskCount < studentCount)
            {
                issues.Add(Issue(
                    CampusEcologyValidator.Severity.Error,
                    string.Empty,
                    CampusClassroomValidationTextCatalog.Format(
                        CampusClassroomValidationTextId.StudentDeskCountTooLow,
                        studentCount,
                        studentDeskCount)));
            }

            ReportDuplicateOwners(
                issues,
                deskOwners,
                CampusClassroomValidationTextId.StudentDeskShared);
        }

        private static void ValidateTeacherAssignments(
            CampusWorldFacts facts,
            List<CampusEcologyValidator.ValidationIssue> issues)
        {
            Dictionary<string, List<string>> podiumOwners =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < facts.Actors.Count; i++)
            {
                CampusWorldFacts.ActorFact actor = facts.Actors[i];
                if (actor == null || actor.Role != CampusCharacterRole.Teacher)
                {
                    continue;
                }

                CampusCharacterAssignmentData assignments = actor.Assignments;
                string classroomId = assignments != null ? assignments.TeacherClassroomId : string.Empty;
                string podiumId = assignments != null ? assignments.TeacherPodiumId : string.Empty;

                if (string.IsNullOrWhiteSpace(classroomId))
                {
                    issues.Add(Issue(
                        CampusEcologyValidator.Severity.Warning,
                        actor.ActorId,
                        CampusClassroomValidationTextCatalog.Get(
                            CampusClassroomValidationTextId.TeacherMissingClassroomId)));
                }
                else
                {
                    ValidateRoomReference(
                        facts,
                        issues,
                        actor.ActorId,
                        "TeacherClassroomId",
                        classroomId,
                        CampusRoomType.Classroom);
                }

                if (string.IsNullOrWhiteSpace(podiumId))
                {
                    issues.Add(Issue(
                        CampusEcologyValidator.Severity.Warning,
                        actor.ActorId,
                        CampusClassroomValidationTextCatalog.Get(
                            CampusClassroomValidationTextId.TeacherMissingPodiumId)));
                    continue;
                }

                AddOwner(podiumOwners, podiumId, actor.ActorId);
                if (ValidateFacilityType(
                        facts,
                        issues,
                        actor.ActorId,
                        podiumId,
                        "TeacherPodiumId",
                        CampusFacilityType.Podium,
                        CampusFacilityType.Blackboard))
                {
                    ValidateFacilityInAssignedRoom(
                        facts,
                        issues,
                        actor.ActorId,
                        podiumId,
                        "TeacherPodiumId",
                        classroomId,
                        "TeacherClassroomId");
                }
            }

            ReportDuplicateOwners(
                issues,
                podiumOwners,
                CampusClassroomValidationTextId.TeacherPodiumShared);
        }

        private static bool ValidateRoomReference(
            CampusWorldFacts facts,
            List<CampusEcologyValidator.ValidationIssue> issues,
            string actorId,
            string fieldName,
            string roomId,
            CampusRoomType expectedType)
        {
            if (!facts.TryGetRoom(roomId, out CampusWorldFacts.RoomFact room))
            {
                issues.Add(Issue(
                    CampusEcologyValidator.Severity.Error,
                    actorId,
                    CampusClassroomValidationTextCatalog.Format(
                        CampusClassroomValidationTextId.RoomReferenceMissing,
                        fieldName,
                        roomId)));
                return false;
            }

            if (room.RoomType != expectedType)
            {
                issues.Add(Issue(
                    CampusEcologyValidator.Severity.Error,
                    actorId,
                    CampusClassroomValidationTextCatalog.Format(
                        CampusClassroomValidationTextId.RoomReferenceWrongType,
                        fieldName,
                        room.RoomType,
                        expectedType)));
                return false;
            }

            return true;
        }

        private static bool ValidateFacilityType(
            CampusWorldFacts facts,
            List<CampusEcologyValidator.ValidationIssue> issues,
            string actorId,
            string facilityId,
            string fieldName,
            params CampusFacilityType[] allowedTypes)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                issues.Add(Issue(
                    CampusEcologyValidator.Severity.Warning,
                    actorId,
                    CampusClassroomValidationTextCatalog.Format(
                        CampusClassroomValidationTextId.FacilityReferenceEmpty,
                        fieldName)));
                return false;
            }

            if (!facts.TryGetFacility(facilityId, out CampusWorldFacts.FacilityFact facility))
            {
                issues.Add(Issue(
                    CampusEcologyValidator.Severity.Error,
                    actorId,
                    CampusClassroomValidationTextCatalog.Format(
                        CampusClassroomValidationTextId.FacilityReferenceMissing,
                        fieldName,
                        facilityId)));
                return false;
            }

            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (facility.FacilityType == allowedTypes[i])
                {
                    return true;
                }
            }

            issues.Add(Issue(
                CampusEcologyValidator.Severity.Error,
                actorId,
                CampusClassroomValidationTextCatalog.Format(
                    CampusClassroomValidationTextId.FacilityReferenceWrongType,
                    fieldName,
                    facility.FacilityType,
                    string.Join("/", allowedTypes))));
            return false;
        }

        private static void ValidateFacilityInAssignedRoom(
            CampusWorldFacts facts,
            List<CampusEcologyValidator.ValidationIssue> issues,
            string actorId,
            string facilityId,
            string facilityFieldName,
            string classroomId,
            string classroomFieldName)
        {
            if (string.IsNullOrWhiteSpace(classroomId) ||
                !facts.TryGetFacility(facilityId, out CampusWorldFacts.FacilityFact facility))
            {
                return;
            }

            if (string.Equals(facility.RoomId, classroomId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            issues.Add(Issue(
                CampusEcologyValidator.Severity.Error,
                actorId,
                CampusClassroomValidationTextCatalog.Format(
                    CampusClassroomValidationTextId.FacilityOutsideAssignedRoom,
                    facilityFieldName,
                    string.IsNullOrWhiteSpace(facility.RoomId) ? "-" : facility.RoomId,
                    classroomFieldName,
                    classroomId)));
        }

        private static void AddOwner(Dictionary<string, List<string>> ownersById, string id, string actorId)
        {
            string normalizedId = CampusWorldFacts.NormalizeId(id);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return;
            }

            if (!ownersById.TryGetValue(normalizedId, out List<string> owners))
            {
                owners = new List<string>();
                ownersById.Add(normalizedId, owners);
            }

            owners.Add(actorId);
        }

        private static void ReportDuplicateOwners(
            List<CampusEcologyValidator.ValidationIssue> issues,
            Dictionary<string, List<string>> ownersById,
            CampusClassroomValidationTextId textId)
        {
            foreach (KeyValuePair<string, List<string>> pair in ownersById)
            {
                if (pair.Value == null || pair.Value.Count <= 1)
                {
                    continue;
                }

                issues.Add(Issue(
                    CampusEcologyValidator.Severity.Error,
                    pair.Key,
                    CampusClassroomValidationTextCatalog.Format(textId, string.Join(", ", pair.Value))));
            }
        }

        private static CampusEcologyValidator.ValidationIssue Issue(
            CampusEcologyValidator.Severity severity,
            string subjectId,
            string message)
        {
            return new CampusEcologyValidator.ValidationIssue(severity, subjectId, message);
        }
    }
}
