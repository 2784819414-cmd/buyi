using System.Collections.Generic;
using System.IO;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEditor;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampusMapEditor
{
    internal enum CampusSourceArchitectureTextId
    {
        NpcAiSceneScanValidationStarted = 0,
        NpcAiSceneScanValidationPassed = 1,
        NpcAiSceneScanValidationDirectoryMissing = 2,
        NpcAiSceneScanValidationForbiddenCall = 3,
        NpcAiSceneScanValidationFailed = 4
    }

    internal static class CampusSourceArchitectureTextCatalog
    {
        private static readonly Dictionary<CampusSourceArchitectureTextId, Entry> Entries = new()
        {
            {
                CampusSourceArchitectureTextId.NpcAiSceneScanValidationStarted,
                new Entry(
                    "\u005bNtingCampusSourceValidation\u005d \u5f00\u59cb\u68c0\u67e5 NPC AI \u6e90\u7801\u626b\u63cf\u8fb9\u754c\u3002",
                    "[NtingCampusSourceValidation] Started NPC AI source scan boundary validation.")
            },
            {
                CampusSourceArchitectureTextId.NpcAiSceneScanValidationPassed,
                new Entry(
                    "\u005bNtingCampusSourceValidation\u005d NPC AI \u6e90\u7801\u6ca1\u6709\u76f4\u63a5\u5168\u573a\u626b\u63cf\u8c03\u7528\u3002",
                    "[NtingCampusSourceValidation] NPC AI source has no direct scene-wide scan calls.")
            },
            {
                CampusSourceArchitectureTextId.NpcAiSceneScanValidationDirectoryMissing,
                new Entry(
                    "\u005bNtingCampusSourceValidation\u005d NPC AI \u6e90\u7801\u76ee\u5f55\u4e0d\u5b58\u5728\uff1a{0}",
                    "[NtingCampusSourceValidation] NPC AI source directory is missing: {0}")
            },
            {
                CampusSourceArchitectureTextId.NpcAiSceneScanValidationForbiddenCall,
                new Entry(
                    "\u005bNtingCampusSourceValidation\u005d NPC AI \u51b3\u7b56\u8def\u5f84\u4e0d\u5f97\u76f4\u63a5\u5168\u573a\u626b\u63cf\uff1a{0}:{1} \u5305\u542b {2}\u3002",
                    "[NtingCampusSourceValidation] NPC AI decision source must not call scene-wide scan APIs directly: {0}:{1} contains {2}.")
            },
            {
                CampusSourceArchitectureTextId.NpcAiSceneScanValidationFailed,
                new Entry(
                    "\u005bNtingCampusSourceValidation\u005d NPC AI \u6e90\u7801\u626b\u63cf\u8fb9\u754c\u68c0\u67e5\u5931\u8d25\uff0c\u8bf7\u6539\u7528 CampusWorldService\u3001CampusWorldFacts \u6216 CampusRosterService \u63d0\u4f9b\u7684\u4e8b\u5b9e\u7d22\u5f15\u3002",
                    "[NtingCampusSourceValidation] NPC AI source scan boundary validation failed. Use fact indexes from CampusWorldService, CampusWorldFacts, or CampusRosterService instead.")
            }
        };

        public static string Get(CampusSourceArchitectureTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }

        public static string Format(CampusSourceArchitectureTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }

    public static class CampusSourceArchitectureValidator
    {
        private const string NpcAiSourceDirectory = "Assets/_NtingCampus/Scripts/Gameplay/Characters/AI";

        private static readonly string[] ForbiddenSceneScanCalls =
        {
            "FindObjectsByType",
            "FindFirstObjectByType",
            "FindObjectOfType"
        };

        [MenuItem("Tools/Nting Campus/Validate Source Architecture/NPC AI Scene Scan Boundary")]
        public static void ValidateNpcAiSceneScanBoundaryFromMenu()
        {
            ValidateNpcAiSceneScanBoundary(logPassed: true);
        }

        public static bool ValidateNpcAiSceneScanBoundary(bool logPassed)
        {
            Debug.Log(CampusSourceArchitectureTextCatalog.Get(
                CampusSourceArchitectureTextId.NpcAiSceneScanValidationStarted));

            string absoluteDirectory = Path.GetFullPath(NpcAiSourceDirectory);
            if (!Directory.Exists(absoluteDirectory))
            {
                Debug.LogError(CampusSourceArchitectureTextCatalog.Format(
                    CampusSourceArchitectureTextId.NpcAiSceneScanValidationDirectoryMissing,
                    NpcAiSourceDirectory));
                return false;
            }

            bool passed = true;
            string[] sourceFiles = Directory.GetFiles(absoluteDirectory, "*.cs", SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < sourceFiles.Length; fileIndex++)
            {
                passed &= ValidateSourceFile(sourceFiles[fileIndex]);
            }

            if (passed)
            {
                if (logPassed)
                {
                    Debug.Log(CampusSourceArchitectureTextCatalog.Get(
                        CampusSourceArchitectureTextId.NpcAiSceneScanValidationPassed));
                }
            }
            else
            {
                Debug.LogError(CampusSourceArchitectureTextCatalog.Get(
                    CampusSourceArchitectureTextId.NpcAiSceneScanValidationFailed));
            }

            return passed;
        }

        private static bool ValidateSourceFile(string absolutePath)
        {
            bool passed = true;
            string assetPath = ToAssetPath(absolutePath);
            string[] lines = File.ReadAllLines(absolutePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int callIndex = 0; callIndex < ForbiddenSceneScanCalls.Length; callIndex++)
                {
                    string forbiddenCall = ForbiddenSceneScanCalls[callIndex];
                    if (!line.Contains(forbiddenCall))
                    {
                        continue;
                    }

                    passed = false;
                    Debug.LogError(CampusSourceArchitectureTextCatalog.Format(
                        CampusSourceArchitectureTextId.NpcAiSceneScanValidationForbiddenCall,
                        assetPath,
                        lineIndex + 1,
                        forbiddenCall));
                }
            }

            return passed;
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(".");
            string relativePath = absolutePath.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase)
                ? absolutePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : absolutePath;
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
