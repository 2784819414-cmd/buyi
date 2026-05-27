using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEditor;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampusMapEditor
{
    internal enum CampusGameplayArchitectureTextId
    {
        ValidationStarted = 0,
        ValidationPassed = 1,
        ValidationFailed = 2,
        DirectoryMissing = 3,
        ForbiddenPlayerUiReference = 4,
        InlineConfiguredActionPayload = 5,
        MissingConfiguredActionPresetFile = 6,
        MissingExplicitInteractionPreset = 7,
        ObjectCatalogSettingsObjectIdMismatch = 8
    }

    internal static class CampusGameplayArchitectureTextCatalog
    {
        private static readonly Dictionary<CampusGameplayArchitectureTextId, Entry> Entries = new()
        {
            {
                CampusGameplayArchitectureTextId.ValidationStarted,
                new Entry(
                    "[NtingCampusSourceValidation] \u5f00\u59cb\u68c0\u67e5 Gameplay \u67b6\u6784\u8fb9\u754c\u3002",
                    "[NtingCampusSourceValidation] Started Gameplay architecture boundary validation.")
            },
            {
                CampusGameplayArchitectureTextId.ValidationPassed,
                new Entry(
                    "[NtingCampusSourceValidation] Gameplay \u67b6\u6784\u8fb9\u754c\u68c0\u67e5\u901a\u8fc7\u3002",
                    "[NtingCampusSourceValidation] Gameplay architecture boundary validation passed.")
            },
            {
                CampusGameplayArchitectureTextId.ValidationFailed,
                new Entry(
                    "[NtingCampusSourceValidation] Gameplay \u67b6\u6784\u8fb9\u754c\u68c0\u67e5\u5931\u8d25\u3002",
                    "[NtingCampusSourceValidation] Gameplay architecture boundary validation failed.")
            },
            {
                CampusGameplayArchitectureTextId.DirectoryMissing,
                new Entry(
                    "[NtingCampusSourceValidation] \u76ee\u5f55\u4e0d\u5b58\u5728\uff1a{0}",
                    "[NtingCampusSourceValidation] Directory is missing: {0}")
            },
            {
                CampusGameplayArchitectureTextId.ForbiddenPlayerUiReference,
                new Entry(
                    "[NtingCampusSourceValidation] Gameplay \u6267\u884c\u5c42\u4e0d\u5f97\u76f4\u63a5\u6253\u5f00\u73a9\u5bb6 UI\uff1a{0}:{1} \u5305\u542b {2}\u3002",
                    "[NtingCampusSourceValidation] Gameplay execution must not open player UI directly: {0}:{1} contains {2}.")
            },
            {
                CampusGameplayArchitectureTextId.InlineConfiguredActionPayload,
                new Entry(
                    "[NtingCampusSourceValidation] \u914d\u7f6e\u52a8\u4f5c payload \u5fc5\u987b\u7531 ConfiguredActionPresets.json \u62e5\u6709\uff1a{0}:{1}\u3002",
                    "[NtingCampusSourceValidation] Configured action payload must be owned by ConfiguredActionPresets.json: {0}:{1}.")
            },
            {
                CampusGameplayArchitectureTextId.MissingConfiguredActionPresetFile,
                new Entry(
                    "[NtingCampusSourceValidation] \u7f3a\u5c11 ConfiguredActionPresets.json\u3002",
                    "[NtingCampusSourceValidation] ConfiguredActionPresets.json is missing.")
            },
            {
                CampusGameplayArchitectureTextId.MissingExplicitInteractionPreset,
                new Entry(
                    "[NtingCampusSourceValidation] \u5df2\u5728 ObjectInteractionPresets.json \u6620\u5c04\u7684\u5bf9\u8c61\u5fc5\u987b\u663e\u5f0f\u5199\u5165 InteractionPresetEid\uff1a{0}:{1} \u5bf9\u8c61 {2}\u3002",
                    "[NtingCampusSourceValidation] Object mapped by ObjectInteractionPresets.json must write InteractionPresetEid explicitly: {0}:{1} object {2}.")
            },
            {
                CampusGameplayArchitectureTextId.ObjectCatalogSettingsObjectIdMismatch,
                new Entry(
                    "[NtingCampusSourceValidation] RuntimeObjectCatalog.json \u7684\u7269\u4f53\u76ee\u5f55\u9879\u548c Settings.ObjectId \u5fc5\u987b\u4e00\u81f4\uff1a{0} \u7684 Settings.ObjectId \u662f {1}\u3002",
                    "[NtingCampusSourceValidation] RuntimeObjectCatalog.json entry ObjectId must match Settings.ObjectId: {0} has Settings.ObjectId {1}.")
            }
        };

        public static string Get(CampusGameplayArchitectureTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }

        public static string Format(CampusGameplayArchitectureTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }

    public static class CampusGameplayArchitectureValidator
    {
        private const string GameplaySourceDirectory = "Assets/_NtingCampus/Scripts/Gameplay";
        private const string RuntimePresetDirectory =
            "Assets/NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/RuntimePresets";
        private const string RuntimeContentDirectory =
            "Assets/NtingCampus/UserGeneratedRuntimeContent";
        private const string ConfiguredActionPresetFile =
            RuntimePresetDirectory + "/ConfiguredActionPresets.json";
        private const string ObjectInteractionPresetFile =
            RuntimePresetDirectory + "/ObjectInteractionPresets.json";
        private const string RuntimeObjectCatalogFile =
            RuntimeContentDirectory + "/CampusRuntimeImports/RuntimeObjectCatalog.json";

        private static readonly string[] ForbiddenGameplayPlayerUiTokens =
        {
            "StorageWindowUI",
            "OpenPlayerStorage",
            "Canvas_Storage"
        };

        [MenuItem("Tools/Nting Campus/Validate Source Architecture/Gameplay Runtime Boundaries")]
        public static void ValidateGameplayRuntimeBoundariesFromMenu()
        {
            ValidateGameplayRuntimeBoundaries(logPassed: true);
        }

        public static bool ValidateGameplayRuntimeBoundaries(bool logPassed)
        {
            Debug.Log(CampusGameplayArchitectureTextCatalog.Get(
                CampusGameplayArchitectureTextId.ValidationStarted));

            bool passed = true;
            passed &= ValidateNoPlayerUiInGameplayExecution();
            passed &= ValidateConfiguredActionPayloadOwnership();
            passed &= ValidateExplicitInteractionPresetBindings();
            passed &= ValidateRuntimeObjectCatalogOwnership();

            if (passed)
            {
                if (logPassed)
                {
                    Debug.Log(CampusGameplayArchitectureTextCatalog.Get(
                        CampusGameplayArchitectureTextId.ValidationPassed));
                }
            }
            else
            {
                Debug.LogError(CampusGameplayArchitectureTextCatalog.Get(
                    CampusGameplayArchitectureTextId.ValidationFailed));
            }

            return passed;
        }

        private static bool ValidateNoPlayerUiInGameplayExecution()
        {
            string absoluteDirectory = Path.GetFullPath(GameplaySourceDirectory);
            if (!Directory.Exists(absoluteDirectory))
            {
                Debug.LogError(CampusGameplayArchitectureTextCatalog.Format(
                    CampusGameplayArchitectureTextId.DirectoryMissing,
                    GameplaySourceDirectory));
                return false;
            }

            bool passed = true;
            string[] sourceFiles = Directory.GetFiles(absoluteDirectory, "*.cs", SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < sourceFiles.Length; fileIndex++)
            {
                string file = sourceFiles[fileIndex];
                string assetPath = ToAssetPath(file);
                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    for (int tokenIndex = 0; tokenIndex < ForbiddenGameplayPlayerUiTokens.Length; tokenIndex++)
                    {
                        string token = ForbiddenGameplayPlayerUiTokens[tokenIndex];
                        if (!line.Contains(token))
                        {
                            continue;
                        }

                        passed = false;
                        Debug.LogError(CampusGameplayArchitectureTextCatalog.Format(
                            CampusGameplayArchitectureTextId.ForbiddenPlayerUiReference,
                            assetPath,
                            lineIndex + 1,
                            token));
                    }
                }
            }

            return passed;
        }

        private static bool ValidateConfiguredActionPayloadOwnership()
        {
            if (!File.Exists(ConfiguredActionPresetFile))
            {
                Debug.LogError(CampusGameplayArchitectureTextCatalog.Get(
                    CampusGameplayArchitectureTextId.MissingConfiguredActionPresetFile));
                return false;
            }

            bool passed = true;
            string[] files =
            {
                RuntimePresetDirectory + "/NpcEcologyPresets.json",
                RuntimePresetDirectory + "/ObjectInteractionPresets.json"
            };

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string file = files[fileIndex];
                if (!File.Exists(file))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(file);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (!lines[lineIndex].Contains("\"Payload\": \"{"))
                    {
                        continue;
                    }

                    passed = false;
                    Debug.LogError(CampusGameplayArchitectureTextCatalog.Format(
                        CampusGameplayArchitectureTextId.InlineConfiguredActionPayload,
                        file,
                        lineIndex + 1));
                }
            }

            return passed;
        }

        private static bool ValidateExplicitInteractionPresetBindings()
        {
            if (!File.Exists(ObjectInteractionPresetFile))
            {
                return true;
            }

            HashSet<string> mappedObjectIds = ReadMappedObjectIds(ObjectInteractionPresetFile);
            if (mappedObjectIds.Count == 0 || !Directory.Exists(RuntimeContentDirectory))
            {
                return true;
            }

            bool passed = true;
            string[] files = Directory.GetFiles(RuntimeContentDirectory, "*.json", SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string file = ToAssetPath(files[fileIndex]);
                if (file.Replace('\\', '/').Contains("/RuntimePresets/"))
                {
                    continue;
                }

                passed &= ValidateExplicitInteractionPresetBindingsInFile(file, mappedObjectIds);
            }

            string catalogPath = RuntimeContentDirectory + "/CampusRuntimeImports/RuntimeObjectCatalog.json";
            if (File.Exists(catalogPath))
            {
                passed &= ValidateExplicitInteractionPresetBindingsInFile(catalogPath, mappedObjectIds);
            }

            return passed;
        }

        private static bool ValidateExplicitInteractionPresetBindingsInFile(
            string file,
            HashSet<string> mappedObjectIds)
        {
            bool passed = true;
            string activeObjectId = string.Empty;
            string[] lines = File.ReadAllLines(file);
            Regex objectIdPattern = new Regex("\"ObjectId\"\\s*:\\s*\"([^\"]+)\"");
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                Match objectIdMatch = objectIdPattern.Match(lines[lineIndex]);
                if (objectIdMatch.Success)
                {
                    activeObjectId = objectIdMatch.Groups[1].Value.Trim();
                }

                if (string.IsNullOrEmpty(activeObjectId) ||
                    !mappedObjectIds.Contains(activeObjectId) ||
                    !lines[lineIndex].Contains("\"InteractionPresetEid\": \"\""))
                {
                    continue;
                }

                passed = false;
                Debug.LogError(CampusGameplayArchitectureTextCatalog.Format(
                    CampusGameplayArchitectureTextId.MissingExplicitInteractionPreset,
                    file,
                    lineIndex + 1,
                    activeObjectId));
                activeObjectId = string.Empty;
            }

            return passed;
        }

        private static HashSet<string> ReadMappedObjectIds(string file)
        {
            HashSet<string> ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(file);
            Regex objectIdPattern = new Regex("\"ObjectIds\"|\"([^\"]+)\"");
            bool insideObjectIds = false;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (line.Contains("\"ObjectIds\""))
                {
                    insideObjectIds = true;
                    continue;
                }

                if (!insideObjectIds)
                {
                    continue;
                }

                if (line.Contains("]"))
                {
                    insideObjectIds = false;
                    continue;
                }

                Match match = objectIdPattern.Match(line);
                if (match.Success && match.Groups.Count > 1)
                {
                    string id = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(id))
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids;
        }

        private static bool ValidateRuntimeObjectCatalogOwnership()
        {
            if (!File.Exists(RuntimeObjectCatalogFile))
            {
                return true;
            }

            RuntimeObjectCatalogValidationData data =
                JsonUtility.FromJson<RuntimeObjectCatalogValidationData>(File.ReadAllText(RuntimeObjectCatalogFile));
            if (data == null || data.Objects == null)
            {
                return true;
            }

            bool passed = true;
            for (int i = 0; i < data.Objects.Count; i++)
            {
                RuntimeObjectCatalogValidationEntry entry = data.Objects[i];
                if (entry == null || entry.Settings == null)
                {
                    continue;
                }

                string objectId = CleanId(entry.ObjectId);
                string settingsObjectId = CleanId(entry.Settings.ObjectId);
                if (string.IsNullOrEmpty(objectId) ||
                    string.IsNullOrEmpty(settingsObjectId) ||
                    string.Equals(objectId, settingsObjectId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                passed = false;
                Debug.LogError(CampusGameplayArchitectureTextCatalog.Format(
                    CampusGameplayArchitectureTextId.ObjectCatalogSettingsObjectIdMismatch,
                    objectId,
                    settingsObjectId));
            }

            return passed;
        }

        private static string CleanId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

#pragma warning disable 0649
        [System.Serializable]
        private sealed class RuntimeObjectCatalogValidationData
        {
            public List<RuntimeObjectCatalogValidationEntry> Objects =
                new List<RuntimeObjectCatalogValidationEntry>();
        }

        [System.Serializable]
        private sealed class RuntimeObjectCatalogValidationEntry
        {
            public string ObjectId;
            public RuntimeObjectCatalogValidationSettings Settings;
        }

        [System.Serializable]
        private sealed class RuntimeObjectCatalogValidationSettings
        {
            public string ObjectId;
        }
#pragma warning restore 0649

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
