using System;
using System.Collections.Generic;
using System.IO;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public enum CampusFacilityTypeSource
    {
        Unknown = 0,
        ExplicitTypeId = 1,
        ExplicitMarker = 2,
        StorageFallback = 3,
        LegacyInference = 4,
        MissingTypeId = 5,
        UnknownTypeId = 6
    }

    public readonly struct CampusFacilityTypeResolution
    {
        public CampusFacilityTypeResolution(
            CampusFacilityType facilityType,
            CampusFacilityTypeSource source,
            string diagnostic)
        {
            FacilityType = facilityType;
            Source = source;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CampusFacilityType FacilityType { get; }
        public CampusFacilityTypeSource Source { get; }
        public string Diagnostic { get; }
        public bool IsExplicit => Source == CampusFacilityTypeSource.ExplicitTypeId ||
                                  Source == CampusFacilityTypeSource.ExplicitMarker;

        public static CampusFacilityTypeResolution ExplicitMarker(CampusFacilityType facilityType)
        {
            return new CampusFacilityTypeResolution(
                facilityType,
                CampusFacilityTypeSource.ExplicitMarker,
                string.Empty);
        }
    }

    public static class CampusFacilityTypeResolver
    {
        private const string RuleFileRelativePath =
            "NtingCampus/UserGeneratedRuntimeContent/CampusRuntimeImports/FacilityRules.json";

        private static readonly List<FacilityRule> Rules = new List<FacilityRule>();

        private static bool rulesLoaded;

        public static CampusFacilityType Resolve(CampusPlacedObject placedObject)
        {
            return ResolveDetailed(placedObject).FacilityType;
        }

        public static CampusFacilityType Resolve(CampusPlacedObject placedObject, out string diagnostic)
        {
            CampusFacilityTypeResolution resolution = ResolveDetailed(placedObject);
            diagnostic = resolution.Diagnostic;
            return resolution.FacilityType;
        }

        public static CampusFacilityTypeResolution ResolveDetailed(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return new CampusFacilityTypeResolution(
                    CampusFacilityType.Unknown,
                    CampusFacilityTypeSource.Unknown,
                    string.Empty);
            }

            EnsureRulesLoaded();
            string typeId = Normalize(placedObject.TypeId);
            string rawTypeId = placedObject.TypeId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(typeId))
            {
                if (TryResolveTypeId(typeId, rawTypeId, out CampusFacilityType typeIdType))
                {
                    return new CampusFacilityTypeResolution(
                        typeIdType,
                        CampusFacilityTypeSource.ExplicitTypeId,
                        string.Empty);
                }

                CampusFacilityType fallback = placedObject.IsStorageContainer
                    ? CampusFacilityType.Storage
                    : CampusFacilityType.Unknown;
                return new CampusFacilityTypeResolution(
                    fallback,
                    CampusFacilityTypeSource.UnknownTypeId,
                    rawTypeId.Trim());
            }

            string objectId = Normalize(placedObject.ObjectId);
            string displayName = Normalize(placedObject.DisplayName);
            string rawObjectId = placedObject.ObjectId ?? string.Empty;
            string rawDisplayName = placedObject.DisplayName ?? string.Empty;
            if (placedObject.IsStorageContainer)
            {
                return new CampusFacilityTypeResolution(
                    CampusFacilityType.Storage,
                    CampusFacilityTypeSource.StorageFallback,
                    string.Empty);
            }

            for (int i = 0; i < Rules.Count; i++)
            {
                FacilityRule rule = Rules[i];
                if (rule != null &&
                    rule.TryResolveLegacy(
                        objectId,
                        displayName,
                        rawObjectId,
                        rawDisplayName,
                        out CampusFacilityType type,
                        out string source))
                {
                    return new CampusFacilityTypeResolution(
                        type,
                        CampusFacilityTypeSource.LegacyInference,
                        source);
                }
            }

            return new CampusFacilityTypeResolution(
                CampusFacilityType.Unknown,
                CampusFacilityTypeSource.MissingTypeId,
                string.Empty);
        }

        private static bool TryResolveTypeId(string normalizedTypeId, string rawTypeId, out CampusFacilityType type)
        {
            type = CampusFacilityType.Unknown;
            if (Enum.TryParse(rawTypeId, true, out CampusFacilityType parsedType) &&
                parsedType != CampusFacilityType.Unknown)
            {
                type = parsedType;
                return true;
            }

            string compactTypeId = normalizedTypeId.Replace("_", string.Empty);
            Array values = Enum.GetValues(typeof(CampusFacilityType));
            for (int i = 0; i < values.Length; i++)
            {
                CampusFacilityType candidate = (CampusFacilityType)values.GetValue(i);
                if (candidate == CampusFacilityType.Unknown)
                {
                    continue;
                }

                string candidateKey = Normalize(candidate.ToString()).Replace("_", string.Empty);
                if (string.Equals(candidateKey, compactTypeId, StringComparison.OrdinalIgnoreCase))
                {
                    type = candidate;
                    return true;
                }
            }

            for (int i = 0; i < Rules.Count; i++)
            {
                FacilityRule rule = Rules[i];
                if (rule != null && rule.TryResolveTypeId(normalizedTypeId, out CampusFacilityType ruleType))
                {
                    type = ruleType;
                    return true;
                }
            }

            return false;
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
                TypeIds = new[] { "Door", "door" },
                ObjectIds = new[] { "door", "\u95e8" },
                DisplayNames = new[] { "door", "\u95e8" },
                Contains = new[] { "door", "\u95e8" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Podium),
                TypeIds = new[] { "Podium", "podium", "teacher_podium" },
                DisplayNames = new[] { "podium", "teacher podium", "\u8bb2\u53f0", "\u6559\u5e08\u8bb2\u53f0" },
                Contains = new[] { "podium", "teacher_podium", "teacherpodium", "\u8bb2\u53f0" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Blackboard),
                TypeIds = new[] { "Blackboard", "blackboard", "whiteboard", "chalkboard" },
                DisplayNames = new[] { "blackboard", "whiteboard", "\u9ed1\u677f", "\u767d\u677f" },
                Contains = new[] { "blackboard", "whiteboard", "chalkboard", "\u9ed1\u677f", "\u767d\u677f" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.OfficeDesk),
                TypeIds = new[] { "OfficeDesk", "office_desk", "teacher_desk" },
                DisplayNames = new[] { "office desk", "teacher desk", "\u529e\u516c\u684c", "\u6559\u5e08\u684c" },
                Contains = new[] { "office_desk", "officedesk", "teacher_desk", "teacherdesk", "\u529e\u516c\u684c", "\u6559\u5e08\u684c" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.StudentDesk),
                TypeIds = new[] { "StudentDesk", "student_desk", "desk_1x1" },
                ObjectIds = new[] { "desk_1x1" },
                DisplayNames = new[] { "desk", "student desk", "\u8bfe\u684c", "\u4e66\u684c" },
                Contains = new[] { "studentdesk", "student_desk", "\u8bfe\u684c", "\u4e66\u684c" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Chair),
                TypeIds = new[] { "Chair", "chair", "side_chair", "wooden_chair" },
                ObjectIds = new[] { "chair_1x2", "side_chair", "wooden_chair" },
                DisplayNames = new[] { "chair", "\u6905\u5b50" },
                Contains = new[] { "chair", "\u6905\u5b50" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Bed),
                TypeIds = new[] { "Bed", "bed" },
                ObjectIds = new[] { "bed" },
                DisplayNames = new[] { "bed", "\u5e8a" },
                Contains = new[] { "bed", "\u5e8a" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.BulletinBoard),
                TypeIds = new[] { "BulletinBoard", "bulletin_board", "bulletin" },
                DisplayNames = new[] { "bulletin board", "\u516c\u544a\u680f" },
                Contains = new[] { "bulletin", "\u516c\u544a\u680f" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Sink),
                TypeIds = new[] { "Sink", "sink" },
                ObjectIds = new[] { "rotating_sink" },
                DisplayNames = new[] { "sink", "\u6d17\u624b\u6c60", "\u6c34\u6c60" },
                Contains = new[] { "sink", "\u6d17\u624b\u6c60", "\u6c34\u6c60" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CanteenFoodBox),
                TypeIds = new[] { "CanteenFoodBox", "canteen_food_box", "ready_food_box", "hot_food_box" },
                DisplayNames = new[] { "canteen food box", "ready food box", "\u73b0\u6210\u98df\u7269\u7bb1", "\u98df\u5802\u98df\u7269\u7bb1" },
                Contains = new[] { "canteen_food_box", "readyfoodbox", "hotfoodbox", "\u73b0\u6210\u98df\u7269\u7bb1", "\u98df\u5802\u98df\u7269\u7bb1" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.Storage),
                TypeIds = new[] { "Storage", "storage", "storage_box" },
                DisplayNames = new[] { "storage", "\u50a8\u7269" },
                Contains = new[] { "storage", "box", "\u50a8\u7269", "\u7bb1" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CanteenServingWindow),
                TypeIds = new[] { "CanteenServingWindow", "canteen_serving_window", "meal_window", "food_window" },
                DisplayNames = new[] { "canteen serving window", "meal window", "\u6253\u996d\u7a97\u53e3", "\u98df\u5802\u7a97\u53e3" },
                Contains = new[] { "canteen_serving_window", "servingwindow", "mealwindow", "foodwindow", "\u6253\u996d\u7a97\u53e3", "\u98df\u5802\u7a97\u53e3" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CanteenCounter),
                TypeIds = new[] { "CanteenCounter", "canteen_counter", "food_counter" },
                DisplayNames = new[] { "canteen counter", "food counter", "\u98df\u5802\u67dc\u53f0" },
                Contains = new[] { "canteen_counter", "foodcounter", "malatang", "noodle", "\u98df\u5802\u67dc\u53f0", "\u9ebb\u8fa3\u70eb", "\u9762\u6761" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CanteenClerkStandPoint),
                TypeIds = new[] { "CanteenClerkStandPoint", "canteen_clerk_stand", "canteen_back_counter", "canteen_staff_point" },
                DisplayNames = new[] { "canteen clerk stand", "canteen staff point", "\u98df\u5802\u5e97\u5458\u7ad9\u4f4d", "\u67dc\u53f0\u540e\u4fa7" },
                Contains = new[] { "canteen_clerk_stand", "canteenbackcounter", "canteen_staff", "\u5e97\u5458\u7ad9\u4f4d", "\u67dc\u53f0\u540e" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CanteenCustomerPickupPoint),
                TypeIds = new[] { "CanteenCustomerPickupPoint", "canteen_pickup", "canteen_customer_point", "meal_pickup" },
                DisplayNames = new[] { "canteen pickup", "meal pickup", "\u98df\u5802\u53d6\u9910\u70b9", "\u987e\u5ba2\u53d6\u9910\u70b9" },
                Contains = new[] { "canteen_pickup", "mealpickup", "customer_pickup", "\u53d6\u9910\u70b9", "\u987e\u5ba2\u53d6\u9910" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CanteenQueuePoint),
                TypeIds = new[] { "CanteenQueuePoint", "canteen_queue", "meal_queue" },
                DisplayNames = new[] { "canteen queue", "meal queue", "\u98df\u5802\u6392\u961f\u70b9", "\u6253\u996d\u961f\u5217" },
                Contains = new[] { "canteen_queue", "mealqueue", "food_queue", "\u98df\u5802\u6392\u961f", "\u6253\u996d\u961f" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CanteenFoodTray),
                TypeIds = new[] { "CanteenFoodTray", "canteen_food", "food_tray", "fried_chicken", "burger", "oden" },
                DisplayNames = new[] { "canteen food tray", "fried chicken tray", "\u98df\u5802\u83dc\u76d8", "\u70b8\u9e21\u76d8" },
                Contains = new[] { "canteen_food", "foodtray", "friedchicken", "burger", "oden", "\u70b8\u9e21", "\u6c49\u5821", "\u5173\u4e1c\u716e" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.StoreShelf),
                TypeIds = new[] { "StoreShelf", "store_shelf", "shop_shelf", "snack_shelf" },
                DisplayNames = new[] { "store shelf", "shop shelf", "snack shelf", "\u8d85\u5e02\u8d27\u67b6", "\u5c0f\u5356\u90e8\u8d27\u67b6" },
                Contains = new[] { "store_shelf", "shopshelf", "snackshelf", "goods_shelf", "\u8d27\u67b6", "\u96f6\u98df\u67b6" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.StoreCheckout),
                TypeIds = new[] { "StoreCheckout", "store_checkout", "cash_register", "checkout" },
                DisplayNames = new[] { "store checkout", "cash register", "checkout counter", "\u8d85\u5e02\u6536\u94f6\u53f0", "\u6536\u94f6\u53f0" },
                Contains = new[] { "store_checkout", "checkout", "cashregister", "cashier", "\u6536\u94f6", "\u6536\u94f6\u53f0" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.StoreQueuePoint),
                TypeIds = new[] { "StoreQueuePoint", "store_queue", "checkout_queue" },
                DisplayNames = new[] { "store queue", "checkout queue", "\u8d85\u5e02\u6392\u961f\u70b9", "\u6536\u94f6\u961f\u5217" },
                Contains = new[] { "store_queue", "checkout_queue", "cashier_queue", "\u8d85\u5e02\u6392\u961f", "\u6536\u94f6\u961f" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.DeliveryDropPoint),
                TypeIds = new[] { "DeliveryDropPoint", "delivery_drop", "delivery", "takeout", "waimai" },
                DisplayNames = new[] { "delivery drop point", "delivery point", "\u5916\u5356\u70b9", "\u5916\u5356\u653e\u7f6e\u70b9" },
                Contains = new[] { "delivery", "takeout", "waimai", "\u5916\u5356" }
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
            public string[] TypeIds = Array.Empty<string>();
            public string[] ObjectIds = Array.Empty<string>();
            public string[] DisplayNames = Array.Empty<string>();
            public string[] Contains = Array.Empty<string>();

            public bool TryResolveTypeId(string normalizedTypeId, out CampusFacilityType type)
            {
                type = CampusFacilityType.Unknown;
                if (!Enum.TryParse(FacilityType, true, out CampusFacilityType parsedType))
                {
                    return false;
                }

                if (MatchesExact(TypeIds, normalizedTypeId))
                {
                    type = parsedType;
                    return true;
                }

                return false;
            }

            public bool TryResolveLegacy(
                string normalizedObjectId,
                string normalizedDisplayName,
                string rawObjectId,
                string rawDisplayName,
                out CampusFacilityType type,
                out string source)
            {
                type = CampusFacilityType.Unknown;
                source = string.Empty;
                if (!Enum.TryParse(FacilityType, true, out CampusFacilityType parsedType))
                {
                    return false;
                }

                if (MatchesExact(ObjectIds, normalizedObjectId))
                {
                    type = parsedType;
                    source = "ObjectId";
                    return true;
                }

                if (MatchesExact(DisplayNames, normalizedDisplayName))
                {
                    type = parsedType;
                    source = "DisplayName";
                    return true;
                }

                string combined = normalizedObjectId + "|" + normalizedDisplayName + "|" + rawObjectId + "|" + rawDisplayName;
                if (MatchesContains(Contains, combined))
                {
                    type = parsedType;
                    source = "Contains";
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
