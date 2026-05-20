using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;
using ValidationIssue = NtingCampus.Gameplay.Rooms.CampusEcologyValidator.ValidationIssue;
using ValidationSeverity = NtingCampus.Gameplay.Rooms.CampusEcologyValidator.Severity;

namespace NtingCampus.Gameplay.Economy
{
    public static class CampusStoreValidator
    {
        private const string RegistryResourcePath = "StorageItemRegistry";

        public static List<ValidationIssue> Validate(
            CampusWorldService worldService,
            IReadOnlyList<CampusStoreCatalogEntry> catalog,
            bool usingRuntimeFallbackCatalog = false)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            StorageItemRegistry registry = ResolveItemRegistry(issues);
            CatalogFacts catalogFacts = ValidateCatalog(catalog, usingRuntimeFallbackCatalog, registry, issues);
            ValidateStoreRooms(worldService, catalogFacts, registry, issues);
            return issues;
        }

        public static void LogIssues(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                Debug.Log("[Store] Validation passed.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                string prefix = string.IsNullOrWhiteSpace(issue.SubjectId)
                    ? "[Store]"
                    : "[Store][" + issue.SubjectId + "]";
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

        private static CatalogFacts ValidateCatalog(
            IReadOnlyList<CampusStoreCatalogEntry> catalog,
            bool usingRuntimeFallbackCatalog,
            StorageItemRegistry registry,
            List<ValidationIssue> issues)
        {
            CatalogFacts facts = new CatalogFacts();
            if (usingRuntimeFallbackCatalog)
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    "catalog",
                    "Store service is using the runtime fallback catalog. Assign explicit store catalog entries for normal gameplay."));
            }

