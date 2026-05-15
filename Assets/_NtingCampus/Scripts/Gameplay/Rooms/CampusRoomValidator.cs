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
                issues.Add(new ValidationIssue(Severity.Error, string.Empty, "Room registry is null."));
                return issues;
            }

            HashSet<string> roomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rooms.Count; i++)
            {
                CampusGameplayRoom room = rooms[i];
                if (room == null)
                {
                    issues.Add(new ValidationIssue(Severity.Error, string.Empty, "Null room entry found."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(room.RoomId))
                {
                    issues.Add(new ValidationIssue(Severity.Error, string.Empty, "Room is missing a room id."));
                }
                else if (!roomIds.Add(room.RoomId))
                {
                    issues.Add(new ValidationIssue(Severity.Error, room.RoomId, "Duplicate room id."));
                }

                if (string.IsNullOrWhiteSpace(room.SourceRoomName))
                {
                    issues.Add(new ValidationIssue(Severity.Warning, room.RoomId, "Room marker name is empty."));
                }

                if (room.MarkerCount <= 0)
                {
                    issues.Add(new ValidationIssue(Severity.Error, room.RoomId, "Room has no marker cells."));
                }

                if (room.MarkerBounds.size.x <= 0 || room.MarkerBounds.size.y <= 0)
                {
                    issues.Add(new ValidationIssue(Severity.Error, room.RoomId, "Room bounds are invalid."));
                }

                if (room.RoomType == CampusRoomType.Unknown)
                {
                    issues.Add(new ValidationIssue(Severity.Warning, room.RoomId, "Room type is still Unknown."));
                }

                foreach (CampusFacilityType requiredFacility in GetRequiredFacilities(room.RoomType))
                {
                    if (room.GetFacilityCount(requiredFacility) <= 0)
                    {
                        issues.Add(new ValidationIssue(
                            Severity.Warning,
                            room.RoomId,
                            "Missing core facility: " + requiredFacility + "."));
                    }
                }
            }

            return issues;
        }

        public static ValidationSummary Summarize(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return new ValidationSummary(false, false, "Room is null.");
            }

            if (room.RoomType == CampusRoomType.Unknown)
            {
                return new ValidationSummary(false, false, "Missing formal room type.");
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
                        "Missing core facility: " + requiredFacility + ".");
                }
            }

            return new ValidationSummary(true, true, "Ready for gameplay.");
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
