using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public static class CampusRoomValidator
    {
        public enum Severity
        {
            Info = 0,
            Warning = 1,
            Error = 2
        }

        public readonly struct ValidationIssue
        {
            public ValidationIssue(Severity severity, string roomId, string message)
            {
                SeverityLevel = severity;
                RoomId = roomId ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public Severity SeverityLevel { get; }
            public string RoomId { get; }
            public string Message { get; }
        }

        public readonly struct ValidationSummary
        {
            public ValidationSummary(bool isValid, bool isUsableForGameplay, string message)
            {
                IsValid = isValid;
                IsUsableForGameplay = isUsableForGameplay;
                Message = message ?? string.Empty;
            }

            public bool IsValid { get; }
            public bool IsUsableForGameplay { get; }
            public string Message { get; }
        }

        public static List<ValidationIssue> Validate(IReadOnlyList<CampusGameplayRoom> rooms)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            if (rooms == null)
            {
                issues.Add(new ValidationIssue(
                    Severity.Error,
                    string.Empty,
                    CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.RegistryNull)));
                return issues;
            }

            HashSet<string> roomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        string.Empty,
                        CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.NullRoomEntry)));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(room.RoomId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        string.Empty,
                        CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.MissingRoomId)));
                }
                else if (!roomIds.Add(room.RoomId))
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        room.RoomId,
                        CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.DuplicateRoomId)));
                }

                if (!room.HasDisplayName)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        room.RoomId,
                        CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.MissingDisplayName)));
                }

                if (room.MarkerCount <= 0)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        room.RoomId,
                        CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.MissingMarkerCells)));
                }

                if (room.MarkerBounds.size.x <= 0 || room.MarkerBounds.size.y <= 0)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Error,
                        room.RoomId,
                        CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.InvalidBounds)));
                }

                if (room.RoomType == CampusRoomType.Unknown)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        room.RoomId,
                        CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.UnknownRoomType)));
                }
                else if (!room.HasExplicitRoomType)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        room.RoomId,
                        CampusRoomValidationTextCatalog.Format(
                            CampusRoomValidationTextId.LegacyRoomTypeInference,
                            room.RoomType)));
                }

                if (room.RoomTypeSource == CampusRoomTypeSource.Unknown)
                {
                    issues.Add(new ValidationIssue(
                        Severity.Warning,
                        room.RoomId,
                        CampusRoomValidationTextCatalog.Get(
                            CampusRoomValidationTextId.UnknownRoomTypeSource)));
                }

                foreach (CampusFacilityType requiredFacility in GetRequiredFacilities(room.RoomType))
                {
                    if (room.GetFacilityCount(requiredFacility) <= 0)
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Warning,
                            room.RoomId,
                            CampusRoomValidationTextCatalog.Format(
                                CampusRoomValidationTextId.MissingCoreFacility,
                                requiredFacility)));
                    }
                }
            }

            return issues;
        }

        public static ValidationSummary Summarize(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return new ValidationSummary(
                    false,
                    false,
                    CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.RoomNull));
            }

            if (room.RoomType == CampusRoomType.Unknown)
            {
                return new ValidationSummary(
                    false,
                    false,
                    CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.MissingFormalRoomType));
            }

            CampusFacilityType[] requiredFacilities = GetRequiredFacilities(room.RoomType);
            for (int i = 0; i < requiredFacilities.Length; i++)
            {
                CampusFacilityType requiredFacility = requiredFacilities[i];
                if (room.GetFacilityCount(requiredFacility) <= 0)
                {
                    return new ValidationSummary(
                        false,
                        false,
                        CampusRoomValidationTextCatalog.Format(
                            CampusRoomValidationTextId.MissingCoreFacility,
                            requiredFacility));
                }
            }

            return new ValidationSummary(
                true,
                true,
                CampusRoomValidationTextCatalog.Get(CampusRoomValidationTextId.ReadyForGameplay));
        }

        public static void LogIssues(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                string prefix = string.IsNullOrWhiteSpace(issue.RoomId) ? "[Rooms]" : "[Rooms][" + issue.RoomId + "]";
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

        public static CampusFacilityType[] GetRequiredFacilities(CampusRoomType roomType)
        {
            switch (roomType)
            {
                case CampusRoomType.Classroom:
                    return new[] { CampusFacilityType.Blackboard, CampusFacilityType.StudentDesk, CampusFacilityType.Podium };
                case CampusRoomType.Dormitory:
                    return new[] { CampusFacilityType.Bed };
                case CampusRoomType.Office:
                    return new[] { CampusFacilityType.OfficeDesk };
                case CampusRoomType.CommonActivityZone:
                    return new[] { CampusFacilityType.BulletinBoard };
                case CampusRoomType.HumanResources:
                    return new[] { CampusFacilityType.Recruitment };
                default:
                    return Array.Empty<CampusFacilityType>();
            }
        }
    }
}
