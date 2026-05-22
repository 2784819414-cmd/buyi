using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public static class CampusEcologyValidator
    {
        public enum Severity
        {
            Info = 0,
            Warning = 1,
            Error = 2
        }

        public readonly struct ValidationIssue
        {
            public ValidationIssue(Severity severity, string subjectId, string message)
            {
                SeverityLevel = severity;
                SubjectId = subjectId ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public Severity SeverityLevel { get; }
            public string SubjectId { get; }
            public string Message { get; }
        }

        public static List<ValidationIssue> Validate(CampusWorldFacts facts)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            if (facts == null)
            {
                issues.Add(new ValidationIssue(Severity.Error, string.Empty, "World facts are missing."));
                return issues;
            }

            ValidateRooms(facts, issues);
            ValidateFacilities(facts, issues);
            ValidateActors(facts, issues);
            ValidateAssignments(facts, issues);
            return issues;
        }

        public static void LogIssues(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                Debug.Log("[Ecology] Validation passed.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                string prefix = string.IsNullOrWhiteSpace(issue.SubjectId)
                    ? "[Ecology]"
                    : "[Ecology][" + issue.SubjectId + "]";
                switch (issue.SeverityLevel)
                {
                    case Severity.Error:
                        Debug.LogError(prefix + " " + issue.Message);
                        break;
                    case Severity.Warning:
                        Debug.LogWarning(prefix + " " + issue.Message);
                        break;
                    default:
                        Debug.Log(prefix + " " + issue.Message);
                        break;
                }
            }
        }

        private static void ValidateRooms(CampusWorldFacts facts, List<ValidationIssue> issues)
        {
            bool hasStudents = facts.CountActors(CampusCharacterRole.Student) > 0;
            bool hasTeachers = facts.CountActors(CampusCharacterRole.Teacher) > 0;
            bool hasStaff = facts.CountActors(CampusCharacterRole.Staff) > 0;

            if (hasStudents || hasTeachers)
            {
                RequireRoom(facts, issues, CampusRoomType.Classroom, true, "Characters with class duties need at least one classroom.");
            }

            if (hasTeachers || hasStaff)
            {
                RequireRoom(facts, issues, CampusRoomType.Office, false, "Teachers and staff work better with at least one office room.");
            }

            if (hasStudents)
            {
                RequireRoom(facts, issues, CampusRoomType.Dormitory, false, "Students have no dormitory fallback room.");
            }

            if (facts.CountRooms(CampusRoomType.CommonActivityZone) == 0 &&
                facts.CountRooms(CampusRoomType.Corridor) == 0)
            {
                issues.Add(new ValidationIssue(
                    Severity.Warning,
                    string.Empty,
                    "NPC free movement needs a CommonActivityZone or Corridor fallback."));
            }
        }

        private static void ValidateFacilities(CampusWorldFacts facts, List<ValidationIssue> issues)
        {
            HashSet<string> facilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < facts.Facilities.Count; i++)
            {
                CampusWorldFacts.FacilityFact facility = facts.Facilities[i];
                if (facility == null)
                {
                    continue;
                }

                if (facility.FacilityType == CampusFacilityType.Unknown)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        facility.FacilityId,
                        "Facility type is Unknown and cannot be targeted reliably."));
                }

                ValidateFacilityTypeSource(facility, issues);

                if (string.IsNullOrWhiteSpace(facility.FacilityId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        facility.RoomId,
                        "Facility is missing a stable facility id."));
                    continue;
                }

                if (!facilityIds.Add(facility.FacilityId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        facility.FacilityId,
                        "Duplicate facility id. Actor bindings may resolve to the wrong target."));
                }
            }

            ValidateSupportStaffStations(facts, issues);
        }

        private static void ValidateFacilityTypeSource(
            CampusWorldFacts.FacilityFact facility,
            List<ValidationIssue> issues)
        {
            if (facility == null || facility.HasExplicitFacilityType)
            {
                return;
            }

            string subjectId = string.IsNullOrWhiteSpace(facility.FacilityId)
                ? facility.RoomId
                : facility.FacilityId;
            string diagnostic = string.IsNullOrWhiteSpace(facility.FacilityTypeDiagnostic)
                ? string.Empty
                : facility.FacilityTypeDiagnostic;

            switch (facility.FacilityTypeSource)
            {
                case CampusFacilityTypeSource.LegacyInference:
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        subjectId,
                        CampusFacilityValidationTextCatalog.Format(
                            CampusFacilityValidationTextId.LegacyInference,
                            facility.FacilityType,
                            diagnostic)));
                    break;
                case CampusFacilityTypeSource.StorageFallback:
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        subjectId,
                        CampusFacilityValidationTextCatalog.Format(
                            CampusFacilityValidationTextId.StorageFallback,
                            diagnostic)));
                    break;
                case CampusFacilityTypeSource.MissingTypeId:
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        subjectId,
                        CampusFacilityValidationTextCatalog.Format(
                            CampusFacilityValidationTextId.MissingTypeId,
                            diagnostic)));
                    break;
                case CampusFacilityTypeSource.UnknownTypeId:
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        subjectId,
                        CampusFacilityValidationTextCatalog.Format(
                            CampusFacilityValidationTextId.UnknownTypeId,
                            diagnostic)));
                    break;
                case CampusFacilityTypeSource.Unknown:
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        subjectId,
                        CampusFacilityValidationTextCatalog.Get(
                            CampusFacilityValidationTextId.UnknownTypeSource)));
                    break;
            }
        }

        private static void ValidateActors(CampusWorldFacts facts, List<ValidationIssue> issues)
        {
            HashSet<string> actorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < facts.Actors.Count; i++)
            {
                CampusWorldFacts.ActorFact actor = facts.Actors[i];
                if (actor == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(actor.ActorId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        string.Empty,
                        "Actor is missing a stable actor id."));
                    continue;
                }

                if (!actorIds.Add(actor.ActorId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        actor.ActorId,
                        "Duplicate actor id."));
                }
            }
        }

        private static void ValidateAssignments(CampusWorldFacts facts, List<ValidationIssue> issues)
        {
            Dictionary<string, List<string>> officeDeskOwners =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> serviceWindowOwners =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < facts.Actors.Count; i++)
            {
                CampusWorldFacts.ActorFact actor = facts.Actors[i];
                CampusCharacterAssignmentData assignments = actor != null ? actor.Assignments : null;
                if (actor == null || assignments == null)
                {
                    continue;
                }

                ValidateRoomReference(facts, issues, actor.ActorId, "StudentClassroomId", assignments.StudentClassroomId);
                ValidateRoomReference(facts, issues, actor.ActorId, "TeacherClassroomId", assignments.TeacherClassroomId);
                ValidateRoomReference(facts, issues, actor.ActorId, "OfficeRoomId", assignments.OfficeRoomId);
                ValidateRoomReference(facts, issues, actor.ActorId, "WorkRoomId", assignments.WorkRoomId);

                ValidateFacilityReference(
                    facts,
                    issues,
                    actor.ActorId,
                    "StudentDeskId",
                    assignments.StudentDeskId,
                    CampusFacilityType.StudentDesk);
                ValidateFacilityReference(
                    facts,
                    issues,
                    actor.ActorId,
                    "TeacherPodiumId",
                    assignments.TeacherPodiumId,
                    CampusFacilityType.Podium,
                    CampusFacilityType.Blackboard);
                ValidateFacilityReference(
                    facts,
                    issues,
                    actor.ActorId,
                    "OfficeDeskId",
                    assignments.OfficeDeskId,
                    CampusFacilityType.OfficeDesk,
                    CampusFacilityType.Desk);
                ValidateFacilityReference(
                    facts,
                    issues,
                    actor.ActorId,
                    "ServiceWindowId",
                    assignments.ServiceWindowId,
                    CampusFacilityType.ServiceWindow);
                ValidateFacilityReference(
                    facts,
                    issues,
                    actor.ActorId,
                    "PrimaryWorkstationId",
                    assignments.PrimaryWorkstationId);

                if (actor.Role == CampusCharacterRole.Teacher && !string.IsNullOrWhiteSpace(assignments.OfficeDeskId))
                {
                    AddOwner(officeDeskOwners, assignments.OfficeDeskId, actor.ActorId);
                }

                if (actor.Role == CampusCharacterRole.Staff &&
                    (actor.StaffDuty & CampusStaffDuty.SupportStaff) != 0 &&
                    !string.IsNullOrWhiteSpace(assignments.ServiceWindowId))
                {
                    AddOwner(serviceWindowOwners, assignments.ServiceWindowId, actor.ActorId);
                }
            }

            ReportDuplicateOwners(issues, officeDeskOwners, "OfficeDeskId is assigned to multiple teachers.");
            ReportDuplicateOwners(issues, serviceWindowOwners, "ServiceWindowId is assigned to multiple support staff.");
        }

        private static void ValidateSupportStaffStations(CampusWorldFacts facts, List<ValidationIssue> issues)
        {
            Dictionary<string, Dictionary<string, ServiceStationTopology>> stationsByRoom =
                new Dictionary<string, Dictionary<string, ServiceStationTopology>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Dictionary<string, string>> ownerIdByLegacyStationIdByRoom =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < facts.Facilities.Count; i++)
            {
                CampusWorldFacts.FacilityFact facility = facts.Facilities[i];
                if (facility == null ||
                    facility.RoomType != CampusRoomType.ServiceArea ||
                    facility.FacilityType != CampusFacilityType.ServiceWindow)
                {
                    continue;
                }

                string roomId = CampusWorldFacts.NormalizeId(facility.RoomId);
                string ownerFacilityId = CampusWorldFacts.NormalizeId(facility.FacilityId);
                if (string.IsNullOrEmpty(ownerFacilityId))
                {
                    continue;
                }

                ServiceStationTopology topology = GetOrCreateStationTopology(stationsByRoom, roomId, ownerFacilityId);
                topology.Record(facility.FacilityType);
                stationsByRoom[roomId][ownerFacilityId] = topology;

                string legacyStationId =
                    CampusGameplayFacilityMarker.NormalizeLegacyServiceStationId(facility.ServiceStationId);
                if (!string.IsNullOrEmpty(legacyStationId))
                {
                    if (!ownerIdByLegacyStationIdByRoom.TryGetValue(roomId, out Dictionary<string, string> ownersByLegacyId))
                    {
                        ownersByLegacyId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        ownerIdByLegacyStationIdByRoom[roomId] = ownersByLegacyId;
                    }

                    ownersByLegacyId[legacyStationId] = ownerFacilityId;
                }
            }

            for (int i = 0; i < facts.Facilities.Count; i++)
            {
                CampusWorldFacts.FacilityFact facility = facts.Facilities[i];
                if (facility == null ||
                    facility.RoomType != CampusRoomType.ServiceArea ||
                    !IsServiceStationFacility(facility.FacilityType) ||
                    facility.FacilityType == CampusFacilityType.ServiceWindow)
                {
                    continue;
                }

                string roomId = CampusWorldFacts.NormalizeId(facility.RoomId);
                string ownerFacilityId = CampusWorldFacts.NormalizeId(facility.OwnerFacilityId);
                if (string.IsNullOrEmpty(ownerFacilityId) &&
                    ownerIdByLegacyStationIdByRoom.TryGetValue(roomId, out Dictionary<string, string> ownersByLegacyId))
                {
                    string legacyStationId =
                        CampusGameplayFacilityMarker.NormalizeLegacyServiceStationId(facility.ServiceStationId);
                    if (!string.IsNullOrEmpty(legacyStationId) &&
                        ownersByLegacyId.TryGetValue(legacyStationId, out string migratedOwnerFacilityId))
                    {
                        ownerFacilityId = migratedOwnerFacilityId;
                    }
                }

                if (string.IsNullOrEmpty(ownerFacilityId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        string.IsNullOrWhiteSpace(facility.FacilityId) ? roomId : facility.FacilityId,
                        "ServiceArea support points must declare OwnerFacilityId that references a ServiceWindow."));
                    continue;
                }

                if (!facts.TryGetFacility(ownerFacilityId, out CampusWorldFacts.FacilityFact ownerFacility))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        string.IsNullOrWhiteSpace(facility.FacilityId) ? ownerFacilityId : facility.FacilityId,
                        "OwnerFacilityId references a missing facility: " + ownerFacilityId + "."));
                    continue;
                }

                if (ownerFacility.FacilityType != CampusFacilityType.ServiceWindow)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        string.IsNullOrWhiteSpace(facility.FacilityId) ? ownerFacilityId : facility.FacilityId,
                        "OwnerFacilityId must reference a ServiceWindow."));
                    continue;
                }

                if (!string.Equals(ownerFacility.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        string.IsNullOrWhiteSpace(facility.FacilityId) ? ownerFacilityId : facility.FacilityId,
                        "OwnerFacilityId must reference a ServiceWindow in the same room."));
                    continue;
                }

                ServiceStationTopology topology = GetOrCreateStationTopology(stationsByRoom, roomId, ownerFacilityId);
                topology.Record(facility.FacilityType);
                stationsByRoom[roomId][ownerFacilityId] = topology;
            }

            int operationalStationCount = 0;
            foreach (CampusWorldFacts.RoomFact room in facts.Rooms)
            {
                if (room == null || room.RoomType != CampusRoomType.ServiceArea)
                {
                    continue;
                }

                string roomId = CampusWorldFacts.NormalizeId(room.RoomId);
                if (!stationsByRoom.TryGetValue(roomId, out Dictionary<string, ServiceStationTopology> stations))
                {
                    continue;
                }

                foreach (KeyValuePair<string, ServiceStationTopology> pair in stations)
                {
                    string ownerFacilityId = pair.Key;
                    ServiceStationTopology topology = pair.Value;
                    if (topology.ServiceWindowCount != 1)
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Error,
                            ownerFacilityId,
                            "Each service window station needs exactly one ServiceWindow owner."));
                    }

                    if (topology.WorkerStandCount != 1)
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Error,
                            ownerFacilityId,
                            "Each service window station needs exactly one WorkerStandPoint."));
                    }

                    if (topology.PickupPointCount != 1)
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Warning,
                            ownerFacilityId,
                            "Each service window station should expose exactly one PickupPoint for customer targeting."));
                    }

                    if (topology.ServiceWindowCount == 1 && topology.WorkerStandCount == 1)
                    {
                        operationalStationCount++;
                    }
                }
            }

            int supportStaffCount = 0;
            for (int i = 0; i < facts.Actors.Count; i++)
            {
                CampusWorldFacts.ActorFact actor = facts.Actors[i];
                if (actor != null &&
                    actor.Role == CampusCharacterRole.Staff &&
                    (actor.StaffDuty & CampusStaffDuty.SupportStaff) != 0)
                {
                    supportStaffCount++;
                }
            }

            if (supportStaffCount > operationalStationCount)
            {
                issues.Add(new ValidationIssue(
                    Severity.Warning,
                    CampusRoomType.ServiceArea.ToString(),
                    "SupportStaff count exceeds operational service station count. Extra support staff will not receive a meal-duty station."));
            }
        }

        private static bool IsServiceStationFacility(CampusFacilityType facilityType)
        {
            switch (facilityType)
            {
                case CampusFacilityType.ServiceWindow:
                case CampusFacilityType.WorkerStandPoint:
                case CampusFacilityType.PickupPoint:
                case CampusFacilityType.WaitingPoint:
                case CampusFacilityType.DropPoint:
                    return true;
                default:
                    return false;
            }
        }

        private static ServiceStationTopology GetOrCreateStationTopology(
            Dictionary<string, Dictionary<string, ServiceStationTopology>> stationsByRoom,
            string roomId,
            string ownerFacilityId)
        {
            if (!stationsByRoom.TryGetValue(roomId, out Dictionary<string, ServiceStationTopology> stations))
            {
                stations = new Dictionary<string, ServiceStationTopology>(StringComparer.OrdinalIgnoreCase);
                stationsByRoom[roomId] = stations;
            }

            if (!stations.TryGetValue(ownerFacilityId, out ServiceStationTopology topology))
            {
                topology = new ServiceStationTopology();
            }

            return topology;
        }

        private static void ValidateRoomReference(
            CampusWorldFacts facts,
            List<ValidationIssue> issues,
            string actorId,
            string fieldName,
            string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            if (!facts.TryGetRoom(roomId, out _))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    actorId,
                    fieldName + " references a missing room: " + roomId + "."));
            }
        }

        private static void ValidateFacilityReference(
            CampusWorldFacts facts,
            List<ValidationIssue> issues,
            string actorId,
            string fieldName,
            string facilityId,
            params CampusFacilityType[] allowedTypes)
        {
            if (string.IsNullOrWhiteSpace(facilityId))
            {
                return;
            }

            if (!facts.TryGetFacility(facilityId, out CampusWorldFacts.FacilityFact facility))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    actorId,
                    fieldName + " references a missing facility: " + facilityId + "."));
                return;
            }

            if (allowedTypes == null || allowedTypes.Length == 0)
            {
                return;
            }

            for (int i = 0; i < allowedTypes.Length; i++)
            {
                if (facility.FacilityType == allowedTypes[i])
                {
                    return;
                }
            }

            issues.Add(new ValidationIssue(
                Severity.Error,
                actorId,
                fieldName + " points to " + facility.FacilityType + ", expected " + string.Join("/", allowedTypes) + "."));
        }

        private static void RequireRoom(
            CampusWorldFacts facts,
            List<ValidationIssue> issues,
            CampusRoomType roomType,
            bool error,
            string message)
        {
            if (facts.CountRooms(roomType) > 0)
            {
                return;
            }

            issues.Add(new ValidationIssue(
                error ? Severity.Error : Severity.Warning,
                roomType.ToString(),
                message));
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

        private static void AddCount(Dictionary<string, int> countsById, string id)
        {
            string normalizedId = CampusWorldFacts.NormalizeId(id);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return;
            }

            countsById.TryGetValue(normalizedId, out int count);
            countsById[normalizedId] = count + 1;
        }

        private static int GetCount(Dictionary<string, int> countsById, string id)
        {
            string normalizedId = CampusWorldFacts.NormalizeId(id);
            return countsById.TryGetValue(normalizedId, out int count)
                ? count
                : 0;
        }

        private struct ServiceStationTopology
        {
            public int ServiceWindowCount;
            public int WorkerStandCount;
            public int PickupPointCount;

            public void Record(CampusFacilityType facilityType)
            {
                switch (facilityType)
                {
                    case CampusFacilityType.ServiceWindow:
                        ServiceWindowCount++;
                        break;
                    case CampusFacilityType.WorkerStandPoint:
                        WorkerStandCount++;
                        break;
                    case CampusFacilityType.PickupPoint:
                        PickupPointCount++;
                        break;
                }
            }
        }

        private static void ReportDuplicateOwners(
            List<ValidationIssue> issues,
            Dictionary<string, List<string>> ownersById,
            string message)
        {
            foreach (KeyValuePair<string, List<string>> pair in ownersById)
            {
                if (pair.Value == null || pair.Value.Count <= 1)
                {
                    continue;
                }

                issues.Add(new ValidationIssue(
                    Severity.Error,
                    pair.Key,
                    message + " Owners=" + string.Join(", ", pair.Value) + "."));
            }
        }
    }
}
