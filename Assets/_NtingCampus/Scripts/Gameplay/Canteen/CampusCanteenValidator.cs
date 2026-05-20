using System;
using System.Collections.Generic;
using UnityEngine;
using ValidationIssue = NtingCampus.Gameplay.Rooms.CampusEcologyValidator.ValidationIssue;
using ValidationSeverity = NtingCampus.Gameplay.Rooms.CampusEcologyValidator.Severity;

namespace NtingCampus.Gameplay.Canteen
{
    public static class CampusCanteenValidator
    {
        private const string GenericWindowTypeId = "generic";

        public static List<ValidationIssue> Validate(CampusCanteenService service)
        {
            if (service == null)
            {
                return new List<ValidationIssue>
                {
                    Issue(ValidationSeverity.Error, "canteen", "Canteen service is missing.")
                };
            }

            return Validate(
                service.Menu,
                service.Stations,
                service.IsUsingRuntimeFallbackMenu);
        }

        public static List<ValidationIssue> Validate(
            CampusCanteenMenuProfile menu,
            IReadOnlyList<CampusCanteenStation> stations,
            bool usingRuntimeFallbackMenu = false)
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            ValidateMenu(menu, usingRuntimeFallbackMenu, issues);
            ValidateStations(stations, issues);
            ValidateMenuCoverage(menu, stations, issues);
            return issues;
        }

        public static void LogIssues(IReadOnlyList<ValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                Debug.Log("[Canteen] Validation passed.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                ValidationIssue issue = issues[i];
                string prefix = string.IsNullOrWhiteSpace(issue.SubjectId)
                    ? "[Canteen]"
                    : "[Canteen][" + issue.SubjectId + "]";
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

        private static void ValidateMenu(
            CampusCanteenMenuProfile menu,
            bool usingRuntimeFallbackMenu,
            List<ValidationIssue> issues)
        {
            if (menu == null)
            {
                issues.Add(Issue(ValidationSeverity.Error, "menu", "Canteen menu profile is missing."));
                return;
            }

            if (usingRuntimeFallbackMenu)
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    "menu",
                    "Canteen service is using the runtime fallback menu. Assign a CampusCanteenMenuProfile for normal gameplay."));
            }

            IReadOnlyList<CampusCanteenDishDefinition> dishes = menu.Items;
            if (dishes == null || dishes.Count == 0)
            {
                issues.Add(Issue(ValidationSeverity.Error, "menu", "Canteen menu has no dish definitions."));
                return;
            }

