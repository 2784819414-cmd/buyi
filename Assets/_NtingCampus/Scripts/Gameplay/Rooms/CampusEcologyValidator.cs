using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Services;
using NtingCampusMapEditor;
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
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    string.Empty,
                    CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.WorldFactsMissing)));
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
                Debug.Log(CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.ValidationPassedLog));
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
                RequireRoom(
                    facts,
                    issues,
                    CampusRoomType.Classroom,
                    true,
                    CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.ClassroomRequired));
            }

            if (hasTeachers || hasStaff)
            {
                RequireRoom(
                    facts,
                    issues,
                    CampusRoomType.Office,
                    false,
                    CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.OfficeRecommended));
            }

            if (hasStudents)
            {
                RequireRoom(
                    facts,
                    issues,
                    CampusRoomType.Dormitory,
                    false,
                    CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.DormitoryMissing));
            }

            if (facts.CountRooms(CampusRoomType.CommonActivityZone) == 0 &&
                facts.CountRooms(CampusRoomType.Corridor) == 0)
            {
                issues.Add(new ValidationIssue(
                    Severity.Warning,
                    string.Empty,
                    CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.FreeMovementFallbackMissing)));
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
                        CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.FacilityTypeUnknown)));
                }

                ValidateFacilityTypeSource(facility, issues);

                if (string.IsNullOrWhiteSpace(facility.FacilityId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        facility.RoomId,
                        CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.FacilityMissingId)));
                    continue;
                }

                if (!facilityIds.Add(facility.FacilityId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        facility.FacilityId,
                        CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.FacilityDuplicateId)));
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
                        CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.ActorMissingId)));
                    continue;
                }

                if (!actorIds.Add(actor.ActorId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        actor.ActorId,
                        CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.ActorDuplicateId)));
                }
            }
        }

        private static void ValidateAssignments(CampusWorldFacts facts, List<ValidationIssue> issues)
        {
            Dictionary<string, List<string>> officeDeskOwners =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> studentDeskOwners =
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
                ValidateAssignedFacilityRoom(
                    facts,
                    issues,
                    actor.ActorId,
                    "StudentDeskId",
                    assignments.StudentDeskId,
                    assignments.StudentClassroomId);
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
                    "WorkFacilityId",
                    assignments.WorkFacilityId);
                ValidateServiceStationReference(
                    facts,
                    issues,
                    actor.ActorId,
                    "ServiceStationId",
                    assignments.ServiceStationId);

                if (actor.Role == CampusCharacterRole.Student && !string.IsNullOrWhiteSpace(assignments.StudentDeskId))
                {
                    AddOwner(studentDeskOwners, assignments.StudentDeskId, actor.ActorId);
                }

                if (actor.Role == CampusCharacterRole.Teacher && !string.IsNullOrWhiteSpace(assignments.OfficeDeskId))
                {
                    AddOwner(officeDeskOwners, assignments.OfficeDeskId, actor.ActorId);
                }

                if (actor.Role == CampusCharacterRole.Staff &&
                    (actor.StaffDuty & CampusStaffDuty.SupportStaff) != 0 &&
                    !string.IsNullOrWhiteSpace(assignments.ServiceStationId))
                {
                    AddOwner(serviceWindowOwners, assignments.ServiceStationId, actor.ActorId);
                }
            }

            ReportDuplicateOwners(
                issues,
                studentDeskOwners,
                CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.StudentDeskDuplicateOwners));
            ReportDuplicateOwners(
                issues,
                officeDeskOwners,
                CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.OfficeDeskDuplicateOwners));
            ReportDuplicateOwners(
                issues,
                serviceWindowOwners,
                CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.ServiceStationDuplicateOwners));
        }

        private static void ValidateSupportStaffStations(CampusWorldFacts facts, List<ValidationIssue> issues)
        {
            int operationalStationCount = 0;
            for (int i = 0; i < facts.ServiceStations.Count; i++)
            {
                CampusWorldFacts.ServiceStationFact station = facts.ServiceStations[i];
                if (station == null)
                {
                    continue;
                }

                if (ValidateServiceStation(facts, issues, station))
                {
                    operationalStationCount++;
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
                    CampusEcologyValidationTextCatalog.Get(CampusEcologyValidationTextId.SupportStaffExceedsStations)));
            }
        }

        private static bool ValidateServiceStation(
            CampusWorldFacts facts,
            List<ValidationIssue> issues,
            CampusWorldFacts.ServiceStationFact station)
        {
            string subjectId = !string.IsNullOrWhiteSpace(station.StationId)
                ? station.StationId
                : station.OwnerFacilityId;
            bool valid = true;

            if (string.IsNullOrWhiteSpace(station.StationId))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Get(CampusServiceStationValidationTextId.MissingId)));
                valid = false;
            }

            if (!CampusServiceStationPresetCatalog.TryResolve(
                    station.StationTypeId,
                    out CampusServiceStationTypeDefinition definition))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.UnknownType,
                        station.StationTypeId)));
                return false;
            }

            if (!facts.TryGetRoom(station.RoomId, out CampusWorldFacts.RoomFact room))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.MissingRoom,
                        station.RoomId)));
                valid = false;
            }
            else if (!definition.AcceptsRoomType(room.RoomType))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.InvalidRoomType,
                        station.StationTypeId,
                        room.RoomType)));
                valid = false;
            }

            if (!facts.TryGetFacility(station.OwnerFacilityId, out CampusWorldFacts.FacilityFact ownerFacility))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.MissingOwner,
                        station.OwnerFacilityId)));
                valid = false;
            }
            else
            {
                if (!definition.AcceptsOwnerFacilityType(ownerFacility.FacilityType))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        subjectId,
                        CampusServiceStationValidationTextCatalog.Format(
                            CampusServiceStationValidationTextId.InvalidOwnerType,
                            ownerFacility.FacilityType,
                            station.StationTypeId)));
                    valid = false;
                }

                if (!string.Equals(ownerFacility.RoomId, station.RoomId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        subjectId,
                        CampusServiceStationValidationTextCatalog.Get(
                            CampusServiceStationValidationTextId.OwnerRoomMismatch)));
                    valid = false;
                }

                if (!ValidateServiceStationOwnerInteraction(
                        issues,
                        definition,
                        ownerFacility,
                        subjectId))
                {
                    valid = false;
                }
            }

            for (int slotIndex = 0; slotIndex < definition.Slots.Count; slotIndex++)
            {
                CampusServiceStationSlotDefinition slotDefinition = definition.Slots[slotIndex];
                int count = CountSlotFacilities(facts, issues, station, slotDefinition, subjectId);
                if (count < slotDefinition.MinCount)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        subjectId,
                        CampusServiceStationValidationTextCatalog.Format(
                            CampusServiceStationValidationTextId.SlotBelowMinimum,
                            slotDefinition.RoleId,
                            slotDefinition.MinCount)));
                    valid = false;
                }

                if (count > slotDefinition.MaxCount)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        subjectId,
                        CampusServiceStationValidationTextCatalog.Format(
                            CampusServiceStationValidationTextId.SlotAboveMaximum,
                            slotDefinition.RoleId,
                            slotDefinition.MaxCount)));
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidateServiceStationOwnerInteraction(
            List<ValidationIssue> issues,
            CampusServiceStationTypeDefinition definition,
            CampusWorldFacts.FacilityFact ownerFacility,
            string subjectId)
        {
            string actionId = CampusInteractionActionIds.Normalize(
                definition != null ? definition.InteractionActionId : string.Empty);
            if (string.IsNullOrEmpty(actionId))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.MissingInteractionAction,
                        definition != null ? definition.StationTypeId : string.Empty)));
                return false;
            }

            if (ownerFacility == null || !ownerFacility.HasPlacedObject)
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Get(
                        CampusServiceStationValidationTextId.OwnerMissingPlacedObject)));
                return false;
            }

            if (string.IsNullOrWhiteSpace(ownerFacility.InteractionPresetEid))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Get(
                        CampusServiceStationValidationTextId.OwnerMissingInteractionPreset)));
                return false;
            }

            if (!CampusObjectInteractionPresetCatalog.Current.TryGetPreset(
                    ownerFacility.InteractionPresetEid,
                    out CampusObjectInteractionPreset preset))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.OwnerUnknownInteractionPreset,
                        ownerFacility.InteractionPresetEid)));
                return false;
            }

            if (!PresetContainsAction(preset, actionId))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    subjectId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.OwnerInteractionActionMissing,
                        actionId)));
                return false;
            }

            return true;
        }

        private static bool PresetContainsAction(CampusObjectInteractionPreset preset, string actionId)
        {
            if (preset == null ||
                preset.Anchors == null ||
                string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            for (int i = 0; i < preset.Anchors.Count; i++)
            {
                CampusPlacedObjectInteractionAnchor anchor = preset.Anchors[i];
                if (anchor != null &&
                    anchor.Enabled &&
                    CampusInteractionActionIds.Equals(anchor.ActionId, actionId))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountSlotFacilities(
            CampusWorldFacts facts,
            List<ValidationIssue> issues,
            CampusWorldFacts.ServiceStationFact station,
            CampusServiceStationSlotDefinition slotDefinition,
            string subjectId)
        {
            int count = 0;
            if (station.Slots == null || slotDefinition == null)
            {
                return count;
            }

            for (int i = 0; i < station.Slots.Count; i++)
            {
                CampusWorldFacts.ServiceStationSlotFact slot = station.Slots[i];
                if (slot == null ||
                    !string.Equals(slot.RoleId, slotDefinition.RoleId, StringComparison.OrdinalIgnoreCase) ||
                    slot.FacilityIds == null)
                {
                    continue;
                }

                for (int facilityIndex = 0; facilityIndex < slot.FacilityIds.Count; facilityIndex++)
                {
                    string facilityId = slot.FacilityIds[facilityIndex];
                    if (!facts.TryGetFacility(facilityId, out CampusWorldFacts.FacilityFact facility))
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Error,
                            subjectId,
                            CampusServiceStationValidationTextCatalog.Format(
                                CampusServiceStationValidationTextId.SlotMissingFacility,
                                slotDefinition.RoleId,
                                facilityId)));
                        continue;
                    }

                    if (!slotDefinition.Accepts(facility.FacilityType))
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Error,
                            subjectId,
                            CampusServiceStationValidationTextCatalog.Format(
                                CampusServiceStationValidationTextId.SlotInvalidFacilityType,
                                slotDefinition.RoleId,
                                facility.FacilityType)));
                        continue;
                    }

                    if (!string.Equals(facility.RoomId, station.RoomId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Error,
                            subjectId,
                            CampusServiceStationValidationTextCatalog.Format(
                                CampusServiceStationValidationTextId.SlotRoomMismatch,
                                slotDefinition.RoleId)));
                        continue;
                    }

                    count++;
                }
            }

            return count;
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
                    CampusEcologyValidationTextCatalog.Format(
                        CampusEcologyValidationTextId.RoomReferenceMissing,
                        fieldName,
                        roomId)));
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
                    CampusEcologyValidationTextCatalog.Format(
                        CampusEcologyValidationTextId.FacilityReferenceMissing,
                        fieldName,
                        facilityId)));
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
                CampusEcologyValidationTextCatalog.Format(
                    CampusEcologyValidationTextId.FacilityReferenceWrongType,
                    fieldName,
                    facility.FacilityType,
                    string.Join("/", allowedTypes))));
        }

        private static void ValidateAssignedFacilityRoom(
            CampusWorldFacts facts,
            List<ValidationIssue> issues,
            string actorId,
            string fieldName,
            string facilityId,
            string roomId)
        {
            if (string.IsNullOrWhiteSpace(facilityId) ||
                string.IsNullOrWhiteSpace(roomId) ||
                !facts.TryGetFacility(facilityId, out CampusWorldFacts.FacilityFact facility) ||
                !facts.TryGetRoom(roomId, out _))
            {
                return;
            }

            if (string.Equals(facility.RoomId, roomId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            issues.Add(new ValidationIssue(
                Severity.Error,
                actorId,
                CampusEcologyValidationTextCatalog.Format(
                    CampusEcologyValidationTextId.StudentDeskRoomMismatch,
                    fieldName,
                    facilityId,
                    facility.RoomId)));
        }

        private static void ValidateServiceStationReference(
            CampusWorldFacts facts,
            List<ValidationIssue> issues,
            string actorId,
            string fieldName,
            string stationId)
        {
            if (string.IsNullOrWhiteSpace(stationId))
            {
                return;
            }

            if (!facts.TryGetServiceStation(stationId, out _))
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    actorId,
                    CampusServiceStationValidationTextCatalog.Format(
                        CampusServiceStationValidationTextId.ReferenceMissing,
                        fieldName,
                        stationId)));
            }
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
                    CampusEcologyValidationTextCatalog.Format(
                        CampusEcologyValidationTextId.DuplicateOwners,
                        message,
                        string.Join(", ", pair.Value))));
            }
        }
    }
}
