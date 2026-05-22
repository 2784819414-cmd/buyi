using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal sealed class CampusRuntimeGameplayMarkerPreset
    {
        public readonly string ChineseLabel;
        public readonly string EnglishLabel;
        public readonly string ChineseDisplayName;
        public readonly string EnglishDisplayName;
        public readonly CampusRoomType RoomType;
        public readonly CampusFacilityType FacilityType;
        public readonly Color Color;
        public readonly bool RequiresOwnerFacility;
        public readonly CampusFacilityType[] AllowedOwnerFacilityTypes;

        private CampusRuntimeGameplayMarkerPreset(
            string chineseLabel,
            string englishLabel,
            string chineseDisplayName,
            string englishDisplayName,
            CampusRoomType roomType,
            CampusFacilityType facilityType,
            Color color,
            bool requiresOwnerFacility,
            CampusFacilityType[] allowedOwnerFacilityTypes)
        {
            ChineseLabel = chineseLabel;
            EnglishLabel = englishLabel;
            ChineseDisplayName = chineseDisplayName;
            EnglishDisplayName = englishDisplayName;
            RoomType = roomType;
            FacilityType = facilityType;
            Color = color;
            RequiresOwnerFacility = requiresOwnerFacility;
            AllowedOwnerFacilityTypes = allowedOwnerFacilityTypes ?? Array.Empty<CampusFacilityType>();
        }

        public static CampusRuntimeGameplayMarkerPreset FacilityPoint(
            string chineseLabel,
            string englishLabel,
            CampusFacilityType facilityType,
            Color color,
            bool requiresOwnerFacility = false,
            CampusFacilityType[] allowedOwnerFacilityTypes = null)
        {
            return new CampusRuntimeGameplayMarkerPreset(
                chineseLabel,
                englishLabel,
                chineseLabel,
                englishLabel,
                CampusRoomType.Unknown,
                facilityType,
                color,
                requiresOwnerFacility,
                allowedOwnerFacilityTypes);
        }

        public bool AcceptsOwnerFacilityType(CampusFacilityType facilityType)
        {
            if (!RequiresOwnerFacility || AllowedOwnerFacilityTypes == null)
            {
                return false;
            }

            for (int i = 0; i < AllowedOwnerFacilityTypes.Length; i++)
            {
                if (AllowedOwnerFacilityTypes[i] == facilityType)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class CampusRuntimeGameplayMarkerPresetCatalog
    {
        private static CampusRuntimeGameplayMarkerPreset[] cachedPresets;

        internal static CampusRuntimeGameplayMarkerPreset[] Presets
        {
            get
            {
                if (cachedPresets == null)
                {
                    cachedPresets = LoadPresets();
                }

                return cachedPresets;
            }
        }

        internal static bool TryGetPreset(
            CampusFacilityType facilityType,
            out CampusRuntimeGameplayMarkerPreset preset)
        {
            CampusRuntimeGameplayMarkerPreset[] presets = Presets;
            for (int i = 0; i < presets.Length; i++)
            {
                CampusRuntimeGameplayMarkerPreset candidate = presets[i];
                if (candidate != null && candidate.FacilityType == facilityType)
                {
                    preset = candidate;
                    return true;
                }
            }

            preset = null;
            return false;
        }

        private static readonly CampusRuntimeGameplayMarkerPreset[] BuiltInPresets =
        {
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u670d\u52a1\u7a97\u53e3",
                "Service Window",
                CampusFacilityType.ServiceWindow,
                new Color(0.16f, 0.78f, 0.78f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u64cd\u4f5c\u5458\u4f4d",
                "Operator Slot",
                CampusFacilityType.WorkerStandPoint,
                new Color(0.16f, 0.58f, 0.67f, 1f),
                true,
                new[] { CampusFacilityType.ServiceWindow }),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u987e\u5ba2\u4f4d",
                "Customer Slot",
                CampusFacilityType.PickupPoint,
                new Color(0.48f, 0.82f, 0.62f, 1f),
                true,
                new[] { CampusFacilityType.ServiceWindow }),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u6392\u961f\u4f4d",
                "Queue Slot",
                CampusFacilityType.WaitingPoint,
                new Color(0.95f, 0.76f, 0.28f, 1f),
                true,
                new[] { CampusFacilityType.ServiceWindow }),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u653e\u7f6e\u4f4d",
                "Drop Slot",
                CampusFacilityType.DropPoint,
                new Color(0.32f, 0.54f, 0.98f, 1f),
                true,
                new[] { CampusFacilityType.ServiceWindow })
        };

        private static CampusRuntimeGameplayMarkerPreset[] LoadPresets()
        {
            if (!CampusRuntimeModPresetStore.TryReadJson("GameplayMarkerPresets.json", out string json))
            {
                return BuiltInPresets;
            }

            try
            {
                GameplayMarkerPresetFile file = JsonUtility.FromJson<GameplayMarkerPresetFile>(json);
                if (file == null || file.Presets == null || file.Presets.Count == 0)
                {
                    return BuiltInPresets;
                }

                List<CampusRuntimeGameplayMarkerPreset> loaded = new List<CampusRuntimeGameplayMarkerPreset>();
                for (int i = 0; i < file.Presets.Count; i++)
                {
                    GameplayMarkerPresetRecord record = file.Presets[i];
                    if (record == null)
                    {
                        continue;
                    }

                    string chinese = string.IsNullOrWhiteSpace(record.ChineseLabel) ? record.EnglishLabel : record.ChineseLabel;
                    string english = string.IsNullOrWhiteSpace(record.EnglishLabel) ? record.ChineseLabel : record.EnglishLabel;
                    if (string.IsNullOrWhiteSpace(chinese) && string.IsNullOrWhiteSpace(english))
                    {
                        continue;
                    }

                    Color color = CampusRuntimeModPresetStore.ParseColor(record.Color, new Color(0.5f, 0.65f, 0.9f, 1f));
                    CampusFacilityType facilityType = ParseEnum(record.FacilityType, CampusFacilityType.Unknown);
                    if (facilityType == CampusFacilityType.Unknown)
                    {
                        continue;
                    }

                    CampusFacilityType[] ownerFacilityTypes = ParseFacilityTypes(record.OwnerFacilityTypes);

                    loaded.Add(CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                        chinese,
                        english,
                        facilityType,
                        color,
                        record.RequiresOwnerFacility,
                        ownerFacilityTypes));
                }

                return loaded.Count > 0 ? loaded.ToArray() : BuiltInPresets;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusRuntimeGameplayMarkerPresetCatalog] Failed to load GameplayMarkerPresets.json: " + exception.Message);
                return BuiltInPresets;
            }
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Enum.TryParse(value.Trim(), true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        private static CampusFacilityType[] ParseFacilityTypes(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<CampusFacilityType>();
            }

            List<CampusFacilityType> result = new List<CampusFacilityType>();
            for (int i = 0; i < values.Length; i++)
            {
                CampusFacilityType parsed = ParseEnum(values[i], CampusFacilityType.Unknown);
                if (parsed != CampusFacilityType.Unknown)
                {
                    result.Add(parsed);
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<CampusFacilityType>();
        }

        [Serializable]
        private sealed class GameplayMarkerPresetFile
        {
            public List<GameplayMarkerPresetRecord> Presets = new List<GameplayMarkerPresetRecord>();
        }

        [Serializable]
        private sealed class GameplayMarkerPresetRecord
        {
            public string ChineseLabel = string.Empty;
            public string EnglishLabel = string.Empty;
            public string FacilityType = string.Empty;
            public string Color = string.Empty;
            public bool RequiresOwnerFacility = false;
            public string[] OwnerFacilityTypes = Array.Empty<string>();
        }
    }
}
