using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Services
{
    internal static class CampusServiceStationSlotRoleIds
    {
        public const string Operator = "operator";
        public const string Customer = "customer";
        public const string Queue = "queue";
        public const string Output = "output";
    }

    internal sealed class CampusServiceStationSlotDefinition
    {
        public readonly string RoleId;
        public readonly CampusFacilityType[] FacilityTypes;
        public readonly int MinCount;
        public readonly int MaxCount;
        public readonly bool NavigationTargetWhenAvailable;
        public readonly bool NavigationTargetWhenWaiting;

        public CampusServiceStationSlotDefinition(
            string roleId,
            CampusFacilityType[] facilityTypes,
            int minCount,
            int maxCount,
            bool navigationTargetWhenAvailable,
            bool navigationTargetWhenWaiting)
        {
            RoleId = NormalizeId(roleId);
            FacilityTypes = facilityTypes ?? Array.Empty<CampusFacilityType>();
            MinCount = Mathf.Max(0, minCount);
            MaxCount = maxCount <= 0 ? int.MaxValue : Mathf.Max(MinCount, maxCount);
            NavigationTargetWhenAvailable = navigationTargetWhenAvailable;
            NavigationTargetWhenWaiting = navigationTargetWhenWaiting;
        }

        public bool Accepts(CampusFacilityType facilityType)
        {
            if (FacilityTypes.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < FacilityTypes.Length; i++)
            {
                if (FacilityTypes[i] == facilityType)
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal sealed class CampusServiceStationTypeDefinition
    {
        private readonly Dictionary<string, CampusServiceStationSlotDefinition> slotsByRoleId =
            new Dictionary<string, CampusServiceStationSlotDefinition>(StringComparer.OrdinalIgnoreCase);

        public readonly string StationTypeId;
        public readonly CampusLocalizedText DisplayName;
        public readonly string InteractionActionId;
        public readonly string AvailabilityRuleId;
        public readonly CampusRoomType[] AllowedRoomTypes;
        public readonly CampusFacilityType[] OwnerFacilityTypes;
        public readonly IReadOnlyList<CampusServiceStationSlotDefinition> Slots;

        public CampusServiceStationTypeDefinition(
            string stationTypeId,
            CampusLocalizedText displayName,
            string interactionActionId,
            string availabilityRuleId,
            CampusRoomType[] allowedRoomTypes,
            CampusFacilityType[] ownerFacilityTypes,
            List<CampusServiceStationSlotDefinition> slots)
        {
            StationTypeId = NormalizeId(stationTypeId);
            DisplayName = displayName;
            InteractionActionId = CampusInteractionActionIds.Normalize(interactionActionId);
            AvailabilityRuleId = NormalizeId(availabilityRuleId);
            AllowedRoomTypes = allowedRoomTypes ?? Array.Empty<CampusRoomType>();
            OwnerFacilityTypes = ownerFacilityTypes ?? Array.Empty<CampusFacilityType>();
            Slots = slots ?? new List<CampusServiceStationSlotDefinition>();

            for (int i = 0; i < Slots.Count; i++)
            {
                CampusServiceStationSlotDefinition slot = Slots[i];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.RoleId))
                {
                    slotsByRoleId[slot.RoleId] = slot;
                }
            }
        }

        public bool TryGetSlot(string roleId, out CampusServiceStationSlotDefinition slot)
        {
            return slotsByRoleId.TryGetValue(NormalizeId(roleId), out slot);
        }

        public bool AcceptsRoomType(CampusRoomType roomType)
        {
            if (AllowedRoomTypes.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < AllowedRoomTypes.Length; i++)
            {
                if (AllowedRoomTypes[i] == roomType)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AcceptsOwnerFacilityType(CampusFacilityType facilityType)
        {
            if (OwnerFacilityTypes.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < OwnerFacilityTypes.Length; i++)
            {
                if (OwnerFacilityTypes[i] == facilityType)
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal static class CampusServiceStationPresetCatalog
    {
        private const string PresetFileName = "ServiceStationPresets.json";
        public const string AvailabilityAlways = "always_available";
        public const string AvailabilityCanteenOperatorMealPeak = "canteen_operator_present_meal_peak";

        private static Dictionary<string, CampusServiceStationTypeDefinition> definitionsById;

        public static IReadOnlyCollection<CampusServiceStationTypeDefinition> Definitions
        {
            get
            {
                EnsureLoaded();
                return definitionsById.Values;
            }
        }

        public static bool TryResolve(
            string stationTypeId,
            out CampusServiceStationTypeDefinition definition)
        {
            EnsureLoaded();
            return definitionsById.TryGetValue(NormalizeId(stationTypeId), out definition);
        }

        private static void EnsureLoaded()
        {
            if (definitionsById != null)
            {
                return;
            }

            definitionsById = LoadDefinitions();
        }

        private static Dictionary<string, CampusServiceStationTypeDefinition> LoadDefinitions()
        {
            Dictionary<string, CampusServiceStationTypeDefinition> definitions =
                new Dictionary<string, CampusServiceStationTypeDefinition>(StringComparer.OrdinalIgnoreCase);
            if (!CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                AddBuiltIns(definitions);
                return definitions;
            }

            try
            {
                ServiceStationPresetFile file = JsonUtility.FromJson<ServiceStationPresetFile>(json);
                if (file != null && file.ServiceStations != null)
                {
                    for (int i = 0; i < file.ServiceStations.Count; i++)
                    {
                        CampusServiceStationTypeDefinition definition =
                            BuildDefinition(file.ServiceStations[i]);
                        if (definition != null && !string.IsNullOrWhiteSpace(definition.StationTypeId))
                        {
                            definitions[definition.StationTypeId] = definition;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusServiceStationPresetCatalog] Failed to parse " + PresetFileName + ": " + exception.Message);
            }

            if (definitions.Count == 0)
            {
                AddBuiltIns(definitions);
            }

            return definitions;
        }

        private static void AddBuiltIns(Dictionary<string, CampusServiceStationTypeDefinition> definitions)
        {
            CampusServiceStationTypeDefinition canteen = new CampusServiceStationTypeDefinition(
                "canteen_meal_window",
                new CampusLocalizedText("食堂打饭窗口", "Canteen Meal Window"),
                CampusInteractionActionIds.ServiceWindowUse,
                AvailabilityCanteenOperatorMealPeak,
                new[] { CampusRoomType.ServiceArea },
                new[] { CampusFacilityType.ServiceWindow },
                new List<CampusServiceStationSlotDefinition>
                {
                    new CampusServiceStationSlotDefinition(
                        CampusServiceStationSlotRoleIds.Operator,
                        new[] { CampusFacilityType.WorkerStandPoint },
                        1,
                        1,
                        false,
                        false),
                    new CampusServiceStationSlotDefinition(
                        CampusServiceStationSlotRoleIds.Customer,
                        new[] { CampusFacilityType.PickupPoint },
                        1,
                        1,
                        true,
                        true),
                    new CampusServiceStationSlotDefinition(
                        CampusServiceStationSlotRoleIds.Queue,
                        new[] { CampusFacilityType.WaitingPoint },
                        0,
                        99,
                        false,
                        true),
                    new CampusServiceStationSlotDefinition(
                        CampusServiceStationSlotRoleIds.Output,
                        new[] { CampusFacilityType.DropPoint },
                        0,
                        1,
                        false,
                        false)
                });
            definitions[canteen.StationTypeId] = canteen;

            CampusServiceStationTypeDefinition retail = new CampusServiceStationTypeDefinition(
                "retail_checkout",
                new CampusLocalizedText("零售收银台", "Retail Checkout"),
                NtingCampus.Gameplay.Retail.CampusRetailActionIds.Checkout,
                AvailabilityAlways,
                new[] { CampusRoomType.RetailArea },
                new[] { CampusFacilityType.CheckoutPoint },
                new List<CampusServiceStationSlotDefinition>());
            definitions[retail.StationTypeId] = retail;
        }

        private static CampusServiceStationTypeDefinition BuildDefinition(ServiceStationPresetRecord record)
        {
            if (record == null)
            {
                return null;
            }

            List<CampusServiceStationSlotDefinition> slots = new List<CampusServiceStationSlotDefinition>();
            if (record.Slots != null)
            {
                for (int i = 0; i < record.Slots.Count; i++)
                {
                    CampusServiceStationSlotDefinition slot = BuildSlot(record.Slots[i]);
                    if (slot != null && !string.IsNullOrWhiteSpace(slot.RoleId))
                    {
                        slots.Add(slot);
                    }
                }
            }

            return new CampusServiceStationTypeDefinition(
                record.StationTypeId,
                record.DisplayName,
                record.InteractionActionId,
                string.IsNullOrWhiteSpace(record.AvailabilityRuleId)
                    ? AvailabilityAlways
                    : record.AvailabilityRuleId,
                ParseRoomTypes(record.AllowedRoomTypes),
                ParseFacilityTypes(record.OwnerFacilityTypes),
                slots);
        }

        private static CampusServiceStationSlotDefinition BuildSlot(ServiceStationSlotPresetRecord record)
        {
            if (record == null)
            {
                return null;
            }

            return new CampusServiceStationSlotDefinition(
                record.RoleId,
                ParseFacilityTypes(record.FacilityTypes),
                record.MinCount,
                record.MaxCount,
                record.NavigationTargetWhenAvailable,
                record.NavigationTargetWhenWaiting);
        }

        private static CampusRoomType[] ParseRoomTypes(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return Array.Empty<CampusRoomType>();
            }

            List<CampusRoomType> roomTypes = new List<CampusRoomType>();
            for (int i = 0; i < ids.Length; i++)
            {
                if (Enum.TryParse(ids[i], true, out CampusRoomType roomType) &&
                    roomType != CampusRoomType.Unknown)
                {
                    roomTypes.Add(roomType);
                }
            }

            return roomTypes.ToArray();
        }

        private static CampusFacilityType[] ParseFacilityTypes(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return Array.Empty<CampusFacilityType>();
            }

            List<CampusFacilityType> facilityTypes = new List<CampusFacilityType>();
            for (int i = 0; i < ids.Length; i++)
            {
                if (Enum.TryParse(ids[i], true, out CampusFacilityType facilityType) &&
                    facilityType != CampusFacilityType.Unknown)
                {
                    facilityTypes.Add(facilityType);
                }
            }

            return facilityTypes.ToArray();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        [Serializable]
        private sealed class ServiceStationPresetFile
        {
            public List<ServiceStationPresetRecord> ServiceStations =
                new List<ServiceStationPresetRecord>();
        }

        [Serializable]
        private sealed class ServiceStationPresetRecord
        {
            public string StationTypeId = string.Empty;
            public CampusLocalizedText DisplayName = default;
            public string InteractionActionId = string.Empty;
            public string AvailabilityRuleId = string.Empty;
            public string[] AllowedRoomTypes = Array.Empty<string>();
            public string[] OwnerFacilityTypes = Array.Empty<string>();
            public List<ServiceStationSlotPresetRecord> Slots =
                new List<ServiceStationSlotPresetRecord>();
        }

        [Serializable]
        private sealed class ServiceStationSlotPresetRecord
        {
            public string RoleId = string.Empty;
            public string[] FacilityTypes = Array.Empty<string>();
            public int MinCount = 0;
            public int MaxCount = 0;
            public bool NavigationTargetWhenAvailable = false;
            public bool NavigationTargetWhenWaiting = false;
        }
    }
}