            HashSet<string> dishIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dishes.Count; i++)
            {
                CampusCanteenDishDefinition dish = dishes[i];
                string subjectId = dish != null && !string.IsNullOrWhiteSpace(dish.name)
                    ? dish.name
                    : "menu[" + i + "]";

                if (dish == null)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Null dish definition in canteen menu."));
                    continue;
                }

                string dishId = NormalizeId(dish.ResolveDishId());
                if (string.IsNullOrEmpty(dishId))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Dish has no stable dish id."));
                }
                else if (!dishIds.Add(dishId))
                {
                    issues.Add(Issue(ValidationSeverity.Error, dishId, "Duplicate canteen dish id."));
                }

                if (string.IsNullOrWhiteSpace(dish.DishId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "DishId is empty. Normal mod data should use an explicit stable id."));
                }

                if (string.IsNullOrWhiteSpace(dish.StorageDefinitionId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "StorageDefinitionId is empty. Runtime will fall back to DishId."));
                }

                if (!dish.LocalizedDisplayName.HasAnyText)
                {
                    issues.Add(Issue(
                        ValidationSeverity.Error,
                        subjectId,
                        "LocalizedDisplayName is required. DisplayName is migration-only."));
                }

                if (!dish.LocalizedDescription.HasAnyText)
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "LocalizedDescription is empty. Description is migration-only."));
                }

                if (dish.Width <= 0 || dish.Height <= 0)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Dish footprint must be positive."));
                }

                if (dish.Weight < 0f)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Dish weight cannot be negative."));
                }

                if (dish.Price < 0)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Dish price cannot be negative."));
                }

                if (dish.PrepareSeconds <= 0f)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "PrepareSeconds must be positive."));
                }

                if (dish.SuspicionRisk < 0)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "SuspicionRisk cannot be negative."));
                }

                if (string.IsNullOrWhiteSpace(dish.WindowTypeId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "WindowTypeId is empty. Runtime will treat this dish as generic."));
                }
            }
        }

        private static void ValidateStations(
            IReadOnlyList<CampusCanteenStation> stations,
            List<ValidationIssue> issues)
        {
            if (stations == null)
            {
                issues.Add(Issue(ValidationSeverity.Error, "stations", "Canteen station list is missing."));
                return;
            }

            if (stations.Count == 0)
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    "stations",
                    "No canteen stations were built. Add CanteenServingWindow facilities in a Canteen room."));
                return;
            }

            HashSet<string> stationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation station = stations[i];
                string subjectId = station != null && !string.IsNullOrWhiteSpace(station.StationId)
                    ? station.StationId
                    : "station[" + i + "]";

                if (station == null)
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Null canteen station."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(station.StationId))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "StationId is required."));
                }
                else if (!stationIds.Add(NormalizeId(station.StationId)))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "Duplicate canteen station id."));
                }

                if (string.IsNullOrWhiteSpace(station.RoomId))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, subjectId, "Station has no RoomId."));
                }

                if (string.IsNullOrWhiteSpace(station.DisplayName))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, subjectId, "Station DisplayName is empty."));
                }

                if (string.IsNullOrWhiteSpace(station.WindowTypeId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "Station WindowTypeId is empty. Runtime will treat it as generic."));
                }

                if (string.IsNullOrWhiteSpace(station.WindowFacilityId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Error,
                        subjectId,
                        "WindowFacilityId is required. Do not rely on generated window ids for normal gameplay."));
                }

                if (station.WindowObject == null)
                {
                    issues.Add(Issue(
                        ValidationSeverity.Error,
                        subjectId,
                        "WindowObject is required because player and NPC actions press E on the same placed object."));
                }

                if (string.IsNullOrWhiteSpace(station.CounterFacilityId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "CounterFacilityId is empty. Meal drop position will use the serving window fallback."));
                }

                if (string.IsNullOrWhiteSpace(station.ClerkStandFacilityId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "ClerkStandFacilityId is empty. Clerk action position will use the serving window fallback."));
                }

                if (string.IsNullOrWhiteSpace(station.CustomerPickupFacilityId))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "CustomerPickupFacilityId is empty. Customer action position will use the serving window fallback."));
                }

                if (string.IsNullOrWhiteSpace(station.CounterContainerId))
                {
                    issues.Add(Issue(ValidationSeverity.Error, subjectId, "CounterContainerId is required."));
                }

                if (!station.HasFoodBox)
                {
                    issues.Add(Issue(
                        ValidationSeverity.Info,
                        subjectId,
                        "Station has no food box. This is valid for simple serving, but stock-box actions will be unavailable."));
                    continue;
                }

                if (station.FoodBoxObject == null)
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        subjectId,
                        "FoodBoxObject is missing although the station says it has a food box."));
                }

                if (string.IsNullOrWhiteSpace(station.FoodBoxFacilityId))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, subjectId, "FoodBoxFacilityId is empty."));
                }

                if (string.IsNullOrWhiteSpace(station.FoodBoxContainerId))
                {
                    issues.Add(Issue(ValidationSeverity.Warning, subjectId, "FoodBoxContainerId is empty."));
                }
            }
        }

        private static void ValidateMenuCoverage(
            CampusCanteenMenuProfile menu,
            IReadOnlyList<CampusCanteenStation> stations,
            List<ValidationIssue> issues)
        {
            if (menu == null || menu.Items == null || stations == null || stations.Count == 0)
            {
                return;
            }

            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation station = stations[i];
                if (station == null)
                {
                    continue;
                }

                if (!CanAnyDishServeStation(menu, station))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        station.StationId,
                        "No menu dish can be served at this station WindowTypeId."));
                }
            }

            IReadOnlyList<CampusCanteenDishDefinition> dishes = menu.Items;
            for (int i = 0; i < dishes.Count; i++)
            {
                CampusCanteenDishDefinition dish = dishes[i];
                if (dish == null || CanAnyStationServeDish(menu, dish, stations))
                {
                    continue;
                }

                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    dish.ResolveDishId(),
                    "No canteen station can serve this dish WindowTypeId."));
            }
        }

        private static bool CanAnyDishServeStation(CampusCanteenMenuProfile menu, CampusCanteenStation station)
        {
            IReadOnlyList<CampusCanteenDishDefinition> dishes = menu.Items;
            for (int i = 0; i < dishes.Count; i++)
            {
                CampusCanteenDishDefinition dish = dishes[i];
                if (menu.CanServeDishAtWindow(dish, station.WindowTypeId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanAnyStationServeDish(
            CampusCanteenMenuProfile menu,
            CampusCanteenDishDefinition dish,
            IReadOnlyList<CampusCanteenStation> stations)
        {
            string dishWindowTypeId = NormalizeWindowTypeId(dish.ResolveWindowTypeId());
            if (dishWindowTypeId == GenericWindowTypeId)
            {
                return true;
            }

            for (int i = 0; i < stations.Count; i++)
            {
                CampusCanteenStation station = stations[i];
                if (station != null && menu.CanServeDishAtWindow(dish, station.WindowTypeId))
                {
                    return true;
                }
            }

            return false;
        }

        private static ValidationIssue Issue(ValidationSeverity severity, string subjectId, string message)
        {
            return new ValidationIssue(severity, subjectId, message);
        }

        private static string NormalizeWindowTypeId(string value)
        {
            string id = NormalizeId(value);
            return string.IsNullOrEmpty(id) ? GenericWindowTypeId : id;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
