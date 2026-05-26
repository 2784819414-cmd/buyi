using System;
using System.Collections.Generic;
using System.IO;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public enum CampusPlacedObjectConceptKind
    {
        Prop = 0,
        Facility = 1,
        PickupItem = 2
    }

    public readonly struct CampusPlacedObjectConceptResolution
    {
        public CampusPlacedObjectConceptResolution(
            CampusPlacedObjectConceptKind conceptKind,
            CampusFacilityTypeResolution facilityResolution)
        {
            ConceptKind = conceptKind;
            FacilityResolution = facilityResolution;
        }

        public CampusPlacedObjectConceptKind ConceptKind { get; }
        public CampusFacilityTypeResolution FacilityResolution { get; }
        public bool IsFacility => ConceptKind == CampusPlacedObjectConceptKind.Facility;

        public static CampusPlacedObjectConceptResolution Prop()
        {
            return new CampusPlacedObjectConceptResolution(
                CampusPlacedObjectConceptKind.Prop,
                new CampusFacilityTypeResolution(
                    CampusFacilityType.Unknown,
                    CampusFacilityTypeSource.Unknown,
                    string.Empty));
        }

        public static CampusPlacedObjectConceptResolution PickupItem()
        {
            return new CampusPlacedObjectConceptResolution(
                CampusPlacedObjectConceptKind.PickupItem,
                new CampusFacilityTypeResolution(
                    CampusFacilityType.Unknown,
                    CampusFacilityTypeSource.Unknown,
                    string.Empty));
        }

        public static CampusPlacedObjectConceptResolution Facility(CampusFacilityTypeResolution facilityResolution)
        {
            return new CampusPlacedObjectConceptResolution(
                CampusPlacedObjectConceptKind.Facility,
                facilityResolution);
        }
    }

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

    // A placed object is only the scene shell. Facility and pickup-item meaning are derived here.
    public static class CampusPlacedObjectConceptResolver
    {
        public static CampusPlacedObjectConceptResolution Resolve(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return CampusPlacedObjectConceptResolution.Prop();
            }

            if (IsPickupItem(placedObject))
            {
                return CampusPlacedObjectConceptResolution.PickupItem();
            }

            CampusFacilityTypeResolution facilityResolution =
                CampusFacilityTypeResolver.ResolveDetailed(placedObject);
            return IsFacilityCandidate(facilityResolution)
                ? CampusPlacedObjectConceptResolution.Facility(facilityResolution)
                : CampusPlacedObjectConceptResolution.Prop();
        }

        public static bool TryResolveFacility(
            CampusPlacedObject placedObject,
            out CampusFacilityTypeResolution facilityResolution)
        {
            CampusPlacedObjectConceptResolution concept = Resolve(placedObject);
            facilityResolution = concept.FacilityResolution;
            return concept.IsFacility;
        }

        public static bool IsPickupItem(CampusPlacedObject placedObject)
        {
            return placedObject != null &&
                   placedObject.GetComponent<CampusDroppedStorageItem>() != null;
        }

        private static bool IsFacilityCandidate(CampusFacilityTypeResolution facilityResolution)
        {
            switch (facilityResolution.Source)
            {
                case CampusFacilityTypeSource.ExplicitTypeId:
                    return true;

                default:
                    return false;
            }
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

            if (CampusPlacedObjectConceptResolver.IsPickupItem(placedObject))
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

                return new CampusFacilityTypeResolution(
                    CampusFacilityType.Unknown,
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
                    CampusFacilityType.Unknown,
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
                        CampusFacilityType.Unknown,
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
                Debug.LogWarning(CampusFacilityValidationTextCatalog.Format(
                    CampusFacilityValidationTextId.FailedToLoadRules,
                    exception.Message));
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
                FacilityType = nameof(CampusFacilityType.ReadyItemContainer),
                TypeIds = new[] { "ReadyItemContainer", "ready_item_container", "ready_box", "hot_item_container" },
                DisplayNames = new[] { "ready item container", "ready item box", "\u73b0\u6210\u7269\u54c1\u7bb1", "\u51c6\u5907\u7269\u54c1\u7bb1" },
                Contains = new[] { "ready_item_container", "readyitembox", "hotitemcontainer", "\u73b0\u6210\u7269\u54c1\u7bb1", "\u51c6\u5907\u7269\u54c1\u7bb1" }
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
                FacilityType = nameof(CampusFacilityType.ServiceWindow),
                TypeIds = new[] { "ServiceWindow", "service_window", "pickup_window", "service_counter_window" },
                DisplayNames = new[] { "service window", "pickup window", "\u670d\u52a1\u7a97\u53e3", "\u6253\u996d\u7a97\u53e3", "\u53d6\u7269\u7a97\u53e3" },
                Contains = new[] { "service_window", "servicewindow", "pickupwindow", "\u670d\u52a1\u7a97\u53e3", "\u6253\u996d\u7a97\u53e3", "\u53d6\u7269\u7a97\u53e3" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.ServiceCounter),
                TypeIds = new[] { "ServiceCounter", "service_counter", "front_counter" },
                DisplayNames = new[] { "service counter", "front counter", "\u670d\u52a1\u67dc\u53f0" },
                Contains = new[] { "service_counter", "frontcounter", "\u670d\u52a1\u67dc\u53f0" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.WorkerStandPoint),
                TypeIds = new[] { "WorkerStandPoint", "worker_stand_point", "worker_station", "staff_point" },
                DisplayNames = new[] { "worker stand point", "worker station", "\u5de5\u4f5c\u7ad9\u4f4d", "\u804c\u5458\u7ad9\u4f4d" },
                Contains = new[] { "worker_stand_point", "workerstation", "staff_point", "\u5de5\u4f5c\u7ad9\u4f4d", "\u804c\u5458\u7ad9\u4f4d" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.PickupPoint),
                TypeIds = new[] { "PickupPoint", "pickup_point", "claim_point", "collection_point" },
                DisplayNames = new[] { "pickup point", "claim point", "\u53d6\u7269\u70b9", "\u9886\u53d6\u70b9" },
                Contains = new[] { "pickup_point", "claimpoint", "collectionpoint", "\u53d6\u7269\u70b9", "\u9886\u53d6\u70b9" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.WaitingPoint),
                TypeIds = new[] { "WaitingPoint", "waiting_point", "queue_point", "line_point" },
                DisplayNames = new[] { "waiting point", "queue point", "\u7b49\u5f85\u70b9", "\u6392\u961f\u70b9" },
                Contains = new[] { "waiting_point", "queuepoint", "linepoint", "\u7b49\u5f85\u70b9", "\u6392\u961f\u70b9" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.ReadyItemSurface),
                TypeIds = new[] { "ReadyItemSurface", "ready_item_surface", "item_tray", "display_tray" },
                DisplayNames = new[] { "ready item surface", "item tray", "\u7269\u54c1\u6258\u76d8", "\u9648\u5217\u6258\u76d8" },
                Contains = new[] { "ready_item_surface", "itemtray", "displaytray", "\u7269\u54c1\u6258\u76d8", "\u9648\u5217\u6258\u76d8" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.GoodsShelf),
                TypeIds = new[] { "GoodsShelf", "goods_shelf", "item_shelf", "display_shelf" },
                DisplayNames = new[] { "goods shelf", "item shelf", "display shelf", "\u7269\u54c1\u8d27\u67b6" },
                Contains = new[] { "goods_shelf", "itemshelf", "displayshelf", "\u7269\u54c1\u8d27\u67b6", "\u8d27\u67b6" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CheckoutPoint),
                TypeIds = new[] { "CheckoutPoint", "checkout_point", "settlement_point", "cash_register" },
                DisplayNames = new[] { "checkout point", "settlement point", "cash register", "\u7ed3\u7b97\u70b9" },
                Contains = new[] { "checkout_point", "settlementpoint", "cashregister", "\u7ed3\u7b97\u70b9", "\u7ed3\u8d26" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.CheckoutQueuePoint),
                TypeIds = new[] { "CheckoutQueuePoint", "checkout_queue_point", "checkout_queue", "settlement_queue" },
                DisplayNames = new[] { "checkout queue point", "settlement queue", "\u7ed3\u7b97\u6392\u961f\u70b9" },
                Contains = new[] { "checkout_queue_point", "checkoutqueue", "settlementqueue", "\u7ed3\u7b97\u6392\u961f\u70b9" }
            });
            rules.Add(new FacilityRule
            {
                FacilityType = nameof(CampusFacilityType.DropPoint),
                TypeIds = new[] { "DropPoint", "drop_point", "claim_drop_point", "handoff_point" },
                DisplayNames = new[] { "drop point", "handoff point", "\u6295\u653e\u70b9", "\u4ea4\u63a5\u70b9" },
                Contains = new[] { "drop_point", "handoffpoint", "\u6295\u653e\u70b9", "\u4ea4\u63a5\u70b9" }
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
