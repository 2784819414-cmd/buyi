using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeGameplayActorPresetCatalog
    {
        private static CampusRuntimeGameplayActorPreset[] cachedPresets;

        internal static CampusRuntimeGameplayActorPreset[] Presets
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

        private static readonly CampusRuntimeGameplayActorPreset[] BuiltInPresets =
        {
            new CampusRuntimeGameplayActorPreset(
                "\u5b66\u751f",
                "Student",
                "\u5b66\u751f",
                "Student",
                "student",
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                "class_1",
                40,
                20,
                60,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.38f, 0.56f, 0.83f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                "\u6363\u86cb\u5b66\u751f",
                "Troublemaker",
                "\u6363\u86cb\u5b66\u751f",
                "Troublemaker",
                "student_trouble",
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                "class_1",
                28,
                68,
                45,
                new[] { CampusCharacterTrait.Troublemaker },
                new Color(0.62f, 0.42f, 0.66f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                "\u597d\u5b66\u751f",
                "Good Student",
                "\u597d\u5b66\u751f",
                "GoodStudent",
                "student_good",
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                "class_1",
                24,
                12,
                80,
                new[] { CampusCharacterTrait.GoodStudent },
                new Color(0.42f, 0.62f, 0.48f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                "\u8bed\u6587\u8001\u5e08",
                "World Teacher",
                "\u8bed\u6587\u8001\u5e08",
                "WorldTeacher",
                "teacher_world",
                CampusCharacterRole.Teacher,
                CampusTeacherDuty.WorldLanguageTeacher,
                CampusStaffDuty.None,
                "class_1",
                10,
                0,
                160,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.48f, 0.48f, 0.52f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                "\u6570\u5b66\u8001\u5e08",
                "Math Teacher",
                "\u6570\u5b66\u8001\u5e08",
                "MathTeacher",
                "teacher_math",
                CampusCharacterRole.Teacher,
                CampusTeacherDuty.MathTeacher,
                CampusStaffDuty.None,
                "class_1",
                10,
                0,
                160,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.42f, 0.42f, 0.48f, 1f)),
            new CampusRuntimeGameplayActorPreset(
                "\u804c\u5458",
                "Staff",
                "\u804c\u5458",
                "Staff",
                "staff",
                CampusCharacterRole.Staff,
                CampusTeacherDuty.None,
                CampusStaffDuty.None,
                string.Empty,
                10,
                0,
                120,
                new[] { CampusCharacterTrait.Ordinary },
                new Color(0.65f, 0.54f, 0.32f, 1f))
        };

        private static CampusRuntimeGameplayActorPreset[] LoadPresets()
        {
            if (!CampusRuntimeModPresetStore.TryReadJson("GameplayActorPresets.json", out string json))
            {
                return BuiltInPresets;
            }

            try
            {
                GameplayActorPresetFile file = JsonUtility.FromJson<GameplayActorPresetFile>(json);
                if (file == null || file.Presets == null || file.Presets.Count == 0)
                {
                    return BuiltInPresets;
                }

                List<CampusRuntimeGameplayActorPreset> loaded = new List<CampusRuntimeGameplayActorPreset>();
                for (int i = 0; i < file.Presets.Count; i++)
                {
                    GameplayActorPresetRecord record = file.Presets[i];
                    if (record == null)
                    {
                        continue;
                    }

                    string chinese = string.IsNullOrWhiteSpace(record.ChineseLabel) ? record.EnglishLabel : record.ChineseLabel;
                    string english = string.IsNullOrWhiteSpace(record.EnglishLabel) ? record.ChineseLabel : record.EnglishLabel;
                    string actorIdPrefix = string.IsNullOrWhiteSpace(record.ActorIdPrefix)
                        ? NormalizeActorIdPrefix(english)
                        : record.ActorIdPrefix.Trim();
                    if (string.IsNullOrWhiteSpace(chinese) || string.IsNullOrWhiteSpace(english) || string.IsNullOrWhiteSpace(actorIdPrefix))
                    {
                        continue;
                    }

                    loaded.Add(new CampusRuntimeGameplayActorPreset(
                        chinese,
                        english,
                        string.IsNullOrWhiteSpace(record.ChineseDisplayName) ? chinese : record.ChineseDisplayName.Trim(),
                        string.IsNullOrWhiteSpace(record.EnglishDisplayName) ? english : record.EnglishDisplayName.Trim(),
                        actorIdPrefix,
                        ParseEnum(record.Role, CampusCharacterRole.Student),
                        ParseEnum(record.TeacherDuty, CampusTeacherDuty.None),
                        ParseEnum(record.StaffDuty, CampusStaffDuty.None),
                        string.IsNullOrWhiteSpace(record.ClassId) ? string.Empty : record.ClassId.Trim(),
                        record.Sleepiness,
                        record.Mischief,
                        record.InitialMoney,
                        ParseTraits(record.Traits),
                        CampusRuntimeModPresetStore.ParseColor(record.Color, new Color(0.5f, 0.6f, 0.75f, 1f))));
                }

                return loaded.Count > 0 ? loaded.ToArray() : BuiltInPresets;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusRuntimeGameplayActorPresetCatalog] Failed to load GameplayActorPresets.json: " + exception.Message);
                return BuiltInPresets;
            }
        }

        private static CampusCharacterTrait[] ParseTraits(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new[] { CampusCharacterTrait.Ordinary };
            }

            List<CampusCharacterTrait> traits = new List<CampusCharacterTrait>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]) &&
                    Enum.TryParse(values[i].Trim(), true, out CampusCharacterTrait trait))
                {
                    traits.Add(trait);
                }
            }

            return traits.Count > 0 ? traits.ToArray() : new[] { CampusCharacterTrait.Ordinary };
        }

        private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   Enum.TryParse(value.Trim(), true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        private static string NormalizeActorIdPrefix(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
        }

        [Serializable]
        private sealed class GameplayActorPresetFile
        {
            public List<GameplayActorPresetRecord> Presets = new List<GameplayActorPresetRecord>();
        }

        [Serializable]
        private sealed class GameplayActorPresetRecord
        {
            public string ChineseLabel = string.Empty;
            public string EnglishLabel = string.Empty;
            public string ChineseDisplayName = string.Empty;
            public string EnglishDisplayName = string.Empty;
            public string ActorIdPrefix = string.Empty;
            public string Role = string.Empty;
            public string TeacherDuty = string.Empty;
            public string StaffDuty = string.Empty;
            public string ClassId = string.Empty;
            public int Sleepiness = 40;
            public int Mischief = 20;
            public int InitialMoney = NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney;
            public string[] Traits = Array.Empty<string>();
            public string Color = string.Empty;
        }
    }
}
