using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal sealed class CampusRuntimeGameplayMarkerPreset
    {
        public readonly CampusLocalizedTextEntry Label;
        public readonly CampusLocalizedTextEntry DisplayName;
        public readonly CampusRoomType RoomType;
        public readonly CampusFacilityType FacilityType;
        public readonly Color Color;
        public readonly bool RequiresOwnerFacility;
        public readonly CampusFacilityType[] AllowedOwnerFacilityTypes;

        private CampusRuntimeGameplayMarkerPreset(
            CampusLocalizedTextEntry label,
            CampusLocalizedTextEntry displayName,
            CampusRoomType roomType,
            CampusFacilityType facilityType,
            Color color,
            bool requiresOwnerFacility,
            CampusFacilityType[] allowedOwnerFacilityTypes)
        {
            Label = label;
            DisplayName = displayName.HasAnyText ? displayName : label;
            RoomType = roomType;
            FacilityType = facilityType;
            Color = color;
            RequiresOwnerFacility = requiresOwnerFacility;
            AllowedOwnerFacilityTypes = allowedOwnerFacilityTypes ?? Array.Empty<CampusFacilityType>();
        }

        public static CampusRuntimeGameplayMarkerPreset FacilityPoint(
            CampusLocalizedTextEntry label,
            CampusLocalizedTextEntry displayName,
            CampusFacilityType facilityType,
            Color color,
            bool requiresOwnerFacility = false,
            CampusFacilityType[] allowedOwnerFacilityTypes = null)
        {
            return new CampusRuntimeGameplayMarkerPreset(
                label,
                displayName,
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
                new CampusLocalizedTextEntry("\u670d\u52a1\u7a97\u53e3", "Service Window", "\u670d\u52d9\u7a97\u53e3", "\u041e\u043a\u043d\u043e \u043e\u0431\u0441\u043b\u0443\u0436\u0438\u0432\u0430\u043d\u0438\u044f", "\u30b5\u30fc\u30d3\u30b9\u7a93\u53e3"),
                new CampusLocalizedTextEntry("\u670d\u52a1\u7a97\u53e3", "Service Window", "\u670d\u52d9\u7a97\u53e3", "\u041e\u043a\u043d\u043e \u043e\u0431\u0441\u043b\u0443\u0436\u0438\u0432\u0430\u043d\u0438\u044f", "\u30b5\u30fc\u30d3\u30b9\u7a93\u53e3"),
                CampusFacilityType.ServiceWindow,
                new Color(0.16f, 0.78f, 0.78f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                new CampusLocalizedTextEntry("\u64cd\u4f5c\u5458\u4f4d", "Operator Slot", "\u64cd\u4f5c\u54e1\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u043e\u043f\u0435\u0440\u0430\u0442\u043e\u0440\u0430", "\u62c5\u5f53\u8005\u4f4d\u7f6e"),
                new CampusLocalizedTextEntry("\u64cd\u4f5c\u5458\u4f4d", "Operator Slot", "\u64cd\u4f5c\u54e1\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u043e\u043f\u0435\u0440\u0430\u0442\u043e\u0440\u0430", "\u62c5\u5f53\u8005\u4f4d\u7f6e"),
                CampusFacilityType.WorkerStandPoint,
                new Color(0.16f, 0.58f, 0.67f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                new CampusLocalizedTextEntry("\u987e\u5ba2\u4f4d", "Customer Slot", "\u9867\u5ba2\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u043a\u043b\u0438\u0435\u043d\u0442\u0430", "\u5229\u7528\u8005\u4f4d\u7f6e"),
                new CampusLocalizedTextEntry("\u987e\u5ba2\u4f4d", "Customer Slot", "\u9867\u5ba2\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u043a\u043b\u0438\u0435\u043d\u0442\u0430", "\u5229\u7528\u8005\u4f4d\u7f6e"),
                CampusFacilityType.PickupPoint,
                new Color(0.48f, 0.82f, 0.62f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                new CampusLocalizedTextEntry("\u6392\u961f\u4f4d", "Queue Slot", "\u6392\u968a\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u043e\u0447\u0435\u0440\u0435\u0434\u0438", "\u5f85\u6a5f\u5217\u4f4d\u7f6e"),
                new CampusLocalizedTextEntry("\u6392\u961f\u4f4d", "Queue Slot", "\u6392\u968a\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u043e\u0447\u0435\u0440\u0435\u0434\u0438", "\u5f85\u6a5f\u5217\u4f4d\u7f6e"),
                CampusFacilityType.WaitingPoint,
                new Color(0.95f, 0.76f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                new CampusLocalizedTextEntry("\u653e\u7f6e\u4f4d", "Drop Slot", "\u653e\u7f6e\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u0432\u044b\u043a\u043b\u0430\u0434\u043a\u0438", "\u914d\u7f6e\u4f4d\u7f6e"),
                new CampusLocalizedTextEntry("\u653e\u7f6e\u4f4d", "Drop Slot", "\u653e\u7f6e\u4f4d", "\u041c\u0435\u0441\u0442\u043e \u0432\u044b\u043a\u043b\u0430\u0434\u043a\u0438", "\u914d\u7f6e\u4f4d\u7f6e"),
                CampusFacilityType.DropPoint,
                new Color(0.32f, 0.54f, 0.98f, 1f))
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

                    CampusLocalizedTextEntry label = BuildTextEntry(
                        record.ChineseLabel,
                        record.EnglishLabel,
                        record.TraditionalChineseLabel,
                        record.RussianLabel,
                        record.JapaneseLabel);
                    if (!label.HasAnyText)
                    {
                        continue;
                    }

                    CampusLocalizedTextEntry displayName = BuildTextEntry(
                        record.ChineseDisplayName,
                        record.EnglishDisplayName,
                        record.TraditionalChineseDisplayName,
                        record.RussianDisplayName,
                        record.JapaneseDisplayName);

                    Color color = CampusRuntimeModPresetStore.ParseColor(record.Color, new Color(0.5f, 0.65f, 0.9f, 1f));
                    CampusFacilityType facilityType = ParseEnum(record.FacilityType, CampusFacilityType.Unknown);
                    if (facilityType == CampusFacilityType.Unknown)
                    {
                        continue;
                    }

                    CampusFacilityType[] ownerFacilityTypes = ParseFacilityTypes(record.OwnerFacilityTypes);

                    loaded.Add(CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                        label,
                        displayName,
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

        private static CampusLocalizedTextEntry BuildTextEntry(
            string chinese,
            string english,
            string traditionalChinese,
            string russian,
            string japanese)
        {
            return new CampusLocalizedTextEntry(
                Clean(chinese),
                Clean(english),
                Clean(traditionalChinese),
                Clean(russian),
                Clean(japanese));
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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
            public string TraditionalChineseLabel = string.Empty;
            public string RussianLabel = string.Empty;
            public string JapaneseLabel = string.Empty;
            public string ChineseDisplayName = string.Empty;
            public string EnglishDisplayName = string.Empty;
            public string TraditionalChineseDisplayName = string.Empty;
            public string RussianDisplayName = string.Empty;
            public string JapaneseDisplayName = string.Empty;
            public string FacilityType = string.Empty;
            public string Color = string.Empty;
            public bool RequiresOwnerFacility = false;
            public string[] OwnerFacilityTypes = Array.Empty<string>();
        }
    }
}