            if (catalog == null || catalog.Count == 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, "catalog", "Store catalog has no entries."));
                return facts;
            }

            HashSet<string> categoryDefinitionPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < catalog.Count; i++)
            {
                CampusStoreCatalogEntry entry = catalog[i];
                string subjectId = entry != null && !string.IsNullOrWhiteSpace(entry.DefinitionId)
                    ? entry.DefinitionId.Trim()
                    : "catalog[" + i + "]";

                if (entry == null)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Null store catalog entry."));
                    continue;
                }

                string categoryId = NormalizeId(entry.CategoryId);
                string resolvedCategoryId = entry.ResolveCategoryId();
                string definitionId = entry.ResolveDefinitionId();

                if (string.IsNullOrWhiteSpace(categoryId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "CategoryId is empty. Runtime will treat this entry as general."));
                }

                if (string.IsNullOrWhiteSpace(definitionId))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "DefinitionId is required."));
                }
                else
                {
                    facts.Add(resolvedCategoryId, definitionId);
                    string pairKey = NormalizeId(resolvedCategoryId) + ":" + NormalizeId(definitionId);
                    if (!categoryDefinitionPairs.Add(pairKey))
                    {
                        issues.Add(Issue(
                            ValidationSeverity.Warning,
                            subjectId,
                            "Duplicate store catalog category plus DefinitionId pair."));
                    }

                    ValidateItemDefinition(registry, definitionId, subjectId, issues);
                }

                if (entry.Price < 0)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Price cannot be negative."));
                }

                if (!entry.LocalizedDisplayNameOverride.HasAnyText &&
                    !string.IsNullOrWhiteSpace(entry.DisplayNameOverride))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "DisplayNameOverride is migration-only. Use LocalizedDisplayNameOverride for visible item text."));
                }
            }

            return facts;
        }

        private static void ValidateStoreRooms(
            CampusWorldService worldService,
            CatalogFacts catalogFacts,
            StorageItemRegistry registry,
            List<ValidationIssue> issues)
        {
            if (worldService == null)
            {
                issues.Add(Issue(ValidationSeverity.Error, "world", "World service is missing. Store rooms cannot be validated."));
                return;
            }

            List<CampusGameplayRoom> storeRooms = worldService.GetRoomsByType(CampusRoomType.Store, false);
            if (storeRooms == null || storeRooms.Count == 0)
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    "store",
                    "No Store room found. Store ecology needs at least one Store room with shelves and a checkout."));
                return;
            }

            HashSet<string> facilityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < storeRooms.Count; i++)
            {
                CampusGameplayRoom room = storeRooms[i];
                ValidateStoreRoom(room, i, facilityIds, catalogFacts, registry, issues);
            }
        }

        private static void ValidateStoreRoom(
            CampusGameplayRoom room,
            int roomIndex,
            HashSet<string> facilityIds,
            CatalogFacts catalogFacts,
            StorageItemRegistry registry,
            List<ValidationIssue> issues)
        {
            string subjectId = room != null && !string.IsNullOrWhiteSpace(room.RoomId)
                ? room.RoomId.Trim()
                : "storeRoom[" + roomIndex + "]";
            if (room == null)
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Null Store room."));
                return;
            }

            if (string.IsNullOrWhiteSpace(room.RoomId))
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Store room is missing a stable RoomId."));
            }

            IReadOnlyList<CampusGameplayRoom.FacilityRecord> facilities = room.Facilities;
            if (facilities == null || facilities.Count == 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Store room has no facilities."));
                return;
            }

            int shelfCount = 0;
            int checkoutCount = 0;
            for (int i = 0; i < facilities.Count; i++)
            {
                CampusGameplayRoom.FacilityRecord facility = facilities[i];
                if (facility == null)
                {
                    continue;
                }

                if (facility.FacilityType == CampusFacilityType.StoreShelf)
                {
                    shelfCount++;
                    ValidateShelf(room, facility, i, facilityIds, catalogFacts, registry, issues);
                    continue;
                }

                if (facility.FacilityType == CampusFacilityType.StoreCheckout)
                {
                    checkoutCount++;
                    ValidateCheckout(room, facility, i, facilityIds, issues);
                }
            }

            if (shelfCount == 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Store room has no StoreShelf facility."));
            }

            if (checkoutCount == 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Store room has no StoreCheckout facility."));
            }
        }

        private static void ValidateShelf(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord shelf,
            int index,
            HashSet<string> facilityIds,
            CatalogFacts catalogFacts,
            StorageItemRegistry registry,
            List<ValidationIssue> issues)
        {
            string subjectId = ResolveFacilitySubjectId(shelf, room, index);
            ValidateFacilityIdentity(room, shelf, subjectId, facilityIds, issues);
            if (shelf.PlacedObject == null)
            {
                issues.Add(Issue(
                    ValidationSeverity.Error,
                    subjectId,
                    "StoreShelf needs a placed object because player and NPC actions press E on the same object."));
                return;
            }

            CampusStoreShelfDefinition definition = shelf.PlacedObject.GetComponent<CampusStoreShelfDefinition>();
            if (definition == null)
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    subjectId,
                    "StoreShelf has no CampusStoreShelfDefinition. Runtime will infer category from legacy object metadata."));
                return;
            }

            string categoryId = definition.ResolveCategoryId();
            if (string.IsNullOrWhiteSpace(definition.CategoryId))
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    subjectId,
                    "Shelf CategoryId is empty. Runtime will use general."));
            }

            if (definition.TargetItemCount <= 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Shelf TargetItemCount must be positive."));
            }

            if (definition.HasExplicitItemDefinitions)
            {
                ValidateExplicitShelfItems(definition, subjectId, catalogFacts, registry, issues);
                return;
            }

            if (!string.Equals(categoryId, CampusStoreFacts.GeneralCategoryId, StringComparison.OrdinalIgnoreCase) &&
                !catalogFacts.HasCategory(categoryId))
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    subjectId,
                    "Shelf category has no direct store catalog entries. Runtime will fall back to the general catalog."));
            }
        }

        private static void ValidateExplicitShelfItems(
            CampusStoreShelfDefinition definition,
            string subjectId,
            CatalogFacts catalogFacts,
            StorageItemRegistry registry,
            List<ValidationIssue> issues)
        {
            HashSet<string> itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < definition.ItemDefinitionIds.Count; i++)
            {
                string definitionId = NormalizeId(definition.ItemDefinitionIds[i]);
                if (string.IsNullOrWhiteSpace(definitionId))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, subjectId, "Shelf explicit item list contains an empty DefinitionId."));
                    continue;
                }

                if (!itemIds.Add(definitionId))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, subjectId, "Shelf explicit item list contains a duplicate DefinitionId: " + definitionId + "."));
                }

                if (!catalogFacts.HasDefinition(definitionId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Info,
                        subjectId,
                        "Shelf explicit item is not priced in the store catalog and will use loose zero-price stock: " + definitionId + "."));
                }

                ValidateItemDefinition(registry, definitionId, subjectId, issues);
            }
        }

        private static void ValidateCheckout(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord checkout,
            int index,
            HashSet<string> facilityIds,
            List<ValidationIssue> issues)
        {
            string subjectId = ResolveFacilitySubjectId(checkout, room, index);
            ValidateFacilityIdentity(room, checkout, subjectId, facilityIds, issues);
            if (checkout.PlacedObject == null)
            {
                issues.Add(Issue(
                    ValidationSeverity.Error,
                    subjectId,
                    "StoreCheckout needs a placed object because player and NPC checkout actions press E on the same object."));
                return;
            }

        }

        private static void ValidateFacilityIdentity(
            CampusGameplayRoom room,
            CampusGameplayRoom.FacilityRecord facility,
            string subjectId,
            HashSet<string> facilityIds,
            List<ValidationIssue> issues)
        {
            if (facility == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(facility.FacilityId))
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Store facility is missing a stable FacilityId."));
                return;
            }

            if (!facilityIds.Add(facility.FacilityId.Trim()))
            {
                issues.Add(Issue(ValidationSeverity.Error, subjectId, "Duplicate store facility id in Store room validation."));
            }
        }

        private static void ValidateItemDefinition(
            StorageItemRegistry registry,
            string definitionId,
            string subjectId,
            List<ValidationIssue> issues)
        {
            if (registry == null || string.IsNullOrWhiteSpace(definitionId))
            {
                return;
            }

            if (!registry.TryGetDefinition(definitionId, out StorageItemDefinition definition))
            {
                issues.Add(Issue(
                    ValidationSeverity.Error,
                    subjectId,
                    "Store stock references a missing storage item definition: " + definitionId + "."));
                return;
            }

            if (!definition.LocalizedDisplayName.HasAnyText)
            {
                issues.Add(Issue(
                    ValidationSeverity.Error,
                    subjectId,
                    "Storage item definition needs LocalizedDisplayName: " + definitionId + "."));
            }

            if (!definition.LocalizedDescription.HasAnyText)
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    subjectId,
                    "Storage item definition has no LocalizedDescription: " + definitionId + "."));
            }
        }

        private static StorageItemRegistry ResolveItemRegistry(List<ValidationIssue> issues)
        {
            StorageItemRegistry registry = Resources.Load<StorageItemRegistry>(RegistryResourcePath);
            if (registry != null)
            {
                return registry;
            }

            issues.Add(Issue(
                ValidationSeverity.Warning,
                "StorageItemRegistry",
                "StorageItemRegistry resource is missing. Runtime fallback item definitions will be used for validation."));
            return StorageItemRegistry.CreateFallbackRegistry();
        }

        private static string ResolveFacilitySubjectId(
            CampusGameplayRoom.FacilityRecord facility,
            CampusGameplayRoom room,
            int index)
        {
            if (facility != null && !string.IsNullOrWhiteSpace(facility.FacilityId))
            {
                return facility.FacilityId.Trim();
            }

            string roomId = room != null && !string.IsNullOrWhiteSpace(room.RoomId)
                ? room.RoomId.Trim()
                : "store";
            return roomId + ".facility[" + index + "]";
        }

        private static ValidationIssue Issue(ValidationSeverity severity, string subjectId, string message)
        {
            return new ValidationIssue(severity, subjectId, message);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private sealed class CatalogFacts
        {
            private readonly HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> definitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public void Add(string categoryId, string definitionId)
            {
                string category = NormalizeId(categoryId);
                string definition = NormalizeId(definitionId);
                if (!string.IsNullOrWhiteSpace(category))
                {
                    categories.Add(category);
                }

                if (!string.IsNullOrWhiteSpace(definition))
                {
                    definitionIds.Add(definition);
                }
            }

            public bool HasCategory(string categoryId)
            {
                return !string.IsNullOrWhiteSpace(categoryId) && categories.Contains(categoryId.Trim());
            }

            public bool HasDefinition(string definitionId)
            {
                return !string.IsNullOrWhiteSpace(definitionId) && definitionIds.Contains(definitionId.Trim());
            }
        }
    }
}
