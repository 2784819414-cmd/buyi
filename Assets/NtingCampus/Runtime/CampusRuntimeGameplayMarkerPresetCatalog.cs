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

        private CampusRuntimeGameplayMarkerPreset(
            string chineseLabel,
            string englishLabel,
            string chineseDisplayName,
            string englishDisplayName,
            CampusRoomType roomType,
            CampusFacilityType facilityType,
            Color color)
        {
            ChineseLabel = chineseLabel;
            EnglishLabel = englishLabel;
            ChineseDisplayName = chineseDisplayName;
            EnglishDisplayName = englishDisplayName;
            RoomType = roomType;
            FacilityType = facilityType;
            Color = color;
        }

        public static CampusRuntimeGameplayMarkerPreset FacilityPoint(
            string chineseLabel,
            string englishLabel,
            CampusFacilityType facilityType,
            Color color)
        {
            return new CampusRuntimeGameplayMarkerPreset(
                chineseLabel,
                englishLabel,
                chineseLabel,
                englishLabel,
                CampusRoomType.Unknown,
                facilityType,
                color);
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

        private static readonly CampusRuntimeGameplayMarkerPreset[] BuiltInPresets =
        {
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u95e8",
                "Door",
                CampusFacilityType.Door,
                new Color(0.72f, 0.72f, 0.72f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u9ed1\u677f",
                "Blackboard",
                CampusFacilityType.Blackboard,
                new Color(0.12f, 0.46f, 0.36f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8bfe\u684c",
                "Student Desk",
                CampusFacilityType.StudentDesk,
                new Color(0.26f, 0.56f, 0.96f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u8bb2\u53f0",
                "Podium",
                CampusFacilityType.Podium,
                new Color(0.18f, 0.38f, 0.86f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u529e\u516c\u684c",
                "Office Desk",
                CampusFacilityType.OfficeDesk,
                new Color(0.72f, 0.48f, 0.28f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5e8a",
                "Bed",
                CampusFacilityType.Bed,
                new Color(0.56f, 0.42f, 0.88f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u516c\u544a\u680f",
                "Bulletin Board",
                CampusFacilityType.BulletinBoard,
                new Color(0.88f, 0.62f, 0.24f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u62db\u52df\u70b9",
                "Recruitment",
                CampusFacilityType.Recruitment,
                new Color(0.74f, 0.36f, 0.88f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u6d17\u624b\u6c60",
                "Sink",
                CampusFacilityType.Sink,
                new Color(0.22f, 0.68f, 0.92f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u50a8\u7269\u70b9",
                "Storage",
                CampusFacilityType.Storage,
                new Color(0.62f, 0.56f, 0.46f, 1f)),
            CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                "\u5403\u996d\u533a\u5ea7\u4f4d",
                "Dining Table",
                CampusFacilityType.DiningTable,
                new Color(0.55f, 0.76f, 0.28f, 1f))
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

                    loaded.Add(CampusRuntimeGameplayMarkerPreset.FacilityPoint(
                        chinese,
                        english,
                        facilityType,
                        color));
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
        }
    }
}
