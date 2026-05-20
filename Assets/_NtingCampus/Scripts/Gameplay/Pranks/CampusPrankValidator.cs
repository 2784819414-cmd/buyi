using System;
using System.Collections.Generic;
using UnityEngine;
using ValidationIssue = NtingCampus.Gameplay.Rooms.CampusEcologyValidator.ValidationIssue;
using ValidationSeverity = NtingCampus.Gameplay.Rooms.CampusEcologyValidator.Severity;

namespace NtingCampus.Gameplay.Pranks
{
    public static class CampusPrankValidator
    {
        public static List<ValidationIssue> Validate(IReadOnlyList<CampusPrankTarget> targets)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            ValidateCatalog(issues);
            ValidateTargets(targets, issues);
            return issues;
        }

        public static void LogIssues(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                Debug.Log("[Prank] Validation passed.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                string prefix = string.IsNullOrWhiteSpace(issue.SubjectId)
                    ? "[Prank]"
                    : "[Prank][" + issue.SubjectId + "]";
                switch (issue.SeverityLevel)
                {
                    case ValidationSeverity.Error:
                        Debug.LogError(prefix + " " + issue.Message);
                        break;
                    case ValidationSeverity.Warning:
                        Debug.LogWarning(prefix + " " + issue.Message);
                        break;
                    default:
                        Debug.Log(prefix + " " + issue.Message);
                        break;
                }
            }
        }

        private static void ValidateCatalog(List<ValidationIssue> issues)
        {
            if (CampusPrankCatalog.Count == 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, "catalog", "Prank catalog has no definitions."));
                return;
            }

            HashSet<string> objectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> payloads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < CampusPrankCatalog.Count; i++)
            {
                CampusPrankDefinition definition = CampusPrankCatalog.GetAt(i);
                string subjectId = string.IsNullOrWhiteSpace(definition.Payload)
                    ? "catalog[" + i + "]"
                    : definition.Payload;

                if (string.IsNullOrWhiteSpace(definition.ObjectId))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Prank definition needs a stable ObjectId."));
                }
                else if (!objectIds.Add(definition.ObjectId))
                {
                    issues.Add(Issue(ValidationSeverity.Error, definition.ObjectId, "Duplicate prank ObjectId."));
                }

                if (string.IsNullOrWhiteSpace(definition.Payload))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Prank definition needs a stable Payload."));
                }
                else if (!payloads.Add(definition.Payload))
                {
                    issues.Add(Issue(ValidationSeverity.Error, definition.Payload, "Duplicate prank Payload."));
                }

                if (!definition.LocalizedDisplayName.HasAnyText)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Prank definition needs LocalizedDisplayName."));
                }

                if (!definition.LocalizedUnsupportedReason.HasAnyText)
                {
                    issues.Add(Issue(ValidationSeverity.Warning, subjectId, "Prank definition has no LocalizedUnsupportedReason."));
                }
            }
        }

        private static void ValidateTargets(
            IReadOnlyList<CampusPrankTarget> targets,
            List<ValidationIssue> issues)
        {
            if (targets == null || targets.Count == 0)
            {
                issues.Add(Issue(
                    ValidationSeverity.Info,
                    "targets",
                    "No prank interaction targets are currently cached. This is valid only if the scene intentionally has no prank spots."));
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                CampusPrankTarget target = targets[i];
                string subjectId = string.IsNullOrWhiteSpace(target.Payload)
                    ? "target[" + i + "]"
                    : target.Payload;

                if (!target.IsValid)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Prank target needs a payload and an interact target."));
                    continue;
                }

                if (!CampusPrankCatalog.TryGetByPayload(target.Payload, out _))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Prank target payload is not in the prank catalog."));
                }

                if (string.IsNullOrWhiteSpace(target.RoomId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "Prank target has no resolved RoomId. NPC navigation can still use position, but mod data should place the target inside a declared room."));
                }
            }
        }

        private static ValidationIssue Issue(ValidationSeverity severity, string subjectId, string message)
        {
            return new ValidationIssue(severity, subjectId, message);
        }
    }
}
