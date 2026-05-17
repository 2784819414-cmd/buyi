using System;
using System.Collections.Generic;
using System.IO;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public static class CampusFacilityTypeResolver
    {
        private const string RuleFileRelativePath =
            "NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/FacilityRules.json";

        private static readonly List<FacilityRule> Rules = new List<FacilityRule>();
        private static bool rulesLoaded;

        public static CampusFacilityType Resolve(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return CampusFacilityType.Unknown;
            }

            EnsureRulesLoaded();
            string objectId = Normalize(placedObject.ObjectId);
            string displayName = Normalize(placedObject.DisplayName);
            string rawObjectId = placedObject.ObjectId ?? string.Empty;
            string rawDisplayName = placedObject.DisplayName ?? string.Empty;

            for (int i = 0; i < Rules.Count; i++)
            {
                FacilityRule rule = Rules[i];
                if (rule != null && rule.TryResolve(objectId, displayName, rawObjectId, rawDisplayName, out CampusFacilityType type))
                {
                    return type;
                }
            }

            return placedObject.IsStorageContainer ? CampusFacilityType.Storage : CampusFacilityType.Unknown;
        }

        private static void EnsureRulesLoaded()
        {
            if (rulesLoaded)
            {
                return;
            }

            rulesLoaded = true;
            Rules.Clear();
            AddBuiltInRules(Rules);

            string path = ResolveRuleFilePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                FacilityRuleFile file = JsonUtility.FromJson<FacilityRuleFile>(File.ReadAllText(path));
                if (file == null || file.Rules == null)
                {
                    return;
                }

                for (int i = 0; i < file.Rules.Count; i++)
                {
                    FacilityRule rule = file.Rules[i];
                    if (rule != null && !string.IsNullOrWhiteSpace(rule.FacilityType))
                    {
                        Rules.Insert(0, rule);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Rooms] Failed to load facility rules: " + exception.Message);
            }
        }

        private static string ResolveRuleFilePath()
        {
            string assetsPath = Application.dataPath;
            if (!string.IsNullOrWhiteSpace(assetsPath))
            {
                return Path.Combine(assetsPath, RuleFileRelativePath.Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                RuleFileRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void AddBuiltInRules(List<FacilityRule> rules)
        {
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Door),
                ObjectIds = new[] { "door", "门" },
                DisplayNames = new[] { "door", "门" },
                Contains = new[] { "door", "门" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.StudentDesk),
                ObjectIds = new[] { "desk_1x1" },
                DisplayNames = new[] { "desk", "student desk", "课桌", "书桌" },
                Contains = new[] { "studentdesk", "student_desk", "课桌", "书桌" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Chair),
                ObjectIds = new[] { "chair_1x2", "side_chair", "wooden_chair" },
                DisplayNames = new[] { "chair", "椅子" },
                Contains = new[] { "chair", "椅" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Bed),
                ObjectIds = new[] { "bed" },
                DisplayNames = new[] { "bed", "床" },
                Contains = new[] { "bed", "床" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.BulletinBoard),
                ObjectIds = new[] { "公告栏_1x1" },
                DisplayNames = new[] { "bulletin board", "公告栏" },
                Contains = new[] { "bulletin", "公告栏" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Sink),
                ObjectIds = new[] { "rotating_sink" },
                DisplayNames = new[] { "sink", "洗手池", "水池" },
                Contains = new[] { "sink", "洗手池", "水池" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Storage),
                ObjectIds = new[] { "测试箱" },
                DisplayNames = new[] { "storage", "储物", "箱" },
                Contains = new[] { "storage", "box", "箱" }
            });
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", string.Empty).Replace("-", "_").ToLowerInvariant();
        }

        [Serializable]
        private sealed class FacilityRuleFile
        {
            public List<FacilityRule> Rules = new List<FacilityRule>();
        }

        [Serializable]
        private sealed class FacilityRule
        {
            public string FacilityType = string.Empty;
            public string[] ObjectIds = Array.Empty<string>();
            public string[] DisplayNames = Array.Empty<string>();
            public string[] Contains = Array.Empty<string>();

            public bool TryResolve(
                string normalizedObjectId,
                string normalizedDisplayName,
                string rawObjectId,
                string rawDisplayName,
                out CampusFacilityType type)
            {
                type = CampusFacilityType.Unknown;
                if (!Enum.TryParse(FacilityType, true, out CampusFacilityType parsedType))
                {
                    return false;
                }

                if (MatchesExact(ObjectIds, normalizedObjectId) || MatchesExact(DisplayNames, normalizedDisplayName))
                {
                    type = parsedType;
                    return true;
                }

                string combined = normalizedObjectId + "|" + normalizedDisplayName + "|" + rawObjectId + "|" + rawDisplayName;
                if (MatchesContains(Contains, combined))
                {
                    type = parsedType;
                    return true;
                }

                return false;
            }

            private static bool MatchesExact(string[] values, string target)
            {
                if (values == null || string.IsNullOrWhiteSpace(target))
                {
                    return false;
                }

                for (int i = 0; i < values.Length; i++)
                {
                    if (Normalize(values[i]) == target)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool MatchesContains(string[] values, string target)
            {
                if (values == null || string.IsNullOrWhiteSpace(target))
                {
                    return false;
                }

                string normalizedTarget = Normalize(target);
                for (int i = 0; i < values.Length; i++)
                {
                    string needle = Normalize(values[i]);
                    if (!string.IsNullOrEmpty(needle) && normalizedTarget.Contains(needle))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
