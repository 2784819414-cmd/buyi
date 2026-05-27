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

    internal enum CampusServiceStationAvailabilityMode
    {
        Always = 0,
        RequiresAssignedOperator = 1
    }

    internal sealed class CampusServiceStationAvailabilityDefinition
    {
        public static readonly CampusServiceStationAvailabilityDefinition Always =
            new CampusServiceStationAvailabilityDefinition(
                CampusServiceStationAvailabilityMode.Always,
                Array.Empty<string>(),
                CampusServiceStationSlotRoleIds.Operator,
                0.85f,
                default);

        public readonly CampusServiceStationAvailabilityMode Mode;
        public readonly string[] ScheduleWindows;
        public readonly string OperatorSlotRoleId;
        public readonly float OperatorActivationRadius;
        public readonly CampusLocalizedText UnavailableText;

        public CampusServiceStationAvailabilityDefinition(
            CampusServiceStationAvailabilityMode mode,
            string[] scheduleWindows,
            string operatorSlotRoleId,
            float operatorActivationRadius,
            CampusLocalizedText unavailableText)
        {
            Mode = mode;
            ScheduleWindows = NormalizeIds(scheduleWindows);
            OperatorSlotRoleId = string.IsNullOrWhiteSpace(operatorSlotRoleId)
                ? CampusServiceStationSlotRoleIds.Operator
                : operatorSlotRoleId.Trim();
            OperatorActivationRadius = Mathf.Max(0.05f, operatorActivationRadius);
            UnavailableText = unavailableText;
        }

        private static string[] NormalizeIds(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            List<string> normalized = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    normalized.Add(values[i].Trim());
                }
            }

            return normalized.ToArray();
        }
    }

    internal enum CampusServiceStationClearanceMode
    {
        None = 0,
        ClearPendingProtectedTransfers = 1
    }

    internal enum CampusServiceStationClearancePriceMode
    {
        Free = 0,
        ItemPrice = 1
    }

    internal sealed class CampusServiceStationClearanceDefinition
    {
        public static readonly CampusServiceStationClearanceDefinition None =
            new CampusServiceStationClearanceDefinition(
                CampusServiceStationClearanceMode.None,
                CampusServiceStationClearancePriceMode.Free,
                default,
                default,
                default);

        public readonly CampusServiceStationClearanceMode Mode;
        public readonly CampusServiceStationClearancePriceMode PriceMode;
        public readonly CampusLocalizedText CompleteText;
        public readonly CampusLocalizedText NoPendingItemsText;
        public readonly CampusLocalizedText InsufficientFundsText;

        public CampusServiceStationClearanceDefinition(
            CampusServiceStationClearanceMode mode,
            CampusServiceStationClearancePriceMode priceMode,
            CampusLocalizedText completeText,
            CampusLocalizedText noPendingItemsText,
            CampusLocalizedText insufficientFundsText)
        {
            Mode = mode;
            PriceMode = priceMode;
            CompleteText = completeText;
            NoPendingItemsText = noPendingItemsText;
            InsufficientFundsText = insufficientFundsText;
        }

        public bool ClearsPendingProtectedTransfers =>
            Mode == CampusServiceStationClearanceMode.ClearPendingProtectedTransfers;
    }

    internal sealed class CampusServiceStationTypeDefinition
    {
        private readonly Dictionary<string, CampusServiceStationSlotDefinition> slotsByRoleId =
            new Dictionary<string, CampusServiceStationSlotDefinition>(StringComparer.OrdinalIgnoreCase);

        public readonly string StationTypeId;
        public readonly CampusLocalizedText DisplayName;
        public readonly string InteractionActionId;
        public readonly string AvailabilityRuleId;
        public readonly CampusServiceStationAvailabilityDefinition Availability;
        public readonly CampusServiceStationClearanceDefinition Clearance;
        public readonly CampusRoomType[] AllowedRoomTypes;
        public readonly CampusFacilityType[] OwnerFacilityTypes;
        public readonly IReadOnlyList<CampusServiceStationSlotDefinition> Slots;

        public CampusServiceStationTypeDefinition(
            string stationTypeId,
            CampusLocalizedText displayName,
            string interactionActionId,
            string availabilityRuleId,
            CampusServiceStationAvailabilityDefinition availability,
            CampusServiceStationClearanceDefinition clearance,
            CampusRoomType[] allowedRoomTypes,
            CampusFacilityType[] ownerFacilityTypes,
            List<CampusServiceStationSlotDefinition> slots)
        {
            StationTypeId = NormalizeId(stationTypeId);
            DisplayName = displayName;
            InteractionActionId = CampusInteractionActionIds.Normalize(interactionActionId);
            AvailabilityRuleId = NormalizeId(availabilityRuleId);
            Availability = availability ?? CampusServiceStationAvailabilityDefinition.Always;
            Clearance = clearance ?? CampusServiceStationClearanceDefinition.None;
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
                Debug.LogWarning(CampusServiceStationValidationTextCatalog.Format(
                    CampusServiceStationValidationTextId.FailedToParsePreset,
                    PresetFileName,
                    exception.Message));
            }

            return definitions;
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
                record.AvailabilityRuleId,
                BuildAvailability(record.Availability),
                BuildClearance(record.Clearance),
                ParseRoomTypes(record.AllowedRoomTypes),
                ParseFacilityTypes(record.OwnerFacilityTypes),
                slots);
        }

        private static CampusServiceStationClearanceDefinition BuildClearance(
            ServiceStationClearancePresetRecord record)
        {
            if (record == null)
            {
                return CampusServiceStationClearanceDefinition.None;
            }

            CampusServiceStationClearanceMode mode =
                Enum.TryParse(record.Mode, true, out CampusServiceStationClearanceMode parsedMode)
                    ? parsedMode
                    : CampusServiceStationClearanceMode.None;
            CampusServiceStationClearancePriceMode priceMode =
                Enum.TryParse(record.PriceMode, true, out CampusServiceStationClearancePriceMode parsedPriceMode)
                    ? parsedPriceMode
                    : CampusServiceStationClearancePriceMode.Free;
            return new CampusServiceStationClearanceDefinition(
                mode,
                priceMode,
                record.CompleteText,
                record.NoPendingItemsText,
                record.InsufficientFundsText);
        }

        private static CampusServiceStationAvailabilityDefinition BuildAvailability(
            ServiceStationAvailabilityPresetRecord record)
        {
            if (record == null)
            {
                return CampusServiceStationAvailabilityDefinition.Always;
            }

            CampusServiceStationAvailabilityMode mode =
                Enum.TryParse(record.Mode, true, out CampusServiceStationAvailabilityMode parsedMode)
                    ? parsedMode
                    : CampusServiceStationAvailabilityMode.Always;
            return new CampusServiceStationAvailabilityDefinition(
                mode,
                record.ScheduleWindows,
                record.OperatorSlotRoleId,
                record.OperatorActivationRadius,
                record.UnavailableText);
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
            public ServiceStationAvailabilityPresetRecord Availability = null;
            public ServiceStationClearancePresetRecord Clearance = null;
            public string[] AllowedRoomTypes = Array.Empty<string>();
            public string[] OwnerFacilityTypes = Array.Empty<string>();
            public List<ServiceStationSlotPresetRecord> Slots =
                new List<ServiceStationSlotPresetRecord>();
        }

        [Serializable]
        private sealed class ServiceStationClearancePresetRecord
        {
            public string Mode = string.Empty;
            public string PriceMode = string.Empty;
            public CampusLocalizedText CompleteText = default;
            public CampusLocalizedText NoPendingItemsText = default;
            public CampusLocalizedText InsufficientFundsText = default;
        }

        [Serializable]
        private sealed class ServiceStationAvailabilityPresetRecord
        {
            public string Mode = string.Empty;
            public string[] ScheduleWindows = Array.Empty<string>();
            public string OperatorSlotRoleId = string.Empty;
            public float OperatorActivationRadius = 0.85f;
            public CampusLocalizedText UnavailableText = default;
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
