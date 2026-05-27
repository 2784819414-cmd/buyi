using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nting.AIPrefabGuard.Editor
{
    public sealed class HighRiskFileClassifier
    {
        public IReadOnlyList<RiskFinding> Classify(IReadOnlyList<GitChangedFile> changedFiles)
        {
            return Classify(changedFiles, null);
        }

        public IReadOnlyList<RiskFinding> Classify(IReadOnlyList<GitChangedFile> changedFiles, IReadOnlyDictionary<string, IReadOnlyList<string>> extraInsights)
        {
            var findings = new List<RiskFinding>();
            if (changedFiles == null || changedFiles.Count == 0)
            {
                return findings;
            }

            var changedPathSet = new HashSet<string>(changedFiles.Select(file => file.RelativePath), System.StringComparer.OrdinalIgnoreCase);

            foreach (var changedFile in changedFiles)
            {
                var fileType = GetFileType(changedFile.RelativePath);
                if (fileType == RiskFileType.Other)
                {
                    continue;
                }

                findings.Add(CreateFinding(changedFile, fileType, changedPathSet, extraInsights));
            }

            return findings
                .OrderByDescending(finding => finding.RiskLevel)
                .ThenBy(finding => finding.File.RelativePath)
                .ToList();
        }

        private static RiskFinding CreateFinding(
            GitChangedFile changedFile,
            RiskFileType fileType,
            HashSet<string> changedPathSet,
            IReadOnlyDictionary<string, IReadOnlyList<string>> extraInsights)
        {
            var insights = GetInsights(changedFile, fileType, changedPathSet);
            if (extraInsights != null && extraInsights.TryGetValue(changedFile.RelativePath, out var additionalInsights))
            {
                insights.AddRange(additionalInsights);
            }

            return new RiskFinding(
                changedFile,
                GetRiskLevel(fileType),
                fileType,
                GetReason(fileType),
                GetChecklist(fileType),
                insights);
        }

        private static RiskFileType GetFileType(string relativePath)
        {
            var extension = Path.GetExtension(relativePath).ToLowerInvariant();
            switch (extension)
            {
                case ".prefab":
                    return RiskFileType.Prefab;
                case ".unity":
                    return RiskFileType.Scene;
                case ".meta":
                    return RiskFileType.Meta;
                case ".asset":
                    return RiskFileType.Asset;
                case ".asmdef":
                    return RiskFileType.AssemblyDefinition;
                default:
                    return RiskFileType.Other;
            }
        }

        private static RiskLevel GetRiskLevel(RiskFileType fileType)
        {
            switch (fileType)
            {
                case RiskFileType.Prefab:
                case RiskFileType.Scene:
                case RiskFileType.Meta:
                    return RiskLevel.VeryHigh;
                case RiskFileType.Asset:
                case RiskFileType.AssemblyDefinition:
                    return RiskLevel.High;
                default:
                    return RiskLevel.Low;
            }
        }

        private static string GetReason(RiskFileType fileType)
        {
            switch (fileType)
            {
                case RiskFileType.Prefab:
                    return "Prefab serialization changed. Hierarchy, components, references, and overrides may have been altered.";
                case RiskFileType.Scene:
                    return "Scene serialization changed. Scene objects, cameras, lights, UI, and logic objects may have been altered.";
                case RiskFileType.Meta:
                    return "Unity meta file changed. GUIDs, import settings, or references may be affected.";
                case RiskFileType.Asset:
                    return "Unity asset changed. ScriptableObject data, render settings, profiles, or configuration may have been altered.";
                case RiskFileType.AssemblyDefinition:
                    return "Assembly definition changed. Compilation boundaries, platform filters, and dependencies may have been altered.";
                default:
                    return string.Empty;
            }
        }

        private static IReadOnlyList<string> GetChecklist(RiskFileType fileType)
        {
            switch (fileType)
            {
                case RiskFileType.Prefab:
                    return new[]
                    {
                        "Open Prefab Mode and verify hierarchy, components, serialized references, and overrides.",
                        "Check for Missing Script warnings and broken object references.",
                        "Run the smallest relevant Play Mode flow that uses this prefab."
                    };
                case RiskFileType.Scene:
                    return new[]
                    {
                        "Open the scene and verify key GameObjects, cameras, lights, UI, and bootstrap objects.",
                        "Check Console for missing scripts, missing references, or import warnings.",
                        "Enter Play Mode and verify the scene starts without unexpected behavior."
                    };
                case RiskFileType.Meta:
                    return new[]
                    {
                        "Inspect whether the guid line changed in the diff.",
                        "Confirm the corresponding asset still exists and references are intact.",
                        "Reimport only if you intentionally changed import settings."
                    };
                case RiskFileType.Asset:
                    return new[]
                    {
                        "Inspect the asset in the Inspector and verify important serialized fields.",
                        "Check whether this asset is a shared configuration or render/profile asset.",
                        "Run the feature path that reads this asset."
                    };
                case RiskFileType.AssemblyDefinition:
                    return new[]
                    {
                        "Review references, includePlatforms, excludePlatforms, and autoReferenced.",
                        "Confirm Editor-only and Runtime assemblies are still separated correctly.",
                        "Wait for Unity compilation and resolve all Console errors before merging."
                    };
                default:
                    return new string[0];
            }
        }

        private static List<string> GetInsights(GitChangedFile changedFile, RiskFileType fileType, HashSet<string> changedPathSet)
        {
            var insights = new List<string>();

            if (changedFile.ChangeKind == GitChangeKind.Deleted)
            {
                insights.Add("Deleted file: Unity references may now point to a missing asset.");
            }

            if (changedFile.ChangeKind == GitChangeKind.Renamed)
            {
                insights.Add("Renamed file: confirm Unity references and meta pairing survived the move.");
            }

            if (fileType == RiskFileType.Meta)
            {
                var assetPath = changedFile.RelativePath.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)
                    ? changedFile.RelativePath.Substring(0, changedFile.RelativePath.Length - ".meta".Length)
                    : string.Empty;

                if (!string.IsNullOrEmpty(assetPath) && changedPathSet.Contains(assetPath))
                {
                    insights.Add("Meta changed together with its asset. Verify this pairing was intentional.");
                }
                else if (!string.IsNullOrEmpty(assetPath))
                {
                    insights.Add("Meta changed without the matching asset in the baseline diff. Pay special attention to GUID/import setting changes.");
                }
            }

            if (fileType == RiskFileType.AssemblyDefinition)
            {
                insights.Add("Sensitive asmdef fields to review: references, includePlatforms, excludePlatforms, autoReferenced, overrideReferences, precompiledReferences.");
            }

            return insights;
        }
    }
}
